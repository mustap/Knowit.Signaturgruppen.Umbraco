using Knowit.Signaturgruppen.Umbraco.Constants;

namespace Knowit.Signaturgruppen.Umbraco.Configuration;

public enum SignaturgruppenEnvironment
{
    Preproduction = 0,
    Production = 1,
    Custom = 2
}

public static class SignaturgruppenEnvironmentExtensions
{
    public static string ResolveAuthority(this SignaturgruppenEnvironment env, string? customAuthority) => env switch
    {
        SignaturgruppenEnvironment.Production => SignaturgruppenDefaults.ProductionAuthority,
        SignaturgruppenEnvironment.Preproduction => SignaturgruppenDefaults.PreproductionAuthority,
        SignaturgruppenEnvironment.Custom => !string.IsNullOrWhiteSpace(customAuthority)
            ? customAuthority
            : throw new InvalidOperationException(
                $"{nameof(SignaturgruppenOptions.CustomAuthority)} must be set when {nameof(SignaturgruppenOptions.Environment)} is {nameof(SignaturgruppenEnvironment.Custom)}."),
        _ => throw new ArgumentOutOfRangeException(nameof(env), env, "Unknown Signaturgruppen environment.")
    };
}
