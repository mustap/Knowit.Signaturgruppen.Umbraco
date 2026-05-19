using System.Net;
using System.Text;
using Knowit.Signaturgruppen.Umbraco.Akait;

namespace Knowit.Signaturgruppen.Umbraco.Tests.Akait;

public class AkaitMembershipClientTests
{
    [Fact]
    public async Task FindPerson_returns_medlemsnummer_on_200()
    {
        var (sut, handler) = Build((req, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"medlemsnummer\":\"42-123\"}", Encoding.UTF8, "application/json")
            }));

        var result = await sut.FindPersonMedSubjectAsync("sub-abc", CancellationToken.None);

        Assert.Equal("42-123", result);
        Assert.Single(handler.Requests);
        Assert.Contains("subjectId=sub-abc", handler.Requests[0].RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindPerson_returns_null_on_404()
    {
        var (sut, _) = Build((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var result = await sut.FindPersonMedSubjectAsync("sub-abc", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindPerson_throws_AkaitValidationException_on_400_with_body()
    {
        var (sut, _) = Build((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Ugyldigt input for feltet MitIdSubject må ikke være tomt", Encoding.UTF8, "text/plain")
            }));

        var ex = await Assert.ThrowsAsync<AkaitValidationException>(
            () => sut.FindPersonMedSubjectAsync("sub-abc", CancellationToken.None));
        Assert.Contains("Ugyldigt input", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistrerSubject_succeeds_on_201()
    {
        var (sut, handler) = Build((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)));

        await sut.RegistrerSubjectForPersonAsync("sub-abc", "1234567890", CancellationToken.None);

        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        Assert.Contains("\"subjectId\":\"sub-abc\"", body, StringComparison.Ordinal);
        Assert.Contains("\"cprNummer\":\"1234567890\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegistrerSubject_throws_AkaitValidationException_on_400()
    {
        var (sut, _) = Build((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    "Det er ikke muligt at registrere MitId subjectId'et, da CPR nummeret: 1234567890 ikke findes i medlems databasen",
                    Encoding.UTF8, "text/plain")
            }));

        var ex = await Assert.ThrowsAsync<AkaitValidationException>(
            () => sut.RegistrerSubjectForPersonAsync("sub-abc", "1234567890", CancellationToken.None));
        Assert.Contains("CPR nummeret", ex.Message, StringComparison.Ordinal);
    }

    private static (AkaitMembershipClient sut, StubHttpMessageHandler handler) Build(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://akait.test") };
        return (new AkaitMembershipClient(http), handler);
    }
}
