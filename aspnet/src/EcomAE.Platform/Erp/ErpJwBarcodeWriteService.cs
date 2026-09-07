using System.Data.Common;
using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_jewel_barcode_generate</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpJwBarcodeWriteService
{
    Task<ErpSimpleWriteResult> GenerateAsync(
        ErpJwBarcodeGenerateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwBarcodeGenerateRequest(
    int CompanyId = 0,
    string? StockCode = null,
    string? Division = null,
    string? Karat = null,
    decimal GrossWt = 0,
    decimal Purity = 0,
    decimal TagPrice = 0);

public sealed class ErpJwBarcodeWriteService : IErpJwBarcodeWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwBarcodeWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> GenerateAsync(
        ErpJwBarcodeGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stockCode = (request.StockCode ?? string.Empty).Trim();
        if (stockCode.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Stock code is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_barcode", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery barcode tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var division = (request.Division ?? string.Empty).Trim().ToUpperInvariant();
        if (division.Length == 0)
        {
            division = "G";
        }

        division = Clip(division, 2);
        var karat = Clip((request.Karat ?? string.Empty).Trim(), 10);
        var prefix = BuildPrefix(division, karat);
        var seq = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_jewel_barcode` WHERE `company_id` = ?"),
            cancellationToken,
            companyId).ConfigureAwait(false);
        var barcode = prefix + (seq + 1).ToString("00000000", CultureInfo.InvariantCulture);
        var gross = RoundNonNeg(request.GrossWt, 4);
        var purity = RoundNonNeg(request.Purity, 6);
        var net = decimal.Round(gross * purity, 4, MidpointRounding.AwayFromZero);
        var tagPrice = RoundNonNeg(request.TagPrice, 2);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_barcode` (`company_id`,`stock_code`,`barcode`,`item_type`,`division`,`karat`,`gross_wt`,`net_wt`,`purity`,`tag_price`) VALUES (?, ?, ?, 'metal', ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            companyId,
            Clip(stockCode, 20),
            Clip(barcode, 40),
            division,
            karat,
            gross,
            net,
            purity,
            tagPrice).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Barcode " + barcode + " generated", id);
    }

    private static string BuildPrefix(string division, string karat)
    {
        var head = division.Length == 0 ? "G" : division[..1].ToUpperInvariant();
        var builder = new StringBuilder(head.Length + karat.Length);
        builder.Append(head);
        foreach (var ch in karat)
        {
            if (ch is not (' ' or '.'))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().ToUpperInvariant();
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

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
