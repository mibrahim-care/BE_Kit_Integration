using FluentValidation;

namespace Fakeeh.MedicalKit.Application.Common;

internal static class ValidationExtensions
{
    /// <summary>A Saudi national ID (starts with 1) or Iqama number (starts with 2): exactly 10 digits.</summary>
    public static IRuleBuilderOptions<T, string?> NationalId<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .NotEmpty().WithErrorCode("patient.national_id_required")
            .WithMessage("nationalId is required.")
            .Matches(@"^[12]\d{9}$").WithErrorCode("patient.national_id_format")
            .WithMessage("nationalId must be 10 digits starting with 1 (national ID) or 2 (Iqama).")
            .OverridePropertyName("nationalId");
}
