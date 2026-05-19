namespace Knowit.Signaturgruppen.Umbraco.Akait;

/// <summary>
/// Thrown when the AKAIT MitId v1 API rejects a request with HTTP 400. The message carries
/// the Danish validation text returned by the server verbatim, suitable for surfacing in UI.
/// </summary>
public sealed class AkaitValidationException : Exception
{
    public AkaitValidationException(string message) : base(message) { }
}
