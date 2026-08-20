using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpPurchaseInvoiceInput
{
    public int SupplierId { get; init; }

    public long OrderId { get; init; }

    public long StorageId { get; init; }

    public long PurchaseOrderId { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public long PurchaseDate { get; init; }

    public decimal AmountExVat { get; init; }

    public bool Import { get; init; }

    public string Status { get; init; } = "confirmed";

    public string Note { get; init; } = string.Empty;
}

public sealed record ErpPurchaseInvoiceResult(
    long PurchaseId,
    string InvoiceNumber,
    decimal AmountExVat,
    decimal VatAmount,
    decimal TotalAmount,
    decimal ImportDuty,
    long GlJournalId);

public sealed record ErpPoToPurchaseResult(
    long PurchaseOrderId,
    long PurchaseId,
    string VoucherNo,
    decimal AmountExVat,
    decimal VatAmount,
    decimal TotalAmount);

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_create_purchase</c> (<c>epc_erp_helpers.php</c>) and
/// <c>epc_erp_po_convert_to_purchase</c> (<c>epc_erp_vouchers.php</c>): purchase-side tax resolution,
/// PI numbering, supplier AP ledger row and best-effort GL posting, then the PO link/status flip.
/// The VAT-treatment legislation reference follows the tenant's registered country, so non-AE tenants
/// are not stamped with the UAE decree.
/// </summary>
public interface IErpPurchaseInvoiceWriteService
{
    Task<ErpPurchaseInvoiceResult> CreateAsync(ErpPurchaseInvoiceInput input, int adminId, CancellationToken cancellationToken = default);

    Task<ErpPoToPurchaseResult> ConvertPurchaseOrderAsync(long purchaseOrderId, int adminId, CancellationToken cancellationToken = default);
}

public sealed class ErpPurchaseInvoiceWriteService : IErpPurchaseInvoiceWriteService
{
    private static readonly string[] ConvertibleStatuses = ["approved", "partial", "received"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVoucherNumberService _vouchers;
    private readonly IErpTaxAmountCalculator _tax;
    private readonly IErpGlPostingService _gl;
    private readonly IErpAuditLogWriter _audit;

    public ErpPurchaseInvoiceWriteService(
        IErpWriteConnectionFactory connections,
        IErpVoucherNumberService vouchers,
        IErpTaxAmountCalculator tax,
        IErpGlPostingService gl,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _vouchers = vouchers;
        _tax = tax;
        _gl = gl;
        _audit = audit;
    }

    public async Task<ErpPurchaseInvoiceResult> CreateAsync(ErpPurchaseInvoiceInput input, int adminId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();
        if (input.SupplierId <= 0)
        {
            throw new ErpWriteException("Supplier is required");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        return await CreateOnConnectionAsync(connection, input, adminId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpPoToPurchaseResult> ConvertPurchaseOrderAsync(long purchaseOrderId, int adminId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (purchaseOrderId <= 0)
        {
            throw new ErpWriteException("Purchase order not found");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var po = await LoadPurchaseOrderAsync(connection, purchaseOrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Purchase order not found");
        if (po.PurchaseId > 0)
        {
            throw new ErpWriteException("Purchase order already linked to a purchase invoice");
        }

        if (!ConvertibleStatuses.Contains(po.Status, StringComparer.Ordinal))
        {
            throw new ErpWriteException("Approve the PO before converting to purchase invoice");
        }

        var piNo = await _vouchers.NextAsync(connection, null, "PI", cancellationToken).ConfigureAwait(false);
        var note = "From PO " + po.PoNo + (po.Notes.Length > 0 ? " — " + po.Notes : string.Empty);
        var purchase = await CreateOnConnectionAsync(
            connection,
            new ErpPurchaseInvoiceInput
            {
                SupplierId = po.SupplierId,
                OrderId = po.OrderId,
                PurchaseOrderId = purchaseOrderId,
                InvoiceNumber = piNo,
                AmountExVat = po.AmountExVat,
                Note = note,
                Status = "confirmed",
            },
            adminId,
            cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_erp_purchase_orders` SET `purchase_id` = ?, `voucher_no` = ?, `status` = 'received',"
                + " `received_at` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            purchase.PurchaseId,
            po.VoucherNo.Length > 0 ? po.VoucherNo : po.PoNo,
            now,
            now,
            purchaseOrderId).ConfigureAwait(false);

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "po_to_purchase",
            "purchase_order",
            purchaseOrderId,
            "Converted to purchase invoice",
            new Dictionary<string, string?>
            {
                ["purchase_id"] = purchase.PurchaseId.ToString(CultureInfo.InvariantCulture),
                ["pi_no"] = purchase.InvoiceNumber,
            },
            cancellationToken).ConfigureAwait(false);

        return new ErpPoToPurchaseResult(
            purchaseOrderId,
            purchase.PurchaseId,
            purchase.InvoiceNumber,
            purchase.AmountExVat,
            purchase.VatAmount,
            purchase.TotalAmount);
    }

    private async Task<ErpPurchaseInvoiceResult> CreateOnConnectionAsync(
        DbConnection connection,
        ErpPurchaseInvoiceInput input,
        int adminId,
        CancellationToken cancellationToken)
    {
        var amountEx = ErpTaxAmountCalculator.Round2(input.AmountExVat);
        var tax = await _tax.CalcPurchaseAsync(connection, null, amountEx, input.SupplierId, input.Import, cancellationToken).ConfigureAwait(false);
        var total = tax.ImportDuty > 0m ? tax.TotalWithDuty : tax.TotalAmount;
        var invoiceNumber = input.InvoiceNumber.Trim();
        if (invoiceNumber.Length == 0)
        {
            invoiceNumber = await _vouchers.NextAsync(connection, null, "PI", cancellationToken).ConfigureAwait(false);
        }

        var purchaseDate = input.PurchaseDate > 0 ? input.PurchaseDate : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var status = input.Status.Trim().Length > 0 ? input.Status.Trim() : "confirmed";
        var legislationRef = LegislationRefFor(tax.CountryCode);
        var treatment = tax.VatApplicable ? "standard" : "out_of_scope";

        long purchaseId;
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_purchases` (`supplier_id`, `order_id`, `storage_id`, `invoice_number`, `voucher_no`,"
                    + " `purchase_date`, `amount_ex_vat`, `vat_amount`, `total_amount`, `vat_applicable`, `vat_rate`,"
                    + " `uae_vat_treatment`, `uae_tax_legislation_ref`, `status`, `note`, `admin_id`, `time_created`)"
                    + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                cancellationToken,
                input.SupplierId,
                input.OrderId,
                input.StorageId,
                invoiceNumber,
                invoiceNumber,
                purchaseDate,
                amountEx,
                tax.VatAmount,
                total,
                tax.VatApplicable ? 1 : 0,
                tax.TaxRate,
                treatment,
                legislationRef,
                status,
                input.Note.Trim(),
                adminId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            purchaseId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            if (input.PurchaseOrderId > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("UPDATE `epc_erp_purchases` SET `po_id` = ? WHERE `id` = ?"),
                    cancellationToken,
                    input.PurchaseOrderId,
                    purchaseId).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_supplier_accounting` (`supplier_id`, `time`, `is_credit`, `amount`, `purchase_id`,"
                    + " `order_id`, `reference`, `note`, `admin_id`, `entry_kind`) VALUES (?,?,1,?,?,?,?,'Purchase invoice',?,'invoice')"),
                cancellationToken,
                input.SupplierId,
                purchaseDate,
                total,
                purchaseId,
                input.OrderId,
                invoiceNumber,
                adminId).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var journalId = await TryPostGlAsync(connection, purchaseId, adminId, cancellationToken).ConfigureAwait(false);
        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "purchase_create",
            "purchase",
            purchaseId,
            "Purchase invoice created",
            new Dictionary<string, string?> { ["invoice_number"] = invoiceNumber },
            cancellationToken).ConfigureAwait(false);

        return new ErpPurchaseInvoiceResult(
            purchaseId,
            invoiceNumber,
            amountEx,
            tax.VatAmount,
            ErpTaxAmountCalculator.Round2(total),
            tax.ImportDuty,
            journalId);
    }

    /// <summary>The UAE VAT decree only applies to AE tenants; other jurisdictions keep a generic reference.</summary>
    public static string LegislationRefFor(string countryCode)
        => (countryCode ?? string.Empty).Trim().ToUpperInvariant() is "AE" or "ARE"
            ? "vat-decree-8-2017"
            : string.Empty;

    private async Task<long> TryPostGlAsync(DbConnection connection, long purchaseId, int adminId, CancellationToken cancellationToken)
    {
        try
        {
            return await _gl.PostPurchaseAsync(connection, purchaseId, adminId, cancellationToken).ConfigureAwait(false);
        }
        catch (ErpWriteException)
        {
            return 0L;
        }
        catch (DbException)
        {
            return 0L;
        }
    }

    private void EnsureConfigured()
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }
    }

    private static async Task<(int SupplierId, long OrderId, long PurchaseId, string Status, string PoNo, string VoucherNo, string Notes, decimal AmountExVat)?> LoadPurchaseOrderAsync(
        DbConnection connection,
        long purchaseOrderId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `supplier_id`, `order_id`, `purchase_id`, `status`, `po_no`, `voucher_no`, `notes`, `amount_ex_vat`"
            + " FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1");
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@p0";
        parameter.Value = purchaseOrderId;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (
            reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
            reader.IsDBNull(1) ? 0L : reader.GetInt64(1),
            reader.IsDBNull(2) ? 0L : reader.GetInt64(2),
            reader.IsDBNull(3) ? "draft" : reader.GetString(3),
            reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            reader.IsDBNull(7) ? 0m : reader.GetDecimal(7));
    }

    /// <summary>Subset of PHP <c>epc_erp_ensure_schema</c> for the purchase + supplier ledger tables.</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_purchases` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `supplier_id` int(11) NOT NULL DEFAULT 0,"
            + " `order_id` int(11) NOT NULL DEFAULT 0,"
            + " `storage_id` int(11) NOT NULL DEFAULT 0,"
            + " `invoice_number` varchar(64) NOT NULL DEFAULT '',"
            + " `voucher_no` varchar(32) NOT NULL DEFAULT '',"
            + " `purchase_date` int(11) NOT NULL DEFAULT 0,"
            + " `amount_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `vat_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `total_amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `vat_applicable` tinyint(1) NOT NULL DEFAULT 1,"
            + " `vat_rate` decimal(6,3) NOT NULL DEFAULT 0.000,"
            + " `uae_vat_treatment` varchar(32) NOT NULL DEFAULT 'standard',"
            + " `uae_tax_legislation_ref` varchar(64) NOT NULL DEFAULT '',"
            + " `status` varchar(32) NOT NULL DEFAULT 'confirmed',"
            + " `note` text,"
            + " `po_id` int(11) NOT NULL DEFAULT 0,"
            + " `gl_journal_id` int(11) NOT NULL DEFAULT 0,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_supplier` (`supplier_id`),"
            + " KEY `x_po` (`po_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP purchase invoices'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_purchases` ADD COLUMN `po_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_purchases` ADD COLUMN `voucher_no` varchar(32) NOT NULL DEFAULT ''",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_supplier_accounting` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `supplier_id` int(11) NOT NULL DEFAULT 0,"
            + " `time` int(11) NOT NULL DEFAULT 0,"
            + " `is_credit` tinyint(1) NOT NULL DEFAULT 1,"
            + " `amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `purchase_id` int(11) NOT NULL DEFAULT 0,"
            + " `order_id` int(11) NOT NULL DEFAULT 0,"
            + " `cash_entry_id` int(11) NOT NULL DEFAULT 0,"
            + " `reference` varchar(64) NOT NULL DEFAULT '',"
            + " `note` varchar(255) NOT NULL DEFAULT '',"
            + " `entry_kind` varchar(32) NOT NULL DEFAULT 'invoice',"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_supplier` (`supplier_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Supplier AP ledger'",
            cancellationToken).ConfigureAwait(false);
    }
}
