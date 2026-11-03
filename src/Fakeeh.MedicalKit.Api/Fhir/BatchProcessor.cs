using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Fakeeh.MedicalKit.Application.Readings;
using Fakeeh.MedicalKit.Domain.Common;
using Hl7.Fhir.Model;
using MediatR;

namespace Fakeeh.MedicalKit.Api.Fhir;

public sealed class BatchProcessor(ISender sender, ObservationFhirMapper mapper, ILogger<BatchProcessor> logger)
{
    public async Task<Bundle> ProcessAsync(Bundle batch, CancellationToken cancellationToken)
    {
        var response = new Bundle
        {
            Id = Guid.NewGuid().ToString(),
            Type = Bundle.BundleType.BatchResponse,
            Meta = new Meta { LastUpdated = DateTimeOffset.UtcNow }
        };

        foreach (var entry in batch.Entry)
        {
            response.Entry.Add(new Bundle.EntryComponent
            {
                Response = await ProcessEntryAsync(entry, cancellationToken)
            });
        }

        return response;
    }

    private async Task<Bundle.ResponseComponent> ProcessEntryAsync(Bundle.EntryComponent entry, CancellationToken cancellationToken)
    {
        if (entry.Request?.Method != Bundle.HTTPVerb.POST
            || !string.Equals(entry.Request.Url?.Trim('/'), "Observation", StringComparison.OrdinalIgnoreCase))
        {
            return Failed("405 Method Not Allowed", OperationOutcomes.Single(
                OperationOutcome.IssueType.NotSupported,
                "Only 'POST Observation' entries are supported in a batch.",
                code: "batch.entry_not_supported"));
        }

        if (entry.Resource is not Observation observation)
        {
            return Failed("400 Bad Request", OperationOutcomes.Single(
                OperationOutcome.IssueType.Invalid,
                "The entry resource must be an Observation.",
                code: "batch.resource_not_observation"));
        }

        try
        {
            var input = mapper.ToInput(observation, ExternalIdFromIfNoneExist(entry.Request.IfNoneExist));
            var result = await sender.Send(new RecordReadingCommand(input), cancellationToken);

            if (result.IsFailure)
            {
                return Failed(StatusLine(result.Error), OperationOutcomes.FromErrors(result.Errors));
            }

            return new Bundle.ResponseComponent
            {
                Status = result.Value.Outcome == RecordReadingOutcome.Created ? "201 Created" : "200 OK",
                Location = $"Observation/{result.Value.Id}",
                LastModified = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Batch entry {FullUrl} failed", entry.FullUrl);
            return Failed("500 Internal Server Error", OperationOutcomes.Single(
                OperationOutcome.IssueType.Exception,
                "An unexpected error occurred while saving this reading.",
                code: "batch.entry_failed"));
        }
    }

    public static string? ExternalIdFromIfNoneExist(string? ifNoneExist)
    {
        if (string.IsNullOrWhiteSpace(ifNoneExist))
        {
            return null;
        }

        foreach (var part in ifNoneExist.TrimStart('?').Split('&'))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0] == "identifier")
            {
                var value = Uri.UnescapeDataString(pair[1]);
                var pipe = value.LastIndexOf('|');
                return pipe < 0 ? value : value[(pipe + 1)..];
            }
        }

        return null;
    }

    private static Bundle.ResponseComponent Failed(string status, OperationOutcome outcome) =>
        new() { Status = status, Outcome = outcome };

    private static string StatusLine(Error error)
    {
        var status = OperationOutcomes.StatusFor(error);
        return $"{(int)status} {status switch
        {
            System.Net.HttpStatusCode.BadRequest => "Bad Request",
            System.Net.HttpStatusCode.NotFound => "Not Found",
            System.Net.HttpStatusCode.Conflict => "Conflict",
            System.Net.HttpStatusCode.Unauthorized => "Unauthorized",
            _ => "Internal Server Error"
        }}";
    }
}
