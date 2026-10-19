using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Builds UAE FTA tax invoices from approved fit-out progress claims so the
/// tenant can share a compliant invoice (TRN, 5% VAT breakdown, retention
/// disclosure) with the customer. Creates the e-invoice document through the
/// shared manual-invoice writer (PINT-AE, tax_invoice/380), links it through
/// the fit-out invoice bridge, and leaves printing to /erp/invoice-print.
/// </summary>
public interface IErpFitOutClaimInvoiceWriteService
{
    Task<ErpSimpleWriteResult> CreateFromClaimAsync(long claimId, int actorId, CancellationToken cancellationToken = default);
    Task<string> CreatePendingAsync(long projectId, int actorId, CancellationToken cancellationToken = default);
}

public sealed class ErpFitOutClaimInvoiceWriteService : IErpFitOutClaimInvoiceWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpManualInvoiceWriteService _invoices;
    private readonly IErpFitOutInvoiceBridgeWriteService _bridge;

    public ErpFitOutClaimInvoiceWriteService(
        IErpWriteConnectionFactory connections,
        IErpManualInvoiceWriteService invoices,
        IErpFitOutInvoiceBridgeWriteService bridge)
    {
        _connections = connections;
        _invoices = invoices;
        _bridge = bridge;
    }

    public async Task<ErpSimpleWriteResult> CreateFromClaimAsync(long claimId, int actorId, CancellationToken cancellationToken = default)
    {
        if (claimId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A progress-claim id is required.");
        }
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var claim = await LoadClaimAsync(connection, claimId, cancellationToken).ConfigureAwait(false);
        if (claim is null)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Approved client progress claim not found.");
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `invoice_id` FROM `ecomae_fitout_invoice_links` WHERE `project_id`=? AND `reference`=? LIMIT 1"),
            cancellationToken,
            claim.ProjectId,
            "claim-" + claimId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Ok("Invoice already issued for this claim.", existing);
        }

        // claim.Amount is work-done − retention + VAT; split VAT back out so the
        // invoice lines carry net + 5% VAT per FTA tax-invoice rules.
        var vat = ExtractAmount(claim.Description, "VAT");
        var net = ErpFitOutCalc.Money(vat > 0 ? claim.Amount - vat : claim.Amount / 1.05m);
        var retention = ExtractAmount(claim.Description, "retention");
        var buyer = await LoadBuyerAsync(connection, claim.ProjectId, cancellationToken).ConfigureAwait(false);

        var linesJson = JsonSerializer.Serialize(new object[]
        {
            new { itemName = "Progress claim — " + claim.Title, itemDescription = claim.Description, quantity = 1m, unitPrice = net, taxRate = 5m },
        });
        var buyerJson = JsonSerializer.Serialize(new { name = buyer.Name, trn = buyer.Trn, address = buyer.Address });

        var written = await _invoices.SaveAsync(
            new ErpManualInvoiceWriteRequest(
                Id: 0,
                UserId: 0,
                InvoiceNumber: "FO-INV-P" + claim.ProjectId.ToString(CultureInfo.InvariantCulture) + "-" + claimId.ToString(CultureInfo.InvariantCulture),
                CurrencyCode: "AED",
                SellerJson: null,
                BuyerJson: buyerJson,
                LinesJson: linesJson,
                PaymentTerms: "Per contract terms — retention " + claim.RetentionNote + " withheld",
                DueDate: null,
                IssueDate: null,
                OrderId: claimId,
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

        var linked = await _bridge.SaveAsync(
            new ErpFitOutInvoiceBridgeSaveRequest(
                ProjectId: claim.ProjectId,
                InvoiceId: written.InvoiceId,
                Stage: "progress_claim",
                Reference: "claim-" + claimId.ToString(CultureInfo.InvariantCulture),
                ConfirmWrites: true),
            cancellationToken).ConfigureAwait(false);
        if (!linked.Succeeded)
        {
            return ErpSimpleWriteResult.Fail("link", "Invoice created but the project link failed: " + linked.Message);
        }

        _ = retention;
        return ErpSimpleWriteResult.Ok(
            "Tax invoice " + written.InvoiceNumber + " issued — AED " + written.TotalInclVat.ToString("N2", CultureInfo.InvariantCulture)
            + " (net " + written.SubtotalExVat.ToString("N2", CultureInfo.InvariantCulture) + " + VAT " + written.TotalVat.ToString("N2", CultureInfo.InvariantCulture)
            + "). Print/share: /erp/invoice-print?id=" + written.InvoiceId.ToString(CultureInfo.InvariantCulture),
            written.InvoiceId);
    }

    public async Task<string> CreatePendingAsync(long projectId, int actorId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — invoice build skipped.";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var pending = new List<long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT r.`id` FROM `ecomae_fitout_delivery_records` r"
                + " WHERE r.`record_type`='client_progress_claim' AND r.`status`='approved'"
                + " AND (?<=0 OR r.`project_id`=?)"
                + " AND NOT EXISTS (SELECT 1 FROM `ecomae_fitout_invoice_links` l WHERE l.`project_id`=r.`project_id` AND l.`reference`=CONCAT('claim-', r.`id`))"
                + " ORDER BY r.`id`");
            ErpDb.AddParameters(command, projectId, projectId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                pending.Add(reader.GetInt64(0));
            }
        }

        var issued = 0;
        var failed = 0;
        foreach (var claimId in pending)
        {
            var result = await CreateFromClaimAsync(claimId, actorId, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                issued++;
            }
            else
            {
                failed++;
            }
        }

        return "Issued " + issued + " tax invoice(s) for approved claims"
               + (pending.Count - issued - failed > 0 ? ", " + (pending.Count - issued - failed) + " already invoiced" : "")
               + (failed > 0 ? ", " + failed + " failed" : "")
               + ". Open an invoice from the claims tab or /erp/invoice-print?id=N to share it with the customer.";
    }

    private sealed record ClaimRow(long Id, long ProjectId, string Title, string Description, decimal Amount, string RetentionNote);
    private sealed record BuyerRow(string Name, string Trn, string Address);

    private static async Task<ClaimRow?> LoadClaimAsync(DbConnection connection, long claimId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT r.`id`, r.`project_id`, r.`title`, r.`description`, r.`amount`, t.`retention_percent`"
            + " FROM `ecomae_fitout_delivery_records` r"
            + " LEFT JOIN `ecomae_fitout_contract_terms` t ON t.`contract_id`=r.`project_id`"
            + " WHERE r.`id`=? AND r.`record_type`='client_progress_claim' AND r.`status`='approved' LIMIT 1");
        ErpDb.AddParameters(command, claimId);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        var retentionPct = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5);
        return new ClaimRow(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.IsDBNull(2) ? "" : reader.GetString(2),
            reader.IsDBNull(3) ? "" : reader.GetString(3),
            reader.GetDecimal(4),
            retentionPct.ToString("0.##", CultureInfo.InvariantCulture) + "%");
    }

    private static async Task<BuyerRow> LoadBuyerAsync(DbConnection connection, long projectId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT c.`name`, IFNULL(c.`trn`,''), IFNULL(c.`address`,'')"
            + " FROM `epc_prj_projects` p INNER JOIN `epc_erp_customers` c ON c.`id`=p.`customer_id`"
            + " WHERE p.`id`=? LIMIT 1");
        ErpDb.AddParameters(command, projectId);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return new BuyerRow(reader.GetString(0), reader.GetString(1), reader.GetString(2));
            }
        }
        catch (DbException)
        {
        }
        return new BuyerRow("Customer", string.Empty, string.Empty);
    }

    private static decimal ExtractAmount(string description, string label)
    {
        var idx = description.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return 0m;
        }

        var tail = description[(idx + label.Length)..].TrimStart(' ', '=', ':');
        var end = 0;
        while (end < tail.Length && (char.IsDigit(tail[end]) || tail[end] is '.' or '-' or ','))
        {
            end++;
        }
        return decimal.TryParse(tail[..end], NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;
    }
}
