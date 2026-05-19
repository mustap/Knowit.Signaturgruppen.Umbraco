using System.Security.Claims;
using Knowit.Signaturgruppen.Umbraco.Akait;
using Knowit.Signaturgruppen.Umbraco.Authentication;
using Knowit.Signaturgruppen.Umbraco.Pipeline;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Knowit.Signaturgruppen.Umbraco.Tests.Akait;

public class AkaitMembershipResolverTests
{
    [Fact]
    public async Task Continue_with_medlemsnummer_when_subject_is_known()
    {
        var client = new FakeClient
        {
            FindResults = new Queue<string?>(new[] { "42-123" })
        };
        var resolver = CreateResolver(client);
        var ctx = CreateContext(("sub", "abc"));

        var result = await resolver.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(PostLoginResultKind.Continue, result.Kind);
        Assert.Equal("42-123", ((ClaimsIdentity)ctx.Principal.Identity!).FindFirst("akait.medlemsnummer")?.Value);
        Assert.Equal(0, client.RegistrerCalls);
    }

    [Fact]
    public async Task StepUpRequired_when_subject_unknown_and_no_cpr()
    {
        var client = new FakeClient
        {
            FindResults = new Queue<string?>(new string?[] { null })
        };
        var resolver = CreateResolver(client);
        var ctx = CreateContext(("sub", "abc"));

        var result = await resolver.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(PostLoginResultKind.StepUpRequired, result.Kind);
        Assert.Equal(new[] { "ssn" }, result.StepUpScopes);
        Assert.Equal(0, client.RegistrerCalls);
    }

    [Fact]
    public async Task Registers_then_continues_when_cpr_is_present()
    {
        var client = new FakeClient
        {
            FindResults = new Queue<string?>(new string?[] { null, "42-123" })
        };
        var resolver = CreateResolver(client);
        var ctx = CreateContext(("sub", "abc"), (ClaimMaps.Cpr, "1234567890"));

        var result = await resolver.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(PostLoginResultKind.Continue, result.Kind);
        Assert.Equal(1, client.RegistrerCalls);
        Assert.Equal("abc", client.RegistrerSubject);
        Assert.Equal("1234567890", client.RegistrerCpr);
    }

    [Fact]
    public async Task Drops_cpr_claim_after_registration_path_regardless_of_outcome()
    {
        var client = new FakeClient
        {
            FindResults = new Queue<string?>(new string?[] { null, "42-123" })
        };
        var resolver = CreateResolver(client);
        var ctx = CreateContext(("sub", "abc"), (ClaimMaps.Cpr, "1234567890"));

        await resolver.HandleAsync(ctx, CancellationToken.None);

        var identity = (ClaimsIdentity)ctx.Principal.Identity!;
        Assert.Null(identity.FindFirst(ClaimMaps.Cpr));
    }

    [Fact]
    public async Task Aborts_when_registration_validation_fails()
    {
        var client = new FakeClient
        {
            FindResults = new Queue<string?>(new string?[] { null }),
            RegistrerThrows = new AkaitValidationException("Det er ikke muligt at registrere MitId subjectId'et, da CPR nummeret: 1234567890 ikke findes i medlems databasen")
        };
        var resolver = CreateResolver(client);
        var ctx = CreateContext(("sub", "abc"), (ClaimMaps.Cpr, "1234567890"));

        var result = await resolver.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(PostLoginResultKind.Abort, result.Kind);
        Assert.Contains("ikke findes i medlems databasen", result.AbortReason);
        Assert.Null(((ClaimsIdentity)ctx.Principal.Identity!).FindFirst(ClaimMaps.Cpr));
    }

    [Fact]
    public async Task Aborts_when_subject_still_not_mapped_after_registration()
    {
        var client = new FakeClient
        {
            FindResults = new Queue<string?>(new string?[] { null, null })
        };
        var resolver = CreateResolver(client);
        var ctx = CreateContext(("sub", "abc"), (ClaimMaps.Cpr, "1234567890"));

        var result = await resolver.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(PostLoginResultKind.Abort, result.Kind);
    }

    [Fact]
    public async Task Skips_silently_when_principal_has_no_sub()
    {
        var client = new FakeClient
        {
            FindResults = new Queue<string?>(new string?[] { "should-not-be-called" })
        };
        var resolver = CreateResolver(client);
        var ctx = CreateContext(("name", "Alice"));

        var result = await resolver.HandleAsync(ctx, CancellationToken.None);

        Assert.Equal(PostLoginResultKind.Continue, result.Kind);
        Assert.Equal(0, client.FindCalls);
    }

    private static AkaitMembershipResolver CreateResolver(IAkaitMembershipClient client)
        => new(
            client,
            new StaticOptionsMonitor<AkaitApiOptions>(new AkaitApiOptions()),
            NullLogger<AkaitMembershipResolver>.Instance);

    private static PostLoginContext CreateContext(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test");
        var principal = new ClaimsPrincipal(identity);
        return new PostLoginContext(new DefaultHttpContext(), principal, properties: null);
    }

    private sealed class FakeClient : IAkaitMembershipClient
    {
        public Queue<string?> FindResults { get; init; } = new();
        public int FindCalls { get; private set; }
        public int RegistrerCalls { get; private set; }
        public string? RegistrerSubject { get; private set; }
        public string? RegistrerCpr { get; private set; }
        public Exception? RegistrerThrows { get; init; }

        public Task<string?> FindPersonMedSubjectAsync(string mitIdSubject, CancellationToken ct)
        {
            FindCalls++;
            return Task.FromResult(FindResults.Count > 0 ? FindResults.Dequeue() : null);
        }

        public Task RegistrerSubjectForPersonAsync(string mitIdSubject, string cpr, CancellationToken ct)
        {
            RegistrerCalls++;
            RegistrerSubject = mitIdSubject;
            RegistrerCpr = cpr;
            if (RegistrerThrows is not null) throw RegistrerThrows;
            return Task.CompletedTask;
        }
    }
}
