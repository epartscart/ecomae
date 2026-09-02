namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP CMS twins linked from every live tenant package
/// (<c>/kontakty</c>, <c>/o-dostavke</c>, <c>/ob-oplate</c>, <c>/o-vozvrate</c>).
/// Copy is industry-scoped so jewellery/fashion/tax never inherit auto-parts wording.
/// </summary>
public static class PhpIndustryCmsPages
{
    public sealed record Page(string Slug, string Title, string Lead, IReadOnlyList<string> Paragraphs);

    public static readonly string[] Slugs = ["kontakty", "o-dostavke", "ob-oplate", "o-vozvrate"];

    public static bool IsSlug(string? path)
    {
        var slug = NormalizeSlug(path);
        return Slugs.Contains(slug, StringComparer.OrdinalIgnoreCase);
    }

    public static Page Resolve(string? path, string industryCode)
    {
        var slug = NormalizeSlug(path);
        var industry = string.IsNullOrWhiteSpace(industryCode) ? "auto_parts" : industryCode.Trim().ToLowerInvariant();
        return slug switch
        {
            "o-dostavke" => Delivery(industry),
            "ob-oplate" => Payment(industry),
            "o-vozvrate" => Returns(industry),
            _ => Contact(industry),
        };
    }

    public static string NormalizeSlug(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "kontakty";
        }

        var value = path.Trim().Trim('/');
        var q = value.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            value = value[..q];
        }

        return value.ToLowerInvariant();
    }

    private static Page Contact(string industry) => industry switch
    {
        "electronics" => new(
            "kontakty",
            "Help & contact",
            "Electronicae — tech, gaming and electronics support in the UAE.",
            [
                "Questions about an order, warranty, or a product on the site — write to us and the store team will reply.",
                "Store locator and gift-card help use this same contact form. Prices stay in AED.",
            ]),
        "fashion" => new(
            "kontakty",
            "Help & contact",
            "Style N Look — fashion and beauty customer care in the UAE.",
            [
                "Sizing, delivery, or an order update — send a message and customer care will get back to you.",
                "Ring the store locator from this page if you prefer to visit. Prices stay in AED.",
            ]),
        "jewellery" => new(
            "kontakty",
            "Help & contact",
            "The Jewellery Trend — ring sizing, certification, and store help.",
            [
                "Need a ring size, gold certificate, or help with a bridal set? Contact the boutique team here.",
                "Insured delivery questions also go through this page. Prices stay in AED.",
            ]),
        "tax_advisory" => new(
            "kontakty",
            "Contact an advisor",
            "Taxofinca — corporate tax, VAT, and business advisory in the UAE.",
            [
                "Book a consultation for VAT registration, corporate tax filing, audit, or company formation.",
                "Client ERP and staff control panel stay on the same host after you sign in.",
            ]),
        _ => new(
            "kontakty",
            "Contact us",
            "eParts Cart — spare-parts support for VIN, orders, and delivery.",
            [
                "Need help with a part number, VIN request, or an existing order? Send a message and the parts desk will reply.",
                "Warehouse pickup and courier questions use the same contact page.",
            ]),
    };

    private static Page Delivery(string industry) => industry switch
    {
        "electronics" => new(
            "o-dostavke",
            "Delivery information",
            "UAE delivery on electronics — free over AED 200.",
            [
                "Orders ship from the UAE warehouse. Standard delivery covers the main emirates; remote areas may take longer.",
                "Track the shipment from My orders after checkout. Official warranty stays with the product card.",
            ]),
        "fashion" => new(
            "o-dostavke",
            "Delivery information",
            "UAE fashion and beauty delivery.",
            [
                "Most Style N Look orders ship within the UAE. Delivery windows show at checkout.",
                "Track the parcel from My orders. Gift sets and beauty items ship in branded packaging.",
            ]),
        "jewellery" => new(
            "o-dostavke",
            "Delivery & insurance",
            "Insured UAE jewellery delivery — free on orders over AED 500.",
            [
                "Fine jewellery ships insured. Signature may be required on arrival.",
                "Track the shipment from My orders. Bridal sets can be reserved for boutique collection.",
            ]),
        "tax_advisory" => new(
            "o-dostavke",
            "How we work",
            "Advisory delivery — documents, filings, and client ERP.",
            [
                "Engagements are delivered as filings, reports, and working papers — not physical parcels.",
                "Signed-in clients follow progress in Client ERP. Staff use the control panel on this same host.",
            ]),
        _ => new(
            "o-dostavke",
            "About delivery",
            "Courier and warehouse pickup for spare parts.",
            [
                "Choose courier delivery or warehouse pickup at checkout. Lead times follow the supplier row on the search result.",
                "Track the order from My orders. Pickup offices are listed under How to get.",
            ]),
    };

    private static Page Payment(string industry) => industry switch
    {
        "electronics" => new(
            "ob-oplate",
            "Payment & refunds",
            "Cards, Apple Pay, Tabby and Tamara — prices in AED.",
            [
                "Pay with Visa, Mastercard, Apple Pay, Tabby, or Tamara at checkout.",
                "Refunds follow the returns window on the product. Warranty claims stay with the brand service centre.",
            ]),
        "fashion" => new(
            "ob-oplate",
            "Payment & refunds",
            "Cards and split payments — prices in AED.",
            [
                "Pay with Visa, Mastercard, Apple Pay, Tabby, or Tamara.",
                "Unused beauty sets and fashion items follow the returns page. Sale items may be final.",
            ]),
        "jewellery" => new(
            "ob-oplate",
            "Payment",
            "Secure card and split payments — prices in AED.",
            [
                "Pay with Visa, Mastercard, Apple Pay, Tabby, or Tamara. High-value pieces may require a deposit.",
                "Gold weight and diamond certificates stay with the invoice.",
            ]),
        "tax_advisory" => new(
            "ob-oplate",
            "Fees & retainers",
            "Fixed fees and monthly retainers — quoted in AED.",
            [
                "Service cards show the published fee. Complex groups receive a written engagement letter.",
                "Monthly bookkeeping and CFO retainers invoice on the schedule in Client ERP.",
            ]),
        _ => new(
            "ob-oplate",
            "About payment",
            "Cards and account terms on spare-parts orders.",
            [
                "Guests pay at checkout. Approved customers may use account terms shown on the price row.",
                "Invoices stay under My orders. Bank transfer references use the order number.",
            ]),
    };

    private static Page Returns(string industry) => industry switch
    {
        "electronics" => new(
            "o-vozvrate",
            "Returns & refunds",
            "Unused electronics can be returned within the stated window.",
            [
                "Keep the original box and serial labels. Opened software or personalised items are not returnable.",
                "Start a return from My orders or the contact page. Refunds go back to the original payment method.",
            ]),
        "fashion" => new(
            "o-vozvrate",
            "Returns",
            "Unworn fashion and unopened beauty — easy UAE returns.",
            [
                "Items must be unused with tags attached. Opened cosmetics and pierced jewellery are final sale.",
                "Start a return from My orders. Refunds go back to the original payment method.",
            ]),
        "jewellery" => new(
            "o-vozvrate",
            "Returns",
            "Easy returns within 14 days on unused pieces.",
            [
                "Pieces must be unused with certificates attached. Custom bridal sets follow the engagement letter.",
                "Start a return from My orders or the boutique contact page.",
            ]),
        "tax_advisory" => new(
            "o-vozvrate",
            "Engagement changes",
            "Scope changes and cancellations.",
            [
                "Filed returns cannot be reversed. Unused retainer hours can be reallocated on the next invoice.",
                "Write to the advisor from the contact page before the filing deadline.",
            ]),
        _ => new(
            "o-vozvrate",
            "Returns",
            "Spare-parts returns follow the supplier and warehouse rules.",
            [
                "Electrical parts and opened fluids are often non-returnable. Check the line before you order.",
                "Open a return from My orders. Credit notes appear on the same account.",
            ]),
    };
}
