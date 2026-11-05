using System.Globalization;
using System.Text.RegularExpressions;
using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Application.Readings;
using Fakeeh.MedicalKit.Domain.Common;
using Fakeeh.MedicalKit.Domain.Entities;
using Fakeeh.MedicalKit.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Observations;

/// <summary>Validates and stores one reading sent as a FHIR Observation. Idempotent on the KIT reading id.</summary>
public sealed record RecordObservationCommand(ObservationInputDto Reading) : IRequest<Result<RecordReadingResult>>;

internal sealed partial class RecordObservationCommandValidator : AbstractValidator<RecordObservationCommand>
{
    private static readonly string[] AllowedStatuses = ["final", "amended", "corrected", "preliminary"];

    public RecordObservationCommandValidator()
    {
        RuleFor(c => c.Reading.ExternalId)
            .NotEmpty().WithErrorCode("reading.identifier_required")
            .WithMessage("Observation.identifier (the KIT reading id) is required.")
            .MaximumLength(200).WithErrorCode("reading.identifier_too_long")
            .OverridePropertyName("identifier");

        RuleFor(c => c.Reading.PatientId)
            .NotEmpty().WithErrorCode("reading.subject_required")
            .WithMessage("Observation.subject must reference a patient as Patient/{id}.")
            .OverridePropertyName("subject");

        RuleFor(c => c.Reading.EffectiveDateTime)
            .NotEmpty().WithErrorCode("reading.effective_required")
            .WithMessage("Observation.effectiveDateTime (when the reading was taken) is required.")
            .Must(HaveTimeZone).WithErrorCode("reading.effective_timezone")
            .WithMessage("Observation.effectiveDateTime must be a full date-time with a time zone, e.g. 2026-10-18T07:58:42+03:00.")
            .OverridePropertyName("effectiveDateTime");

        RuleFor(c => c.Reading)
            .Must(r => !string.IsNullOrWhiteSpace(r.VitalTypeCode) || !string.IsNullOrWhiteSpace(r.LoincCode))
            .WithErrorCode("reading.vital_type_required")
            .WithMessage("The Observation code must include a vital-type code or a LOINC code.")
            .OverridePropertyName("code");

        RuleFor(c => c.Reading)
            .Must(r => r.Value is not null || r.Components.Count > 0)
            .WithErrorCode("reading.value_required")
            .WithMessage("The Observation must have a valueQuantity or components.")
            .OverridePropertyName("value[x]");

        RuleFor(c => c.Reading.Status)
            .Must(s => s is null || AllowedStatuses.Contains(s))
            .WithErrorCode("reading.status_invalid")
            .WithMessage($"Observation.status must be one of: {string.Join(", ", AllowedStatuses)}.")
            .OverridePropertyName("status");

        RuleFor(c => c.Reading.Note).MaximumLength(2000).OverridePropertyName("note");
        RuleFor(c => c.Reading.Address).MaximumLength(500).OverridePropertyName("extension[address]");
        RuleFor(c => c.Reading.PerformerRef).MaximumLength(200).OverridePropertyName("performer");
        RuleFor(c => c.Reading.DeviceRef).MaximumLength(200).OverridePropertyName("device");
    }

    internal static bool HaveTimeZone(string? value) =>
        value is not null
        && TimeZoneRegex().IsMatch(value)
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex TimeZoneRegex();
}

internal sealed partial class RecordObservationCommandHandler(
    IReadingRepository readings,
    IUnitOfWork unitOfWork,
    IHisPatientClient hisClient,
    ObservationResolver resolver,
    IClock clock)
    : IRequestHandler<RecordObservationCommand, Result<RecordReadingResult>>
{
    public async Task<Result<RecordReadingResult>> Handle(RecordObservationCommand request, CancellationToken cancellationToken)
    {
        var input = request.Reading;
        var externalId = input.ExternalId.Trim();

        var existing = await readings.GetByExternalIdAsync(externalId, cancellationToken);
        if (existing is not null)
        {
            return new RecordReadingResult(existing.Id, externalId, RecordReadingOutcome.AlreadyExists);
        }

        var resolved = await resolver.ResolveAsync(input, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<RecordReadingResult>(resolved.Errors);
        }

        // Patient.id is the National ID / Iqama number itself.
        var patientId = input.PatientId.Trim();
        var patient = NationalIdRegex().IsMatch(patientId)
            ? await hisClient.FindByNationalIdAsync(patientId, cancellationToken)
            : null;
        if (patient is null)
        {
            return ObservationErrors.PatientNotFound(patientId);
        }

        var reading = BuildReading(input, externalId, resolved.Value, patient.NationalId);
        readings.Add(reading);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            var winner = await readings.GetByExternalIdAsync(externalId, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return new RecordReadingResult(winner.Id, externalId, RecordReadingOutcome.AlreadyExists);
        }

        return new RecordReadingResult(reading.Id, externalId, RecordReadingOutcome.Created);
    }

    // Values are stored as components, the same way AddVitals stores them, so both APIs read every reading.
    private Reading BuildReading(ObservationInputDto input, string externalId, ResolvedObservation resolved, string nationalId)
    {
        var type = resolved.VitalType;

        return new Reading
        {
            Id = Guid.NewGuid(),
            ExternalId = externalId,
            PatientId = nationalId,
            NationalId = nationalId,
            VitalTypeCode = type.Code,
            FieldKey = type.ObservationModel == ObservationModel.PerField ? resolved.Field?.Key : null,
            Category = type.Category,
            Status = input.Status ?? "final",
            EffectiveAt = DateTimeOffset.Parse(input.EffectiveDateTime!, CultureInfo.InvariantCulture),
            Components = resolved.Components
                .Select(c => new ReadingComponent
                {
                    FieldKey = c.Field.Key,
                    LoincCode = c.Field.LoincCode,
                    Value = c.Value,
                    Unit = c.Field.Unit,
                    UcumCode = c.Field.UcumCode
                })
                .ToList(),
            Interpretation = NullIfEmpty(input.Interpretation),
            Note = NullIfEmpty(input.Note),
            Address = NullIfEmpty(input.Address),
            PerformerRef = NullIfEmpty(input.PerformerRef),
            DeviceRef = NullIfEmpty(input.DeviceRef),
            ReceivedAt = clock.UtcNow
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^[12]\d{9}$")]
    private static partial Regex NationalIdRegex();
}
