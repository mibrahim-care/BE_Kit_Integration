using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;

namespace Fakeeh.MedicalKit.Api.Controllers;

[Route("fhir")]
public sealed class SystemController(ConformanceMapper conformance, BatchProcessor batchProcessor) : FhirControllerBase
{
    [HttpGet("metadata")]
    public IActionResult Metadata() => Fhir(conformance.BuildCapabilityStatement(FhirBaseUrl));

    [HttpPost("")]
    [Consumes(FhirConstants.ContentType, "application/json")]
    public async Task<IActionResult> Batch([FromBody] Resource resource, CancellationToken cancellationToken)
    {
        if (resource is not Bundle bundle)
        {
            return FhirBadRequest("The request body must be a Bundle.", "batch.not_a_bundle");
        }

        if (bundle.Type != Bundle.BundleType.Batch)
        {
            return FhirBadRequest(
                $"Bundle.type '{bundle.TypeElement?.ObjectValue}' is not supported. Send a 'batch' Bundle: each reading is saved or rejected on its own.",
                "batch.type_not_supported");
        }

        if (bundle.Entry.Count == 0)
        {
            return FhirBadRequest("The batch Bundle has no entries.", "batch.empty");
        }

        var response = await batchProcessor.ProcessAsync(bundle, cancellationToken);
        return Fhir(response);
    }
}
