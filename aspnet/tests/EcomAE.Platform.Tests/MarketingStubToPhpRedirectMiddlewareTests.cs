using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MarketingStubToPhpRedirectMiddlewareTests
{
    [Fact]
    public async Task MarketingAppPassesThrough()
    {
        var nextCalled = false;
        var mw = new MarketingStubToPhpRedirectMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/marketing/app";

        await mw.InvokeAsync(ctx);

        Assert.True(nextCalled);
        Assert.False(ctx.Response.Headers.ContainsKey("Location"));
    }

    [Theory]
    [InlineData("/marketing/platform", "/platform")]
    [InlineData("/marketing/pricing", "/platform/pricing")]
    [InlineData("/marketing/privacy", "/privacy")]
    [InlineData("/marketing/bos", "/bos/what-is-a-business-operating-system")]
    public async Task KnownStubRedirectsToPhpCanonical(string stub, string expected)
    {
        var nextCalled = false;
        var mw = new MarketingStubToPhpRedirectMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = stub;

        await mw.InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status302Found, ctx.Response.StatusCode);
        Assert.Equal(expected, ctx.Response.Headers.Location.ToString());
        Assert.Equal("php-canonical", ctx.Response.Headers["X-EcomAE-Marketing-Stub-Redirect"].ToString());
    }

    [Fact]
    public async Task UnknownStubDoesNotOpenRedirect()
    {
        var nextCalled = false;
        var mw = new MarketingStubToPhpRedirectMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/marketing/../../evil";

        await mw.InvokeAsync(ctx);

        Assert.True(nextCalled);
        Assert.False(ctx.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public void BosKnowledgeConstantIsNotProductBosPath()
    {
        Assert.False(PlatformHostPolicy.IsProductBosPath(EcomaeMarketingPages.BosKnowledgePhp));
    }
}
