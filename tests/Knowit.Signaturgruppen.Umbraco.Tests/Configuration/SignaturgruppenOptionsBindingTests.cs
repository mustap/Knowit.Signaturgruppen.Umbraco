using Knowit.Signaturgruppen.Umbraco.Configuration;
using Microsoft.Extensions.Configuration;

namespace Knowit.Signaturgruppen.Umbraco.Tests.Configuration;

public class SignaturgruppenOptionsBindingTests
{
    [Fact]
    public void Binds_nested_sections_from_configuration()
    {
        var data = new Dictionary<string, string?>
        {
            ["Signaturgruppen:Environment"] = "Production",
            ["Signaturgruppen:ClientId"] = "test-client",
            ["Signaturgruppen:ClientSecret"] = "test-secret",
            ["Signaturgruppen:Scopes:0"] = "openid",
            ["Signaturgruppen:Scopes:1"] = "mitid",
            ["Signaturgruppen:MitId:LevelOfAssurance"] = "High",
            ["Signaturgruppen:MitId:EnableAppSwitch"] = "false",
            ["Signaturgruppen:AutoLink:Enabled"] = "true",
            ["Signaturgruppen:AutoLink:MemberType"] = "CustomMember"
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        var options = new SignaturgruppenOptions();
        config.GetSection(SignaturgruppenOptions.SectionName).Bind(options);

        Assert.Equal(SignaturgruppenEnvironment.Production, options.Environment);
        Assert.Equal("test-client", options.ClientId);
        Assert.Equal("test-secret", options.ClientSecret);
        Assert.Equal(new[] { "openid", "mitid" }, options.Scopes);
        Assert.Equal(NsisLoa.High, options.MitId.LevelOfAssurance);
        Assert.False(options.MitId.EnableAppSwitch);
        Assert.True(options.AutoLink.Enabled);
        Assert.Equal("CustomMember", options.AutoLink.MemberType);
    }

    [Fact]
    public void Defaults_are_sane_when_section_is_empty()
    {
        var config = new ConfigurationBuilder().Build();
        var options = new SignaturgruppenOptions();
        config.GetSection(SignaturgruppenOptions.SectionName).Bind(options);

        Assert.Equal(SignaturgruppenEnvironment.Preproduction, options.Environment);
        Assert.Equal(NsisLoa.Substantial, options.MitId.LevelOfAssurance);
        Assert.True(options.MitId.EnableAppSwitch);
        Assert.True(options.AutoLink.Enabled);
        Assert.Equal("Member", options.AutoLink.MemberType);
        Assert.Empty(options.Scopes);
        Assert.Equal(new[] { "openid", "mitid" }, SignaturgruppenOptions.DefaultScopes);
    }
}
