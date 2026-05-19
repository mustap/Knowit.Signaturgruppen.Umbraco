using Knowit.Signaturgruppen.Umbraco.Constants;
using Knowit.Signaturgruppen.Umbraco.Members;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Web.Common.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Knowit.Signaturgruppen.Umbraco.DependencyInjection;

/// <summary>
/// Attaches <see cref="MemberAutoLinkConfigurator"/>'s callbacks to the AutoLinkOptions
/// created earlier in the options pipeline. Runs as IPostConfigure so the AutoLinkOptions
/// instance already exists; we only set the OnAutoLinking / OnExternalLogin delegates.
/// </summary>
internal sealed class MemberAutoLinkCallbacksPostConfigurator
    : IPostConfigureOptions<MemberExternalLoginProviderOptions>
{
    private static readonly string SchemeName =
        UmbracoConstants.Security.MemberExternalAuthenticationTypePrefix + SignaturgruppenDefaults.AuthenticationScheme;

    private readonly MemberAutoLinkConfigurator _configurator;

    public MemberAutoLinkCallbacksPostConfigurator(MemberAutoLinkConfigurator configurator)
        => _configurator = configurator;

    public void PostConfigure(string? name, MemberExternalLoginProviderOptions options)
    {
        if (!string.Equals(name, SchemeName, StringComparison.Ordinal))
        {
            return;
        }

        if (options.AutoLinkOptions is null)
        {
            return;
        }

        options.AutoLinkOptions.OnAutoLinking = _configurator.OnAutoLinking;
        options.AutoLinkOptions.OnExternalLogin = _configurator.OnExternalLogin;
    }
}
