using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Domain.Common;
using MediatR;

namespace Fakeeh.MedicalKit.Application.VitalTypes;

public sealed record GetVitalTypesQuery : IRequest<Result<IReadOnlyList<VitalTypeDto>>>;

internal sealed class GetVitalTypesQueryHandler(IVitalTypeRepository vitalTypes)
    : IRequestHandler<GetVitalTypesQuery, Result<IReadOnlyList<VitalTypeDto>>>
{
    public async Task<Result<IReadOnlyList<VitalTypeDto>>> Handle(GetVitalTypesQuery request, CancellationToken cancellationToken)
    {
        var types = await vitalTypes.GetAllActiveAsync(cancellationToken);
        return types.Select(VitalTypeDto.From).ToList();
    }
}
