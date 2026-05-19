using Knowit.Signaturgruppen.Umbraco.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Knowit.Signaturgruppen.Umbraco.StepUp;

internal sealed class SignaturgruppenStepUpService : ISignaturgruppenStepUpService
{
    internal const string ReturnUrlQueryKey = "return_url";

    private static readonly string SchemeName =
        UmbracoConstants.Security.MemberExternalAuthenticationTypePrefix + SignaturgruppenDefaults.AuthenticationScheme;

    private readonly ILogger<SignaturgruppenStepUpService> _logger;

    public SignaturgruppenStepUpService(ILogger<SignaturgruppenStepUpService> logger)
        => _logger = logger;

    public Task ChallengeAsync(HttpContext httpContext, IReadOnlyCollection<string> extraScopes, string returnUrl)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(extraScopes);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnUrl);

        if (extraScopes.Count == 0)
        {
            throw new ArgumentException("At least one extra scope is required for step-up.", nameof(extraScopes));
        }

        // Round-trip the original return URL via a query string on the OIDC RedirectUri:
        // after sign-in completes, OpenIdConnectHandler redirects the browser to
        // /sg/step-up/callback?return_url=…, which is the only signal the callback
        // controller needs. The step-up itself is detected by ExtraScopesAuthPropertyKey
        // being set on AuthenticationProperties.Items.
        var redirectUri = QueryHelpers.AddQueryString(
            SignaturgruppenDefaults.StepUpCallbackPath,
            ReturnUrlQueryKey,
            returnUrl);

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };
        properties.Items[SignaturgruppenDefaults.ExtraScopesAuthPropertyKey] = string.Join(' ', extraScopes);

        _logger.LogInformation(
            "Triggering Signaturgruppen step-up; extra scopes {ScopeCount}", extraScopes.Count);

        return httpContext.ChallengeAsync(SchemeName, properties);
    }
}
