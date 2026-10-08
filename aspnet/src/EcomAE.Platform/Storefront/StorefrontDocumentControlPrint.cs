using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string DocumentControlPrintPath = "/content/shop/document_control/service/print.php";

    /// <summary>The query PHP <c>document_control/service/print.php</c> reads; null when the parameter is absent.</summary>
    public sealed record DocumentControlPrintRequest(string? Doc, string? OrderId, string? InvoiceId, string? Preview, ErpUserAccess.Cookies Cookies);

    /// <summary>
    /// PHP <c>content/shop/document_control/service/print.php</c>: an admin, backend-group or ERP user (403 otherwise) gets the
    /// Document Control template rendered for the invoice, the order or the preview context; a render failure is a 400 with the message.
    /// </summary>
    public static async Task<RawHttp> PrintDocumentControlAsync(DbConnection connection, DocumentControlPrintRequest request, CancellationToken cancellationToken)
    {
        if (!await ErpUserAccess.CanPrintDocumentsAsync(connection, request.Cookies, cancellationToken).ConfigureAwait(false))
        {
            return new RawHttp("Access denied — sign in to ERP or the control panel.", PrintHtmlType, 403);
        }

        var doc = (request.Doc ?? "fta_tax_invoice").Trim();
        var orderId = ShopPayForOrderService.PhpIntCast(request.OrderId);
        var invoiceId = ShopPayForOrderService.PhpIntCast(request.InvoiceId);
        var preview = !string.IsNullOrEmpty(request.Preview) && request.Preview != "0";
        try
        {
            var html = await ErpDocumentControlRender.RenderTemplateAsync(
                connection,
                doc,
                preview ? 0 : orderId,
                !preview && invoiceId > 0 ? invoiceId : 0,
                cancellationToken).ConfigureAwait(false);
            return new RawHttp(html, PrintHtmlType);
        }
        catch (Exception ex) when (ex is ErpWriteException or DbException)
        {
            return new RawHttp("<p>" + ErpDocumentControlRender.H(ex.Message) + "</p>", PrintHtmlType, 400);
        }
    }
}
