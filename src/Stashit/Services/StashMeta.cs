namespace Stashit.Services;

/// <summary>
/// Metadata kept alongside a stashed file so downloads can restore the
/// original filename and Content-Type.
/// </summary>
public sealed class StashMeta
{
    public required string OriginalFilename { get; init; }
    public required string ContentType { get; init; }
    public DateTimeOffset UploadedAt { get; init; }
}
