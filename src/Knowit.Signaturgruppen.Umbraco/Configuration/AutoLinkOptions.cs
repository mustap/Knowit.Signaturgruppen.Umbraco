namespace Knowit.Signaturgruppen.Umbraco.Configuration;

public sealed class AutoLinkOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Alias of the member type that newly auto-linked Signaturgruppen members are created as.
    /// The member type must exist in Umbraco; this package does not create it.
    /// </summary>
    public string MemberType { get; set; } = "Member";

    public string? DefaultCulture { get; set; }

    public bool IsApproved { get; set; } = true;
}
