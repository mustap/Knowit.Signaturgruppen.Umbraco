# Hooks cookbook — Knowit.Signaturgruppen.Umbraco

The package exposes one replaceable DI service. This cookbook shows the common patterns: enrich principals, gate access, request step-ups, and persist additional claim values onto member properties.

| Hook | Default behaviour | When to replace |
|---|---|---|
| `IPostLoginHandler` *(many)* | None registered. | Run any post-login business logic; the main extension surface. |

Claim-to-member-property projection is configuration-driven via `Signaturgruppen:ClaimToMemberProperty` and runs inside the built-in `MemberAutoLinkConfigurator`. For computed values or conditional mappings, write an `IPostLoginHandler` that mutates the member directly via `IMemberService`.

---

## 1. Anatomy of a post-login handler

```csharp
public interface IPostLoginHandler
{
    Task<PostLoginResult> HandleAsync(PostLoginContext context, CancellationToken ct);
}
```

`PostLoginContext` carries `HttpContext`, the `ClaimsPrincipal` about to be signed in, and the `AuthenticationProperties`. Mutations to `context.Principal` propagate into the cookie.

Result is one of:

- **`PostLoginResult.Continue`** — let the next handler run; if all return Continue, the cookie is issued and the user is redirected to `AuthenticationProperties.RedirectUri`.
- **`PostLoginResult.Abort("reason")`** — short-circuits the pipeline; the package responds with HTTP 403 and the cookie is not issued.
- **`PostLoginResult.StepUpRequired(scopes, returnUrl?)`** — short-circuits and triggers a second OIDC challenge with the given extra scopes (e.g. `["ssn"]` for CPR). Original return URL is preserved automatically.

Handlers run in registration order. The **first non-Continue result wins** and the remaining handlers are skipped.

---

## 2. Recipe: enrich the principal with a custom claim

```csharp
public sealed class CompanyClaimHandler : IPostLoginHandler
{
    public Task<PostLoginResult> HandleAsync(PostLoginContext ctx, CancellationToken ct)
    {
        var identity = (ClaimsIdentity)ctx.Principal.Identity!;
        if (identity.FindFirst("knowit.tenant") is null)
        {
            identity.AddClaim(new Claim("knowit.tenant", "akait"));
        }
        return Task.FromResult(PostLoginResult.Continue);
    }
}

// Register:
builder.Services.AddSingleton<IPostLoginHandler, CompanyClaimHandler>();
```

To persist the claim onto a member-type property, add `"knowit.tenant": "tenant"` to `Signaturgruppen.ClaimToMemberProperty` and ensure your member type has a `tenant` property alias.

---

## 3. Recipe: gate access based on broker claims

```csharp
public sealed class AdultsOnlyHandler : IPostLoginHandler
{
    public Task<PostLoginResult> HandleAsync(PostLoginContext ctx, CancellationToken ct)
    {
        var ageRaw = ctx.Principal.FindFirst(ClaimMaps.MitIdAge)?.Value
                    ?? ctx.Principal.FindFirst("mitid.age")?.Value;

        if (int.TryParse(ageRaw, out var age) && age < 18)
        {
            return Task.FromResult(PostLoginResult.Abort("Du skal være 18 år for at logge ind."));
        }
        return Task.FromResult(PostLoginResult.Continue);
    }
}
```

The Abort message is what the package surfaces with the 403; consider rendering a custom error page that reads `error_description` / status reason if your UI needs to show it.

---

## 4. Recipe: request additional scopes only when needed

```csharp
public sealed class CprOnDemandHandler : IPostLoginHandler
{
    private readonly IBusinessLookup _lookup;
    public CprOnDemandHandler(IBusinessLookup lookup) => _lookup = lookup;

    public async Task<PostLoginResult> HandleAsync(PostLoginContext ctx, CancellationToken ct)
    {
        var sub = ctx.Principal.FindFirst(ClaimMaps.Sub)!.Value;
        var known = await _lookup.HasMemberForSubjectAsync(sub, ct);
        if (known) return PostLoginResult.Continue;

        // Need CPR to resolve the member. Step-up.
        if (ctx.Principal.FindFirst(ClaimMaps.Cpr) is null)
        {
            return PostLoginResult.StepUpRequired(["ssn"]);
        }

        // We came back from step-up with CPR in hand — do the registration.
        var cpr = ctx.Principal.FindFirst(ClaimMaps.Cpr)!.Value;
        await _lookup.RegisterAsync(sub, cpr, ct);
        return PostLoginResult.Continue;
    }
}
```

**Always drop the raw CPR claim before returning Continue**, otherwise it ends up in the cookie:

```csharp
var identity = (ClaimsIdentity)ctx.Principal.Identity!;
foreach (var c in identity.FindAll(ClaimMaps.Cpr).ToArray())
{
    identity.RemoveClaim(c);
}
```

The bundled `AkaitMembershipResolver` is a working example — see `src/Knowit.Signaturgruppen.Umbraco.Akait/AkaitMembershipResolver.cs`.

---

## 5. Handler ordering and DI lifetimes

- The pipeline iterates `IPostLoginHandler` services in **registration order**.
- Built-in registrations (e.g. `AkaitMembershipResolver` from `AddAkaitMembershipResolver()`) register first. Your `services.AddSingleton<IPostLoginHandler, …>()` calls afterwards run after them.
- To force a handler to run **before** the AKAIT resolver, register it before calling `AddAkaitMembershipResolver(...)`, or `Insert(0, …)` the descriptor.
- Handlers receive a `PostLoginContext` that carries `HttpContext.RequestServices` — resolve scoped services from there if needed.
- The pipeline itself is **scoped**; handlers can be singleton, scoped, or transient. Prefer singleton for stateless logic and scoped when you need request-scoped dependencies (e.g. `IUmbracoContext`).

---

## 6. Reference: the claim type constants

```csharp
ClaimMaps.Sub                  // "sub"               — broker subject (pairwise per organisation)
ClaimMaps.MitIdUuid            // "mitid.uuid"        — MitID-internal stable id
ClaimMaps.MitIdIdentityName    // "mitid.identity_name"
ClaimMaps.MitIdDateOfBirth     // "mitid.date_of_birth"
ClaimMaps.Cpr                  // "dk.cpr"            — SENSITIVE; never log raw
ClaimMaps.Idp                  // "idp"
ClaimMaps.IdentityType         // "identity_type"
ClaimMaps.NebSessionId         // "neb_sid"
ClaimMaps.TransactionId        // "transaction_id"
```

Do not log `dk.cpr`, `ssn`, or `ssn.details.*` values. If your hook must persist a CPR-derived value (e.g. for member lookup), hash or encrypt it yourself — for example, HMAC-SHA256 with a key from your secret store — before writing it to a member property or any other persisted surface. The package itself does not persist CPR.
