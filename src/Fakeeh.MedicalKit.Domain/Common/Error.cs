namespace Fakeeh.MedicalKit.Domain.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Failure
}

public sealed record Error(string Code, string Message, ErrorType Type, string? Target = null)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Validation(string code, string message, string? target = null) =>
        new(code, message, ErrorType.Validation, target);

    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) =>
        new(code, message, ErrorType.Unauthorized);

    public static Error Failure(string code, string message) =>
        new(code, message, ErrorType.Failure);
}
