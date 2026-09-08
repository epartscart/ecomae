using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_prj_log_time</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpPrjLogTimePhpParityTests
{
    [Fact]
    public void ProjectsOverviewApp_PostsNativeLogTimeForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpProjectsOverviewApp.razor"));
        Assert.Contains("action=\"/erp/projects/timesheets/log\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"project_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"hours\"", text, StringComparison.Ordinal);
        Assert.Contains("Log time", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPrjLogTimeWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPrjLogTimeWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpPrjLogTimeWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPrjLogTimeLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/projects/timesheets/log");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_prj_log_time", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/prj-log-time");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_RequiresProjectAndRefusesConfirm()
    {
        var ok = new ErpPrjLogTimeDryRun().Evaluate(new ErpPrjLogTimeRequest(4, 1, 2.5m));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpPrjLogTimeDryRun().Evaluate(new ErpPrjLogTimeRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Select a project", missing.Detail);

        var confirm = new ErpPrjLogTimeDryRun().Evaluate(new ErpPrjLogTimeRequest(4, 1, 2.5m, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpProjectsTimesheetsLog", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxPrjLogTime", text, StringComparison.Ordinal);
        Assert.Contains("IErpPrjLogTimeWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandlePrjLogTimeAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPrjLogTimeWriteService.cs"));
        Assert.Contains("Time logged", service, StringComparison.Ordinal);
        Assert.Contains("Project timesheet table is not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("Select a project", service, StringComparison.Ordinal);
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
