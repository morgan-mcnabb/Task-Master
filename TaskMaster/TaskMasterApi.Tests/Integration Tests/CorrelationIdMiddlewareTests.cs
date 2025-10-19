using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TaskMasterApi.Middleware;
using TaskMasterApi.Services;
using TaskMasterApi.Tests.TestHelpers;

namespace TaskMasterApi.Tests.Integration_Tests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task Generates_When_Header_Missing_And_Echoes_Header()
    {
        var ctx = HttpContextHelper.NewContext();
        var sut = new CorrelationIdMiddleware(new NullLogger<CorrelationIdMiddleware>());

        await sut.InvokeAsync(ctx, _ => Task.CompletedTask);

        ctx.Response.Headers.TryGetValue(CorrelationIds.HeaderName, out var header).Should().BeTrue();
        Guid.TryParse(header.ToString(), out _).Should().BeTrue();

        ctx.Items.TryGetValue(CorrelationIds.HttpContextItemKey, out var item).Should().BeTrue();
        item.Should().BeOfType<Guid>();
    }

    [Fact]
    public async Task Accepts_Valid_Header_And_Preserves_Value()
    {
        var incoming = Guid.NewGuid().ToString();
        var ctx = HttpContextHelper.NewContext();
        ctx.Request.Headers[CorrelationIds.HeaderName] = incoming;

        var sut = new CorrelationIdMiddleware(new NullLogger<CorrelationIdMiddleware>());
        await sut.InvokeAsync(ctx, _ => Task.CompletedTask);

        ctx.Response.Headers[CorrelationIds.HeaderName].ToString().Should().Be(incoming.ToLowerInvariant());
        ((Guid)ctx.Items[CorrelationIds.HttpContextItemKey]!).ToString("D").Should().Be(incoming.ToLowerInvariant());
    }
}