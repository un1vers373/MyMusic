using System.Buffers;
using Microsoft.Net.Http.Headers;

namespace MyMusic.Services;

/// <summary>
/// Writes a stored file to the response with HTTP Range support (206 Partial Content),
/// so seeking in the browser's audio element works for any storage backend.
/// </summary>
public static class RangeStreamer
{
    public static async Task WriteAsync(
        HttpContext ctx, IFileStorage storage, string key, string contentType, long totalLength, CancellationToken ct)
    {
        var req = ctx.Request;
        var res = ctx.Response;

        long start = 0, end = totalLength - 1;
        var partial = false;

        var range = req.GetTypedHeaders().Range;
        if (range != null && range.Unit.Equals("bytes", StringComparison.OrdinalIgnoreCase) && range.Ranges.Count == 1)
        {
            var r = range.Ranges.First();
            if (r.From.HasValue)
            {
                start = r.From.Value;
                if (r.To.HasValue) end = Math.Min(r.To.Value, totalLength - 1);
                partial = true;
            }
            else if (r.To.HasValue && r.To.Value > 0)
            {
                start = Math.Max(0, totalLength - r.To.Value);
                partial = true;
            }

            if (partial && (start >= totalLength || start > end))
            {
                res.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                res.Headers["Content-Range"] = $"bytes */{totalLength}";
                return;
            }
        }

        var length = end - start + 1;

        Stream? stream = null;
        if (!HttpMethods.IsHead(req.Method))
        {
            try
            {
                stream = await storage.OpenReadAsync(key, start, length, ct);
            }
            catch (FileNotFoundException)
            {
                throw new AppException(StatusCodes.Status404NotFound, "The file is missing from storage.");
            }
        }

        res.StatusCode = partial ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK;
        res.ContentType = contentType;
        res.ContentLength = length;
        res.Headers["Accept-Ranges"] = "bytes";
        res.Headers["Cache-Control"] = "private, max-age=3600";
        if (partial) res.Headers["Content-Range"] = $"bytes {start}-{end}/{totalLength}";

        if (stream == null) return;

        await using (stream)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            try
            {
                var remaining = length;
                while (remaining > 0)
                {
                    var toRead = (int)Math.Min(buffer.Length, remaining);
                    var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), ct);
                    if (read == 0) break;
                    await res.Body.WriteAsync(buffer.AsMemory(0, read), ct);
                    remaining -= read;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
