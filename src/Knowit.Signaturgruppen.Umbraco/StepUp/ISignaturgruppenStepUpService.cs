using Microsoft.AspNetCore.Http;

namespace Knowit.Signaturgruppen.Umbraco.StepUp;

public interface ISignaturgruppenStepUpService
{
    /// <summary>
    /// Issues an OIDC challenge against Signaturgruppen with additional scopes, reusing the
    /// broker's existing SSO session. Stores resume state in a DataProtection-protected
    /// cookie; the broker callback returns to <c>/sg/step-up/callback</c>, which finally
    /// redirects the browser to <paramref name="returnUrl"/>.
    /// </summary>
    /// <param name="httpContext">Current HTTP context (must support authentication challenges).</param>
    /// <param name="extraScopes">Scopes to add to the OIDC request (e.g. <c>"ssn"</c>).</param>
    /// <param name="returnUrl">Where to redirect the browser after a successful step-up.</param>
    Task ChallengeAsync(HttpContext httpContext, IReadOnlyCollection<string> extraScopes, string returnUrl);
}
