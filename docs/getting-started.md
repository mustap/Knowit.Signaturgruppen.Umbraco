# Getting started — Knowit.Signaturgruppen.Umbraco

This walks you through adding **Signaturgruppen (Nets eID Broker)** OIDC login to an Umbraco 13 site, using the `Knowit.Signaturgruppen.Umbraco` package and — if you need AKAIT membership resolution — the `Knowit.Signaturgruppen.Umbraco.Akait` sibling package.

---

## 1. Prerequisites

- **Umbraco 13.14.0 or newer** on `net8.0`. Older 13.x patches carry advisories the package's audit floor rejects.
- A **Signaturgruppen / Nets eID Broker client** in preproduction or production. Request via `broker@signaturgruppen.dk`. You will get a `client_id`, a `client_secret`, and you must whitelist your `CallbackPath` and `SignedOutCallbackPath` in the broker admin UI.
- For AKAIT: a `ClientId` / `ClientSecret` plus the OAuth token endpoint URL. AKAIT API root is `https://apiprodtest.<consumer>.dk` in test.

---

## 2. Install

```bash
dotnet add package Knowit.Signaturgruppen.Umbraco
# Optional, only if you use AKAIT's medlemsnummer resolution:
dotnet add package Knowit.Signaturgruppen.Umbraco.Akait
```

---

## 3. Wire it up in `Program.cs`

```csharp
using Knowit.Signaturgruppen.Umbraco.Akait.DependencyInjection;
using Knowit.Signaturgruppen.Umbraco.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddSignaturgruppen() // needed
    .AddAkaitMembershipResolver()   // optional
    .Build();

var app = builder.Build();
await app.BootUmbracoAsync();
app.UseUmbraco()
    .WithMiddleware(u => { u.UseBackOffice(); u.UseWebsite(); })
    .WithEndpoints(u => { u.UseBackOfficeEndpoints(); u.UseWebsiteEndpoints(); });
await app.RunAsync();
```

Both extensions bind from `appsettings.json` by default (sections `Signaturgruppen` and `Akait`) and accept an optional `Action<TOptions>` for inline overrides.

---

## 4. Configure via `appsettings.json`

```json
{
  "Signaturgruppen": {
    "Environment": "Preproduction",
    "ClientId": "REPLACE_VIA_USER_SECRETS",
    "ClientSecret": "REPLACE_VIA_USER_SECRETS",
    "CallbackPath": "/sg/login/callback",
    "SignedOutCallbackPath": "/sg/logout/callback",
    "Scopes": [ "openid", "mitid" ],
    "MitId": {
      "LevelOfAssurance": "Substantial",
      "EnableAppSwitch": true
    },
    "AutoLink": {
      "Enabled": true,
      "MemberType": "Member",
      "IsApproved": true
    },
    "ClaimToMemberProperty": {
      "akait.medlemsnummer": "medlemsnummer",
      "mitid.identity_name": "mitidIdentityName"
    }
  },
  "Akait": {
    "BaseUrl": "https://apiprodtest.mycompany.dk",
    "TokenUrl": "https://...",
    "ClientId": "REPLACE_VIA_USER_SECRETS",
    "ClientSecret": "REPLACE_VIA_USER_SECRETS",
    "Scope": "akait.mitid"
  }
}
```

**Secrets belong in user-secrets / environment variables / Key Vault — never check them into source control.**

```bash
dotnet user-secrets init
dotnet user-secrets set "Signaturgruppen:ClientId" "your-broker-client-id"
dotnet user-secrets set "Signaturgruppen:ClientSecret" "your-broker-client-secret"
dotnet user-secrets set "Akait:ClientId" "your-akait-id"
dotnet user-secrets set "Akait:ClientSecret" "your-akait-secret"
```

### Environment values

| Value | Authority |
|---|---|
| `Production` | `https://netseidbroker.dk/op` |
| `Preproduction` | `https://pp.netseidbroker.dk/op` |
| `Custom` | uses `CustomAuthority` |

---

## 5. Create the member type

`Signaturgruppen:AutoLink:MemberType` (default `Member` — Umbraco's built-in member type) must resolve to a member type that already exists in Umbraco; the package itself does not create it. You can keep the default or point it at a custom alias (e.g. `MitIdMember`) — in the latter case, create the type through the back-office UI or bootstrap it from your own `IComposer` on `UmbracoApplicationStartedNotification`. Add the property aliases your `ClaimToMemberProperty` map refers to (e.g. `medlemsnummer`, `mitidIdentityName`).

The `ClaimToMemberProperty` map in your `appsettings.json` wires broker / AKAIT claims onto these property aliases on every login.

---

## 6. Add a login button

In any Razor view (e.g. `Views/Login.cshtml`):

```cshtml
@inject Umbraco.Cms.Web.Common.Security.IMemberExternalLoginProviders MemberExternalLoginProviders

@foreach (var login in await MemberExternalLoginProviders.GetMemberProvidersAsync())
{
    using (Html.BeginUmbracoForm<Umbraco.Cms.Web.Website.Controllers.UmbExternalLoginController>(
        nameof(Umbraco.Cms.Web.Website.Controllers.UmbExternalLoginController.ExternalLogin)))
    {
        <button type="submit"
                name="provider"
                value="@login.ExternalLoginProvider.AuthenticationType">
            Log ind med @login.AuthenticationScheme.DisplayName
        </button>
    }
}
```

The button's `value` is the full scheme name (`UmbracoMembers.Signaturgruppen` by default).

---

## 7. First run

```bash
dotnet run
```

Open the site, click **Log ind med Signaturgruppen**, complete the MitID flow in the broker preprod environment, and you should land back signed in. The package's wire-up does the rest:

1. OIDC `/authorize` is sent with `idp_values=mitid` and an `idp_params` JSON object holding the configured `LevelOfAssurance` and other MitID parameters.
2. The broker callback at `/sg/login/callback` produces a `ClaimsPrincipal`.
3. Umbraco's auto-link creates a member of the type configured in `AutoLink.MemberType` if one does not yet exist for this broker `sub`.
4. The post-login pipeline runs — by default a no-op, unless you registered handlers or added `AddAkaitMembershipResolver()`.
5. The member cookie is issued, browser is redirected to the originally requested URL.

If AKAIT is wired and the broker subject is not yet mapped to a `medlemsnummer`, the resolver returns `StepUpRequired(["ssn"])` — the package then issues a second OIDC challenge with the `ssn` scope, the broker prompts only for CPR, and on return the resolver completes registration and adds the `akait.medlemsnummer` claim.

---

## 8. First-run troubleshooting

| Symptom | Likely cause |
|---|---|
| 404 on `/sg/login/callback` | Umbraco intercepts the URL. The package auto-appends the callback path to `Umbraco:CMS:Global:ReservedPaths`, but a custom `ReservedPaths` value in `appsettings.json` can override it — add the path manually if so. |
| `invalid_client` from the broker | `ClientId` / `ClientSecret` mismatch, or the redirect URI is not whitelisted in the broker admin UI. Whitelist `https://localhost:44339/sg/login/callback` (or your real host). |
| Member created but properties are blank | The member type does not have property aliases matching `ClaimToMemberProperty`. Check member-type aliases and the map. |
| Repeated step-up loop | The broker returned `ssn` scope but the AKAIT API still 404s. Check that the CPR is actually in the AKAIT member database. |

See `production-checklist.md` before going live.
