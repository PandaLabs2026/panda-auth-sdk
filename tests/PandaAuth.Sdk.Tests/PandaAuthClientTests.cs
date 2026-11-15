using System.Net;
using System.Net.Http.Headers;
using System.Text;
using PandaAuth.Sdk;
using Xunit;

namespace PandaAuth.Sdk.Tests;

public sealed class PandaAuthClientTests
{
    private static PandaAuthClient CreateClient(FakeHttpMessageHandler handler)
    {
        return new PandaAuthClient(handler, new PandaAuthClientOptions
        {
            Issuer = "https://issuer.example",
            ClientId = "web-client",
            ClientSecret = "secret",
            RedirectUri = "https://app.example/callback"
        });
    }

    [Fact]
    public void CreateAuthorizationRequestUsesPkceAndCallerOwnedState()
    {
        using var handler = new FakeHttpMessageHandler();
        var client = CreateClient(handler);

        var request = client.CreateAuthorizationRequest();

        Assert.Equal("https", request.Uri.Scheme);
        Assert.Contains("response_type=code", request.Uri.Query);
        Assert.Contains("client_id=web-client", request.Uri.Query);
        Assert.Contains("code_challenge_method=S256", request.Uri.Query);
        Assert.Contains("code_challenge=", request.Uri.Query);
        Assert.NotEmpty(request.State);
        Assert.NotEmpty(request.CodeVerifier);
        Assert.Contains("state=" + Uri.EscapeDataString(request.State), request.Uri.Query);
    }

    [Fact]
    public void EndSessionUrlCarriesOnlyCallerSuppliedParameters()
    {
        using var handler = new FakeHttpMessageHandler();
        var client = CreateClient(handler);

        var url = client.CreateEndSessionUrl(
            idTokenHint: "id-token-1",
            postLogoutRedirectUri: "https://app.example/logout",
            state: "state-1");

        Assert.Equal("https", url.Scheme);
        Assert.Equal("/connect/logout", url.AbsolutePath);
        Assert.Contains("id_token_hint=id-token-1", url.Query);
        Assert.Contains("post_logout_redirect_uri=https%3A%2F%2Fapp.example%2Flogout", url.Query);
        Assert.Contains("state=state-1", url.Query);
        Assert.Equal("https://issuer.example/connect/logout", client.CreateEndSessionUrl().ToString());
    }

    [Fact]
    public async Task ExchangeCodeDiscoversEndpointAndSendsPkceVerifier()
    {
        using var handler = new FakeHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/.well-known/openid-configuration")
            {
                return Json("""{"issuer":"https://issuer.example","token_endpoint":"https://issuer.example/connect/token","userinfo_endpoint":"https://issuer.example/connect/userinfo","revocation_endpoint":"https://issuer.example/connect/revoke"}""");
            }

            Assert.Equal("/connect/token", request.RequestUri.AbsolutePath);
            Assert.Equal(HttpMethod.Post, request.Method);
            var form = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("grant_type=authorization_code", form);
            Assert.Contains("code_verifier=verifier-1", form);
            return Json("""{"access_token":"access-1","token_type":"Bearer","expires_in":300,"refresh_token":"refresh-1","scope":"openid profile"}""");
        });
        var client = CreateClient(handler);

        var token = await client.ExchangeCodeAsync("code-1", "verifier-1");

        Assert.Equal("access-1", token.AccessToken);
        Assert.Equal("refresh-1", token.RefreshToken);
        Assert.Equal(300, token.ExpiresIn);
    }

    [Fact]
    public async Task UserInfoReturnsUntypedClaimsAndUsesBearerToken()
    {
        using var handler = new FakeHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/.well-known/openid-configuration")
            {
                return Json("""{"issuer":"https://issuer.example","userinfo_endpoint":"https://issuer.example/connect/userinfo"}""");
            }

            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("access-1", request.Headers.Authorization.Parameter);
            return Json("""{"sub":"user-1","name":"Panda","roles":["admin","operator"]}""");
        });
        var client = CreateClient(handler);

        var userInfo = await client.GetUserInfoAsync("access-1");

        Assert.Equal("user-1", userInfo.Subject);
        Assert.Equal("Panda", userInfo.Name);
        Assert.Equal(["admin", "operator"], userInfo.Roles);
        Assert.Equal("user-1", userInfo.Claims.GetProperty("sub").GetString());
    }

    [Theory]
    [InlineData("""{"sub":"user-1","role":"admin"}""", new string[] { "admin" })]
    [InlineData("""{"sub":"user-1","role":["admin","operator"]}""", new string[] { "admin", "operator" })]
    [InlineData("""{"sub":"user-1","role":["admin","operator"],"roles":["operator"]}""", new string[] { "admin", "operator" })]
    [InlineData("""{"sub":"user-1","roles":"admin"}""", new string[] { "admin" })]
    public async Task UserInfoProjectsProviderRoleClaimAndLegacyRoles(string payload, string[] expected)
    {
        using var handler = new FakeHttpMessageHandler((request, _) =>
            request.RequestUri!.AbsolutePath == "/.well-known/openid-configuration"
                ? Json("""{"issuer":"https://issuer.example","userinfo_endpoint":"https://issuer.example/connect/userinfo"}""")
                : Json(payload));
        var info = await CreateClient(handler).GetUserInfoAsync("fixture-access-token");
        Assert.Equal(expected, info.Roles);
    }

    [Fact]
    public async Task ProtocolFailureIncludesSafeErrorDetails()
    {
        using var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"invalid_grant\",\"error_description\":\"code expired\"}", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var error = await Assert.ThrowsAsync<PandaAuthProtocolException>(() => client.RefreshTokenAsync("refresh-1"));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal("invalid_grant", error.Error);
        Assert.Equal("code expired", error.ErrorDescription);
        Assert.DoesNotContain("secret", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClientCredentialsAndRevokeUseDiscoveredEndpoints()
    {
        var calls = new List<string>();
        using var handler = new FakeHttpMessageHandler((request, _) =>
        {
            calls.Add(request.RequestUri!.AbsolutePath);
            if (request.RequestUri.AbsolutePath == "/.well-known/openid-configuration")
            {
                return Json("""{"issuer":"https://issuer.example","token_endpoint":"https://issuer.example/connect/token","revocation_endpoint":"https://issuer.example/connect/revoke"}""");
            }

            if (request.RequestUri.AbsolutePath == "/connect/token")
            {
                return Json("""{"access_token":"service-access","token_type":"Bearer","expires_in":600}""");
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(handler);

        var token = await client.GetClientCredentialsTokenAsync();
        await client.RevokeAsync(token.AccessToken, "access_token");

        Assert.Equal("service-access", token.AccessToken);
        Assert.Equal(["/.well-known/openid-configuration", "/connect/token", "/.well-known/openid-configuration", "/connect/revoke"], calls);
    }

    [Fact]
    public async Task ClientCredentialsDoesNotRequireRedirectUri()
    {
        using var handler = new FakeHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/.well-known/openid-configuration")
                return Json("""{"issuer":"https://issuer.example","token_endpoint":"https://issuer.example/connect/token"}""");

            return Json("""{"access_token":"service-access","token_type":"Bearer","expires_in":600}""");
        });
        var client = new PandaAuthClient(handler, new PandaAuthClientOptions
        {
            Issuer = "https://issuer.example",
            ClientId = "service-client",
            ClientSecret = "secret",
            Scopes = ["api"]
        });

        var token = await client.GetClientCredentialsTokenAsync();

        Assert.Equal("service-access", token.AccessToken);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"issuer\":\"https://attacker.example\",\"token_endpoint\":\"https://attacker.example/token\"}")]
    [InlineData("{\"issuer\":\"https://issuer.example/\",\"token_endpoint\":\"https://issuer.example/token\"}")]
    [InlineData("{\"issuer\":\"https://issuer.example\",\"token_endpoint\":\"http://issuer.example/token\"}")]
    [InlineData("{\"issuer\":\"https://issuer.example\",\"token_endpoint\":\"/token\"}")]
    [InlineData("{\"issuer\":\"https://issuer.example\",\"token_endpoint\":\"https://user:pass@issuer.example/token\"}")]
    [InlineData("{\"issuer\":\"https://issuer.example\",\"token_endpoint\":\"https://issuer.example/token#fragment\"}")]
    [InlineData("{\"issuer\":\"https://issuer.example\",\"token_endpoint\":\"https://issuer.example/token\",\"userinfo_endpoint\":\"http://unsafe.example/info\"}")]
    public async Task InvalidMetadataNeverSendsCredentials(string metadata)
    {
        var requests = new List<string>();
        using var handler = new FakeHttpMessageHandler((request, _) => {
            requests.Add(request.RequestUri!.AbsoluteUri);
            return request.RequestUri.AbsolutePath == "/.well-known/openid-configuration"
                ? Json(metadata) : Json("{\"access_token\":\"unexpected\",\"token_type\":\"Bearer\"}");
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateClient(handler).GetClientCredentialsTokenAsync());
        Assert.Single(requests);
    }

    [Fact]
    public async Task TrustedIssuerCanDelegateTokenEndpointToAnotherHttpsHost()
    {
        using var handler = new FakeHttpMessageHandler((request, _) => {
            if (request.RequestUri!.AbsolutePath == "/.well-known/openid-configuration")
                return Json("""{"issuer":"https://issuer.example","token_endpoint":"https://tokens.example/exchange"}""");
            Assert.Equal("tokens.example", request.RequestUri.Host);
            return Json("""{"access_token":"valid","token_type":"Bearer"}""");
        });
        Assert.Equal("valid", (await CreateClient(handler).GetClientCredentialsTokenAsync()).AccessToken);
    }

    [Fact]
    public async Task OpaqueHttpClientFailsBeforeAnyNetworkRequest()
    {
        var calls = 0;
        using var handler = new FakeHttpMessageHandler((_, _) => { calls++; return Json("{}"); });
        using var http = new HttpClient(handler);
        using var client = new PandaAuthClient(http, new PandaAuthClientOptions { Issuer = "https://issuer.example", ClientId = "fixture", ClientSecret = "fixture-secret" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetClientCredentialsTokenAsync());
        Assert.Equal(0, calls);
        Assert.Equal("/connect/logout", client.CreateEndSessionUrl().AbsolutePath);
    }

    [Fact]
    public async Task CallerCannotChangeTrustedIssuerAfterConstruction()
    {
        using var handler = new FakeHttpMessageHandler((request, _) => {
            Assert.Equal("issuer.example", request.RequestUri!.Host);
            return Json("""{"issuer":"https://issuer.example"}""");
        });
        var options = new PandaAuthClientOptions { Issuer = "https://issuer.example", ClientId = "fixture" };
        using var client = new PandaAuthClient(handler, options);
        options.Issuer = "https://attacker.example";
        Assert.Equal("https://issuer.example", (await client.GetDiscoveryAsync()).Issuer);
    }

    [Theory]
    [InlineData("https://issuer.example/?query=yes")]
    [InlineData("https://issuer.example/#fragment")]
    [InlineData("https://user@issuer.example/")]
    [InlineData(" https://issuer.example")]
    [InlineData("http://issuer.example")]
    public void RejectsAmbiguousIssuerConfiguration(string issuer)
    {
        using var handler = new FakeHttpMessageHandler();
        Assert.Throws<ArgumentException>(() => new PandaAuthClient(handler, new PandaAuthClientOptions { Issuer = issuer, ClientId = "fixture" }));
    }

    [Theory]
    [InlineData("code")]
    [InlineData("refresh")]
    [InlineData("userinfo")]
    [InlineData("revoke")]
    public async Task WrongIssuerBlocksEveryCredentialBearingOperation(string operation)
    {
        var calls = 0;
        using var handler = new FakeHttpMessageHandler((_, _) => {
            calls++;
            return Json("""{"issuer":"https://attacker.example","token_endpoint":"https://attacker.example/token","userinfo_endpoint":"https://attacker.example/info","revocation_endpoint":"https://attacker.example/revoke"}""");
        });
        var client = CreateClient(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            switch (operation) {
                case "code": await client.ExchangeCodeAsync("fixture-code", "fixture-verifier"); break;
                case "refresh": await client.RefreshTokenAsync("fixture-refresh"); break;
                case "userinfo": await client.GetUserInfoAsync("fixture-access"); break;
                default: await client.RevokeAsync("fixture-token"); break;
            }
        });
        Assert.Equal(1, calls);
    }

    private static HttpResponseMessage Json(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage>? responder = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder?.Invoke(request, cancellationToken) ?? Json("{}"));
    }
}
