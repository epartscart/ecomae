using System.Reflection;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards /erp/multi-currency-gl-app live FX rate writes without inventing revaluation twins.
/// </summary>
public sealed class ErpMultiCurrencyGlPhpParityTests
{
    [Fact]
    public void ErpMultiCurrencyGlApp_EmitsSetRateForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpMultiCurrencyGlApp.razor"));
        Assert.Contains("/erp/multi-currency-gl/set-rate", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("action\" value=\"set_rate\"", text, StringComparison.Ordinal);
        Assert.Contains("baseCurrency", text, StringComparison.Ordinal);
        Assert.Contains("targetCurrency", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:void(0)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterMcglSetRate()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpMultiCurrencyGlSetRate", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/multi-currency-gl/set-rate", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpMultiCurrencyGlWriteService", program, StringComparison.Ordinal);
        Assert.Contains("IErpMultiCurrencyGlWriteDryRun", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("IErpMultiCurrencyGlWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_BlocksUntilConfirmWrites()
    {
        var dry = new ErpMultiCurrencyGlWriteDryRun();
        var blocked = dry.Evaluate(new ErpMultiCurrencyGlWriteRequest("set_rate", false));
        Assert.Equal("dry-run-validated", blocked.Status);
        Assert.Equal(0, blocked.Writes);
        Assert.True(blocked.WritesBlocked);
        Assert.False(blocked.PhpAuthoritative);
        var refused = dry.Evaluate(new ErpMultiCurrencyGlWriteRequest("set_rate", true));
        Assert.Equal("dry-run-confirm-refused", refused.Status);
    }

    [Fact]
    public void Catalog_KeepsWriteLiveGatedStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var write = catalog.First(item => item.AspNetRouteOrCapability.Contains("/erp/multi-currency-gl/set-rate", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_mcgl_set_rate", write.Notes, StringComparison.Ordinal);
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

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rooted = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", relative));
        Assert.True(File.Exists(rooted), $"Missing repo file: {relative}");
        return rooted;
    }
}
