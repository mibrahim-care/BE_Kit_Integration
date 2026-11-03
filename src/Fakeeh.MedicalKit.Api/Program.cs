using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Security;
using Fakeeh.MedicalKit.Api.Swagger;
using Fakeeh.MedicalKit.Application;
using Fakeeh.MedicalKit.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

const string PortalCorsPolicy = "Portal";

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFhirApi(builder.Configuration);
builder.Services.AddSwagger();

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

app.UseMiddleware<FhirExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseCors(PortalCorsPolicy);
app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.Run();

public partial class Program;
