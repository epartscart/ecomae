using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_coll_case_promise</c> twin: SSR form, DI, catalog.
/// Schema ensure stays PHP.
/// </summary>
public sealed class ErpCollectionsCasePromisePhpParityTests
{
    [Fact]
    public void CollectionsDunningApp_PostsNativePromiseForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCollectionsDunningApp.razor"));
        Assert.Contains("action=\"/erp/collections/cases/promise\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"promise_date\"", text, StringComparison.Ordinal);
        Assert.Contains("Record promise", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Promise to pay, activity log", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPromiseWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpCollectionsCasePromiseWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpCollectionsCasePromiseWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPromiseLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/collections/cases/promise");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_coll_case_promise", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/coll-case-promise");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpCollCasePromiseDryRun().Evaluate(new ErpCollCasePromiseRequest(3));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var missing = new ErpCollCasePromiseDryRun().Evaluate(new ErpCollCasePromiseRequest(0));
        Assert.Equal("invalid_request", missing.ValidationCode);

        var confirm = new ErpCollCasePromiseDryRun().Evaluate(new ErpCollCasePromiseRequest(3, ConfirmWrites: true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpCollectionsCasePromise", text, StringComparison.Ordinal);
        Assert.Contains("IErpCollectionsCasePromiseWriteService", text, StringComparison.Ordinal);
        Assert.Contains("promise_date", text, StringComparison.Ordinal);
        Assert.Contains(
            "Promise to pay recorded",
            File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpCollectionsCasePromiseWriteService.cs")),
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
