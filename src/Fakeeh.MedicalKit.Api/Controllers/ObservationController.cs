using System.Globalization;
using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Fakeeh.MedicalKit.Api.Fhir.Search;
using Fakeeh.MedicalKit.Application.Observations;
using Fakeeh.MedicalKit.Application.Readings;
using Hl7.Fhir.Model;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Fakeeh.MedicalKit.Api.Controllers;

[Route(FhirConstants.RoutePrefix + "/Observation")]
public sealed class ObservationController(
    ISender sender,
    ObservationFhirMapper mapper,
    FhirSearchParser searchParser,
    PageLinkProtector pageLinks) : FhirControllerBase
{
    private const string ResourceType = "Observation";
    private const string RecordsPerTypeParameter = "recordsPerType";

    /// <summary>
    /// Records one reading. 201 with the stored Observation and a Location header; re-sending the same reading
    /// identifier (or If-None-Exist: identifier=…) returns 200 with the stored Observation instead of a duplicate.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromHeader(Name = "If-None-Exist")] string? ifNoneExist,
        CancellationToken cancellationToken)
    {
        var (resource, error) = await ReadResourceAsync(cancellationToken);
        if (resource is null)
        {
            return FhirBadRequest(error!, "request.invalid");
        }

        if (resource is not Observation observation)
        {
            return FhirBadRequest("The request body must be an Observation.", "reading.not_an_observation");
        }

        var input = mapper.ToInput(observation, BatchProcessor.ExternalIdFromIfNoneExist(ifNoneExist));
        var recorded = await sender.Send(new RecordObservationCommand(input), cancellationToken);
        if (recorded.IsFailure)
        {
            return FhirProblem(recorded);
        }

        var saved = await sender.Send(new GetObservationByIdQuery(recorded.Value.Id), cancellationToken);
        if (saved.IsFailure)
        {
            return FhirProblem(saved);
        }

        Response.Headers.Location = $"{FhirBaseUrl}/Observation/{recorded.Value.Id}";
        var status = recorded.Value.Outcome == RecordReadingOutcome.Created ? StatusCodes.Status201Created : StatusCodes.Status200OK;
        return Fhir(mapper.ToFhir(saved.Value), status);
    }

    /// <summary>
    /// POST Observation/_search with the parameters in a form body: patient, _id, code, category, date (repeatable),
    /// _sort, _count, _offset, recordsPerType; or _page from a paging link.
    /// </summary>
    [HttpPost("_search")]
    public async Task<IActionResult> Search(CancellationToken cancellationToken) =>
        await SearchAsync(await ReadSearchParametersAsync(cancellationToken), cancellationToken);

    /// <summary>
    /// Follows a self / previous / next link with GET. Only the opaque _page token is accepted in the URL.
    /// Not in Swagger: the KIT collection searches with POST only, and POST accepts the same _page links.
    /// </summary>
    [HttpGet("_search")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> SearchPage(CancellationToken cancellationToken)
    {
        var parameters = await ReadSearchParametersAsync(cancellationToken);
        if (!parameters.Has(PageLinkProtector.ParameterName)
            || parameters.Names.Any(n => n is not PageLinkProtector.ParameterName and not "_format"))
        {
            return FhirBadRequest(
                "Search with POST Observation/_search and the parameters in an application/x-www-form-urlencoded body, " +
                "so no patient identifier appears in the URL. GET is only accepted for the _page links of a search Bundle.",
                "search.use_post");
        }

        return await SearchAsync(parameters, cancellationToken);
    }

    private async Task<IActionResult> SearchAsync(SearchParameters parameters, CancellationToken cancellationToken)
    {
        if (parameters.First(PageLinkProtector.ParameterName) is { } token)
        {
            if (!pageLinks.TryUnprotect(token, ResourceType, out var stored))
            {
                return FhirBadRequest("The _page link is invalid or has expired. Run the search again.", "search.page_invalid");
            }

            parameters = stored;
        }

        Guid? id = null;
        if (parameters.First("_id") is { } rawId)
        {
            if (!Guid.TryParse(rawId, out var parsedId))
            {
                return EmptySearch();
            }

            id = parsedId;
        }

        var patientId = ObservationFhirMapper.ReferenceId(parameters.First("patient") ?? parameters.First("subject"), "Patient");
        if (string.IsNullOrWhiteSpace(patientId) && id is null)
        {
            return FhirBadRequest(
                "The 'patient' search parameter is required, e.g. patient=Patient/1234567890 (or search by _id).",
                "search.patient_required");
        }

        if (!searchParser.TryParseCodeFilter(parameters.First("code"), out var codeFilter))
        {
            return EmptySearch();
        }

        var dateError = searchParser.TryParseDates(parameters.All("date"), out var range);
        if (dateError is not null)
        {
            return FhirBadRequest(dateError, "search.date_invalid");
        }

        var sort = parameters.First("_sort");
        if (sort is not null and not "-date" and not "date")
        {
            return FhirBadRequest("Only _sort=-date and _sort=date are supported.", "search.sort_not_supported");
        }

        if (!TryParseInt(parameters.First("_count"), SearchObservationsQuery.DefaultCount, out var count))
        {
            return FhirBadRequest("_count must be a whole number.", "search.count_invalid");
        }

        if (!TryParseInt(parameters.First("_offset"), 0, out var offset))
        {
            return FhirBadRequest("_offset must be a whole number.", "search.offset_invalid");
        }

        if (!TryParseOptionalInt(parameters.First(RecordsPerTypeParameter), out var recordsPerType))
        {
            return FhirBadRequest("recordsPerType must be a whole number.", "search.records_per_type_invalid");
        }

        if (recordsPerType is not null && parameters.Has("_count"))
        {
            return FhirBadRequest(
                "Send either _count (one page across all vital types) or recordsPerType (a page per vital type), not both.",
                "search.count_with_records_per_type");
        }

        var result = await sender.Send(new SearchObservationsQuery
        {
            PatientId = patientId,
            Id = id,
            Code = codeFilter,
            Category = FhirSearchParser.ParseToken(parameters.First("category"))?.Code,
            From = range.From,
            To = range.To,
            SortDescending = sort != "date",
            Offset = offset,
            Count = count,
            RecordsPerType = recordsPerType
        }, cancellationToken);

        if (result.IsFailure)
        {
            return FhirProblem(result);
        }

        // Paging links carry the search as an opaque token, never the patient id itself.
        var pageBase = parameters.Without("_count", "_offset", PageLinkProtector.ParameterName);
        string PageUrl(int pageOffset)
        {
            // With recordsPerType (kept in pageBase) the offset steps per vital type and _count is not added.
            var page = (recordsPerType is null ? pageBase.With("_count", count.ToString(CultureInfo.InvariantCulture)) : pageBase)
                .With("_offset", pageOffset.ToString(CultureInfo.InvariantCulture));
            return $"{FhirBaseUrl}/Observation/_search?{PageLinkProtector.ParameterName}={Uri.EscapeDataString(pageLinks.Protect(ResourceType, page))}";
        }

        return Fhir(SearchBundleBuilder.Build(result.Value, r => mapper.ToFhir(r), FhirBaseUrl, PageUrl));
    }

    private static bool TryParseOptionalInt(string? value, out int? result)
    {
        result = null;
        if (value is null)
        {
            return true;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool TryParseInt(string? value, int fallback, out int result)
    {
        if (value is null)
        {
            result = fallback;
            return true;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }
}
