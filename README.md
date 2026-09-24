# panda-auth-sdk

**PandaAuth by PandaLabs** · [English](README.en.md)

> 研发阶段，尚无正式受支持发行版；接入采用邀请或申请口径。已有实现不等于已完成发行验证。

## 职责与边界

PandaAuth 的 .NET 接入 SDK 仓，依赖同级 [panda-auth-share](https://github.com/PandaLabs2026/panda-auth-share)。产品发行与 SDK 计划采用独立版本节奏。

## 当前实现与限制

当前已提供无状态的 [PandaAuthClient](src/PandaAuth.Sdk/PandaAuthClient.cs) 最小 OIDC 能力：discovery、授权码 + PKCE、client credentials、refresh token、userinfo 和 revoke；userinfo 同时暴露原始 claims 与常用角色数组。SDK 不持久化 state、PKCE verifier、access token 或 refresh token，调用方负责保存、过期处理和安全清理。协议错误通过 [PandaAuthProtocolException](src/PandaAuth.Sdk/PandaAuthProtocolException.cs) 暴露有限错误字段，不回显 client secret。

授权码流程由 `CreateAuthorizationRequestAsync` 生成授权 URL、state 和 PKCE verifier；调用方必须校验回调 state，并把 verifier 传入 `ExchangeCodeAsync`。本阶段不包含 provider adapter、本地 JWT/JWKS 校验、introspection、Web/Maui/桌面封装，也不承诺具体外部 provider。

[项目配置](src/PandaAuth.Sdk/PandaAuth.Sdk.csproj)已发布 **1.0.0** 正式包（包内包含本 README、根目录 MIT 许可证正文和许可证元数据），经 GitHub Release 分发，构成 1.0 支持承诺。测试位于 [PandaAuth.Sdk.Tests](tests/PandaAuth.Sdk.Tests)。

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
