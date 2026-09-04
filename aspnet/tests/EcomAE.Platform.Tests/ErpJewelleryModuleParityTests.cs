using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Jewellery company ERP modules must render PHP tabs, forms and columns — not a blank table.</summary>
public sealed class ErpJewelleryModuleParityTests
{
    [Fact]
    public void RepairsApp_UsesPhpTabsFormsAndColumns()
    {
        var text = ReadApp("CpJewelleryRepairsApp.razor");
        Assert.Contains("tab=jw_repairs", text, StringComparison.Ordinal);
        Assert.Contains("repair_status", text, StringComparison.Ordinal);
        Assert.Contains("New Repair Receipt", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryRepairCreateForm", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryRepairStatusForm", text, StringComparison.Ordinal);
        Assert.Contains(ErpJewelleryModuleChrome.RepairJobColumns, c => c == "Repair #");
        Assert.Contains(ErpJewelleryModuleChrome.RepairJobColumns, c => c.Contains("Wt In", StringComparison.Ordinal));
        Assert.Contains("epc_erp_jw_repairs", text, StringComparison.Ordinal);
        Assert.Contains("HasJewelleryStaffAccess", text, StringComparison.Ordinal);
        Assert.Contains("PhpErpDesktopChrome", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/jewellery-repairs-app\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit", text, StringComparison.Ordinal);
        Assert.DoesNotContain("2,458", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MastersApp_UsesKaratGoldRateAndSeedChrome()
    {
        var text = ReadApp("CpJewelleryMastersApp.razor");
        Assert.Contains("jw_karat", text, StringComparison.Ordinal);
        Assert.Contains("gold_rate", text, StringComparison.Ordinal);
        Assert.Contains(ErpJewelleryModuleChrome.MasterTabs, t => t.Key == "jewellery_tag");
        Assert.Equal("Karat Master", ErpJewelleryModuleChrome.MasterSpec("jw_karat").Title);
        Assert.Contains("Seed defaults", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryKaratSaveForm", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryKaratSeedForm", text, StringComparison.Ordinal);
        Assert.Contains(ErpJewelleryModuleChrome.KaratColumns, c => c.Contains("Purity", StringComparison.Ordinal));
        Assert.DoesNotContain("295.50", text, StringComparison.Ordinal);
        Assert.DoesNotContain("2,458", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MastersApp_DoesNotShowPhpDemoGoldRates()
    {
        var text = ReadApp("CpJewelleryMastersApp.razor");
        Assert.DoesNotContain("295.50", text, StringComparison.Ordinal);
        Assert.DoesNotContain("270.88", text, StringComparison.Ordinal);
        Assert.Contains("demo figures", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FixingRetailStockApps_HonorTabAndCreateForms()
    {
        var fixing = ReadApp("CpJewelleryFixingApp.razor");
        Assert.Contains("jw_purchase_fixing", fixing, StringComparison.Ordinal);
        Assert.Contains("New Fixing", fixing, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryModuleSaveForm", fixing, StringComparison.Ordinal);
        Assert.Contains("Fixed Rate", fixing, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", fixing, StringComparison.Ordinal);

        var retail = ReadApp("CpJewelleryRetailApp.razor");
        Assert.Contains("jw_retail_sales", retail, StringComparison.Ordinal);
        Assert.Contains("New Invoice", retail, StringComparison.Ordinal);
        Assert.Equal("Retail Sales (POS)", ErpJewelleryModuleChrome.RetailSpec("jw_retail_sales").Title);
        Assert.DoesNotContain("@onclick", retail, StringComparison.Ordinal);

        var stock = ReadApp("CpJewelleryStockVerificationApp.razor");
        Assert.Contains("jw_stock_verification", stock, StringComparison.Ordinal);
        Assert.Contains(ErpJewelleryModuleChrome.StockTabs, t => t.Key == "jw_metal_stock");
        Assert.Contains("New Count", stock, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", stock, StringComparison.Ordinal);
    }

    [Fact]
    public void ChromeCatalog_ListsPhpJewelleryTabs()
    {
        Assert.Contains(ErpJewelleryModuleChrome.RepairTabs, t => t.Key == "jw_repairs");
        Assert.Contains(ErpJewelleryModuleChrome.RepairTabs, t => t.Key == "jw_repair_receipt");
        Assert.Contains(ErpJewelleryModuleChrome.MasterTabs, t => t.Key == "jw_karat");
        Assert.Contains(ErpJewelleryModuleChrome.MasterTabs, t => t.Key == "gold_rate");
        Assert.Contains(ErpJewelleryModuleChrome.FixingTabs, t => t.Key == "jw_purchase_fixing");
        Assert.Contains(ErpJewelleryModuleChrome.RetailTabs, t => t.Key == "jw_retail_sales");
        Assert.Contains(ErpJewelleryModuleChrome.StockTabs, t => t.Key == "jw_metal_stock");
        Assert.Equal("jw_repairs", ErpJewelleryModuleChrome.NormalizeTab("", ErpJewelleryModuleChrome.RepairTabs, "jw_repairs"));
        Assert.Equal("jw_repair_delivery", ErpJewelleryModuleChrome.NormalizeTab("jw_repair_delivery", ErpJewelleryModuleChrome.RepairTabs, "jw_repairs"));
        Assert.Contains("/cp/jewellery-repairs-app?company=2&tab=jw_repairs", ErpJewelleryModuleChrome.AppHref("/cp/jewellery-repairs-app", 2, ("tab", "jw_repairs")), StringComparison.Ordinal);
    }

    [Fact]
    public void IndustryNav_HidesJewelleryTabsOnMainCompany()
    {
        var jw = new ErpCompanyDigest(2, "JW", "Jewellery Division", "AED", "AE", "jewellery_diamond", true);
        var main = new ErpCompanyDigest(1, "MAIN", "Main", "AED", "AE", "", true);
        Assert.True(ErpIndustryNav.IsJewelleryCompany(jw));
        Assert.False(ErpIndustryNav.IsJewelleryCompany(main));
        Assert.True(ErpIndustryNav.IsJewelleryFromHostOrPack("jewellery", null, null));
        Assert.False(ErpIndustryNav.IsJewelleryFromHostOrPack("auto_parts", null, main));
    }

    [Fact]
    public void RepairSql_ReadsBothPhpSchemas()
    {
        Assert.Contains("epc_jewel_repair", LegacySurfaceDashboardSql.SelectCpJewelleryRepairs, StringComparison.Ordinal);
        Assert.Contains("epc_erp_jw_repairs", LegacySurfaceDashboardSql.SelectCpJewelleryIntegrationRepairs, StringComparison.Ordinal);
        Assert.Contains("customer_phone", LegacySurfaceDashboardSql.SelectCpJewelleryIntegrationRepairs, StringComparison.Ordinal);
        Assert.Contains("gross_wt_in", LegacySurfaceDashboardSql.SelectCpJewelleryIntegrationRepairs, StringComparison.Ordinal);
        Assert.DoesNotContain("`email`", LegacySurfaceDashboardSql.SelectCpJewelleryRepairs, StringComparison.Ordinal);
        Assert.DoesNotContain("workshop_notes", LegacySurfaceDashboardSql.SelectCpJewelleryIntegrationRepairs, StringComparison.Ordinal);
    }

    [Fact]
    public void JewelleryFormRoutes_AreDedicatedHtmlPosts()
    {
        Assert.Equal("/erp/jewellery/repair-create", EcomAeRoutes.ErpJewelleryRepairCreateForm);
        Assert.Equal("/erp/jewellery/repair-status", EcomAeRoutes.ErpJewelleryRepairStatusForm);
        Assert.Equal("/erp/jewellery/karat-save", EcomAeRoutes.ErpJewelleryKaratSaveForm);
        Assert.Equal("/erp/jewellery/karat-seed", EcomAeRoutes.ErpJewelleryKaratSeedForm);
        Assert.Equal("/erp/jewellery/module-save", EcomAeRoutes.ErpJewelleryModuleSaveForm);
    }

    [Fact]
    public void TabRouteMap_JewelleryTabsKeepQuery()
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab("jw_repairs", out var repairs));
        Assert.Contains("tab=jw_repairs", repairs, StringComparison.Ordinal);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("jw_karat", out var karat));
        Assert.Contains("tab=jw_karat", karat, StringComparison.Ordinal);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("gold_rate", out var gold));
        Assert.Contains("jewellery-masters-app", gold, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleSaveDryRun_ValidatesWithoutWrites()
    {
        var dry = new ErpJwModuleSaveDryRun();
        var ok = dry.Evaluate(new ErpJwModuleSaveRequest("jw_karat_save", "22", false));
        Assert.Equal("ok", ok.ValidationCode);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);

        var missing = dry.Evaluate(new ErpJwModuleSaveRequest("", "22", false));
        Assert.Equal("invalid_request", missing.ValidationCode);

        var refused = dry.Evaluate(new ErpJwModuleSaveRequest("jw_karat_save", "22", true));
        Assert.Equal("confirm_writes_refused", refused.ValidationCode);
    }

    private static string ReadApp(string fileName)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages", fileName);
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
                return dir.FullName;
            dir = dir.Parent;
        }

        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
