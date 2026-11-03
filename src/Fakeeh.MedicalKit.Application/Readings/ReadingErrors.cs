using Fakeeh.MedicalKit.Domain.Common;

namespace Fakeeh.MedicalKit.Application.Readings;

public static class ReadingErrors
{
    public static Error UnknownVitalType(string? code) =>
        Error.Validation("reading.unknown_vital_type", $"Unknown or inactive vital type '{code}'.", "vitalTypeCode");

    public static Error UnknownField(string vitalTypeCode, string key) =>
        Error.Validation("reading.unknown_field", $"Vital type '{vitalTypeCode}' has no field '{key}'.", $"readingValues.{key}");

    public static Error DuplicateField(string key) =>
        Error.Validation("reading.duplicate_field", $"Field '{key}' is sent more than once.", $"readingValues.{key}");

    public static Error ValueRequired(string key) =>
        Error.Validation("reading.value_required", $"Field '{key}' must have a numeric value.", $"readingValues.{key}");

    public static Error MissingRequiredField(string vitalTypeCode, string key) =>
        Error.Validation("reading.required_field_missing", $"Vital type '{vitalTypeCode}' requires field '{key}'.", $"readingValues.{key}");

    public static Error PatientNotFound(string nationalId) =>
        Error.Validation("reading.patient_not_found", $"No patient with national ID / Iqama '{nationalId}' was found in HIS.", "nationalId");

    public static Error HisUnavailable(string message) =>
        Error.Failure("his.unavailable", message);

    public static Error SaveFailed() =>
        Error.Failure("reading.save_failed", "An unexpected error occurred while saving this reading.");
}
