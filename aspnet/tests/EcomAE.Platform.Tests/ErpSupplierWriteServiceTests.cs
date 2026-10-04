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
}
