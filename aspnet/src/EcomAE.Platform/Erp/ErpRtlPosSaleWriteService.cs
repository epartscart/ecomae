using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rtl_pos_sale</c> / ajax <c>rtl_pos_sale</c> twin
/// (<c>epc_erp_retail.php</c>). Prices each line with the active channel/global
/// discounts (best net unit wins, PHP <c>epc_rtl_best_discount</c>), applies the
/// caller-supplied tax rate on net, then inserts the <c>epc_rtl_txn</c> header
/// and <c>epc_rtl_txn_line</c> rows in one transaction. Tender whitelist is
/// cash/card/online/voucher; invalid tenders refuse like PHP's Exception.
/// Company resolves PHP <c>epc_erp_active_company_id</c>-style: explicit
/// company id wins when it belongs to the tenant, else first active legal
/// entity (0 when none). Fails closed when the POS tables are not provisioned.
/// Beyond PHP: the same transaction also posts the GL journal (Dr cash 1000 /
/// Cr revenue 4000 / Cr VAT output 2100 when those COA accounts exist) and, when
/// <see cref="ErpRtlPosSaleRequest.WarehouseId"/> names a provisioned inventory
/// warehouse, a <c>sale_out</c> movement + stock decrement per line — a missing
/// item/warehouse or a posting failure refuses the whole sale (audit finding 3).
/// Money is decimal end-to-end (audit finding 6).
/// </summary>
public interface IErpRtlPosSaleWriteService
{
    Task<ErpRtlPosSaleResult> SaleAsync(
        ErpRtlPosSaleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRtlPosSaleLine(long ItemId, decimal Qty, decimal UnitPrice);

public sealed record ErpRtlPosSaleRequest(
    long CompanyId = 0,
    long ChannelId = 0,
    IReadOnlyList<ErpRtlPosSaleLine>? Lines = null,
    string? Tender = null,
    decimal TaxRate = 0m,
    long WarehouseId = 0,
    int AdminId = 0);

public sealed record ErpRtlPosSaleResult(
    bool Ok,
    string Message,
    long Id,
    decimal Gross,
    decimal Discount,
    decimal Net,
    decimal Tax,
    decimal Total,
    int Writes)
{
    public static ErpRtlPosSaleResult FailMsg(string message) =>
        new(false, message, 0, 0m, 0m, 0m, 0m, 0m, 0);
}

public sealed class ErpRtlPosSaleWriteService : IErpRtlPosSaleWriteService
{
    private static readonly HashSet<string> Tenders = new(StringComparer.Ordinal)
    {
        "cash", "card", "online", "voucher"
    };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;
    private readonly IErpVoucherNumberService _vouchers;

    public ErpRtlPosSaleWriteService(
        IErpWriteConnectionFactory connections,
        IErpGlPostingService gl,
        IErpVoucherNumberService vouchers)
    {
        _connections = connections;
        _gl = gl;
        _vouchers = vouchers;
    }

    public async Task<ErpRtlPosSaleResult> SaleAsync(
        ErpRtlPosSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpRtlPosSaleResult.FailMsg("TenantRegistry DB is not configured.");
        }

        var tender = (request.Tender ?? string.Empty).Trim();
        if (tender.Length == 0) tender = "cash";
        if (!Tenders.Contains(tender))
        {
            return ErpRtlPosSaleResult.FailMsg("Invalid tender type");
        }

        var atTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lines = request.Lines ?? [];

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_rtl_txn", "receipt_no", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_rtl_txn_line", "line_net", cancellationToken).ConfigureAwait(false))
        {
            return ErpRtlPosSaleResult.FailMsg("Retail POS tables are not provisioned");
        }

        var companyId = request.CompanyId;
        if (companyId <= 0 || !await CompanyExistsAsync(connection, companyId, cancellationToken).ConfigureAwait(false))
        {
            companyId = await FirstCompanyIdAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var postStock = request.WarehouseId > 0 && await InventoryReadyAsync(connection, cancellationToken).ConfigureAwait(false);
        var glReady = await GlAccountsReadyAsync(connection, cancellationToken).ConfigureAwait(false);
        if (glReady)
        {
            await _gl.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await _vouchers.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var discounts = new List<(string Type, decimal Value)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `disc_type`, `value` FROM `epc_rtl_discount` WHERE company_id=? AND active=1 AND (channel_id=? OR channel_id=0)"
                + " AND (starts=0 OR starts<=?) AND (ends=0 OR ends>=?) ORDER BY id");
            ErpDb.AddParameters(command, companyId, request.ChannelId, atTime, atTime);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                discounts.Add((
                    Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "percent",
                    Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture)));
            }
        }

        var gross = 0m;
        var disc = 0m;
        var net = 0m;
        var tax = 0m;
        var total = 0m;
        var priced = new List<(long ItemId, decimal Qty, decimal UnitPrice, decimal Discount, decimal Net)>();
        foreach (var line in lines)
        {
            var priced_line = PriceLine(line.UnitPrice, line.Qty, discounts, request.TaxRate);
            gross += priced_line.Gross;
            disc += priced_line.Discount;
            net += priced_line.Net;
            tax += priced_line.Tax;
            total += priced_line.Total;
            priced.Add((line.ItemId, line.Qty, line.UnitPrice, priced_line.Discount, priced_line.Net));
        }

        gross = decimal.Round(gross, 2);
        disc = decimal.Round(disc, 2);
        net = decimal.Round(net, 2);
        tax = decimal.Round(tax, 2);
        total = decimal.Round(total, 2);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var writes = 0;
        long txnId = 0;
        try
        {
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "INSERT INTO `epc_rtl_txn` (`company_id`,`channel_id`,`receipt_no`,`gross`,`discount`,`net`,`tax`,`total`,`tender_type`,`txn_time`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            request.ChannelId,
            string.Empty,
            gross,
            disc,
            net,
            tax,
            total,
            tender,
            atTime).ConfigureAwait(false);
        writes++;
        txnId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        foreach (var line in priced)
        {
            writes += await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_rtl_txn_line` (`txn_id`,`item_id`,`qty`,`unit_price`,`line_discount`,`line_net`) VALUES (?,?,?,?,?,?)"),
                cancellationToken,
                txnId,
                line.ItemId,
                line.Qty,
                line.UnitPrice,
                line.Discount,
                line.Net).ConfigureAwait(false);
        }

        if (postStock)
        {
            foreach (var line in priced)
            {
                if (line.Qty == 0) continue;
                var movementDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                writes += await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_inv_movements` (`movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`transfer_warehouse_id`,`purchase_id`,`order_id`,`batch_no`,`expiry_date`,`reference`,`note`,`movement_date`,`admin_id`) VALUES ('sale_out',?,?,?,?,?,0,0,0,NULL,NULL,?,?,?,?)"),
                    cancellationToken,
                    request.WarehouseId,
                    line.ItemId,
                    line.Qty,
                    line.Net,
                    decimal.Round(line.Qty * line.Net, 2, MidpointRounding.AwayFromZero),
                    "POS-" + txnId,
                    "RTL POS sale",
                    movementDate,
                    request.AdminId).ConfigureAwait(false);
                await ErpInventoryMovementWriteService.UpsertStockAsync(
                    connection,
                    transaction,
                    request.WarehouseId,
                    line.ItemId,
                    -line.Qty,
                    line.Net,
                    string.Empty,
                    string.Empty,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (glReady)
        {
            var journalLines = new List<ErpGlLine>
            {
                new(await CoaIdByCodeAsync(connection, transaction, "1000", cancellationToken).ConfigureAwait(false), total, 0m, "POS cash"),
                new(await CoaIdByCodeAsync(connection, transaction, "4000", cancellationToken).ConfigureAwait(false), 0m, net, "POS revenue"),
            };
            if (tax > 0)
            {
                journalLines.Add(new(await CoaIdByCodeAsync(connection, transaction, "2100", cancellationToken).ConfigureAwait(false), 0m, tax, "POS VAT output"));
            }

            var journalId = await _gl.PostJournalAsync(
                connection,
                transaction,
                new ErpGlJournalHeader
                {
                    JournalDate = atTime,
                    Reference = "RTL-POS-" + txnId,
                    Description = "Retail POS sale " + txnId,
                    SourceType = "sales",
                    SourceId = txnId,
                    CompanyId = companyId,
                },
                journalLines,
                request.AdminId,
                cancellationToken).ConfigureAwait(false);
            writes++;
            _ = journalId;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var message = "Sale recorded · total " + total.ToString("0.00", CultureInfo.InvariantCulture);
        return new(true, message, txnId, gross, disc, net, tax, total, writes);
    }

    private static async Task<bool> InventoryReadyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN ('epc_erp_inv_movements','epc_erp_inv_stock','epc_erp_inv_items','epc_erp_inv_warehouses')"),
            cancellationToken).ConfigureAwait(false);
        return count == 4;
    }

    private static async Task<bool> GlAccountsReadyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_erp_coa_accounts'"),
            cancellationToken).ConfigureAwait(false);
        if (count == 0) return false;
        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_coa_accounts` WHERE `code` IN ('1000','4000')"),
            cancellationToken).ConfigureAwait(false) == 2;
    }

    private static async Task<long> CoaIdByCodeAsync(DbConnection connection, DbTransaction transaction, string code, CancellationToken cancellationToken)
    {
        var id = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            code).ConfigureAwait(false);
        if (id <= 0)
        {
            throw new ErpWriteException("COA account " + code + " is missing; cannot post the POS journal.");
        }
        return id;
    }

    /// <summary>PHP epc_rtl_price_line: best discount wins, tax on net.</summary>
    private static (decimal Gross, decimal Discount, decimal Net, decimal Tax, decimal Total, decimal NetUnit)
        PriceLine(decimal unitPrice, decimal qty, List<(string Type, decimal Value)> discounts, decimal taxRate)
    {
        var bestNet = unitPrice;
        foreach (var (type, value) in discounts)
        {
            var netUnit = type == "percent"
                ? unitPrice * (1m - Math.Max(0m, Math.Min(100m, value)) / 100m)
                : unitPrice - value;
            if (netUnit < 0) netUnit = 0m;
            if (netUnit < bestNet) bestNet = netUnit;
        }

        var gross = decimal.Round(unitPrice * qty, 2);
        var bestNetRounded = decimal.Round(bestNet, 4);
        var net = decimal.Round(bestNetRounded * qty, 2);
        var discount = decimal.Round(gross - net, 2);
        var tax = decimal.Round(net * (taxRate / 100m), 2);
        var total = decimal.Round(net + tax, 2);
        return (gross, discount, net, tax, total, bestNetRounded);
    }

    private static async Task<bool> CompanyExistsAsync(DbConnection connection, long companyId, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_pm_legal_entities` WHERE `id` = ? AND `active` = 1"),
            cancellationToken,
            companyId).ConfigureAwait(false);
        return n > 0;
    }

    private static async Task<long> FirstCompanyIdAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `active` = 1 ORDER BY `id` LIMIT 1"),
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
