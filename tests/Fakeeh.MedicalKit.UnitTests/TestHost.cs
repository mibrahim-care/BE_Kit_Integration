using Fakeeh.MedicalKit.Api.Fhir;
using Fakeeh.MedicalKit.Api.Fhir.Mappers;
using Fakeeh.MedicalKit.Application;
using Fakeeh.MedicalKit.Application.Abstractions;
using Fakeeh.MedicalKit.Application.Patients;
using Fakeeh.MedicalKit.Infrastructure.Persistence;
using Fakeeh.MedicalKit.Infrastructure.Persistence.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Fakeeh.MedicalKit.UnitTests;

internal sealed class TestHost : IAsyncDisposable
{
    public static readonly PatientDto Ahmed = new(
        "1001", PatientIdentifierType.NationalId, "1123456781", "MRN-1", "Ahmed", null, "Ali", null,
        "male", new DateOnly(1990, 4, 12), null, null, "Saudi", "Jeddah");

    private readonly ServiceProvider _provider;

    public TestHost()
    {
        His = Substitute.For<IHisPatientClient>();
        His.GetByIdAsync(Ahmed.Id, Arg.Any<CancellationToken>()).Returns(Ahmed);
        His.FindByIdentifierAsync(PatientIdentifierType.NationalId, Ahmed.IdentifierValue, Arg.Any<CancellationToken>()).Returns(Ahmed);

        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddApplication();
        services.AddDbContext<IntegrationDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<IReadingRepository, ReadingRepository>();
        services.AddScoped<IVitalTypeRepository, VitalTypeRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IClock>(new FixedClock());
        services.AddSingleton(His);

        services.AddSingleton(Options);
        services.AddSingleton<ObservationFhirMapper>();
        services.AddScoped<BatchProcessor>();

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IntegrationDbContext>().Database.EnsureCreated();
    }

    public FhirOptions Options { get; } = new();

    public IHisPatientClient His { get; }

    public ObservationFhirMapper Mapper => new(Options);

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 10, 18, 5, 0, 0, TimeSpan.Zero);
    }
}
