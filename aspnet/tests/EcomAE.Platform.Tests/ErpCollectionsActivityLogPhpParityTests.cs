using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_coll_activity_log</c> twin: SSR form, DI, catalog.
/// Schema ensure stays PHP.
/// </summary>
public sealed class ErpCollectionsActivityLogPhpParityTests
{
    [Fact]
    public void CollectionsDunningApp_PostsNativeActivityForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCollectionsDunningApp.razor"));
        Assert.Contains("action=\"/erp/collections/activity/log\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"case_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"outcome\"", text, StringComparison.Ordinal);
        Assert.Contains("Log activity", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Activity log, dunning run", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersActivityLogWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpCollectionsActivityLogWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpCollectionsActivityLogWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksActivityLogLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/collections/activity/log");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_coll_activity_log", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/coll-activity-log");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpCollActivityLogDryRun().Evaluate(new ErpCollActivityLogRequest(3));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var missing = new ErpCollActivityLogDryRun().Evaluate(new ErpCollActivityLogRequest(0));
        Assert.Equal("invalid_request", missing.ValidationCode);

        var confirm = new ErpCollActivityLogDryRun().Evaluate(new ErpCollActivityLogRequest(3, ConfirmWrites: true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpCollectionsActivityLog", text, StringComparison.Ordinal);
        Assert.Contains("IErpCollectionsActivityLogWriteService", text, StringComparison.Ordinal);
        Assert.Contains("follow_up_date", text, StringComparison.Ordinal);
        Assert.Contains(
            "Activity logged",
            File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpCollectionsActivityLogWriteService.cs")),
            StringComparison.Ordinal);
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
