# Knowit.Signaturgruppen.Umbraco

OIDC integration for **Signaturgruppen / Nets eID Broker** (MitID, MitID Erhverv, NemLog-in) targeting **Umbraco 13 frontend Members**.

The package wraps the standard ASP.NET Core OpenID Connect handler with:

- Signaturgruppen-specific request shaping (`idp_values`, `idp_params`, NSIS LoA, language).
- Umbraco Members auto-link wiring + a configurable claim-to-member-property mapper.
- A pluggable post-login pipeline that supports OIDC **step-up** (e.g. requesting the `ssn` scope on a second `/authorize` to obtain CPR).

If you need the AKAIT MitId v1 API consumer (SubjectId → medlemsnummer), install the sibling [`Knowit.Signaturgruppen.Umbraco.Akait`](https://www.nuget.org/packages/Knowit.Signaturgruppen.Umbraco.Akait) package.

## Install

```bash
dotnet add package Knowit.Signaturgruppen.Umbraco
```

Requires Umbraco **13.14.0 or newer** on **`net8.0`**.

## Minimum wire-up

```csharp
using Knowit.Signaturgruppen.Umbraco.DependencyInjection;

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddSignaturgruppen() // add Signaturegruppen authentication
    .Build();
```

```jsonc
// appsettings.json
{
  "Signaturgruppen": {
    "Environment": "Preproduction",       // or "Production"
    "ClientId": "REPLACE_VIA_USER_SECRETS",
    "ClientSecret": "REPLACE_VIA_USER_SECRETS",
    "CallbackPath": "/sg/login/callback",
    "Scopes": [ "openid", "mitid" ],
    "MitId": { "LevelOfAssurance": "Substantial" },
    "AutoLink": { "Enabled": true, "MemberType": "Member" },
    "ClaimToMemberProperty": {
      "mitid.identity_name": "mitidIdentityName"
    }
  }
}
```

Whitelist `https://your-host/sg/login/callback` and `https://your-host/sg/logout/callback` in the Signaturgruppen broker admin UI.

## Defaults

| Setting | Value |
|---|---|
| Authentication scheme | `UmbracoMembers.Signaturgruppen` |
| Display name | `Signaturgruppen` |
| Login callback path | `/sg/login/callback` |
| Signed-out callback path | `/sg/logout/callback` |
| Step-up callback path | `/sg/step-up/callback` |
| Production authority | `https://netseidbroker.dk/op` |
| Preproduction authority | `https://pp.netseidbroker.dk/op` |

## Documentation

The full documentation lives in the [repository `docs/` folder](https://github.com/mustap/Knowit.Signaturgruppen.Umbraco/tree/main/docs):

- **getting-started.md** — install, configure, member type, login button, first run.
- **hooks-cookbook.md** — recipes for `IPostLoginHandler`.
- **production-checklist.md** — what to verify before going live (HTTPS, DataProtection persistence, GDPR).
- **ARCHITECTURE.md** — design, pipeline, extension points, the step-up flow.

## License

MIT — see `LICENSE`.
