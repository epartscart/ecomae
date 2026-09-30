using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

public interface IErpEinvoiceCreditNoteWriteService
{
    Task<ErpEinvoiceCreditNoteWriteResult> CreateAsync(
        ErpEinvoiceCreditNoteWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpEinvoiceCreditNoteWriteRequest(
    long OriginalDocumentId = 0,
    string? Reason = null,
    int AdminId = 0);

public sealed record ErpEinvoiceCreditNoteWriteResult(
    bool Succeeded,
    string Code,
    string Message,
    long Id,
    string? CreditNoteNumber,
    decimal SubtotalExVat,
    decimal TotalVat,
    decimal TotalInclVat,
    int Lines,
    int Writes)
{
    public static ErpEinvoiceCreditNoteWriteResult Fail(string code, string message, long id = 0)
        => new(false, code, message, id, null, 0, 0, 0, 0, 0);
}

/// <summary>
/// Live port of PHP epc_einvoice_create_credit_note for full-reversal type 381 notes.
/// The PHP-owned e-invoice schema is required and is never created by this service.
/// </summary>
public sealed class ErpEinvoiceCreditNoteWriteService : IErpEinvoiceCreditNoteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpEinvoiceCreditNoteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpEinvoiceCreditNoteWriteResult> CreateAsync(
        ErpEinvoiceCreditNoteWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OriginalDocumentId <= 0)
        {
            return ErpEinvoiceCreditNoteWriteResult.Fail("invalid", "Original document id must be positive.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpEinvoiceCreditNoteWriteResult.Fail("not_configured", "Tenant database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var tables = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                SELECT COUNT(*)
                FROM information_schema.tables
                WHERE table_schema = DATABASE()
                  AND table_name IN ('epc_einvoice_documents','epc_einvoice_lines','epc_einvoice_events')
                """),
            cancellationToken).ConfigureAwait(false);
        if (tables != 3)
        {
            return ErpEinvoiceCreditNoteWriteResult.Fail("not_provisioned", "The PHP e-invoice tables are unavailable.");
        }

        var original = await ReadDocumentAsync(connection, request.OriginalDocumentId, cancellationToken)
            .ConfigureAwait(false);
        if (original is null)
        {
            return ErpEinvoiceCreditNoteWriteResult.Fail("not_found", "Original invoice not found.");
        }

        if (original.Lines.Count == 0)
        {
            return ErpEinvoiceCreditNoteWriteResult.Fail("invalid", "Original invoice has no lines.", request.OriginalDocumentId);
        }

        var reason = Clip(string.IsNullOrWhiteSpace(request.Reason) ? "Sales return" : request.Reason.Trim(), 255);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lineRecords = original.Lines.Select((line, index) => new CreditLine(
            index + 1,
            line.ItemName,
            Clip((line.ItemDescription + " [Credit for " + original.InvoiceNumber + "]").Trim(), 512),
            line.Quantity,
            line.UomCode,
            line.UnitPrice,
            line.TaxCategory,
            line.TaxRate,
            Round2(line.Quantity * line.UnitPrice),
            Round2(Round2(line.Quantity * line.UnitPrice) * line.TaxRate / 100m))).ToList();
        var subtotal = Round2(lineRecords.Sum(line => line.LineNet));
        var totalVat = Round2(lineRecords.Sum(line => line.TaxAmount));
        var total = Round2(subtotal + totalVat);
        var taxBreakdown = JsonSerializer.Serialize(new[]
        {
            new
            {
                tax_category = "S",
                tax_rate = 5m,
                taxable_amount = subtotal.ToString("0.00", CultureInfo.InvariantCulture),
                tax_amount = totalVat.ToString("0.00", CultureInfo.InvariantCulture),
            },
        });

        var sequence = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) + 1 FROM `epc_einvoice_documents` WHERE `invoice_type_code` = '381'"),
            cancellationToken).ConfigureAwait(false);
        var number = "CN-" + original.InvoiceNumber + "-" + sequence.ToString("D3", CultureInfo.InvariantCulture);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_einvoice_documents`
                        (`uuid`,`invoice_number`,`order_id`,`user_id`,`doc_category`,`invoice_type_code`,
                         `issue_date`,`payment_due_date`,`vat_point_date`,`currency_code`,`vat_currency_code`,
                         `payment_means_code`,`payment_terms`,`seller_json`,`buyer_json`,
                         `subtotal_ex_vat`,`total_vat`,`total_incl_vat`,`amount_due`,
                         `tax_breakdown_json`,`status`,`validation_ok`,`admin_id`,`time_created`,`time_updated`)
                    VALUES (?,?,?,?,?,'381',?,?,?,'AED','AED',?,?,?,?,?,?,?,?,?,'draft',0,?,?,?)
                    """),
                cancellationToken,
                Guid.NewGuid().ToString("D"),
                number,
                original.OrderId,
                original.UserId,
                "tax_credit_note",
                now,
                now,
                now,
                original.PaymentMeansCode,
                "Credit note for " + original.InvoiceNumber + ": " + reason,
                original.SellerJson,
                original.BuyerJson,
                subtotal,
                totalVat,
                total,
                total,
                taxBreakdown,
                request.AdminId,
                now,
                now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            foreach (var line in lineRecords)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        INSERT INTO `epc_einvoice_lines`
                            (`document_id`,`line_no`,`item_name`,`item_description`,`quantity`,`uom_code`,
                             `unit_price`,`line_net`,`tax_category`,`tax_rate`,`tax_amount`,`gross_amount`,
                             `vat_line_aed`,`line_amount_aed`)
                        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                        """),
                    cancellationToken,
                    id,
                    line.LineNo,
                    line.ItemName,
                    line.ItemDescription,
                    line.Quantity,
                    line.UomCode,
                    line.UnitPrice,
                    line.LineNet,
                    line.TaxCategory,
                    line.TaxRate,
                    line.TaxAmount,
                    Round2(line.LineNet + line.TaxAmount),
                    line.TaxAmount,
                    line.LineNet).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_einvoice_events`
                        (`document_id`,`event_type`,`status`,`message`,`payload_json`,`time_created`)
                    VALUES (?, 'created', 'draft', ?, ?, ?)
                    """),
                cancellationToken,
                id,
                "Credit note created for invoice " + original.InvoiceNumber + ": " + reason,
                JsonSerializer.Serialize(new
                {
                    original_doc_id = request.OriginalDocumentId,
                    original_invoice = original.InvoiceNumber,
                }),
                now).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new(true, "ok", "Credit note " + number + " created.", id, number, subtotal, totalVat, total, lineRecords.Count, 3 + lineRecords.Count);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<OriginalDocument?> ReadDocumentAsync(
        DbConnection connection,
        long id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            """
            SELECT `invoice_number`,`order_id`,`user_id`,`payment_means_code`,
                   COALESCE(`seller_json`,''),COALESCE(`buyer_json`,'')
            FROM `epc_einvoice_documents`
            WHERE `id` = ? AND `active` = 1
            LIMIT 1
            """);
        ErpDb.AddParameters(command, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var original = new OriginalDocument(
            Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
            Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
            Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture) ?? string.Empty,
            []);
        await reader.CloseAsync().ConfigureAwait(false);

        await using var linesCommand = connection.CreateCommand();
        linesCommand.CommandText = ErpDb.Positional(
            """
            SELECT `item_name`,COALESCE(`item_description`,''),`quantity`,`uom_code`,
                   `unit_price`,`tax_category`,`tax_rate`
            FROM `epc_einvoice_lines`
            WHERE `document_id` = ?
            ORDER BY `line_no`
            """);
        ErpDb.AddParameters(linesCommand, id);
        await using var linesReader = await linesCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await linesReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            original.Lines.Add(new OriginalLine(
                Convert.ToString(linesReader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(linesReader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToDecimal(linesReader.GetValue(2), CultureInfo.InvariantCulture),
                Convert.ToString(linesReader.GetValue(3), CultureInfo.InvariantCulture) ?? "C62",
                Convert.ToDecimal(linesReader.GetValue(4), CultureInfo.InvariantCulture),
                Convert.ToString(linesReader.GetValue(5), CultureInfo.InvariantCulture) ?? "S",
                Convert.ToDecimal(linesReader.GetValue(6), CultureInfo.InvariantCulture)));
        }

        return original;
    }

    private static decimal Round2(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private sealed record OriginalDocument(
        string InvoiceNumber,
        long OrderId,
        long UserId,
        string PaymentMeansCode,
        string SellerJson,
        string BuyerJson,
        List<OriginalLine> Lines);

    private sealed record OriginalLine(
        string ItemName,
        string ItemDescription,
        decimal Quantity,
        string UomCode,
        decimal UnitPrice,
        string TaxCategory,
        decimal TaxRate);

    private sealed record CreditLine(
        int LineNo,
        string ItemName,
        string ItemDescription,
        decimal Quantity,
        string UomCode,
        decimal UnitPrice,
        string TaxCategory,
        decimal TaxRate,
        decimal LineNet,
        decimal TaxAmount);
}
