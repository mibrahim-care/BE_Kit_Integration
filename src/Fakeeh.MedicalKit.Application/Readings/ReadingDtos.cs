using Fakeeh.MedicalKit.Application.Common;

namespace Fakeeh.MedicalKit.Application.Readings;

/// <summary>One KIT reading, in the KIT's own payload format.</summary>
public sealed record VitalInput
{
    /// <summary>The KIT's unique id for this reading. Re-sending the same id does not create a duplicate.</summary>
    public string? ReadingId { get; init; }

    public string? NationalId { get; init; }

    public string? VitalTypeCode { get; init; }

    /// <summary>Field key → value, e.g. { "systolic": 121, "diastolic": 81 } or { "Value": 37.5 }.</summary>
    public Dictionary<string, decimal?>? ReadingValues { get; init; }

    /// <summary>When the reading was taken. Without a UTC offset, the configured offset (+03:00) is assumed. Defaults to the time it is received.</summary>
    public string? DateTime { get; init; }

    public bool? IsAbnormal { get; init; }

    public string? Notes { get; init; }

    public string? Address { get; init; }

    public string? DeviceId { get; init; }

    public string? NurseId { get; init; }
}

public enum RecordReadingOutcome
{
    Created,
    AlreadyExists
}

public sealed record RecordReadingResult(Guid Id, string ReadingId, RecordReadingOutcome Outcome);

public enum VitalResultStatus
{
    Created,
    AlreadyExists,
    Failed
}

public sealed record VitalResultDto(
    int Index,
    string? ReadingId,
    string? VitalTypeCode,
    VitalResultStatus Status,
    Guid? Id,
    IReadOnlyList<ErrorDto>? Errors);

public sealed record AddVitalsResultDto(
    int Total,
    int Created,
    int AlreadyExisted,
    int Failed,
    IReadOnlyList<VitalResultDto> Results);

public sealed record LatestVitalsDto(
    string NationalId,
    int RecordsPerType,
    IReadOnlyList<VitalTypeReadingsDto> VitalTypes);

public sealed record VitalTypeReadingsDto(
    string VitalTypeCode,
    string VitalTypeName,
    string? Unit,
    IReadOnlyList<VitalFieldDto> Fields,
    IReadOnlyList<VitalReadingDto> Readings);

public sealed record VitalFieldDto(string Key, string Label, string Unit);

public sealed record VitalReadingDto(
    Guid Id,
    string ReadingId,
    DateTimeOffset DateTime,
    IReadOnlyDictionary<string, decimal> Values,
    bool? IsAbnormal,
    string? Notes,
    string? Address,
    string? DeviceId,
    string? NurseId,
    DateTimeOffset ReceivedAt);
