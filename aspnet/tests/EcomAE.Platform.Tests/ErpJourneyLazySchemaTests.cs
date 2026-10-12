using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJourneyLazySchemaTests
{
    [Fact]
    public void AmlKycAndAlertEnsureSchemaBeforeFailClosedGuards()
    {
        var kyc = Read("aspnet/src/EcomAE.Platform/Erp/ErpAmlKycSaveWriteService.cs");
        Assert.Contains("ErpAmlShared.EnsureSchemaAsync", kyc, StringComparison.Ordinal);
        var alert = Read("aspnet/src/EcomAE.Platform/Erp/ErpAmlAlertStatusWriteService.cs");
        Assert.Contains("ErpAmlShared.EnsureSchemaAsync", alert, StringComparison.Ordinal);
        var reporter = Read("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs");
        Assert.Contains("ErpAmlShared.EnsureSchemaAsync", reporter, StringComparison.Ordinal);
        Assert.Contains("ErpLazySchema.EnsureFinAdvancedAsync", reporter, StringComparison.Ordinal);
    }

    [Fact]
    public void FinAdvancedVwhDimAndOplWritersCallPhpTwinnedEnsure()
    {
        Assert.Contains("EnsureFinAdvancedAsync", Read("aspnet/src/EcomAE.Platform/Erp/ErpFinPeriodsGenerateWriteService.cs"), StringComparison.Ordinal);
        Assert.Contains("EnsureFinAdvancedAsync", Read("aspnet/src/EcomAE.Platform/Erp/ErpFinAllocSaveWriteService.cs"), StringComparison.Ordinal);
        Assert.Contains("EnsureFinAdvancedAsync", Read("aspnet/src/EcomAE.Platform/Erp/ErpFinAllocRunWriteService.cs"), StringComparison.Ordinal);
        Assert.Contains("EnsureFinAdvancedAsync", Read("aspnet/src/EcomAE.Platform/Erp/ErpFinAccrualSaveWriteService.cs"), StringComparison.Ordinal);
        Assert.Contains("EnsureFinAdvancedAsync", Read("aspnet/src/EcomAE.Platform/Erp/ErpFinFxRevalueWriteService.cs"), StringComparison.Ordinal);
        Assert.Contains("EnsureDimLinksAsync", Read("aspnet/src/EcomAE.Platform/Erp/ErpDimensionWriteService.cs"), StringComparison.Ordinal);
        Assert.Contains("EnsureVirtualWarehouseAsync", Read("aspnet/src/EcomAE.Platform/Erp/ErpVirtualWarehouseWriteService.cs"), StringComparison.Ordinal);
        Assert.Contains("EnsureOrderPlanningAsync", Read("aspnet/src/EcomAE.Platform/Erp/ErpOplParamsSaveWriteService.cs"), StringComparison.Ordinal);
        Assert.Contains("EnsureOrderPlanningAsync", Read("aspnet/src/EcomAE.Platform/Erp/ErpOplSetStatusWriteService.cs"), StringComparison.Ordinal);
        var lazy = Read("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs");
        Assert.Contains("epc_fin_adv_ensure_schema", lazy, StringComparison.Ordinal);
        Assert.Contains("epc_erp_dim_ensure_schema", lazy, StringComparison.Ordinal);
        Assert.Contains("epc_vwh_ensure_schema", lazy, StringComparison.Ordinal);
        Assert.Contains("epc_opl_ensure_schema", lazy, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontCatalogueSearchAndProductTitlesUseHostBrand()
    {
        Assert.Contains("ResolveBrandLabel", Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"), StringComparison.Ordinal);
        Assert.DoesNotContain("· eParts Cart", Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"), StringComparison.Ordinal);
        Assert.Contains("ResolveBrandLabel", Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontProductApp.razor"), StringComparison.Ordinal);
        Assert.DoesNotContain("· eParts Cart", Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontProductApp.razor"), StringComparison.Ordinal);
        Assert.DoesNotContain("ecomae.com", Read("robots.txt"), StringComparison.Ordinal);
    }

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir!.FullName, relative));
    }
}
