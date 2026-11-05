using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Domain.Common;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Observations;

public sealed record GetObservationByIdQuery(Guid Id) : IRequest<Result<ObservationDto>>;

internal sealed class GetObservationByIdQueryHandler(IReadingRepository readings, IVitalTypeRepository vitalTypes)
    : IRequestHandler<GetObservationByIdQuery, Result<ObservationDto>>
{
    public async Task<Result<ObservationDto>> Handle(GetObservationByIdQuery request, CancellationToken cancellationToken)
    {
        var reading = await readings.GetByIdAsync(request.Id, cancellationToken);
        if (reading is null)
        {
            return ObservationErrors.NotFound(request.Id);
        }

        var type = await vitalTypes.GetByCodeAsync(reading.VitalTypeCode, cancellationToken);
        return reading.ToObservationDto(type);
    }
}
