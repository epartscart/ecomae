using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Static parity pins for the hrt_review_finalize and costm_close_run live twins.</summary>
public sealed class ErpHrtCostmPhpParityTests
{
    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative);
    }

    private static string ReadRepo(string relative) => File.ReadAllText(FindRepoFile(relative));

    [Fact]
    public void CatalogRowsAreLiveGated()
    {
        foreach (var route in new[] { EcomAeRoutes.ErpAjaxHrtReviewFinalize, EcomAeRoutes.ErpAjaxCostmCloseRun })
        {
            var row = SurfacePayloadContractCatalog.Functions.Single(r => r.AspNetRouteOrCapability == route);
            Assert.Equal("write-live-gated", row.Status);
            Assert.Contains("confirm_writes=true", row.Notes);
        }
    }

    [Fact]
    public void HandlersAreWiredToLiveServiceAndCsrfKept()
    {
        var module = ReadRepo("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("endpoints.MapPost(EcomAeRoutes.ErpAjaxHrtReviewFinalize, HandleHrtReviewFinalizeAsync).DisableAntiforgery();", module);
        Assert.Contains("endpoints.MapPost(EcomAeRoutes.ErpAjaxCostmCloseRun, HandleCostmCloseRunAsync).DisableAntiforgery();", module);
        Assert.Contains("IErpHrtReviewWriteService", module);
        Assert.Contains("IErpCostmCloseWriteService", module);
    }

    [Fact]
    public void ServicesKeepPhpGuardsAndContracts()
    {
        var hrt = ReadRepo("aspnet/src/EcomAE.Platform/Erp/ErpHrtReviewWriteService.cs");
        Assert.Contains("Review not found", hrt);
        Assert.Contains("Review is already completed", hrt);
        Assert.Contains("Add at least one goal before finalizing", hrt);
        Assert.Contains("epc_hrt_review", hrt);
        Assert.Contains("epc_hrt_goal", hrt);
        Assert.Contains("overall_rating", hrt);
        Assert.Contains("`status`='completed'", hrt);

        var costm = ReadRepo("aspnet/src/EcomAE.Platform/Erp/ErpCostmCloseWriteService.cs");
        Assert.Contains("epc_costm_item", costm);
        Assert.Contains("epc_costm_txn", costm);
        Assert.Contains("epc_costm_close", costm);
        Assert.Contains("\"standard\", \"fifo\", \"lifo\", \"moving_avg\"", costm);
        Assert.Contains("moving_avg", costm);
        Assert.Contains("detail_json", costm);
        Assert.Contains("Closing: COGS ", costm);
    }

    [Fact]
    public void ServicesAreRegistered()
    {
        var program = ReadRepo("aspnet/src/EcomAE.Platform/Program.cs");
        Assert.Contains("IErpHrtReviewWriteService, EcomAE.Platform.Erp.ErpHrtReviewWriteService", program);
        Assert.Contains("IErpCostmCloseWriteService, EcomAE.Platform.Erp.ErpCostmCloseWriteService", program);
        Assert.Contains("IErpBosComplianceFetchService, EcomAE.Platform.Erp.ErpBosComplianceFetchService", program);
        Assert.Contains("IErpOplPlanningWriteService, EcomAE.Platform.Erp.ErpOplPlanningWriteService", program);
        Assert.Contains("IErpCsDeclarationsReadService, EcomAE.Platform.Erp.ErpCsDeclarationsReadService", program);
    }

    [Fact]
    public void OplAndCsRoutesKeepPhpContract()
    {
        foreach (var route in new[]
        {
            EcomAeRoutes.ErpAjaxOplSeedDemo,
            EcomAeRoutes.ErpAjaxOplClearDemo,
            EcomAeRoutes.ErpAjaxOplConfirmAll,
            EcomAeRoutes.ErpAjaxCsListDeclarations,
        })
        {
            var row = SurfacePayloadContractCatalog.Functions.Single(r => r.AspNetRouteOrCapability == route);
            Assert.Equal("write-live-gated", row.Status);
            Assert.Contains("confirm_writes=true", row.Notes);
        }

        var module = ReadRepo("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("ErpAjaxOplSeedDemo, HandleOplSeedDemoAsync).DisableAntiforgery()", module);
        Assert.Contains("ErpAjaxOplClearDemo, HandleOplClearDemoAsync).DisableAntiforgery()", module);
        Assert.Contains("ErpAjaxOplConfirmAll, HandleOplConfirmAllAsync).DisableAntiforgery()", module);
        Assert.Contains("ErpAjaxCsListDeclarations, HandleCsListDeclarationsAsync).DisableAntiforgery()", module);

        var opl = ReadRepo("aspnet/src/EcomAE.Platform/Erp/ErpOplPlanningWriteService.cs");
        Assert.Contains("DEMO-DEMAND", opl);
        Assert.Contains("epc_erp_inv_movements", opl);
        Assert.Contains("epc_erp_order_recommendations", opl);
        Assert.Contains("epc_erp_planning_params", opl);
        Assert.Contains("recommendation(s) confirmed", opl);
        Assert.Contains("seeded demand movements", opl);
        Assert.Contains("1.32", opl);
        Assert.Contains("0.49", opl);
        Assert.Contains("30.4375", opl);

        var cs = ReadRepo("aspnet/src/EcomAE.Platform/Erp/ErpCsDeclarationsReadService.cs");
        Assert.Contains("epc_custom_shipping_declarations", cs);
        Assert.Contains("epc_custom_shipping_declaration_items", cs);
        Assert.Contains("field_data", cs);
        Assert.Contains("pdf_autofill_keys", cs);
        Assert.Contains("uq_cs_declaration_number", cs);
        Assert.Contains("item_count", cs);
    }

    [Fact]
    public void BosComplianceFetchKeepsPhpSeedContract()
    {
        var row = SurfacePayloadContractCatalog.Functions.Single(r => r.AspNetRouteOrCapability == EcomAeRoutes.ErpAjaxBosComplianceFetch);
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("confirm_writes=true", row.Notes);

        var module = ReadRepo("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("endpoints.MapPost(EcomAeRoutes.ErpAjaxBosComplianceFetch, HandleBosComplianceFetchAsync).DisableAntiforgery();", module);

        var service = ReadRepo("aspnet/src/EcomAE.Platform/Erp/ErpBosComplianceFetchService.cs");
        Assert.Contains("epc_bos_compliance_obligations", service);
        Assert.Contains("epc_bos_retention_rules", service);
        Assert.Contains("epc_price_settings", service);
        Assert.Contains("is_seed", service);
        Assert.Contains("2026.06.2", service);
        Assert.Contains("erp_company_country", service);
        Assert.Contains("erp_industry_pack", service);
        Assert.Contains("Compliance catalog is up to date", service);
        Assert.Contains("UAE FTA (EmaraTax)", service);
        Assert.Contains("ZATCA (Saudi Arabia)", service);
        Assert.Contains("aml_dpmsr", service);
    }
}
