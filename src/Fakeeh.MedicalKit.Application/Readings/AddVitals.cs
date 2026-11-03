using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Application.Common;
using Fakeeh.MedicalKit.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Fakeeh.MedicalKit.Application.Readings;

/// <summary>Stores a collection of KIT readings. Each reading is saved or rejected on its own.</summary>
public sealed record AddVitalsCommand : IRequest<Result<AddVitalsResultDto>>
{
    public IReadOnlyList<VitalInput?>? Payloads { get; init; }
}

internal sealed class AddVitalsCommandValidator : AbstractValidator<AddVitalsCommand>
{
    public AddVitalsCommandValidator(ReadingSettings settings)
    {
        RuleFor(c => c.Payloads)
            .Must(p => p is { Count: > 0 }).WithErrorCode("vitals.empty")
            .WithMessage("payloads must contain at least one reading.")
            .Must(p => p is null || p.Count <= settings.MaxVitalsPerRequest).WithErrorCode("vitals.too_many")
            .WithMessage($"payloads can contain at most {settings.MaxVitalsPerRequest} readings per request.")
            .OverridePropertyName("payloads");
    }
}

internal sealed class AddVitalsCommandHandler(ISender sender, ILogger<AddVitalsCommandHandler> logger)
    : IRequestHandler<AddVitalsCommand, Result<AddVitalsResultDto>>
{
    public async Task<Result<AddVitalsResultDto>> Handle(AddVitalsCommand request, CancellationToken cancellationToken)
    {
        var results = new List<VitalResultDto>(request.Payloads!.Count);

        for (var index = 0; index < request.Payloads.Count; index++)
        {
            results.Add(await RecordAsync(index, request.Payloads[index], cancellationToken));
        }

        return new AddVitalsResultDto(
            results.Count,
            results.Count(r => r.Status == VitalResultStatus.Created),
            results.Count(r => r.Status == VitalResultStatus.AlreadyExists),
            results.Count(r => r.Status == VitalResultStatus.Failed),
            results);
    }

    private async Task<VitalResultDto> RecordAsync(int index, VitalInput? input, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return Failed(index, null, [Error.Validation("vitals.payload_null", "The reading is empty.", $"payloads[{index}]")]);
        }

        try
        {
            var result = await sender.Send(new RecordReadingCommand(input), cancellationToken);
            if (result.IsFailure)
            {
                return Failed(index, input, result.Errors);
            }

            return new VitalResultDto(
                index,
                result.Value.ReadingId,
                input.VitalTypeCode,
                result.Value.Outcome == RecordReadingOutcome.Created ? VitalResultStatus.Created : VitalResultStatus.AlreadyExists,
                result.Value.Id,
                null);
        }
        catch (HisUnavailableException ex)
        {
            logger.LogWarning(ex, "HIS unavailable while saving reading {Index} ({ReadingId})", index, input.ReadingId);
            return Failed(index, input, [ReadingErrors.HisUnavailable(ex.Message)]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Saving reading {Index} ({ReadingId}) failed", index, input.ReadingId);
            return Failed(index, input, [ReadingErrors.SaveFailed()]);
        }
    }

    private static VitalResultDto Failed(int index, VitalInput? input, IEnumerable<Error> errors) =>
        new(index, input?.ReadingId, input?.VitalTypeCode, VitalResultStatus.Failed, null, errors.Select(ErrorDto.From).ToList());
}
