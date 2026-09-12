namespace PandaAuth.Sdk;

/// <summary>
/// 内部业务应用接入 PandaAuth 的客户端选项。
/// Phase 1 实装完整 SDK 客户端（token 管理、userinfo 封装、角色 Claim 读取）。
/// </summary>
public sealed class PandaAuthClientOptions
{
    /// <summary>服务端 Issuer，如 https://auth.pandalabs.cn（国内）或 https://auth.pandalabs.cc（海外）。</summary>
    public string Issuer { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>严格白名单回调地址，禁止通配符。</summary>
    public string RedirectUri { get; set; } = string.Empty;

    public string[] Scopes { get; set; } = ["openid", "profile", "email", "roles", "offline_access"];
}
