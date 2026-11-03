using Hl7.Fhir.Model;

namespace Fakeeh.MedicalKit.Api.Fhir;

public sealed class FhirExceptionMiddleware(RequestDelegate next, FhirResponseWriter writer, ILogger<FhirExceptionMiddleware> logger)
{
    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !context.Response.HasStarted)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            var (status, type, message) = ex switch
            {
                NotImplementedException => (StatusCodes.Status501NotImplemented, OperationOutcome.IssueType.NotSupported, ex.Message),
                Infrastructure.His.HisUnavailableException => (StatusCodes.Status502BadGateway, OperationOutcome.IssueType.Transient, ex.Message),
                HttpRequestException => (StatusCodes.Status502BadGateway, OperationOutcome.IssueType.Transient, "Fakeeh HIS could not be reached."),
                _ => (StatusCodes.Status500InternalServerError, OperationOutcome.IssueType.Exception, "An unexpected error occurred.")
            };

            context.Response.Clear();
            await writer.WriteAsync(context, OperationOutcomes.Single(type, message, code: "server.error"), status);
        }
    }
}
