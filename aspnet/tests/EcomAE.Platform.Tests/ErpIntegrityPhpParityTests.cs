using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Static parity pins for the integrity_scan / integrity_apply_fks live twins.</summary>
public sealed class ErpIntegrityPhpParityTests
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
        foreach (var route in new[] { EcomAeRoutes.ErpAjaxIntegrityScan, EcomAeRoutes.ErpAjaxIntegrityApplyFks })
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
        Assert.Contains("endpoints.MapPost(EcomAeRoutes.ErpAjaxIntegrityScan, HandleIntegrityScanAsync).DisableAntiforgery();", module);
        Assert.Contains("endpoints.MapPost(EcomAeRoutes.ErpAjaxIntegrityApplyFks, HandleIntegrityApplyFksAsync).DisableAntiforgery();", module);
        Assert.Contains("IErpIntegrityService", module);
    }

    [Fact]
    public void ServiceKeepsPhpRelationshipCatalogueAndGuard()
    {
        var service = ReadRepo("aspnet/src/EcomAE.Platform/Erp/ErpIntegrityService.cs");
        foreach (var token in new[]
                 {
                     "epc_erp_gl_lines", "journal_id", "epc_erp_gl_journals",
                     "epc_erp_inv_movements", "epc_erp_inv_serials", "epc_erp_inv_item_fields",
                     "epc_einvoice_lines", "epc_einvoice_documents",
                     "epc_erp_purchase_inv_lines", "epc_erp_purchases",
                     "ON DELETE ",
                     "information_schema.TABLES",
                     "information_schema.KEY_COLUMN_USAGE",
                     "information_schema.STATISTICS",
                     "orphan rows — clean data first",
                     "FK already present",
                     "table/column missing",
                 })
        {
            Assert.Contains(token, service);
        }
    }

    [Fact]
    public void ServiceIsRegistered()
    {
        var program = ReadRepo("aspnet/src/EcomAE.Platform/Program.cs");
        Assert.Contains("IErpIntegrityService", program);
    }
}
