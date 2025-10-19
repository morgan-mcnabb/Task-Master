using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace TaskMasterApi.Middleware;

public sealed class SecurityHeadersMiddleware(IOptions<SecurityHeadersOptions> optionsAccessor) : IMiddleware
{
    private readonly SecurityHeadersOptions _options = optionsAccessor.Value;

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Simple idempotent header adds, safe to repeat
        context.Response.Headers.TryAdd("X-Content-Type-Options", _options.XContentTypeOptions);
        context.Response.Headers.TryAdd("X-Frame-Options", _options.XFrameOptions);
        context.Response.Headers.TryAdd("Referrer-Policy", _options.ReferrerPolicy);

        // Requested security headers
        context.Response.Headers.TryAdd("Cross-Origin-Opener-Policy", _options.CrossOriginOpenerPolicy);
        context.Response.Headers.TryAdd("Cross-Origin-Resource-Policy", _options.CrossOriginResourcePolicy);
        context.Response.Headers.TryAdd("Permissions-Policy", _options.PermissionsPolicy);

        return next(context);
    }
}