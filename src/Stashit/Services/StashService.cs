using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Stashit.Configuration;
using Stashit.Services;
using StackExchange.Redis;

namespace Stashit.Services;

/// <summary>
/// Redis-backed implementation of <see cref="IStashService"/>.
/// </summary>
public sealed class StashService(IDatabase db, IOptions<LimitsOptions> limits) : IStashService
{
    private static readonly JsonSerializerOptions MetaJson = new(JsonSerializerDefaults.Web);

    public async Task<StashResult> StoreAsync(Stream content, string? originalFilename, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var ttl = limits.Value.DefaultTtl;
        if (ttl <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Limits__DefaultTtl must be a positive TimeSpan.");
        }

        var id = NewId();

        // "Small to medium" by definition of this service; one buffer keeps the code
        // simple and the Redis write a single round trip.
        var buffer = new MemoryStream(capacity: MaxBufferHint);
        await content.CopyToAsync(buffer, cancellationToken);
        var data = buffer.ToArray();

        var meta = new StashMeta
        {
            OriginalFilename = originalFilename ?? "download",
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            UploadedAt = DateTimeOffset.UtcNow,
        };
        var metaBytes = JsonSerializer.SerializeToUtf8Bytes(meta, MetaJson);

        // Both keys carry the same TTL so data and metadata always expire together.
        await db.StringSetAsync($"stash:data:{id}", data, ttl);
        await db.StringSetAsync($"stash:meta:{id}", metaBytes, ttl);

        return new StashResult(id, ttl);
    }

    public async Task<StashData?> RetrieveAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var metaBytes = (byte[]?)await db.StringGetAsync($"stash:meta:{id}");
        if (metaBytes is null)
        {
            return null;
        }

        var meta = JsonSerializer.Deserialize<StashMeta>(metaBytes, MetaJson);
        if (meta is null)
        {
            return null;
        }

        var dataBytes = (byte[]?)await db.StringGetAsync($"stash:data:{id}");
        if (dataBytes is null)
        {
            return null;
        }

        return new StashData(new MemoryStream(dataBytes, writable: false), dataBytes.Length, meta);
    }

    private const int MaxBufferHint = 16 * 1024;

    private static string NewId()
    {
        Span<byte> buf = stackalloc byte[16];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToHexString(buf);
    }
}
