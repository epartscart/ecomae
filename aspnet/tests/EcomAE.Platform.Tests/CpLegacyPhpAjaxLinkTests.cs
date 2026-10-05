using EcomAE.Platform.Middleware;
using Xunit;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Tests;

public sealed class CpLegacyPhpAjaxLinkTests
{
    [Fact]
    public async Task Post_OnProcurementEndpoint_ReachesTheExistingDispatcher()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = CpLegacyPhpAjaxLinks.ProcurementEndpoint;
        var middleware = new CpLegacyPhpAjaxLinkMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);
        Assert.Equal(EcomAeRoutes.CpProcurementAjax, context.Request.Path.Value);
    }

    [Fact]
    public async Task Post_OnErpEndpoint_ReachesTheExistingErpAjaxEngine()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = CpLegacyPhpAjaxLinks.ErpEndpoint;
        var middleware = new CpLegacyPhpAjaxLinkMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);
        Assert.Equal(EcomAeRoutes.ErpAjaxPhpEndpoint, context.Request.Path.Value);
    }

    [Fact]
    public async Task DirectScripts_StayPut_SoTheGuardCanAnswerNoAccess()
    {
        foreach (var path in new[] { CpLegacyPhpAjaxLinks.ProcurementScript, CpLegacyPhpAjaxLinks.ErpScript })
        {
            var context = new DefaultHttpContext();
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = path;
            var middleware = new CpLegacyPhpAjaxLinkMiddleware(_ => Task.CompletedTask);
            await middleware.InvokeAsync(context);
            Assert.Equal(path, context.Request.Path.Value);
            Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(path, out _));
        }
    }

    [Fact]
    public void Endpoints_AreNotRedirectedToABrowsePage()
    {
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.ProcurementEndpoint, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.ErpEndpoint + "?action=dashboard", out _));
    }
}
