using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Fakeeh.MedicalKit.Infrastructure.Persistence.Repositories;

internal sealed class VitalTypeRepository(IntegrationDbContext db, IMemoryCache cache) : IVitalTypeRepository
{
    private const string CacheKey = "vital-types:active";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<IReadOnlyList<VitalType>> GetAllActiveAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyList<VitalType>? cached) && cached is not null)
        {
            return cached;
        }

        var types = await db.VitalTypes
            .AsNoTracking()
            .Include(t => t.Fields)
            .Where(t => t.IsActive)
            .OrderBy(t => t.Code)
            .ToListAsync(cancellationToken);

        foreach (var type in types)
        {
            type.Fields = type.Fields.OrderBy(f => f.SortOrder).ToList();
        }

        cache.Set(CacheKey, (IReadOnlyList<VitalType>)types, CacheDuration);
        return types;
    }

    public async Task<VitalType?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var types = await GetAllActiveAsync(cancellationToken);
        return types.FirstOrDefault(t => string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}
