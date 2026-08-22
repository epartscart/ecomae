using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>One open AR invoice / AP bill with its remaining outstanding amount.</summary>
public sealed record ErpOpenDocument(long Id, string DocumentNumber, long DocumentDate, decimal Total, decimal Outstanding);

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_settlement.php</c>: lets a receipt voucher settle open
/// customer invoices and a payment voucher settle open supplier bills, with per-document
/// knock-off, partial settlement, FIFO auto-allocation and an allocation audit trail on
/// <c>epc_erp_settlement_allocations</c>.
/// </summary>
public interface IErpSettlementAllocationService
{
    Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_erp_open_customer_invoices</c>: outstanding AR, oldest due first.</summary>
    Task<IReadOnlyList<ErpOpenDocument>> OpenCustomerInvoicesAsync(
        DbConnection connection,
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_erp_open_supplier_bills</c>: outstanding AP, oldest first.</summary>
    Task<IReadOnlyList<ErpOpenDocument>> OpenSupplierBillsAsync(
        DbConnection connection,
        int supplierId,
        CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_erp_apply_receipt_allocations</c>: raises <c>paid_amount</c> on the settled invoices.</summary>
    Task<decimal> ApplyReceiptAllocationsAsync(
        DbConnection connection,
        long cashEntryId,
        string voucherNo,
        int userId,
        IReadOnlyDictionary<long, decimal> allocations,
        long time,
        decimal cap,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_erp_apply_payment_allocations</c>: per-bill payable knock-off plus bill status.</summary>
    Task<decimal> ApplyPaymentAllocationsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long cashEntryId,
        string voucherNo,
        int supplierId,
        IReadOnlyDictionary<long, decimal> allocations,
        long time,
        decimal cap,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpSettlementAllocationService : IErpSettlementAllocationService
{
    /// <summary>PHP compares allocation amounts against half a cent throughout.</summary>
    private const decimal Epsilon = 0.005m;

    /// <summary>
    /// PHP <c>epc_erp_settlement_parse_allocations</c>: parallel <c>alloc_invoice_id[]</c> /
    /// <c>alloc_amount[]</c> arrays collapsed into one amount per document.
    /// </summary>
    public static Dictionary<long, decimal> ParseAllocations(
        IReadOnlyList<long>? invoiceIds,
        IReadOnlyList<decimal>? amounts)
    {
        var parsed = new Dictionary<long, decimal>();
        if (invoiceIds is null)
        {
            return parsed;
        }

        for (var i = 0; i < invoiceIds.Count; i++)
        {
            var id = invoiceIds[i];
            var amount = ErpTaxAmountCalculator.Round2(amounts is not null && i < amounts.Count ? amounts[i] : 0m);
            if (id <= 0 || amount <= Epsilon)
            {
                continue;
            }

            parsed[id] = ErpTaxAmountCalculator.Round2(parsed.TryGetValue(id, out var running) ? running + amount : amount);
        }

        return parsed;
    }

    /// <summary>PHP <c>epc_erp_settlement_fifo</c>: spend the amount across open documents, oldest first.</summary>
    public static Dictionary<long, decimal> Fifo(IReadOnlyList<ErpOpenDocument> openDocuments, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(openDocuments);
        var allocations = new Dictionary<long, decimal>();
        var left = ErpTaxAmountCalculator.Round2(amount);
        foreach (var document in openDocuments)
        {
            if (left <= Epsilon)
            {
                break;
            }

            var outstanding = ErpTaxAmountCalculator.Round2(document.Outstanding);
            var take = decimal.Min(outstanding, left);
            if (take <= Epsilon)
            {
                continue;
            }

            allocations[document.Id] = take;
            left = ErpTaxAmountCalculator.Round2(left - take);
        }

        return allocations;
    }

    public Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_settlement_allocations` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `doc_type` enum('ar','ap') NOT NULL,"
            + " `cash_entry_id` int(11) NOT NULL DEFAULT 0,"
            + " `voucher_no` varchar(64) DEFAULT NULL,"
            + " `counterparty_id` int(11) NOT NULL DEFAULT 0,"
            + " `invoice_id` int(11) NOT NULL DEFAULT 0,"
            + " `amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `time` int(11) NOT NULL DEFAULT 0,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_cash` (`cash_entry_id`),"
            + " KEY `x_doc` (`doc_type`,`invoice_id`),"
            + " KEY `x_party` (`doc_type`,`counterparty_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP receipt/payment to invoice/bill allocations'",
            cancellationToken);
    }

    public async Task<IReadOnlyList<ErpOpenDocument>> OpenCustomerInvoicesAsync(
        DbConnection connection,
        int userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (userId <= 0)
        {
            return [];
        }

        try
        {
            return await ReadOpenDocumentsAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT `id`, `invoice_number`, (CASE WHEN `payment_due_date` > 0 THEN `payment_due_date` ELSE `issue_date` END),"
                    + " `total_incl_vat`, ROUND(`total_incl_vat` - `paid_amount`, 2) AS outstanding"
                    + " FROM `epc_einvoice_documents`"
                    + " WHERE `active` = 1 AND `status` <> 'cancelled'"
                    + " AND `doc_category` IN ('tax_invoice','commercial_invoice') AND `user_id` = ?"
                    + " AND ROUND(`total_incl_vat` - `paid_amount`, 2) > 0.005"
                    + " ORDER BY (CASE WHEN `payment_due_date` > 0 THEN `payment_due_date` ELSE `issue_date` END) ASC, `id` ASC"),
                cancellationToken,
                userId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // PHP probes SHOW TABLES first and returns nothing when e-invoicing was never installed.
            return [];
        }
    }

    public async Task<IReadOnlyList<ErpOpenDocument>> OpenSupplierBillsAsync(
        DbConnection connection,
        int supplierId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (supplierId <= 0)
        {
            return [];
        }

        try
        {
            return await ReadOpenDocumentsAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT p.`id`, p.`invoice_number`, p.`purchase_date`, p.`total_amount`,"
                    + " ROUND(p.`total_amount` - " + PaidSubquery + ", 2) AS outstanding"
                    + " FROM `epc_erp_purchases` p"
                    + " WHERE p.`active` = 1 AND p.`status` <> 'draft' AND p.`supplier_id` = ?"
                    + " HAVING outstanding > 0.005"
                    + " ORDER BY p.`purchase_date` ASC, p.`id` ASC"),
                cancellationToken,
                supplierId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return [];
        }
    }

    public async Task<decimal> ApplyReceiptAllocationsAsync(
        DbConnection connection,
        long cashEntryId,
        string voucherNo,
        int userId,
        IReadOnlyDictionary<long, decimal> allocations,
        long time,
        decimal cap,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(allocations);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var total = 0m;
        var capLeft = ErpTaxAmountCalculator.Round2(cap);
        foreach (var (invoiceId, requested) in allocations)
        {
            var amount = ErpTaxAmountCalculator.Round2(requested);
            if (invoiceId <= 0 || amount <= Epsilon || capLeft <= Epsilon)
            {
                continue;
            }

            var outstanding = await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT ROUND(`total_incl_vat` - `paid_amount`, 2) FROM `epc_einvoice_documents`"
                    + " WHERE `id` = ? AND `user_id` = ? AND `active` = 1 LIMIT 1"),
                cancellationToken,
                invoiceId,
                userId).ConfigureAwait(false);
            if (outstanding <= Epsilon)
            {
                continue;
            }

            var apply = decimal.Min(decimal.Min(amount, outstanding), capLeft);
            if (apply <= Epsilon)
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                // MySQL applies SET assignments left to right, so `amount_due` must be derived
                // before `paid_amount` is raised or the payment is subtracted twice.
                ErpDb.Positional(
                    "UPDATE `epc_einvoice_documents` SET `amount_due` = ROUND(`total_incl_vat` - (`paid_amount` + ?), 2),"
                    + " `paid_amount` = ROUND(`paid_amount` + ?, 2), `time_updated` = ?"
                    + " WHERE `id` = ?"),
                cancellationToken,
                apply,
                apply,
                time,
                invoiceId).ConfigureAwait(false);

            await InsertAllocationAsync(
                connection,
                null,
                "ar",
                cashEntryId,
                voucherNo,
                userId,
                invoiceId,
                apply,
                time,
                adminId,
                cancellationToken).ConfigureAwait(false);

            total = ErpTaxAmountCalculator.Round2(total + apply);
            capLeft = ErpTaxAmountCalculator.Round2(capLeft - apply);
        }

        return total;
    }

    public async Task<decimal> ApplyPaymentAllocationsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long cashEntryId,
        string voucherNo,
        int supplierId,
        IReadOnlyDictionary<long, decimal> allocations,
        long time,
        decimal cap,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(allocations);

        var total = 0m;
        var capLeft = ErpTaxAmountCalculator.Round2(cap);
        foreach (var (billId, requested) in allocations)
        {
            var amount = ErpTaxAmountCalculator.Round2(requested);
            if (billId <= 0 || amount <= Epsilon || capLeft <= Epsilon)
            {
                continue;
            }

            var bill = await LoadBillAsync(connection, transaction, billId, supplierId, cancellationToken).ConfigureAwait(false);
            if (bill is null)
            {
                continue;
            }

            var (billTotal, paid) = bill.Value;
            var outstanding = ErpTaxAmountCalculator.Round2(billTotal - paid);
            if (outstanding <= Epsilon)
            {
                continue;
            }

            var apply = decimal.Min(decimal.Min(amount, outstanding), capLeft);
            if (apply <= Epsilon)
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_supplier_accounting` (`supplier_id`, `time`, `is_credit`, `amount`, `purchase_id`,"
                    + " `cash_entry_id`, `reference`, `note`, `admin_id`, `entry_kind`) VALUES (?,?,0,?,?,?,?,?,?,'payment')"),
                cancellationToken,
                supplierId,
                time,
                apply,
                billId,
                cashEntryId,
                voucherNo,
                "Bill settlement " + voucherNo,
                adminId).ConfigureAwait(false);

            await InsertAllocationAsync(
                connection,
                transaction,
                "ap",
                cashEntryId,
                voucherNo,
                supplierId,
                billId,
                apply,
                time,
                adminId,
                cancellationToken).ConfigureAwait(false);

            var newPaid = ErpTaxAmountCalculator.Round2(paid + apply);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `epc_erp_purchases` SET `status` = ? WHERE `id` = ?"),
                cancellationToken,
                newPaid + Epsilon >= billTotal ? "paid" : "partial",
                billId).ConfigureAwait(false);

            total = ErpTaxAmountCalculator.Round2(total + apply);
            capLeft = ErpTaxAmountCalculator.Round2(capLeft - apply);
        }

        return total;
    }

    /// <summary>Payments already booked against a bill (PHP sums the debit supplier ledger rows).</summary>
    private const string PaidSubquery =
        "IFNULL((SELECT SUM(a.`amount`) FROM `epc_erp_supplier_accounting` a"
        + " WHERE a.`purchase_id` = p.`id` AND a.`active` = 1 AND a.`is_credit` = 0), 0)";

    private static Task InsertAllocationAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string docType,
        long cashEntryId,
        string voucherNo,
        int counterpartyId,
        long documentId,
        decimal amount,
        long time,
        int adminId,
        CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_settlement_allocations` (`doc_type`, `cash_entry_id`, `voucher_no`,"
                + " `counterparty_id`, `invoice_id`, `amount`, `time`, `admin_id`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            docType,
            cashEntryId,
            voucherNo,
            counterpartyId,
            documentId,
            amount,
            time,
            adminId);

    private static async Task<(decimal Total, decimal Paid)?> LoadBillAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long billId,
        int supplierId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(
            "SELECT p.`total_amount`, " + PaidSubquery + " AS paid FROM `epc_erp_purchases` p"
            + " WHERE p.`id` = ? AND p.`supplier_id` = ? AND p.`active` = 1 LIMIT 1");
        ErpDb.AddParameters(command, billId, supplierId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (
            reader.IsDBNull(0) ? 0m : ErpTaxAmountCalculator.Round2(reader.GetDecimal(0)),
            reader.IsDBNull(1) ? 0m : ErpTaxAmountCalculator.Round2(reader.GetDecimal(1)));
    }

    private static async Task<IReadOnlyList<ErpOpenDocument>> ReadOpenDocumentsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken cancellationToken,
        params object?[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        ErpDb.AddParameters(command, parameters);

        var documents = new List<ErpOpenDocument>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            documents.Add(new ErpOpenDocument(
                reader.GetInt64(0),
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? 0L : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture),
                reader.IsDBNull(3) ? 0m : ErpTaxAmountCalculator.Round2(reader.GetDecimal(3)),
                reader.IsDBNull(4) ? 0m : ErpTaxAmountCalculator.Round2(reader.GetDecimal(4))));
        }

        return documents;
    }
}
