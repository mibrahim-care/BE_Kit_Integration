using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Fakeeh.MedicalKit.Api.Fhir.Search;
using Fakeeh.MedicalKit.Application.Readings;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fakeeh.MedicalKit.Api.Controllers;

[Route("fhir/Observation")]
public sealed class ObservationController(
    ISender sender,
    ObservationFhirMapper mapper,
    FhirSearchParser searchParser) : FhirControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? patient,
        [FromQuery] string? code,
        [FromQuery] string? category,
        [FromQuery(Name = "date")] string[]? date,
        [FromQuery(Name = "_count")] int? count,
        [FromQuery(Name = "_offset")] int? offset,
        [FromQuery(Name = "_sort")] string? sort,
        CancellationToken cancellationToken)
    {
        var patientId = ObservationFhirMapper.ReferenceId(patient, "Patient");
        if (string.IsNullOrWhiteSpace(patientId))
        {
            return FhirBadRequest("The 'patient' search parameter is required, e.g. patient=Patient/123.", "search.patient_required");
        }

        if (!searchParser.TryParseCodeFilter(code, out var codeFilter))
        {
            return Fhir(SearchBundleBuilder.Build([], FhirBaseUrl));
        }

        var dateError = searchParser.TryParseDates(date ?? [], out var range);
        if (dateError is not null)
        {
            return FhirBadRequest(dateError, "search.date_invalid");
        }

        if (sort is not null and not "-date" and not "date")
        {
            return FhirBadRequest("Only _sort=-date and _sort=date are supported.", "search.sort_not_supported");
        }

        var categoryToken = FhirSearchParser.ParseToken(category);

        var result = await sender.Send(new SearchReadingsQuery
        {
            PatientId = patientId,
            Code = codeFilter,
            Category = categoryToken?.Code,
            From = range.From,
            To = range.To,
            SortDescending = sort != "date",
            Offset = offset ?? 0,
            Count = count ?? SearchReadingsQuery.DefaultCount
        }, cancellationToken);

        if (result.IsFailure)
        {
            return FhirProblem(result);
        }

        return Fhir(SearchBundleBuilder.Build(result.Value, r => mapper.ToFhir(r), FhirBaseUrl, Request));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Read(string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var readingId))
        {
            return Fhir(
                OperationOutcomes.Single(OperationOutcome.IssueType.NotFound, $"Observation '{id}' was not found.", code: "reading.not_found"),
                StatusCodes.Status404NotFound);
        }

        var result = await sender.Send(new GetReadingByIdQuery(readingId), cancellationToken);
        return result.IsFailure ? FhirProblem(result) : Fhir(mapper.ToFhir(result.Value));
    }

    [HttpPost]
    [Consumes(FhirConstants.ContentType, "application/json")]
    public async Task<IActionResult> Create(
        [FromBody] Resource resource,
        [FromHeader(Name = "If-None-Exist")] string? ifNoneExist,
        CancellationToken cancellationToken)
    {
        if (resource is not Observation observation)
        {
            return FhirBadRequest("The request body must be an Observation.", "reading.not_an_observation");
        }

        var input = mapper.ToInput(observation, BatchProcessor.ExternalIdFromIfNoneExist(ifNoneExist));
        var recorded = await sender.Send(new RecordReadingCommand(input), cancellationToken);
        if (recorded.IsFailure)
        {
            return FhirProblem(recorded);
        }

        var saved = await sender.Send(new GetReadingByIdQuery(recorded.Value.Id), cancellationToken);
        if (saved.IsFailure)
        {
            return FhirProblem(saved);
        }

        Response.Headers.Location = $"{FhirBaseUrl}/Observation/{recorded.Value.Id}";
        var status = recorded.Value.Outcome == RecordReadingOutcome.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK;
        return Fhir(mapper.ToFhir(saved.Value), status);
    }
}
