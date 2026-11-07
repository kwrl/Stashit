using Stashit.Services;

namespace Stashit.Services;

/// <summary>
/// Result of a successful stash operation.
/// </summary>
/// <param name="Id">Unguessable identifier for the stashed file.</param>
/// <param name="Ttl">Applied time-to-live before the file expires.</param>
public sealed record StashResult(string Id, TimeSpan Ttl);

/// <summary>
/// A stashed file and its metadata, ready for streaming back to the caller.
/// </summary>
/// <param name="Content">Bytes of the stored file.</param>
/// <param name="Length">Byte length of the stored file.</param>
/// <param name="Meta">Metadata such as original filename and content type.</param>
public sealed record StashData(Stream Content, long Length, StashMeta Meta);

/// <summary>
/// Core stashing operations: store a file and retrieve it before it expires.
/// </summary>
public interface IStashService
{
    /// <summary>
    /// Stores the given content under a fresh random id, applying the
    /// configured TTL to both the data and metadata keys.
    /// </summary>
    /// <param name="content">Bytes of the file to store.</param>
    /// <param name="originalFilename">Filename to remember for download, or null.</param>
    /// <param name="contentType">MIME type to remember for download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new id and the TTL that was applied.</returns>
    Task<StashResult> StoreAsync(Stream content, string? originalFilename, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a stashed file and its metadata.
    /// </summary>
    /// <param name="id">Identifier previously returned by <see cref="StoreAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored data, or null if the file does not exist or has expired.</returns>
    Task<StashData?> RetrieveAsync(string id, CancellationToken cancellationToken);
}
