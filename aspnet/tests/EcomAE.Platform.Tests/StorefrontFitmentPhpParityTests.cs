using System.Text.Json;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards PHP part_search fitment button parity on ASP.NET CHPU:
/// brands picker → /storefront/fitment (cache) → /storefront/fitment-widget.js fallback.
/// </summary>
public sealed class StorefrontFitmentPhpParityTests
{
    [Fact]
    public void ParityJs_WiresAspNetFitmentRoutesAndEpartscrossFallback()
    {
        var text = File.ReadAllText(FindRepoFile("content/general_pages/epc_warehouse_search_parity.js"));

        Assert.Contains("/storefront/search-brands?", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/fitment?", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/fitment-widget.js?", text, StringComparison.Ordinal);
        Assert.Contains("loadEpartscrossFitmentFallback", text, StringComparison.Ordinal);
        Assert.Contains("Loading vehicle applicability from cross-reference catalog", text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Fitment action requires ASP.NET catalog route (product PHP proxies disabled).",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("epartscross_fitment.js.php", text, StringComparison.Ordinal);
        Assert.DoesNotContain("umapi_proxy.php", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchApp_UsesFitmentCacheBustedParityScript()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("epc_warehouse_search_parity.js?v=20260812-whgroup", text, StringComparison.Ordinal);
        Assert.Contains("epc-fitment-check-btn", text, StringComparison.Ordinal);
        Assert.Contains("applicability_widget", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontModule_MapsFitmentRoutes()
    {
        Assert.Equal("/storefront/fitment", EcomAeRoutes.StorefrontFitment);
        Assert.Equal("/storefront/fitment-widget.js", EcomAeRoutes.StorefrontFitmentWidgetJs);
        Assert.Equal("/storefront/fitment-table", EcomAeRoutes.StorefrontFitmentTable);

        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("EcomAeRoutes.StorefrontFitment", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.StorefrontFitmentWidgetJs", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.StorefrontFitmentTable", text, StringComparison.Ordinal);
        Assert.Contains("IStorefrontFitmentService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FitmentService_ExtractsArtIdAndVehicleSections()
    {
        var analogs = JsonSerializer.Deserialize<JsonElement>("""
            {"data":[
              {"BRAND":"OTHER","ARTICLE_NR":"C110J","ART_ID":1},
              {"BRAND":"JS ASAKASHI","ARTICLE_NR":"C110J","ART_ID":777}
            ]}
            """);
        var id = StorefrontFitmentService.ExtractArticleId(analogs!, "C110J", "JS ASAKASHI");
        Assert.Equal(777, id);

        var links = JsonSerializer.Deserialize<JsonElement>("""
            {"PC":[{"MANUFACTURER":"TOYOTA","MODEL_SERIES":"Corolla","CI_FROM":"2000","CI_TO":"2007"}],
             "CV":[],"Motorcycle":[{"MANUFACTURER":"YAMAHA"}]}
            """);
        var (pc, cv, moto, total) = StorefrontFitmentService.ExtractVehicleSections(links!);
        Assert.Equal(2, total);
        Assert.NotNull(pc);
        Assert.NotNull(cv);
        Assert.NotNull(moto);
    }

    [Fact]
    public void FitmentService_RewritesCrossbaseGettableToAspNetProxy()
    {
        var raw = "var url='https://crossbase.ru/prim/getjs/gettable.php?n=C110J&lang=en&cartype=UNI';";
        var rewritten = StorefrontFitmentService.RewriteWidgetJsForTests(raw, "C110J", "en");
        Assert.Contains("/storefront/fitment-table?n=C110J", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("crossbase.ru/prim/getjs/gettable.php", rewritten, StringComparison.Ordinal);
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

        throw new FileNotFoundException(relative);
    }
}
