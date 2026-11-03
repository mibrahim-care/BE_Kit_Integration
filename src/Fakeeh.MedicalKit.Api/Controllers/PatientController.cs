using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Fakeeh.MedicalKit.Api.Fhir.Search;
using Fakeeh.MedicalKit.Application.Patients;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fakeeh.MedicalKit.Api.Controllers;

[Route("fhir/Patient")]
public sealed class PatientController(ISender sender, PatientFhirMapper mapper) : FhirControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? identifier, CancellationToken cancellationToken)
    {
        var token = FhirSearchParser.ParseToken(identifier);
        if (token is null)
        {
            return FhirBadRequest("The 'identifier' search parameter is required.", "search.identifier_required");
        }

        var identifierType = mapper.IdentifierTypeFor(token.System, token.Code);
        if (identifierType is null)
        {
            return Fhir(SearchBundleBuilder.Build([], FhirBaseUrl));
        }

        var result = await sender.Send(new SearchPatientByIdentifierQuery(identifierType.Value, token.Code), cancellationToken);
        if (result.IsFailure)
        {
            return FhirProblem(result);
        }

        Resource[] matches = result.Value is null ? [] : [mapper.ToFhir(result.Value)];
        return Fhir(SearchBundleBuilder.Build(matches, FhirBaseUrl));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Read(string id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetPatientByIdQuery(id), cancellationToken);
        return result.IsFailure ? FhirProblem(result) : Fhir(mapper.ToFhir(result.Value));
    }
}
