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
