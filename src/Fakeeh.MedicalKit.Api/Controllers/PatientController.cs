using System.Text.RegularExpressions;
using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Fakeeh.MedicalKit.Api.Fhir.Search;
using Fakeeh.MedicalKit.Application.Patients;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fakeeh.MedicalKit.Api.Controllers;

[Route(FhirConstants.RoutePrefix + "/Patient")]
public sealed partial class PatientController(ISender sender, PatientFhirMapper mapper) : FhirControllerBase
{
    /// <summary>
    /// POST Patient/_search with <c>identifier=[system|]value</c> and/or <c>_id=value</c> in a form body.
    /// Patient.id is the National ID / Iqama, so it is never taken from the URL. No match is an empty Bundle, not 404.
    /// </summary>
    [HttpPost("_search")]
    public async Task<IActionResult> Search(CancellationToken cancellationToken)
    {
        var parameters = await ReadSearchParametersAsync(cancellationToken);
        var identifiers = parameters.All("identifier");
        var ids = parameters.All("_id");

        if (identifiers.Count == 0 && ids.Count == 0)
        {
            return FhirBadRequest(
                "Search by 'identifier' (system|National ID or Iqama) or '_id' in the form body.",
                "search.identifier_required");
        }

        string? value = null;
        PatientIdentifierType? identifierType = null;

        if (identifiers.Count > 0)
        {
            // Repeated parameters are ANDed: different identifiers cannot match one patient.
            var tokens = identifiers.Select(FhirSearchParser.ParseToken).OfType<TokenParam>().ToList();
            if (tokens.Select(t => t.Code).Distinct().Count() > 1)
            {
                return EmptySearch();
            }

            var token = tokens[0];
            identifierType = mapper.IdentifierTypeFor(token.System, token.Code);
            if (identifierType is null)
            {
                return EmptySearch();
            }

            value = token.Code;
        }

        if (ids.Count > 0)
        {
            var id = ids[0];
            if (ids.Distinct().Count() > 1 || (value is not null && value != id) || !PatientIdRegex().IsMatch(id))
            {
                return EmptySearch();
            }

            value = id;
            identifierType ??= mapper.IdentifierTypeFor(null, id);
        }

        var result = await sender.Send(new SearchPatientByIdentifierQuery(identifierType!.Value, value!), cancellationToken);
        if (result.IsFailure)
        {
            return FhirProblem(result);
        }

        Resource[] matches = result.Value is null ? [] : [mapper.ToFhir(result.Value)];
        return Fhir(SearchBundleBuilder.Build(matches, FhirBaseUrl));
    }

    [GeneratedRegex(@"^[12]\d{9}$")]
    private static partial Regex PatientIdRegex();
}
