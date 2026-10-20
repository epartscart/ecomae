using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Builds UAE FTA tax invoices from auto-parts sales orders so the tenant can
/// share a compliant invoice (TRN, 5% VAT breakdown) with the customer.
/// Creates the e-invoice document through the shared manual-invoice writer
/// (PINT-AE, tax_invoice/380), links it to the order, and leaves printing to
/// /erp/invoice-print. Mirrors <see cref="ErpFitOutClaimInvoiceWriteService"/>.
/// </summary>
public interface IErpAutoPartsInvoiceWriteService
{
    Task<ErpSimpleWriteResult> CreateFromOrderAsync(long orderId, int actorId, CancellationToken cancellationToken = default);
    Task<string> CreatePendingAsync(int actorId, CancellationToken cancellationToken = default);
}

public sealed class ErpAutoPartsInvoiceWriteService : IErpAutoPartsInvoiceWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpManualInvoiceWriteService _invoices;

    public ErpAutoPartsInvoiceWriteService(
        IErpWriteConnectionFactory connections,
        IErpManualInvoiceWriteService invoices)
    {
        _connections = connections;
        _invoices = invoices;
    }

    public async Task<ErpSimpleWriteResult> CreateFromOrderAsync(long orderId, int actorId, CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A sales-order id is required.");
        }
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpAutoPartsWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `invoice_id` FROM `ecomae_autoparts_invoice_links` WHERE `order_id`=? LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Ok("Invoice already issued for this order.", existing);
        }

        var order = await LoadOrderAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Invoiced sales order not found.");
        }

        var lines = await LoadLinesAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
        if (lines.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("empty", "Sales order has no lines to invoice.");
        }

        var linesJson = JsonSerializer.Serialize(
            lines.Select(l => new ErpManualInvoiceLineInput(l.Description, l.Qty, l.UnitPrice, l.TaxRate, null)).ToArray());
        var buyerJson = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["buyer_name"] = order.PartyName,
            ["buyer_trn"] = "",
            ["buyer_legal_reg_no"] = "",
            ["buyer_legal_reg_type"] = "TL",
            ["buyer_address_line1"] = "United Arab Emirates",
            ["buyer_city"] = "Dubai",
            ["buyer_emirate"] = "Dubai",
            ["buyer_country_code"] = "AE",
            ["buyer_email"] = "",
            ["buyer_peppol_endpoint"] = "0235:9900000098",
        });

        var written = await _invoices.SaveAsync(
            new ErpManualInvoiceWriteRequest(
                Id: 0,
                UserId: 0,
                InvoiceNumber: "AP-INV-" + order.OrderNo,
                CurrencyCode: "AED",
                SellerJson: null,
                BuyerJson: buyerJson,
                LinesJson: linesJson,
                PaymentTerms: "Due on receipt",
                DueDate: null,
                IssueDate: null,
                OrderId: orderId,
                PaidAmount: 0m,
                TransactionTypeCode: null,
                PaymentMeansCode: null,
                BankAccount: null,
                AdminId: actorId),
            cancellationToken).ConfigureAwait(false);
        if (written.InvoiceId <= 0)
        {
            return ErpSimpleWriteResult.Fail("write", "Invoice document was not created.");
        }

        await ErpDb.ExecuteAsync(connection, null,
            ErpDb.Positional(
                "INSERT INTO `ecomae_autoparts_invoice_links` (`order_id`,`invoice_id`,`invoice_number`,`net`,`vat`,`gross`,`created_at_utc`)"
                + " VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            orderId, written.InvoiceId, written.InvoiceNumber,
            written.SubtotalExVat, written.TotalVat, written.TotalInclVat,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

        return ErpSimpleWriteResult.Ok(
            "Tax invoice " + written.InvoiceNumber + " issued — AED " + written.TotalInclVat.ToString("N2", CultureInfo.InvariantCulture)
            + " (net " + written.SubtotalExVat.ToString("N2", CultureInfo.InvariantCulture) + " + VAT " + written.TotalVat.ToString("N2", CultureInfo.InvariantCulture)
            + "). Print/share: /erp/invoice-print?id=" + written.InvoiceId.ToString(CultureInfo.InvariantCulture),
            written.InvoiceId);
    }

    public async Task<string> CreatePendingAsync(int actorId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — invoice build skipped.";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpAutoPartsWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var pending = new List<long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT o.`id` FROM `ecomae_autoparts_orders` o"
                + " WHERE o.`order_type`='sale' AND o.`status`='invoiced'"
                + " AND NOT EXISTS (SELECT 1 FROM `ecomae_autoparts_invoice_links` k WHERE k.`order_id`=o.`id`)"
                + " ORDER BY o.`id`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                pending.Add(reader.GetInt64(0));
            }
        }

        var issued = 0;
        var failed = 0;
        foreach (var id in pending)
        {
            var result = await CreateFromOrderAsync(id, actorId, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded) issued++; else failed++;
        }

        return "Issued " + issued + " tax invoice(s) for invoiced sales orders"
               + (failed > 0 ? ", " + failed + " failed" : "")
               + ". Open an invoice from the invoices tab or /erp/invoice-print?id=N to share it with the customer.";
    }

    private sealed record OrderRow(long Id, string OrderNo, string PartyName);
    private sealed record LineRow(string Description, decimal Qty, decimal UnitPrice, decimal TaxRate);

    private static async Task<OrderRow?> LoadOrderAsync(DbConnection connection, long orderId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,`order_no`,`party_name` FROM `ecomae_autoparts_orders`"
            + " WHERE `id`=? AND `order_type`='sale' AND `status` IN ('invoiced','paid') LIMIT 1");
        ErpDb.AddParameters(command, orderId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }
        return new OrderRow(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? "" : reader.GetString(2));
    }

    private static async Task<List<LineRow>> LoadLinesAsync(DbConnection connection, long orderId, CancellationToken ct)
    {
        var lines = new List<LineRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `description`,`qty`,`unit_price`,`tax_rate` FROM `ecomae_autoparts_order_lines` WHERE `order_id`=? ORDER BY `id`");
        ErpDb.AddParameters(command, orderId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            lines.Add(new LineRow(reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2), reader.GetDecimal(3)));
        }
        return lines;
    }
}
