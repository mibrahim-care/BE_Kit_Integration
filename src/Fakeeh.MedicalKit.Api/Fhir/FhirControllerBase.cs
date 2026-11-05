using Fakeeh.MedicalKit.Api.Fhir.Search;
using Fakeeh.MedicalKit.Domain.Common;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Mvc;

namespace Fakeeh.MedicalKit.Api.Fhir;

/// <summary>Base for the FHIR R4 endpoints under /fhir. Bodies are read and written as application/fhir+json.</summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public abstract class FhirControllerBase : ControllerBase
{
    protected string FhirBaseUrl => $"{Request.Scheme}://{Request.Host}{Request.PathBase}/{FhirConstants.RoutePrefix}";

    protected static FhirResult Fhir(Resource resource, int statusCode = StatusCodes.Status200OK) => new(resource, statusCode);

    protected static FhirResult FhirProblem(Result result) =>
        Fhir(OperationOutcomes.FromErrors(result.Errors), (int)OperationOutcomes.StatusFor(result.Error));

    protected static FhirResult FhirBadRequest(string message, string? code = null) =>
        Fhir(OperationOutcomes.Single(OperationOutcome.IssueType.Invalid, message, code: code), StatusCodes.Status400BadRequest);

    /// <summary>A search that cannot match anything: 200 with an empty searchset Bundle (total 0), never 404.</summary>
    protected FhirResult EmptySearch() => Fhir(SearchBundleBuilder.Build([], FhirBaseUrl));

    protected Task<(Resource? Resource, string? Error)> ReadResourceAsync(CancellationToken cancellationToken) =>
        FhirJson.ReadAsync(Request, cancellationToken);

    /// <summary>
    /// The search parameters of a FHIR <c>POST [type]/_search</c>: the application/x-www-form-urlencoded body,
    /// plus any query-string parameters (FHIR allows both, and treats them as one list).
    /// </summary>
    protected async Task<SearchParameters> ReadSearchParametersAsync(CancellationToken cancellationToken)
    {
        var pairs = Request.Query
            .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string>(q.Key, v ?? string.Empty)))
            .ToList();

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            pairs.AddRange(form.SelectMany(f => f.Value.Select(v => new KeyValuePair<string, string>(f.Key, v ?? string.Empty))));
        }

        return new SearchParameters(pairs);
    }
}
