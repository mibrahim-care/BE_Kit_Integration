using Fakeeh.MedicalKit.Application.Abstractions;

namespace Fakeeh.MedicalKit.Api.Http;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !context.Response.HasStarted)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            var (status, code, message) = ex switch
            {
                HisUnavailableException => (StatusCodes.Status502BadGateway, "his.unavailable", ex.Message),
                HttpRequestException => (StatusCodes.Status502BadGateway, "his.unavailable", "Fakeeh HIS could not be reached."),
                _ => (StatusCodes.Status500InternalServerError, "server.error", "An unexpected error occurred.")
            };

            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(ErrorResponse.Single(code, message), context.RequestAborted);
        }
    }
}
