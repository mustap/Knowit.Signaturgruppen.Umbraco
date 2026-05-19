using System.Security.Claims;
using Knowit.Signaturgruppen.Umbraco.Authentication;
using Knowit.Signaturgruppen.Umbraco.Configuration;
using Knowit.Signaturgruppen.Umbraco.Members;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Security;

namespace Knowit.Signaturgruppen.Umbraco.Tests.Members;

public class MemberAutoLinkConfiguratorOnAutoLinkingTests
{
    [Fact]
    public void Sets_name_from_mitid_identity_name_claim_when_present()
    {
        var sut = CreateSut();
        var user = NewUser();
        var info = NewExternalLoginInfo(
            ("sub", "abc-123"),
            (ClaimMaps.MitIdIdentityName, "Alice Andersen"));

        sut.OnAutoLinking(user, info);

        Assert.Equal("Alice Andersen", user.Name);
    }

    [Fact]
    public void Falls_back_to_claimtypes_name_when_mitid_identity_name_missing()
    {
        var sut = CreateSut();
        var user = NewUser();
        var info = NewExternalLoginInfo(
            ("sub", "abc-123"),
            (ClaimTypes.Name, "Fallback Name"));

        sut.OnAutoLinking(user, info);

        Assert.Equal("Fallback Name", user.Name);
    }

    [Fact]
    public void Synthesises_email_and_username_from_sub()
    {
        var sut = CreateSut();
        var user = NewUser();
        var info = NewExternalLoginInfo(
            ("sub", "abc-123"),
            (ClaimMaps.MitIdIdentityName, "Alice Andersen"));

        sut.OnAutoLinking(user, info);

        Assert.Equal($"abc-123@{MemberAutoLinkConfigurator.SyntheticEmailDomain}", user.Email);
        Assert.Equal(user.Email, user.UserName);
    }

    [Fact]
    public void Falls_back_to_provider_key_when_sub_claim_missing()
    {
        var sut = CreateSut();
        var user = NewUser();
        var info = NewExternalLoginInfo(providerKey: "fallback-key");

        sut.OnAutoLinking(user, info);

        Assert.Equal($"fallback-key@{MemberAutoLinkConfigurator.SyntheticEmailDomain}", user.Email);
    }

    private static MemberAutoLinkConfigurator CreateSut()
    {
        return new MemberAutoLinkConfigurator(
            new StaticOptionsMonitor(new SignaturgruppenOptions()),
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MemberAutoLinkConfigurator>.Instance);
    }

    private static MemberIdentityUser NewUser()
        => new();

    private static ExternalLoginInfo NewExternalLoginInfo(
        params (string Type, string Value)[] claims)
        => NewExternalLoginInfo("provider-key-default", claims);

    private static ExternalLoginInfo NewExternalLoginInfo(string providerKey, params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test");
        return new ExternalLoginInfo(new ClaimsPrincipal(identity), "Signaturgruppen", providerKey, "Signaturgruppen");
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<SignaturgruppenOptions>
    {
        public StaticOptionsMonitor(SignaturgruppenOptions value) => CurrentValue = value;
        public SignaturgruppenOptions CurrentValue { get; }
        public SignaturgruppenOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<SignaturgruppenOptions, string?> listener) => null;
    }
}
