# Architecture — Knowit.Signaturgruppen.Umbraco

Status: draft

Target: Umbraco 13 LTS (`net8.0`), Members only, v1.0.0

---

## 1. Purpose

Provide an opt-in NuGet package that lets an Umbraco site authenticate **frontend Members** against the Danish **Signaturgruppen / Nets eID Broker** OIDC provider (MitID, MitID Erhverv, NemLog-in as identity providers behind the broker).

The package wraps the standard ASP.NET Core OpenID Connect handler with:

- Signaturgruppen-specific request shaping (`idp_values`, `idp_params`, LoA, language).
- Umbraco Members auto-link wiring (`MemberExternalLoginProviderOptions` + `MemberExternalSignInAutoLinkOptions`).
- A pluggable **post-login pipeline** that can trigger an OIDC **step-up** (e.g. request `ssn` scope on a second `/authorize` call to obtain CPR).
- A GDPR-aware default for CPR handling.

A sibling NuGet, **`Knowit.Signaturgruppen.Umbraco.Akait`**, implements the AKAIT "SubjectId → medlemsnummer" flow as a reference consumer of the post-login pipeline.

---

## 2. Out of scope for v1

- Umbraco backoffice user login (Members only).
- Umbraco 14/15/16+ and `net9.0` (single TFM `net8.0`).
- CIBA, PAR, signed/encrypted `request` objects.
- Transaction signing (`transaction_token` scope).

---

## 3. Naming

- Convention: `Knowit.<IntegrationTarget>.<Host>`.
- Core: `Knowit.Signaturgruppen.Umbraco`.
- AKAIT consumer: `Knowit.Signaturgruppen.Umbraco.Akait`.

Risk: "Signaturgruppen" is the legacy brand; Nets owns the product and officially names it "Nets eID Broker". Accepting this risk because the Danish market still uses "Signaturgruppen" as the recognisable name. Revisit if Nets fully retires the brand.

---

## 4. Solution layout

```
Knowit.Signaturgruppen.Umbraco.sln
├─ Directory.Build.props                       # shared metadata, source link, deterministic build
├─ Directory.Packages.props                    # central package management
├─ docs/                                       # ADRs, sequence diagrams, supplementary guides
├─ src/
│  ├─ Knowit.Signaturgruppen.Umbraco/          # core, publishable
│  └─ Knowit.Signaturgruppen.Umbraco.Akait/    # AKAIT sibling, publishable
├─ samples/
│  └─ Knowit.Signaturgruppen.Umbraco.Sample/   # Umbraco 13 starter site, not published
└─ tests/
   └─ Knowit.Signaturgruppen.Umbraco.Tests/
```

The full file/folder layout, what each file does, and the request-time call sequence are documented in [`how-it-works.md`](how-it-works.md). This document focuses on design rationale.

---

## 5. Configuration surface

Consumer wires the package once in `Program.cs`:

```csharp
builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddSignaturgruppen(opts =>
    {
        opts.Environment   = SignaturgruppenEnvironment.Preproduction;
        opts.ClientId      = builder.Configuration["Signaturgruppen:ClientId"]!;
        opts.ClientSecret  = builder.Configuration["Signaturgruppen:ClientSecret"]!;
        opts.CallbackPath  = "/sg/login/callback";
        opts.Scopes        = ["openid", "mitid"];        // ssn requested on step-up only
        opts.MitId.LevelOfAssurance = NsisLoa.Substantial;
        opts.MitId.EnableAppSwitch  = true;
        opts.AutoLink.Enabled            = true;
        opts.AutoLink.MemberType         = "Member";
        opts.AutoLink.IsApproved         = true;
    })
    // optional sibling package
    .AddAkaitMembershipResolver(opts =>
    {
        opts.BaseUrl      = builder.Configuration["Akait:BaseUrl"]!;
        opts.TokenUrl     = builder.Configuration["Akait:TokenUrl"]!;
        opts.ClientId     = builder.Configuration["Akait:ClientId"]!;
        opts.ClientSecret = builder.Configuration["Akait:ClientSecret"]!;
    })
    .Build();
```

Mirrors `appsettings.json` for everything except secrets, which are expected via user-secrets, environment variables, or Azure Key Vault.

`SignaturgruppenEnvironment` resolves the authority:

| Value | Authority |
|---|---|
| `Production` | `https://netseidbroker.dk/op` |
| `Preproduction` | `https://pp.netseidbroker.dk/op` |
| `Custom` | consumer-supplied authority URL |

`CallbackPath` is automatically appended to `Umbraco:CMS:Global:ReservedPaths` via a `PostConfigureOptions<GlobalSettings>` so Umbraco's request pipeline lets it through.

---

## 6. Authentication pipeline

### 6.1 Standard login (no CPR needed)

```
[Member]               [Umbraco]                       [Nets eID Broker]                [MitID]
   |                       |                                  |                            |
   | click "Log ind"       |                                  |                            |
   |---------------------->|                                  |                            |
   |                       | POST UmbExternalLogin            |                            |
   |                       | scheme=Umbraco.Members.Signaturgruppen                        |
   |                       |--- OIDC challenge -------------->|                            |
   |                       |   scope=openid mitid             |                            |
   |                       |   idp_values=mitid               |                            |
   |                       |   idp_params={loa,ref_text,...}  |                            |
   |                       |                                  |--- delegate to MitID ----->|
   |                       |                                  |<-- MitID auth complete ----|
   |                       |<-- /sg/login/callback ----------|                            |
   |                       | OpenIdConnectHandler: token exchange + UserInfo               |
   |                       |   ClaimActions copy dk.cpr, mitid.uuid, mitid.identity_name…  |
   |                       | SignaturgruppenOidcEvents.TicketReceived:                     |
   |                       |   PostLoginPipeline.Run() → all handlers Continue             |
   |                       | OIDC handler SignInAsync to external cookie scheme            |
   |                       |   ("UmbracoExternalCookie")                                   |
   |                       | 302 → /umbraco/surface/UmbExternalLogin/ExternalLoginCallback |
   |                       |                                                               |
   |                       | UmbExternalLoginController.ExternalLoginCallback              |
   |                       |   IMemberSignInManager.ExternalLoginSignInAsync(...)          |
   |                       |     → on first login, AutoLinkOptions invokes our delegates:  |
   |                       |         OnAutoLinking   (set Name/Email/UserName)             |
   |                       |         OnExternalLogin (project ClaimToMemberProperty)       |
   |                       |     → MemberIdentityUser created/updated                      |
   |                       |     → member auth cookie issued                               |
   |<--------------------- 302 to original returnUrl ---------|                            |
```

The Umbraco auth scheme name follows Umbraco convention: `UmbracoMembers.Signaturgruppen`, produced via `b.SchemeForMembers("Signaturgruppen")` inside `AddMemberExternalLogins`.

Steps after `TicketReceived` are Umbraco machinery, not package code: the OIDC handler hands the broker ticket off to the external cookie scheme, then 302s to `UmbExternalLoginController.ExternalLoginCallback` (the URL `UmbExternalLoginController.ExternalLogin` baked into `AuthenticationProperties.RedirectUri` when it issued the Challenge). That controller is what creates/loads the `MemberIdentityUser` via `IMemberSignInManager.ExternalLoginSignInAsync` and issues the member auth cookie. The package's contribution to this leg is the `OnAutoLinking` / `OnExternalLogin` pair attached to `MemberExternalSignInAutoLinkOptions` by `MemberAutoLinkCallbacksPostConfigurator`.

### 6.2 Step-up (CPR / scope elevation)

The Signaturgruppen broker supports re-authorizing with extra scopes while reusing the existing SSO session. This is the mechanism the AKAIT flow uses to obtain CPR only when needed. The full step-up sequence is documented in [`how-it-works.md`](how-it-works.md); the design notes that follow apply to the current implementation.

Step-up implementation notes:

- A handler returns `PostLoginResult.StepUpRequired(scopes)` to request another `/authorize` round with extra scopes. After the round-trip, `TicketReceived` runs the pipeline again with the upgraded principal.
- **No server-side cookie.** The original return URL is encoded as a query string on `AuthenticationProperties.RedirectUri` (`/sg/step-up/callback?return_url=…`); the requested extra scopes are placed in `AuthenticationProperties.Items["sg.extra_scopes"]`. Both ride through the broker inside OIDC's `state` parameter, so they are restored verbatim on the callback `TicketReceivedContext`.
- The presence of `Items["sg.extra_scopes"]` on the second `TicketReceived` is the **loop guard**: if a handler still cannot proceed (e.g. `dk.cpr` did not arrive after `ssn` was requested), it must return `Abort` instead of `StepUpRequired` to avoid an infinite loop. The most common cause is the broker client not being whitelisted for the `ssn` scope.
- The challenge sets `prompt` to its default (interactive) rather than trying `prompt=none` first. Rationale: the broker only prompts for the missing CPR step anyway; doing a `prompt=none` probe first adds latency and complexity without saving user interaction.
- Scope extension is implemented by `SignaturgruppenOidcEvents.RedirectToIdentityProvider` reading `Items["sg.extra_scopes"]` and appending its value to `context.ProtocolMessage.Scope` before the redirect to `/authorize`.
- The `/sg/step-up/callback` controller is a thin pass-through: it reads `return_url` from the query string, runs it through `Url.IsLocalUrl`, and redirects (fallback `/`). Sign-in has already been completed by `OpenIdConnectHandler` at `/sg/login/callback` by the time the controller runs.

For the AKAIT flow specifically — `/MitId/FindPersonMedSubject` → step-up → `/MitId/RegistrerSubjectIdForPerson` — see the next section.

### 6.3 Logout

`end_session_endpoint` requires `id_token_hint`, so the OIDC handler is configured with `SaveTokens = true`. A `LogoutController` action at `/sg/logout` triggers `SignOutAsync` with both the Members cookie scheme and the Signaturgruppen scheme; `post_logout_redirect_uri` is taken from options and must be whitelisted in the broker admin UI.

---

## 7. Extension points

| Interface | Purpose | Default implementation |
|---|---|---|
| `IPostLoginHandler` | Run business logic after sign-in. Can request step-up via `PostLoginResult.StepUpRequired(scopes)`, abort via `Abort(reason)`, or continue. | None — empty pipeline. |
| `IMitIdParameterBuilder` | Build the `idp_params` JSON per request (e.g. dynamic `reference_text` per surface form). | Reads `MitIdParameters` from options. |
| `ISignaturgruppenStepUpService` | Issue secondary OIDC challenges. | Built-in; replaceable for tests. |

All extension points are registered via DI; consumers replace them with `services.Replace(...)` after the `AddSignaturgruppen` call.

---

## 8. GDPR / CPR handling

Defaults are deliberately strict; v1 does not expose an opt-out.

- **CPR is never persisted by the package.** The AKAIT handler holds CPR only in memory for the duration of the broker callback that obtains it; once the `POST /MitId/RegistrerSubjectIdForPerson` request returns, the local variable falls out of scope and the raw `dk.cpr` claim is stripped from the principal in `finally`. If your own `IPostLoginHandler` needs to persist a CPR-derived value, hash or encrypt it yourself before writing.
- **No automated decisions about persons.** The auto-link only enriches the member's profile data. Denial of access requires an explicit consumer-implemented `IPostLoginHandler` returning `Abort(reason)`. This is consistent with EU AI Act guidance on human-in-the-loop for decisions affecting natural persons.

The README will document Datatilsynet's published guidance on CPR storage and link to the relevant Databeskyttelsesforordning articles.

---

## 9. Implementation phases

Phases are ordered for dependency, not duration.

**Phase 0 — Foundation**
- Create solution, projects, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`.
- Configure deterministic build, source link, symbol packages.
- GitHub Actions: build, test, pack on PR; publish to nuget.org on tag.

**Phase 1 — Core OIDC + Members wiring**
- `SignaturgruppenOptions`, environment-to-authority resolver.
- `UmbracoBuilderExtensions.AddSignaturgruppen` calling `AddMemberExternalLogins(logins => logins.AddMemberLogin(memberAuth => memberAuth.AddOpenIdConnect(memberAuth.SchemeForMembers("Signaturgruppen"), …), loginProviderOptions => { loginProviderOptions.AutoLinkOptions = … }))`. `AutoLinkOptions` is supplied via the second callback of `AddMemberLogin`, which `MemberAuthenticationBuilder.AddRemoteScheme` registers internally as the scheme's named `MemberExternalLoginProviderOptions` — the consumer-facing `IConfigureNamedOptions<MemberExternalLoginProviderOptions>` workaround for Umbraco issue #17027 is therefore not needed for this package.
- Reactive configuration of `OpenIdConnectOptions` for the scheme via `services.AddOptions<OpenIdConnectOptions>(schemeName).Configure<IOptionsMonitor<SignaturgruppenOptions>>(...)`.
- `SignaturgruppenOidcEvents` for `idp_params`, claim mapping, error formatting.
- `PostConfigureOptions<GlobalSettings>` adding `CallbackPath` to `ReservedPaths`.

**Phase 2 — Auto-link and claim mapping**
- `MemberExternalSignInAutoLinkOptions` with `OnAutoLinking` / `OnExternalLogin`.
- Built-in `ClaimToMemberProperty` projection inside `MemberAutoLinkConfigurator.OnExternalLogin`.

**Phase 3 — Step-up service**
- `ISignaturgruppenStepUpService.ChallengeForScopesAsync(...)`.
- DataProtection-protected resume cookie with TTL and replay protection.
- `StepUpController` exposing `/sg/step-up` and its callback.
- `OnRedirectToIdentityProvider` honours per-request extra scopes from `AuthenticationProperties.Items`.

**Phase 4 — Post-login pipeline and CPR protection**
- `IPostLoginHandler`, `PostLoginContext`, `PostLoginResult`.
- Pipeline executed from `OnTicketReceived` and from the step-up callback.

**Phase 5 — AKAIT sibling package**
- Typed `HttpClient` with `Microsoft.Extensions.Http.Resilience`.
- OAuth2 client-credentials Bearer attached transparently by `IdentityModel.AspNetCore`'s `AddClientAccessTokenHandler("akait")`; the access token is cached and auto-refreshed on 401.
- `AkaitMembershipResolver : IPostLoginHandler` implementing the five-step AKAIT flow:
  1. `GET /MitId/FindPersonMedSubject?subjectId={sub}`.
  2. On 200, write `medlemsnummer`.
  3. On 404, return `StepUpRequired(["ssn"])`.
  4. After step-up, `POST /MitId/RegistrerSubjectIdForPerson` with body `{ "subjectId": "...", "cprNummer": "..." }`, drop the `dk.cpr` claim, re-call step 1.
  5. On 400, throw `AkaitValidationException` with the Danish error message verbatim.
- Endpoint shape re-verified against the live AKAIT MitId-v1 OpenAPI document at implementation time.

**Phase 6 — Sample site**
- Umbraco 13 starter project.
- Member type with `medlemsnummer` and `mitidIdentityName` properties.
- Login Razor view using `BeginUmbracoForm<UmbExternalLoginController>`.
- Manual smoke test against `pp.netseidbroker.dk` and AKAIT test environment.

**Phase 7 — Tests and documentation**
- Unit tests: options binding, `idp_params` JSON shape, claim mapping, pipeline result handling.
- Integration tests: `WebApplicationFactory` hosting a minimal Umbraco app, broker mocked via WireMock.Net for `/authorize`, `/token`, `/userinfo`, `/endsession`.
- README with sequence diagrams, GDPR section, troubleshooting matrix, sample `appsettings.json`.

---

## 10. Risks and known issues

- **Umbraco 13 issue [#17027](https://github.com/umbraco/Umbraco-CMS/issues/17027) — not currently exposure.** The bug only fires when a consumer registers `IConfigureNamedOptions<MemberExternalLoginProviderOptions>` directly in DI. This package instead passes `AutoLinkOptions` through the second callback of `MemberExternalLoginsBuilder.AddMemberLogin(build, loginProviderOptions)`, which `MemberAuthenticationBuilder.AddRemoteScheme` registers internally with the correct named-options key. If a future version of the package switches to direct `IConfigureNamedOptions` registration (e.g. for hot-reload), implement both `Configure(string, T)` and `Configure(T)` overloads and re-check the minimum 13.x version.
- **Subject identifier semantics.** Broker docs say `sub` is per-organization pairwise; discovery metadata advertises `subject_types_supported: ["public"]`. Treat `sub` as durable per-tenant key; document that consumers should not assume portability to another `client_id`.
- **`prompt=none` not used for step-up.** See §6.2 rationale. Revisit if real-world latency on step-up becomes a complaint.
- **Brand risk on package name.** See §3.
- **AKAIT Swagger drift.** The endpoint shape captured here is from the documentation excerpt the user provided. Live OpenAPI must be re-verified before `AkaitMembershipClient` is finalised.

---

## 11. References

Signaturgruppen / Nets eID Broker

- [Documentation home](https://signaturgruppen-a-s.github.io/signaturgruppen-broker-documentation/)
- [Environments](https://signaturgruppen-a-s.github.io/signaturgruppen-broker-documentation/environments.html)
- [OIDC integration](https://signaturgruppen-a-s.github.io/signaturgruppen-broker-documentation/oidc-integration.html)
- [MitID IdP](https://signaturgruppen-a-s.github.io/signaturgruppen-broker-documentation/idps/mitid.html)
- [Sessions and SSO](https://signaturgruppen-a-s.github.io/signaturgruppen-broker-documentation/sessions-and-sso.html)
- [Issued claims and tokens](https://signaturgruppen-a-s.github.io/signaturgruppen-broker-documentation/issued-claims-tokens.html)
- [Troubleshooting](https://signaturgruppen-a-s.github.io/signaturgruppen-broker-documentation/troubleshooting.html)
- [.NET Core demo](https://github.com/Signaturgruppen-A-S/signaturgruppen-broker-demo)
- [OWIN .NET Framework demo](https://github.com/Signaturgruppen-A-S/netseidbroker-dotnet-demo)

Umbraco

- [Members external login providers (13)](https://github.com/umbraco/UmbracoDocs/blob/main/13/umbraco-cms/reference/security/external-login-providers.md)
- [Entra ID for Members tutorial](https://github.com/umbraco/UmbracoDocs/blob/main/13/umbraco-cms/tutorials/add-microsoft-entra-id-authentication.md)
- [`Login.cshtml` snippet (`UmbExternalLoginController` usage)](https://github.com/umbraco/Umbraco-CMS/blob/main/src/Umbraco.Core/EmbeddedResources/Snippets/Login.cshtml)
- [Issue #17027 — `Configure(name, options)` not called](https://github.com/umbraco/Umbraco-CMS/issues/17027)

AKAIT

- [MitId-v1 Swagger (test)](https://apiprodtest.mycompany.dk/swagger/index.html?urls.primaryName=MitId-v1)

