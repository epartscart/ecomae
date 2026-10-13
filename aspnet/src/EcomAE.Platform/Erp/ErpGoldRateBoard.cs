using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Daily jewellery gold-rate board: karat factors, today roll-forward, and invoice lookup.
/// </summary>
public static class ErpGoldRateBoard
{
    public const decimal GramsPerOunce = 31.1035m;

    public static readonly IReadOnlyList<(string Karat, decimal Factor)> DefaultKarats =
    [
        ("24K", 1.0000m),
        ("22K", 0.9167m),
        ("21K", 0.8750m),
        ("20K", 0.8333m),
        ("18K", 0.7500m),
        ("14K", 0.5833m),
        ("9K", 0.3750m),
    ];

    public static string NormalizeKarat(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToUpperInvariant();
        if (value.Length == 0)
        {
            return "24K";
        }

        if (value.EndsWith("KARAT", StringComparison.Ordinal))
        {
            value = value[..^5].Trim();
        }

        if (value.EndsWith('K') && value.Length > 1)
        {
            return value;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? value + "K"
            : value;
    }

    public static decimal FactorFor(string karat, decimal masterPurity = 0)
    {
        if (masterPurity > 0)
        {
            return decimal.Round(masterPurity, 6, MidpointRounding.AwayFromZero);
        }

        var key = NormalizeKarat(karat);
        foreach (var row in DefaultKarats)
        {
            if (string.Equals(row.Karat, key, StringComparison.OrdinalIgnoreCase))
            {
                return row.Factor;
            }
        }

        return 0;
    }

    public static string KaratFromPurity(decimal purity)
    {
        var fine = decimal.Round(purity < 0 ? 0 : purity, 4, MidpointRounding.AwayFromZero);
        if (fine <= 0)
        {
            return "24K";
        }

        foreach (var row in DefaultKarats)
        {
            if (Math.Abs(row.Factor - fine) <= 0.01m)
            {
                return row.Karat;
            }
        }

        return "24K";
    }

    public static string FormatDate(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            || DateTime.TryParse(value, out parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static decimal ToGramRate(decimal rate, string? unit)
    {
        var key = (unit ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "ounce" or "oz" => decimal.Round(rate / GramsPerOunce, 4, MidpointRounding.AwayFromZero),
            "tola" => decimal.Round(rate / 11.6638m, 4, MidpointRounding.AwayFromZero),
            "kg" => decimal.Round(rate / 1000m, 4, MidpointRounding.AwayFromZero),
            _ => decimal.Round(rate < 0 ? 0 : rate, 4, MidpointRounding.AwayFromZero)
        };
    }

    public static async Task<decimal> ResolveSellAsync(
        DbConnection connection,
        int companyId,
        string? karat,
        string? currency,
        string? rateDate,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var date = FormatDate(rateDate);
        var code = NormalizeKarat(karat);
        var ccy = ClipCurrency(currency);
        await EnsureTodayAsync(connection, companyId, ccy, "system", cancellationToken).ConfigureAwait(false);

        var sell = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `sell_rate` FROM `epc_gold_rates` WHERE `company_id` = ? AND `rate_date` = ? AND `karat` = ? AND `currency` = ? LIMIT 1"),
            cancellationToken,
            companyId,
            date,
            code,
            ccy).ConfigureAwait(false);
        if (sell > 0)
        {
            return sell;
        }

        return await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `sell_rate` FROM `epc_gold_rates` WHERE `company_id` = ? AND `rate_date` = ? AND `currency` = ? AND `karat` = '24K' AND `sell_rate` > 0 LIMIT 1"),
            cancellationToken,
            companyId,
            date,
            ccy).ConfigureAwait(false);
    }

    public static async Task<int> EnsureTodayAsync(
        DbConnection connection,
        int companyId,
        string currency,
        string createdBy,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM `epc_gold_rates` WHERE `company_id` = ? AND `rate_date` = ? AND `currency` = ?"),
            cancellationToken,
            companyId,
            today,
            currency).ConfigureAwait(false);
        if (existing > 0)
        {
            return 0;
        }

        var lastDate = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `rate_date` FROM `epc_gold_rates` WHERE `company_id` = ? AND `currency` = ? AND `rate_date` < ? ORDER BY `rate_date` DESC LIMIT 1"),
            cancellationToken,
            companyId,
            currency,
            today).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(lastDate))
        {
            return 0;
        }

        var copied = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_gold_rates` (`company_id`,`rate_date`,`karat`,`currency`,`buy_rate`,`sell_rate`,`unit`,`source`,`time_created`,`created_by`) "
                + "SELECT `company_id`, ?, `karat`, `currency`, `buy_rate`, `sell_rate`, `unit`, 'daily', ?, ? FROM `epc_gold_rates` "
                + "WHERE `company_id` = ? AND `rate_date` = ? AND `currency` = ? "
                + "ON DUPLICATE KEY UPDATE `buy_rate` = VALUES(`buy_rate`), `sell_rate` = VALUES(`sell_rate`), `source` = VALUES(`source`)"),
            cancellationToken,
            today,
            UnixNow(),
            Clip(createdBy, 80),
            companyId,
            lastDate,
            currency).ConfigureAwait(false);
        return copied;
    }

    public static string ClipCurrency(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToUpperInvariant();
        if (value.Length != 3 || !value.All(char.IsLetter))
        {
            return "AED";
        }

        return value;
    }

    public static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

    public static long UnixNow()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static async Task<bool> TableExistsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            "epc_gold_rates").ConfigureAwait(false);
        return n > 0;
    }
}
