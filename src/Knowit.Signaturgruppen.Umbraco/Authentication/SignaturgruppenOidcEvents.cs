using Knowit.Signaturgruppen.Umbraco.Configuration;
using Knowit.Signaturgruppen.Umbraco.Constants;
using Knowit.Signaturgruppen.Umbraco.Pipeline;
using Knowit.Signaturgruppen.Umbraco.StepUp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knowit.Signaturgruppen.Umbraco.Authentication;

public class SignaturgruppenOidcEvents : OpenIdConnectEvents
{
    private readonly DefaultIdpParamsBuilder _idpParamsBuilder;
    private readonly IOptionsMonitor<SignaturgruppenOptions> _options;
    private readonly ILogger<SignaturgruppenOidcEvents> _logger;

    public SignaturgruppenOidcEvents(
        DefaultIdpParamsBuilder idpParamsBuilder,
        IOptionsMonitor<SignaturgruppenOptions> options,
        ILogger<SignaturgruppenOidcEvents> logger)
    {
        _idpParamsBuilder = idpParamsBuilder;
        _options = options;
        _logger = logger;
    }

    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        var options = _options.CurrentValue;

        context.ProtocolMessage.SetParameter("idp_values", "mitid");
        context.ProtocolMessage.SetParameter("idp_params", _idpParamsBuilder.Build(context.HttpContext, options));

        var culture = context.HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture;
        if (culture is not null)
        {
            context.ProtocolMessage.SetParameter("language", culture.TwoLetterISOLanguageName);
        }

        if (context.Properties.Items.TryGetValue(SignaturgruppenDefaults.ExtraScopesAuthPropertyKey, out var extra)
            && !string.IsNullOrEmpty(extra))
        {
            context.ProtocolMessage.Scope = string.IsNullOrEmpty(context.ProtocolMessage.Scope)
                ? extra
                : $"{context.ProtocolMessage.Scope} {extra}";
        }

        return base.RedirectToIdentityProvider(context);
    }

    public override async Task TicketReceived(TicketReceivedContext context)
    {
        if (context.Principal is null)
        {
            await base.TicketReceived(context);
            return;
        }

        var pipeline = context.HttpContext.RequestServices.GetService<PostLoginPipeline>();
        if (pipeline is null)
        {
            await base.TicketReceived(context);
            return;
        }

        var postLoginContext = new PostLoginContext(context.HttpContext, context.Principal, context.Properties);
        var result = await pipeline.RunAsync(postLoginContext, context.HttpContext.RequestAborted);

        switch (result.Kind)
        {
            case PostLoginResultKind.Continue:
                await base.TicketReceived(context);
                return;

            case PostLoginResultKind.Abort:
                _logger.LogWarning("Post-login pipeline aborted sign-in: {Reason}", result.AbortReason);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.HandleResponse();
                return;

            case PostLoginResultKind.StepUpRequired:
                var returnUrl = result.StepUpReturnUrl
                    ?? context.Properties?.RedirectUri
                    ?? "/";
                var stepUp = context.HttpContext.RequestServices.GetRequiredService<ISignaturgruppenStepUpService>();
                await stepUp.ChallengeAsync(context.HttpContext, result.StepUpScopes!, returnUrl);
                context.HandleResponse();
                return;

            default:
                await base.TicketReceived(context);
                return;
        }
    }

    public override Task RemoteFailure(RemoteFailureContext context)
    {
        _logger.LogWarning(context.Failure, "Signaturgruppen remote failure: {Error}", context.Failure?.Message);
        return base.RemoteFailure(context);
    }
}
