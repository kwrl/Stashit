using Stashit.Configuration;
using Stashit.Services;
using StackExchange.Redis;
using Microsoft.Extensions.Options;

namespace Stashit.Infrastructure;

/// <summary>
/// Convenience extension to register the Redis-backed stashing service.
/// </summary>
public static class StashServiceCollectionExtensions
{
    /// <summary>
    /// Registers a shared <see cref="IConnectionMultiplexer"/> (built from
    /// <see cref="StorageOptions.BaseUrl"/>), a scoped <see cref="IDatabase"/>,
    /// and <see cref="IStashService"/> as a transient.
    /// </summary>
    public static IServiceCollection AddStashing(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var baseUrl = sp.GetRequiredService<IOptions<StorageOptions>>().Value.BaseUrl;
            return ConnectionMultiplexer.Connect(baseUrl);
        });

        services.AddSingleton<IDatabase>(sp => sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());
        services.AddSingleton<IStashService, StashService>();

        return services;
    }
}
