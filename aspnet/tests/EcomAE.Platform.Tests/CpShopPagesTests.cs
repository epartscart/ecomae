using EcomAE.Platform;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpShopPagesTests
{
    [Fact]
    public void OrderProcessPages_NameThePhpWorkspace()
    {
        Assert.Equal("cp/content/shop/order_process/order_card.php", CpOrderProcessPages.OrderCardPath);
        Assert.Equal("cp/content/shop/order_process/orders_items.php", CpOrderProcessPages.OrdersItemsPath);
        Assert.Equal("cp/content/shop/order_process/epc_orders_detail_pane.php", CpOrderProcessPages.OrdersDetailPanePath);
        Assert.Equal("cp/content/shop/order_process/epc_order_whatsapp_share.php", CpOrderProcessPages.WhatsappSharePath);
        Assert.Equal("WhatsApp share", CpOrderProcessPages.GuideTitle("whatsapp"));
        Assert.Equal("OMS daily", CpOrderProcessPages.GuideTitle("oms"));
        Assert.Contains("One-window OMS", CpOrderProcessPages.GuideBody("oms"), StringComparison.Ordinal);
        Assert.Equal("epc_orders_ws_kpi", CpOrderProcessPages.epc_orders_ws_kpi);
        Assert.Equal("get_email_user", CpOrderProcessPages.get_email_user);
    }

    [Fact]
    public void ProductEditor_NamesProductPhp()
    {
        Assert.Equal("cp/content/shop/catalogue/product.php", CpShopProductEditor.ProductPath);
        Assert.Equal("sort_line_list_asc_number", CpShopProductEditor.sort_line_list_asc_number);
        var app = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpProductCatalogueApp.razor"));
        Assert.Contains("CpShopProductEditor.ProductPath", app, StringComparison.Ordinal);
        Assert.Contains("@page \"/cp/shop/catalogue/product\"", app, StringComparison.Ordinal);
    }

    [Fact]
    public void PricesUploadPages_NameThePhpBodies()
    {
        Assert.Equal("cp/content/shop/prices_upload/upload_file.php", CpPricesUploadPages.UploadFilePath);
        Assert.Equal("cp/content/shop/prices_upload/price_review/price_review.php", CpPricesUploadPages.PriceReviewPath);
        Assert.Equal("clear_dir", CpPricesUploadPages.clear_dir);
        Assert.Equal("downloadFile", CpPricesUploadPages.downloadFile);
        var app = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpPricesUploadApp.razor"));
        Assert.Contains("CpPricesUploadPages.UploadFilePath", app, StringComparison.Ordinal);
        Assert.Contains("epc-price-review", app, StringComparison.Ordinal);
        var orders = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpOrdersApp.razor"));
        Assert.Contains("CpOrderProcessPages.OrderCardPath", orders, StringComparison.Ordinal);
        Assert.Contains("CpOrderProcessPages.WhatsappGuideHref", orders, StringComparison.Ordinal);
        Assert.Equal("cp/content/shop/logistics/logistics.php", CpShopDeskPages.LogisticsPath);
        Assert.Equal("cp/content/shop/crosses/crosses.php", CpShopDeskPages.CrossesPath);
        Assert.Equal("/cp/crosses-app", CpShopDeskPages.CrossesHref);
        Assert.Equal("cp/content/shop/marketing/marketing_main.php", CpShopDeskPages.MarketingMainPath);
        Assert.Contains("content/general_pages/epc_ecomae_platform_home.php", EcomaeGeneralPages.Paths);
        Assert.Contains("cp/content/control/portal/epc_auto_price_engine.php", CpControlPortalPages.Paths);
        Assert.Contains("templates/expan/desktop.php", EcomaeFrontShell.Paths);
        Assert.Contains("core/dp_core.php", EcomaeFrontShell.Paths);
        Assert.Contains("content/shop/price_engine/epc_auto_price_engine.php", CpPriceEnginePages.Paths);
        Assert.Contains("cp/content/users/helper.php", CpUsersPages.Paths);
        Assert.Contains("plugins/metadata_handler/metadata_handler.php", EcomaePluginPages.Paths);
        Assert.Contains("content/shop/tenant_hub/epc_tenant_hub_helpers.php", StorefrontShopExtras.Paths);
        Assert.Contains("cp/modules/logout/module.php", CpChromePages.Paths);
        Assert.Contains("content/shop/crm/epc_crm_helpers.php", StorefrontShopExtras.Paths);
        Assert.Equal("content/originalnye-katalogi/settings.php", StorefrontOemIlcatsPages.SettingsPath);
        Assert.Equal("autoxp_clicks_control.php", StorefrontOemIlcatsPages.AutoxpClicksPath);
        Assert.Equal("api/UCatalog/ucatalog_index.php", StorefrontOemIlcatsPages.UCatalogIndexPath);
        Assert.Equal("deploy/on-premises/epc_license_manager.php", OnPremisesLicensePages.LicenseManagerPath);
        Assert.Equal("lib/DocpartMailer/docpart_mailer.php", CpPlatformMailer.DocpartMailerPath);
        Assert.Contains("epc_demo_autoparts_bootstrap.php", EcomaeGeneralPages.Paths);
        Assert.Equal("epc_license_valid", OnPremisesLicensePages.epc_license_valid);
        Assert.Equal("generateArticleUrl2", StorefrontOemIlcatsPages.generateArticleUrl2);
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
