using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TaskMasterApi.Services;

namespace TaskMasterApi.Middleware;

/// <summary>
/// Ensures every request has a Guid correlation id. Accepts inbound X-Correlation-Id (if it parses as Guid),
/// otherwise generates a new Guid. Echoes the value on the response header.
/// </summary>
public sealed class CorrelationIdMiddleware : IMiddleware
{
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(ILogger<CorrelationIdMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var rawHeader = context.Request.Headers[CorrelationIds.HeaderName].FirstOrDefault();
        if (!Guid.TryParse(rawHeader, out var correlationId))
        {
            correlationId = Guid.NewGuid();
        }

        // stash as Guid for app code
        context.Items[CorrelationIds.HttpContextItemKey] = correlationId;

        // echo for clients (lowercase "d" format for consistency)
        context.Response.Headers[CorrelationIds.HeaderName] = correlationId.ToString("D").ToLowerInvariant();

        // tag activity for future tracing
        Activity.Current?.SetTag("CorrelationId", correlationId.ToString("D"));

        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = correlationId
               }))
        {
            await next(context);
        }
    }
}