namespace Knowit.Signaturgruppen.Umbraco.Akait;

public interface IAkaitMembershipClient
{
    /// <summary>
    /// GET /MitId/FindPersonMedSubject. Returns the medlemsnummer when the broker subject is
    /// mapped to a member, or null on HTTP 404 (mapping not yet established).
    /// </summary>
    /// <exception cref="AkaitValidationException">HTTP 400 from AKAIT with the Danish reason.</exception>
    Task<string?> FindPersonMedSubjectAsync(string mitIdSubject, CancellationToken ct);

    /// <summary>
    /// POST /MitId/RegistrerSubjectForPerson. Creates the (subject, medlemsnummer) link by
    /// looking up the CPR in the AKAIT member database.
    /// </summary>
    /// <exception cref="AkaitValidationException">HTTP 400 from AKAIT (e.g. CPR not in member database).</exception>
    Task RegistrerSubjectForPersonAsync(string mitIdSubject, string cpr, CancellationToken ct);
}
