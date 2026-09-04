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
    public async Task PreferAspNetAppsAloneDoesNotBlockPhpReferenceCompare()
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
                TemporarilyDeactivatePhpServing = false,
                KeepPhpProjectAvailable = true,
            }));

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/php-reference/cp";
        await mw.InvokeAsync(ctx);

        Assert.True(calledNext);
        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        Assert.True(string.IsNullOrEmpty(ctx.Response.Headers[PhpServingDeactivatedMiddleware.FlagHeader].ToString()));
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
        Assert.Equal("primary", ctx.Response.Headers["X-EcomAE-Platform"].ToString());
        Assert.Equal("paused", ctx.Response.Headers["X-EcomAE-Compat"].ToString());
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

    [Fact]
    public async Task EnPartSearchRedirectsToSearchAppWhenPreferAspNetApps()
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
        ctx.Request.Path = "/en/shop/part_search";
        ctx.Request.QueryString = new QueryString("?article=1310154101");
        ctx.Response.Body = new MemoryStream();
        await mw.InvokeAsync(ctx);

        // /en/shop/part_search is a Blazor same-URL alias — do not 302 it away.
        Assert.True(calledNext);
        Assert.True(string.IsNullOrEmpty(ctx.Response.Headers.Location.ToString()));
    }

    [Fact]
    public async Task EnWarehouseSearchPassesThroughWhenSelfMapped()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
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
        ctx.Request.Path = "/en/shop/warehouse-search";
        await mw.InvokeAsync(ctx);

        Assert.True(calledNext);
        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
    }
}
