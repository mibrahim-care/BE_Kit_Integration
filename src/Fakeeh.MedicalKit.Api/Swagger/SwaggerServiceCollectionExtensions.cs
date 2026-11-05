using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Security;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Fakeeh.MedicalKit.Api.Swagger;

public static class SwaggerServiceCollectionExtensions
{
    private const string ApiKeySchemeId = "ApiKey";

    /// <summary>The FHIR R4 API the Medical KIT integrates with (/fhir). Matches the KIT Postman collection.</summary>
    public const string KitDocument = "kit";

    /// <summary>The JSON endpoints used by the Medical KIT portal (/api/MedicalKit).</summary>
    public const string PortalDocument = "portal";

    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            var kit = KitSwaggerContent.Instance;

            options.SwaggerDoc(KitDocument, new OpenApiInfo
            {
                Title = "Fakeeh Medical KIT Integration (FHIR R4)",
                Version = "v1"
            });

            options.SwaggerDoc(PortalDocument, new OpenApiInfo
            {
                Title = "Fakeeh Medical Kit Portal API",
                Version = "v1",
                Description = "JSON endpoints used by the Medical KIT portal. Every endpoint is a POST with a JSON body " +
                              "and requires the X-Api-Key header. The Medical KIT itself uses the FHIR API."
            });

            options.DocInclusionPredicate((document, api) =>
            {
                var isFhir = (api.RelativePath ?? string.Empty).StartsWith(FhirConstants.RoutePrefix + "/", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(api.RelativePath, FhirConstants.RoutePrefix, StringComparison.OrdinalIgnoreCase);
                return document == KitDocument ? isFhir : !isFhir;
            });

            // Group and order the FHIR operations like the folders and requests of the KIT collection.
            options.TagActionsBy(api =>
                [kit.Find(api)?.Tag ?? api.ActionDescriptor.RouteValues["controller"] ?? "Other"]);
            options.OrderActionsBy(api => kit.Find(api) is { } operation
                ? $"{operation.Order:D3}"
                : $"999 {api.RelativePath}");

            options.OperationFilter<KitOperationFilter>();
            options.DocumentFilter<KitTagsDocumentFilter>();

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
            // The first entry is the one Swagger UI opens with.
            options.SwaggerEndpoint($"/swagger/{KitDocument}/swagger.json", "Medical KIT integration (FHIR R4)");
            options.SwaggerEndpoint($"/swagger/{PortalDocument}/swagger.json", "Portal API");
            options.DocumentTitle = "Fakeeh Medical Kit API";
            options.EnableTryItOutByDefault();
            options.DisplayRequestDuration();
        });

        return app;
    }
}

/// <summary>Lists the KIT collection folders as tags, in collection order and with their descriptions.</summary>
public sealed class KitTagsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        if (context.DocumentName != SwaggerServiceCollectionExtensions.KitDocument)
        {
            return;
        }

        document.Tags = new HashSet<OpenApiTag>(
            KitSwaggerContent.Instance.Tags.Select(t => new OpenApiTag { Name = t.Name }));
    }
}
