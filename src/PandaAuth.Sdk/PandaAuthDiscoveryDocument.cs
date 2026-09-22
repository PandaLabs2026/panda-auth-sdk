using System.Text.Json.Serialization;

namespace PandaAuth.Sdk;

public sealed record PandaAuthDiscoveryDocument(
    [property: JsonPropertyName("authorization_endpoint")] string? AuthorizationEndpoint,
    [property: JsonPropertyName("token_endpoint")] string? TokenEndpoint,
    [property: JsonPropertyName("userinfo_endpoint")] string? UserInfoEndpoint,
    [property: JsonPropertyName("revocation_endpoint")] string? RevocationEndpoint,
    [property: JsonPropertyName("introspection_endpoint")] string? IntrospectionEndpoint);

public sealed record PandaAuthAuthorizationRequest(Uri Uri, string State, string CodeVerifier);

public sealed record PandaAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("scope")] string? Scope,
    [property: JsonPropertyName("id_token")] string? IdToken);

public sealed record PandaAuthUserInfo(
    string Subject,
    string? Name,
    string? Email,
    IReadOnlyList<string> Roles,
    System.Text.Json.JsonElement Claims);
