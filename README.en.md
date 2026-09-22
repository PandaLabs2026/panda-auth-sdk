# panda-auth-sdk

**PandaAuth by PandaLabs** · [简体中文](README.md)

> In development; no formally supported release yet. Access is by invitation or request. Implementation does not imply a verified release.

## Responsibility and boundaries

The .NET integration SDK repository for PandaAuth, depending on sibling [panda-auth-share](https://github.com/PandaLabs2026/panda-auth-share). The SDK is intended to version independently of product releases.

## Current implementation and limitations

The stateless [PandaAuthClient](src/PandaAuth.Sdk/PandaAuthClient.cs) provides the minimum OIDC capabilities: discovery, authorization code + PKCE, client credentials, refresh token, userinfo and revoke. It does not persist state, PKCE verifiers, access tokens or refresh tokens. The [project](src/PandaAuth.Sdk/PandaAuth.Sdk.csproj) can generate the `0.2.0-preview.1` prerelease package, which includes this README and the MIT license text; it has not been published to NuGet and is not a 1.0 support commitment.

Web, MAUI, desktop and Blazor wrappers/samples will follow actual needs. Protocol support does not imply a complete cross-platform SDK. The existing [Server DemoClient](https://github.com/PandaLabs2026/panda-auth-server/tree/main/samples/PandaAuth.DemoClient) is not evidence that this SDK has passed acceptance testing.

## Build

Use the .NET SDK selected by [global.json](global.json) (currently 10.0.112 with latestFeature roll-forward). This repository can be built without the private PandaAuth coordination repository; clone the public Share repository beside it:

```bash
git clone https://github.com/PandaLabs2026/panda-auth-sdk.git
git clone https://github.com/PandaLabs2026/panda-auth-share.git
cd panda-auth-sdk
```

Run the commands below from the SDK repository root:

```bash
dotnet build PandaAuth.Sdk.slnx
```

This repository has no standalone application entry point. The current phase targets minimal integration and consumer validation; public packaging requires separate release acceptance.

## Roadmap and governance

Product roadmap, release gates and community/commercial boundaries remain maintainer-governed until a formal public release. This README documents only the independently reproducible SDK build and integration boundary; it does not make private coordination documents a build dependency.

- [Security](SECURITY.md): selected private reporting channel, enablement unverified; no public vulnerability details.
- [Contributing](CONTRIBUTING.md): repository-specific checks and the shared contribution policy.
- [MIT License](LICENSE) for project-owned code/documentation, subject to [license scope](LICENSING.md); third-party terms remain applicable and brand images are excluded.
