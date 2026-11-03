using Fakeeh.MedicalKit.Api.Security;
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
                Title = "Fakeeh Medical Kit API",
                Version = "v1",
                Description = "Integration API for the Medical KIT. Every endpoint is a POST with a JSON body and requires the X-Api-Key header."
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
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Fakeeh Medical Kit API v1");
            options.DocumentTitle = "Fakeeh Medical Kit API";
        });

        return app;
    }
}
