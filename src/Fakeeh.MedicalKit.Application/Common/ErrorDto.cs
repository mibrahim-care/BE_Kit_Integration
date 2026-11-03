using Fakeeh.MedicalKit.Domain.Common;

namespace Fakeeh.MedicalKit.Application.Common;

/// <summary>The error shape returned to API clients.</summary>
public sealed record ErrorDto(string Code, string Message, string? Field)
{
    public static ErrorDto From(Error error) =>
        new(error.Code, error.Message, string.IsNullOrWhiteSpace(error.Target) ? null : error.Target);
}
