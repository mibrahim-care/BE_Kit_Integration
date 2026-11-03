using Fakeeh.MedicalKit.Application.Common;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Fakeeh.MedicalKit.Api.Fhir.Search;

public static class SearchBundleBuilder
{
    public static Bundle Build(IReadOnlyList<Resource> resources, string fhirBaseUrl) =>
        Build<object>(resources, fhirBaseUrl, resources.Count, null, null);

    public static Bundle Build<T>(
        PagedList<T> page,
        Func<T, Resource> map,
        string fhirBaseUrl,
        HttpRequest request) =>
        Build(page.Items.Select(map).ToList(), fhirBaseUrl, page.Total, page, request);

    private static Bundle Build<T>(
        IReadOnlyList<Resource> resources,
        string fhirBaseUrl,
        int total,
        PagedList<T>? page,
        HttpRequest? request)
    {
        var bundle = new Bundle
        {
            Id = Guid.NewGuid().ToString(),
            Type = Bundle.BundleType.Searchset,
            Total = total,
            Meta = new Meta { LastUpdated = DateTimeOffset.UtcNow }
        };

        foreach (var resource in resources)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                FullUrl = $"{fhirBaseUrl}/{resource.TypeName}/{resource.Id}",
                Resource = resource,
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match }
            });
        }

        if (page is not null && request is not null)
        {
            bundle.Link.Add(new Bundle.LinkComponent { Relation = "self", Url = PageUrl(request, page.Offset, page.Count) });

            if (page.HasPrevious)
            {
                bundle.Link.Add(new Bundle.LinkComponent
                {
                    Relation = "previous",
                    Url = PageUrl(request, Math.Max(0, page.Offset - page.Count), page.Count)
                });
            }

            if (page.HasNext)
            {
                bundle.Link.Add(new Bundle.LinkComponent
                {
                    Relation = "next",
                    Url = PageUrl(request, page.Offset + page.Count, page.Count)
                });
            }
        }

        return bundle;
    }

    private static string PageUrl(HttpRequest request, int offset, int count)
    {
        var query = request.Query
            .Where(q => q.Key is not "_offset" and not "_count")
            .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, StringValues>(q.Key, v)))
            .ToList();

        query.Add(new("_count", count.ToString()));
        query.Add(new("_offset", offset.ToString()));

        var path = $"{request.Scheme}://{request.Host}{request.PathBase}{request.Path}";
        return QueryHelpers.AddQueryString(path, query);
    }
}
