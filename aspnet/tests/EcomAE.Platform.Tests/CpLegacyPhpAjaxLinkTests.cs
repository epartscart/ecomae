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
    public void CurrencyPreview_ReachesTheExistingLiveRateReader()
    {
        var rewritten = CpLegacyPhpAjaxLinks.TryRewrite(HttpMethods.Get, CpLegacyPhpAjaxLinks.CurrencyRates, "preview", out var target, out var operatorPost);
        Assert.True(rewritten);
        Assert.False(operatorPost);
        Assert.Equal(EcomAeRoutes.CpCurrenciesLivePreview, target.Value);
    }

    [Fact]
    public void CurrencyApply_ReachesTheExistingWriter_AsTheOperatorButton()
    {
        var rewritten = CpLegacyPhpAjaxLinks.TryRewrite(HttpMethods.Post, CpLegacyPhpAjaxLinks.CurrencyRates, "apply", out var target, out var operatorPost);
        Assert.True(rewritten);
        Assert.True(operatorPost);
        Assert.Equal(EcomAeRoutes.CpCurrenciesLiveApply, target.Value);
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.CurrencyRates, out _));
    }

    [Fact]
    public void QuoteAltOptions_ReachTheExistingReader()
    {
        var rewritten = CpLegacyPhpAjaxLinks.TryRewrite(HttpMethods.Get, CpLegacyPhpAjaxLinks.QuoteAltOptions, null, out var target, out _);
        Assert.True(rewritten);
        Assert.Equal(EcomAeRoutes.CpQuoteAltOptions, target.Value);
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.QuoteAltOptions, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.PricePackSetup, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.PriceExtract, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.PriceImportCsv, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.OrdersDetailPane, out _));
    }

    [Fact]
    public void Endpoints_AreNotRedirectedToABrowsePage()
    {
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.ProcurementEndpoint, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.ErpEndpoint + "?action=dashboard", out _));
    }
}
