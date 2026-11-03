using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Domain.Common;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Readings;

public sealed record GetReadingByIdQuery(Guid Id) : IRequest<Result<ReadingDto>>;

internal sealed class GetReadingByIdQueryHandler(IReadingRepository readings, IVitalTypeRepository vitalTypes)
    : IRequestHandler<GetReadingByIdQuery, Result<ReadingDto>>
{
    public async Task<Result<ReadingDto>> Handle(GetReadingByIdQuery request, CancellationToken cancellationToken)
    {
        var reading = await readings.GetByIdAsync(request.Id, cancellationToken);
        if (reading is null)
        {
            return ReadingErrors.NotFound(request.Id);
        }

        var type = await vitalTypes.GetByCodeAsync(reading.VitalTypeCode, cancellationToken);
        return reading.ToDto(type);
    }
}
