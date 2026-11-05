using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Fakeeh.MedicalKit.Api.Fhir;

/// <summary>
/// Reads and writes FHIR JSON with its own serializer settings, so the FHIR endpoints do not change how
/// the JSON (api/MedicalKit) endpoints serialize.
/// </summary>
public static class FhirJson
{
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions()
        .ForFhir(typeof(Patient).Assembly)
        .UsingMode(DeserializerModes.Ostrich)
        .Pretty();

    public static async System.Threading.Tasks.Task WriteAsync(HttpContext context, Resource resource, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = FhirConstants.ContentType + "; charset=utf-8";
        await JsonSerializer.SerializeAsync(context.Response.Body, resource, resource.GetType(), Options, context.RequestAborted);
    }

    /// <summary>Reads the request body as a FHIR resource. Returns null with an error message when it is not one.</summary>
    public static async Task<(Resource? Resource, string? Error)> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var resource = await JsonSerializer.DeserializeAsync<Resource>(request.Body, Options, cancellationToken);
            return resource is null
                ? (null, "The request body is empty.")
                : (resource, null);
        }
        catch (JsonException ex)
        {
            return (null, $"The request body is not valid FHIR JSON: {ex.Message}");
        }
        catch (DeserializationFailedException ex)
        {
            return (null, $"The request body is not a valid FHIR resource: {ex.Message}");
        }
    }
}

/// <summary>Writes a FHIR resource as application/fhir+json with the given status code.</summary>
public sealed class FhirResult(Resource resource, int statusCode = StatusCodes.Status200OK) : IActionResult
{
    public Resource Resource { get; } = resource;

    public int StatusCode { get; } = statusCode;

    public System.Threading.Tasks.Task ExecuteResultAsync(ActionContext context) =>
        FhirJson.WriteAsync(context.HttpContext, Resource, StatusCode);
}
