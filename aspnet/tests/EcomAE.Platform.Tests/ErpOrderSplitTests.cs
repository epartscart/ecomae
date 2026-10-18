using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOrderSplitTests
{
    [Fact]
    public void Partial_confirm_and_delivery_leave_a_backorder()
    {
        var line = new ErpOrderSplit.Line(42, 7, 10m, 0m, 0m, 0m, 0m);
        line = ErpOrderSplit.Apply(line, ErpOrderSplit.Confirm, 6m);
        line = ErpOrderSplit.Apply(line, ErpOrderSplit.Deliver, 4m);
        line = ErpOrderSplit.Apply(line, ErpOrderSplit.Invoice, 4m);
        Assert.Equal(6m, line.Confirmed);
        Assert.Equal(4m, line.Delivered);
        Assert.Equal(2m, line.Deliverable);
        Assert.Equal(2m, line.Invoiceable);
        Assert.Equal(6m, line.Backorder);
        Assert.Empty(ErpOrderSplit.Refuse([line], ErpOrderSplit.Deliver, [new ErpOrderSplit.Move(42, 7, 2m)]));
        Assert.NotEmpty(ErpOrderSplit.Refuse([line], ErpOrderSplit.Deliver, [new ErpOrderSplit.Move(42, 7, 3m)]));
    }

    [Fact]
    public void Delivery_and_invoice_wait_for_confirmation()
    {
        var line = new ErpOrderSplit.Line(42, 7, 10m, 0m, 0m, 0m, 0m);
        var deliver = ErpOrderSplit.Refuse([line], ErpOrderSplit.Deliver, [new ErpOrderSplit.Move(42, 7, 1m)]);
        var invoice = ErpOrderSplit.Refuse([line], ErpOrderSplit.Invoice, [new ErpOrderSplit.Move(42, 7, 1m)]);
        Assert.Contains("Confirm", deliver[0], StringComparison.Ordinal);
        Assert.Contains("Confirm", invoice[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Cancelling_the_backorder_closes_the_balance()
    {
        var line = new ErpOrderSplit.Line(42, 7, 10m, 6m, 4m, 4m, 0m);
        Assert.Equal(6m, line.Backorder);
        var closed = ErpOrderSplit.Apply(line, ErpOrderSplit.CancelBackorder, 6m);
        Assert.Equal(0m, closed.Backorder);
        Assert.Equal(0m, closed.Deliverable);
        Assert.NotEmpty(ErpOrderSplit.Refuse([line], ErpOrderSplit.CancelBackorder, [new ErpOrderSplit.Move(42, 7, 7m)]));
    }

    [Fact]
    public void One_document_can_carry_several_orders_and_one_order_can_be_split()
    {
        var first = new ErpOrderSplit.Line(42, 7, 10m, 6m, 0m, 0m, 0m);
        var second = new ErpOrderSplit.Line(43, 8, 5m, 5m, 0m, 0m, 0m);
        var together = ErpOrderSplit.Refuse(
            [first, second],
            ErpOrderSplit.Deliver,
            [new ErpOrderSplit.Move(42, 7, 4m), new ErpOrderSplit.Move(43, 8, 2m)]);
        Assert.Empty(together);

        var again = ErpOrderSplit.Apply(first, ErpOrderSplit.Deliver, 4m);
        again = ErpOrderSplit.Apply(again, ErpOrderSplit.Deliver, 2m);
        Assert.Equal(6m, again.Delivered);
        Assert.Equal(4m, again.Backorder);

        var parsed = ErpOrderSplit.Parse("42:7:4\n43:8:2");
        Assert.Equal(2, parsed.Count);
        Assert.Equal(43, parsed[1].OrderId);
    }

    [Fact]
    public void Invoiced_quantity_cannot_be_cancelled_as_a_backorder()
    {
        var line = new ErpOrderSplit.Line(42, 7, 10m, 10m, 4m, 8m, 0m);
        Assert.Equal(2m, line.Backorder);
        Assert.Empty(ErpOrderSplit.Refuse([line], ErpOrderSplit.CancelBackorder, [new ErpOrderSplit.Move(42, 7, 2m)]));
        Assert.NotEmpty(ErpOrderSplit.Refuse([line], ErpOrderSplit.CancelBackorder, [new ErpOrderSplit.Move(42, 7, 3m)]));
    }

    [Fact]
    public void Both_order_pages_post_the_split()
    {
        var root = RepoRoot();
        var sales = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor"));
        var purchase = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpPurchaseOrdersApp.razor"));
        var panel = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpOrderSplitPanel.razor"));
        var routes = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("ErpOrderSplitPanel Side=\"sales\"", sales, StringComparison.Ordinal);
        Assert.Contains("ErpOrderSplitPanel Side=\"purchase\"", purchase, StringComparison.Ordinal);
        Assert.Contains("order:line:qty", panel, StringComparison.Ordinal);
        Assert.Contains("Partial confirmation", panel, StringComparison.Ordinal);
        Assert.Contains("Cancel backorder", panel, StringComparison.Ordinal);
        Assert.Contains("/erp/sales-orders/split", routes, StringComparison.Ordinal);
        Assert.Contains("/erp/purchase-orders/split", routes, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", panel, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
