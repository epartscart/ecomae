using System.Reflection;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards printable POS receipt HTML (PHP epc_pos_receipt_html twin).
/// </summary>
public sealed class CpPosReceiptPhpParityTests
{
    [Fact]
    public void FormatQty_MatchesPhpRtrimNumberFormat()
    {
        Assert.Equal("1", CpPosWriteService.FormatQty(1m));
        Assert.Equal("1.25", CpPosWriteService.FormatQty(1.250m));
        Assert.Equal("2.5", CpPosWriteService.FormatQty(2.5m));
        Assert.Equal("0.001", CpPosWriteService.FormatQty(0.001m));
    }

    [Fact]
    public void CpPosReceiptApp_IsPrintableWithoutBlazorClicks()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpPosReceiptApp.razor"));
        Assert.Contains("@page \"/cp/pos/receipt/{SaleId:long}\"", text, StringComparison.Ordinal);
        Assert.Contains("data-epc-pos-receipt", text, StringComparison.Ordinal);
        Assert.Contains("onclick=\"window.print()\"", text, StringComparison.Ordinal);
        Assert.Contains("PhpSurfaceLinkMap.PhpReferenceOnlyHref", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CpPosOverviewApp_LinksReceipt()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpPosOverviewApp.razor"));
        Assert.Contains("/cp/pos/receipt/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("receipt HTML stay", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramAndRoutes_RegisterPosReceipt()
    {
        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("/cp/pos/receipt/{saleId:long}", routes, StringComparison.Ordinal);
        var catalog = SurfacePayloadContractCatalog.Functions;
        Assert.Contains("/cp/pos/receipt/{id}", catalog.First(f => f.AspNetRouteOrCapability == "/cp/pos-overview-app").Notes, StringComparison.Ordinal);
        Assert.Contains("Walk-in user create, tax-toolkit totals, ERP SO/invoice/voucher, and inventory sale_out are ASP.NET-live", catalog.First(f => f.AspNetRouteOrCapability == "/cp/pos-overview-app").Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void WalkinHash_MatchesPhpMd5OfRandomHexPlusSecret()
    {
        Assert.Equal("pos.walkin@local", CpPosWriteService.WalkinEmail);
        Assert.Equal(
            LegacyPasswordVerifier.Md5Hex("abcd1234local-test-secret"),
            CpPosWriteService.HashWalkinPassword("abcd1234", "local-test-secret"));
    }

    [Fact]
    public void SumCart_UsesGrossThenDiscountLikePhp()
    {
        var lines = CpPosWriteService.ParseLines(
        [
            new("Pad", 2, 10m, LineDiscountAmt: 1.50m),
            new("Oil", 1, 8.5m),
        ]);
        var cart = CpPosWriteService.SumCart(lines);
        Assert.Equal(28.50m, cart.SubtotalEx);
        Assert.Equal(1.50m, cart.DiscountTotal);
        Assert.Equal(27.00m, cart.AmountEx);
    }

    [Fact]
    public void BuildSoLinesJson_UsesPhpFieldNames()
    {
        var lines = CpPosWriteService.ParseLines([new("Wiper", 2, 10m)]);
        var json = CpPosWriteService.BuildSoLinesJson(lines);
        Assert.Contains("\"description\":\"Wiper\"", json, StringComparison.Ordinal);
        Assert.Contains("\"unit_price_ex_vat\":", json, StringComparison.Ordinal);
        Assert.Contains("\"line_ex_vat\":", json, StringComparison.Ordinal);
        Assert.Contains("\"qty\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PickDefaultCashAndCardAccounts_MatchPhpNameAndTypeRules()
    {
        var accounts = new[]
        {
            new CpPosCashAccountHint(3, "Main till", "cash"),
            new CpPosCashAccountHint(8, "Card terminal", "bank"),
        };
        Assert.Equal(11, CpPosWriteService.PickDefaultCashAccount(11, accounts));
        Assert.Equal(3, CpPosWriteService.PickDefaultCashAccount(0, accounts));
        Assert.Equal(8, CpPosWriteService.PickDefaultCardAccount(0, accounts, 3));
        Assert.Equal(9, CpPosWriteService.PickDefaultCardAccount(9, accounts, 3));
        Assert.Equal(3, CpPosWriteService.PickDefaultCardAccount(0, [new(3, "Main till", "cash")], 3));
        Assert.Equal(0, CpPosWriteService.PickDefaultCashAccount(0, []));
    }

    [Fact]
    public void PickWarehouseId_UsesPayloadThenSettingsThenFirstActive()
    {
        Assert.Equal(9, CpPosWriteService.PickWarehouseId(9, 3, 1));
        Assert.Equal(3, CpPosWriteService.PickWarehouseId(0, 3, 1));
        Assert.Equal(1, CpPosWriteService.PickWarehouseId(0, 0, 1));
        Assert.Equal(0, CpPosWriteService.PickWarehouseId(0, 0, 0));
        Assert.False(CpPosWriteService.HasSaleOutSku(""));
        Assert.False(CpPosWriteService.HasSaleOutSku("   "));
        Assert.True(CpPosWriteService.HasSaleOutSku("WIPER-1"));
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
