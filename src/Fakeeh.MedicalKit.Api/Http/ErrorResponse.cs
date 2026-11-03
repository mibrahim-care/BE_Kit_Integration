using Fakeeh.MedicalKit.Application.Common;
using Fakeeh.MedicalKit.Domain.Common;

namespace Fakeeh.MedicalKit.Api.Http;

/// <summary>The body of every non-2xx response.</summary>
public sealed record ErrorResponse(IReadOnlyList<ErrorDto> Errors)
{
    public static ErrorResponse From(IEnumerable<Error> errors) => new(errors.Select(ErrorDto.From).ToList());

    public static ErrorResponse Single(string code, string message, string? field = null) =>
        new([new ErrorDto(code, message, field)]);

    public static int StatusFor(Error error) => error.Type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError
    };
}
