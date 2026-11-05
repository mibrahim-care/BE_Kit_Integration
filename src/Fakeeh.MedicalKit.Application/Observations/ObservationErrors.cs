using Fakeeh.MedicalKit.Domain.Common;

namespace Fakeeh.MedicalKit.Application.Observations;

public static class ObservationErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("reading.not_found", $"Observation '{id}' was not found.");

    public static Error UnknownVitalType(string? code) =>
        Error.Validation("reading.unknown_vital_type", $"Unknown or inactive vital type '{code}'.", "code");

    public static Error VitalTypeNotIdentified() =>
        Error.Validation(
            "reading.vital_type_required",
            "The Observation code must include a known vital-type code or LOINC code.",
            "code");

    public static Error FieldRequired(string vitalTypeCode) =>
        Error.Validation(
            "reading.field_required",
            $"Vital type '{vitalTypeCode}' is recorded one Observation per field; the code must include the field code.",
            "code");

    public static Error UnknownField(string vitalTypeCode, string? fieldKey) =>
        Error.Validation("reading.unknown_field", $"Vital type '{vitalTypeCode}' has no field '{fieldKey}'.", "code");

    public static Error ValueRequired(string target) =>
        Error.Validation("reading.value_required", "A numeric value is required.", target);

    public static Error ComponentsNotAllowed(string vitalTypeCode) =>
        Error.Validation("reading.components_not_allowed", $"Vital type '{vitalTypeCode}' does not use components.", "component");

    public static Error ComponentsRequired(string vitalTypeCode) =>
        Error.Validation("reading.components_required", $"Vital type '{vitalTypeCode}' must be sent with components.", "component");

    public static Error DuplicateComponent(string fieldKey) =>
        Error.Validation("reading.duplicate_component", $"Component '{fieldKey}' is sent more than once.", "component");

    public static Error MissingRequiredComponent(string vitalTypeCode, string fieldKey) =>
        Error.Validation(
            "reading.required_component_missing",
            $"Vital type '{vitalTypeCode}' requires component '{fieldKey}'.",
            "component");

    public static Error UnitMismatch(string fieldKey, string expectedUcum, string? actual) =>
        Error.Validation(
            "reading.unit_mismatch",
            $"Field '{fieldKey}' must use UCUM unit '{expectedUcum}', but '{actual}' was sent.",
            "valueQuantity");

    public static Error PatientNotFound(string patientId) =>
        Error.Validation("reading.patient_not_found", $"Patient '{patientId}' was not found in HIS.", "subject");
}
