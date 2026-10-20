using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Builds UAE FTA tax invoices from jewellery retail sale vouchers so the
/// tenant can share a compliant invoice (TRN, 5% VAT breakdown) with the
/// customer. Creates the e-invoice document through the shared manual-invoice
/// writer, links it to the voucher, printing via /erp/invoice-print.
/// Mirrors <see cref="ErpAutoPartsInvoiceWriteService"/>.
/// </summary>
public interface IErpJewelleryInvoiceWriteService
{
    Task<ErpSimpleWriteResult> CreateFromVoucherAsync(long voucherId, int actorId, CancellationToken cancellationToken = default);
    Task<string> CreatePendingAsync(int actorId, CancellationToken cancellationToken = default);
}

public sealed class ErpJewelleryInvoiceWriteService : IErpJewelleryInvoiceWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpManualInvoiceWriteService _invoices;

    public ErpJewelleryInvoiceWriteService(
        IErpWriteConnectionFactory connections,
        IErpManualInvoiceWriteService invoices)
    {
        _connections = connections;
        _invoices = invoices;
    }

    public async Task<ErpSimpleWriteResult> CreateFromVoucherAsync(long voucherId, int actorId, CancellationToken cancellationToken = default)
    {
        if (voucherId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A voucher id is required.");
        }
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpJewelleryWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `invoice_id` FROM `ecomae_jw_invoice_links` WHERE `voucher_id`=? LIMIT 1"),
            cancellationToken,
            voucherId).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Ok("Invoice already issued for this voucher.", existing);
        }

        var voucher = await LoadVoucherAsync(connection, voucherId, cancellationToken).ConfigureAwait(false);
        if (voucher is null)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Retail sale voucher not found.");
        }

        var lines = await LoadLinesAsync(connection, voucherId, cancellationToken).ConfigureAwait(false);
        if (lines.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("empty", "Voucher has no lines to invoice.");
        }

        var linesJson = JsonSerializer.Serialize(
            lines.Select(l => new ErpManualInvoiceLineInput(l.Description, l.Qty, l.UnitPrice, 5m, null)).ToArray());
        var buyerJson = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["buyer_name"] = voucher.Customer,
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
                InvoiceNumber: "JW-INV-" + voucher.Type + "-" + voucher.VocNo.ToString(CultureInfo.InvariantCulture) + "-" + voucherId.ToString(CultureInfo.InvariantCulture),
                CurrencyCode: "AED",
                SellerJson: null,
                BuyerJson: buyerJson,
                LinesJson: linesJson,
                PaymentTerms: "Due on receipt",
                DueDate: null,
                IssueDate: null,
                OrderId: voucherId,
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
                "INSERT INTO `ecomae_jw_invoice_links` (`voucher_id`,`invoice_id`,`invoice_number`,`net`,`vat`,`gross`,`created_at_utc`)"
                + " VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            voucherId, written.InvoiceId, written.InvoiceNumber,
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

        var salesTypes = string.Join("','", ErpJwVocTypes.SalesAnalysisAliases);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpJewelleryWorkspaceSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        var pending = new List<long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT v.`id` FROM `epc_jewel_voucher` v"
                + " WHERE v.`voc_type` IN ('" + salesTypes + "') AND v.`status`<>'cancelled'"
                + " AND NOT EXISTS (SELECT 1 FROM `ecomae_jw_invoice_links` k WHERE k.`voucher_id`=v.`id`)"
                + " ORDER BY v.`id`";
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
            var result = await CreateFromVoucherAsync(id, actorId, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded) issued++; else failed++;
        }

        return "Issued " + issued + " tax invoice(s) for retail sale vouchers"
               + (failed > 0 ? ", " + failed + " failed" : "")
               + ". Open an invoice from the invoices tab or /erp/invoice-print?id=N to share it with the customer.";
    }

    private sealed record VoucherRow(long Id, string Type, int VocNo, string Customer);
    private sealed record LineRow(string Description, decimal Qty, decimal UnitPrice);

    private static async Task<VoucherRow?> LoadVoucherAsync(DbConnection connection, long voucherId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,`voc_type`,`voc_no`,`customer_name` FROM `epc_jewel_voucher`"
            + " WHERE `id`=? AND `status`<>'cancelled' LIMIT 1");
        ErpDb.AddParameters(command, voucherId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }
        var row = new VoucherRow(reader.GetInt64(0), reader.GetString(1), reader.GetInt32(2), reader.IsDBNull(3) ? "" : reader.GetString(3));
        // Only sale vouchers can be issued as customer tax invoices — never a
        // purchase, repair-receipt, advance or journal voucher.
        return ErpJwVocTypes.SalesAnalysisAliases.Contains(row.Type, StringComparer.Ordinal) ? row : null;
    }

    private static async Task<List<LineRow>> LoadLinesAsync(DbConnection connection, long voucherId, CancellationToken ct)
    {
        var lines = new List<LineRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `description`,`pcs`,`gr_wt`,`net_amount` FROM `epc_jewel_voucher_lines` WHERE `voucher_id`=? ORDER BY `line_no`");
        ErpDb.AddParameters(command, voucherId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var pcs = reader.GetInt32(1);
            var wt = reader.GetDecimal(2);
            var net = reader.GetDecimal(3);
            var qty = pcs > 0 ? pcs : wt > 0 ? wt : 1m;
            lines.Add(new LineRow(reader.GetString(0), qty, qty > 0 ? net / qty : net));
        }
        return lines;
    }
}
