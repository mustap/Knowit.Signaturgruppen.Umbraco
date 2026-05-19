using Knowit.Signaturgruppen.Umbraco.Constants;

namespace Knowit.Signaturgruppen.Umbraco.Configuration;

public sealed class SignaturgruppenOptions
{
    public const string SectionName = "Signaturgruppen";

    public SignaturgruppenEnvironment Environment { get; set; } = SignaturgruppenEnvironment.Preproduction;

    public string? CustomAuthority { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string CallbackPath { get; set; } = SignaturgruppenDefaults.CallbackPath;

    public string SignedOutCallbackPath { get; set; } = SignaturgruppenDefaults.SignedOutCallbackPath;

    public IList<string> Scopes { get; set; } = new List<string>();

    public static IReadOnlyList<string> DefaultScopes { get; } = ["openid", "mitid"];

    public string DisplayName { get; set; } = SignaturgruppenDefaults.DisplayName;

    public MitIdParameters MitId { get; set; } = new();

    public AutoLinkOptions AutoLink { get; set; } = new();

    /// <summary>
    /// Map of incoming broker claim type → Umbraco member-type property alias.
    /// On every successful sign-in, claims listed here are written to the member's properties
    /// when the alias exists on the member type. Keys are case-insensitive.
    /// </summary>
    public IDictionary<string, string> ClaimToMemberProperty { get; set; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string ResolveAuthority() => Environment.ResolveAuthority(CustomAuthority);
}
