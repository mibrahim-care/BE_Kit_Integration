using Fakeeh.MedicalKit.Domain.Common;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;

namespace Fakeeh.MedicalKit.Api.Fhir;

[ApiController]
[Produces(FhirConstants.ContentType)]
public abstract class FhirControllerBase : ControllerBase
{
    protected string FhirBaseUrl => $"{Request.Scheme}://{Request.Host}{Request.PathBase}/fhir";

    protected static ObjectResult Fhir(Resource resource, int statusCode = StatusCodes.Status200OK) =>
        new(resource) { StatusCode = statusCode, DeclaredType = typeof(Resource) };

    protected static ObjectResult FhirProblem(Result result)
    {
        var status = (int)OperationOutcomes.StatusFor(result.Error);
        return Fhir(OperationOutcomes.FromErrors(result.Errors), status);
    }

    protected static ObjectResult FhirBadRequest(string message, string? code = null) =>
        Fhir(OperationOutcomes.Single(OperationOutcome.IssueType.Invalid, message, code: code), StatusCodes.Status400BadRequest);
}
