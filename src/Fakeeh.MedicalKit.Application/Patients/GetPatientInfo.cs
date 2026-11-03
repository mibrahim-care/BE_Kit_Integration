using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Application.Common;
using Fakeeh.MedicalKit.Domain.Common;
using FluentValidation;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Patients;

public sealed record GetPatientInfoQuery(string? NationalId) : IRequest<Result<PatientDto>>;

internal sealed class GetPatientInfoQueryValidator : AbstractValidator<GetPatientInfoQuery>
{
    public GetPatientInfoQueryValidator()
    {
        RuleFor(q => q.NationalId).NationalId();
    }
}

internal sealed class GetPatientInfoQueryHandler(IHisPatientClient hisClient)
    : IRequestHandler<GetPatientInfoQuery, Result<PatientDto>>
{
    public async Task<Result<PatientDto>> Handle(GetPatientInfoQuery request, CancellationToken cancellationToken)
    {
        var patient = await hisClient.FindByNationalIdAsync(request.NationalId!, cancellationToken);
        return patient is null ? PatientErrors.NotFound(request.NationalId!) : patient;
    }
}
