# How `Knowit.Signaturgruppen.Umbraco` works

## What the package does

It plugs Signaturgruppen / Nets eID Broker (MitID) into Umbraco 13's **Members** authentication. After a successful broker callback it auto-links the member, projects broker claims onto member-type properties, runs a pluggable post-login pipeline, and supports OIDC **step-up** to acquire additional scopes (e.g. `ssn` for CPR) on a second `/authorize` round.

---

## Login flow — what happens on a single sign-in

```
┌─────────┐  click button   ┌──────────────────────┐   302 to broker    ┌────────┐
│ Browser │ ──────────────► │ Umbraco / Member-form│ ─────────────────► │ Broker │
└─────────┘                 │ UmbExternalLoginCtl  │                    └───┬────┘
                            └──────────────────────┘                        │
                                       ▲                                    │ MitID UI
                                       │ form_post(code, state)             ▼
                                       └─────────────────────── back to host:
                                                                /sg/login/callback
                                                                       │
                                                                       ▼
┌───────────────────────────────────────────────────────────────────────────────────────┐
│  Microsoft OpenIdConnectHandler  (scheme "UmbracoMembers.Signaturgruppen")            │
│                                                                                       │
│  1. RedirectToIdentityProvider event   ◄── SignaturgruppenOidcEvents                  │
│        • idp_values = "mitid"                                                         │
│        • idp_params = JSON from DefaultIdpParamsBuilder (LoA, app_switch, …)          │
│        • language hint, extra scopes from AuthenticationProperties                    │
│                                                                                       │
│  2. Token exchange  (uses _configuration.TokenEndpoint from discovery)                │
│  3. UserInfo                                                                          │
│        → ClaimActions (wired in UmbracoBuilderExtensions.ConfigureOpenIdConnect)      │
│          MapUniqueJsonKey for dk.cpr, mitid.uuid, mitid.identity_name, …              │
│                                                                                       │
│  4. TicketReceived event       ◄── SignaturgruppenOidcEvents                          │
│        • PostLoginPipeline.RunAsync(ctx) ─► IPostLoginHandler[]                       │
│              ├─ Continue        → fall through to OIDC sign-in                        │
│              ├─ Abort(reason)   → 403, sign-in halted                                 │
│              └─ StepUpRequired  → see step-up flow below                              │
│                                                                                       │
│  5. SignInAsync to the external cookie scheme  ("UmbracoExternalCookie")              │
│        Stores the broker ticket in a short-lived cookie that the next request reads.  │
│                                                                                       │
│  6. 302 to properties.RedirectUri =                                                   │
│        /umbraco/surface/UmbExternalLogin/ExternalLoginCallback?returnUrl=<orig>       │
│        (that URL was set by UmbExternalLoginController.ExternalLogin when it issued   │
│         the Challenge from the login form-post.)                                      │
└───────────────────────────────────────────────────────────────────────────────────────┘
                                            │
                                            ▼
┌───────────────────────────────────────────────────────────────────────────────────────┐
│  UmbExternalLoginController.ExternalLoginCallback   (Umbraco built-in)                │
│                                                                                       │
│  7. Reads the broker login info from the external cookie.                             │
│  8. Calls IMemberSignInManager.ExternalLoginSignInAsync(provider, providerKey, …).    │
│        This is where Umbraco's auto-link runs, driven by AutoLinkOptions which        │
│        MemberAutoLinkCallbacksPostConfigurator pointed at our two delegates:          │
│           OnAutoLinking    → set Name/Email/UserName (sub@signaturgruppen.local)      │
│           OnExternalLogin  → iterate Signaturgruppen:ClaimToMemberProperty,           │
│                              copy each claim value onto the matching member property  │
│  9. SignInManager issues the **member auth cookie** and 302s to the original          │
│     returnUrl. The browser lands on the requested page, signed in as the member.      │
└───────────────────────────────────────────────────────────────────────────────────────┘
```

The package itself ends at step 6: the OIDC handler 302s the browser. Steps 7–9 are Umbraco's built-in `UmbExternalLoginController.ExternalLoginCallback` + `IMemberSignInManager` machinery — we only contribute the two delegates on `MemberExternalSignInAutoLinkOptions`.

---

## File map — what each folder owns

```
src/Knowit.Signaturgruppen.Umbraco/
├── DependencyInjection/
│   ├── UmbracoBuilderExtensions.cs                — ENTRY POINT. AddSignaturgruppen(...)
│   │       Binds SignaturgruppenOptions, registers all services, configures the named
│   │       OpenIdConnectOptions (Authority, ClientId, CallbackPath, ClaimActions, …)
│   │       inline inside memberAuth.AddOpenIdConnect's configure delegate.
│   ├── MemberAutoLinkCallbacksPostConfigurator    — IPostConfigureOptions<MemberExternalLoginProviderOptions>
│   │       Hooks MemberAutoLinkConfigurator.OnAutoLinking / .OnExternalLogin into
│   │       Umbraco's auto-link plumbing for our scheme name only.
│   └── SignaturgruppenReservedPathsConfigurator    — IPostConfigureOptions<GlobalSettings>
│           Appends /sg/login/callback, /sg/logout/callback, /sg/step-up/callback
│           to ReservedPaths so Umbraco routing doesn't swallow them.
│
├── Authentication/
│   ├── SignaturgruppenOidcEvents.cs    — subclass of OpenIdConnectEvents.
│   │       Owns RedirectToIdentityProvider (adds idp params + step-up scopes) and
│   │       TicketReceived (runs PostLoginPipeline, handles StepUp/Abort/Continue).
│   │       Wired via oidc.EventsType = typeof(SignaturgruppenOidcEvents).
│   ├── DefaultIdpParamsBuilder.cs       — Builds the idp_params JSON from
│   │       SignaturgruppenOptions.MitId (LoA, reference_text, app_switch, hints).
│   │       Injected into SignaturgruppenOidcEvents.
│   └── ClaimMaps.cs                     — String constants ("sub", "mitid.uuid",
│           "dk.cpr", "mitid.identity_name", "idp", "neb_sid", …) used by the
│           configurator and by your own IPostLoginHandlers.
│
├── Members/
│   └── MemberAutoLinkConfigurator.cs   — Two delegates:
│           OnAutoLinking      = synthesizes Name/Email/UserName on a new MemberIdentityUser
│           OnExternalLogin    = reads Signaturgruppen:ClaimToMemberProperty,
│                                copies each broker claim onto the matching member
│                                property alias via IMemberService, saves on change.
│
├── Pipeline/
│   ├── IPostLoginHandler.cs    — single method: HandleAsync(PostLoginContext) → PostLoginResult
│   ├── PostLoginContext.cs     — carries HttpContext, Principal, AuthenticationProperties
│   ├── PostLoginResult.cs      — discriminated outcome:
│   │                              Continue | Abort(reason) | StepUpRequired(scopes)
│   └── PostLoginPipeline.cs    — iterates IPostLoginHandler[] in DI registration order;
│                                  short-circuits on the first non-Continue result.
│           AKAIT sibling registers AkaitMembershipResolver here.
│
├── StepUp/
│   ├── ISignaturgruppenStepUpService.cs  — single method: ChallengeAsync(ctx, scopes, returnUrl)
│   ├── SignaturgruppenStepUpService.cs   — issues HttpContext.ChallengeAsync with
│   │      AuthenticationProperties.RedirectUri = "/sg/step-up/callback?return_url=<url>"
│   │      and Items[ExtraScopesAuthPropertyKey] = the requested scopes. OIDC's `state`
│   │      parameter round-trips both back through the broker; no server-side cookie used.
│   └── Controllers/StepUpCallbackController.cs
│           — receives /sg/step-up/callback?return_url=…, validates the URL with
│             Url.IsLocalUrl, redirects there (fallback `/`). By the time this runs the
│             OIDC handler has already completed sign-in at /sg/login/callback.
│
├── Configuration/    — pure POCOs bound from "Signaturgruppen" section in appsettings
│   ├── SignaturgruppenOptions.cs       — root: Environment, ClientId, ClientSecret,
│   │       CallbackPath, Scopes, DisplayName, MitId, AutoLink, ClaimToMemberProperty.
│   ├── MitIdParameters.cs              — LoA + idp_params toggles (consumed by builder)
│   ├── AutoLinkOptions.cs              — Enabled, MemberType (alias), DefaultCulture, IsApproved
│   ├── NsisLoa.cs                      — Low/Substantial/High enum
│   └── SignaturgruppenEnvironment.cs   — Preproduction/Production/Custom + ResolveAuthority(...)
│
└── Constants/
    └── SignaturgruppenDefaults.cs      — scheme name "Signaturgruppen", display name,
            callback paths, ProductionAuthority/PreproductionAuthority URIs,
            ExtraScopesAuthPropertyKey.
```

---

## Step-up flow — the second round-trip

```
TicketReceived runs PostLoginPipeline
        │
        │   handler returns StepUpRequired(["ssn"])
        ▼
SignaturgruppenOidcEvents.TicketReceived
        │   context.HandleResponse()                       ── stops normal sign-in
        │   stepUpService.ChallengeAsync(ctx, ["ssn"], returnUrl)
        ▼
SignaturgruppenStepUpService.ChallengeAsync
        │   • props.RedirectUri = "/sg/step-up/callback?return_url=<original>"
        │   • props.Items[ExtraScopesAuthPropertyKey] = "ssn"
        │   • HttpContext.ChallengeAsync("UmbracoMembers.Signaturgruppen", props)
        ▼
SignaturgruppenOidcEvents.RedirectToIdentityProvider  (second time)
        │   • reads ExtraScopesAuthPropertyKey from props
        │   • appends "ssn" to context.ProtocolMessage.Scope
        │   • re-injects idp_values + idp_params
        ▼
[broker re-authenticates user; returns to /sg/login/callback]
   OIDC `state` carries the original AuthenticationProperties (incl. RedirectUri and Items)
        ▼
SignaturgruppenOidcEvents.TicketReceived  (second time)
        │   principal now carries dk.cpr (because ssn was in the scope list)
        │   PostLoginPipeline runs again
        │     • AkaitMembershipResolver registers the CPR → returns Continue
        │     • OR: still no CPR + props.Items[ExtraScopesAuthPropertyKey] present
        │       → returns Abort  (loop guard — no cookie needed; the property survives
        │                          the round-trip via OIDC `state`)
        │
OIDC handler signs in to the external cookie scheme ("UmbracoExternalCookie")
   and 302s to props.RedirectUri = /sg/step-up/callback?return_url=<original>
        ▼
StepUpCallbackController.Get
        │   • reads return_url from Request.Query
        │   • Url.IsLocalUrl check (fallback "/")
        │   • redirect to return_url
        ▼
<original> is the UmbExternalLoginController.ExternalLoginCallback URL that
UmbExternalLoginController.ExternalLogin set before the first Challenge.
        ▼
UmbExternalLoginController.ExternalLoginCallback   (Umbraco built-in)
        │   • reads broker login info from the external cookie
        │   • IMemberSignInManager.ExternalLoginSignInAsync(...)
        │       → auto-link runs (OnAutoLinking, OnExternalLogin) the first time
        │       → main member auth cookie is issued
        │   • 302 to the originally requested returnUrl
        ▼
Browser lands on the original page, signed in as the member.
```

---

## How the AKAIT sibling plugs in

`AddAkaitMembershipResolver(...)` (in the sibling package) registers exactly one extra `IPostLoginHandler` — `AkaitMembershipResolver` — plus a typed `HttpClient` whose Bearer header is owned by `IdentityModel.AspNetCore`'s `AddClientAccessTokenHandler("akait")`. From the core package's point of view it's just one more entry in the `IEnumerable<IPostLoginHandler>` the pipeline iterates. That handler is what drives the step-up (it returns `StepUpRequired(["ssn"])` when AKAIT can't find the subject) and the abort guard (when CPR still isn't present after the second round).
