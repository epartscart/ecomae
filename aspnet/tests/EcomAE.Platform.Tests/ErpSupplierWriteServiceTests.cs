using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpSupplierWriteServiceTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("yes", false)]
    public void VatRegisteredFollowsPhpTruthiness(string? raw, bool expected) =>
        Assert.Equal(expected, ErpSupplierWriteService.VatRegistered(raw));

    [Theory]
    [InlineData(null, "no")]
    [InlineData("invoice", "invoice")]
    [InlineData(" payment ", "payment")]
    [InlineData("all", "all")]
    [InlineData("bogus", "no")]
    public void OnHoldIsWhitelisted(string? raw, string expected) =>
        Assert.Equal(expected, ErpSupplierWriteService.OnHold(raw));

    private static string Src(string rel)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, "aspnet/src/EcomAE.Platform", rel));
    }

    [Fact]
    public void ServiceNeverProvisionsSchema()
    {
        var src = Src("Erp/ErpSupplierWriteService.cs");
        Assert.DoesNotContain("CREATE TABLE", src, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", src, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INSERT INTO `epc_erp_suppliers`", src, StringComparison.Ordinal);
    }

    [Fact]
    public void RoutesUseLiveHandlersAndVendorPurchaseDimensions()
    {
        var module = Src("Modules/ErpModule.cs");
        Assert.Contains("MapPost(EcomAeRoutes.ErpSuppliersCreate, HandleSupplierCreateAsync)", module, StringComparison.Ordinal);
        Assert.Contains("MapPost(EcomAeRoutes.ErpPurchasesCreate, HandlePurchaseCreateAsync)", module, StringComparison.Ordinal);
        Assert.Contains("dimensions.SaveAsync(\"vendor\", id, dim", module, StringComparison.Ordinal);
        Assert.Contains("dimensions.SaveAsync(\"purchase\", created.PurchaseId, dim", module, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncFromStoragesMirrorsPhpNotExistsInsert()
    {
        var src = Src("Erp/ErpSupplierWriteService.cs");
        Assert.Contains("SELECT `id`, `name`, `short_name` FROM `shop_storages`", src);
        Assert.Contains("WHERE NOT EXISTS (SELECT 1 FROM `epc_erp_suppliers` WHERE `storage_id` = ? AND `active` = 1)", src);
        var module = Src("Modules/ErpModule.cs");
        Assert.Contains("writes.SyncFromStoragesAsync(cancellationToken)", module);
        Assert.Contains("\"Synced \" + n + \" supplier(s) from warehouses\"", module);
    }

    [Fact]
    public void GlSyncUnpostedMirrorsPhpSubledgerSweep()
    {
        var src = Src("Erp/ErpGlSyncUnpostedWriteService.cs");
        Assert.Contains("SELECT `id` FROM `epc_erp_purchases` WHERE `active` = 1 AND `gl_journal_id` = 0", src);
        Assert.Contains("SELECT `id` FROM `epc_erp_cash_bank_entries` WHERE `active` = 1 AND `gl_journal_id` = 0", src);
        Assert.Contains("_gl.PostPurchaseAsync(c, id, adminId", src);
        Assert.Contains("_gl.PostCashEntryAsync(c, id, adminId", src);
        Assert.DoesNotContain("CREATE TABLE", src, StringComparison.OrdinalIgnoreCase);
        var module = Src("Modules/ErpModule.cs");
        Assert.Contains("MapPost(EcomAeRoutes.ErpGlSyncUnposted, HandleGlSyncUnpostedAsync)", module);
        Assert.Contains("\"Synced \" + n + \" sub-ledger entry(ies) to GL\"", module);
    }

    [Fact]
    public void GlPostSalesMirrorsPhpSalesRecognition()
    {
        var src = Src("Erp/ErpGlPostSalesWriteService.cs");
        Assert.Contains("WHERE `source_type` = 'sales' AND `source_id` = ? AND `active` = 1 LIMIT 1", src);
        Assert.Contains("Reference = \"ORD-\" + id", src);
        Assert.Contains("\"Sales recognition order #\" + id", src);
        Assert.Contains("LegislationRef = \"vat-decree-8-2017\"", src);
        Assert.DoesNotContain("CREATE TABLE", src, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("exempt", ErpGlPostSalesWriteService.SalesTreatment(new ErpDashboardReadService.TenantVat("AE", false, true, 5m), "AE", false));
        Assert.Equal("export", ErpGlPostSalesWriteService.SalesTreatment(new ErpDashboardReadService.TenantVat("AE", true, true, 5m), "SA", false));
        Assert.Equal("standard", ErpGlPostSalesWriteService.SalesTreatment(new ErpDashboardReadService.TenantVat("AE", true, true, 5m), "AE", false));
        var module = Src("Modules/ErpModule.cs");
        Assert.Contains("MapPost(EcomAeRoutes.ErpGlPostSales, HandleGlPostSalesAsync)", module);
        Assert.Contains("\"Posted \" + n + \" sales journal(s) to GL\"", module);
    }
}
