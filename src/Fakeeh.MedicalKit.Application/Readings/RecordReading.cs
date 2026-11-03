using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Application.Common;
using Fakeeh.MedicalKit.Domain.Common;
using Fakeeh.MedicalKit.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Fakeeh.MedicalKit.Application.Readings;

/// <summary>Validates and stores one KIT reading. Used by <see cref="AddVitalsCommand"/> for each payload.</summary>
public sealed record RecordReadingCommand(VitalInput Reading) : IRequest<Result<RecordReadingResult>>;

internal sealed class RecordReadingCommandValidator : AbstractValidator<RecordReadingCommand>
{
    public RecordReadingCommandValidator(ReadingSettings settings)
    {
        RuleFor(c => c.Reading.NationalId).NationalId();

        RuleFor(c => c.Reading.ReadingId)
            .MaximumLength(200).WithErrorCode("reading.reading_id_too_long")
            .OverridePropertyName("readingId");

        RuleFor(c => c.Reading.VitalTypeCode)
            .NotEmpty().WithErrorCode("reading.vital_type_required")
            .WithMessage("vitalTypeCode is required.")
            .OverridePropertyName("vitalTypeCode");

        RuleFor(c => c.Reading.ReadingValues)
            .Must(v => v is { Count: > 0 }).WithErrorCode("reading.values_required")
            .WithMessage("readingValues must contain at least one value.")
            .OverridePropertyName("readingValues");

        RuleFor(c => c.Reading.DateTime)
            .Must(v => KitDateTime.TryParse(v, settings.DefaultTimeZoneOffset, out _))
            .When(c => !string.IsNullOrWhiteSpace(c.Reading.DateTime))
            .WithErrorCode("reading.date_time_invalid")
            .WithMessage("dateTime must be an ISO 8601 date-time, e.g. 2026-10-18T07:58:42 or 2026-10-18T07:58:42+03:00.")
            .OverridePropertyName("dateTime");

        RuleFor(c => c.Reading.Notes).MaximumLength(2000).OverridePropertyName("notes");
        RuleFor(c => c.Reading.Address).MaximumLength(500).OverridePropertyName("address");
        RuleFor(c => c.Reading.DeviceId).MaximumLength(200).OverridePropertyName("deviceId");
        RuleFor(c => c.Reading.NurseId).MaximumLength(200).OverridePropertyName("nurseId");
    }
}

internal sealed class RecordReadingCommandHandler(
    IReadingRepository readings,
    IVitalTypeRepository vitalTypes,
    IUnitOfWork unitOfWork,
    IHisPatientClient hisClient,
    ReadingSettings settings,
    IClock clock)
    : IRequestHandler<RecordReadingCommand, Result<RecordReadingResult>>
{
    public async Task<Result<RecordReadingResult>> Handle(RecordReadingCommand request, CancellationToken cancellationToken)
    {
        var input = request.Reading;
        var readingId = string.IsNullOrWhiteSpace(input.ReadingId) ? Guid.NewGuid().ToString() : input.ReadingId.Trim();

        var existing = await readings.GetByExternalIdAsync(readingId, cancellationToken);
        if (existing is not null)
        {
            return new RecordReadingResult(existing.Id, readingId, RecordReadingOutcome.AlreadyExists);
        }

        var type = await vitalTypes.GetByCodeAsync(input.VitalTypeCode!, cancellationToken);
        if (type is null)
        {
            return ReadingErrors.UnknownVitalType(input.VitalTypeCode);
        }

        var components = ResolveValues(type, input.ReadingValues!);
        if (components.IsFailure)
        {
            return Result.Failure<RecordReadingResult>(components.Errors);
        }

        var patient = await hisClient.FindByNationalIdAsync(input.NationalId!, cancellationToken);
        if (patient is null)
        {
            return ReadingErrors.PatientNotFound(input.NationalId!);
        }

        var now = clock.UtcNow;
        var reading = new Reading
        {
            Id = Guid.NewGuid(),
            ExternalId = readingId,
            PatientId = patient.NationalId,
            NationalId = patient.NationalId,
            VitalTypeCode = type.Code,
            Category = type.Category,
            Status = "final",
            EffectiveAt = KitDateTime.TryParse(input.DateTime, settings.DefaultTimeZoneOffset, out var takenAt)
                ? takenAt
                : now.ToOffset(settings.DefaultTimeZoneOffset),
            Components = components.Value,
            Interpretation = input.IsAbnormal switch
            {
                true => "A",
                false => "N",
                null => null
            },
            Note = NullIfEmpty(input.Notes),
            Address = NullIfEmpty(input.Address),
            DeviceRef = NullIfEmpty(input.DeviceId),
            PerformerRef = NullIfEmpty(input.NurseId),
            ReceivedAt = now
        };

        readings.Add(reading);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            var winner = await readings.GetByExternalIdAsync(readingId, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return new RecordReadingResult(winner.Id, readingId, RecordReadingOutcome.AlreadyExists);
        }

        return new RecordReadingResult(reading.Id, readingId, RecordReadingOutcome.Created);
    }

    private static Result<List<ReadingComponent>> ResolveValues(VitalType type, IReadOnlyDictionary<string, decimal?> values)
    {
        var errors = new List<Error>();
        var components = new List<(VitalTypeField Field, decimal Value)>();

        foreach (var (key, value) in values)
        {
            var field = type.FindField(key);
            if (field is null)
            {
                errors.Add(ReadingErrors.UnknownField(type.Code, key));
                continue;
            }

            if (components.Any(c => c.Field.Key == field.Key))
            {
                errors.Add(ReadingErrors.DuplicateField(field.Key));
                continue;
            }

            if (value is null)
            {
                errors.Add(ReadingErrors.ValueRequired(field.Key));
                continue;
            }

            components.Add((field, value.Value));
        }

        errors.AddRange(type.Fields
            .Where(f => f.Required && components.All(c => c.Field.Key != f.Key))
            .Select(f => ReadingErrors.MissingRequiredField(type.Code, f.Key)));

        if (errors.Count > 0)
        {
            return Result.Failure<List<ReadingComponent>>(errors);
        }

        return components
            .OrderBy(c => c.Field.SortOrder)
            .Select(c => new ReadingComponent
            {
                FieldKey = c.Field.Key,
                LoincCode = c.Field.LoincCode,
                Value = c.Value,
                Unit = c.Field.Unit,
                UcumCode = c.Field.UcumCode
            })
            .ToList();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
