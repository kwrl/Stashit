using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stashit.Configuration;
using Stashit.Controllers;
using Stashit.Services;

namespace Stashit.Tests;

public class FilesControllerTests
{
    private sealed class FakeStash : IStashService
    {
        public StashResult? LastStore { get; set; }
        public StashData? RetrieveResult { get; set; }

        public Task<StashResult> StoreAsync(Stream content, string? originalFilename, string contentType, CancellationToken cancellationToken)
        {
            LastStore = new StashResult("ABCDEF", TimeSpan.FromHours(24));
            return Task.FromResult(LastStore);
        }

        public Task<StashData?> RetrieveAsync(string id, CancellationToken cancellationToken)
        {
            return Task.FromResult(RetrieveResult);
        }
    }

    private static (FilesController Controller, FakeStash Stash) Build(long maxBytes = 1024 * 1024)
    {
        var stash = new FakeStash();
        var limits = Options.Create(new LimitsOptions { MaxUploadBytes = maxBytes });

        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost", 8080);
        context.Request.Scheme = "https";

        var controller = new FilesController(stash, limits)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        return (controller, stash);
    }

    [Fact]
    public async Task Create_ValidBody_Returns201WithLocation()
    {
        var (controller, stash) = Build();
        var bytes = new byte[] { 1, 2, 3 };
        var request = controller.ControllerContext!.HttpContext.Request;
        request.ContentType = "text/plain";
        request.Headers.ContentLength = bytes.Length;
        request.Body = new MemoryStream(bytes);

        var action = await controller.Create(CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(action);
        Assert.Equal("https://localhost:8080/files/ABCDEF", created.Location);
        Assert.NotNull(stash.LastStore);
    }

    [Fact]
    public async Task Create_DeclaredLengthTooLarge_Returns413()
    {
        var (controller, stash) = Build(maxBytes: 1024);
        var request = controller.ControllerContext!.HttpContext.Request;
        request.ContentType = "text/plain";
        request.Headers.ContentLength = 2048;
        request.Body = new MemoryStream(new byte[2048]);

        var action = await controller.Create(CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(413, result.StatusCode);
        Assert.Null(stash.LastStore);
    }

    [Fact]
    public async Task Create_StreamingBodyTooLarge_Returns413()
    {
        var (controller, stash) = Build(maxBytes: 1024);
        var request = controller.ControllerContext!.HttpContext.Request;
        request.ContentType = "text/plain";
        request.Headers.ContentLength = null;
        request.Body = new MemoryStream(new byte[2048]);

        var action = await controller.Create(CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action);
        Assert.Equal(413, result.StatusCode);
        Assert.Null(stash.LastStore);
    }

    [Fact]
    public async Task Create_ParsesFilenameFromContentDisposition()
    {
        var (controller, stash) = Build();
        var request = controller.ControllerContext!.HttpContext.Request;
        request.ContentType = "text/plain";
        request.Headers.Append("Content-Disposition", "attachment; filename=\"my report.txt\"");
        request.Headers.ContentLength = 10;
        request.Body = new MemoryStream(new byte[10]);

        await controller.Create(CancellationToken.None);

        Assert.NotNull(stash.LastStore);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        var (controller, stash) = Build();
        stash.RetrieveResult = null;

        var action = await controller.Get("ABCD1234", CancellationToken.None);

        var result = Assert.IsType<NotFoundObjectResult>(action);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Get_KnownId_ReturnsFileWithOriginalContentTypeAndName()
    {
        var (controller, stash) = Build();
        var payload = new byte[] { 1, 2, 3, 4 };
        stash.RetrieveResult = new StashData(
            new MemoryStream(payload),
            payload.Length,
            new StashMeta { OriginalFilename = "hello.txt", ContentType = "text/plain" });

        var action = await controller.Get("ABCD1234", CancellationToken.None);

        var file = Assert.IsAssignableFrom<FileResult>(action);
        Assert.Equal("text/plain", file.ContentType);
        Assert.Equal("hello.txt", file.FileDownloadName);
    }
}
