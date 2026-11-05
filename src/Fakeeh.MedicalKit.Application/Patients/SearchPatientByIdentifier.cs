using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Domain.Common;
using FluentValidation;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Patients;

/// <summary>FHIR Patient search: the patient with this National ID / Iqama, or null when HIS has none.</summary>
public sealed record SearchPatientByIdentifierQuery(PatientIdentifierType IdentifierType, string Value)
    : IRequest<Result<PatientDto?>>;

internal sealed class SearchPatientByIdentifierQueryValidator : AbstractValidator<SearchPatientByIdentifierQuery>
{
    public SearchPatientByIdentifierQueryValidator()
    {
        RuleFor(q => q.Value)
            .NotEmpty().WithErrorCode("patient.identifier_required")
            .WithMessage("The identifier value is required.")
            .Matches(@"^\d{10}$").WithErrorCode("patient.identifier_format")
            .WithMessage("The identifier must be exactly 10 digits.")
            .OverridePropertyName("identifier");

        RuleFor(q => q.Value)
            .Must(v => v.StartsWith('1')).When(q => q.IdentifierType == PatientIdentifierType.NationalId)
            .WithErrorCode("patient.identifier_format")
            .WithMessage("A national ID must start with 1.")
            .OverridePropertyName("identifier");

        RuleFor(q => q.Value)
            .Must(v => v.StartsWith('2')).When(q => q.IdentifierType == PatientIdentifierType.Iqama)
            .WithErrorCode("patient.identifier_format")
            .WithMessage("An Iqama number must start with 2.")
            .OverridePropertyName("identifier");
    }
}

internal sealed class SearchPatientByIdentifierQueryHandler(IHisPatientClient hisClient)
    : IRequestHandler<SearchPatientByIdentifierQuery, Result<PatientDto?>>
{
    public async Task<Result<PatientDto?>> Handle(SearchPatientByIdentifierQuery request, CancellationToken cancellationToken) =>
        Result.Success(await hisClient.FindByNationalIdAsync(request.Value, cancellationToken));
}
