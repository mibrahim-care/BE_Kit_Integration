using Fakeeh.MedicalKit.Application.Common;
using Hl7.Fhir.Model;

namespace Fakeeh.MedicalKit.Api.Fhir.Search;

public static class SearchBundleBuilder
{
    /// <summary>An unpaged searchset Bundle (e.g. a Patient search, which matches at most one patient).</summary>
    public static Bundle Build(IReadOnlyList<Resource> resources, string fhirBaseUrl) =>
        Build(resources, fhirBaseUrl, resources.Count);

    /// <summary>
    /// A paged searchset Bundle. <paramref name="pageUrl"/> builds the self / previous / next link for a page offset;
    /// it must not put search values such as the patient id in the URL.
    /// </summary>
    public static Bundle Build<T>(PagedList<T> page, Func<T, Resource> map, string fhirBaseUrl, Func<int, string> pageUrl)
    {
        var bundle = Build(page.Items.Select(map).ToList(), fhirBaseUrl, page.Total);

        bundle.Link.Add(new Bundle.LinkComponent { Relation = "self", Url = pageUrl(page.Offset) });

        if (page.HasPrevious)
        {
            bundle.Link.Add(new Bundle.LinkComponent { Relation = "previous", Url = pageUrl(Math.Max(0, page.Offset - page.Count)) });
        }

        if (page.HasNext)
        {
            bundle.Link.Add(new Bundle.LinkComponent { Relation = "next", Url = pageUrl(page.Offset + page.Count) });
        }

        return bundle;
    }

    private static Bundle Build(IReadOnlyList<Resource> resources, string fhirBaseUrl, int total)
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

        return bundle;
    }
}
