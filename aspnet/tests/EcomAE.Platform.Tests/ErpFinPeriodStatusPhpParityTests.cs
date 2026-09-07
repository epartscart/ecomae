using System.Reflection;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards /erp/fin/periods/status live writes without inventing period generate.</summary>
public sealed class ErpFinPeriodStatusPhpParityTests
{
    [Fact]
    public void FinAdvancedApp_EmitsSetStatusForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpFinAdvancedApp.razor"));
        Assert.Contains("/erp/fin/periods/status", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Set status", text, StringComparison.Ordinal);
        Assert.Contains("on_hold", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterFinPeriodStatusWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpFinPeriodStatus", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/fin/periods/status", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpFinPeriodStatusWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("IErpFinPeriodStatusWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_KeepsWriteLiveGatedStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var write = catalog.First(item => item.AspNetRouteOrCapability.Contains("/erp/fin/periods/status", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_fin_period_set_status", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_MatchPhp()
    {
        Assert.Equal("open", ErpFinPeriodStatusWriteService.NormalizeStatus("open"));
        Assert.Equal("on_hold", ErpFinPeriodStatusWriteService.NormalizeStatus("ON_HOLD"));
        Assert.Equal("closed", ErpFinPeriodStatusWriteService.NormalizeStatus("closed"));
        Assert.Null(ErpFinPeriodStatusWriteService.NormalizeStatus(""));
        Assert.Null(ErpFinPeriodStatusWriteService.NormalizeStatus("locked"));
        Assert.Null(ErpFinPeriodStatusWriteService.NormalizeStatus("nope"));
    }

    [Fact]
    public void DryRun_ValidatesAndRefusesConfirm()
    {
        var ok = new ErpFinPeriodStatusDryRun().Evaluate(new ErpFinPeriodStatusRequest(2026, 8, "closed"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(0, ok.Writes);
        var missing = new ErpFinPeriodStatusDryRun().Evaluate(new ErpFinPeriodStatusRequest(0, 8, "open"));
        Assert.Equal("invalid_request", missing.ValidationCode);
        var bad = new ErpFinPeriodStatusDryRun().Evaluate(new ErpFinPeriodStatusRequest(2026, 8, "locked"));
        Assert.Equal("invalid_request", bad.ValidationCode);
        var refused = new ErpFinPeriodStatusDryRun().Evaluate(new ErpFinPeriodStatusRequest(2026, 8, "closed", true));
        Assert.Equal("confirm_writes_refused", refused.ValidationCode);
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
