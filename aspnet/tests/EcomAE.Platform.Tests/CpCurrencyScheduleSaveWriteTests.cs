using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpCurrencyScheduleSaveWriteTests
{
    [Fact]
    public void Route_exposes_schedule_save()
    {
        Assert.Equal("/cp/currencies/schedule-save", EcomAeRoutes.CpCurrenciesScheduleSave);
    }

    [Fact]
    public void Timezone_normalize_and_validate_match_php()
    {
        Assert.Equal("Asia/Dubai", CpCurrencyWriteService.NormalizeTimezone(""));
        Assert.Equal("Asia/Dubai", CpCurrencyWriteService.NormalizeTimezone("   "));
        Assert.Equal("UTC", CpCurrencyWriteService.NormalizeTimezone("UTC"));
        Assert.True(CpCurrencyWriteService.IsValidTimezone("UTC"));
        Assert.False(CpCurrencyWriteService.IsValidTimezone("Not/AZone"));
        Assert.False(CpCurrencyWriteService.IsValidTimezone(""));
    }

    [Fact]
    public void Page_posts_native_schedule_save()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpCurrenciesApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/currencies/schedule-save\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"enabled\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"timezone\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"hour\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_schedule_save_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/currencies/schedule-save");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("schedule_save", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_price_settings", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_currency_live_rates.php", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_schedule_save_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("CpCurrenciesScheduleSave", module, StringComparison.Ordinal);
        Assert.Contains("SaveScheduleAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpCurrencyWriteService.cs"));
        Assert.Contains("epc_currency_live_schedule_save", service, StringComparison.Ordinal);
        Assert.Contains("fx_live_auto_enabled", service, StringComparison.Ordinal);
        Assert.Contains("fx_live_auto_timezone", service, StringComparison.Ordinal);
        Assert.Contains("fx_live_auto_hour", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
