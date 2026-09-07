using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_barcode_purchase_create</c> / <c>epc_barcode_purchase_sell</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpJwBarcodePurchaseWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpJwBarcodePurchaseCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SellAsync(
        ErpJwBarcodePurchaseSellRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwBarcodePurchaseCreateRequest(
    int CompanyId = 0,
    string? Barcode = null,
    string? ItemDescription = null,
    int SupplierId = 0,
    string? SupplierName = null,
    string? PurchaseDate = null,
    string? PurchaseInvoiceNo = null,
    string? MetalType = null,
    string? Karat = null,
    decimal GrossWeight = 0,
    decimal NetWeight = 0,
    decimal StoneWeight = 0,
    decimal GoldRateAtPurchase = 0,
    decimal MakingCharges = 0,
    decimal StoneValue = 0,
    decimal OtherCharges = 0,
    decimal MarginPct = 15,
    int SalesmanId = 0,
    string? SalesmanName = null,
    decimal SalesmanCommissionPct = 2,
    string? Category = null,
    string? DesignNo = null,
    string? HallmarkNo = null,
    string? CertificateNo = null);

public sealed record ErpJwBarcodePurchaseSellRequest(
    long Id = 0,
    int CustomerId = 0,
    int InvoiceId = 0);

public sealed class ErpJwBarcodePurchaseWriteService : IErpJwBarcodePurchaseWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwBarcodePurchaseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpJwBarcodePurchaseCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var barcode = Clip((request.Barcode ?? string.Empty).Trim(), 100);
        var description = Clip((request.ItemDescription ?? string.Empty).Trim(), 300);
        if (barcode.Length == 0 && description.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Barcode or description is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_barcode_purchases", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_barcode_purchases", "barcode", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Barcode purchase tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        if (barcode.Length == 0)
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_barcode_purchases`"),
                cancellationToken).ConfigureAwait(false);
            barcode = "BP-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-"
                      + (count + 1).ToString("D4", CultureInfo.InvariantCulture);
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_barcode_purchases` WHERE `company_id` = ? AND `barcode` = ?"),
            cancellationToken,
            companyId,
            barcode).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Barcode already exists.");
        }

        var net = RoundNonNeg(request.NetWeight, 3);
        var goldRate = RoundNonNeg(request.GoldRateAtPurchase, 4);
        var metalValue = decimal.Round(net * goldRate, 2, MidpointRounding.AwayFromZero);
        var making = RoundNonNeg(request.MakingCharges, 2);
        var stoneValue = RoundNonNeg(request.StoneValue, 2);
        var other = RoundNonNeg(request.OtherCharges, 2);
        var totalCost = decimal.Round(metalValue + making + stoneValue + other, 2, MidpointRounding.AwayFromZero);
        var marginPct = RoundNonNeg(request.MarginPct, 2);
        var marginAmount = decimal.Round(totalCost * (marginPct / 100m), 2, MidpointRounding.AwayFromZero);
        var selling = decimal.Round(totalCost + marginAmount, 2, MidpointRounding.AwayFromZero);
        var metal = Clip((request.MetalType ?? string.Empty).Trim(), 30);
        if (metal.Length == 0)
        {
            metal = "gold";
        }

        var karat = Clip((request.Karat ?? string.Empty).Trim(), 10);
        if (karat.Length == 0)
        {
            karat = "22K";
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_barcode_purchases` (`company_id`,`barcode`,`item_description`,`supplier_id`,`supplier_name`,`purchase_date`,`purchase_invoice_no`,`metal_type`,`karat`,`gross_weight`,`net_weight`,`stone_weight`,`gold_rate_at_purchase`,`metal_value`,`making_charges`,`stone_value`,`other_charges`,`total_cost`,`margin_pct`,`margin_amount`,`selling_price`,`salesman_id`,`salesman_name`,`salesman_commission_pct`,`category`,`design_no`,`hallmark_no`,`certificate_no`,`status`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'available', ?)"),
            cancellationToken,
            companyId,
            barcode,
            description,
            request.SupplierId < 0 ? 0 : request.SupplierId,
            Clip((request.SupplierName ?? string.Empty).Trim(), 200),
            FormatDateOrToday(request.PurchaseDate),
            Clip((request.PurchaseInvoiceNo ?? string.Empty).Trim(), 64),
            metal,
            karat,
            RoundNonNeg(request.GrossWeight, 3),
            net,
            RoundNonNeg(request.StoneWeight, 3),
            goldRate,
            metalValue,
            making,
            stoneValue,
            other,
            totalCost,
            marginPct,
            marginAmount,
            selling,
            request.SalesmanId < 0 ? 0 : request.SalesmanId,
            Clip((request.SalesmanName ?? string.Empty).Trim(), 120),
            RoundNonNeg(request.SalesmanCommissionPct, 2),
            Clip((request.Category ?? string.Empty).Trim(), 100),
            Clip((request.DesignNo ?? string.Empty).Trim(), 50),
            Clip((request.HallmarkNo ?? string.Empty).Trim(), 50),
            Clip((request.CertificateNo ?? string.Empty).Trim(), 50),
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Barcode purchase " + barcode + " created", id);
    }

    public async Task<ErpSimpleWriteResult> SellAsync(
        ErpJwBarcodePurchaseSellRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Purchase id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_barcode_purchases", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_barcode_purchases", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Barcode purchase tables are not provisioned");
        }

        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_barcode_purchases` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        if (string.IsNullOrEmpty(status))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Purchase is missing.");
        }

        if (string.Equals(status, "sold", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Purchase is already sold.");
        }

        var updated = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_barcode_purchases` SET `status` = 'sold', `sold_to_customer_id` = ?, `sold_invoice_id` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            request.CustomerId < 0 ? 0 : request.CustomerId,
            request.InvoiceId < 0 ? 0 : request.InvoiceId,
            UnixNow(),
            request.Id).ConfigureAwait(false);
        if (updated <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Purchase is missing.");
        }

        return ErpSimpleWriteResult.Ok("Barcode purchase sold", request.Id);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
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

    private static string FormatDateOrToday(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            return parsed.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return DateTime.Now.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static int UnixNow()
        => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
