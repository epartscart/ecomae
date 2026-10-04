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
/// </summary>
public interface IErpRtlPosSaleWriteService
{
    Task<ErpRtlPosSaleResult> SaleAsync(
        ErpRtlPosSaleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRtlPosSaleLine(long ItemId, double Qty, double UnitPrice);

public sealed record ErpRtlPosSaleRequest(
    long CompanyId = 0,
    long ChannelId = 0,
    IReadOnlyList<ErpRtlPosSaleLine>? Lines = null,
    string? Tender = null,
    double TaxRate = 0);

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

    public ErpRtlPosSaleWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
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

        var discounts = new List<(string Type, double Value)>();
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
                    Convert.ToDouble(reader.GetValue(1), CultureInfo.InvariantCulture)));
            }
        }

        var gross = 0m;
        var disc = 0m;
        var net = 0m;
        var tax = 0m;
        var total = 0m;
        var priced = new List<(long ItemId, double Qty, double UnitPrice, decimal Discount, decimal Net)>();
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
        var txnId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
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
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var message = "Sale recorded · total " + total.ToString("0.00", CultureInfo.InvariantCulture);
        return new(true, message, txnId, gross, disc, net, tax, total, writes);
    }

    /// <summary>PHP epc_rtl_price_line: best discount wins, tax on net.</summary>
    private static (decimal Gross, decimal Discount, decimal Net, decimal Tax, decimal Total, decimal NetUnit)
        PriceLine(double unitPrice, double qty, List<(string Type, double Value)> discounts, double taxRate)
    {
        var bestNet = unitPrice;
        foreach (var (type, value) in discounts)
        {
            var netUnit = type == "percent"
                ? unitPrice * (1 - Math.Max(0.0, Math.Min(100.0, value)) / 100.0)
                : unitPrice - value;
            if (netUnit < 0) netUnit = 0.0;
            if (netUnit < bestNet - 1e-9) bestNet = netUnit;
        }

        var gross = decimal.Round((decimal)(unitPrice * qty), 2);
        var net = decimal.Round((decimal)(Math.Round(bestNet, 4) * qty), 2);
        var discount = decimal.Round(gross - net, 2);
        var tax = decimal.Round(net * (decimal)(taxRate / 100.0), 2);
        var total = decimal.Round(net + tax, 2);
        return (gross, discount, net, tax, total, (decimal)Math.Round(bestNet, 4));
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
