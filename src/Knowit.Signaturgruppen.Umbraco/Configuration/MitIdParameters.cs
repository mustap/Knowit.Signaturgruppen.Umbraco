namespace Knowit.Signaturgruppen.Umbraco.Configuration;

public sealed class MitIdParameters
{
    public NsisLoa LevelOfAssurance { get; set; } = NsisLoa.Substantial;

    public string? ReferenceText { get; set; }

    public bool EnableAppSwitch { get; set; } = true;

    public bool RequirePsd2 { get; set; }

    public string? UuidHint { get; set; }

    public string? CprHint { get; set; }
}
