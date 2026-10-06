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
    public async Task CrmPost_ReachesTheExistingCrmWriter_AsTheOperatorButton()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = CpLegacyPhpAjaxLinks.CrmEndpoint;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        var body = System.Text.Encoding.UTF8.GetBytes("action=save_lead&company=Acme");
        context.Request.Body = new MemoryStream(body);
        context.Request.ContentLength = body.Length;
        var middleware = new CpLegacyPhpAjaxLinkMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context);
        Assert.Equal(EcomAeRoutes.CpCrmAction, context.Request.Path.Value);
        Assert.True(context.Items.ContainsKey(CpLegacyPhpAjaxLinks.OperatorPostItem));
        Assert.Equal("XMLHttpRequest", context.Request.Headers["X-Requested-With"].ToString());
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.CrmEndpoint, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.CustomerEndpoint, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.DocumentEndpoint, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.SaoExec, out _));
    }

    [Fact]
    public void Endpoints_AreNotRedirectedToABrowsePage()
    {
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.ProcurementEndpoint, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.ErpEndpoint + "?action=dashboard", out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.SaoExec, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.OrderPayRefund, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.OrdersOms, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.MultivendorIngest, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.CommerceIngest, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.CrossCp, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.ContentJsonList, out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(CpLegacyPhpAjaxLinks.NotificationTest, out _));
    }
}
