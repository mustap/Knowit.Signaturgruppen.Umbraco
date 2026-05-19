using Knowit.Signaturgruppen.Umbraco.Configuration;
using Knowit.Signaturgruppen.Umbraco.Constants;

namespace Knowit.Signaturgruppen.Umbraco.Tests.Configuration;

public class SignaturgruppenEnvironmentTests
{
    [Fact]
    public void Production_resolves_to_netseidbroker_op()
    {
        Assert.Equal(SignaturgruppenDefaults.ProductionAuthority,
            SignaturgruppenEnvironment.Production.ResolveAuthority(null));
    }

    [Fact]
    public void Preproduction_resolves_to_pp_netseidbroker_op()
    {
        Assert.Equal(SignaturgruppenDefaults.PreproductionAuthority,
            SignaturgruppenEnvironment.Preproduction.ResolveAuthority(null));
    }

    [Fact]
    public void Custom_requires_explicit_authority()
    {
        Assert.Throws<InvalidOperationException>(
            () => SignaturgruppenEnvironment.Custom.ResolveAuthority(null));
        Assert.Throws<InvalidOperationException>(
            () => SignaturgruppenEnvironment.Custom.ResolveAuthority("  "));
    }

    [Fact]
    public void Custom_returns_provided_authority()
    {
        Assert.Equal("https://example.test/op",
            SignaturgruppenEnvironment.Custom.ResolveAuthority("https://example.test/op"));
    }
}
