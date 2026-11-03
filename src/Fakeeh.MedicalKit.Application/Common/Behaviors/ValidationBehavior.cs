using System.Collections.Concurrent;
using System.Reflection;
using Fakeeh.MedicalKit.Domain.Common;
using FluentValidation;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var errors = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .Select(f => Error.Validation(f.ErrorCode, f.ErrorMessage, f.PropertyName))
            .Distinct()
            .ToList();

        return errors.Count == 0 ? await next() : ResultFactory.Failure<TResponse>(errors);
    }
}

internal static class ResultFactory
{
    private static readonly ConcurrentDictionary<Type, Func<IReadOnlyList<Error>, object>> Factories = new();

    private static readonly MethodInfo GenericFailure = typeof(Result)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == nameof(Result.Failure)
                     && m.IsGenericMethodDefinition
                     && m.GetParameters()[0].ParameterType == typeof(IReadOnlyList<Error>));

    public static TResponse Failure<TResponse>(IReadOnlyList<Error> errors) where TResponse : Result
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)Result.Failure(errors);
        }

        var factory = Factories.GetOrAdd(typeof(TResponse), type =>
        {
            var method = GenericFailure.MakeGenericMethod(type.GetGenericArguments()[0]);
            return e => method.Invoke(null, [e])!;
        });

        return (TResponse)factory(errors);
    }
}
