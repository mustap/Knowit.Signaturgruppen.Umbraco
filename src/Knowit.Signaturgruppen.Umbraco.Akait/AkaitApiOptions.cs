namespace Knowit.Signaturgruppen.Umbraco.Akait;

public sealed class AkaitApiOptions
{
    public const string SectionName = "Akait";

    /// <summary>Absolute URL of the AKAIT MitId v1 API root, e.g. <c>https://apiprodtest.mycompany.dk</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Absolute OAuth2 token endpoint URL.</summary>
    public string TokenUrl { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Optional OAuth2 scope value for the client-credentials grant.</summary>
    public string? Scope { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Claim type written onto the principal when AKAIT returns a medlemsnummer.</summary>
    public string MedlemsnummerClaimType { get; set; } = "akait.medlemsnummer";
}
