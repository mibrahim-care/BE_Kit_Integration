using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Application.Abstractions;
using Hl7.Fhir.Model;

namespace Fakeeh.MedicalKit.Api.Http;

/// <summary>Turns unhandled exceptions into an error body: an OperationOutcome under /fhir, an ErrorResponse elsewhere.</summary>
public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
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

            var (status, code, message, issueType) = ex switch
            {
                HisUnavailableException => (StatusCodes.Status502BadGateway, "his.unavailable", ex.Message, OperationOutcome.IssueType.Transient),
                HttpRequestException => (StatusCodes.Status502BadGateway, "his.unavailable", "Fakeeh HIS could not be reached.", OperationOutcome.IssueType.Transient),
                _ => (StatusCodes.Status500InternalServerError, "server.error", "An unexpected error occurred.", OperationOutcome.IssueType.Exception)
            };

            context.Response.Clear();

            if (FhirConstants.IsFhirRequest(context.Request))
            {
                await FhirJson.WriteAsync(context, OperationOutcomes.Single(issueType, message, code: code), status);
                return;
            }

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(ErrorResponse.Single(code, message), context.RequestAborted);
        }
    }
}
