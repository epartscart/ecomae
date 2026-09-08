using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_fy_create_year</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpFyCreatePhpParityTests
{
    [Fact]
    public void PeriodCloseApp_PostsNativeCreateYearForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPeriodCloseApp.razor"));
        Assert.Contains("action=\"/erp/fiscal-years/create\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"label\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"start_date\"", text, StringComparison.Ordinal);
        Assert.Contains("Create fiscal year", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersFyCreateWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpFyCreateWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpFyCreateWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksFyCreateLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/fiscal-years/create");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_fy_create_year", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/fy-create");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void TryParseUnix_AcceptsIsoDateAndUnix()
    {
        Assert.True(ErpFyCreateWriteService.TryParseUnix("2026-01-01", 0, out var iso));
        Assert.True(iso > 0);
        Assert.True(ErpFyCreateWriteService.TryParseUnix(null, 1767225600, out var unix));
        Assert.Equal(1767225600, unix);
        Assert.False(ErpFyCreateWriteService.TryParseUnix("", 0, out _));
    }

    [Fact]
    public void DryRun_RequiresDatesAndRefusesConfirm()
    {
        var ok = new ErpFyCreateDryRun().Evaluate(new ErpFyCreateRequest("FY26", 1767225600, 1798761599, true));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpFyCreateDryRun().Evaluate(new ErpFyCreateRequest("FY26"));
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Valid start and end dates are required", missing.Detail);

        var confirm = new ErpFyCreateDryRun().Evaluate(new ErpFyCreateRequest("FY26", 1767225600, 1798761599, true, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpFiscalYearCreate", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxFyCreate", text, StringComparison.Ordinal);
        Assert.Contains("IErpFyCreateWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleFyCreateAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFyCreateWriteService.cs"));
        Assert.Contains("Fiscal year created", service, StringComparison.Ordinal);
        Assert.Contains("Valid start and end dates are required", service, StringComparison.Ordinal);
        Assert.Contains("Fiscal year table is not provisioned", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
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

        throw new FileNotFoundException("Could not locate " + relative);
    }
}
