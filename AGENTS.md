# panda-auth-sdk 协作规则

## 职责与边界

本仓是 PandaAuth .NET 接入 SDK，提供应用接入所需的客户端抽象；不拥有协议常量、身份服务实现或调用方业务状态。七仓关系与发行治理见 `../panda-auth/WORKSPACE.md` 和 `../panda-auth/AGENTS.md`。

## 跨仓来源与安全

- OIDC 端点、Claim 和共享形状以同级 `../panda-auth-share/` 为准；身份服务能力以 `../panda-auth-server/` 的已实现行为为准。不要复制字面量，也不要把未验收的平台封装写成已支持能力。
- SDK 保持无状态：不替调用方持久化 state、PKCE verifier、access token 或 refresh token；不得回显 client secret。
- 示例不得输出或保存 Access Token。不得提交密码、Token、私钥、env 内容、真实连接串或生产回调配置。
- 本仓无生成资产；不要手改其他仓生成物或将生成物提交为源文件。

## 验证

从本仓根目录运行：

```bash
dotnet build PandaAuth.Sdk.slnx
dotnet test tests/PandaAuth.Sdk.Tests/PandaAuth.Sdk.Tests.csproj
git diff --check
```

solution 与测试项目均存在。跨仓 ProjectReference 依赖同级 `../panda-auth-share/` 检出；缺失时先按 `WORKSPACE.md` 修复七仓同级布局，不把依赖错误记作测试通过。
