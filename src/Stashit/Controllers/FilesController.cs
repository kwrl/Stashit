using System.Buffers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stashit.Configuration;
using Stashit.Services;

namespace Stashit.Controllers;

/// <summary>
/// HTTP surface for stashing and retrieving files.
/// </summary>
[ApiController]
[Route("files")]
public class FilesController(IStashService stash, IOptions<LimitsOptions> limits) : ControllerBase
{
    private const int ReadChunkSize = 64 * 1024;

    /// <summary>
    /// Reads the raw request body and stashes it under a fresh id.
    /// Responds 201 Created with a Location header pointing at GET /files/{id}.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var contentType = Request.ContentType ?? "application/octet-stream";
        var maxBytes = limits.Value.MaxUploadBytes;

        if (Request.ContentLength is long declared && declared > maxBytes)
        {
            return PayloadTooLarge(maxBytes);
        }

        var body = new MemoryStream(capacity: ReadChunkSize);
        var rented = ArrayPool<byte>.Shared.Rent(ReadChunkSize);
        try
        {
            int read;
            while ((read = await Request.Body.ReadAsync(rented.AsMemory(0, ReadChunkSize), cancellationToken)) > 0)
            {
                if (body.Length + read > maxBytes)
                {
                    return PayloadTooLarge(maxBytes);
                }
                body.Write(rented, 0, read);
            }

            body.Position = 0;
            var result = await stash.StoreAsync(body, ParseFilename(), contentType, cancellationToken);
            var location = $"{Request.Scheme}://{Request.Host}/files/{result.Id}";
            return Created(location, null);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
            body.Dispose();
        }
    }

    /// <summary>
    /// Downloads a previously stashed file by id, restoring the original
    /// Content-Type and filename.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        var stored = await stash.RetrieveAsync(id, cancellationToken);
        if (stored is null)
        {
            return NotFound(new { error = "not_found", message = "File not found or expired." });
        }

        return File(stored.Content, stored.Meta.ContentType, stored.Meta.OriginalFilename);
    }

    private static ObjectResult PayloadTooLarge(long maxBytes) =>
        new(new { error = "too_large", message = $"Upload exceeds the maximum size of {maxBytes} bytes." })
        { StatusCode = StatusCodes.Status413PayloadTooLarge };

    private string? ParseFilename()
    {
        var disposition = Request.Headers.ContentDisposition.ToString();
        if (string.IsNullOrWhiteSpace(disposition))
        {
            return null;
        }

        foreach (var part in disposition.Split(';'))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("filename=", StringComparison.OrdinalIgnoreCase)
                && trimmed.Length > "filename=".Length)
            {
                var value = trimmed["filename=".Length..].Trim().Trim('"');
                return value.Length > 0 ? value : null;
            }
        }

        return null;
    }
}
