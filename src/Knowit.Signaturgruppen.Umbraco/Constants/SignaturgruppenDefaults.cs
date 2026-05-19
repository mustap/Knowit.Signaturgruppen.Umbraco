namespace Knowit.Signaturgruppen.Umbraco.Constants;

public static class SignaturgruppenDefaults
{
    public const string AuthenticationScheme = "Signaturgruppen";
    public const string DisplayName = "Signaturgruppen";
    public const string CallbackPath = "/sg/login/callback";
    public const string SignedOutCallbackPath = "/sg/logout/callback";

    public const string ProductionAuthority = "https://netseidbroker.dk/op";
    public const string PreproductionAuthority = "https://pp.netseidbroker.dk/op";

    public const string ExtraScopesAuthPropertyKey = "sg.extra_scopes";

    public const string StepUpCallbackPath = "/sg/step-up/callback";
}
