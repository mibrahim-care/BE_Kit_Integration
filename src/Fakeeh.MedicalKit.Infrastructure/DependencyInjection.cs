using System.Net.Http.Headers;
using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Infrastructure.His;
using Fakeeh.MedicalKit.Infrastructure.Persistence;
using Fakeeh.MedicalKit.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oracle.EntityFrameworkCore.Infrastructure;

namespace Fakeeh.MedicalKit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IntegrationDbContext>(options =>
            options.UseOracle(
                configuration.GetConnectionString("OracleDb")
                ?? throw new InvalidOperationException("Connection string 'OracleDb' is not configured."),
                oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19)));

        services.AddMemoryCache();
        services.AddScoped<IReadingRepository, ReadingRepository>();
        services.AddScoped<IVitalTypeRepository, VitalTypeRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IClock, SystemClock>();

        AddHis(services, configuration);

        return services;
    }

    private static void AddHis(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(HisOptions.SectionName);
        services.Configure<HisOptions>(section);

        var options = section.Get<HisOptions>() ?? new HisOptions();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException("His:BaseUrl is not configured.");
        }

        var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";

        services.AddHttpClient<IHisPatientClient, HttpHisPatientClient>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("ceq-conv-lang", options.Language);

                if (!string.IsNullOrWhiteSpace(options.ApiToken))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", options.ApiToken);
                }
            })
            .ConfigurePrimaryHttpMessageHandler(() => HisHttpHandler.Create(options));
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DependencyInjection));

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Database is up to date");
            return;
        }

        logger.LogInformation("Applying {Count} database migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied");
    }
}
