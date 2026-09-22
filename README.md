# panda-auth-sdk

**PandaAuth by PandaLabs** · [English](README.en.md)

> 研发阶段，尚无正式受支持发行版；接入采用邀请或申请口径。已有实现不等于已完成发行验证。

## 职责与边界

PandaAuth 的 .NET 接入 SDK 仓，依赖同级 [panda-auth-share](https://github.com/PandaLabs2026/panda-auth-share)。产品发行与 SDK 计划采用独立版本节奏。

## 当前实现与限制

当前已提供无状态的 [PandaAuthClient](src/PandaAuth.Sdk/PandaAuthClient.cs) 最小 OIDC 能力：discovery、授权码 + PKCE、client credentials、refresh token、userinfo 和 revoke；userinfo 同时暴露原始 claims 与常用角色数组。SDK 不持久化 state、PKCE verifier、access token 或 refresh token，调用方负责保存、过期处理和安全清理。协议错误通过 [PandaAuthProtocolException](src/PandaAuth.Sdk/PandaAuthProtocolException.cs) 暴露有限错误字段，不回显 client secret。

授权码流程由 `CreateAuthorizationRequestAsync` 生成授权 URL、state 和 PKCE verifier；调用方必须校验回调 state，并把 verifier 传入 `ExchangeCodeAsync`。本阶段不包含 provider adapter、本地 JWT/JWKS 校验、introspection、Web/Maui/桌面封装，也不承诺具体外部 provider。

[项目配置](src/PandaAuth.Sdk/PandaAuth.Sdk.csproj)仍为 `IsPackable=false`。测试位于 [PandaAuth.Sdk.Tests](tests/PandaAuth.Sdk.Tests)，不提供已发布 NuGet 包或包安装命令。

本仓提供一个可执行的 [M2M 示例](samples/PandaAuth.Sdk.M2m/README.md)，演示 discovery + client credentials；示例不输出或保存 Access Token。Web/浏览器示例仍需真实环境验收。

Web、MAUI、桌面、Blazor 等客户端封装与示例按需求推进；不能由协议支持推断全平台 SDK 已交付。现有 Web 示例在 [Server DemoClient](https://github.com/PandaLabs2026/panda-auth-server/tree/main/samples/PandaAuth.DemoClient)，不属于本仓 SDK 验收证据。

## 构建

需要 .NET SDK，版本选择见本仓 [global.json](global.json)（当前请求 10.0.112，允许 latestFeature roll-forward）。七仓按[工作区布局](https://github.com/PandaLabs2026/panda-auth/blob/main/WORKSPACE.md)同级克隆，跨仓链接需要对应访问权限。以下命令在本仓根目录执行；本轮仅静态核对命令，未执行构建或启动。

必须先将 Share 克隆到与本仓同级的位置。

```bash
dotnet build PandaAuth.Sdk.slnx
```

本仓没有独立应用启动入口。本阶段目标为最小接入能力及消费方验证，公开包需另行通过发行门禁。

## Roadmap 与治理

实现目标见[能力矩阵](https://github.com/PandaLabs2026/panda-auth/blob/main/docs/open-source/capabilities.md)与[发布门禁](https://github.com/PandaLabs2026/panda-auth/blob/main/docs/open-source/release-readiness.md)。实际业务需求驱动路线图，社区请求按方向和维护成本评估，不承诺交付。[社区/商业边界](https://github.com/PandaLabs2026/panda-auth/blob/main/docs/open-source/strategy.md)表示能力归属，不代表商业模块已经交付。

- [安全政策](SECURITY.md)：选定私密报告渠道，启用状态未核验；不公开提交漏洞细节。
- [贡献指南](CONTRIBUTING.md)：本仓检查与统一贡献规则。
- [MIT License](LICENSE)：适用于自有代码和文档，具体范围见[许可说明](LICENSING.md)；第三方许可仍适用，品牌图片除外。
