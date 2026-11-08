using Fakeeh.MedicalKit.Application.Common;
using Fakeeh.MedicalKit.Domain.Entities;

namespace Fakeeh.MedicalKit.Application.Abstractions;

public interface IReadingRepository
{
    Task<Reading?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Reading?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken);

    void Add(Reading reading);

    /// <summary>Returns, for each vital type, the newest <paramref name="criteria"/>.RecordsPerType readings of the patient.</summary>
    Task<IReadOnlyList<Reading>> GetLatestPerTypeAsync(LatestReadingsCriteria criteria, CancellationToken cancellationToken);

    /// <summary>One page of readings matching the FHIR Observation search.</summary>
    Task<PagedList<Reading>> SearchAsync(ReadingSearchCriteria criteria, CancellationToken cancellationToken);
}

public sealed record LatestReadingsCriteria(
    string NationalId,
    int RecordsPerType,
    string? VitalTypeCode,
    DateTimeOffset? From,
    DateTimeOffset? To);

/// <summary>A vital type, or one field of a per-field vital type, that a reading must match.</summary>
public sealed record CodeMatch(string VitalTypeCode, string? FieldKey);

public sealed record ReadingSearchCriteria
{
    /// <summary>Patient id (National ID / Iqama). Null only when <see cref="Id"/> is set.</summary>
    public string? PatientId { get; init; }

    public Guid? Id { get; init; }

    /// <summary>Null: any code. Empty: nothing can match.</summary>
    public IReadOnlyList<CodeMatch>? Codes { get; init; }

    public string? Category { get; init; }

    public DateTimeOffset? From { get; init; }

    /// <summary>Exclusive upper bound.</summary>
    public DateTimeOffset? To { get; init; }

    public bool SortDescending { get; init; } = true;

    public int Offset { get; init; }

    public int Count { get; init; } = 20;

    /// <summary>
    /// When set, paging is per vital type: up to this many readings of each vital type, skipping <see cref="Offset"/>
    /// readings within each type. <see cref="Count"/> is then ignored.
    /// </summary>
    public int? RecordsPerType { get; init; }
}
