using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Fakeeh.MedicalKit.Api.Fhir.Search;
using Microsoft.Extensions.Options;

namespace Fakeeh.MedicalKit.Api.Fhir;

public static class FhirServiceCollectionExtensions
{
    /// <summary>Services for the FHIR R4 endpoints under /fhir.</summary>
    public static IServiceCollection AddFhirApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FhirOptions>(configuration.GetSection(FhirOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<FhirOptions>>().Value);

        // Protects the opaque _page tokens of search paging links.
        services.AddDataProtection();

        services.AddSingleton<PatientFhirMapper>();
        services.AddSingleton<ObservationFhirMapper>();
        services.AddSingleton<ConformanceMapper>();
        services.AddSingleton<FhirSearchParser>();
        services.AddSingleton<PageLinkProtector>();
        services.AddScoped<BatchProcessor>();

        return services;
    }
}
