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

        if (criteria.RecordsPerType is { } perType)
        {
            return await SearchPerTypeAsync(query, criteria, perType, cancellationToken);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await Order(query, criteria.SortDescending)
            .Skip(criteria.Offset)
            .Take(criteria.Count)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new PagedList<Reading>(items, total, criteria.Offset, criteria.Count);
    }

    /// <summary>
    /// Pages each vital type on its own: skips <c>Offset</c> and takes <paramref name="perType"/> readings of every type.
    /// Results are grouped by vital type and sorted by date within each type.
    /// </summary>
    private static async Task<PagedList<Reading>> SearchPerTypeAsync(
        IQueryable<Reading> query, ReadingSearchCriteria criteria, int perType, CancellationToken cancellationToken)
    {
        var countsPerType = await query
            .GroupBy(r => r.VitalTypeCode)
            .Select(g => g.Count())
            .ToListAsync(cancellationToken);

        var total = countsPerType.Sum();
        var hasMore = countsPerType.Any(c => c > criteria.Offset + perType);

        if (!countsPerType.Any(c => c > criteria.Offset))
        {
            return new PagedList<Reading>([], total, criteria.Offset, perType) { HasMore = hasMore };
        }

        // One CROSS APPLY per vital type (as in GetLatestPerTypeAsync), with OFFSET … FETCH inside each.
        var items = await query
            .Select(r => r.VitalTypeCode)
            .Distinct()
            .SelectMany(typeCode => Order(query.Where(r => r.VitalTypeCode == typeCode), criteria.SortDescending)
                .Skip(criteria.Offset)
                .Take(perType))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var byType = items.OrderBy(r => r.VitalTypeCode, StringComparer.OrdinalIgnoreCase);
        var sorted = criteria.SortDescending
            ? byType.ThenByDescending(r => r.EffectiveAt).ThenByDescending(r => r.ReceivedAt).ThenBy(r => r.Id)
            : byType.ThenBy(r => r.EffectiveAt).ThenBy(r => r.ReceivedAt).ThenBy(r => r.Id);

        return new PagedList<Reading>(sorted.ToList(), total, criteria.Offset, perType) { HasMore = hasMore };
    }

    private static IOrderedQueryable<Reading> Order(IQueryable<Reading> query, bool descending) =>
        descending
            ? query.OrderByDescending(r => r.EffectiveAt).ThenByDescending(r => r.ReceivedAt).ThenBy(r => r.Id)
            : query.OrderBy(r => r.EffectiveAt).ThenBy(r => r.ReceivedAt).ThenBy(r => r.Id);
}
