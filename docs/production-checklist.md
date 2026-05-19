# Production checklist — Knowit.Signaturgruppen.Umbraco

Work through this list before pointing real users at the package. Items marked **[blocker]** will cause incidents in production if you ship without them.

---

## 1. Broker (Signaturgruppen / Nets eID Broker)

- [ ] **[blocker]** `Signaturgruppen:Environment` is set to `Production`. The default is `Preproduction`.
- [ ] **[blocker]** Production `client_id` / `client_secret` issued by Signaturgruppen are stored in user-secrets / Key Vault / environment variables — **never** in `appsettings.json` or source control.
- [ ] Production `redirect_uri` (your real public host + `/sg/login/callback`) is whitelisted in the broker admin UI.
- [ ] Production `post_logout_redirect_uri` (your real public host + `/sg/logout/callback`) is whitelisted.
- [ ] You verified the actual public origin matches what is whitelisted — including `https://` and (if applicable) port. Mismatches return `invalid_request` from the broker.
- [ ] You picked a `LevelOfAssurance` your security review approved (`Substantial` is the broker default; `High` requires explicit configuration on the broker side).

## 2. AKAIT (only if you wired `AddAkaitMembershipResolver`)

- [ ] **[blocker]** Production AKAIT `ClientId` / `ClientSecret` and `TokenUrl` are stored as secrets.
- [ ] `Akait:BaseUrl` points to production (not `apiprodtest.*`).
- [ ] You tested the AKAIT happy path end-to-end against the **test environment** with a real test CPR present in the AKAIT member database.
- [ ] You tested the AKAIT 404 path (subject unknown → step-up triggers).
- [ ] You tested the AKAIT 400 path (CPR not in member database → user-visible Danish error message bubbles up).

## 3. Secrets management

- [ ] No `REPLACE_VIA_USER_SECRETS` / `REPLACE_WITH_*` placeholders remain in any deployed `appsettings.json`.
- [ ] `.gitignore` excludes `appsettings.Development.json` if it ever holds real credentials.
- [ ] Secret rotation procedure documented for: broker client_secret, AKAIT client_secret, DataProtection key ring.

## 4. HTTPS and cookie security

- [ ] Site is served exclusively over HTTPS in production. HTTP requests are rejected or redirected to HTTPS.
- [ ] HSTS is configured with a reasonable `max-age` (≥ 12 months once you are sure HTTPS is stable).
- [ ] The Umbraco / ASP.NET Core cookie policy is set so the **member auth cookie is `Secure`, `HttpOnly`, `SameSite=Lax`**.

## 5. ASP.NET Core DataProtection

- [ ] **[blocker]** DataProtection keys are persisted to durable storage (file share, Azure Blob, Redis, Key Vault). Default ephemeral keys are wiped on every restart, invalidating the OIDC correlation/nonce cookies and the cookie auth ticket.
- [ ] All instances of the app share the same DataProtection key ring (otherwise step-up cookies issued by instance A cannot be decoded by instance B).
- [ ] The DataProtection key ring is itself protected at rest (encryption with a separate KMS key is preferred).

## 6. Umbraco reserved paths

- [ ] `Umbraco:CMS:Global:ReservedPaths` includes `/sg/login/callback/`, `/sg/logout/callback/`, `/sg/step-up/callback/`. The package's `IPostConfigureOptions<GlobalSettings>` adds these automatically; if you override `ReservedPaths` in config, re-add them manually.

## 7. Member type and property aliases

- [ ] A member type matching `Signaturgruppen:AutoLink:MemberType` (default `Member`) exists in the target Umbraco environment.
- [ ] All aliases listed as values in `Signaturgruppen:ClaimToMemberProperty` exist as property aliases on the member type. Missing aliases silently no-op.
- [ ] No member-type property stores raw CPR. If you need it persistently, hash or encrypt it via your own infrastructure (e.g. HMAC with a key from Key Vault) inside a custom mapper or post-login handler before writing.

## 8. Logging and redaction

- [ ] Logs are emitted to a sink that supports redaction and access control (Application Insights, Datadog, Elastic with field-level masking, etc.).
- [ ] Any custom log statements you wrote scrub sensitive claim values (`dk.cpr`, `ssn`, `ssn.details.*`) before emitting — either by not logging them at all, or by routing through a sink-level masking rule.
- [ ] Log level for `Knowit.Signaturgruppen` is `Information` or `Warning` in production — `Debug` may expose sensitive context.
- [ ] You searched the log stream for `dk.cpr`, `ssn.details`, and raw 10-digit number patterns after a smoke test, and confirmed nothing leaked.

## 9. GDPR / Datatilsynet

- [ ] You have a documented legal basis for processing CPR — typically Databeskyttelseslovens § 11 with stk. 2 nr. 4 (necessary for an authority) or stk. 2 nr. 3 (consent). Consult legal.
- [ ] The privacy notice on the site mentions CPR use, retention, and that processing is delegated to AKAIT (if applicable).
- [ ] Member data retention: there is a documented schedule for deleting Umbraco member records and the protected CPR hash for inactive members.
- [ ] **Human-in-the-loop:** any access decision your `IPostLoginHandler` makes (e.g. `Abort` based on age or assurance level) is a documented business rule, not an automated profiling decision in the EU AI Act sense. If the decision has legal effect, a human appeal channel exists.

## 10. Performance and caching

- [ ] AKAIT token endpoint is reachable from production network. `IdentityModel.AspNetCore`'s `AddClientAccessTokenHandler("akait")` caches the access token in `IDistributedCache` (in-memory by default) until ~60 seconds before expiry and refreshes automatically on 401, so a token-endpoint outage shorter than the remaining lifetime is invisible.
- [ ] The AKAIT typed `HttpClient` has the standard resilience handler enabled (default in `AddAkaitMembershipResolver`); verify retry budgets do not amplify a broker outage.
- [ ] Login flow latency (broker round-trip + AKAIT lookup + step-up if needed) was measured under load. Expect 1–3 seconds end-to-end for the happy path, +1–2 seconds for step-up.

## 11. Observability

- [ ] You can answer: "How many logins succeeded / failed / triggered step-up / were aborted in the last hour?" — wire metrics to a dashboard.
- [ ] Alert on: `Signaturgruppen remote failure: …` log warning frequency above baseline, AKAIT `AkaitValidationException` rate, post-step-up abort rate (broker not returning CPR).
- [ ] Health check endpoint that exercises `IClientAccessTokenManagement.GetAccessTokenAsync("akait")` (cheap, exercises the AKAIT client-credentials path) if AKAIT is wired.

## 12. Smoke tests before flipping DNS

- [ ] Happy path login: anonymous → `/sg/login/callback` → cookie issued → expected return URL.
- [ ] Step-up: 404 from membership lookup → broker prompts only for CPR → cookie issued → `medlemsnummer` claim is present and persisted to the member.
- [ ] Logout: `id_token_hint` is sent to `/op/connect/endsession`; broker session terminated; member cookie cleared; redirect to whitelisted `post_logout_redirect_uri`.
- [ ] Cancellation: user clicks "Cancel" in MitID → broker returns `access_denied` → user is shown a friendly error, not a stack trace.
- [ ] Tampered step-up return URL: change `return_url` in the address bar to an external host → callback redirects to `/` instead of the manipulated URL.

## 13. Disaster recovery

- [ ] DataProtection key ring is backed up.
- [ ] CPR HMAC key is backed up in a sealed envelope / second secrets store. Losing it irrevocably loses the ability to lookup existing members by CPR.
- [ ] Database backup includes the `cmsMember` + `cmsExternalLogin` tables; restore drills tested.

## 14. EU AI Act / governance

- [ ] No automated decision affecting a natural person is taken without a human review path. The package itself does not make such decisions; any `IPostLoginHandler` you wrote that denies access counts and must have an appeal/override channel.
