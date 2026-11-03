using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Fakeeh.MedicalKit.Api.Fhir.Search;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Options;

namespace Fakeeh.MedicalKit.Api.Fhir;

public static class FhirServiceCollectionExtensions
{
    public static IServiceCollection AddFhirApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FhirOptions>(configuration.GetSection(FhirOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<FhirOptions>>().Value);

        services
            .AddControllers(options =>
            {
                var output = options.OutputFormatters.OfType<SystemTextJsonOutputFormatter>().First();
                output.SupportedMediaTypes.Insert(0, FhirConstants.ContentType);

                var input = options.InputFormatters.OfType<SystemTextJsonInputFormatter>().First();
                input.SupportedMediaTypes.Insert(0, FhirConstants.ContentType);
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions
                    .ForFhir(typeof(Patient).Assembly)
                    .UsingMode(DeserializerModes.Ostrich)
                    .Pretty();
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var messages = context.ModelState
                        .SelectMany(e => e.Value!.Errors.Select(err =>
                            string.IsNullOrWhiteSpace(err.ErrorMessage) ? err.Exception?.Message : err.ErrorMessage))
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .DefaultIfEmpty("The request body is not a valid FHIR resource.");

                    var outcome = new OperationOutcome
                    {
                        Issue = messages.Select(m => new OperationOutcome.IssueComponent
                        {
                            Severity = OperationOutcome.IssueSeverity.Error,
                            Code = OperationOutcome.IssueType.Structure,
                            Diagnostics = m
                        }).ToList()
                    };

                    return new BadRequestObjectResult(outcome)
                    {
                        ContentTypes = { FhirConstants.ContentType },
                        DeclaredType = typeof(Resource)
                    };
                };
            });

        services.AddSingleton<FhirResponseWriter>();
        services.AddSingleton<PatientFhirMapper>();
        services.AddSingleton<ObservationFhirMapper>();
        services.AddSingleton<ConformanceMapper>();
        services.AddSingleton<FhirSearchParser>();
        services.AddScoped<BatchProcessor>();

        return services;
    }
}
