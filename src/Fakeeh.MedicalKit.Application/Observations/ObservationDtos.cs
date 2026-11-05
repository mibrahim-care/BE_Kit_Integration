using Fakeeh.MedicalKit.Domain.Enums;

namespace Fakeeh.MedicalKit.Application.Observations;

/// <summary>One reading as received in a FHIR Observation, before validation.</summary>
public sealed record ObservationInputDto
{
    public string ExternalId { get; init; } = string.Empty;

    public string PatientId { get; init; } = string.Empty;

    public string? VitalTypeCode { get; init; }

    public string? FieldKey { get; init; }

    public string? LoincCode { get; init; }

    public string? Status { get; init; }

    public string? EffectiveDateTime { get; init; }

    public decimal? Value { get; init; }

    public string? Unit { get; init; }

    public string? UcumCode { get; init; }

    public IReadOnlyList<ObservationComponentInputDto> Components { get; init; } = [];

    public string? Interpretation { get; init; }

    public string? Note { get; init; }

    public string? Address { get; init; }

    public string? PerformerRef { get; init; }

    public string? DeviceRef { get; init; }
}

public sealed record ObservationComponentInputDto(
    string? FieldKey,
    string? LoincCode,
    decimal? Value,
    string? Unit,
    string? UcumCode);

/// <summary>
/// A stored reading shaped as a FHIR Observation: either one value (<see cref="Value"/>, plus <see cref="FieldKey"/>
/// for per-field types) or a list of <see cref="Components"/>.
/// </summary>
public sealed record ObservationDto(
    Guid Id,
    string ExternalId,
    string PatientId,
    string? NationalId,
    string VitalTypeCode,
    string VitalTypeName,
    string? VitalTypeLoincCode,
    ObservationModel? ObservationModel,
    string? FieldKey,
    string? FieldLabel,
    string? FieldLoincCode,
    string Category,
    string Status,
    DateTimeOffset EffectiveAt,
    decimal? Value,
    string? Unit,
    string? UcumCode,
    IReadOnlyList<ObservationComponentDto> Components,
    string? Interpretation,
    string? Note,
    string? Address,
    string? PerformerRef,
    string? DeviceRef,
    DateTimeOffset ReceivedAt);

public sealed record ObservationComponentDto(
    string FieldKey,
    string Label,
    string? LoincCode,
    decimal Value,
    string Unit,
    string UcumCode);
