# panda-auth-sdk

PandaAuth 内部业务应用接入 SDK（.NET）——供熊猫实验室旗下全部 .NET 应用对接 PandaAuth（OIDC 登录、令牌管理、userinfo、角色 Claim 读取）。

## 定位

- 消费方：所有内部 .NET 业务应用（NuGet 私有包分发，独立版本节奏）
- 依赖 [panda-auth-share](../panda-auth-share) 的契约常量（须同级克隆）
- **Phase 1 实装**：当前为骨架（端点常量引用 + Options），将提供完整的令牌生命周期封装

## 构建

```bash
dotnet build PandaAuth.Sdk.slnx
```

克隆约定：与 panda-auth-share 等仓同级目录克隆（工作区布局见元仓 WORKSPACE.md）。
