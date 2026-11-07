using Microsoft.Extensions.Options;
using NSubstitute;
using Stashit.Configuration;
using Stashit.Services;
using StackExchange.Redis;
using System.Text.Json;

namespace Stashit.Tests;

public class StashServiceTests
{
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly LimitsOptions _limits = new() { DefaultTtl = TimeSpan.FromHours(1) };
    private readonly StashService _sut;

    public StashServiceTests()
    {
        _sut = new StashService(_db, Options.Create(_limits));
    }

    [Fact]
    public async Task StoreAsync_ValidContent_WritesDataAndMetaKeysWithConfiguredTtl()
    {
        var payload = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await _sut.StoreAsync(payload, "f.txt", "text/plain", CancellationToken.None);


        var setCalls = _db.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "StringSetAsync")
            .ToList();
        Assert.Equal(2, setCalls.Count);
        Assert.Contains(setCalls, c => c.GetArguments()[0] is RedisKey dataKey && dataKey.ToString().StartsWith("stash:data:"));
        Assert.Contains(setCalls, c => c.GetArguments()[0] is RedisKey metaKey && metaKey.ToString().StartsWith("stash:meta:"));

        Assert.NotEmpty(result.Id);
        Assert.Equal(_limits.DefaultTtl, result.Ttl);
    }

    [Fact]
    public async Task StoreAsync_ResultIdIs32CharHex()
    {
        var payload = new MemoryStream(new byte[] { 1 });

        var result = await _sut.StoreAsync(payload, "f.txt", "text/plain", CancellationToken.None);

        Assert.Matches("^[0-9A-Fa-f]{32}$", result.Id);
    }

    [Fact]
    public async Task StoreAsync_NonPositiveTtl_ThrowsInvalidOperation()
    {
        var svc = new StashService(_db, Options.Create(new LimitsOptions { DefaultTtl = TimeSpan.Zero }));
        var payload = new MemoryStream(new byte[] { 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.StoreAsync(payload, "f", "text/plain", CancellationToken.None));
    }

    [Fact]
    public async Task RetrieveAsync_AllKeysPresent_ReturnsStashData()
    {
        const string id = "ABCDEF";
        var meta = new StashMeta { OriginalFilename = "hello.txt", ContentType = "text/plain", UploadedAt = DateTimeOffset.UtcNow };
        var metaJson = JsonSerializer.SerializeToUtf8Bytes(meta, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var data = new byte[] { 1, 2, 3, 4 };

        _db.StringGetAsync($"stash:meta:{id}").Returns(Task.FromResult<RedisValue>(metaJson));
        _db.StringGetAsync($"stash:data:{id}").Returns(Task.FromResult<RedisValue>(data));

        var result = await _sut.RetrieveAsync(id, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("hello.txt", result!.Meta.OriginalFilename);
        Assert.Equal("text/plain", result.Meta.ContentType);
        Assert.Equal(4, result.Length);
    }

    [Fact]
    public async Task RetrieveAsync_MissingMeta_ReturnsNull()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>()).Returns(Task.FromResult<RedisValue>(RedisValue.Null));

        var result = await _sut.RetrieveAsync("ABC", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RetrieveAsync_EmptyId_ReturnsNullWithoutRedisCalls()
    {
        Assert.Null(await _sut.RetrieveAsync(string.Empty, CancellationToken.None));
        Assert.Null(await _sut.RetrieveAsync("   ", CancellationToken.None));

        await _db.DidNotReceive().StringGetAsync(Arg.Any<RedisKey>());
    }
}
