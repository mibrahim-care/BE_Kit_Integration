namespace Fakeeh.MedicalKit.Infrastructure.His;

public sealed class HisOptions
{
    public const string SectionName = "His";

    public string? BaseUrl { get; set; }

    public string PatientLookupPath { get; set; } = "api/eAppointments/PatientAuthenticationWithNationalId";

    public string? ApiToken { get; set; }

    public string Language { get; set; } = "en";

    public bool IgnoreSslErrors { get; set; }

    public int TimeoutSeconds { get; set; } = 15;

    public int ConnectTimeoutSeconds { get; set; } = 5;

    public int CacheSeconds { get; set; } = 60;
}
