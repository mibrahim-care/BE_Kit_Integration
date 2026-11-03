using Fakeeh.MedicalKit.Application.Common.Behaviors;
using Fakeeh.MedicalKit.Application.Readings;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Fakeeh.MedicalKit.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);
            config.AddOpenBehavior(typeof(LoggingBehavior<,>));
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddScoped<VitalTypeResolver>();

        return services;
    }
}
