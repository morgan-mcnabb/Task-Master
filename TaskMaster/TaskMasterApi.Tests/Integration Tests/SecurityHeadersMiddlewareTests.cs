using FluentAssertions;
using Microsoft.Extensions.Options;
using TaskMasterApi.Middleware;
using TaskMasterApi.Tests.TestHelpers;

namespace TaskMasterApi.Tests.Integration_Tests;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Adds_Configured_Headers()
    {
        var options = Options.Create(new SecurityHeadersOptions
        {
            XContentTypeOptions = "nosniff",
            XFrameOptions = "DENY",
            ReferrerPolicy = "no-referrer",
            CrossOriginOpenerPolicy = "same-origin",
            CrossOriginResourcePolicy = "same-site",
            PermissionsPolicy = "geolocation=()"
        });

        var sut = new SecurityHeadersMiddleware(options);
        var ctx = HttpContextHelper.NewContext();

        await sut.InvokeAsync(ctx, _ => Task.CompletedTask);

        ctx.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        ctx.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        ctx.Response.Headers["Referrer-Policy"].ToString().Should().Be("no-referrer");
        ctx.Response.Headers["Cross-Origin-Opener-Policy"].ToString().Should().Be("same-origin");
        ctx.Response.Headers["Cross-Origin-Resource-Policy"].ToString().Should().Be("same-site");
        ctx.Response.Headers["Permissions-Policy"].ToString().Should().Be("geolocation=()");
    }
}