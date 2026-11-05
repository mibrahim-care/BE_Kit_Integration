using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Application.Common;
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

    public Task<Reading?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Readings.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<PagedList<Reading>> SearchAsync(ReadingSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var query = db.Readings.AsNoTracking();

        if (criteria.PatientId is { } patientId)
        {
            query = query.Where(r => r.PatientId == patientId);
        }

        if (criteria.Id is { } id)
        {
            query = query.Where(r => r.Id == id);
        }

        if (criteria.Codes is not null)
        {
            if (criteria.Codes.Count == 0)
            {
                return new PagedList<Reading>([], 0, criteria.Offset, criteria.Count);
            }

            // Inlined as IN (...) lists: Oracle 19 has no translation for parameterized collections.
            var wholeTypes = criteria.Codes.Where(c => c.FieldKey is null).Select(c => c.VitalTypeCode).ToList();
            var typeFields = criteria.Codes.Where(c => c.FieldKey is not null).Select(c => c.VitalTypeCode + "|" + c.FieldKey).ToList();

            // A per-field reading from FHIR has FieldKey set; one from AddVitals carries several fields as components.
            query = query.Where(r =>
                EF.Constant(wholeTypes).Contains(r.VitalTypeCode)
                || (r.FieldKey != null && EF.Constant(typeFields).Contains(r.VitalTypeCode + "|" + r.FieldKey))
                || (r.FieldKey == null && r.Components.Any(c => EF.Constant(typeFields).Contains(r.VitalTypeCode + "|" + c.FieldKey))));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Category))
        {
            query = query.Where(r => r.Category == criteria.Category);
        }

        if (criteria.From is { } from)
        {
            query = query.Where(r => r.EffectiveAt >= from);
        }

        if (criteria.To is { } to)
        {
            query = query.Where(r => r.EffectiveAt < to);
        }

        var total = await query.CountAsync(cancellationToken);

        query = criteria.SortDescending
            ? query.OrderByDescending(r => r.EffectiveAt).ThenByDescending(r => r.ReceivedAt).ThenBy(r => r.Id)
            : query.OrderBy(r => r.EffectiveAt).ThenBy(r => r.ReceivedAt).ThenBy(r => r.Id);

        var items = await query
            .Skip(criteria.Offset)
            .Take(criteria.Count)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PagedList<Reading>(items, total, criteria.Offset, criteria.Count);
    }
}
