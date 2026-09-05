using System.Reflection;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards /cp/fulfillment-queue-app against inventing a non-PHP queue shell.
/// Must keep dual-pane workspace + GET hrefs from PHP epc_fulfillment_list / epc_fulfillment_get.
/// </summary>
public sealed class CpFulfillmentQueuePhpParityTests
{
    [Fact]
    public void CpFulfillmentQueueApp_EmitsPhpQueueConsoleMarkers()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpFulfillmentQueueApp.razor"));
        Assert.Contains("epc-orders-page", text, StringComparison.Ordinal);
        Assert.Contains("epc-orders-page__hero", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-kpi", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi__card", text, StringComparison.Ordinal);
        Assert.Contains("epc-orders-tabs", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-workspace", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-workspace__list", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-workspace__detail", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-data-table", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-row", text, StringComparison.Ordinal);
        Assert.Contains("epc-od epc-od--oms", text, StringComparison.Ordinal);
        Assert.Contains("epc-od__tabs", text, StringComparison.Ordinal);
        Assert.Contains("data-epc-od-tab", text, StringComparison.Ordinal);
        Assert.Contains("epc-od__lines", text, StringComparison.Ordinal);
        Assert.Contains("epc-od__doc-grid", text, StringComparison.Ordinal);
        Assert.Contains("CpOrdersOmsStylesheets", text, StringComparison.Ordinal);
        Assert.Contains("fulfillment_id=", text, StringComparison.Ordinal);
        Assert.Contains("data-epc-fulfillment-ssr", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/cp/shop/finance/epc_fulfillment_queue\"", text, StringComparison.Ordinal);
        Assert.Contains("GetCpFulfillmentDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpFulfillmentQueueDigestAsync(200, ctx.RequestAborted, _status)", text, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/fulfillment-queue/write\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"transition\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"create_wave\"", text, StringComparison.Ordinal);
        Assert.Contains("QtyOrdered", text, StringComparison.Ordinal);
        Assert.Contains("QtyPicked", text, StringComparison.Ordinal);
        Assert.Contains("QtyPacked", text, StringComparison.Ordinal);
        Assert.Contains("/cp/orders?order_id=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:void(0)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-nw-hero", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reporter_ExposesFulfillmentDetailDigest()
    {
        var iface = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/ISurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("GetCpFulfillmentDetailAsync", iface, StringComparison.Ordinal);
        var sql = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"));
        Assert.Contains("SelectCpFulfillmentOrderById", sql, StringComparison.Ordinal);
        Assert.Contains("SelectCpFulfillmentItems", sql, StringComparison.Ordinal);
        Assert.Contains("epc_fulfillment_items", sql, StringComparison.Ordinal);
        Assert.Contains("BuildSelectCpFulfillmentQueueRows", sql, StringComparison.Ordinal);
        var queued = LegacySurfaceDashboardSql.BuildSelectCpFulfillmentQueueRows("queued");
        Assert.Contains("WHERE IFNULL(`status`,'') = 'queued'", queued, StringComparison.Ordinal);
        Assert.Contains("LIMIT @limit", queued, StringComparison.Ordinal);
        var picking = LegacySurfaceDashboardSql.BuildSelectCpFulfillmentQueueRows("picking");
        Assert.Contains("'picking','picked','packing','packed'", picking, StringComparison.Ordinal);
        Assert.DoesNotContain("WHERE IFNULL(`status`,'') = 'queued'", LegacySurfaceDashboardSql.BuildSelectCpFulfillmentQueueRows("all"), StringComparison.Ordinal);
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ControlPanelFulfillmentQueueDetailDigest", routes, StringComparison.Ordinal);
        Assert.Contains("/cp/fulfillment-queue-detail-digest/{fulfillmentId:long}", routes, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksFulfillmentQueueDigestWired()
    {
        var catalog = SurfacePayloadContractCatalog.Functions;
        var shell = catalog.First(item => item.AspNetRouteOrCapability.Contains("/cp/fulfillment-queue-app", StringComparison.Ordinal));
        Assert.Equal("digest-wired-awaiting-dual-sample", shell.Status);
        Assert.Equal("write-live-gated", catalog.First(item => item.AspNetRouteOrCapability == "/cp/fulfillment-queue/write").Status);
    }

    [Fact]
    public void DetailFromRow_BuildsPhpGetShape()
    {
        var row = new CpFulfillmentQueueRowDigest(
            9, "SO-9", "Ada", "picking", "high", "main", "DHL",
            OrderId: 44, AssignedName: "Pat", TrackingNumber: "1Z", TotalItems: 3, WaveId: 2609031200);
        var detail = new CpFulfillmentDetailDigest(
            row.Id, row.OrderId, row.OrderNumber, row.CustomerName, row.Status, row.Priority, row.Warehouse,
            row.AssignedName, row.WaveId, row.Carrier, row.TrackingNumber, "standard", row.TotalItems, 0m,
            "", "", "", "", "", "", "",
            [new CpFulfillmentItemDigest(1, "SKU-1", "Oil filter", 2, 1, 0, "A-1", 0.4m, "pending", "")],
            "list", "");
        Assert.Equal(9, detail.Id);
        Assert.Equal(44, detail.OrderId);
        Assert.Equal("1Z", detail.TrackingNumber);
        Assert.Equal("SKU-1", detail.Items[0].Sku);
        Assert.Equal(2, detail.Items[0].QtyOrdered);
        Assert.Equal(1, detail.Items[0].QtyPicked);
    }

    [Fact]
    public void PhpLinkMap_PreservesFulfillmentId()
    {
        Assert.Equal(
            "/cp/fulfillment-queue-app?fulfillment_id=12",
            PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/shop/finance/epc_fulfillment_queue?fulfillment_id=12"));
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

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rooted = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", relative));
        Assert.True(File.Exists(rooted), $"Missing repo file: {relative}");
        return rooted;
    }
}
