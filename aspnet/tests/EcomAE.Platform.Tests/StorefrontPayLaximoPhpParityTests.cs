using EcomAE.Platform.Migration;
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
