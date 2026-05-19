using Knowit.Signaturgruppen.Umbraco.Pipeline;

namespace Knowit.Signaturgruppen.Umbraco.Tests.Pipeline;

public class PostLoginResultTests
{
    [Fact]
    public void Continue_singleton_has_continue_kind_and_no_payload()
    {
        var r = PostLoginResult.Continue;

        Assert.Equal(PostLoginResultKind.Continue, r.Kind);
        Assert.Null(r.AbortReason);
        Assert.Null(r.StepUpScopes);
        Assert.Null(r.StepUpReturnUrl);
    }

    [Fact]
    public void Abort_carries_reason()
    {
        var r = PostLoginResult.Abort("not-a-member");

        Assert.Equal(PostLoginResultKind.Abort, r.Kind);
        Assert.Equal("not-a-member", r.AbortReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Abort_rejects_null_or_whitespace_reason(string? reason)
    {
        // ThrowIfNullOrWhiteSpace throws ArgumentNullException for null, ArgumentException for whitespace.
        Assert.ThrowsAny<ArgumentException>(() => PostLoginResult.Abort(reason!));
    }

    [Fact]
    public void StepUpRequired_carries_scopes_and_optional_return_url()
    {
        var r = PostLoginResult.StepUpRequired(["ssn"], "/members/profile");

        Assert.Equal(PostLoginResultKind.StepUpRequired, r.Kind);
        Assert.Equal(new[] { "ssn" }, r.StepUpScopes);
        Assert.Equal("/members/profile", r.StepUpReturnUrl);
    }

    [Fact]
    public void StepUpRequired_rejects_empty_scope_list()
    {
        Assert.Throws<ArgumentException>(() => PostLoginResult.StepUpRequired(Array.Empty<string>()));
    }

    [Fact]
    public void StepUpRequired_rejects_null_scope_list()
    {
        Assert.Throws<ArgumentNullException>(() => PostLoginResult.StepUpRequired(null!));
    }
}
