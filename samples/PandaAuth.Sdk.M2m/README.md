# PandaAuth SDK M2M sample

This sample demonstrates discovery followed by the client-credentials flow. It does not store or print the access token.

```bash
export PANDA_AUTH_ISSUER=https://auth.pandalabs.cn
export PANDA_AUTH_CLIENT_ID=your-client-id
export PANDA_AUTH_CLIENT_SECRET=your-client-secret
dotnet run --project samples/PandaAuth.Sdk.M2m/PandaAuth.Sdk.M2m.csproj
```

The client must be provisioned with the `client_credentials` grant and the requested `api` scope. Never put the secret in source control or shared shell history.
