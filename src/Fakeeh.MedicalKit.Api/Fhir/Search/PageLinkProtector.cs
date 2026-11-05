using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Fakeeh.MedicalKit.Api.Fhir.Search;

/// <summary>
/// Turns the parameters of a search page into an opaque, signed and encrypted <c>_page</c> token, so the
/// paging links of a search Bundle never carry a patient id (National ID / Iqama) in the URL.
/// </summary>
public sealed class PageLinkProtector(IDataProtectionProvider provider, FhirOptions options)
{
    public const string ParameterName = "_page";

    private readonly ITimeLimitedDataProtector _protector =
        provider.CreateProtector("Fakeeh.MedicalKit.Fhir.SearchPage.v1").ToTimeLimitedDataProtector();

    public string Protect(string resourceType, SearchParameters parameters)
    {
        var payload = new PagePayload(resourceType, parameters.Pairs.Select(p => new[] { p.Key, p.Value }).ToList());
        return _protector.Protect(JsonSerializer.Serialize(payload), options.PageLinkLifetime);
    }

    public bool TryUnprotect(string token, string resourceType, out SearchParameters parameters)
    {
        parameters = new SearchParameters([]);
        try
        {
            var payload = JsonSerializer.Deserialize<PagePayload>(_protector.Unprotect(token));
            if (payload is null || payload.Type != resourceType)
            {
                return false;
            }

            parameters = new SearchParameters(payload.Params
                .Where(p => p.Length == 2)
                .Select(p => new KeyValuePair<string, string>(p[0], p[1]))
                .ToList());
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return false;
        }
    }

    private sealed record PagePayload(string Type, List<string[]> Params);
}
