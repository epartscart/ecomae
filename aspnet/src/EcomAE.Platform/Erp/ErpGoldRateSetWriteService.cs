using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_gold_rate_set</c> twin (manual). API refresh stays PHP.
/// </summary>
public interface IErpGoldRateSetWriteService
{
    Task<ErpSimpleWriteResult> SetAsync(
        ErpGoldRateSetRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpGoldRateSetRequest(
    int CompanyId = 0,
    string? RateDate = null,
    string? Karat = null,
    string? Currency = null,
    decimal BuyRate = 0,
    decimal SellRate = 0,
    string? Unit = null,
    string? Source = null);

public sealed class ErpGoldRateSetWriteService : IErpGoldRateSetWriteService
{
    private static readonly HashSet<string> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        "gram", "ounce", "tola", "kg"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpGoldRateSetWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetAsync(
        ErpGoldRateSetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.BuyRate <= 0 && request.SellRate <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A buy or sell rate greater than zero is required.");
        }

        var currency = Clip((request.Currency ?? string.Empty).Trim().ToUpperInvariant(), 3);
        if (currency.Length != 3 || !currency.All(char.IsLetter))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Currency must be a 3-letter ISO code.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_gold_rates", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_gold_rates", "buy_rate", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Gold rate tables are not provisioned");
        }

        var karat = Clip((request.Karat ?? string.Empty).Trim(), 10);
        if (karat.Length == 0)
        {
            karat = "24K";
        }

        var unit = Clip((request.Unit ?? string.Empty).Trim().ToLowerInvariant(), 10);
        if (!Units.Contains(unit))
        {
            unit = "gram";
        }

        var source = Clip((request.Source ?? string.Empty).Trim(), 50);
        if (source.Length == 0 || source.Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            source = "manual";
        }

        var date = FormatDate(request.RateDate);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_gold_rates` (`company_id`,`rate_date`,`karat`,`currency`,`buy_rate`,`sell_rate`,`unit`,`source`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `buy_rate` = VALUES(`buy_rate`), `sell_rate` = VALUES(`sell_rate`), `source` = VALUES(`source`)"),
            cancellationToken,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            date,
            karat,
            currency,
            RoundNonNeg(request.BuyRate, 4),
            RoundNonNeg(request.SellRate, 4),
            unit,
            source,
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Gold rate set for " + karat + " " + currency, id);
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

    private static string FormatDate(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

    private static long UnixNow()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
