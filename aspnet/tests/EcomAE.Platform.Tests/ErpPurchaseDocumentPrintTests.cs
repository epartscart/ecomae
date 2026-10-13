using EcomAE.Platform.Erp;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPurchaseDocumentPrintTests
{
    [Fact]
    public void OfficialPo_IsApprovedPartialReceived_NotDraft()
    {
        Assert.True(ErpPurchaseDocumentPrint.IsOfficialPo("approved"));
        Assert.True(ErpPurchaseDocumentPrint.IsOfficialPo("partial"));
        Assert.True(ErpPurchaseDocumentPrint.IsOfficialPo("received"));
        Assert.False(ErpPurchaseDocumentPrint.IsOfficialPo("draft"));
        Assert.False(ErpPurchaseDocumentPrint.IsOfficialPo("cancelled"));
    }

    [Fact]
    public void OfficialPi_IsPostedUnlessDraftOrVoid()
    {
        Assert.True(ErpPurchaseDocumentPrint.IsOfficialPi("confirmed"));
        Assert.True(ErpPurchaseDocumentPrint.IsOfficialPi("posted"));
        Assert.False(ErpPurchaseDocumentPrint.IsOfficialPi("draft"));
        Assert.False(ErpPurchaseDocumentPrint.IsOfficialPi("voided"));
        Assert.True(ErpPurchaseDocumentPrint.IsOfficialJewellery("posted", false));
        Assert.True(ErpPurchaseDocumentPrint.IsOfficialJewellery("draft", true));
        Assert.False(ErpPurchaseDocumentPrint.IsOfficialJewellery("draft", false));
    }

    [Fact]
    public void PostedPurchaseInvoice_MatchesJewelleryLetterhead()
    {
        var html = ErpPurchaseDocumentPrint.Render(Sample(ErpPurchasePrintKind.PurchaseInvoice, official: true, metal: false));
        Assert.Contains("TAX INVOICE / PURCHASE", html, StringComparison.Ordinal);
        Assert.Contains("ACCOUNT'S COPY", html, StringComparison.Ordinal);
        Assert.Contains("INDUS JEWELLERS", html, StringComparison.Ordinal);
        Assert.Contains("TRN: 100123456789012", html, StringComparison.Ordinal);
        Assert.Contains("Rizan Jewellery", html, StringComparison.Ordinal);
        Assert.Contains("Amount in words:", html, StringComparison.Ordinal);
        Assert.Contains("UAE Dirhams", html, StringComparison.Ordinal);
        Assert.Contains("PARTY'S SIGNATURE", html, StringComparison.Ordinal);
        Assert.Contains("data-official=\"1\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("DRAFT", html, StringComparison.Ordinal);
        Assert.DoesNotContain("password", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$2y$", html, StringComparison.Ordinal);
    }

    [Fact]
    public void MetalPurchase_UsesWeightColumns()
    {
        var html = ErpPurchaseDocumentPrint.Render(Sample(ErpPurchasePrintKind.MetalPurchase, official: true, metal: true));
        Assert.Contains("TAX INVOICE / METAL PURCHASE (FIXED)", html, StringComparison.Ordinal);
        Assert.Contains("Gross wt", html, StringComparison.Ordinal);
        Assert.Contains("Purity", html, StringComparison.Ordinal);
        Assert.Contains("24K BAR", html, StringComparison.Ordinal);
        Assert.Contains("Metal rate", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DraftPo_WatermarksAndExplainsNotPosted()
    {
        var html = ErpPurchaseDocumentPrint.Render(Sample(ErpPurchasePrintKind.PurchaseOrder, official: false, metal: false));
        Assert.Contains("PURCHASE ORDER", html, StringComparison.Ordinal);
        Assert.Contains("DRAFT", html, StringComparison.Ordinal);
        Assert.Contains("not posted yet", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-official=\"0\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void LpoTitle_IsLocalPurchaseOrder()
    {
        var html = ErpPurchaseDocumentPrint.Render(Sample(ErpPurchasePrintKind.Lpo, official: true, metal: false));
        Assert.Contains("LOCAL PURCHASE ORDER", html, StringComparison.Ordinal);
        Assert.Contains("LPO NO", html, StringComparison.Ordinal);
        Assert.Contains("LPO → PO → PI", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RoutesAndPagesAreWired()
    {
        Assert.Equal("/erp/purchase-order-print", EcomAeRoutes.ErpPurchaseOrderPrint);
        Assert.Equal("/erp/purchase-print", EcomAeRoutes.ErpPurchasePrint);
        Assert.Equal("/erp/lpo-print", EcomAeRoutes.ErpLpoPrint);

        var po = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPurchaseOrdersApp.razor"));
        Assert.Contains("Print PO", po, StringComparison.Ordinal);
        Assert.Contains("/erp/lpo-print", po, StringComparison.Ordinal);
        Assert.Contains("LPO → PO → PI", po, StringComparison.Ordinal);

        var pi = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPurchasesApp.razor"));
        Assert.Contains("Print PI", pi, StringComparison.Ordinal);
        Assert.Contains("/erp/purchase-print", pi, StringComparison.Ordinal);

        var jw = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryFixingApp.razor"));
        Assert.Contains("/erp/purchase-print?jwId=", jw, StringComparison.Ordinal);

        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("MapGet(EcomAeRoutes.ErpLpoPrint", module, StringComparison.Ordinal);
        Assert.Contains("RenderJewelleryVoucherAsync", module, StringComparison.Ordinal);
        Assert.DoesNotContain("u.`password`", module, StringComparison.Ordinal);
    }

    private static ErpPurchasePrintDoc Sample(ErpPurchasePrintKind kind, bool official, bool metal)
        => new(
            kind,
            kind == ErpPurchasePrintKind.Lpo ? "LPO-101" : "HO-371433",
            "03/10/2026",
            "Cash",
            "Buyer",
            "AED",
            metal ? "495.940" : string.Empty,
            official,
            official ? "confirmed" : "draft",
            new ErpPurchasePrintParty("", "INDUS JEWELLERS L.L.C", "Al Fahidi, Dubai, UAE", "04 3516668", "accounts@example.com", "100123456789012"),
            new ErpPurchasePrintParty("RJ50012", "Rizan Jewellery L.L.C", "Bur Dubai", "04 1234567", "rizan@example.com", "100398631900003"),
            [
                new ErpPurchasePrintLine(
                    1,
                    metal ? "24K BAR" : "Filter kit",
                    1,
                    48950m,
                    48950m,
                    5,
                    2447.50m,
                    49397.50m,
                    metal ? 100.01m : 0,
                    metal ? 0.999m : 0,
                    metal ? 99.91m : 0,
                    0,
                    0,
                    metal ? 48950m : 0),
            ],
            [],
            48950m,
            2447.50m,
            49080m,
            string.Empty,
            "Purchase process: LPO → PO → PI");

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

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
