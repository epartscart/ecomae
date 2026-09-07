using System.Reflection;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards /erp/workflow/create live writes without inventing process-flow cases.</summary>
public sealed class ErpWorkflowCreatePhpParityTests
{
    [Fact]
    public void ErpWorkflowApp_EmitsCreateForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpWorkflowApp.razor"));
        Assert.Contains("/erp/workflow/create", text, StringComparison.Ordinal);
        Assert.Contains("/erp/workflow/status", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Create task", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterWorkflowCreateWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpWorkflowCreate", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/workflow/create", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpWorkflowCreateWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("IErpWorkflowCreateWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_BlocksUntilConfirmWrites()
    {
        var dry = new ErpWorkflowCreateDryRun();
        var blocked = dry.Evaluate(new ErpWorkflowCreateRequest("Pick parts", "warehouse", "high", 9));
        Assert.Equal("dry-run-validated", blocked.Status);
        Assert.Equal(0, blocked.Writes);
        Assert.True(blocked.WritesBlocked);
        Assert.False(blocked.PhpAuthoritative);
        var missing = dry.Evaluate(new ErpWorkflowCreateRequest(""));
        Assert.Equal("title_required", missing.ValidationCode);
        var refused = dry.Evaluate(new ErpWorkflowCreateRequest("Pick parts", ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", refused.Status);
    }

    [Fact]
    public void Catalog_KeepsWriteLiveGatedStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var write = catalog.First(item => item.AspNetRouteOrCapability.Contains("/erp/workflow/create", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("epc_erp_workflow_create", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void DueDefault_IsThreeDays()
    {
        const long now = 1_788_547_200;
        Assert.Equal(now + 86400L * 3, ErpWorkflowCreateWriteService.ResolveDueUnix("", now));
        Assert.Equal(1_788_480_000, ErpWorkflowCreateWriteService.ResolveDueUnix("2026-09-04", now));
        Assert.Contains("high", ErpWorkflowCreateWriteService.AllowedPriorities);
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
