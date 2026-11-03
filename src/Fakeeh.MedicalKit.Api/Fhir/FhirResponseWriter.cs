using System.Text.Json;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace Fakeeh.MedicalKit.Api.Fhir;

public sealed class FhirResponseWriter(IOptions<MvcJsonOptions> jsonOptions)
{
    public async System.Threading.Tasks.Task WriteAsync(HttpContext context, Resource resource, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = FhirConstants.ContentType;
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            resource,
            resource.GetType(),
            jsonOptions.Value.JsonSerializerOptions,
            context.RequestAborted);
    }
}
