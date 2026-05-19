namespace Knowit.Signaturgruppen.Umbraco.Pipeline;

public enum PostLoginResultKind
{
    Continue,
    Abort,
    StepUpRequired
}

public sealed class PostLoginResult
{
    private PostLoginResult(
        PostLoginResultKind kind,
        string? abortReason,
        IReadOnlyList<string>? stepUpScopes,
        string? stepUpReturnUrl)
    {
        Kind = kind;
        AbortReason = abortReason;
        StepUpScopes = stepUpScopes;
        StepUpReturnUrl = stepUpReturnUrl;
    }

    public PostLoginResultKind Kind { get; }

    public string? AbortReason { get; }

    public IReadOnlyList<string>? StepUpScopes { get; }

    public string? StepUpReturnUrl { get; }

    public static PostLoginResult Continue { get; } =
        new(PostLoginResultKind.Continue, null, null, null);

    public static PostLoginResult Abort(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new PostLoginResult(PostLoginResultKind.Abort, reason, null, null);
    }

    public static PostLoginResult StepUpRequired(
        IReadOnlyCollection<string> extraScopes,
        string? returnUrl = null)
    {
        ArgumentNullException.ThrowIfNull(extraScopes);
        if (extraScopes.Count == 0)
        {
            throw new ArgumentException(
                "Step-up requires at least one extra scope.",
                nameof(extraScopes));
        }
        return new PostLoginResult(
            PostLoginResultKind.StepUpRequired,
            null,
            extraScopes.ToArray(),
            returnUrl);
    }
}
