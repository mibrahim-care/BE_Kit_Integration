namespace Fakeeh.MedicalKit.Application.Readings;

public sealed record ReadingInputDto
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

    public IReadOnlyList<ReadingComponentInputDto> Components { get; init; } = [];

    public string? Interpretation { get; init; }

    public string? Note { get; init; }

    public string? Address { get; init; }

    public string? PerformerRef { get; init; }

    public string? DeviceRef { get; init; }
}

public sealed record ReadingComponentInputDto(
    string? FieldKey,
    string? LoincCode,
    decimal? Value,
    string? Unit,
    string? UcumCode);

public sealed record ReadingDto(
    Guid Id,
    string ExternalId,
    string PatientId,
    string? NationalId,
    string VitalTypeCode,
    string VitalTypeName,
    string? VitalTypeLoincCode,
    string? FieldKey,
    string? FieldLabel,
    string? FieldLoincCode,
    string Category,
    string Status,
    DateTimeOffset EffectiveAt,
    decimal? Value,
    string? Unit,
    string? UcumCode,
    IReadOnlyList<ReadingComponentDto> Components,
    string? Interpretation,
    string? Note,
    string? Address,
    string? PerformerRef,
    string? DeviceRef,
    DateTimeOffset ReceivedAt);

public sealed record ReadingComponentDto(
    string FieldKey,
    string Label,
    string? LoincCode,
    decimal Value,
    string Unit,
    string UcumCode);

public enum RecordReadingOutcome
{
    Created,
    AlreadyExists
}

public sealed record RecordReadingResult(Guid Id, RecordReadingOutcome Outcome);
