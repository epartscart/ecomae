using System.Data.Common;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

public sealed record ErpManualInvoiceLineInput(
    string? ItemName,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate = 5m);

public sealed record ErpManualInvoiceWriteRequest(
    long Id,
    long UserId,
    string? InvoiceNumber,
    string? CurrencyCode,
    string? SellerJson,
    string? BuyerJson,
    string? LinesJson,
    string? PaymentTerms,
    string? DueDate,
    int AdminId);

public sealed record ErpManualInvoiceWriteResult(
    long InvoiceId,
    string InvoiceNumber,
    decimal SubtotalExVat,
    decimal TotalVat,
    decimal TotalInclVat);

public interface IErpManualInvoiceWriteService
{
    Task<ErpManualInvoiceWriteResult> SaveAsync(
        ErpManualInvoiceWriteRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Live create path for the PHP manual invoice save contract; edits remain PHP-backed until parity is complete.</summary>
public sealed class ErpManualInvoiceWriteService : IErpManualInvoiceWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVoucherNumberService _vouchers;

    public ErpManualInvoiceWriteService(
        IErpWriteConnectionFactory connections,
        IErpVoucherNumberService vouchers)
    {
        _connections = connections;
        _vouchers = vouchers;
    }

    public async Task<ErpManualInvoiceWriteResult> SaveAsync(
        ErpManualInvoiceWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id > 0)
        {
            throw new ErpWriteException("Invoice edits remain on the PHP reference path.");
        }

        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }

        var lines = ParseLines(request.LinesJson);
        if (lines.Count == 0)
        {
            throw new ErpWriteException("Add at least one line item");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var invoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber)
            ? await _vouchers.NextAsync(connection, null, "SI", cancellationToken).ConfigureAwait(false)
            : request.InvoiceNumber.Trim();
        var currency = Clip(request.CurrencyCode, 8);
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var subtotal = Round(lines.Sum(line => line.Quantity * line.UnitPrice));
        var totalVat = Round(lines.Sum(line => line.Quantity * line.UnitPrice * line.TaxRate / 100m));
        var totalIncl = Round(subtotal + totalVat);
        var issueDate = now;
        var dueDate = ParseDate(request.DueDate, now + 30 * 86400L);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_einvoice_documents` (`uuid`,`invoice_number`,`user_id`,`doc_category`,`invoice_type_code`,`issue_date`,`payment_due_date`,`vat_point_date`,`currency_code`,`vat_currency_code`,`payment_terms`,`seller_json`,`buyer_json`,`subtotal_ex_vat`,`total_vat`,`total_incl_vat`,`amount_due`,`status`,`validation_ok`,`validation_errors_json`,`xml_content`,`time_created`,`time_updated`,`admin_id`,`active`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,'draft',0,'[]','',?,?,?,1)"),
            cancellationToken,
            Guid.NewGuid().ToString("D"),
            invoiceNumber,
            request.UserId > 0 ? request.UserId : 0,
            "tax_invoice",
            "380",
            issueDate,
            dueDate,
            issueDate,
            currency,
            currency,
            Clip(request.PaymentTerms, 255),
            request.SellerJson ?? "{}",
            request.BuyerJson ?? "{}",
            subtotal,
            totalVat,
            totalIncl,
            totalIncl,
            now,
            now,
            request.AdminId).ConfigureAwait(false);
        var invoiceId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        var lineNo = 1;
        foreach (var line in lines)
        {
            var net = Round(line.Quantity * line.UnitPrice);
            var vat = Round(net * line.TaxRate / 100m);
            var gross = Round(net + vat);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_einvoice_lines` (`document_id`,`line_no`,`item_name`,`item_type`,`quantity`,`uom_code`,`unit_price`,`line_net`,`tax_category`,`tax_rate`,`tax_amount`,`gross_amount`,`vat_line_aed`,`line_amount_aed`) VALUES (?,?,?,'G',?,'C62',?,?, 'S',?,?,?,?,?)"),
                cancellationToken,
                invoiceId,
                lineNo++,
                Clip(line.ItemName, 255),
                line.Quantity,
                line.UnitPrice,
                net,
                line.TaxRate,
                vat,
                gross,
                vat,
                gross).ConfigureAwait(false);
        }

        return new(invoiceId, invoiceNumber, subtotal, totalVat, totalIncl);
    }

    private static List<ErpManualInvoiceLineInput> ParseLines(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ErpManualInvoiceLineInput>>(json) ?? [];
        }
        catch (JsonException)
        {
            throw new ErpWriteException("Invalid invoice lines");
        }
    }

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS `epc_einvoice_documents` (`id` int NOT NULL AUTO_INCREMENT, `uuid` char(36) NOT NULL, `invoice_number` varchar(64) NOT NULL, `order_id` int NOT NULL DEFAULT 0, `user_id` int NOT NULL DEFAULT 0, `doc_category` varchar(32) NOT NULL DEFAULT 'tax_invoice', `invoice_type_code` varchar(8) NOT NULL DEFAULT '380', `issue_date` int NOT NULL DEFAULT 0, `payment_due_date` int NOT NULL DEFAULT 0, `vat_point_date` int NOT NULL DEFAULT 0, `currency_code` varchar(8) NOT NULL DEFAULT 'AED', `vat_currency_code` varchar(8) NOT NULL DEFAULT 'AED', `payment_terms` varchar(255) DEFAULT NULL, `seller_json` mediumtext, `buyer_json` mediumtext, `subtotal_ex_vat` decimal(14,2) NOT NULL DEFAULT 0, `total_vat` decimal(14,2) NOT NULL DEFAULT 0, `total_incl_vat` decimal(14,2) NOT NULL DEFAULT 0, `paid_amount` decimal(14,2) NOT NULL DEFAULT 0, `amount_due` decimal(14,2) NOT NULL DEFAULT 0, `status` varchar(32) NOT NULL DEFAULT 'draft', `validation_ok` tinyint NOT NULL DEFAULT 0, `validation_errors_json` text, `xml_content` mediumtext, `time_created` int NOT NULL DEFAULT 0, `time_updated` int NOT NULL DEFAULT 0, `admin_id` int NOT NULL DEFAULT 0, `active` tinyint NOT NULL DEFAULT 1, PRIMARY KEY (`id`), UNIQUE KEY `x_uuid` (`uuid`), UNIQUE KEY `x_invoice_no` (`invoice_number`))", cancellationToken);
        await ErpDb.TryExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS `epc_einvoice_lines` (`id` int NOT NULL AUTO_INCREMENT, `document_id` int NOT NULL, `line_no` int NOT NULL DEFAULT 1, `item_name` varchar(255) NOT NULL, `item_type` varchar(4) NOT NULL DEFAULT 'G', `quantity` decimal(14,4) NOT NULL DEFAULT 0, `uom_code` varchar(16) NOT NULL DEFAULT 'C62', `unit_price` decimal(14,4) NOT NULL DEFAULT 0, `line_net` decimal(14,2) NOT NULL DEFAULT 0, `tax_category` varchar(8) NOT NULL DEFAULT 'S', `tax_rate` decimal(5,2) NOT NULL DEFAULT 5, `tax_amount` decimal(14,2) NOT NULL DEFAULT 0, `gross_amount` decimal(14,2) NOT NULL DEFAULT 0, `vat_line_aed` decimal(14,2) NOT NULL DEFAULT 0, `line_amount_aed` decimal(14,2) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `x_doc` (`document_id`))", cancellationToken);
    }

    private static long ParseDate(string? value, long fallback)
        => DateTimeOffset.TryParse(value, out var parsed) ? parsed.ToUnixTimeSeconds() : fallback;

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
