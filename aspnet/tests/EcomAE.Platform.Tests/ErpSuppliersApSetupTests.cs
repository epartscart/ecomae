using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpSuppliersApSetupTests
{
    [Fact]
    public void WorkspacePostsVendorAndApSetupMasters()
    {
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpSuppliersApp.razor"));
        Assert.Contains("Add vendor", page, StringComparison.Ordinal);
        Assert.Contains("Save vendor", page, StringComparison.Ordinal);
        Assert.Contains("action=\"@CreateAction\"", page, StringComparison.Ordinal);
        Assert.Contains("action=\"@SyncAction\"", page, StringComparison.Ordinal);
        Assert.Contains("action=\"@PmSaveAction\"", page, StringComparison.Ordinal);
        Assert.Contains("epc_erp_pm_pay_methods", page, StringComparison.Ordinal);
        Assert.Contains("epc_erp_pm_pay_terms", page, StringComparison.Ordinal);
        Assert.Contains("epc_erp_pm_vendor_groups", page, StringComparison.Ordinal);
        Assert.Contains("New method of payment", page, StringComparison.Ordinal);
        Assert.Contains("New terms of payment", page, StringComparison.Ordinal);
        Assert.Contains("New vendor group", page, StringComparison.Ordinal);
        Assert.Contains("Create vendors from warehouses", page, StringComparison.Ordinal);
        Assert.Contains("IErpApSetupReadService", page, StringComparison.Ordinal);
        Assert.Contains("IErpSupplierOnboardReadService", page, StringComparison.Ordinal);
        Assert.Contains("name=\"supplier_code\"", page, StringComparison.Ordinal);
        Assert.Contains("FieldName=\"ap_coa_id\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"supplier_group_type\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"supplier_category\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"owner_name\"", page, StringComparison.Ordinal);
        Assert.Contains("Approve supplier", page, StringComparison.Ordinal);
        Assert.Contains("trade_license", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Writes are not available", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", page, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", page, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateAndSyncHandlersRedirectHtmlPosters()
    {
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("ExecuteErpWriteAsync(context, session, \"/erp/suppliers-app?tab=ap_setup\"", module, StringComparison.Ordinal);
        Assert.Contains("MapPost(EcomAeRoutes.ErpSuppliersCreate, HandleSupplierCreateAsync)", module, StringComparison.Ordinal);
        Assert.Contains("MapPost(EcomAeRoutes.ErpSuppliersSync, HandleSupplierSyncAsync)", module, StringComparison.Ordinal);
        Assert.Contains("MapPost(EcomAeRoutes.ErpPmSave, HandlePmSaveAsync)", module, StringComparison.Ordinal);
        Assert.Contains("MapPost(EcomAeRoutes.ErpSuppliersApprove, HandleSupplierApproveAsync)", module, StringComparison.Ordinal);
        Assert.Contains("RequireApprovedForPurchaseAsync", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPurchaseInvoiceWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("RequireApprovedForPurchaseAsync", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPurchaseOrderWriteService.cs")), StringComparison.Ordinal);

        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpApSetupReadService", program, StringComparison.Ordinal);
        Assert.Contains("IErpSupplierOnboardReadService", program, StringComparison.Ordinal);
    }

    [Fact]
    public void RoutesAndCatalogPointAtApSetup()
    {
        Assert.Equal("/erp/suppliers-app", EcomAeRoutes.ErpSuppliersApp);
        Assert.Equal("/erp/suppliers/create", EcomAeRoutes.ErpSuppliersCreate);
        Assert.Equal("/erp/suppliers/sync", EcomAeRoutes.ErpSuppliersSync);
        Assert.Equal("/erp/pm/save", EcomAeRoutes.ErpPmSave);
        Assert.Equal("/erp/suppliers/approve", EcomAeRoutes.ErpSuppliersApprove);
        Assert.Equal("/erp/suppliers-app?tab=ap_setup", ErpPhpTabRouteMap.All["ap_setup"]);
        Assert.Equal("/erp/suppliers-app", ErpPhpTabRouteMap.All["vendors"]);

        var catalog = SurfacePayloadContractCatalog.Functions.First(r => r.AspNetRouteOrCapability == "/erp/suppliers-app");
        Assert.Equal("write-live-gated", catalog.Status);
        Assert.Contains("/erp/suppliers/create", catalog.Notes, StringComparison.Ordinal);
        Assert.Contains("/erp/pm/save", catalog.Notes, StringComparison.Ordinal);
        Assert.Contains("/erp/suppliers/approve", catalog.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void ApSetupReaderCoversPhpMasterTables()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpApSetupReadService.cs"));
        Assert.Contains("epc_erp_pm_pay_methods", src, StringComparison.Ordinal);
        Assert.Contains("epc_erp_pm_pay_terms", src, StringComparison.Ordinal);
        Assert.Contains("epc_erp_pm_vendor_groups", src, StringComparison.Ordinal);
        Assert.Contains("EnsurePmMastersAsync", src, StringComparison.Ordinal);
        Assert.True(ErpPmSaveWriteService.Registry.ContainsKey("epc_erp_pm_pay_methods"));
        Assert.True(ErpPmSaveWriteService.Registry.ContainsKey("epc_erp_pm_pay_terms"));
        Assert.True(ErpPmSaveWriteService.Registry.ContainsKey("epc_erp_pm_vendor_groups"));

        var schema = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs"));
        Assert.Contains("EnsureSuppliersAsync", schema, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_erp_suppliers`", schema, StringComparison.Ordinal);
        Assert.Contains("ROW_FORMAT=DYNAMIC", schema, StringComparison.Ordinal);
        Assert.Contains("`city` varchar(128)", schema, StringComparison.Ordinal);
        Assert.Contains("`notes` varchar(1000)", schema, StringComparison.Ordinal);
        Assert.Contains("approval_status", schema, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_erp_supplier_owners`", schema, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, relative);
    }
}
