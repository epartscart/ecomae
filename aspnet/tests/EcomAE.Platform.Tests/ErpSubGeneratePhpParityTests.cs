using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_sub_generate_invoice</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpSubGeneratePhpParityTests
{
    [Fact]
    public void SalesOrdersApp_PostsNativeGenerateForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor"));
        Assert.Contains("action=\"/erp/subscriptions/generate\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("Generate cycle invoice", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Cycle invoice generate stays on the Classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersGenerateWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpSubGenerateWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpSubGenerateWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksGenerateLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/subscriptions/generate");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_sub_generate_invoice", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/sub-generate");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpSubGenerateDryRun().Evaluate(new ErpSubGenerateRequest(9));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpSubGenerateDryRun().Evaluate(new ErpSubGenerateRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Subscription not found", missing.Detail);

        var confirm = new ErpSubGenerateDryRun().Evaluate(new ErpSubGenerateRequest(9, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void CycleAndMessage_MatchPhp()
    {
        Assert.Equal(1, ErpSubGenerateWriteService.CycleMonths("monthly"));
        Assert.Equal(3, ErpSubGenerateWriteService.CycleMonths("quarterly"));
        Assert.Equal(12, ErpSubGenerateWriteService.CycleMonths("annual"));
        Assert.Equal(1, ErpSubGenerateWriteService.CycleMonths("Monthly"));
        Assert.Equal(1_791_072_000, ErpSubGenerateWriteService.AddMonthsUnix(1_788_480_000, 1));
        Assert.Equal("Cycle invoice #3 generated — 1,250.50 AED", ErpSubGenerateWriteService.FormatGeneratedMessage(3, 1250.50m));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpSubscriptionsGenerate", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxSubGenerate", text, StringComparison.Ordinal);
        Assert.Contains("IErpSubGenerateWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleSubGenerateAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpSubGenerateWriteService.cs"));
        Assert.Contains("Cycle invoice #", service, StringComparison.Ordinal);
        Assert.Contains("Subscription invoice table is not provisioned", service, StringComparison.Ordinal);
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
