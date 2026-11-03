using Fakeeh.MedicalKit.Application.Common;
using Fakeeh.MedicalKit.Application.Readings;
using Fakeeh.MedicalKit.Domain.Entities;

namespace Fakeeh.MedicalKit.Application.Abstractions;

public interface IReadingRepository
{
    Task<Reading?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Reading?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken);

    void Add(Reading reading);

    Task<PagedList<Reading>> SearchAsync(ReadingSearchCriteria criteria, CancellationToken cancellationToken);
}
