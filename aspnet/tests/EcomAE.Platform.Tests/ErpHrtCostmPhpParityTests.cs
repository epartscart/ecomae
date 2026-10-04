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
    }
}
