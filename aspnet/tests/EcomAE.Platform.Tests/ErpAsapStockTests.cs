using System.IO.Compression;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpAsapStockTests
{
    [Fact]
    public void CentralWarehouseGivesTheFirstCommitTheStock()
    {
        var decisions = ErpAsapStock.Allocate(10m,
        [
            ("A", 6m, false),
            ("B", 5m, false),
            ("C", 4m, false),
        ]);

        Assert.Equal(6m, decisions[0].ReservedQty);
        Assert.False(decisions[1].Accepted);
        Assert.Equal(4m, decisions[1].AvailableAfter);
        Assert.True(decisions[2].Accepted);
        Assert.Equal(4m, decisions[2].ReservedQty);
        Assert.Equal(0m, decisions[2].AvailableAfter);
    }

    [Fact]
    public void PartialLeavesNothingForTheNextUser()
    {
        var decisions = ErpAsapStock.Allocate(10m,
        [
            ("A", 6m, false),
            ("B", 5m, true),
            ("C", 4m, false),
        ]);

        Assert.Equal("partial", decisions[1].Code);
        Assert.Equal(4m, decisions[1].AddedQty);
        Assert.Equal(1m, decisions[1].BackorderQty);
        Assert.False(decisions[2].Accepted);
    }

    [Fact]
    public void RetryOfTheSameKeyDoesNotReserveTwice()
    {
        var board = new ErpAsapReservationBoard(10m);
        var first = board.TryReserve("SO-1", 6m, false);
        var again = board.TryReserve("SO-1", 6m, false);
        Assert.Equal(6m, first.ReservedQty);
        Assert.Equal("idempotent", again.Code);
        Assert.Equal(0m, again.AddedQty);
        Assert.Equal(6m, board.Reserved);
        Assert.Equal(4m, board.Available);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(250)]
    [InlineData(500)]
    public void SimultaneousRequestsNeverOversell(int racers)
    {
        var board = new ErpAsapReservationBoard(10m);
        var results = new ErpAsapDecision[racers];
        Parallel.For(0, racers, i =>
        {
            results[i] = board.TryReserve("user-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), 1m, false);
        });

        var won = results.Count(row => row.Accepted && row.AddedQty > 0m);
        Assert.Equal(10, won);
        Assert.Equal(10m, board.Reserved);
        Assert.Equal(0m, board.Available);
        Assert.Equal(10m, results.Where(row => row.AddedQty > 0m).Sum(row => row.AddedQty));
    }

    [Fact]
    public void ConfirmedStockStaysReservedWhenTheBrowserGoesAway()
    {
        Assert.True(ErpInventoryReservationWriteService.CountsAgainstAtp("confirmed", 0, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        Assert.False(ErpInventoryReservationWriteService.CountsAgainstAtp("released", 0, 1));
    }

    [Fact]
    public void AnotherCompanyNeedsAnIntercompanyFulfilment()
    {
        Assert.True(ErpAsapStock.MayReserve("CO-A", "CO-A", ErpAsapStock.FulfilmentOwnCentral, ErpAsapStock.StockOwned));
        Assert.False(ErpAsapStock.MayReserve("CO-B", "CO-A", ErpAsapStock.FulfilmentOwnCentral, ErpAsapStock.StockOwned));
        Assert.True(ErpAsapStock.MayReserve("CO-B", "CO-A", ErpAsapStock.FulfilmentIntercompany, ErpAsapStock.StockOwned));
        Assert.False(ErpAsapStock.MayReserve("CO-B", "CO-A", ErpAsapStock.FulfilmentIntercompany, ErpAsapStock.StockConsignment));
        Assert.False(ErpAsapStock.MayReserve("CO-A", "CO-A", ErpAsapStock.FulfilmentBackorder, ErpAsapStock.StockOwned));
    }

    [Fact]
    public void PartNumbersNormalizeWithoutLosingTheEquivalenceRule()
    {
        Assert.Equal("OEM12345", ErpAsapStock.NormalizeReference("OEM-12 345"));
        Assert.Equal(ErpAsapStock.NormalizeReference("OEM-12345"), ErpAsapStock.NormalizeReference("oem 12345"));
        Assert.True(ErpAsapStock.Interchangeable("verified"));
        Assert.False(ErpAsapStock.Interchangeable("conditional"));
        Assert.False(ErpAsapStock.Interchangeable("unverified"));
    }

    [Fact]
    public void WorkbookMatchesTheSuppliedSheetsAndDoesNotReserve()
    {
        Assert.Equal("/erp/asap/workbook", EcomAeRoutes.ErpAsapWorkbook);
        Assert.Equal(21, ErpAsapWorkbook.TemplateSheets.Length);
        Assert.Equal(
            new[] { "cross_ref_id", "sku", "reference_brand", "reference_part_number", "relationship_type", "fitment_verified", "source", "priority" },
            ErpAsapWorkbook.Headers("Cross_References"));
        var book = ErpAsapWorkbook.Read(ErpAsapWorkbook.Template());
        Assert.Equal(ErpAsapWorkbook.TemplateSheets, book.Select(sheet => sheet.Name));
        var start = book[0];
        Assert.Equal("ASAP — AUTOMOTIVE AFTERMARKET ERP IMPORT REFERENCE", start.Headers[0]);
        Assert.Contains(start.Records, row => row.Values.Any(value => value.Contains("Excel does not reserve stock", StringComparison.Ordinal)));
        Assert.Empty(ErpAsapWorkbook.Validate(book));
        var broken = book.Select(sheet => sheet.Name == "Bank_Cheques"
            ? sheet with
            {
                Records =
                [
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["cheque_id"] = "CHQ-1",
                        ["status"] = "cleared",
                        ["cleared_date"] = "",
                    },
                ],
            }
            : sheet).ToList();
        Assert.Contains(ErpAsapWorkbook.Validate(broken), error => error.Contains("cleared date", StringComparison.Ordinal));
    }

    [Fact]
    public void PackageContainsTheMasterWorkbook()
    {
        using var zip = new ZipArchive(new MemoryStream(ErpAsapWorkbook.ExportPackage()));
        Assert.NotNull(zip.GetEntry("ASAP_AUTO_PARTS_MASTER_IMPORT_TEMPLATE.xlsx"));
        Assert.NotNull(zip.GetEntry("README.txt"));
    }
}
