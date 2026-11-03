using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Domain.Common;
using FluentValidation;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Patients;

public sealed record SearchPatientByIdentifierQuery(PatientIdentifierType IdentifierType, string Value)
    : IRequest<Result<PatientDto?>>;

internal sealed class SearchPatientByIdentifierQueryValidator : AbstractValidator<SearchPatientByIdentifierQuery>
{
    public SearchPatientByIdentifierQueryValidator()
    {
        RuleFor(q => q.Value)
            .NotEmpty().WithErrorCode("patient.identifier_required")
            .Matches(@"^\d{10}$").WithErrorCode("patient.identifier_format")
            .WithMessage("The identifier must be exactly 10 digits.");

        RuleFor(q => q.Value)
            .Must(v => v.StartsWith('1')).When(q => q.IdentifierType == PatientIdentifierType.NationalId)
            .WithErrorCode("patient.identifier_format")
            .WithMessage("A national ID must start with 1.");

        RuleFor(q => q.Value)
            .Must(v => v.StartsWith('2')).When(q => q.IdentifierType == PatientIdentifierType.Iqama)
            .WithErrorCode("patient.identifier_format")
            .WithMessage("An Iqama number must start with 2.");
    }
}

internal sealed class SearchPatientByIdentifierQueryHandler(IHisPatientClient hisClient)
    : IRequestHandler<SearchPatientByIdentifierQuery, Result<PatientDto?>>
{
    public async Task<Result<PatientDto?>> Handle(SearchPatientByIdentifierQuery request, CancellationToken cancellationToken)
    {
        var patient = await hisClient.FindByIdentifierAsync(request.IdentifierType, request.Value, cancellationToken);
        return Result.Success(patient);
    }
}
