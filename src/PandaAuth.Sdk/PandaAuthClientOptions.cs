namespace PandaAuth.Sdk;

/// <summary>
/// 内部业务应用接入 PandaAuth 的客户端选项。
/// Phase 1 实装完整 SDK 客户端（token 管理、userinfo 封装、角色 Claim 读取）。
/// </summary>
public sealed class PandaAuthClientOptions
{
    /// <summary>服务端 Issuer，单域名 https://auth.pandalabs.cc（2026-09-16 域名单一化定案）。</summary>
    public string Issuer { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>严格白名单回调地址，禁止通配符。</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// 申请的 scope。**默认已含 <c>roles</c>**：服务端按最小披露原则**只在客户端申请了
    /// <c>roles</c> 时才把用户角色写入 Access Token**，并只在此时于 userinfo 返回角色。
    /// 若资源服务器需要从 AT（或其内省结果）读取角色，请勿从本数组移除 <c>roles</c>。
    /// </summary>
    public string[] Scopes { get; set; } = ["openid", "profile", "email", "roles", "offline_access"];
}
