using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosHostGateMiddlewareTests
{
    [Theory]
    [InlineData("www.ecomae.com", true)]
    [InlineData("ecomae.com", true)]
    [InlineData("cp.ecomae.com", true)]
    [InlineData("www.epartscart.com", false)]
    [InlineData("epartscart.com", false)]
    [InlineData("www.electronicae.com", false)]
    [InlineData("auto.ecomae.com", false)]
    public void SuperCpHostAllowlist(string host, bool allowed)
    {
        Assert.Equal(allowed, PlatformHostPolicy.IsSuperCpHost(host));
    }

    [Theory]
    [InlineData("/bos", true)]
    [InlineData("/bos/", true)]
    [InlineData("/bos/login", true)]
    [InlineData("/bos/app", true)]
    [InlineData("/bos/tenants-app", true)]
    [InlineData("/bos/fleet-summary", true)]
    [InlineData("/bos/ajax-writes/catalog", true)]
    [InlineData("/BOS/", true)]
    [InlineData("/php-reference/bos", true)]
    [InlineData("/marketing/bos", false)]
    [InlineData("/bos/what-is-a-business-operating-system", false)] // PHP marketing knowledge
    [InlineData("/bos/blockchain-proof-layer", false)]
    [InlineData("/cp", false)]
    [InlineData("/erp/app", false)]
    public void ProductBosPathDetection(string path, bool isBos)
    {
        Assert.Equal(isBos, PlatformHostPolicy.IsProductBosPath(path));
    }

    [Fact]
    public async Task TenantHostGets404ForBos()
    {
        var nextCalled = false;
        var mw = new BosHostGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("www.epartscart.com");
        ctx.Request.Path = "/bos";
        ctx.Response.Body = new MemoryStream();

        await mw.InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.Equal("super-cp-only", ctx.Response.Headers["X-EcomAE-Bos-Host-Gate"].ToString());
    }

    [Fact]
    public async Task SuperCpHostPassesBosThrough()
    {
        var nextCalled = false;
        var mw = new BosHostGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("www.ecomae.com");
        ctx.Request.Path = "/bos/login";

        await mw.InvokeAsync(ctx);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task TenantHostAllowsCp()
    {
        var nextCalled = false;
        var mw = new BosHostGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("www.epartscart.com");
        ctx.Request.Path = "/cp";

        await mw.InvokeAsync(ctx);

        Assert.True(nextCalled);
    }

    [Fact]
    public void LiveTenantMandateExcludesBosSurface()
    {
        var summary = Migration.LiveTenantPresentationLock.BuildSummary();
        Assert.Contains("Super-CP", summary["mandate"]!.ToString()!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/ /cp /erp /bos", summary["mandate"]!.ToString()!, StringComparison.Ordinal);

        var tenants = (Array)summary["tenants"]!;
        foreach (var raw in tenants)
        {
            var row = (IReadOnlyDictionary<string, object>)raw!;
            var surfaces = (string[])row["surfaces"]!;
            Assert.DoesNotContain("bos", surfaces);
            Assert.Equal("super-cp-only-404-on-tenant", row["bos"]);
        }
    }
}
