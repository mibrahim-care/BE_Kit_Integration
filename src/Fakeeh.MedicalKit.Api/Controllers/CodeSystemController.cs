using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Fakeeh.MedicalKit.Application.VitalTypes;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fakeeh.MedicalKit.Api.Controllers;

[Route("fhir/CodeSystem")]
public sealed class CodeSystemController(ISender sender, ConformanceMapper conformance) : FhirControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Read(string id, CancellationToken cancellationToken)
    {
        if (id != FhirConstants.VitalTypeCodeSystemId)
        {
            return Fhir(
                OperationOutcomes.Single(OperationOutcome.IssueType.NotFound, $"CodeSystem '{id}' was not found.", code: "codesystem.not_found"),
                StatusCodes.Status404NotFound);
        }

        var result = await sender.Send(new GetVitalTypesQuery(), cancellationToken);
        return result.IsFailure ? FhirProblem(result) : Fhir(conformance.BuildVitalTypeCodeSystem(result.Value));
    }
}
