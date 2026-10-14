using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class IpHostGateMiddlewareTests
{
    [Theory]
    [InlineData("/ip", true)]
    [InlineData("/ip/", true)]
    [InlineData("/ip/login", true)]
    [InlineData("/ip/app", true)]
    [InlineData("/ip/ecosystem-app", true)]
    [InlineData("/IP/login", true)]
    [InlineData("/php-reference/ip", true)]
    [InlineData("/bos", false)]
    [InlineData("/cp", false)]
    [InlineData("/lifeos", false)]
    public void ProductIpPathDetection(string path, bool isIp)
    {
        Assert.Equal(isIp, PlatformHostPolicy.IsProductIpPath(path));
    }

    [Theory]
    [InlineData("lifeos.ecomae.com", true)]
    [InlineData("www.lifeos.ecomae.com", true)]
    [InlineData("www.ecomae.com", false)]
    [InlineData("epartscart.com", false)]
    public void LifeOsHostAllowlist(string host, bool allowed)
    {
        Assert.Equal(allowed, PlatformHostPolicy.IsLifeOsHost(host));
    }

    [Fact]
    public async Task TenantHostGets404ForIp()
    {
        var nextCalled = false;
        var mw = new IpHostGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("www.epartscart.com");
        ctx.Request.Path = "/ip/login";
        ctx.Response.Body = new MemoryStream();

        await mw.InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.Equal("super-cp-only", ctx.Response.Headers["X-EcomAE-Ip-Host-Gate"].ToString());
    }

    [Fact]
    public async Task SuperCpHostPassesIpThrough()
    {
        var nextCalled = false;
        var mw = new IpHostGateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("www.ecomae.com");
        ctx.Request.Path = "/ip/login";

        await mw.InvokeAsync(ctx);

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/lifeos")]
    [InlineData("/marketing/app")]
    public async Task LifeOsHostRedirectsVisitorsToEcomae(string path)
    {
        var nextCalled = false;
        var mw = new LifeOsHostHomeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("lifeos.ecomae.com");
        ctx.Request.Path = path;

        await mw.InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status302Found, ctx.Response.StatusCode);
        Assert.Equal(LifeOsHostHomeMiddleware.RetiredHome, ctx.Response.Headers.Location.ToString());
        Assert.Equal("retired", ctx.Response.Headers["X-EcomAE-LifeOs-Host"].ToString());
        Assert.Equal(path, ctx.Response.Headers["X-EcomAE-LifeOs-From"].ToString());
    }

    [Fact]
    public async Task LifeOsHostLeavesHealthProbesAlone()
    {
        var nextCalled = false;
        var mw = new LifeOsHostHomeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("lifeos.ecomae.com");
        ctx.Request.Path = "/health";

        await mw.InvokeAsync(ctx);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status302Found, ctx.Response.StatusCode);
    }

    [Theory]
    [InlineData("/", false)]
    [InlineData("/lifeos", false)]
    [InlineData("/health", true)]
    [InlineData("/ready", true)]
    [InlineData("/migration/release", true)]
    public void LifeOsOpsPathsStayOnTheHost(string path, bool ops)
    {
        Assert.Equal(ops, LifeOsHostHomeMiddleware.IsOpsPath(path));
    }

    [Fact]
    public void EcosystemCatalogIncludesLifeOsAndBos()
    {
        Assert.Contains(EcomaeEcosystemCatalog.AmbientOsProducts, p => p.Key == "lifeos");
        Assert.Contains(EcomaeEcosystemCatalog.BosModules, m => m.Key == "erp");
        Assert.Contains(EcomaeEcosystemCatalog.ClientManagement, c => c.Key == "tenants");
        Assert.Contains(EcomaeEcosystemCatalog.LifeOsConsoles, c => c.Key == "home");
        Assert.Equal("live-scaffold", EcomaeEcosystemCatalog.FindOs("lifeos")!.Status);
    }
}
