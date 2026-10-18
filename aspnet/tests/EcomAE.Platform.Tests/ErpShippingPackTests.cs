using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpShippingPackTests
{
    [Fact]
    public void Several_invoices_become_one_shipping_invoice_and_one_packing_slip()
    {
        var built = ErpShippingPack.Sample();
        Assert.Equal(2, built.Invoices.Count);
        Assert.Equal(2, built.Lines.Count);
        Assert.Equal(262.50m, built.Gross);
        Assert.Equal(250m, built.Net);
        Assert.Equal(12.50m, built.Vat);
        Assert.Equal(3m, built.Quantity);
        Assert.Equal(7, built.CustomerId);
        Assert.Contains(built.Lines, line => line.InvoiceNumber == "SI-100" && line.Qty == 2m);
        Assert.Contains(built.Lines, line => line.InvoiceNumber == "SI-101" && line.Item == "Belt");
        Assert.Empty(ErpShippingPack.Refuse([100, 101], built.Invoices, new HashSet<long>()));
    }

    [Fact]
    public void One_invoice_and_two_customers_are_refused()
    {
        var one = ErpShippingPack.Refuse([100], [Invoice(100, 7)], new HashSet<long>());
        Assert.Contains("at least two", one[0], StringComparison.OrdinalIgnoreCase);

        var split = ErpShippingPack.Refuse(
            [100, 101],
            [Invoice(100, 7), Invoice(101, 8)],
            new HashSet<long>());
        Assert.Contains("another customer", split[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Draft_cancelled_credit_note_and_a_second_pack_are_refused()
    {
        var draft = Invoice(100, 7) with { Status = "draft" };
        var live = Invoice(101, 7);
        Assert.Contains("draft", ErpShippingPack.Refuse([100, 101], [draft, live], new HashSet<long>())[0], StringComparison.OrdinalIgnoreCase);

        var cancelled = Invoice(100, 7) with { Status = "cancelled" };
        Assert.Contains("cancelled", ErpShippingPack.Refuse([100, 101], [cancelled, live], new HashSet<long>())[0], StringComparison.OrdinalIgnoreCase);

        var credit = Invoice(100, 7) with { Category = "tax_credit_note" };
        Assert.Contains("customer invoice", ErpShippingPack.Refuse([100, 101], [credit, live], new HashSet<long>())[0], StringComparison.OrdinalIgnoreCase);

        var packed = ErpShippingPack.Refuse([100, 101], [Invoice(100, 7), live], new HashSet<long> { 100 });
        Assert.Contains("already on a shipping pack", packed[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Currencies_must_match_and_a_line_less_invoice_still_ships()
    {
        var dirham = Invoice(100, 7);
        var dollar = Invoice(101, 7) with { Currency = "USD" };
        Assert.Contains("USD", ErpShippingPack.Refuse([100, 101], [dirham, dollar], new HashSet<long>())[0], StringComparison.Ordinal);

        var bare = Invoice(102, 7) with { Lines = [], Net = 40m, Vat = 2m, Gross = 42m, Number = "SI-102" };
        var built = ErpShippingPack.Compose([dirham, bare]);
        Assert.Equal(2, built.Lines.Count);
        Assert.Equal("Shipped as invoiced", built.Lines[1].Description);
        Assert.Equal(252m, built.Gross);
    }

    [Fact]
    public void Invoice_page_prepares_the_shipping_pack()
    {
        var root = RepoRoot();
        var invoices = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpInvoicesApp.razor"));
        var notes = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpDeliveryNotesApp.razor"));
        var panel = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpShippingPackPanel.razor"));
        var routes = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        var service = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Erp/ErpShippingPackService.cs"));
        Assert.Contains("ErpShippingPackPanel", invoices, StringComparison.Ordinal);
        Assert.Contains("shipping-documents", notes, StringComparison.Ordinal);
        Assert.Contains("Prepare shipping documents", panel, StringComparison.Ordinal);
        Assert.Contains("Packing slip", panel, StringComparison.Ordinal);
        Assert.Contains("/erp/invoices/shipping-pack", routes, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO `epc_einvoice_documents`", service, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", panel, StringComparison.Ordinal);
    }

    private static ErpShippingPack.Source Invoice(long id, long customer)
        => new(id, customer, "SI-" + id.ToString(System.Globalization.CultureInfo.InvariantCulture), "AED", "validated", "tax_invoice", true, 200m, 10m, 210m, "Harbour Trading",
            [new ErpShippingPack.Line(id, "SI-" + id, 1, "Filter", "Oil filter", 2m, "C62", 100m, 200m, 5m, 10m, 210m)]);

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
