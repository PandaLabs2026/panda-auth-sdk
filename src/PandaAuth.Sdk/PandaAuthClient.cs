using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PandaAuth.Shared;

namespace PandaAuth.Sdk;

/// <summary>
/// Stateless OIDC client. The caller owns state, code verifiers and all tokens.
/// </summary>
public sealed class PandaAuthClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly PandaAuthClientOptions _options;

    private readonly bool _networkAllowed;
    private readonly bool _ownsHttpClient;

    // Kept for source compatibility with URL-only builders. An opaque HttpClient
    // cannot prove that automatic redirects are disabled.
    public PandaAuthClient(HttpClient httpClient, PandaAuthClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = Snapshot(options);
        ValidateOptions();
    }

    public PandaAuthClient(PandaAuthClientOptions options)
        : this(new HttpClientHandler { AllowAutoRedirect = false }, options) { }

    /// <summary>The application trusts custom transport handlers; they must not redirect or retry requests.</summary>
    public PandaAuthClient(HttpMessageHandler handler, PandaAuthClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(handler);
        switch (handler)
        {
            case HttpClientHandler native: native.AllowAutoRedirect = false; break;
            case SocketsHttpHandler sockets: sockets.AllowAutoRedirect = false; break;
            case DelegatingHandler: throw new ArgumentException("Use a direct transport without redirecting delegates.", nameof(handler));
        }
        _options = Snapshot(options);
        ValidateOptions();
        _httpClient = new HttpClient(handler, disposeHandler: true);
        _networkAllowed = true;
        _ownsHttpClient = true;
    }

    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private static PandaAuthClientOptions Snapshot(PandaAuthClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new PandaAuthClientOptions {
            Issuer = options.Issuer, ClientId = options.ClientId, ClientSecret = options.ClientSecret,
            RedirectUri = options.RedirectUri, Scopes = options.Scopes.ToArray()
        };
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
        if (!_networkAllowed)
            throw new InvalidOperationException("Network operations require the options-only or direct-handler constructor; opaque HttpClient redirects cannot be verified.");
        var discoveryUri = new Uri(new Uri(_options.Issuer.TrimEnd('/') + "/"), ".well-known/openid-configuration");
        using var response = await _httpClient.GetAsync(discoveryUri, cancellationToken).ConfigureAwait(false);
        using var document = await ReadDocumentAsync(response, cancellationToken).ConfigureAwait(false);
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (document.RootElement.ValueKind != JsonValueKind.Object ||
            document.RootElement.EnumerateObject().Any(property => !names.Add(property.Name)))
            throw new InvalidOperationException("OIDC discovery must be an object without duplicate properties.");
        var metadata = document.RootElement.Deserialize<PandaAuthDiscoveryDocument>()
            ?? throw new InvalidOperationException("OIDC discovery is empty.");
        if (!string.Equals(metadata.Issuer, _options.Issuer, StringComparison.Ordinal))
            throw new InvalidOperationException("OIDC discovery issuer does not exactly match the configured issuer.");
        foreach (var endpoint in new[] { metadata.AuthorizationEndpoint, metadata.TokenEndpoint,
            metadata.UserInfoEndpoint, metadata.RevocationEndpoint, metadata.IntrospectionEndpoint })
            if (endpoint is not null) RequireEndpoint(endpoint, "endpoint");
        return metadata;
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
        // The IDP emits "role"; retain the existing plural alias for callers
        // using older/custom userinfo payloads. One role can be a JSON string.
        var roles = GetStringArray(root, "role").Concat(GetStringArray(root, "roles"))
            .Distinct(StringComparer.Ordinal).ToArray();
        return new PandaAuthUserInfo(subject, GetString(root, "name"), GetString(root, "email"), roles, root);
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

    /// <summary>
    /// Builds the RP-initiated logout URL. The caller owns navigation and local-cookie
    /// sign-out; this method only constructs the protocol request.
    /// </summary>
    public Uri CreateEndSessionUrl(
        string? idTokenHint = null,
        string? postLogoutRedirectUri = null,
        string? state = null)
    {
        var endpoint = new Uri(_options.Issuer.TrimEnd('/') + PandaAuthEndpoints.Logout);
        var query = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(idTokenHint)) query["id_token_hint"] = idTokenHint;
        if (!string.IsNullOrWhiteSpace(postLogoutRedirectUri)) query["post_logout_redirect_uri"] = postLogoutRedirectUri;
        if (!string.IsNullOrWhiteSpace(state)) query["state"] = state;
        return query.Count == 0 ? endpoint : AddQuery(endpoint, query);
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
        if (!IsHttpsEndpoint(_options.Issuer, out var issuer) || issuer!.Query.Length != 0)
            throw new ArgumentException("Issuer must be an absolute HTTPS URI.", nameof(_options));
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.ClientId);
    }

    private void RequireRedirectUri()
    {
        if (!Uri.TryCreate(_options.RedirectUri, UriKind.Absolute, out var redirect) || redirect.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("RedirectUri must be an absolute HTTPS URI for the authorization-code flow.", nameof(_options));
    }

    private static bool IsHttpsEndpoint(string? endpoint, out Uri? uri)
    {
        uri = null;
        return !string.IsNullOrWhiteSpace(endpoint) && !endpoint.Any(char.IsWhiteSpace) &&
            !endpoint.Any(char.IsControl) && Uri.TryCreate(endpoint, UriKind.Absolute, out uri) &&
            uri.IsWellFormedOriginalString() && uri.Scheme == Uri.UriSchemeHttps &&
            uri.Host.Length != 0 && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
    }

    private static string RequireEndpoint(string? endpoint, string name)
        => IsHttpsEndpoint(endpoint, out _) ? endpoint!
            : throw new InvalidOperationException($"OIDC discovery did not provide a safe HTTPS {name}.");

    private static string? GetString(JsonElement root, string name) => root.TryGetProperty(name, out var value) ? value.GetString() : null;
    private static IReadOnlyList<string> GetStringArray(JsonElement root, string name)
        => !root.TryGetProperty(name, out var value) ? [] : value.ValueKind switch
        {
            JsonValueKind.String => [value.GetString()!],
            JsonValueKind.Array => value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray(),
            _ => [],
        };

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
