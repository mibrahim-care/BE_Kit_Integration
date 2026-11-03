using Fakeeh.MedicalKit.Domain.Entities;

namespace Fakeeh.MedicalKit.Application.Abstractions;

public interface IVitalTypeRepository
{
    Task<IReadOnlyList<VitalType>> GetAllActiveAsync(CancellationToken cancellationToken);

    Task<VitalType?> GetByCodeAsync(string code, CancellationToken cancellationToken);
}
