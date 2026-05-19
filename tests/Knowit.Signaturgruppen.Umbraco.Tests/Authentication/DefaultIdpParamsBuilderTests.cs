using System.Text;
using System.Text.Json;
using Knowit.Signaturgruppen.Umbraco.Authentication;
using Knowit.Signaturgruppen.Umbraco.Configuration;
using Microsoft.AspNetCore.Http;

namespace Knowit.Signaturgruppen.Umbraco.Tests.Authentication;

public class DefaultIdpParamsBuilderTests
{
    private static readonly DefaultIdpParamsBuilder Sut = new();

    [Fact]
    public void Wraps_mitid_payload_under_mitid_key()
    {
        var json = Sut.Build(new DefaultHttpContext(), new SignaturgruppenOptions());
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("mitid").ValueKind);
    }

    [Theory]
    [InlineData(NsisLoa.Low, "low")]
    [InlineData(NsisLoa.Substantial, "substantial")]
    [InlineData(NsisLoa.High, "high")]
    public void Maps_loa_to_broker_string(NsisLoa loa, string expected)
    {
        var options = new SignaturgruppenOptions();
        options.MitId.LevelOfAssurance = loa;

        var json = Sut.Build(new DefaultHttpContext(), options);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(expected, doc.RootElement.GetProperty("mitid").GetProperty("loa_value").GetString());
    }

    [Fact]
    public void Base64_encodes_reference_text()
    {
        var options = new SignaturgruppenOptions();
        options.MitId.ReferenceText = "Login til AKAIT";
        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("Login til AKAIT"));

        var json = Sut.Build(new DefaultHttpContext(), options);
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(expected, doc.RootElement.GetProperty("mitid").GetProperty("reference_text").GetString());
    }

    [Fact]
    public void Omits_optional_fields_when_unset()
    {
        var options = new SignaturgruppenOptions();
        options.MitId.EnableAppSwitch = false;
        options.MitId.ReferenceText = null;
        options.MitId.UuidHint = null;
        options.MitId.CprHint = null;

        var json = Sut.Build(new DefaultHttpContext(), options);
        using var doc = JsonDocument.Parse(json);
        var mitid = doc.RootElement.GetProperty("mitid");

        Assert.False(mitid.TryGetProperty("reference_text", out _));
        Assert.False(mitid.TryGetProperty("enable_app_switch", out _));
        Assert.False(mitid.TryGetProperty("uuid_hint", out _));
        Assert.False(mitid.TryGetProperty("cpr_hint", out _));
    }
}
