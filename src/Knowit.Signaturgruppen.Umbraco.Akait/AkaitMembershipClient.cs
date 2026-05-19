using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Knowit.Signaturgruppen.Umbraco.Akait;

internal sealed class AkaitMembershipClient : IAkaitMembershipClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;

    public AkaitMembershipClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<string?> FindPersonMedSubjectAsync(string mitIdSubject, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(mitIdSubject);

        var path = $"/MitId/FindPersonMedSubject?subjectId={Uri.EscapeDataString(mitIdSubject)}";

        using var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Get, path), ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw await BuildValidationExceptionAsync(response, ct);
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var body = await JsonSerializer.DeserializeAsync<FindPersonResponse>(stream, JsonOptions, ct);
        return body?.Medlemsnummer;
    }

    public async Task RegistrerSubjectForPersonAsync(string mitIdSubject, string cpr, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(mitIdSubject);
        ArgumentException.ThrowIfNullOrEmpty(cpr);

        var payload = JsonSerializer.Serialize(new RegistrerSubjectRequest(mitIdSubject, cpr));

        using var response = await _http.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "/MitId/RegistrerSubjectIdForPerson")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            },
            ct);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw await BuildValidationExceptionAsync(response, ct);
        }

        response.EnsureSuccessStatusCode();
    }

    private static async Task<AkaitValidationException> BuildValidationExceptionAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        return new AkaitValidationException(string.IsNullOrWhiteSpace(body) ? "AKAIT validation error." : body);
    }

    private sealed record FindPersonResponse(
        [property: JsonPropertyName("medlemsnummer")] string? Medlemsnummer);

    private sealed record RegistrerSubjectRequest(
        [property: JsonPropertyName("subjectId")] string MitIdSubject,
        [property: JsonPropertyName("cprNummer")] string Cpr);
}
