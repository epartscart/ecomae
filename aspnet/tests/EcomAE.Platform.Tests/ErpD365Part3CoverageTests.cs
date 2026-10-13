using System.Data.Common;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpD365Part3CoverageTests
{
    [Fact]
    public void Catalog_CoversEveryPart3ModuleAndPhotoLabel()
    {
        Assert.True(ErpD365Part3Catalog.All.Count >= 200);
        Assert.Equal(ErpD365Part3Catalog.All.Count, ErpD365Part3Catalog.All.Select(p => p.Tab).Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.Contains("Time off requests", ErpD365Part3Catalog.RequiredLabels("leave_abs"));
        Assert.Contains("Leave and absence plans", ErpD365Part3Catalog.RequiredLabels("leave_abs"));
        Assert.Contains("Holidays and closures", ErpD365Part3Catalog.RequiredLabels("leave_abs"));
        Assert.Contains("Planned orders", ErpD365Part3Catalog.RequiredLabels("master_planning_area"));
        Assert.Contains("Demand driven MRP", ErpD365Part3Catalog.RequiredLabels("master_planning_area"));
        Assert.Contains("Item coverage", ErpD365Part3Catalog.RequiredLabels("master_planning_area"));
        Assert.Contains("Inbound queue", ErpD365Part3Catalog.RequiredLabels("mhei"));
        Assert.Contains("Material handling equipment interface parameters", ErpD365Part3Catalog.RequiredLabels("mhei"));
        Assert.Contains("Number sequences", ErpD365Part3Catalog.RequiredLabels("enterprise"));
        Assert.Contains("Global address book", ErpD365Part3Catalog.RequiredLabels("enterprise"));
        Assert.Contains("WPS", ErpD365Part3Catalog.RequiredLabels("payroll_area"));
        Assert.Contains("End of service request", ErpD365Part3Catalog.RequiredLabels("payroll_area"));
        Assert.Contains("Purchase agreements", ErpD365Part3Catalog.RequiredLabels("purchasing"));
        Assert.Contains("Request for quotations follow-up", ErpD365Part3Catalog.RequiredLabels("purchasing"));
    }

    [Fact]
    public void RouteMap_ResolvesNewTabsAndKeepsExistingDedicatedApps()
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab("time_off_requests", out var leave));
        Assert.Equal("/erp/leave-absence-app?tab=time_off_requests", leave);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("planned_orders", out var planned));
        Assert.Equal("/erp/master-planning-app?tab=planned_orders", planned);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("mhei_inbound", out var inbound));
        Assert.Equal("/erp/mhei-app?tab=mhei_inbound", inbound);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("org_number_sequences", out var ns));
        Assert.Equal("/erp/organization-admin-app?tab=org_number_sequences", ns);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("pay_wps", out var wps));
        Assert.Equal("/erp/payroll-workspace-app?tab=pay_wps", wps);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("proc_agreements", out var agr));
        Assert.Equal("/erp/procurement-workspace-app?tab=proc_agreements", agr);

        Assert.True(ErpPhpTabRouteMap.TryMapTab("payroll", out var payroll));
        Assert.Equal("/erp/payroll-app", payroll);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("purchase_orders", out var pos));
        Assert.Equal("/erp/purchase-orders-app", pos);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("legal_entities", out var legal));
        Assert.Equal("/erp/organization-app?tab=legal_entities", legal);
        Assert.True(ErpPhpTabRouteMap.TryMapTab("master_planning", out var mp));
        Assert.Equal("/erp/order-planning-app?tab=master_planning", mp);
    }

    [Fact]
    public void AreaHubs_OpenDedicatedWorkspaces()
    {
        Assert.Equal("/erp/leave-absence-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=leave_abs"));
        Assert.Equal("/erp/master-planning-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=master_planning_area"));
        Assert.Equal("/erp/mhei-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=mhei"));
        Assert.Equal("/erp/organization-admin-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=enterprise"));
        Assert.Equal("/erp/payroll-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=payroll_area"));
        Assert.Equal("/erp/payroll-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=payroll"));
        Assert.Equal("/erp/procurement-workspace-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=purchasing"));
    }

    [Fact]
    public void Nav_ListsNewWorkspaceTabs()
    {
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "leave_abs/time_off_requests");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "master_planning_area/planned_orders");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "mhei/mhei_inbound");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "enterprise/org_number_sequences");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "payroll_area/pay_wps");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "purchasing/proc_agreements");
        Assert.DoesNotContain(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "payroll_area/payroll");
    }

    [Fact]
    public void Write_ValidatesKindAndRefusesDedicatedPages()
    {
        Assert.Equal("invalid", ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "missing"))!.Code);
        Assert.Equal("invalid", ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "payroll"))!.Code);
        Assert.Equal("invalid", ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "time_off_requests"))!.Code);
        Assert.Null(ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "time_off_requests", Worker: "EMP-1")));
        Assert.Null(ErpD365WorkspaceWriteService.Validate(new ErpD365WorkspaceWriteRequest(Tab: "leave_plans", Code: "ANN", Name: "Annual")));
        Assert.Equal("pending", ErpD365WorkspaceWriteService.NormalizeStatus("", ErpD365Part3Catalog.FormKind.Request));
        Assert.Equal("planned", ErpD365WorkspaceWriteService.NormalizeStatus("", ErpD365Part3Catalog.FormKind.Order));
    }

    [Fact]
    public void DryRun_ValidatesAndRefusesConfirm()
    {
        var dry = new ErpD365WorkspaceDryRun();
        var ok = dry.Evaluate(new ErpD365WorkspaceWriteRequest(Tab: "time_off_requests", Worker: "EMP-1"), false);
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.True(ok.WouldWrite);
        Assert.Equal(0, ok.Writes);
        var refused = dry.Evaluate(new ErpD365WorkspaceWriteRequest(Tab: "time_off_requests", Worker: "EMP-1"), true);
        Assert.Equal("confirm_writes_refused", refused.ValidationCode);
    }

    [Fact]
    public void App_EmitsWorkspaceFormsWithoutForbiddenWords()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor"));
        Assert.Contains("@page \"/erp/leave-absence-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/master-planning-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/mhei-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/organization-admin-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/payroll-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/erp/procurement-workspace-app\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpD365WorkspaceCatalog.SavePath", text, StringComparison.Ordinal);
        Assert.Equal("/erp/d365-workspace/save", ErpD365WorkspaceCatalog.SavePath);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteService_InsertsWorkspaceRowOnThrowawayDb()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_d365_" + Guid.NewGuid().ToString("N")[..12];
        var admin = "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        var cs = "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;";
        await using (var adminConn = new MySqlConnection(admin))
        {
            await adminConn.OpenAsync();
            await using var create = new MySqlCommand("CREATE DATABASE `" + database + "`", adminConn);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var writes = new ErpD365WorkspaceWriteService(new Connections(cs));
            var saved = await writes.SaveAsync(new ErpD365WorkspaceWriteRequest(
                Area: "leave_abs",
                Tab: "time_off_requests",
                Worker: "EMP-1",
                Kind: "annual",
                Qty: 2,
                Note: "Eid leave"));
            Assert.True(saved.Succeeded, saved.Message);
            Assert.True(saved.Id > 0);

            var rows = await new ErpD365WorkspaceReadService(new Connections(cs))
                .ListAsync("leave_abs", "time_off_requests");
            var row = Assert.Single(rows);
            Assert.Equal("EMP-1", row.Worker);
            Assert.Equal(2m, row.Qty);
            Assert.Equal("pending", row.Status);
        }
        finally
        {
            await using var adminConn = new MySqlConnection(admin);
            await adminConn.OpenAsync();
            await using var drop = new MySqlCommand("DROP DATABASE IF EXISTS `" + database + "`", adminConn);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public void SchemaAndRoutes_AreRegistered()
    {
        var schema = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs"));
        Assert.Contains("epc_erp_d365_workspace", schema, StringComparison.Ordinal);
        Assert.Contains("EnsureD365WorkspaceAsync", schema, StringComparison.Ordinal);
        Assert.Equal("/erp/d365-workspace/save", EcomAeRoutes.ErpD365WorkspaceSave);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpD365WorkspaceWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("HandleD365WorkspaceSaveAsync", module, StringComparison.Ordinal);
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability.Contains("/erp/d365-workspace/save", StringComparison.Ordinal));
        Assert.Equal("write-live-gated", write.Status);
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

        throw new FileNotFoundException(relative);
    }

    private sealed class Connections(string cs) : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(cs);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
