using Knowit.Signaturgruppen.Umbraco.Authentication;
using Knowit.Signaturgruppen.Umbraco.Configuration;
using Knowit.Signaturgruppen.Umbraco.Constants;
using Knowit.Signaturgruppen.Umbraco.Members;
using Knowit.Signaturgruppen.Umbraco.Pipeline;
using Knowit.Signaturgruppen.Umbraco.StepUp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Extensions;

namespace Knowit.Signaturgruppen.Umbraco.DependencyInjection;

public static class UmbracoBuilderExtensions
{
    public static IUmbracoBuilder AddSignaturgruppen(
        this IUmbracoBuilder builder,
        Action<SignaturgruppenOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<SignaturgruppenOptions>()
            .Bind(builder.Config.GetSection(SignaturgruppenOptions.SectionName));

        if (configure is not null)
        {
            builder.Services.PostConfigure(configure);
        }

        builder.Services.AddSingleton<DefaultIdpParamsBuilder>();
        builder.Services.AddTransient<SignaturgruppenOidcEvents>();
        builder.Services
            .AddSingleton<IPostConfigureOptions<GlobalSettings>, SignaturgruppenReservedPathsConfigurator>();

        builder.Services.AddSingleton<MemberAutoLinkConfigurator>();
        builder.Services
            .AddSingleton<IPostConfigureOptions<MemberExternalLoginProviderOptions>, MemberAutoLinkCallbacksPostConfigurator>();

        builder.Services.AddSingleton<ISignaturgruppenStepUpService, SignaturgruppenStepUpService>();

        builder.Services.AddScoped<PostLoginPipeline>();

        builder.AddMemberExternalLogins(logins =>
        {
            logins.AddMemberLogin(
                memberAuth =>
                {
                    var schemeName = memberAuth.SchemeForMembers(SignaturgruppenDefaults.AuthenticationScheme);

                    memberAuth.AddOpenIdConnect(
                        schemeName,
                        SignaturgruppenDefaults.DisplayName,
                        oidc =>
                        {
                            var snapshot = new SignaturgruppenOptions();
                            builder.Config.GetSection(SignaturgruppenOptions.SectionName).Bind(snapshot);
                            configure?.Invoke(snapshot);
                            ConfigureOpenIdConnect(oidc, snapshot);
                        });
                },
                loginProviderOptions =>
                {
                    var snapshot = new SignaturgruppenOptions();
                    builder.Config.GetSection(SignaturgruppenOptions.SectionName).Bind(snapshot);
                    configure?.Invoke(snapshot);

                    loginProviderOptions.AutoLinkOptions = new MemberExternalSignInAutoLinkOptions(
                        autoLinkExternalAccount: snapshot.AutoLink.Enabled,
                        defaultIsApproved: snapshot.AutoLink.IsApproved,
                        defaultMemberTypeAlias: snapshot.AutoLink.MemberType,
                        defaultCulture: snapshot.AutoLink.DefaultCulture);
                });
        });

        return builder;
    }

    private static void ConfigureOpenIdConnect(
        OpenIdConnectOptions oidc,
        SignaturgruppenOptions sg)
    {
        oidc.Authority = sg.ResolveAuthority();
        oidc.ClientId = sg.ClientId;
        oidc.ClientSecret = sg.ClientSecret;
        oidc.CallbackPath = sg.CallbackPath;
        oidc.SignedOutCallbackPath = sg.SignedOutCallbackPath;
        oidc.ResponseType = OpenIdConnectResponseType.Code;
        oidc.UsePkce = true;
        oidc.SaveTokens = true;
        oidc.GetClaimsFromUserInfoEndpoint = true;
        oidc.MapInboundClaims = false;
        oidc.TokenValidationParameters.NameClaimType = "name";

        oidc.Scope.Clear();
        var scopes = sg.Scopes.Count > 0 ? (IEnumerable<string>)sg.Scopes : SignaturgruppenOptions.DefaultScopes;
        foreach (var scope in scopes)
        {
            oidc.Scope.Add(scope);
        }

        // The default ClaimActions list only contains DeleteClaim entries, so userinfo JSON keys
        // never reach the principal. Map every Signaturgruppen claim the package itself reads,
        // plus every caller-configured ClaimToMemberProperty key, so step-up CPR and member-type
        // property projection both work.
        var mapped = new HashSet<string>(StringComparer.Ordinal)
        {
            ClaimMaps.Sub,
            ClaimMaps.MitIdUuid,
            ClaimMaps.MitIdIdentityName,
            ClaimMaps.MitIdDateOfBirth,
            ClaimMaps.Cpr,
            ClaimMaps.Idp,
            ClaimMaps.IdentityType,
            ClaimMaps.NebSessionId,
            ClaimMaps.TransactionId,
        };
        foreach (var key in sg.ClaimToMemberProperty.Keys)
        {
            mapped.Add(key);
        }
        foreach (var key in mapped)
        {
            oidc.ClaimActions.MapUniqueJsonKey(key, key);
        }

        oidc.EventsType = typeof(SignaturgruppenOidcEvents);
    }
}
