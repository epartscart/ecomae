namespace EcomAE.Platform.Presentation;

public sealed record ErpRelatedDocumentNode(
    string Kind,
    string Title,
    string Number,
    string DateLabel,
    string AmountLabel,
    string Href,
    bool IsCurrent,
    bool HasDocument);

/// <summary>SAP-style document relationship map: PO → GRPO → A/P Invoice and SO → Delivery → A/R Invoice.</summary>
public static class ErpRelatedDocumentMapFactory
{
    public static IReadOnlyList<ErpRelatedDocumentNode> ForPurchase(
        string poNumber,
        string poDate,
        string poAmount,
        string poHref,
        IReadOnlyList<ErpRelatedDocumentNode>? receipts = null,
        string? invoiceNumber = null,
        string? invoiceDate = null,
        string? invoiceAmount = null,
        string? invoiceHref = null,
        bool currentIsPurchaseOrder = true,
        bool currentIsInvoice = false)
    {
        var nodes = new List<ErpRelatedDocumentNode>
        {
            Node("purchase_order", "Purchase Order", poNumber, poDate, poAmount, poHref, currentIsPurchaseOrder, Has(poNumber)),
        };

        if (receipts is { Count: > 0 })
        {
            nodes.AddRange(receipts);
        }
        else
        {
            nodes.Add(Node("goods_receipt", "Goods Receipt PO", "", "", "", "/erp/purchase-orders-app", false, false));
        }

        nodes.Add(Node(
            "ap_invoice",
            "A/P Invoice",
            invoiceNumber ?? "",
            invoiceDate ?? "",
            invoiceAmount ?? "",
            string.IsNullOrWhiteSpace(invoiceHref) ? "/erp/invoices-app" : invoiceHref,
            currentIsInvoice,
            Has(invoiceNumber)));
        return nodes;
    }

    public static IReadOnlyList<ErpRelatedDocumentNode> ForSales(
        string soNumber,
        string soDate,
        string soAmount,
        string soHref,
        IReadOnlyList<ErpRelatedDocumentNode>? deliveries = null,
        string? invoiceNumber = null,
        string? invoiceDate = null,
        string? invoiceAmount = null,
        string? invoiceHref = null,
        bool currentIsSalesOrder = true,
        bool currentIsInvoice = false,
        bool currentIsDelivery = false)
    {
        var nodes = new List<ErpRelatedDocumentNode>
        {
            Node("sales_order", "Sales Order", soNumber, soDate, soAmount, soHref, currentIsSalesOrder, Has(soNumber)),
        };

        if (deliveries is { Count: > 0 })
        {
            nodes.AddRange(deliveries);
        }
        else
        {
            nodes.Add(Node("delivery_note", "Delivery", "", "", "", "/erp/delivery-notes-app", currentIsDelivery, false));
        }

        nodes.Add(Node(
            "ar_invoice",
            "A/R Invoice",
            invoiceNumber ?? "",
            invoiceDate ?? "",
            invoiceAmount ?? "",
            string.IsNullOrWhiteSpace(invoiceHref) ? "/erp/invoices-app" : invoiceHref,
            currentIsInvoice,
            Has(invoiceNumber)));
        return nodes;
    }

    public static IReadOnlyList<ErpRelatedDocumentNode> Template(string? documentKind)
    {
        var kind = (documentKind ?? string.Empty).Trim();
        if (kind.Contains("purchase", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("goods receipt", StringComparison.OrdinalIgnoreCase)
            || kind.Equals("A/P Invoice", StringComparison.OrdinalIgnoreCase))
        {
            return ForPurchase("", "", "", "/erp/purchase-orders-app");
        }

        return ForSales("", "", "", "/erp/sales-orders-app");
    }

    public static bool IsPurchaseFlow(string? documentKind)
    {
        var kind = documentKind ?? string.Empty;
        return kind.Contains("purchase", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("goods receipt", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("A/P", StringComparison.OrdinalIgnoreCase);
    }

    public static ErpRelatedDocumentNode Node(
        string kind,
        string title,
        string number,
        string dateLabel,
        string amountLabel,
        string href,
        bool isCurrent,
        bool hasDocument)
        => new(kind, title, number, dateLabel, amountLabel, href, isCurrent, hasDocument);

    private static bool Has(string? number)
        => !string.IsNullOrWhiteSpace(number) && !number.Equals("—", StringComparison.Ordinal);
}
