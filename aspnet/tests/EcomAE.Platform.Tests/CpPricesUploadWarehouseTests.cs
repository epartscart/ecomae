using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPricesUploadWarehouseTests
{
    [Fact]
    public void UploadWaysCatalog_CoversPhpManagerChannels()
    {
        var keys = CpPricesUploadWaysCatalog.All.Select(w => w.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var key in new[] { "pc", "ftp", "email", "url", "cron", "wizard", "multivendor", "vendor", "review", "edit", "api" })
        {
            Assert.Contains(key, keys);
        }

        Assert.Equal("FTP", CpPricesUploadWaysCatalog.LoadModeLabel(2));
        Assert.Equal("E-mail", CpPricesUploadWaysCatalog.LoadModeLabel(3));
        Assert.Equal("URL", CpPricesUploadWaysCatalog.LoadModeLabel(4));
        Assert.Equal("PC file", CpPricesUploadWaysCatalog.LoadModeLabel(1));
        Assert.Contains("upload_file.php", CpPricesUploadWaysCatalog.PhpUploadFileAction, StringComparison.Ordinal);
        Assert.Equal("/cp/shop/prices/price?price_id=9", CpPricesUploadWaysCatalog.EditListHref(9));
        Assert.Equal("/cp/shop/prices/upload?price_id=9", CpPricesUploadWaysCatalog.WizardHref(9));
        Assert.Equal("/cp/shop/prices/review?price_id=9", CpPricesUploadWaysCatalog.ReviewHref(9));
    }

    [Fact]
    public void OfferMarkup_AppliesGroupPercentOnPurchase()
    {
        var row = new StorefrontPartOfferDigest(1, "WH", "BOSCH", "0986", "0986", "Pad", 100m, 4, "DXB");
        var priced = StorefrontOfferMarkup.Apply(row, 0.25m);
        Assert.Equal(100m, priced.PricePurchase);
        Assert.Equal(125m, priced.Price);
        Assert.Equal(25, priced.Markup);

        var ranges = new List<(int StorageId, int GroupId, decimal Min, decimal Max, decimal Markup)>
        {
            (12, 2, 0m, 200m, 0.15m),
            (12, 2, 200.01m, 999999m, 0.10m),
        };
        Assert.Equal(0.15m, StorefrontOfferMarkup.PickMarkup(100m, ranges, 12, 2));
        Assert.Equal(0.10m, StorefrontOfferMarkup.PickMarkup(250m, ranges, 12, 2));
        Assert.Equal(0m, StorefrontOfferMarkup.PickMarkup(100m, ranges, 99, 2));
    }

    [Fact]
    public void Sql_ReadsDocpartListsAndStorageMarkups()
    {
        var sql = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"));
        Assert.Contains("SelectCpDocpartPriceStats", sql, StringComparison.Ordinal);
        Assert.Contains("FROM `shop_docpart_prices`", sql, StringComparison.Ordinal);
        Assert.Contains("FROM `shop_docpart_prices_data`", sql, StringComparison.Ordinal);
        Assert.Contains("SelectStorefrontStorageMarkups", sql, StringComparison.Ordinal);
        Assert.Contains("shop_offices_storages_map", sql, StringComparison.Ordinal);
        Assert.Contains("SelectStorefrontGroupForPercentage", sql, StringComparison.Ordinal);
        Assert.Contains("for_percentage", sql, StringComparison.Ordinal);
        Assert.Contains("SelectCpDocpartPriceListById", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ftp_password", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void PricesUploadApp_IsPhpManagerNotScaffold()
    {
        var razor = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpPricesUploadApp.razor"));
        Assert.Contains("@page \"/cp/shop/prices\"", razor, StringComparison.Ordinal);
        Assert.Contains("@page \"/cp/shop/prices/price\"", razor, StringComparison.Ordinal);
        Assert.Contains("data-epc-prices-ssr", razor, StringComparison.Ordinal);
        Assert.Contains("id=\"prices_table\"", razor, StringComparison.Ordinal);
        Assert.Contains("Manual update", razor, StringComparison.Ordinal);
        Assert.Contains("Auto-update", razor, StringComparison.Ordinal);
        Assert.Contains("epc-price-action--pc", razor, StringComparison.Ordinal);
        Assert.Contains("epc-price-action--ftp", razor, StringComparison.Ordinal);
        Assert.Contains("epc-price-action--email", razor, StringComparison.Ordinal);
        Assert.Contains("epc-price-action--url", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpDocpartPriceListsDigestAsync", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpDocpartPriceListDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("file_", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("<a href=\"/php-reference/", razor, StringComparison.Ordinal);
        Assert.Contains("shop_offices_storages_map", razor, StringComparison.Ordinal);
        Assert.Contains("for_percentage", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void WarehouseResults_ShowPurchaseUnderSellLikePhp()
    {
        var razor = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("data-show-purchase", razor, StringComparison.Ordinal);
        Assert.Contains("_showPurchaseCost", razor, StringComparison.Ordinal);
        Assert.Contains("show_data_class", razor, StringComparison.Ordinal);
        Assert.Contains("font-size: 10px; color: #959393; position: relative; top: 1px;", razor, StringComparison.Ordinal);
        Assert.Contains("id=\"all_table_products\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-ssr-warehouse-table", razor, StringComparison.Ordinal);
        Assert.Contains("epc-ssr-warehouse-row", razor, StringComparison.Ordinal);
        Assert.Contains("show_purchase_cost", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);

        var css = File.ReadAllText(Find("content/general_pages/epc_warehouse_search_parity.css"));
        Assert.Contains("#all_table_products td.td_price .show_data_class", css, StringComparison.Ordinal);

        var access = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Storefront/StorefrontPriceAccess.cs"));
        Assert.Contains("ShowPurchaseCost", access, StringComparison.Ordinal);
        Assert.Contains("ReadGroupForPercentageAsync", access, StringComparison.Ordinal);
        Assert.Contains("PricePurchase = 0m", access, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogAndRoutes_WireDocpartManager()
    {
        Assert.Equal("/cp/prices-upload-app", EcomAeRoutes.ControlPanelPricesUploadApp);
        Assert.Equal("/cp/docpart-price-lists", EcomAeRoutes.ControlPanelDocpartPriceLists);
        Assert.Equal("/cp/prices-upload-app", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/prices"));
        Assert.Equal("/cp/shop/prices/price?price_id=4", PhpSurfaceLinkMap.MapCpPhpPath("/CP/shop/prices/price?price_id=4"));

        var catalog = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/SurfacePayloadContractCatalog.cs"));
        Assert.Contains("digest-wired-awaiting-dual-sample", catalog, StringComparison.Ordinal);
        Assert.Contains("/cp/prices-upload-app", catalog, StringComparison.Ordinal);

        var module = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("BuildCpDocpartPriceListsDigestAsync", module, StringComparison.Ordinal);
        Assert.Contains("ControlPanelDocpartPriceLists", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Reporter_AppliesMarkupAndFormatsUnixUpdated()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("ApplyCustomerMarkupToOffersAsync", text, StringComparison.Ordinal);
        Assert.Contains("LoadDocpartPriceStorageIdsAsync", text, StringComparison.Ordinal);
        Assert.Contains("FormatDocpartUpdated", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpDocpartPriceListDetailAsync", text, StringComparison.Ordinal);
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
