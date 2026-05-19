using System.Security.Claims;
using Knowit.Signaturgruppen.Umbraco.Authentication;
using Knowit.Signaturgruppen.Umbraco.Configuration;
using Knowit.Signaturgruppen.Umbraco.Pipeline;
using Knowit.Signaturgruppen.Umbraco.StepUp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Knowit.Signaturgruppen.Umbraco.Tests.Authentication;

public class SignaturgruppenOidcEventsTicketReceivedTests
{
    [Fact]
    public async Task Continue_result_leaves_context_unhandled_and_does_not_call_step_up()
    {
        var stepUp = new RecordingStepUpService();
        var sut = CreateSut();
        var ctx = CreateContext(stepUp, new FakeHandler(PostLoginResult.Continue));

        await sut.TicketReceived(ctx);

        Assert.NotEqual(StatusCodes.Status403Forbidden, ctx.HttpContext.Response.StatusCode);
        Assert.Equal(0, stepUp.ChallengeCalls);
        Assert.False(IsHandled(ctx));
    }

    [Fact]
    public async Task Abort_result_sets_403_and_handles_response()
    {
        var stepUp = new RecordingStepUpService();
        var sut = CreateSut();
        var ctx = CreateContext(stepUp, new FakeHandler(PostLoginResult.Abort("not-eligible")));

        await sut.TicketReceived(ctx);

        Assert.Equal(StatusCodes.Status403Forbidden, ctx.HttpContext.Response.StatusCode);
        Assert.True(IsHandled(ctx));
        Assert.Equal(0, stepUp.ChallengeCalls);
    }

    [Fact]
    public async Task StepUpRequired_invokes_step_up_service_with_scopes_and_returnUrl()
    {
        var stepUp = new RecordingStepUpService();
        var sut = CreateSut();
        var ctx = CreateContext(
            stepUp,
            new FakeHandler(PostLoginResult.StepUpRequired(["ssn"])),
            originalReturnUrl: "/members/profile");

        await sut.TicketReceived(ctx);

        Assert.Equal(1, stepUp.ChallengeCalls);
        Assert.Equal(new[] { "ssn" }, stepUp.LastScopes);
        Assert.Equal("/members/profile", stepUp.LastReturnUrl);
        Assert.True(IsHandled(ctx));
    }

    [Fact]
    public async Task Empty_pipeline_lets_OIDC_continue_unhandled()
    {
        var stepUp = new RecordingStepUpService();
        var sut = CreateSut();
        var ctx = CreateContext(stepUp);

        await sut.TicketReceived(ctx);

        Assert.False(IsHandled(ctx));
        Assert.Equal(0, stepUp.ChallengeCalls);
    }

    private static SignaturgruppenOidcEvents CreateSut()
        => new(
            new DefaultIdpParamsBuilder(),
            new StaticOptionsMonitor(new SignaturgruppenOptions()),
            NullLogger<SignaturgruppenOidcEvents>.Instance);

    private static TicketReceivedContext CreateContext(
        ISignaturgruppenStepUpService stepUp,
        IPostLoginHandler? handler = null,
        string? originalReturnUrl = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(stepUp);
        if (handler is not null)
        {
            services.AddSingleton(handler);
        }
        services.AddSingleton<PostLoginPipeline>();

        var sp = services.BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = sp };
        var scheme = new AuthenticationScheme("oidc", "oidc", typeof(OpenIdConnectHandler));
        var options = new OpenIdConnectOptions();
        var identity = new ClaimsIdentity(new[] { new Claim("sub", "abc") }, "test");
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { RedirectUri = originalReturnUrl },
            scheme.Name);

        return new TicketReceivedContext(httpContext, scheme, options, ticket);
    }

    private static bool IsHandled(TicketReceivedContext ctx)
        => ctx.Result?.Handled == true;

    private sealed class FakeHandler : IPostLoginHandler
    {
        private readonly PostLoginResult _result;
        public FakeHandler(PostLoginResult result) => _result = result;
        public Task<PostLoginResult> HandleAsync(PostLoginContext context, CancellationToken ct)
            => Task.FromResult(_result);
    }

    private sealed class RecordingStepUpService : ISignaturgruppenStepUpService
    {
        public int ChallengeCalls { get; private set; }
        public IReadOnlyCollection<string>? LastScopes { get; private set; }
        public string? LastReturnUrl { get; private set; }

        public Task ChallengeAsync(HttpContext httpContext, IReadOnlyCollection<string> extraScopes, string returnUrl)
        {
            ChallengeCalls++;
            LastScopes = extraScopes;
            LastReturnUrl = returnUrl;
            return Task.CompletedTask;
        }
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<SignaturgruppenOptions>
    {
        public StaticOptionsMonitor(SignaturgruppenOptions value) => CurrentValue = value;
        public SignaturgruppenOptions CurrentValue { get; }
        public SignaturgruppenOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<SignaturgruppenOptions, string?> listener) => null;
    }
}
