namespace Stashit.Configuration;

/// <summary>
/// Tunable limits and defaults for the stashing service, bound from the
/// "Limits" configuration section.
/// </summary>
public sealed class LimitsOptions
{
    /// <summary>Section name in appsettings.json.</summary>
    public const string SectionName = "Limits";

    /// <summary>Maximum upload size in bytes. Default 100 MB.</summary>
    public long MaxUploadBytes { get; set; } = 104_857_600;

    /// <summary>Default server-side TTL applied to all stashed files. Default 24h.</summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromHours(24);
}
