namespace EcomAE.Platform.Presentation;

/// <summary>
/// Tenant product brochure twin for PHP <c>content/general_pages/epc_epartscart_brochure.php</c>.
/// Host-gated via middleware — Super marketing <c>/brochure</c> on ecomae.com stays separate.
/// </summary>
public static class PhpStorefrontBrochure
{
    public sealed record Section(string Title, string Body);

    public static IReadOnlyList<Section> ForIndustry(string? industryCode)
    {
        var industry = string.IsNullOrWhiteSpace(industryCode) ? "auto_parts" : industryCode.Trim().ToLowerInvariant();
        return industry switch
        {
            "electronics" =>
            [
                new("Electronics retail", "Phones, laptops, gaming, audio, and smart home with AED prices."),
                new("Warranty", "Brand warranty stays on the product card. Store pickup is available when stock shows."),
                new("Pay in the UAE", "Cards, Apple Pay, Tabby, and Tamara at checkout."),
            ],
            "fashion" =>
            [
                new("Fashion & beauty", "Women, men, modest wear, kids, fragrance, and accessories."),
                new("Delivery", "UAE delivery windows show at checkout. Beauty ships in branded packs."),
                new("Returns", "Unworn fashion and unopened beauty follow the returns page."),
            ],
            "jewellery" =>
            [
                new("Fine jewellery", "Hallmark gold, certified diamonds, bridal sets, and watches."),
                new("Insured delivery", "High-value pieces ship insured. Boutique collection is available."),
                new("Certificates", "Gold weight and diamond certificates stay with the invoice."),
            ],
            "tax_advisory" or "consultancy" =>
            [
                new("Advisory", "VAT, corporate tax, audit, bookkeeping, and company formation."),
                new("Client ERP", "Signed-in clients follow filings and retainers on this host."),
                new("Fees", "Published fees are in AED. Complex groups receive an engagement letter."),
            ],
            _ =>
            [
                new("Spare-parts desk", "Article search, VIN / frame, UCats, and warehouse offers."),
                new("Workshops", "Garage notepad, bulk Excel upload, and seller request when the article is unknown."),
                new("Orders", "Courier or warehouse pickup. Invoices stay under My orders."),
            ],
        };
    }
}
