using System.Security.Claims;
using Knowit.Signaturgruppen.Umbraco.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Knowit.Signaturgruppen.Umbraco.Tests.Pipeline;

public class PostLoginPipelineTests
{
    [Fact]
    public async Task Returns_continue_when_no_handlers_registered()
    {
        var pipeline = Build();
        var ctx = NewContext();

        var result = await pipeline.RunAsync(ctx, CancellationToken.None);

        Assert.Equal(PostLoginResultKind.Continue, result.Kind);
    }

    [Fact]
    public async Task Runs_handlers_in_registration_order_and_records_invocations()
    {
        var calls = new List<string>();
        var pipeline = Build(
            new RecordingHandler("h1", calls, PostLoginResult.Continue),
            new RecordingHandler("h2", calls, PostLoginResult.Continue),
            new RecordingHandler("h3", calls, PostLoginResult.Continue));

        var result = await pipeline.RunAsync(NewContext(), CancellationToken.None);

        Assert.Equal(PostLoginResultKind.Continue, result.Kind);
        Assert.Equal(new[] { "h1", "h2", "h3" }, calls);
    }

    [Fact]
    public async Task Short_circuits_on_first_abort()
    {
        var calls = new List<string>();
        var pipeline = Build(
            new RecordingHandler("h1", calls, PostLoginResult.Continue),
            new RecordingHandler("h2", calls, PostLoginResult.Abort("denied")),
            new RecordingHandler("h3", calls, PostLoginResult.Continue));

        var result = await pipeline.RunAsync(NewContext(), CancellationToken.None);

        Assert.Equal(PostLoginResultKind.Abort, result.Kind);
        Assert.Equal("denied", result.AbortReason);
        Assert.Equal(new[] { "h1", "h2" }, calls);
    }

    [Fact]
    public async Task Short_circuits_on_first_stepup_required()
    {
        var calls = new List<string>();
        var pipeline = Build(
            new RecordingHandler("h1", calls, PostLoginResult.StepUpRequired(["ssn"])),
            new RecordingHandler("h2", calls, PostLoginResult.Continue));

        var result = await pipeline.RunAsync(NewContext(), CancellationToken.None);

        Assert.Equal(PostLoginResultKind.StepUpRequired, result.Kind);
        Assert.Equal(new[] { "ssn" }, result.StepUpScopes);
        Assert.Equal(new[] { "h1" }, calls);
    }

    [Fact]
    public async Task Honours_cancellation_between_handlers()
    {
        var calls = new List<string>();
        using var cts = new CancellationTokenSource();
        var pipeline = Build(
            new RecordingHandler("h1", calls, PostLoginResult.Continue, () => cts.Cancel()),
            new RecordingHandler("h2", calls, PostLoginResult.Continue));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.RunAsync(NewContext(), cts.Token));

        Assert.Equal(new[] { "h1" }, calls);
    }

    private static PostLoginPipeline Build(params IPostLoginHandler[] handlers)
        => new(handlers, NullLogger<PostLoginPipeline>.Instance);

    private static PostLoginContext NewContext()
    {
        var http = new DefaultHttpContext();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "abc") }, "test"));
        return new PostLoginContext(http, principal, properties: null);
    }

    private sealed class RecordingHandler : IPostLoginHandler
    {
        private readonly string _name;
        private readonly List<string> _calls;
        private readonly PostLoginResult _result;
        private readonly Action? _onCalled;

        public RecordingHandler(string name, List<string> calls, PostLoginResult result, Action? onCalled = null)
        {
            _name = name;
            _calls = calls;
            _result = result;
            _onCalled = onCalled;
        }

        public Task<PostLoginResult> HandleAsync(PostLoginContext context, CancellationToken ct)
        {
            _calls.Add(_name);
            _onCalled?.Invoke();
            return Task.FromResult(_result);
        }
    }
}
