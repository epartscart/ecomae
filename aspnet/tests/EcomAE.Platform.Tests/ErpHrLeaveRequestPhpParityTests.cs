using System.Reflection;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards /erp/ajax/hr-leave-request live writes without inventing schema ensure.</summary>
public sealed class ErpHrLeaveRequestPhpParityTests
{
    [Fact]
    public void HrOverviewApp_EmitsLeaveRequestForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpHrOverviewApp.razor"));
        Assert.Contains("/erp/ajax/hr-leave-request", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/hr-leave-status", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Submit leave request", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterLeaveRequestWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpAjaxHrLeaveRequest", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/hr-leave-request", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpHrLeaveRequestWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("IErpHrLeaveRequestWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_KeepsWriteLiveGatedStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var write = catalog.First(item => item.AspNetRouteOrCapability.Contains("/erp/ajax/hr-leave-request", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_hr_leave_request", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Defaults_MatchPhp()
    {
        Assert.Equal("annual", ErpHrLeaveRequestWriteService.NormalizeType(""));
        Assert.Equal("sick", ErpHrLeaveRequestWriteService.NormalizeType("sick"));
        Assert.Equal(0, ErpHrLeaveRequestWriteService.ResolveDateUnix(""));
        Assert.Equal(1_788_480_000, ErpHrLeaveRequestWriteService.ResolveDateUnix("2026-09-04"));
    }

    [Fact]
    public void DryRun_RequiresEmployeeAndRefusesConfirm()
    {
        var ok = new ErpHrLeaveRequestDryRun().Evaluate(new ErpHrLeaveRequestRequest(9, "annual"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(0, ok.Writes);
        var missing = new ErpHrLeaveRequestDryRun().Evaluate(new ErpHrLeaveRequestRequest(0));
        Assert.Equal("invalid_request", missing.ValidationCode);
        var refused = new ErpHrLeaveRequestDryRun().Evaluate(new ErpHrLeaveRequestRequest(9, "annual", true));
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
