using EcomAE.Platform.Configuration;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection(PreferAspNetAppsCollection.Name)]
public sealed class PhpServingDeactivatedMiddlewareTests : IDisposable
{
    public PhpServingDeactivatedMiddlewareTests()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
    }

    public void Dispose()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
    }

    [Fact]
    public async Task PhpReferenceReturns503WhenTemporarilyDeactivated()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
        var calledNext = false;
        var mw = new PhpServingDeactivatedMiddleware(
            _ =>
            {
                calledNext = true;
                return Task.CompletedTask;
            },
            Options.Create(new PhpReferenceOptions
            {
                TemporarilyDeactivatePhpServing = true,
                KeepPhpProjectAvailable = true,
            }));

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/php-reference/storefront";
        ctx.Response.Body = new MemoryStream();
        await mw.InvokeAsync(ctx);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, ctx.Response.StatusCode);
        Assert.False(calledNext);
        Assert.Equal("temporarily-deactivated", ctx.Response.Headers[PhpServingDeactivatedMiddleware.FlagHeader].ToString());
        Assert.Equal("false", ctx.Response.Headers["X-EcomAE-Cutover-Allowed"].ToString());
        Assert.Equal("false", ctx.Response.Headers["X-EcomAE-Ready-For-Php-Removal"].ToString());
    }

    [Fact]
    public async Task ProductPathsPassThroughWhenDeactivated()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
        var calledNext = false;
        var mw = new PhpServingDeactivatedMiddleware(
            _ =>
            {
                calledNext = true;
                return Task.CompletedTask;
            },
            Options.Create(new PhpReferenceOptions
            {
                TemporarilyDeactivatePhpServing = true,
                KeepPhpProjectAvailable = true,
            }));

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/storefront/app";
        await mw.InvokeAsync(ctx);

        Assert.True(calledNext);
        Assert.Equal("temporarily-deactivated", ctx.Response.Headers[PhpServingDeactivatedMiddleware.FlagHeader].ToString());
    }
}
