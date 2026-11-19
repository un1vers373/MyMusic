using MyMusic.Services;

namespace MyMusic.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (AppException ex)
        {
            await WriteAsync(ctx, ex.StatusCode, ex.Message);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // The client went away (for example, skipped a song). Nothing to report.
        }
        catch (BadHttpRequestException ex)
        {
            await WriteAsync(ctx, ex.StatusCode == 413 ? 413 : 400,
                ex.StatusCode == 413 ? "The upload is too large." : "Bad request.");
        }
        catch (InvalidDataException)
        {
            await WriteAsync(ctx, 413, "The upload is too large or malformed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            await WriteAsync(ctx, 500, "Something went wrong on the server.");
        }
    }

    private static async Task WriteAsync(HttpContext ctx, int status, string message)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { error = message });
    }
}
