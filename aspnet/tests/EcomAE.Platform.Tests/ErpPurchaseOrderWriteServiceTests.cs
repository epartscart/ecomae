using System.Data.Common;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPurchaseOrderWriteServiceTests
{
    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Validation must fail before a connection is opened.");
    }

    private static ErpPurchaseOrderWriteService Service() => new(
        new UnconfiguredConnections(),
        new ErpVoucherNumberService(),
        new ErpTaxAmountCalculator(),
        new ErpAuditLogWriter());

    [Fact]
    public void LinesJsonWinsOverRepeatedFields()
    {
        var lines = ErpPurchaseOrderWriteService.ResolveLines(new ErpPurchaseOrderInput
        {
            LinesJson = """[{"item_code":"A1","description":"Brake pad","qty":2,"unit_cost_ex_vat":30.5}]""",
            Lines = [new ErpPurchaseOrderLineInput("Z9", "Ignored", 1m, 1m, 1m)],
        });

        var line = Assert.Single(lines);
        Assert.Equal("A1", line.ItemCode);
        Assert.Equal("Brake pad", line.Description);
        Assert.Equal(2m, line.Qty);
        Assert.Equal(61.00m, line.LineExVat);
    }

    [Fact]
    public void BlankOrZeroQtyLinesAreDropped()
    {
        var lines = ErpPurchaseOrderWriteService.ResolveLines(new ErpPurchaseOrderInput
        {
            Lines =
            [
                new ErpPurchaseOrderLineInput("", "   ", 1m, 10m, 10m),
                new ErpPurchaseOrderLineInput("", "Zero qty", 0m, 10m, 10m),
                new ErpPurchaseOrderLineInput("", "Filter", 3m, 2.5m, 0m),
            ],
        });

        var line = Assert.Single(lines);
        Assert.Equal("Filter", line.Description);
        Assert.Equal(7.50m, line.LineExVat);
    }

    [Theory]
    [InlineData(0, "PO title")]
    [InlineData(5, "")]
    [InlineData(5, "   ")]
    public async Task SupplierAndTitleAreRequiredBeforeAnyWrite(int supplierId, string title)
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().SaveAsync(
            new ErpPurchaseOrderInput { SupplierId = supplierId, Title = title, AmountExVat = 100m },
            adminId: 1));
        Assert.Equal("Supplier and title are required", ex.Message);
    }

    [Fact]
    public async Task UnknownStatusIsRejected()
    {
        var ex = await Assert.ThrowsAsync<ErpWriteException>(() => Service().SetStatusAsync(7, "posted", adminId: 1));
        Assert.Equal("Invalid PO status", ex.Message);
    }

    [Fact]
    public void StatusListMatchesPhpEnum()
        => Assert.Equal(["draft", "approved", "partial", "received", "cancelled"], ErpPurchaseOrderWriteService.AllowedStatuses);

    [Theory]
    [InlineData("draft", 0, "approved")]
    [InlineData("approved", 0, "received")]
    [InlineData("partial", 0, "received")]
    [InlineData("draft", 0, "cancelled")]
    [InlineData("approved", 0, "cancelled")]
    [InlineData("partial", 0, "cancelled")]
    [InlineData("received", 0, "cancelled")]
    [InlineData("approved", 0, "approved")]
    public void PoStatusAllowsPhpTabActions(string current, long purchaseId, string target)
        => Assert.Null(ErpPurchaseOrderWriteService.StatusTransitionError(current, purchaseId, target));

    [Theory]
    [InlineData("cancelled", 0, "approved", "Cancelled purchase orders cannot be reopened")]
    [InlineData("cancelled", 0, "draft", "Cancelled purchase orders cannot be reopened")]
    [InlineData("received", 9, "cancelled", "Purchase orders linked to a purchase invoice cannot be cancelled — void the purchase invoice first")]
    [InlineData("received", 0, "approved", "Only draft purchase orders can be approved")]
    [InlineData("draft", 0, "received", "Only approved or partially received purchase orders can be marked received")]
    [InlineData("approved", 0, "partial", "Use Receive lines to record a partial receipt")]
    [InlineData("approved", 0, "draft", "Purchase orders cannot be moved back to draft")]
    public void PoStatusRefusesTransitionsOutsideTheLifecycle(string current, long purchaseId, string target, string message)
        => Assert.Equal(message, ErpPurchaseOrderWriteService.StatusTransitionError(current, purchaseId, target));

    [Fact]
    public void ReceiptRecomputeDoesNotGoThroughTheManualStatusGuard()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            root = root.Parent;
        }

        var service = File.ReadAllText(Path.Combine(root!.FullName, "aspnet/src/EcomAE.Platform/Erp/ErpPurchaseOrderWriteService.cs"));
        Assert.Contains("await ApplyStatusAsync(connection, purchaseOrderId, status, adminId, cancellationToken)", service, StringComparison.Ordinal);
        Assert.DoesNotContain("await SetStatusAsync(", service, StringComparison.Ordinal);
    }

    [Fact]
    public void ReceivedJsonParsesPhpLineMap()
    {
        var map = ErpPurchaseOrderWriteService.ParseReceivedJson("""{"11":2.5,"12":"3","0":9,"x":4}""");

        Assert.Equal(2, map.Count);
        Assert.Equal(2.5m, map[11]);
        Assert.Equal(3m, map[12]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("not json")]
    public void ReceivedJsonFallsBackToEmptyMap(string? payload)
        => Assert.Empty(ErpPurchaseOrderWriteService.ParseReceivedJson(payload));

    [Fact]
    public void PoVoucherNumbersRenderLikePhp()
    {
        Assert.Equal("PO", ErpVoucherNumberService.NormalizeType("po_1"));
        Assert.Equal("PO-2026-00011", ErpVoucherNumberService.Render("PO-", 2026, 11, 5));
    }
}
