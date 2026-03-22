using System.Reflection;
using AirQualityMonitoring.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AirQualityMonitoring.Core.Extensions;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var serviceDescriptors = assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type));
        
        services.TryAddEnumerable(serviceDescriptors);
        
        var handlerTypes = assembly.DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.Name.EndsWith("Handler"));

        foreach (var type in handlerTypes)
            services.AddTransient(type);
        
        return services;
    }

    public static IApplicationBuilder UseEndpoints(
        this WebApplication app,
        IEndpointRouteBuilder? routeBuilder = null)
    {
        var endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();

        var builder = routeBuilder ?? app;

        foreach (var endpoint in endpoints)
        {
            endpoint.MapEndpoint(builder);
        }

        return app;
    }
}