# Knowit.Signaturgruppen.Umbraco.Akait

AKAIT consumer for the [`Knowit.Signaturgruppen.Umbraco`](https://www.nuget.org/packages/Knowit.Signaturgruppen.Umbraco) package. Implements the AKAIT MitId v1 API flow that maps a Signaturgruppen MitID subject to an AKAIT **medlemsnummer**, as a single `IPostLoginHandler`.

The handler implements this flow:

1. `GET /MitId/FindPersonMedSubject?subjectId={sub}` — if `200`, attach `akait.medlemsnummer` claim and continue.
2. If `404` and no CPR is present in the principal, return `StepUpRequired(["ssn"])`. The core package handles the second OIDC challenge; only the CPR step is prompted at the broker.
3. After step-up returns with CPR: `POST /MitId/RegistrerSubjectIdForPerson` with body `{ "subjectId": "...", "cprNummer": "..." }`, then re-call step 1.
4. On HTTP `400`, raise `AkaitValidationException` carrying the Danish reason verbatim.
5. The raw `dk.cpr` claim is always stripped from the principal in `finally`, so it cannot land in the Umbraco cookie.

## Install

```bash
dotnet add package Knowit.Signaturgruppen.Umbraco
dotnet add package Knowit.Signaturgruppen.Umbraco.Akait
```

## Wire-up

```csharp
using Knowit.Signaturgruppen.Umbraco.Akait.DependencyInjection;
using Knowit.Signaturgruppen.Umbraco.DependencyInjection;

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .AddSignaturgruppen()  // add Signaturgruppen authentication 
    .AddAkaitMembershipResolver()  // map MitID subject to AkaIt medlemsnummer
    .Build();
```

```jsonc
// appsettings.json
{
  "Akait": {
    "BaseUrl": "https://apiprodtest.mycompany.dk",
    "TokenUrl": "https://...",
    "ClientId": "REPLACE_VIA_USER_SECRETS",
    "ClientSecret": "REPLACE_VIA_USER_SECRETS",
    "Scope": "akait.mitid",
    "MedlemsnummerClaimType": "akait.medlemsnummer"
  },
  "Signaturgruppen": {
    "ClaimToMemberProperty": {
      "akait.medlemsnummer": "medlemsnummer"
    }
  }
}
```

Adding the entry under `ClaimToMemberProperty` persists the resolved `medlemsnummer` onto the matching member-type property alias (you need a `medlemsnummer` property on your member type — see the core package's getting-started doc).

## Components

| Type | Role |
|---|---|
| `IAkaitMembershipClient` / `AkaitMembershipClient` | Typed HttpClient against the AKAIT MitId v1 API. `200` → medlemsnummer, `404` → null, `400` → `AkaitValidationException`. |
| `AddClientAccessTokenHandler("akait")` | Provided by `IdentityModel.AspNetCore`; transparently attaches a cached OAuth2 client-credentials Bearer token to the AKAIT HttpClient and refreshes it on 401. Configured via `AddAccessTokenManagement(...)` in `AddAkaitMembershipResolver`. |
| `AkaitMembershipResolver` | Public `IPostLoginHandler` running the five-step flow above. |
| `AkaitValidationException` | Thrown on AKAIT `400`; message is the verbatim Danish server text. |

## Configuration reference

| Key | Purpose |
|---|---|
| `Akait:BaseUrl` | Absolute root URL of the AKAIT MitId v1 API. |
| `Akait:TokenUrl` | OAuth2 client-credentials token endpoint. |
| `Akait:ClientId` / `Akait:ClientSecret` | Client credentials, kept in user-secrets / Key Vault. |
| `Akait:Scope` | Optional OAuth2 scope for the client-credentials grant. |
| `Akait:Timeout` | HttpClient timeout (default `00:00:30`). |
| `Akait:MedlemsnummerClaimType` | Claim type written onto the principal when AKAIT returns a medlemsnummer. Default `akait.medlemsnummer`. |

## License

MIT — see `LICENSE`.
