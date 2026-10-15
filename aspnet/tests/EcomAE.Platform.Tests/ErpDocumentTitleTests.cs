using EcomAE.Platform.Middleware;
using Xunit;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Tests;

public sealed class ErpDocumentTitleTests
{
    [Fact]
    public void RealEstateTab_UsesTheCompanyName()
    {
        var html = "<head><title>Real estate · ERP · eParts Cart</title></head><p>eParts Cart stays in the page body.</p>";
        var rewritten = ErpDocumentTitle.RewriteDocument(html, "REAL Real Estate");
        Assert.Contains("<title>Real estate · ERP · REAL Real Estate</title>", rewritten, StringComparison.Ordinal);
        Assert.Contains("eParts Cart stays in the page body.", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void PartsStorefront_KeepsItsOwnName()
    {
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("www.epartscart.com");
        Assert.Null(ErpDocumentTitle.Brand(http));
        Assert.Equal(
            "<title>Orders · ERP · eParts Cart</title>",
            ErpDocumentTitle.RewriteDocument("<title>Orders · ERP · eParts Cart</title>", null));
    }

    [Fact]
    public void ClientErp_UsesTheOpenCompanyOnThePlatformHost()
    {
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("www.ecomae.com");
        http.Request.Path = "/cp/client-erp/real/erp/real-estate-app";
        http.Items[ClientErpPath.HttpContextItemKey] = "real";
        http.Items[TenantResolutionMiddleware.HttpContextItemKey] = new TenantContext(
            "www.ecomae.com",
            http.Request.Path,
            TenantSurface.Erp,
            TenantMode.ErpOnlyTenant,
            "real",
            IndustryCode: "real_estate",
            DisplayName: "REAL Real Estate");

        Assert.Equal("REAL Real Estate", ErpDocumentTitle.Brand(http));
    }

    [Theory]
    [InlineData("asap", "ASAP Autoparts")]
    [InlineData("indj", "INDJ Jewellery")]
    [InlineData("fito", "FITO Fit-out")]
    [InlineData("real", "REAL Real Estate")]
    public void LaunchCompanies_HaveTheirOwnTabName(string siteKey, string expected)
    {
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("www.ecomae.com");
        http.Items[ClientErpPath.HttpContextItemKey] = siteKey;
        Assert.Equal(expected, ErpDocumentTitle.Brand(http));
    }

    [Fact]
    public void OtherProductHosts_DoNotBorrowThePartsName()
    {
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("www.electronicae.com");
        Assert.Equal("Electronicae", ErpDocumentTitle.Brand(http));
    }

    [Fact]
    public void AmpersandInTheCompanyName_IsEncodedInTheTitle()
    {
        var html = "<title>Leases · ERP · eParts Cart</title>";
        var rewritten = ErpDocumentTitle.RewriteDocument(html, "Skyline & Co");
        Assert.Contains("<title>Leases · ERP · Skyline &amp; Co</title>", rewritten, StringComparison.Ordinal);
    }
}
