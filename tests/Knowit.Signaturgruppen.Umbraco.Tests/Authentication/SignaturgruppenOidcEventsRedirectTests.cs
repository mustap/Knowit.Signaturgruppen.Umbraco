using System.Globalization;
using System.Text.Json;
using Knowit.Signaturgruppen.Umbraco.Authentication;
using Knowit.Signaturgruppen.Umbraco.Configuration;
using Knowit.Signaturgruppen.Umbraco.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Knowit.Signaturgruppen.Umbraco.Tests.Authentication;

public class SignaturgruppenOidcEventsRedirectTests
{
    [Fact]
    public async Task Sets_idp_values_and_idp_params_on_outbound_protocol_message()
    {
        var options = new SignaturgruppenOptions
        {
            MitId = { LevelOfAssurance = NsisLoa.High, ReferenceText = "Knowit AKAIT login" }
        };
        var sut = CreateSut(options);
        var context = CreateRedirectContext();

        await sut.RedirectToIdentityProvider(context);

        Assert.Equal("mitid", context.ProtocolMessage.GetParameter("idp_values"));
        var idpParams = context.ProtocolMessage.GetParameter("idp_params");
        Assert.False(string.IsNullOrEmpty(idpParams));
        using var doc = JsonDocument.Parse(idpParams!);
        Assert.Equal("high", doc.RootElement.GetProperty("mitid").GetProperty("loa_value").GetString());
    }

    [Fact]
    public async Task Sets_language_parameter_from_request_culture()
    {
        var sut = CreateSut(new SignaturgruppenOptions());
        var context = CreateRedirectContext();
        context.HttpContext.Features.Set<IRequestCultureFeature>(
            new RequestCultureFeature(
                new RequestCulture(new CultureInfo("da-DK"), new CultureInfo("da-DK")),
                provider: null));

        await sut.RedirectToIdentityProvider(context);

        Assert.Equal("da", context.ProtocolMessage.GetParameter("language"));
    }

    [Fact]
    public async Task Appends_extra_scopes_from_auth_properties()
    {
        var sut = CreateSut(new SignaturgruppenOptions());
        var context = CreateRedirectContext();
        context.ProtocolMessage.Scope = "openid mitid";
        context.Properties.Items[SignaturgruppenDefaults.ExtraScopesAuthPropertyKey] = "ssn";

        await sut.RedirectToIdentityProvider(context);

        Assert.Equal("openid mitid ssn", context.ProtocolMessage.Scope);
    }

    [Fact]
    public async Task Leaves_scope_unchanged_when_no_extra_scopes_property()
    {
        var sut = CreateSut(new SignaturgruppenOptions());
        var context = CreateRedirectContext();
        context.ProtocolMessage.Scope = "openid mitid";

        await sut.RedirectToIdentityProvider(context);

        Assert.Equal("openid mitid", context.ProtocolMessage.Scope);
    }

    private static SignaturgruppenOidcEvents CreateSut(SignaturgruppenOptions options)
    {
        var monitor = new StaticOptionsMonitor(options);
        return new SignaturgruppenOidcEvents(
            new DefaultIdpParamsBuilder(),
            monitor,
            NullLogger<SignaturgruppenOidcEvents>.Instance);
    }

    private static RedirectContext CreateRedirectContext()
    {
        var httpContext = new DefaultHttpContext();
        var scheme = new AuthenticationScheme("oidc", "oidc", typeof(OpenIdConnectHandler));
        var oidcOptions = new OpenIdConnectOptions();
        var properties = new AuthenticationProperties();
        var ctx = new RedirectContext(httpContext, scheme, oidcOptions, properties)
        {
            ProtocolMessage = new OpenIdConnectMessage()
        };
        return ctx;
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<SignaturgruppenOptions>
    {
        public StaticOptionsMonitor(SignaturgruppenOptions value) => CurrentValue = value;
        public SignaturgruppenOptions CurrentValue { get; }
        public SignaturgruppenOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<SignaturgruppenOptions, string?> listener) => null;
    }
}
