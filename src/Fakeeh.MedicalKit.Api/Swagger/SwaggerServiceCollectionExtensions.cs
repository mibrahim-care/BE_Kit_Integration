using Fakeeh.MedicalKit.Api.Security;
using Hl7.Fhir.Model;
using Microsoft.OpenApi;

namespace Fakeeh.MedicalKit.Api.Swagger;

public static class SwaggerServiceCollectionExtensions
{
    private const string ApiKeySchemeId = "ApiKey";

    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Fakeeh Medical Kit FHIR API",
                Version = "v1",
                Description = "FHIR R4 integration API. Endpoints under /fhir (except /fhir/metadata) require the X-Api-Key header."
            });

            // The HL7 FHIR model is a large, cyclic, polymorphic type graph; describing it as a
            // plain JSON object keeps generation fast and lets clients send any FHIR resource.
            options.MapType<Resource>(() => new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "A FHIR R4 resource (JSON). Must include 'resourceType'.",
                AdditionalPropertiesAllowed = true
            });

            options.AddSecurityDefinition(ApiKeySchemeId, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = ApiKeyOptions.HeaderName,
                Description = "API key issued to the calling client."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(ApiKeySchemeId, document)] = []
            });
        });

        return services;
    }

    public static WebApplication UseSwaggerDocs(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fakeeh Medical Kit FHIR API v1");
            options.DocumentTitle = "Fakeeh Medical Kit API";
        });

        return app;
    }
}
