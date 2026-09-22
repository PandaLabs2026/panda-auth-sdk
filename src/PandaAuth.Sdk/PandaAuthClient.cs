using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PandaAuth.Sdk;

/// <summary>
/// Stateless OIDC client. The caller owns state, code verifiers and all tokens.
/// </summary>
public sealed class PandaAuthClient
{
    private readonly HttpClient _httpClient;
    private readonly PandaAuthClientOptions _options;

    public PandaAuthClient(HttpClient httpClient, PandaAuthClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ValidateOptions();
    }

    public PandaAuthAuthorizationRequest CreateAuthorizationRequest()
    {
        RequireRedirectUri();
        var state = CreateRandomString(32);
        var verifier = CreateRandomString(64);
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var endpoint = new Uri(_options.Issuer.TrimEnd('/') + "/connect/authorize");

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["scope"] = string.Join(' ', _options.Scopes),
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };

        // The async overload replaces this conventional endpoint with the discovery result.
        return new PandaAuthAuthorizationRequest(AddQuery(endpoint, query), state, verifier);
    }

    public async Task<PandaAuthAuthorizationRequest> CreateAuthorizationRequestAsync(CancellationToken cancellationToken = default)
    {
        var discovery = await GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        var request = CreateAuthorizationRequest();
        if (string.IsNullOrWhiteSpace(discovery.AuthorizationEndpoint))
        {
            throw new InvalidOperationException("OIDC discovery did not provide authorization_endpoint.");
        }

        return request with { Uri = AddQuery(new Uri(discovery.AuthorizationEndpoint), ParseQuery(request.Uri)) };
    }

    public async Task<PandaAuthDiscoveryDocument> GetDiscoveryAsync(CancellationToken cancellationToken = default)
    {
        var discoveryUri = new Uri(new Uri(_options.Issuer.TrimEnd('/') + "/"), ".well-known/openid-configuration");
        using var response = await _httpClient.GetAsync(discoveryUri, cancellationToken).ConfigureAwait(false);
        return await ReadJsonAsync<PandaAuthDiscoveryDocument>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PandaAuthTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        RequireRedirectUri();
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);
        var discovery = await GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        return await PostTokenAsync(discovery.TokenEndpoint, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = _options.RedirectUri
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PandaAuthTokenResponse> GetClientCredentialsTokenAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new InvalidOperationException("Client credentials flow requires a client secret.");
        var discovery = await GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        return await PostTokenAsync(discovery.TokenEndpoint, new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = string.Join(' ', _options.Scopes)
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PandaAuthTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        var discovery = await GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        return await PostTokenAsync(discovery.TokenEndpoint, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PandaAuthUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        var discovery = await GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        var endpoint = RequireEndpoint(discovery.UserInfoEndpoint, "userinfo_endpoint");
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        using var document = await ReadDocumentAsync(response, cancellationToken).ConfigureAwait(false);
        var root = document.RootElement.Clone();
        var subject = root.TryGetProperty("sub", out var sub) ? sub.GetString() : null;
        if (string.IsNullOrWhiteSpace(subject)) throw new InvalidOperationException("userinfo response did not provide sub.");
        return new PandaAuthUserInfo(subject, GetString(root, "name"), GetString(root, "email"), GetStringArray(root, "roles"), root);
    }

    public async Task RevokeAsync(string token, string? tokenTypeHint = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var discovery = await GetDiscoveryAsync(cancellationToken).ConfigureAwait(false);
        var endpoint = RequireEndpoint(discovery.RevocationEndpoint, "revocation_endpoint");
        var values = new Dictionary<string, string> { ["token"] = token };
        if (!string.IsNullOrWhiteSpace(tokenTypeHint)) values["token_type_hint"] = tokenTypeHint;
        using var response = await _httpClient.PostAsync(endpoint, new FormUrlEncodedContent(AddClientCredentials(values)), cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PandaAuthTokenResponse> PostTokenAsync(string? endpoint, Dictionary<string, string> values, CancellationToken cancellationToken)
    {
        var tokenEndpoint = RequireEndpoint(endpoint, "token_endpoint");
        using var response = await _httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(AddClientCredentials(values)), cancellationToken).ConfigureAwait(false);
        return await ReadJsonAsync<PandaAuthTokenResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private Dictionary<string, string> AddClientCredentials(Dictionary<string, string> values)
    {
        values["client_id"] = _options.ClientId;
        if (!string.IsNullOrWhiteSpace(_options.ClientSecret)) values["client_secret"] = _options.ClientSecret;
        return values;
    }

    private async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return value ?? throw new InvalidOperationException("OIDC endpoint returned an empty JSON response.");
    }

    private static async Task<JsonDocument> ReadDocumentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string? error = null, description = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            error = document.RootElement.TryGetProperty("error", out var e) ? e.GetString() : null;
            description = document.RootElement.TryGetProperty("error_description", out var d) ? d.GetString() : null;
        }
        catch (JsonException) { }
        throw new PandaAuthProtocolException(response.StatusCode, error, description);
    }

    private void ValidateOptions()
    {
        if (!Uri.TryCreate(_options.Issuer, UriKind.Absolute, out var issuer) || issuer.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Issuer must be an absolute HTTPS URI.", nameof(_options));
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.ClientId);
    }

    private void RequireRedirectUri()
    {
        if (!Uri.TryCreate(_options.RedirectUri, UriKind.Absolute, out var redirect) || redirect.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("RedirectUri must be an absolute HTTPS URI for the authorization-code flow.", nameof(_options));
    }

    private static string RequireEndpoint(string? endpoint, string name)
        => !string.IsNullOrWhiteSpace(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            ? uri.ToString() : throw new InvalidOperationException($"OIDC discovery did not provide {name}.");

    private static string? GetString(JsonElement root, string name) => root.TryGetProperty(name, out var value) ? value.GetString() : null;
    private static IReadOnlyList<string> GetStringArray(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray()
            : [];

    private static Uri AddQuery(Uri endpoint, IEnumerable<KeyValuePair<string, string>> values)
        => new(endpoint + (endpoint.Query.Length == 0 ? "?" : "&") + string.Join('&', values.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}")));

    private static IReadOnlyDictionary<string, string> ParseQuery(Uri uri)
        => uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2)).ToDictionary(x => Uri.UnescapeDataString(x[0]), x => x.Length == 1 ? string.Empty : Uri.UnescapeDataString(x[1]));

    private static string CreateRandomString(int bytes)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Base64Url(buffer);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
