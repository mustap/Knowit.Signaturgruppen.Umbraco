namespace Knowit.Signaturgruppen.Umbraco.Pipeline;

public interface IPostLoginHandler
{
    /// <summary>
    /// Invoked once per successful Signaturgruppen sign-in, immediately after the broker
    /// callback succeeds. May return <see cref="PostLoginResult.Continue"/>, abort the
    /// sign-in, or request a step-up with additional scopes (e.g. <c>"ssn"</c> for CPR).
    /// Handlers are invoked in registration order; the first non-Continue result wins.
    /// </summary>
    Task<PostLoginResult> HandleAsync(PostLoginContext context, CancellationToken ct);
}
