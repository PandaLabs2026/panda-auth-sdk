# panda-auth-sdk

**PandaAuth by PandaLabs** · [简体中文](README.md)

> In development; no formally supported release yet. Access is by invitation or request. Implementation does not imply a verified release.

## Responsibility and boundaries

The .NET integration SDK repository for PandaAuth, depending on sibling [panda-auth-share](https://github.com/PandaLabs2026/panda-auth-share). The SDK is intended to version independently of product releases.

## Current implementation and limitations

Only [PandaAuthClientOptions](src/PandaAuth.Sdk/PandaAuthClientOptions.cs) and the shared-contract reference exist. The [project](src/PandaAuth.Sdk/PandaAuth.Sdk.csproj) has `IsPackable=false`. Complete login, token management, userinfo and role-reading wrappers are not implemented; no released NuGet package or installation command is claimed.

Web, MAUI, desktop and Blazor wrappers/samples will follow actual needs. Protocol support does not imply a complete cross-platform SDK. The existing [Server DemoClient](https://github.com/PandaLabs2026/panda-auth-server/tree/main/samples/PandaAuth.DemoClient) is not evidence that this SDK has passed acceptance testing.

## Build

Use the .NET SDK selected by [global.json](global.json) (currently 10.0.112 with latestFeature roll-forward). Clone repositories as siblings using the [workspace layout](https://github.com/PandaLabs2026/panda-auth/blob/main/WORKSPACE.md); cross-repository links require access. Commands below run from this repository root. They were statically checked, not executed, in this documentation change.

Clone Share beside this repository first.

```bash
dotnet build PandaAuth.Sdk.slnx
```

This is a library scaffold with no standalone application entry point. Phase 1 targets minimal integration and consumer validation; public packaging requires separate release acceptance.

## Roadmap and governance

Implementation targets are tracked in the [capability matrix](https://github.com/PandaLabs2026/panda-auth/blob/main/docs/open-source/capabilities.md) and [release gates](https://github.com/PandaLabs2026/panda-auth/blob/main/docs/open-source/release-readiness.md). Real product needs drive the roadmap; community requests are evaluated without delivery commitments. [Community/commercial boundaries](https://github.com/PandaLabs2026/panda-auth/blob/main/docs/open-source/strategy.md) describe scope, not delivered commercial products.

- [Security](SECURITY.md): selected private reporting channel, enablement unverified; no public vulnerability details.
- [Contributing](CONTRIBUTING.md): repository-specific checks and the shared contribution policy.
- [MIT License](LICENSE) for project-owned code/documentation, subject to [license scope](LICENSING.md); third-party terms remain applicable and brand images are excluded.
