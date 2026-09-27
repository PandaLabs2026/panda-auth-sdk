# panda-auth-sdk 协作规则

PandaAuth .NET 接入 SDK。跨仓布局和发行门禁见 `../panda-auth/WORKSPACE.md`；共享契约以同级 `../panda-auth-share/` 为准。

- SDK 保持无状态：不替调用方持久化 state、PKCE verifier、access token 或 refresh token；不得回显 client secret。
- 协议/端点形状先核对 `panda-auth-share`，不要复制字面量或把未验收的平台封装写成已支持能力。
- 示例不得输出或保存 Access Token；不提交密钥、Token、私钥、env 内容或真实回调地址。
- 验证：`dotnet build PandaAuth.Sdk.slnx`、`dotnet test tests/PandaAuth.Sdk.Tests/PandaAuth.Sdk.Tests.csproj`（如存在）和 `git diff --check`。
