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
        Assert.Contains("ErpJewelleryRepairReceiptSaveForm", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryRepairTransferSaveForm", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryWorkshopReceiveSaveForm", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryRepairDeliverySaveForm", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryRepairStatusForm", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Create repair", text, StringComparison.Ordinal);
        Assert.Contains("Save repair receipt", text, StringComparison.Ordinal);
        Assert.Contains("Save transfer", text, StringComparison.Ordinal);
        Assert.Contains("Save workshop receive", text, StringComparison.Ordinal);
        Assert.Contains("Save delivery", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryVoucherSaveForm", text, StringComparison.Ordinal);
        Assert.Contains("Save repair sale", text, StringComparison.Ordinal);
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
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("Save karat", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryRateTypeSaveForm", text, StringComparison.Ordinal);
        Assert.Contains("Save rate type", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryCurrencySaveForm", text, StringComparison.Ordinal);
        Assert.Contains("Save currency", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryDiamondSaveForm", text, StringComparison.Ordinal);
        Assert.Contains("Save diamond", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryDesignSaveForm", text, StringComparison.Ordinal);
        Assert.Contains("Save design", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryPearlSaveForm", text, StringComparison.Ordinal);
        Assert.Contains("Save pearl", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryColorStoneSaveForm", text, StringComparison.Ordinal);
        Assert.Contains("Save color stone", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryBarcodeGenerateForm", text, StringComparison.Ordinal);
        Assert.Contains("Generate barcode", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryTagCreateForm", text, StringComparison.Ordinal);
        Assert.Contains("Create tag", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryTagSellForm", text, StringComparison.Ordinal);
        Assert.Contains("Sell tag", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryGoldSchemeCreateForm", text, StringComparison.Ordinal);
        Assert.Contains("Create scheme", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryGoldSchemeEnrollForm", text, StringComparison.Ordinal);
        Assert.Contains("Enroll customer", text, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryGoldSchemePayForm", text, StringComparison.Ordinal);
        Assert.Contains("Pay instalment", text, StringComparison.Ordinal);
        Assert.Contains("name=\"rfid\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"certificate_no_1\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"setting_charge\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"price2_fc\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"exclude_gst_metal\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"luster\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"finish\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"certificate_no\"", text, StringComparison.Ordinal);
        Assert.Contains(ErpJewelleryModuleChrome.KaratColumns, c => c.Contains("Purity", StringComparison.Ordinal));
        Assert.Equal(
            ["No.", "Item Code", "RFID", "Design", "Description", "Color", "Clarity", "Gr.Wt", "Cost", "Price 1"],
            ErpJewelleryModuleChrome.MasterSpec("jw_diamond").Columns);
        Assert.Equal(
            ["No.", "Item Code", "Description", "Type", "Shape", "Color", "Size", "Grade", "Cost"],
            ErpJewelleryModuleChrome.MasterSpec("jw_pearl").Columns);
        Assert.Equal(
            ["No.", "Item Code", "Description", "Stone Type", "Shape", "Color", "Carat", "Cost"],
            ErpJewelleryModuleChrome.MasterSpec("jw_color_stone").Columns);
        Assert.DoesNotContain("295.50", text, StringComparison.Ordinal);
        Assert.DoesNotContain("2,458", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TagSell_IsCompanyScopedAndOnlyConsumesInStockTags()
    {
        var service = ReadSource("ErpJwTagWriteService.cs");
        var module = ReadPlatformSource("Modules/ErpModule.cs");
        var page = ReadApp("CpJewelleryMastersApp.razor");
        Assert.Contains("AND `status` = 'in_stock'", service, StringComparison.Ordinal);
        Assert.Contains("company_id` = ?", service, StringComparison.Ordinal);
        Assert.Contains("CompanyId", service, StringComparison.Ordinal);
        Assert.Contains("companyId = LiveWriteFormBinder.Int(form", module, StringComparison.Ordinal);
        Assert.Contains("name=\"company\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void FixUnfixSettlement_IsCompanyScopedAndSingleUse()
    {
        var service = ReadSource("ErpJwFixUnfixWriteService.cs");
        var module = ReadPlatformSource("Modules/ErpModule.cs");
        var page = ReadApp("CpJewelleryFixingApp.razor");
        Assert.Contains("status` = 'open'", service, StringComparison.Ordinal);
        Assert.Contains("structure_type` = 'unfix'", service, StringComparison.Ordinal);
        Assert.Contains("company_id` = ?", service, StringComparison.Ordinal);
        Assert.Contains("CompanyId", service, StringComparison.Ordinal);
        Assert.Contains("companyId = LiveWriteFormBinder.Int(form", module, StringComparison.Ordinal);
        Assert.Contains("name=\"company\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public void FitOutEstimateRevisionComparison_IsRegisteredAndProjectScoped()
    {
        var service = ReadPlatformSource("Erp/ErpFitOutEstimateRevisionReadService.cs");
        var routes = ReadPlatformSource("Routing/EcomAeRoutes.cs");
        var module = ReadPlatformSource("Modules/ErpModule.cs");
        var page = ReadApp("ErpProjectAccountingApp.razor");
        Assert.Contains("WHERE e.`project_id` = ?", service, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutEstimateRevisionComparison", routes, StringComparison.Ordinal);
        Assert.Contains("HandleFitOutEstimateRevisionComparisonAsync", module, StringComparison.Ordinal);
        Assert.Contains("Compare estimate revisions JSON", page, StringComparison.Ordinal);
    }

    [Fact]
    public void FitOutApprovalEvidence_IsProjectScopedAndUsesQueueAndAudit()
    {
        var service = ReadPlatformSource("Erp/ErpFitOutApprovalEvidenceReadService.cs");
        var routes = ReadPlatformSource("Routing/EcomAeRoutes.cs");
        var module = ReadPlatformSource("Modules/ErpModule.cs");
        var page = ReadApp("ErpProjectAccountingApp.razor");
        Assert.Contains("ReadAsync(projectId, cancellationToken)", service, StringComparison.Ordinal);
        Assert.Contains("IErpFitOutApprovalQueueReadService", service, StringComparison.Ordinal);
        Assert.Contains("IErpFitOutApprovalAuditReadService", service, StringComparison.Ordinal);
        Assert.Contains("ErpFitOutApprovalEvidence", routes, StringComparison.Ordinal);
        Assert.Contains("HandleFitOutApprovalEvidenceAsync", module, StringComparison.Ordinal);
        Assert.Contains("Open approval evidence JSON", page, StringComparison.Ordinal);
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
    public void MasterWritesPersistExpandedDiamondFieldsAndExistingMasterEdits()
    {
        var diamond = ReadSource("ErpJwDiamondWriteService.cs");
        Assert.Contains("Price2Code", diamond, StringComparison.Ordinal);
        Assert.Contains("certificate_no", diamond, StringComparison.Ordinal);
        Assert.Contains("setting_charge", diamond, StringComparison.Ordinal);
        Assert.Contains("pure_wt", diamond, StringComparison.Ordinal);
        Assert.Contains("exclude_gst_metal", diamond, StringComparison.Ordinal);
        Assert.Contains("cust_sku", diamond, StringComparison.Ordinal);
        Assert.Contains("ageing_date", diamond, StringComparison.Ordinal);

        var pearl = ReadSource("ErpJwPearlWriteService.cs");
        Assert.Contains("`shape` = VALUES(`shape`)", pearl, StringComparison.Ordinal);
        Assert.Contains("`luster` = VALUES(`luster`)", pearl, StringComparison.Ordinal);
        Assert.Contains("`price1_lc` = VALUES(`price1_lc`)", pearl, StringComparison.Ordinal);

        var colorStone = ReadSource("ErpJwColorStoneWriteService.cs");
        Assert.Contains("`vendor` = VALUES(`vendor`)", colorStone, StringComparison.Ordinal);
        Assert.Contains("`cost_centre` = VALUES(`cost_centre`)", colorStone, StringComparison.Ordinal);
        Assert.Contains("`grade` = VALUES(`grade`)", colorStone, StringComparison.Ordinal);
    }

    [Fact]
    public void JewelleryWriteServicesKeepSqlAndWriteEnvelopeSafe()
    {
        var diamond = ReadSource("ErpJwDiamondWriteService.cs");
        var values = diamond.Split("VALUES (", 2, StringSplitOptions.None)[1]
            .Split(") ON DUPLICATE", 2, StringSplitOptions.None)[0];
        Assert.Equal(51, values.Count(value => value == '?'));

        var voucher = ReadSource("ErpJwVoucherWriteService.cs");
        Assert.Contains("BeginTransactionAsync", voucher, StringComparison.Ordinal);
        Assert.Contains("transaction,", voucher, StringComparison.Ordinal);
        Assert.Contains("pricedLineTotal", voucher, StringComparison.Ordinal);
        Assert.Contains("if (net == 0)", voucher, StringComparison.Ordinal);
        Assert.Contains("if (pricedLineTotal > 0)", voucher, StringComparison.Ordinal);
        Assert.Contains("var pricedLineTotal = lines", voucher, StringComparison.Ordinal);
        Assert.Contains("if (net == 0)\n        {\n            var pricedLineTotal", voucher, StringComparison.Ordinal);
    }

    [Fact]
    public void IndustryCompanyLookupUsesACompanyBoundIndustryPackQuery()
    {
        var root = FindRepoRoot();
        var sql = File.ReadAllText(Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Migration", "LegacySurfaceDashboardSql.cs"));
        var reporter = File.ReadAllText(Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Migration", "SurfaceDashboardSummaryReporter.cs"));

        Assert.Contains("SelectErpCompanyIndustryPackById", sql, StringComparison.Ordinal);
        Assert.Contains("`company_id` = @companyId", sql, StringComparison.Ordinal);
        Assert.Contains("LegacySurfaceDashboardSql.SelectErpCompanyIndustryPackById", reporter, StringComparison.Ordinal);
        Assert.DoesNotContain(".Replace(\"SELECT `company_id`", reporter, StringComparison.Ordinal);
    }

    [Fact]
    public void FixingRetailStockApps_HonorTabAndCreateForms()
    {
        var fixing = ReadApp("CpJewelleryFixingApp.razor");
        Assert.Contains("jw_purchase_fixing", fixing, StringComparison.Ordinal);
        Assert.Contains("New Fixing", fixing, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryFixingSaveForm", fixing, StringComparison.Ordinal);
        Assert.Contains("Save fixing", fixing, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryFixUnfixCreateForm", fixing, StringComparison.Ordinal);
        Assert.Contains("Create fix / unfix", fixing, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryFixUnfixSettleForm", fixing, StringComparison.Ordinal);
        Assert.Contains("Settle unfix", fixing, StringComparison.Ordinal);
        Assert.Contains("Fixed Rate", fixing, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", fixing, StringComparison.Ordinal);

        var retail = ReadApp("CpJewelleryRetailApp.razor");
        Assert.Contains("jw_retail_sales", retail, StringComparison.Ordinal);
        Assert.Contains("New Invoice", retail, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryVoucherSaveForm", retail, StringComparison.Ordinal);
        Assert.Contains("Save voucher", retail, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryBarcodeGenerateForm", retail, StringComparison.Ordinal);
        Assert.Contains("retail_barcode", retail, StringComparison.Ordinal);
        Assert.Equal("Retail Sales (POS)", ErpJewelleryModuleChrome.RetailSpec("jw_retail_sales").Title);
        Assert.DoesNotContain("@onclick", retail, StringComparison.Ordinal);

        var stock = ReadApp("CpJewelleryStockVerificationApp.razor");
        Assert.Contains("jw_stock_verification", stock, StringComparison.Ordinal);
        Assert.Contains(ErpJewelleryModuleChrome.StockTabs, t => t.Key == "jw_metal_stock");
        Assert.Contains("New Count", stock, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryStockVerifySaveForm", stock, StringComparison.Ordinal);
        Assert.Contains("Save stock verification", stock, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryMetalStockSaveForm", stock, StringComparison.Ordinal);
        Assert.Contains("Save metal stock", stock, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", stock, StringComparison.Ordinal);

        var petty = ReadApp("ErpCashAccountsApp.razor");
        Assert.Contains("jw_petty_cash", petty, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryPettyCashSaveForm", petty, StringComparison.Ordinal);
        Assert.Contains("Save petty cash", petty, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", petty, StringComparison.Ordinal);

        var pos = ReadApp("CpPosOverviewApp.razor");
        Assert.Contains("Save POS advance", pos, StringComparison.Ordinal);
        Assert.Contains("jw_pos_advance_save", pos, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", pos, StringComparison.Ordinal);

        var gl = ReadApp("ErpGlJournalsApp.razor");
        Assert.Contains("Save journal voucher", gl, StringComparison.Ordinal);
        Assert.Contains("jw_journal_voucher_save", gl, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", gl, StringComparison.Ordinal);

        var barcodePurchase = ReadApp("ErpPurchaseOrdersApp.razor");
        Assert.Contains("ErpJewelleryBarcodePurchaseCreateForm", barcodePurchase, StringComparison.Ordinal);
        Assert.Contains("Create barcode purchase", barcodePurchase, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryBarcodePurchaseSellForm", barcodePurchase, StringComparison.Ordinal);
        Assert.Contains("Sell barcode purchase", barcodePurchase, StringComparison.Ordinal);
        Assert.Contains("barcode_purchase", barcodePurchase, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", barcodePurchase, StringComparison.Ordinal);

        var tourist = ReadApp("CpUaeTaxComplianceApp.razor");
        Assert.Contains("jw_tourist_vat", tourist, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryTouristVatSaveForm", tourist, StringComparison.Ordinal);
        Assert.Contains("Save tourist VAT", tourist, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", tourist, StringComparison.Ordinal);
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
        Assert.True(ErpIndustryNav.ShowJewelleryModules("jewellery", main));
        Assert.False(ErpIndustryNav.ShowJewelleryModules("auto_parts", main));
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
    public void JewelleryStockBalance_UsesPhpPositiveMetalKaratProjection()
    {
        Assert.Contains("epc_jewel_metal_stock", LegacySurfaceDashboardSql.SelectCpJewelleryStockBalanceRows, StringComparison.Ordinal);
        Assert.Contains("stock_qty", LegacySurfaceDashboardSql.SelectCpJewelleryStockBalanceRows, StringComparison.Ordinal);
        Assert.Contains("GROUP BY `metal`, `karat`", LegacySurfaceDashboardSql.SelectCpJewelleryStockBalanceRows, StringComparison.Ordinal);
        Assert.Contains("company_id", LegacySurfaceDashboardSql.SelectCpJewelleryStockBalanceRows, StringComparison.Ordinal);

        var page = ReadApp("CpJewelleryStockVerificationApp.razor");
        Assert.Contains("jw_stock_balance", page, StringComparison.Ordinal);
        Assert.Contains("BuildCpJewelleryStockBalanceDigestAsync", page, StringComparison.Ordinal);
        Assert.Contains("movement, as-of-date, branch, and valuation reconciliation remain on the legacy workflow until separately accepted", page, StringComparison.Ordinal);
    }

    [Fact]
    public void JewelleryVoucherForm_BindsRepeatedLineFields()
    {
        var module = ReadApp("CpJewelleryRetailApp.razor");
        var handler = ReadPlatformSource("Modules/ErpModule.cs");
        var binder = ReadPlatformSource("Migration/LiveWriteFormBinder.cs");
        Assert.Contains("Additional voucher lines", module, StringComparison.Ordinal);
        Assert.Contains("name=\"line_stock_code\"", module, StringComparison.Ordinal);
        Assert.Contains("form[\"line_stock_code\"].Count", handler, StringComparison.Ordinal);
        Assert.Contains("Enumerable.Range(0, lineCount)", handler, StringComparison.Ordinal);
        Assert.Contains("TextAt", binder, StringComparison.Ordinal);
        Assert.Contains("DecAt", binder, StringComparison.Ordinal);
    }

    [Fact]
    public void JewelleryRetailPage_ExposesTenantScopedTagBarcodeLookup()
    {
        var page = ReadApp("CpJewelleryRetailApp.razor");
        var sql = ReadPlatformSource("Migration/LegacySurfaceDashboardSql.cs");
        var reporter = ReadPlatformSource("Migration/SurfaceDashboardSummaryReporter.cs");
        Assert.Contains("name=\"lookup\"", page, StringComparison.Ordinal);
        Assert.Contains("LookupCpJewelleryTagsAsync", page, StringComparison.Ordinal);
        Assert.Contains("SelectCpJewelleryTagLookup", sql, StringComparison.Ordinal);
        Assert.Contains("SelectCpJewelleryBarcodeLookup", sql, StringComparison.Ordinal);
        Assert.Contains("@companyId = 0 OR `company_id` = @companyId", sql, StringComparison.Ordinal);
        Assert.Contains("rows.Take(safeLimit)", reporter, StringComparison.Ordinal);
    }

    [Fact]
    public void JewelleryFormRoutes_AreDedicatedHtmlPosts()
    {
        Assert.Equal("/erp/jewellery/repair-create", EcomAeRoutes.ErpJewelleryRepairCreateForm);
        Assert.Equal("/erp/jewellery/repair-status", EcomAeRoutes.ErpJewelleryRepairStatusForm);
        Assert.Equal("/erp/jewellery/karat-save", EcomAeRoutes.ErpJewelleryKaratSaveForm);
        Assert.Equal("/erp/jewellery/rate-type-save", EcomAeRoutes.ErpJewelleryRateTypeSaveForm);
        Assert.Equal("/erp/jewellery/currency-save", EcomAeRoutes.ErpJewelleryCurrencySaveForm);
        Assert.Equal("/erp/jewellery/diamond-save", EcomAeRoutes.ErpJewelleryDiamondSaveForm);
        Assert.Equal("/erp/jewellery/design-save", EcomAeRoutes.ErpJewelleryDesignSaveForm);
        Assert.Equal("/erp/jewellery/pearl-save", EcomAeRoutes.ErpJewelleryPearlSaveForm);
        Assert.Equal("/erp/jewellery/color-stone-save", EcomAeRoutes.ErpJewelleryColorStoneSaveForm);
        Assert.Equal("/erp/jewellery/metal-stock-save", EcomAeRoutes.ErpJewelleryMetalStockSaveForm);
        Assert.Equal("/erp/jewellery/fixing-save", EcomAeRoutes.ErpJewelleryFixingSaveForm);
        Assert.Equal("/erp/jewellery/voucher-save", EcomAeRoutes.ErpJewelleryVoucherSaveForm);
        Assert.Equal("/erp/jewellery/petty-cash-save", EcomAeRoutes.ErpJewelleryPettyCashSaveForm);
        Assert.Equal("/erp/jewellery/tourist-vat-save", EcomAeRoutes.ErpJewelleryTouristVatSaveForm);
        Assert.Equal("/erp/jewellery/repair-receipt-save", EcomAeRoutes.ErpJewelleryRepairReceiptSaveForm);
        Assert.Equal("/erp/jewellery/repair-transfer-save", EcomAeRoutes.ErpJewelleryRepairTransferSaveForm);
        Assert.Equal("/erp/jewellery/workshop-receive-save", EcomAeRoutes.ErpJewelleryWorkshopReceiveSaveForm);
        Assert.Equal("/erp/jewellery/repair-delivery-save", EcomAeRoutes.ErpJewelleryRepairDeliverySaveForm);
        Assert.Equal("/erp/jewellery/stock-verify-save", EcomAeRoutes.ErpJewelleryStockVerifySaveForm);
        Assert.Equal("/erp/jewellery/karat-seed", EcomAeRoutes.ErpJewelleryKaratSeedForm);
        Assert.Equal("/erp/jewellery/module-save", EcomAeRoutes.ErpJewelleryModuleSaveForm);
        Assert.Equal("/erp/jewellery/barcode-generate", EcomAeRoutes.ErpJewelleryBarcodeGenerateForm);
        Assert.Equal("/erp/jewellery/tag-create", EcomAeRoutes.ErpJewelleryTagCreateForm);
        Assert.Equal("/erp/jewellery/tag-sell", EcomAeRoutes.ErpJewelleryTagSellForm);
        Assert.Equal("/erp/jewellery/gold-scheme-create", EcomAeRoutes.ErpJewelleryGoldSchemeCreateForm);
        Assert.Equal("/erp/jewellery/gold-scheme-enroll", EcomAeRoutes.ErpJewelleryGoldSchemeEnrollForm);
        Assert.Equal("/erp/jewellery/gold-scheme-pay", EcomAeRoutes.ErpJewelleryGoldSchemePayForm);
        Assert.Equal("/erp/jewellery/fix-unfix-create", EcomAeRoutes.ErpJewelleryFixUnfixCreateForm);
        Assert.Equal("/erp/jewellery/fix-unfix-settle", EcomAeRoutes.ErpJewelleryFixUnfixSettleForm);
        Assert.Equal("/erp/jewellery/barcode-purchase-create", EcomAeRoutes.ErpJewelleryBarcodePurchaseCreateForm);
        Assert.Equal("/erp/jewellery/barcode-purchase-sell", EcomAeRoutes.ErpJewelleryBarcodePurchaseSellForm);
        Assert.Equal("/erp/sla/create", EcomAeRoutes.ErpSlaCreateForm);
        Assert.Equal("/erp/tickets/create", EcomAeRoutes.ErpTicketsCreateForm);
        Assert.Equal("/erp/customer-groups/create", EcomAeRoutes.ErpCustomerGroupsCreateForm);
        Assert.Equal("/erp/customer-groups/assign", EcomAeRoutes.ErpCustomerGroupsAssignForm);
        Assert.Equal("/erp/report-scheduler/create", EcomAeRoutes.ErpReportSchedulerCreateForm);
        Assert.Equal("/erp/virtual-warehouses/create", EcomAeRoutes.ErpVirtualWarehouseCreateForm);
        Assert.Equal("/erp/virtual-warehouses/transfer", EcomAeRoutes.ErpVirtualWarehouseTransferForm);
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
        Assert.True(ErpPhpTabRouteMap.TryMapTab("barcode_purchase", out var barcodePurchase));
        Assert.Equal("/erp/purchase-orders-app?tab=barcode_purchase", barcodePurchase);
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

    [Fact]
    public void JewelleryRepairPending_IsCompanyAndOperationScoped()
    {
        var service = ReadSource("ErpJwRepairPendingReadService.cs");
        var routes = ReadPlatformSource("Routing/EcomAeRoutes.cs");
        var module = ReadPlatformSource("Modules/ErpModule.cs");
        Assert.Contains("FROM `epc_jewel_repair` r", service, StringComparison.Ordinal);
        Assert.Contains("JOIN `epc_jewel_repair_items` ri", service, StringComparison.Ordinal);
        Assert.Contains("WHERE r.`company_id`=? AND r.`status` IN ('received','in_progress')", service, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryRepairPending", routes, StringComparison.Ordinal);
        Assert.Contains("HandleJewelleryRepairPendingAsync", module, StringComparison.Ordinal);
        Assert.Contains("Pending repair jobs are read-only and company-scoped.", module, StringComparison.Ordinal);
    }

    private static string ReadApp(string fileName)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages", fileName);
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
    }

    private static string ReadSource(string fileName)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Erp", fileName);
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
    }

    private static string ReadPlatformSource(string fileName)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", fileName);
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
