using System.Data.Common;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpD365ProcessCoverageTests
{
    [Fact]
    public void Catalog_CoversOfficialLanesEdgesAndParameters()
    {
        Assert.True(ErpD365ProcessCatalog.Lanes.Count >= 20);
        Assert.True(ErpD365ProcessCatalog.Edges.Count >= 70);
        Assert.True(ErpD365ProcessCatalog.Parameters.Count >= 35);
        Assert.Equal(
            ErpD365ProcessCatalog.Lanes.Count,
            ErpD365ProcessCatalog.Lanes.Select(l => l.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(ErpD365ProcessCatalog.Lanes, l => l.Code == "procure_to_pay");
        Assert.Contains(ErpD365ProcessCatalog.Lanes, l => l.Code == "order_to_cash");
        Assert.Contains(ErpD365ProcessCatalog.Lanes, l => l.Code == "general_ledger");
        Assert.Contains(ErpD365ProcessCatalog.Lanes, l => l.Code == "warehouse");
        Assert.Contains(ErpD365ProcessCatalog.Lanes, l => l.Code == "payroll");
    }

    [Fact]
    public void EveryStepAndEdge_UsesExistingWorkspaceTabs()
    {
        var missingSteps = ErpD365ProcessCatalog.Lanes
            .SelectMany(l => l.Steps.Select(s => s.Tab))
            .Where(tab => !ErpD365WorkspaceCatalog.TryGet(tab, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Empty(missingSteps);

        var missingEdges = ErpD365ProcessCatalog.Edges
            .SelectMany(e => new[] { e.FromTab, e.ToTab })
            .Where(tab => !ErpD365WorkspaceCatalog.TryGet(tab, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.Empty(missingEdges);
    }

    [Fact]
    public void FieldMaps_UseOnlyOfficialWriterFields()
    {
        foreach (var edge in ErpD365ProcessCatalog.Edges)
        {
            var fields = ErpD365ProcessCatalog.ParseFieldMap(edge.FieldMap);
            Assert.NotEmpty(fields);
            Assert.All(fields, field => Assert.Contains(field, ErpD365ProcessCatalog.OfficialFields));
            var extras = edge.FieldMap
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(f => !ErpD365ProcessCatalog.OfficialFields.Contains(f))
                .ToList();
            Assert.Empty(extras);
        }
    }

    [Fact]
    public void Parameters_AreOfficialAndAffectExistingTabs()
    {
        Assert.Contains(
            ErpD365ProcessCatalog.Parameters,
            p => p.Caption == "Accept overdelivery" && p.Area == "purchasing");
        Assert.Contains(
            ErpD365ProcessCatalog.Parameters,
            p => p.Caption == "Invoice matching" && p.Area == "ap");
        Assert.Contains(
            ErpD365ProcessCatalog.Parameters,
            p => p.Caption == "Credit limit type" && p.Area == "ar");
        Assert.Contains(
            ErpD365ProcessCatalog.Parameters,
            p => p.Caption == "Must be balanced" && p.Area == "gl");
        Assert.Contains(
            ErpD365ProcessCatalog.Parameters,
            p => p.Caption == "Reservation" && p.Area == "inventory_mgmt");
        Assert.Contains(
            ErpD365ProcessCatalog.Parameters,
            p => p.Caption == "Accounting currency" && p.Area == "gl");

        foreach (var parameter in ErpD365ProcessCatalog.Parameters)
        {
            Assert.False(string.IsNullOrWhiteSpace(parameter.Caption));
            Assert.NotEmpty(parameter.AffectsTabs);
            foreach (var tab in parameter.AffectsTabs)
            {
                Assert.True(ErpD365WorkspaceCatalog.TryGet(tab, out _), tab);
            }
        }
    }

    [Fact]
    public void Interlink_FollowsOfficialProcureAndSalesChains()
    {
        Assert.True(ErpD365ProcessCatalog.TryGetEdge("rfq", "purchase_orders", out var award));
        Assert.Equal("awards", award.Relation);
        Assert.True(ErpD365ProcessCatalog.TryGetEdge("purchase_orders", "proc_receipts", out var receipt));
        Assert.Equal("receives", receipt.Relation);
        Assert.True(ErpD365ProcessCatalog.TryGetEdge("purchase_orders", "purchases", out var invoice));
        Assert.Equal("invoices", invoice.Relation);
        Assert.True(ErpD365ProcessCatalog.TryGetEdge("sm_quotations", "sm_all_orders", out var so));
        Assert.Equal("creates", so.Relation);
        Assert.True(ErpD365ProcessCatalog.TryGetEdge("sm_all_orders", "sm_invoice", out var salesInvoice));
        Assert.Equal("invoices", salesInvoice.Relation);
        Assert.False(ErpD365ProcessCatalog.TryGetEdge("rfq", "sm_all_orders", out _));
    }

    [Fact]
    public void DryRun_BlocksWritesAndRefusesUnknownKeys()
    {
        var parameters = new ErpD365ParameterDryRun();
        var ok = parameters.Evaluate(new ErpD365ParameterWriteRequest("purchasing", "rfq_reply_days", "10"), false);
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        var unknown = parameters.Evaluate(new ErpD365ParameterWriteRequest("purchasing", "invented_field", "x"), false);
        Assert.Equal("invalid", unknown.ValidationCode);
        var refused = parameters.Evaluate(new ErpD365ParameterWriteRequest("purchasing", "rfq_reply_days", "10"), true);
        Assert.Equal("confirm_writes_refused", refused.ValidationCode);

        var links = new ErpD365LinkDryRun();
        var linkOk = links.Evaluate(
            new ErpD365LinkWriteRequest("follow", "rfq", 0, "RFQ-1", "purchase_orders"),
            false);
        Assert.Equal("dry-run-validated", linkOk.Status);
        Assert.Equal(0, linkOk.Writes);
        var invented = links.Evaluate(
            new ErpD365LinkWriteRequest("follow", "rfq", 0, "RFQ-1", "sm_all_orders"),
            false);
        Assert.Equal("invalid", invented.ValidationCode);
    }

    [Fact]
    public void App_ShowsProcessStripParametersAndLinksWithoutForbiddenWords()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpD365WorkspaceApp.razor"));
        Assert.Contains("ErpD365ProcessCatalog", text, StringComparison.Ordinal);
        Assert.Contains("ParameterSavePath", text, StringComparison.Ordinal);
        Assert.Contains("LinkSavePath", text, StringComparison.Ordinal);
        Assert.Contains("Official parameters", text, StringComparison.Ordinal);
        Assert.Contains("Related documents", text, StringComparison.Ordinal);
        Assert.Contains("Process study for administrators", text, StringComparison.Ordinal);
        Assert.Contains("Create next", text, StringComparison.Ordinal);
        Assert.Contains("csrf_guard_key", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);

        var catalog = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Presentation/ErpD365ProcessCatalog.cs"));
        Assert.DoesNotContain("PHP", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", catalog, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void SchemaAndRoutes_AreRegistered()
    {
        var schema = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpLazySchema.cs"));
        Assert.Contains("epc_erp_d365_parameters", schema, StringComparison.Ordinal);
        Assert.Contains("epc_erp_d365_link", schema, StringComparison.Ordinal);
        Assert.Equal("/erp/d365-parameters/save", EcomAeRoutes.ErpD365ParameterSave);
        Assert.Equal("/erp/d365-link/save", EcomAeRoutes.ErpD365LinkSave);
        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpD365ParameterWriteService", program, StringComparison.Ordinal);
        Assert.Contains("IErpD365LinkWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("HandleD365ParameterSaveAsync", module, StringComparison.Ordinal);
        Assert.Contains("HandleD365LinkSaveAsync", module, StringComparison.Ordinal);
        Assert.Contains(
            SurfacePayloadContractCatalog.Functions,
            item => item.AspNetRouteOrCapability.Contains("/erp/d365-parameters/save", StringComparison.Ordinal)
                && item.Status == "write-live-gated");
        Assert.Contains(
            SurfacePayloadContractCatalog.Functions,
            item => item.AspNetRouteOrCapability.Contains("/erp/d365-link/save", StringComparison.Ordinal)
                && item.Status == "write-live-gated");
    }

    [Fact]
    public async Task WriteServices_PersistParameterAndFollowOnThrowawayDb()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var database = "ecomae_d365p_" + Guid.NewGuid().ToString("N")[..12];
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
            var connections = new Connections(cs);
            var workspace = new ErpD365WorkspaceWriteService(connections);
            var parameters = new ErpD365ParameterWriteService(connections);
            var links = new ErpD365LinkWriteService(connections, workspace);
            var reads = new ErpD365ProcessReadService(connections);

            var savedParam = await parameters.SaveAsync(new ErpD365ParameterWriteRequest(
                "purchasing",
                "rfq_reply_days",
                "9",
                7));
            Assert.True(savedParam.Succeeded, savedParam.Message);

            var dedicated = await links.SaveAsync(new ErpD365LinkWriteRequest(
                Action: "follow",
                FromTab: "rfq",
                FromId: 0,
                FromCode: "RFQ-1",
                ToTab: "purchase_orders",
                Party: "VEND-1",
                Name: "Bolt",
                Qty: 10,
                Amount: 50));
            Assert.False(dedicated.Succeeded, "Dedicated purchase-order form must refuse the generic writer.");

            var started = await workspace.SaveAsync(new ErpD365WorkspaceWriteRequest(
                Area: "production",
                Tab: "prd_start",
                Code: "PO-100",
                Name: "Bolt",
                Qty: 10));
            Assert.True(started.Succeeded, started.Message);

            var writerFollow = await links.SaveAsync(new ErpD365LinkWriteRequest(
                Action: "follow",
                FromTab: "prd_start",
                FromId: started.Id,
                FromCode: "PO-100",
                ToTab: "prd_picking_list",
                Name: "Bolt",
                Qty: 10,
                Note: "Pick for start"));
            Assert.True(writerFollow.Succeeded, writerFollow.Message);
            Assert.True(writerFollow.Id > 0);

            var listed = await reads.ListParametersAsync("purchasing");
            var reply = Assert.Single(listed, p => p.Key == "rfq_reply_days");
            Assert.Equal("9", reply.Value);
            Assert.True(reply.Customized);

            var related = await reads.ListLinksAsync("prd_start");
            Assert.Contains(related, row => row.Relation == "creates" && row.ToTab == "prd_picking_list");
        }
        finally
        {
            await using var adminConn = new MySqlConnection(admin);
            await adminConn.OpenAsync();
            await using var drop = new MySqlCommand("DROP DATABASE IF EXISTS `" + database + "`", adminConn);
            await drop.ExecuteNonQueryAsync();
        }
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
