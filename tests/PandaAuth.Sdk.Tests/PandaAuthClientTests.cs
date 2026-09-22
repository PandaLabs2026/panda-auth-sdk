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
        var httpClient = new HttpClient(handler);
        return new PandaAuthClient(httpClient, new PandaAuthClientOptions
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
    public async Task ExchangeCodeDiscoversEndpointAndSendsPkceVerifier()
    {
        using var handler = new FakeHttpMessageHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/.well-known/openid-configuration")
            {
                return Json("""{"token_endpoint":"https://issuer.example/connect/token","userinfo_endpoint":"https://issuer.example/connect/userinfo","revocation_endpoint":"https://issuer.example/connect/revoke"}""");
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
                return Json("""{"userinfo_endpoint":"https://issuer.example/connect/userinfo"}""");
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
                return Json("""{"token_endpoint":"https://issuer.example/connect/token","revocation_endpoint":"https://issuer.example/connect/revoke"}""");
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
                return Json("""{"token_endpoint":"https://issuer.example/connect/token"}""");

            return Json("""{"access_token":"service-access","token_type":"Bearer","expires_in":600}""");
        });
        var client = new PandaAuthClient(new HttpClient(handler), new PandaAuthClientOptions
        {
            Issuer = "https://issuer.example",
            ClientId = "service-client",
            ClientSecret = "secret",
            Scopes = ["api"]
        });

        var token = await client.GetClientCredentialsTokenAsync();

        Assert.Equal("service-access", token.AccessToken);
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
