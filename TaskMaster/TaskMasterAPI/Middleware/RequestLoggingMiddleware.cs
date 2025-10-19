using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TaskMasterApi.Middleware;

/// <summary>
/// Minimal, structured request logging (one line per request).
/// CorrelationId is already in scope from CorrelationIdMiddleware.
/// </summary>
public sealed class RequestLoggingMiddleware : IMiddleware
{
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();

        var httpMethod = context.Request.Method;
        var pathAndQuery = context.Request.Path + context.Request.QueryString;
        var traceId = context.TraceIdentifier;
        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
        var userAgent = context.Request.Headers.UserAgent.ToString();

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["UserId"] = userId,
                   ["ClientIp"] = clientIp,
                   ["UserAgent"] = userAgent,
                   ["RequestId"] = traceId
               }))
        {
            await next(context);

            stopwatch.Stop();

            _logger.LogInformation(
                "HTTP {Method} {PathQuery} responded {StatusCode} in {ElapsedMs} ms",
                httpMethod,
                pathAndQuery,
                context.Response.StatusCode,
                (long)stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}