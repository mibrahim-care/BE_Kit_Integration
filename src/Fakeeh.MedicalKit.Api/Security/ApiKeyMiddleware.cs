using System.Security.Cryptography;
using System.Text;
using Fakeeh.MedicalKit.Api.Fhir;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;

namespace Fakeeh.MedicalKit.Api.Security;

public sealed class ApiKeyOptions
{
    public const string SectionName = "Security";
    public const string HeaderName = "X-Api-Key";

    public List<ApiClient> ApiKeys { get; set; } = [];
}

public sealed class ApiClient
{
    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;
}

public sealed class ApiKeyMiddleware(RequestDelegate next, IOptionsMonitor<ApiKeyOptions> options, FhirResponseWriter writer)
{
    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context)
    {
        if (!RequiresKey(context.Request))
        {
            await next(context);
            return;
        }

        var provided = context.Request.Headers[ApiKeyOptions.HeaderName].ToString();
        var client = string.IsNullOrEmpty(provided) ? null : FindClient(provided);

        if (client is null)
        {
            var message = string.IsNullOrEmpty(provided)
                ? $"Missing {ApiKeyOptions.HeaderName} header."
                : $"Invalid {ApiKeyOptions.HeaderName} header.";

            await writer.WriteAsync(
                context,
                OperationOutcomes.Single(OperationOutcome.IssueType.Security, message, code: "security.api_key"),
                StatusCodes.Status401Unauthorized);
            return;
        }

        context.Items["ApiClient"] = client.Name;
        await next(context);
    }

    private static bool RequiresKey(HttpRequest request) =>
        !HttpMethods.IsOptions(request.Method)
        && request.Path.StartsWithSegments("/fhir")
        && !request.Path.StartsWithSegments("/fhir/metadata");

    private ApiClient? FindClient(string provided)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return options.CurrentValue.ApiKeys.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.Key)
            && CryptographicOperations.FixedTimeEquals(providedBytes, Encoding.UTF8.GetBytes(c.Key)));
    }
}
