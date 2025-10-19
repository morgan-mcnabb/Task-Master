using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TaskMasterApi.Middleware;
using TaskMasterApi.Tests.TestHelpers;

namespace TaskMasterApi.Tests.Integration_Tests;

public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task Logs_Once_At_Information()
    {
        var logger = new Mock<ILogger<RequestLoggingMiddleware>>();
        var sut = new RequestLoggingMiddleware(logger.Object);

        var ctx = HttpContextHelper.NewContext();
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.Path = "/ping";

        await sut.InvokeAsync(ctx, _=>
        {
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });

        logger.Invocations.Any(inv => inv.Method.Name == nameof(ILogger.Log) &&
                                      (LogLevel)(inv.Arguments[0] ?? LogLevel.None) == LogLevel.Information)
            .Should().BeTrue("middleware should log a completion line at Information");
    }
}