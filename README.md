# panda-auth-sdk

**PandaAuth by PandaLabs** · [简体中文](README.zh-CN.md)

**Official page [pandalabs.cc](https://pandalabs.cc/products/panda-auth/)** · Chinese site [pandalabs.cn](https://pandalabs.cn/products/panda-auth/) · [PandaLabs product suite](https://pandalabs.cc/products/)

> The PandaAuth suite is currently in **Community Preview 0.2.0-preview.1** — deployed in production and open to early community users. The stable Community Release 1.0.0 has not shipped yet. Access is by invitation or request.

## Responsibility and boundaries

The .NET integration SDK repository for PandaAuth, depending on sibling [panda-auth-share](https://github.com/PandaLabs2026/panda-auth-share). The SDK is intended to version independently of product releases.

## Current implementation and limitations

The stateless [PandaAuthClient](src/PandaAuth.Sdk/PandaAuthClient.cs) provides the minimum OIDC capabilities: discovery, authorization code + PKCE, client credentials, refresh token, userinfo and revoke. It does not persist state, PKCE verifiers, access tokens or refresh tokens. The [project](src/PandaAuth.Sdk/PandaAuth.Sdk.csproj) ships the **1.0.0** package (including this README and the MIT license text), distributed via GitHub Releases as a 1.0 support commitment (a component-level commitment, separate from the suite's public version).

Web, MAUI, desktop and Blazor wrappers/samples will follow actual needs. Protocol support does not imply a complete cross-platform SDK. The existing [Server DemoClient](https://github.com/PandaLabs2026/panda-auth-server/tree/main/samples/PandaAuth.DemoClient) is not evidence that this SDK has passed acceptance testing.

## Build

Use the .NET SDK selected by [global.json](global.json) (10.0.404, exact stable SDK). This repository can be built without the private PandaAuth coordination repository; clone the public Share repository beside it:

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

## Discovery trust / 2.0 migration

Source candidate: SDK 2.0.0 with Share 1.0.1; these changes have not been published as packages or deployed. Previously distributed 1.0.0 remains historical release evidence.

Use `using var client = new PandaAuthClient(options)` for network calls. This creates a TLS-validating transport with automatic redirects disabled. Direct `HttpMessageHandler` injection is available for trusted application transports; the SDK disables redirects on native handlers, rejects delegating handlers, and owns the supplied handler. Custom transports must never redirect or retry credential requests.

The existing `(HttpClient, options)` constructor remains usable for synchronous URL builders (including BFF logout), but network methods now fail before any request: an opaque HttpClient cannot establish safe redirect policy. Migrate network callers to the options constructor. Options are snapshotted at construction.

Discovery `issuer` must match the one configured issuer exactly, including trailing slash, case and path. Every advertised endpoint must be an absolute HTTPS URI without userinfo, fragment or whitespace; missing optional endpoints fail when used. A valid issuer may advertise another HTTPS host. Metadata failure sends no code, verifier, client secret, refresh/access token or revocation token. Redirect responses are errors, with no follow-up request. Host migration aliases never imply issuer equivalence.

This client does not validate ID Token signatures/audience/nonce or own browser state/session. A complete OIDC login handler remains responsible for those checks. SDK and Share versions evolve independently; no package publication or organization-wide adoption is implied.
