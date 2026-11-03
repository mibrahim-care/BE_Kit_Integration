using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Application.Common;
using Fakeeh.MedicalKit.Application.Readings;
using Fakeeh.MedicalKit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fakeeh.MedicalKit.Infrastructure.Persistence.Repositories;

internal sealed class ReadingRepository(IntegrationDbContext db) : IReadingRepository
{
    public Task<Reading?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Readings.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Reading?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken) =>
        db.Readings.AsNoTracking().FirstOrDefaultAsync(r => r.ExternalId == externalId, cancellationToken);

    public void Add(Reading reading) => db.Readings.Add(reading);

    public async Task<PagedList<Reading>> SearchAsync(ReadingSearchCriteria criteria, CancellationToken cancellationToken)
    {
        var query = db.Readings.AsNoTracking().Where(r => r.PatientId == criteria.PatientId);

        if (criteria.Codes is not null)
        {
            if (criteria.Codes.Count == 0)
            {
                return new PagedList<Reading>([], 0, criteria.Offset, criteria.Count);
            }

            var wholeTypes = criteria.Codes.Where(c => c.FieldKey is null).Select(c => c.VitalTypeCode).ToList();
            var typeFields = criteria.Codes.Where(c => c.FieldKey is not null).Select(c => c.VitalTypeCode + "|" + c.FieldKey).ToList();

            query = query.Where(r =>
                wholeTypes.Contains(r.VitalTypeCode)
                || (r.FieldKey != null && typeFields.Contains(r.VitalTypeCode + "|" + r.FieldKey)));
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
            ? query.OrderByDescending(r => r.EffectiveAt).ThenByDescending(r => r.ReceivedAt)
            : query.OrderBy(r => r.EffectiveAt).ThenBy(r => r.ReceivedAt);

        var items = await query
            .Skip(criteria.Offset)
            .Take(criteria.Count)
            .ToListAsync(cancellationToken);

        return new PagedList<Reading>(items, total, criteria.Offset, criteria.Count);
    }
}
