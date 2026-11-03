using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fakeeh.MedicalKit.Infrastructure.Persistence.Repositories;

internal sealed class ReadingRepository(IntegrationDbContext db) : IReadingRepository
{
    public Task<Reading?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken) =>
        db.Readings.AsNoTracking().FirstOrDefaultAsync(r => r.ExternalId == externalId, cancellationToken);

    public void Add(Reading reading) => db.Readings.Add(reading);

    public async Task<IReadOnlyList<Reading>> GetLatestPerTypeAsync(LatestReadingsCriteria criteria, CancellationToken cancellationToken)
    {
        var filtered = db.Readings.AsNoTracking().Where(r => r.NationalId == criteria.NationalId);

        if (criteria.VitalTypeCode is { } code)
        {
            filtered = filtered.Where(r => r.VitalTypeCode == code);
        }

        if (criteria.From is { } from)
        {
            filtered = filtered.Where(r => r.EffectiveAt >= from);
        }

        if (criteria.To is { } to)
        {
            filtered = filtered.Where(r => r.EffectiveAt < to);
        }

        // One CROSS APPLY per vital type: the newest N readings of each type.
        var latest = filtered
            .Select(r => r.VitalTypeCode)
            .Distinct()
            .SelectMany(typeCode => filtered
                .Where(r => r.VitalTypeCode == typeCode)
                .OrderByDescending(r => r.EffectiveAt)
                .ThenByDescending(r => r.ReceivedAt)
                .Take(criteria.RecordsPerType));

        return await latest.AsSplitQuery().ToListAsync(cancellationToken);
    }
}
