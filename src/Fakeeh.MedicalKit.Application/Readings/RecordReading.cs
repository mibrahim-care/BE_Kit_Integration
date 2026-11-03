using System.Globalization;
using System.Text.RegularExpressions;
using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Domain.Common;
using Fakeeh.MedicalKit.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Readings;

public sealed record RecordReadingCommand(ReadingInputDto Reading) : IRequest<Result<RecordReadingResult>>;

internal sealed partial class RecordReadingCommandValidator : AbstractValidator<RecordReadingCommand>
{
    private static readonly string[] AllowedStatuses = ["final", "amended", "corrected", "preliminary"];

    public RecordReadingCommandValidator()
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
    }

    internal static bool HaveTimeZone(string? value) =>
        value is not null
        && TimeZoneRegex().IsMatch(value)
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex TimeZoneRegex();
}

internal sealed class RecordReadingCommandHandler(
    IReadingRepository readings,
    IUnitOfWork unitOfWork,
    IHisPatientClient hisClient,
    VitalTypeResolver resolver,
    IClock clock)
    : IRequestHandler<RecordReadingCommand, Result<RecordReadingResult>>
{
    public async Task<Result<RecordReadingResult>> Handle(RecordReadingCommand request, CancellationToken cancellationToken)
    {
        var input = request.Reading;

        var existing = await readings.GetByExternalIdAsync(input.ExternalId, cancellationToken);
        if (existing is not null)
        {
            return new RecordReadingResult(existing.Id, RecordReadingOutcome.AlreadyExists);
        }

        var resolved = await resolver.ResolveAsync(input, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<RecordReadingResult>(resolved.Errors);
        }

        var patient = await hisClient.GetByIdAsync(input.PatientId, cancellationToken);
        if (patient is null)
        {
            return ReadingErrors.PatientNotFound(input.PatientId);
        }

        var reading = BuildReading(input, resolved.Value, patient.IdentifierValue);
        readings.Add(reading);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            var winner = await readings.GetByExternalIdAsync(input.ExternalId, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return new RecordReadingResult(winner.Id, RecordReadingOutcome.AlreadyExists);
        }

        return new RecordReadingResult(reading.Id, RecordReadingOutcome.Created);
    }

    private Reading BuildReading(ReadingInputDto input, ResolvedReading resolved, string nationalId)
    {
        var type = resolved.VitalType;
        var field = resolved.Field;

        return new Reading
        {
            Id = Guid.NewGuid(),
            ExternalId = input.ExternalId,
            PatientId = input.PatientId,
            NationalId = nationalId,
            VitalTypeCode = type.Code,
            FieldKey = type.ObservationModel == Domain.Enums.ObservationModel.PerField ? field?.Key : null,
            Category = type.Category,
            Status = input.Status ?? "final",
            EffectiveAt = DateTimeOffset.Parse(input.EffectiveDateTime!, CultureInfo.InvariantCulture),
            Value = resolved.Value,
            Unit = field?.Unit,
            UcumCode = field?.UcumCode,
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
            Interpretation = input.Interpretation,
            Note = input.Note,
            Address = input.Address,
            PerformerRef = input.PerformerRef,
            DeviceRef = input.DeviceRef,
            ReceivedAt = clock.UtcNow
        };
    }
}
