using System.Reflection;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards /erp/ajax/bos-vat-refund-save live writes without inventing schema ensure.</summary>
public sealed class ErpBosVatRefundSavePhpParityTests
{
    [Fact]
    public void VatApp_EmitsSaveRecordForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpVatApp.razor"));
        Assert.Contains("/erp/ajax/bos-vat-refund-save", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/bos-vat-refund-status", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Save record", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterRefundSaveWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpAjaxBosVatRefundSave", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/bos-vat-refund-save", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBosVatRefundSaveWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("IErpBosVatRefundSaveWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_KeepsWriteLiveGatedStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var write = catalog.First(item => item.AspNetRouteOrCapability.Contains("/erp/ajax/bos-vat-refund-save", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_bos_vat_refund_save", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_MatchPhp()
    {
        Assert.Equal("recorded", ErpBosVatRefundSaveWriteService.NormalizeStatus(""));
        Assert.Equal("recorded", ErpBosVatRefundSaveWriteService.NormalizeStatus("nope"));
        Assert.Equal("refunded", ErpBosVatRefundSaveWriteService.NormalizeStatus("refunded"));
        Assert.Equal(12.50m, ErpBosVatRefundSaveWriteService.ResolveVat(null, 250, 5));
        var ae = ErpBosVatRefundSaveWriteService.SchemeFor("AE");
        var calc = ErpBosVatRefundSaveWriteService.Calculate(ae, 12.50m);
        Assert.Equal(5.83m, calc.Refund);
        Assert.Equal(4.80m, calc.Fee);
        Assert.Equal(6.67m, calc.Retained);
        Assert.Equal("Refund record saved (refund 5.83)", ErpBosVatRefundSaveWriteService.FormatSavedMessage(5.83m));
        Assert.Equal(1_788_480_000, ErpBosVatRefundSaveWriteService.ResolveSaleDateUnix("2026-09-04"));
    }

    [Fact]
    public void DryRun_ValidatesAndRefusesConfirm()
    {
        var ok = new ErpBosVatRefundSaveDryRun().Evaluate(new ErpBosVatRefundSaveRequest(0, "SI-1", 250));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(0, ok.Writes);
        var missing = new ErpBosVatRefundSaveDryRun().Evaluate(new ErpBosVatRefundSaveRequest(-1));
        Assert.Equal("invalid_request", missing.ValidationCode);
        var refused = new ErpBosVatRefundSaveDryRun().Evaluate(new ErpBosVatRefundSaveRequest(0, "SI-1", 250, true));
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
