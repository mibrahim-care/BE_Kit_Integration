using Fakeeh.MedicalKit.Domain.Entities;

namespace Fakeeh.MedicalKit.Application.Abstractions;

public interface IReadingRepository
{
    Task<Reading?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken);

    void Add(Reading reading);

    /// <summary>Returns, for each vital type, the newest <paramref name="criteria"/>.RecordsPerType readings of the patient.</summary>
    Task<IReadOnlyList<Reading>> GetLatestPerTypeAsync(LatestReadingsCriteria criteria, CancellationToken cancellationToken);
}

public sealed record LatestReadingsCriteria(
    string NationalId,
    int RecordsPerType,
    string? VitalTypeCode,
    DateTimeOffset? From,
    DateTimeOffset? To);
