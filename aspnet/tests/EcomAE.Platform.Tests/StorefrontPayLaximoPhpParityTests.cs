using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontPayLaximoPhpParityTests
{
    [Fact]
    public void PaymentAndVinApps_PostNativeForms()
    {
        var pay = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontPaymentApp.razor"));
        Assert.Contains("action=\"/storefront/payment/create-operation\"", pay, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", pay, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(StorefrontPhpCanonical.Payment)", pay, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", pay, StringComparison.Ordinal);

        var vin = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVinApp.razor"));
        Assert.Contains("action=\"/storefront/vin/decode\"", vin, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", vin, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit", vin, StringComparison.Ordinal);
        Assert.DoesNotContain("Live Laximo decode stays on the classic catalog", vin, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPayAndVinLive()
    {
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/storefront/payment/create-operation").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/storefront/finance/create-operation").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/storefront/payment/notify").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/storefront/vin/decode").Status);
    }

    [Fact]
    public void Laximo_BuildsPhpCommandAndHmac()
    {
        var command = LaximoVinDecodeService.BuildFindVehicleByVin("WVWZZZ1JZXW000001", "en_US");
        Assert.Equal("FindVehicleByVIN:Locale=en_US|Catalog=|VIN=WVWZZZ1JZXW000001|ssd=|Localized=true", command);
        Assert.Equal(32, LaximoVinDecodeService.Md5Hex(command + "secret").Length);
        var envelope = LaximoVinDecodeService.BuildSoapEnvelope(command, "login", "abc");
        Assert.Contains("QueryDataLogin", envelope, StringComparison.Ordinal);
        Assert.Contains("FindVehicleByVIN", envelope, StringComparison.Ordinal);
    }

    [Fact]
    public void HandlerSanitize_StripsJunk()
    {
        Assert.Equal("epc_demo", StorefrontPaymentWriteService.SanitizeHandler("epc_demo"));
        Assert.Equal("stripedrop", StorefrontPaymentWriteService.SanitizeHandler("stripe;drop"));
        Assert.Equal("epc_demo", StorefrontPaymentWriteService.SanitizeHandler("../epc_demo!"));
        Assert.Equal("0986ABC", CpOmsWriteService.NormArticle("0986-ABC"));
        Assert.Equal("FILTEROIL01", CpOmsWriteService.NormArticle("FILTER-OIL-01"));
        Assert.Equal("", CpOmsWriteService.NormArticle(""));
        Assert.Equal("0986ABC", EpcPricing.NormalizeArticle("0986-ABC"));
        Assert.Equal("BOSCH", EpcPricing.NormalizeBrand(" bosch "));
        var stepped = EpcPricing.ApplyMarginStep(8.50m, 0m, EpcPricing.DefaultGuestRetailMarginPercent);
        Assert.Equal(11.90m, Math.Round(stepped.Price, 2, MidpointRounding.AwayFromZero));
        Assert.Equal(0.40m, stepped.MarkupDecimal);
    }

    [Fact]
    public void VinRequestApps_PostNativeForms()
    {
        var seller = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSellerRequestApp.razor"));
        Assert.Contains("action=\"@PhpSellerRequest.SellerWriteHref\"", seller, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", seller, StringComparison.Ordinal);
        Assert.DoesNotContain("send_vin_email.php", seller, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", seller, StringComparison.Ordinal);

        var inbox = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCustomerRequestsApp.razor"));
        Assert.Contains("action=\"@PhpSellerRequest.MessageWriteHref\"", inbox, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", inbox, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", inbox, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksVinRequestLive()
    {
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/storefront/vin-request/create").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/storefront/vin-request/send-message").Status);
        Assert.Equal("/storefront/vin-request/create", PhpSellerRequest.SellerWriteHref);
        Assert.Equal("/storefront/vin-request/send-message", PhpSellerRequest.MessageWriteHref);
    }

    [Fact]
    public void ResidualPhpTwins_PostNativeFormsAndCatalogLive()
    {
        var garage = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontGarageApp.razor"));
        Assert.Contains("action=\"@PhpCustomerWrites.GarageCheckCarHref\"", garage, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", garage, StringComparison.Ordinal);

        var orders = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontOrdersApp.razor"));
        Assert.Contains("action=\"@PhpCustomerWrites.GarageCheckCarHref\"", orders, StringComparison.Ordinal);

        var profile = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontProfileApp.razor"));
        Assert.Contains("action=\"@PhpCustomerWrites.ProfilePasswordHref\"", profile, StringComparison.Ordinal);
        Assert.Contains("name=\"password\"", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("Live writes remain PHP", profile, StringComparison.Ordinal);

        var cp = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpOrdersApp.razor"));
        Assert.Contains("action=\"/cp/orders/pay-refund\"", cp, StringComparison.Ordinal);
        Assert.Contains("name=\"directRefund\"", cp, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/orders/refresh-item-cost\"", cp, StringComparison.Ordinal);
        Assert.Contains("name=\"repriceFromWarehouse\"", cp, StringComparison.Ordinal);
        Assert.DoesNotContain("Live writes remain PHP", cp, StringComparison.Ordinal);

        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/storefront/garage/check-car").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/storefront/profile/change-password").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/orders/pay-refund").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/orders/refresh-item-cost").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/fulfillment-queue/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/pos/open-session").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/pos/close-session").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/pos/complete-sale").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/pos/save-settings").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/collections-dunning/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/custom-shipping/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/geo-regions/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/search-tabs/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/additional-texts/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/additional-texts/delete").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/slider-banners/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/product-filters/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/order-statuses/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/content/body").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/lang/create-string").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/content/save").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/content/tree").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/menus/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/modules/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/catalogue/line-lists/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/catalogue/tree-lists/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/accessories/photos").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/accessories/listings/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/cp/accessories/taxonomy/write").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/einvoice-save-seller").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/einvoice-save-buyer").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/einvoice-save-asp").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/shortcut-add").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/shortcut-reorder").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/inv-record-movement").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/inv-transfer").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/inv-create-warehouse").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/inv-create-item").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/inv-sync-warehouses").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/inv-run-closing").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/inv-import-csv").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/dim-save").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/customers/master-save").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/customers/settlement").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/orders/settlement").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/aftersales/rma-create").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/jw-repair-create").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/ajax/jw-repair-update-status").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(f => f.AspNetRouteOrCapability == "/erp/jewellery/karat-save").Status);

        var pos = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpPosOverviewApp.razor"));
        Assert.Contains("action=\"/cp/pos/open-session\"", pos, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/pos/close-session\"", pos, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/pos/complete-sale\"", pos, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/pos/save-settings\"", pos, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", pos, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", pos, StringComparison.Ordinal);
        var accessories = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpAccessoriesApp.razor"));
        Assert.Contains("action=\"/cp/accessories/photos\"", accessories, StringComparison.Ordinal);
        Assert.Contains("Save photo", accessories, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/accessories/listings/write\"", accessories, StringComparison.Ordinal);
        Assert.Contains("Save listing", accessories, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/accessories/taxonomy/write\"", accessories, StringComparison.Ordinal);
        Assert.Contains("Save category", accessories, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", accessories, StringComparison.Ordinal);
        var einvoice = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpEinvoiceDocumentsApp.razor"));
        Assert.Contains("action=\"/erp/ajax/einvoice-save-seller\"", einvoice, StringComparison.Ordinal);
        Assert.Contains("Save seller", einvoice, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", einvoice, StringComparison.Ordinal);
        var favorites = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpWorkspaceFavoritesApp.razor"));
        Assert.Contains("action=\"/erp/ajax/shortcut-add\"", favorites, StringComparison.Ordinal);
        Assert.Contains("Add shortcut", favorites, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/ajax/shortcut-reorder\"", favorites, StringComparison.Ordinal);
        Assert.Contains("Reorder shortcuts", favorites, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", favorites, StringComparison.Ordinal);
        var inventory = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpInventoryStockApp.razor"));
        Assert.Contains("action=\"/erp/ajax/inv-record-movement\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Record movement", inventory, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/ajax/inv-transfer\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Transfer stock", inventory, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/ajax/inv-create-warehouse\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Create warehouse", inventory, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/ajax/inv-create-item\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Create item", inventory, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/ajax/inv-run-closing\"", inventory, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/ajax/inv-import-csv\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Import CSV text", inventory, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/ajax/dim-save\"", inventory, StringComparison.Ordinal);
        Assert.Contains("Save dimensions", inventory, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", inventory, StringComparison.Ordinal);
        var contacts = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpContactsApp.razor"));
        Assert.Contains("action=\"/erp/customers/master-save\"", contacts, StringComparison.Ordinal);
        Assert.Contains("Save customer master", contacts, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", contacts, StringComparison.Ordinal);
        var receivables = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpReceivablesApp.razor"));
        Assert.Contains("action=\"/erp/customers/master-save\"", receivables, StringComparison.Ordinal);
        Assert.Contains("Save customer master", receivables, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/customers/settlement\"", receivables, StringComparison.Ordinal);
        Assert.Contains("Post customer settlement", receivables, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", receivables, StringComparison.Ordinal);
        var salesOrders = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor"));
        Assert.Contains("action=\"/erp/orders/settlement\"", salesOrders, StringComparison.Ordinal);
        Assert.Contains("Post order settlement", salesOrders, StringComparison.Ordinal);
        var returnsRma = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpReturnsRmaApp.razor"));
        Assert.Contains("action=\"/erp/aftersales/rma-create\"", returnsRma, StringComparison.Ordinal);
        Assert.Contains("Create aftersales RMA", returnsRma, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", returnsRma, StringComparison.Ordinal);
        var jewellery = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryRepairsApp.razor"));
        Assert.Contains("ErpJewelleryRepairCreateForm", jewellery, StringComparison.Ordinal);
        Assert.Contains("Create repair", jewellery, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", jewellery, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", jewellery, StringComparison.Ordinal);
        var masters = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryMastersApp.razor"));
        Assert.Contains("ErpJewelleryKaratSaveForm", masters, StringComparison.Ordinal);
        Assert.Contains("Save karat", masters, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", masters, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", masters, StringComparison.Ordinal);
        Assert.Equal("/storefront/garage/check-car", PhpCustomerWrites.GarageCheckCarHref);
        Assert.Equal("/storefront/profile/change-password", PhpCustomerWrites.ProfilePasswordHref);
    }

    [Fact]
    public void Laximo_ParsesVehicleRows()
    {
        const string xml = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <row brand="VW" name="Golf" catalog="VW2018" ssd="abc" />
              </soap:Body>
            </soap:Envelope>
            """;
        var rows = LaximoVinDecodeService.ParseVehicles(xml);
        Assert.Single(rows);
        Assert.Equal("VW", rows[0].Brand);
        Assert.Equal("Golf", rows[0].Name);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate " + relative);
    }
}
