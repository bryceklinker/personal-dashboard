using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Personal.Dashboard.Test.Support.Common.Http;

namespace Personal.Dashboard.Test.Support;

public static class PersonalDashboardTestingServicesCollectionExtensions
{
    public static IServiceCollection AddPersonalDashboardTestingServices(this IServiceCollection services)
    {
        var handler = new FakeHttpMessageHandler();
        services.AddSingleton(handler);
        services.ReplaceService<IHttpClientFactory, FakeHttpClientFactory>(new FakeHttpClientFactory(handler));
        return services;
    }

    public static IServiceCollection ReplaceService<TService, TImplementation>(
        this IServiceCollection services,
        TImplementation instance)
        where TService : class
        where TImplementation : class, TService
    {
        return services.ReplaceService<TService, TImplementation>(_ => instance);
    }

    public static IServiceCollection ReplaceService<TService, TImplementation>(this IServiceCollection services,
        Func<IServiceProvider, TImplementation> resolver)
        where TService : class
        where TImplementation : class, TService
    {
        services.RemoveAll(typeof(TService));
        services.AddTransient<TService>(resolver);
        services.AddTransient(resolver);
        return services;
    }
}