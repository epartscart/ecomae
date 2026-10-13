using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPurchaseRfqCompleteTests
{
    [Fact]
    public void ProcurementSchema_CreatesReqCategoryPolicyAndLines()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpProcurementSchema.cs"));
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_proc_category`", text, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_proc_policy`", text, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_proc_req`", text, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_proc_req_line`", text, StringComparison.Ordinal);
        Assert.Contains("company_id", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RfqSchema_CreatesErpAndScmTables()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpRfqSchema.cs"));
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_erp_rfq`", text, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_scm_rfq`", text, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_scm_rfq_lines`", text, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_scm_rfq_responses`", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfirmedWrites_CallNativeSchemaEnsure()
    {
        var save = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpProcurementReqSaveWriteService.cs"));
        var add = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpProcurementReqAddLineWriteService.cs"));
        var lifecycle = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpProcurementReqWriteService.cs"));
        var rfq = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpRfqWriteService.cs"));
        Assert.Contains("ErpProcurementSchema.EnsureSchemaAsync", save, StringComparison.Ordinal);
        Assert.Contains("ErpProcurementSchema.EnsureSchemaAsync", add, StringComparison.Ordinal);
        Assert.Contains("ErpProcurementSchema.EnsureSchemaAsync", lifecycle, StringComparison.Ordinal);
        Assert.Contains("ErpRfqSchema.EnsureSchemaAsync", rfq, StringComparison.Ordinal);
        Assert.DoesNotContain("Schema ensure stays PHP", save, StringComparison.Ordinal);
    }

    [Fact]
    public void PurchaseRequestsApp_IsPresentableAndKeepsCompanyScopedWrites()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpPurchaseRequestsApp.razor"));
        Assert.Contains("epc-pr-create", text, StringComparison.Ordinal);
        Assert.Contains("epc-pr-empty", text, StringComparison.Ordinal);
        Assert.Contains("Save requisition", text, StringComparison.Ordinal);
        Assert.Contains("name=\"company_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"csrf_guard_key\"", text, StringComparison.Ordinal);
        Assert.Contains("ClientErpPath.PrefixIfBound", text, StringComparison.Ordinal);
        Assert.Contains("ClientErpPathRewriteMiddleware.OriginalPathItem", text, StringComparison.Ordinal);
        Assert.Contains("/erp/procurement/requisitions/save", text, StringComparison.Ordinal);
        Assert.Contains("Create requisition", text, StringComparison.Ordinal);
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(
            "/cp/client-erp/indj/erp/purchase-requests-app", out _));
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("classic", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RfqApp_HasListCreateFormAndHidesMissingTableErrors()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpRfqApp.razor"));
        Assert.Contains("epc-rfq-create", text, StringComparison.Ordinal);
        Assert.Contains("epc-rfq-empty", text, StringComparison.Ordinal);
        Assert.Contains("id=\"erp-rfq-new\"", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/save-rfq", text, StringComparison.Ordinal);
        Assert.Contains("Save RFQ", text, StringComparison.Ordinal);
        Assert.Contains("name=\"csrf_guard_key\"", text, StringComparison.Ordinal);
        Assert.Contains("ClientErpPath.PrefixIfBound", text, StringComparison.Ordinal);
        Assert.Contains("IsSchemaGap", text, StringComparison.Ordinal);
        Assert.Contains("doesn't exist", text, StringComparison.Ordinal);
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(
            "/cp/client-erp/indj/erp/rfq-app", out _));
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("classic", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("linear-gradient(135deg", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Digest_DoesNotFailThePageWhenTablesAreMissing()
    {
        var reporter = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("TenantTableExistsAsync(connection, \"epc_proc_req\"", reporter, StringComparison.Ordinal);
        Assert.Contains("TenantTableExistsAsync(connection, \"epc_erp_rfq\"", reporter, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksNativeEnsureOnProcurementAndRfq()
    {
        var save = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/procurement/requisitions/save");
        Assert.Contains("ErpProcurementSchema", save.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("schema remains PHP-managed", save.Notes, StringComparison.Ordinal);

        var rfq = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/rfq-app");
        Assert.Contains("ErpRfqSchema", rfq.Notes, StringComparison.Ordinal);
        Assert.Contains("list create", rfq.Notes, StringComparison.Ordinal);
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
