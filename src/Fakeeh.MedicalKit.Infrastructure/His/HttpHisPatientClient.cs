using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Application.Patients;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fakeeh.MedicalKit.Infrastructure.His;

internal sealed class HttpHisPatientClient(
    HttpClient httpClient,
    IMemoryCache cache,
    IOptions<HisOptions> options,
    ILogger<HttpHisPatientClient> logger) : IHisPatientClient
{
    private const int StatusFound = 1;
    private const int StatusNotFound = 2;

    public Task<PatientDto?> FindByNationalIdAsync(string nationalId, CancellationToken cancellationToken) =>
        LookupAsync(nationalId, cancellationToken);

    private async Task<PatientDto?> LookupAsync(string identifier, CancellationToken cancellationToken)
    {
        var cacheSeconds = options.Value.CacheSeconds;
        if (cacheSeconds <= 0)
        {
            return await FetchAsync(identifier, cancellationToken);
        }

        return await cache.GetOrCreateAsync($"his:patient:{identifier}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheSeconds);
            return await FetchAsync(identifier, cancellationToken);
        });
    }

    private async Task<PatientDto?> FetchAsync(string identifier, CancellationToken cancellationToken)
    {
        try
        {
            return await SendLookupAsync(identifier, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "HIS patient lookup timed out");
            throw new HisUnavailableException("Fakeeh HIS did not respond in time.");
        }
    }

    private async Task<PatientDto?> SendLookupAsync(string identifier, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            options.Value.PatientLookupPath,
            new HisLookupRequest(identifier),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("HIS patient lookup returned HTTP {StatusCode}", (int)response.StatusCode);
            throw new HisUnavailableException($"Fakeeh HIS patient lookup failed with HTTP {(int)response.StatusCode}.");
        }

        var body = await response.Content.ReadFromJsonAsync<HisLookupResponse>(cancellationToken);

        if (body?.Status == StatusFound && body.Patients is { Count: > 0 })
        {
            return ToDto(body.Patients[0], identifier);
        }

        if (body?.Status == StatusNotFound || body?.Status == StatusFound)
        {
            return null;
        }

        logger.LogError("HIS patient lookup returned status {Status}: {Message}", body?.Status, body?.Message);
        throw new HisUnavailableException($"Fakeeh HIS patient lookup failed: {body?.Message ?? "empty response"}.");
    }

    private static PatientDto ToDto(HisPatient patient, string requestedIdentifier)
    {
        var identifier = string.IsNullOrWhiteSpace(patient.NationalId) ? requestedIdentifier : patient.NationalId.Trim();

        return new PatientDto(
            identifier,
            identifier.StartsWith('2') ? PatientIdentifierType.Iqama : PatientIdentifierType.NationalId,
            NullIfEmpty(patient.MrnNumber) ?? NullIfEmpty(patient.RegistrationNo),
            patient.FirstNameEnglish?.Trim() ?? string.Empty,
            null,
            null,
            NullIfEmpty(patient.FirstNameArabic),
            "unknown",
            null,
            FormatPhone(patient.Phone),
            null,
            null,
            null);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FormatPhone(string? phone)
    {
        var value = NullIfEmpty(phone);
        return value is not null && value.All(char.IsAsciiDigit) && value.StartsWith("966") ? $"+{value}" : value;
    }

    private sealed record HisLookupRequest([property: JsonPropertyName("patient_national_id")] string PatientNationalId);

    private sealed class HisLookupResponse
    {
        [JsonPropertyName("staus")]
        public int? Status { get; init; }

        [JsonPropertyName("msg")]
        public string? Message { get; init; }

        [JsonPropertyName("patientAuthenticationResponseList")]
        public List<HisPatient>? Patients { get; init; }
    }

    private sealed class HisPatient
    {
        [JsonPropertyName("registration_no")]
        public string? RegistrationNo { get; init; }

        [JsonPropertyName("firstname_enlgish")]
        public string? FirstNameEnglish { get; init; }

        [JsonPropertyName("firstname_arabic")]
        public string? FirstNameArabic { get; init; }

        [JsonPropertyName("patient_phone")]
        public string? Phone { get; init; }

        [JsonPropertyName("national_Id")]
        public string? NationalId { get; init; }

        [JsonPropertyName("mrn_number")]
        public string? MrnNumber { get; init; }
    }
}

