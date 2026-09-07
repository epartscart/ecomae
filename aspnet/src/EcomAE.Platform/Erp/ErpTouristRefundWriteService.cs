using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_tourist_refund_create</c> twin. Schema-ensure stays PHP.
/// Distinct from jewellery <c>epc_jewel_tourist_vat_refund</c>.
/// </summary>
public interface IErpTouristRefundWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpTouristRefundCreateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpTouristRefundCreateRequest(
    int CompanyId = 0,
    long InvoiceId = 0,
    string? InvoiceNo = null,
    string? TouristName = null,
    string? PassportNo = null,
    string? Nationality = null,
    string? DepartureDate = null,
    decimal TotalAmount = 0,
    decimal VatAmount = 0,
    decimal RefundPct = 85,
    string? RefundProvider = null);

public sealed class ErpTouristRefundWriteService : IErpTouristRefundWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpTouristRefundWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpTouristRefundCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tourist = Clip((request.TouristName ?? string.Empty).Trim(), 200);
        if (tourist.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Tourist name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_tourist_refund_invoices", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_tourist_refund_invoices", "tourist_name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_tourist_refund_invoices", "barcode", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Tourist refund tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var vat = RoundNonNeg(request.VatAmount, 2);
        var pct = request.RefundPct <= 0 || request.RefundPct > 100 ? 85m : decimal.Round(request.RefundPct, 2, MidpointRounding.AwayFromZero);
        var refund = decimal.Round(vat * (pct / 100m), 2, MidpointRounding.AwayFromZero);
        var barcode = await NextBarcodeAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
        var provider = Clip((request.RefundProvider ?? string.Empty).Trim(), 50);
        if (provider.Length == 0)
        {
            provider = "manual";
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_tourist_refund_invoices` (`company_id`,`invoice_id`,`invoice_no`,`tourist_name`,`passport_no`,`nationality`,`departure_date`,`total_amount`,`vat_amount`,`refund_amount`,`refund_pct`,`barcode`,`refund_provider`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            companyId,
            request.InvoiceId < 0 ? 0 : request.InvoiceId,
            Clip((request.InvoiceNo ?? string.Empty).Trim(), 64),
            tourist,
            Clip((request.PassportNo ?? string.Empty).Trim(), 50),
            Clip((request.Nationality ?? string.Empty).Trim(), 50),
            FormatDateOrNull(request.DepartureDate),
            RoundNonNeg(request.TotalAmount, 2),
            vat,
            refund,
            pct,
            barcode,
            provider,
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Tourist refund " + barcode + " created", id);
    }

    private static async Task<string> NextBarcodeAsync(DbConnection connection, int companyId, CancellationToken cancellationToken)
    {
        var prefix = "TR";
        if (await TableExistsAsync(connection, "epc_tourist_refund_config", cancellationToken).ConfigureAwait(false)
            && await ColumnExistsAsync(connection, "epc_tourist_refund_config", "barcode_prefix", cancellationToken).ConfigureAwait(false))
        {
            var configured = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `barcode_prefix` FROM `epc_tourist_refund_config` WHERE `company_id` = ? LIMIT 1"),
                cancellationToken,
                companyId).ConfigureAwait(false);
            var raw = configured?.Trim() ?? "";
            if (raw.Length > 0)
            {
                prefix = Clip(raw, 10);
            }
        }

        return prefix
               + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
               + Random.Shared.Next(1, 999999).ToString("D6", CultureInfo.InvariantCulture);
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

    private static string? FormatDateOrNull(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

    private static long UnixNow()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
