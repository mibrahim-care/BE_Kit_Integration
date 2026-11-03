using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Domain.Common;
using FluentValidation;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Patients;

public sealed record GetPatientByIdQuery(string PatientId) : IRequest<Result<PatientDto>>;

internal sealed class GetPatientByIdQueryValidator : AbstractValidator<GetPatientByIdQuery>
{
    public GetPatientByIdQueryValidator()
    {
        RuleFor(q => q.PatientId).NotEmpty().WithErrorCode("patient.id_required");
    }
}

internal sealed class GetPatientByIdQueryHandler(IHisPatientClient hisClient)
    : IRequestHandler<GetPatientByIdQuery, Result<PatientDto>>
{
    public async Task<Result<PatientDto>> Handle(GetPatientByIdQuery request, CancellationToken cancellationToken)
    {
        var patient = await hisClient.GetByIdAsync(request.PatientId, cancellationToken);

        return patient is null
            ? PatientErrors.NotFound(request.PatientId)
            : patient;
    }
}
