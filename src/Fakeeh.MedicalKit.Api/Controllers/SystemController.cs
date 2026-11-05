using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;

namespace Fakeeh.MedicalKit.Api.Controllers;

[Route(FhirConstants.RoutePrefix)]
public sealed class SystemController(ConformanceMapper conformance, BatchProcessor batchProcessor) : FhirControllerBase
{
    /// <summary>Public CapabilityStatement (no API key). Also used as a health check.</summary>
    [HttpGet("metadata")]
    public IActionResult Metadata() => Fhir(conformance.BuildCapabilityStatement(FhirBaseUrl));

    /// <summary>POST [base] with a batch Bundle of Observation creates. Each entry is saved or rejected on its own.</summary>
    [HttpPost("")]
    public async Task<IActionResult> Batch(CancellationToken cancellationToken)
    {
        var (resource, error) = await ReadResourceAsync(cancellationToken);
        if (resource is null)
        {
            return FhirBadRequest(error!, "request.invalid");
        }

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

        return Fhir(await batchProcessor.ProcessAsync(bundle, cancellationToken));
    }
}
