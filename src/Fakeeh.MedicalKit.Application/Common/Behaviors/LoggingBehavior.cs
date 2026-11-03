using System.Diagnostics;
using Fakeeh.MedicalKit.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Fakeeh.MedicalKit.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        if (response.IsFailure)
        {
            logger.LogWarning(
                "{Request} failed in {Elapsed} ms: {Errors}",
                name,
                stopwatch.ElapsedMilliseconds,
                string.Join("; ", response.Errors.Select(e => $"{e.Code}: {e.Message}")));
        }
        else
        {
            logger.LogInformation("{Request} handled in {Elapsed} ms", name, stopwatch.ElapsedMilliseconds);
        }

        return response;
    }
}
