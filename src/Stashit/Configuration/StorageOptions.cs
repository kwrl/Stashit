namespace Stashit.Configuration;

/// <summary>
/// Storage-related settings, bound from the "Storage" configuration section.
/// </summary>
public sealed class StorageOptions
{
    /// <summary>Section name in appsettings.json.</summary>
    public const string SectionName = "Storage";

    /// <summary>Connection string for the Redis server (StackExchange.Redis format: host:port[,host2:port2]).</summary>
    public string BaseUrl { get; set; } = "localhost:6379";
}
