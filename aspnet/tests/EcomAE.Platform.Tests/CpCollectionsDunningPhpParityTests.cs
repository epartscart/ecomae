using System.Reflection;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards /cp/collections-dunning-app live queue writes without inventing letter/process twins.
/// </summary>
public sealed class CpCollectionsDunningPhpParityTests
{
    [Fact]
    public void CpCollectionsDunningApp_EmitsQueueWriteForms()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCollectionsDunningApp.razor"));
        Assert.Contains("/cp/collections-dunning/write", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("action\" value=\"update_status\"", text, StringComparison.Ordinal);
        Assert.Contains("action\" value=\"record_payment\"", text, StringComparison.Ordinal);
        Assert.Contains("/erp/collections/cases/status", text, StringComparison.Ordinal);
        Assert.Contains("AmountDue", text, StringComparison.Ordinal);
        Assert.Contains("PhpSurfaceLinkMap.PhpReferenceOnlyHref", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:void(0)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterDunningWrite()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("CpCollectionsDunningWrite", routes, StringComparison.Ordinal);
        Assert.Contains("/cp/collections-dunning/write", routes, StringComparison.Ordinal);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpCollectionsDunningWriteService", program, StringComparison.Ordinal);
        Assert.Contains("ICpCollectionsDunningWriteDryRun", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpCollectionsDunningWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_BlocksUntilConfirmWrites()
    {
        var dry = new CpCollectionsDunningWriteDryRun();
        var blocked = dry.Evaluate(new CpCollectionsDunningWriteRequest("update_status", false));
        Assert.Equal("dry-run-validated", blocked.Status);
        Assert.Equal(0, blocked.Writes);
        Assert.True(blocked.WritesBlocked);
        Assert.False(blocked.PhpAuthoritative);
        var refused = dry.Evaluate(new CpCollectionsDunningWriteRequest("update_status", true));
        Assert.Equal("dry-run-confirm-refused", refused.Status);
    }

    [Fact]
    public void Catalog_KeepsDigestWiredStatus()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var shell = catalog.First(item => item.AspNetRouteOrCapability.Contains("/cp/collections-dunning-app", StringComparison.Ordinal));
        Assert.Equal("digest-wired-awaiting-dual-sample", shell.Status);
        Assert.Contains("/cp/collections-dunning/write", shell.Notes, StringComparison.Ordinal);
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
