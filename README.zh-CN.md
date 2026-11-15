# panda-auth-sdk

**PandaAuth by PandaLabs** · [English](README.md)

**官方主页 [pandalabs.cc](https://pandalabs.cc/products/panda-auth/)** · 中文官网 [pandalabs.cn](https://pandalabs.cn/products/panda-auth/) · [PandaLabs 产品矩阵](https://pandalabs.cc/products/)

> PandaAuth 套件当前为 **Community Preview 0.2.0-preview.1**：已部署生产、面向早期社区试用；稳定版 Community Release 1.0.0 尚未发布。接入采用邀请或申请口径。

## 职责与边界

PandaAuth 的 .NET 接入 SDK 仓，依赖同级 [panda-auth-share](https://github.com/PandaLabs2026/panda-auth-share)。产品发行与 SDK 计划采用独立版本节奏。

## 当前实现与限制

当前已提供无状态的 [PandaAuthClient](src/PandaAuth.Sdk/PandaAuthClient.cs) 最小 OIDC 能力：discovery、授权码 + PKCE、client credentials、refresh token、userinfo 和 revoke；userinfo 同时暴露原始 claims 与常用角色数组。SDK 不持久化 state、PKCE verifier、access token 或 refresh token，调用方负责保存、过期处理和安全清理。协议错误通过 [PandaAuthProtocolException](src/PandaAuth.Sdk/PandaAuthProtocolException.cs) 暴露有限错误字段，不回显 client secret。

授权码流程由 `CreateAuthorizationRequestAsync` 生成授权 URL、state 和 PKCE verifier；调用方必须校验回调 state，并把 verifier 传入 `ExchangeCodeAsync`。本阶段不包含 provider adapter、本地 JWT/JWKS 校验、introspection、Web/Maui/桌面封装，也不承诺具体外部 provider。

[项目配置](src/PandaAuth.Sdk/PandaAuth.Sdk.csproj)已发布 **1.0.0** 正式包（包内包含本 README、根目录 MIT 许可证正文和许可证元数据），经 GitHub Release 分发，构成 1.0 支持承诺（组件版本承诺，独立于套件对外版本）。测试位于 [PandaAuth.Sdk.Tests](tests/PandaAuth.Sdk.Tests)。

本仓提供一个可执行的 [M2M 示例](samples/PandaAuth.Sdk.M2m/README.md)，演示 discovery + client credentials；示例不输出或保存 Access Token。Web/浏览器示例仍需真实环境验收。

Web、MAUI、桌面、Blazor 等客户端封装与示例按需求推进；不能由协议支持推断全平台 SDK 已交付。现有 Web 示例在 [Server DemoClient](https://github.com/PandaLabs2026/panda-auth-server/tree/main/samples/PandaAuth.DemoClient)，不属于本仓 SDK 验收证据。

## 构建

需要 .NET SDK，版本选择见本仓 [global.json](global.json)（当前请求 10.0.112，允许 latestFeature roll-forward）。本仓可以脱离 PandaAuth 私有元仓构建；只需将公开的 Share 仓与本仓同级克隆：

```bash
git clone https://github.com/PandaLabs2026/panda-auth-sdk.git
git clone https://github.com/PandaLabs2026/panda-auth-share.git
cd panda-auth-sdk
```

以下命令在 SDK 仓根目录执行：

```bash
dotnet build PandaAuth.Sdk.slnx
```

本仓没有独立应用启动入口。本阶段目标为最小接入能力及消费方验证。实际包发布仍需通过发行门禁、兼容矩阵冻结和非维护者消费验收。

## Roadmap 与治理

产品级路线图、发行门禁和社区/商业边界在正式公开发行前仍由维护者治理；本仓 README 只描述可独立复现的 SDK 构建与接入边界，不把未公开的内部文档作为构建依赖。

- [安全政策](SECURITY.md)：选定私密报告渠道，启用状态未核验；不公开提交漏洞细节。
- [贡献指南](CONTRIBUTING.md)：本仓检查与统一贡献规则。
- [MIT License](LICENSE)：适用于自有代码和文档，具体范围见[许可说明](LICENSING.md)；第三方许可仍适用，品牌图片除外。

## Discovery 信任与 2.0 迁移

源码候选为 SDK 2.0.0 / Share 1.0.1，尚未正式供包或部署。以前分发的 1.0.0 只代表历史版本证据。

网络调用使用 `using var client = new PandaAuthClient(options)`，默认保留 TLS 证书验证并禁用自动重定向。可注入应用信任的直接 `HttpMessageHandler`：SDK 为原生 handler 关闭重定向、拒绝委托链并负责释放 handler；自定义传输不得重定向或重试携密请求。

原 `(HttpClient, options)` 构造保留给同步 URL 构造（含 BFF logout），其网络调用现在在发出任何请求前拒绝，因为无法验证不透明 HttpClient 的重定向策略。网络消费者须迁移到安全构造。配置在构造时冻结。

Discovery 的 `issuer` 必须与唯一配置逐字符精确一致，包含尾斜线、大小写与路径。所有已声明端点必须是无 userinfo/fragment/空白的绝对 HTTPS URI；缺少可选端点在使用时拒绝。可信 issuer 可以声明不同 HTTPS 主机的端点。元数据校验失败不会发送授权码、verifier、secret、刷新/访问或吊销令牌。重定向响应作为错误，不跟随。主机迁移别名不代表 issuer 等价。

SDK 不验证 ID Token 签名/audience/nonce，也不拥有浏览器 state/session；完整 OIDC 登录处理器继续负责这些检查。SDK 与 Share 独立版本，不表示本批已经发布或全组织采用。
