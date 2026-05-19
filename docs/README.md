# Docs — Knowit.Signaturgruppen.Umbraco

Documentation for the **Knowit.Signaturgruppen.Umbraco** package and its AKAIT sibling. Pick the doc that matches what you are trying to do.

| Doc | When to read it |
|---|---|
| **[Getting started](getting-started.md)** | First-time install. Walks through prerequisites, NuGet install, `Program.cs` wiring, `appsettings.json` shape, member-type bootstrap, login Razor snippet, and first-run troubleshooting. |
| **[Hooks cookbook](hooks-cookbook.md)** | You want to extend the package. Recipes for `IPostLoginHandler` plus handler ordering and DI lifetime guidance. |
| **[Production checklist](production-checklist.md)** | You are about to go live. Section gate covering broker setup, AKAIT, secrets, HTTPS, DataProtection key ring, member-type aliases, logging redaction, GDPR / Datatilsynet, smoke tests, and AI-Act-relevant decision paths. |
| **[Architecture](ARCHITECTURE.md)** | You want to understand the design, extension points, or why a particular decision was made. Source of truth for solution layout, configuration surface, authentication pipeline (standard + step-up), GDPR stance, and the phased implementation plan. |

---

## Quick map by question

- *"How do I install this?"* → [Getting started §2–4](getting-started.md#2-install)
- *"Where does the login button go?"* → [Getting started §6](getting-started.md#6-add-a-login-button)
- *"How do I add a custom claim to the member's cookie?"* → [Hooks cookbook §2](hooks-cookbook.md#2-recipe-enrich-the-principal-with-a-custom-claim)
- *"How do I block under-18s from logging in?"* → [Hooks cookbook §3](hooks-cookbook.md#3-recipe-gate-access-based-on-broker-claims)
- *"What does the step-up flow look like?"* → [Architecture §6.2](ARCHITECTURE.md#62-step-up-cpr--scope-elevation)
- *"How do I store CPR safely?"* → [Architecture §8](ARCHITECTURE.md#8--gdpr--cpr-handling) + [Hooks cookbook §5](hooks-cookbook.md#5-recipe-custom-icprprotector-eg-azure-key-vault-hmac)
- *"What MUST I check before going to production?"* → [Production checklist](production-checklist.md), every item marked **[blocker]**.
