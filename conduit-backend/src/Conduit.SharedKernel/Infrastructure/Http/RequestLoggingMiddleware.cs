using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Conduit.SharedKernel.Infrastructure.Http;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var durationMs = Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 2);
            logger.LogInformation(
                "HTTP request method={Method} path={Path} status={Status} durationMs={DurationMs}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                durationMs);
        }
    }
}
