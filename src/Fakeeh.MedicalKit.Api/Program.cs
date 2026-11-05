using System.Text.Json.Serialization;
using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Http;
using Fakeeh.MedicalKit.Api.Security;
using Fakeeh.MedicalKit.Api.Swagger;
using Fakeeh.MedicalKit.Application;
using Fakeeh.MedicalKit.Application.Readings;
using Fakeeh.MedicalKit.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

const string PortalCorsPolicy = "Portal";

builder.Services.AddApplication(
    builder.Configuration.GetSection(ReadingSettings.SectionName).Get<ReadingSettings>());
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFhirApi(builder.Configuration);
builder.Services.AddSwagger();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        var json = options.JsonSerializerOptions;
        json.NumberHandling = JsonNumberHandling.AllowReadingFromString;
        json.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
        json.Converters.Add(new LenientStringConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Malformed JSON or a value of the wrong type: answer with the same error shape as validation errors.
        options.InvalidModelStateResponseFactory = context =>
        {
            var bodyParameters = context.ActionDescriptor.Parameters.Select(p => p.Name).ToHashSet();
            var errors = context.ModelState
                .Where(e => e.Value is { Errors.Count: > 0 } && !bodyParameters.Contains(e.Key))
                .Select(e => e.Key.TrimStart('$', '.'))
                .Select(field => new Fakeeh.MedicalKit.Application.Common.ErrorDto(
                    "request.invalid",
                    string.IsNullOrWhiteSpace(field)
                        ? "The request body is not valid JSON."
                        : $"'{field}' is not valid JSON or has a value of the wrong type.",
                    string.IsNullOrWhiteSpace(field) ? null : field))
                .ToList();

            return new BadRequestObjectResult(new ErrorResponse(
                errors.Count > 0 ? errors : [new("request.invalid", "The request body is not valid JSON.", null)]));
        };
    });

builder.Services.Configure<ApiKeyOptions>(builder.Configuration.GetSection(ApiKeyOptions.SectionName));

builder.Services.AddCors(options =>
{
    options.AddPolicy(PortalCorsPolicy, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .WithHeaders("Content-Type", "Accept", ApiKeyOptions.HeaderName, "If-None-Exist")
        .WithMethods("GET", "POST")
        .WithExposedHeaders("Location"));
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await app.Services.MigrateDatabaseAsync();
}

app.UseSwaggerDocs();

app.UseMiddleware<ExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseCors(PortalCorsPolicy);
app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.Run();

public partial class Program;
