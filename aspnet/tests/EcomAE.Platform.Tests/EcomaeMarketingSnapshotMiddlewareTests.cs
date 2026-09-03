using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomaeMarketingSnapshotMiddlewareTests
{
    [Fact]
    public async Task DemoIndustryQuerySelectsFashionOnMarketingHost()
    {
        var nextCalled = false;
        var mw = new EcomaeMarketingSnapshotMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = HttpMethods.Get;
        ctx.Request.Host = new HostString("www.ecomae.com");
        ctx.Request.Path = "/platform/demo";
        ctx.Request.QueryString = new QueryString("?industry=fashion");
        ctx.Response.Body = new MemoryStream();

        await mw.InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
        ctx.Response.Body.Position = 0;
        var html = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        Assert.Contains("name=\"epm_industry\" value=\"fashion\" checked", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"epm_industry\" value=\"auto_parts\" checked", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SlugForDemoAliasIsPlatformDemo()
    {
        Assert.Equal("platform__demo", EcomaeMarketingSnapshots.SlugFor("/demo?industry=erp_only"));
    }
}
