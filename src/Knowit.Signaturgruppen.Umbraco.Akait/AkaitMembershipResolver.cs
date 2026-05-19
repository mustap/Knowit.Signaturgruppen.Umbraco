using System.Security.Claims;
using Knowit.Signaturgruppen.Umbraco.Authentication;
using Knowit.Signaturgruppen.Umbraco.Constants;
using Knowit.Signaturgruppen.Umbraco.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knowit.Signaturgruppen.Umbraco.Akait;

public sealed class AkaitMembershipResolver : IPostLoginHandler
{
    private readonly IAkaitMembershipClient _client;
    private readonly IOptionsMonitor<AkaitApiOptions> _options;
    private readonly ILogger<AkaitMembershipResolver> _logger;

    public AkaitMembershipResolver(
        IAkaitMembershipClient client,
        IOptionsMonitor<AkaitApiOptions> options,
        ILogger<AkaitMembershipResolver> logger)
    {
        _client = client;
        _options = options;
        _logger = logger;
    }

    public async Task<PostLoginResult> HandleAsync(PostLoginContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var sub = context.Principal.FindFirst(ClaimMaps.Sub)?.Value;
        if (string.IsNullOrEmpty(sub))
        {
            _logger.LogWarning("AKAIT resolver skipped: no 'sub' claim on principal.");
            return PostLoginResult.Continue;
        }

        var existing = await _client.FindPersonMedSubjectAsync(sub, ct);
        if (!string.IsNullOrEmpty(existing))
        {
            AddMedlemsnummerClaim(context.Principal, existing);
            _logger.LogInformation("AKAIT subject mapped to existing medlemsnummer.");
            return PostLoginResult.Continue;
        }

        var cpr = context.Principal.FindFirst(ClaimMaps.Cpr)?.Value;
        if (string.IsNullOrEmpty(cpr))
        {
            // When ChallengeAsync ran for step-up, the step-up service set this property item on
            // AuthenticationProperties, and OpenIdConnectHandler round-trips it through the broker
            // via the OIDC `state` parameter. If we see it on the current ticket we are completing
            // a step-up callback. No CPR after the broker honored (or silently dropped) `ssn`
            // means retrying would loop — the most common cause is the broker client not being
            // whitelisted for the ssn scope.
            if (context.Properties?.Items.ContainsKey(SignaturgruppenDefaults.ExtraScopesAuthPropertyKey) == true)
            {
                _logger.LogError(
                    "AKAIT subject unknown after CPR step-up: broker did not return a {Claim} claim. "
                    + "Verify that the ssn scope is whitelisted on this broker client.",
                    ClaimMaps.Cpr);
                return PostLoginResult.Abort($"Step-up did not yield a {ClaimMaps.Cpr} claim from the broker.");
            }

            _logger.LogInformation("AKAIT subject unknown; requesting CPR step-up.");
            return PostLoginResult.StepUpRequired(["ssn"]);
        }

        try
        {
            try
            {
                await _client.RegistrerSubjectForPersonAsync(sub, cpr, ct);
            }
            catch (AkaitValidationException ex)
            {
                _logger.LogWarning("AKAIT rejected subject registration: {Reason}", ex.Message);
                return PostLoginResult.Abort($"AKAIT registration failed: {ex.Message}");
            }

            var nowMapped = await _client.FindPersonMedSubjectAsync(sub, ct);
            if (!string.IsNullOrEmpty(nowMapped))
            {
                AddMedlemsnummerClaim(context.Principal, nowMapped);
                _logger.LogInformation("AKAIT subject registered and mapped to medlemsnummer.");
                return PostLoginResult.Continue;
            }

            _logger.LogError("AKAIT did not return a medlemsnummer after a successful subject registration.");
            return PostLoginResult.Abort("AKAIT did not return a medlemsnummer after successful subject registration.");
        }
        finally
        {
            RemoveCprClaim(context.Principal);
        }
    }

    private void AddMedlemsnummerClaim(ClaimsPrincipal principal, string medlemsnummer)
    {
        if (principal.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var claimType = _options.CurrentValue.MedlemsnummerClaimType;
        var existing = identity.FindFirst(claimType);
        if (existing is not null)
        {
            identity.RemoveClaim(existing);
        }
        identity.AddClaim(new Claim(claimType, medlemsnummer));
    }

    private static void RemoveCprClaim(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        foreach (var claim in identity.FindAll(ClaimMaps.Cpr).ToArray())
        {
            identity.RemoveClaim(claim);
        }
    }
}
