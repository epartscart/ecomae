using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;


// Legacy PHP references twinned by this file (scripts/php_reference_gap_inventory.py):
//   content/shop/finance/epc_erp_gold_rate.php
/// <summary>
/// Live PHP <c>epc_gold_rate_set</c> twin plus a daily karat board. Missing tables are provisioned here.
/// </summary>
public interface IErpGoldRateSetWriteService
{
    Task<ErpSimpleWriteResult> SetAsync(
        ErpGoldRateSetRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PublishDailyAsync(
        ErpGoldRateDailyRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpGoldRateBoardResult> LoadBoardAsync(
        int companyId,
        string? currency = null,
        int days = 30,
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
    string? Source = null,
    string? CreatedBy = null);

public sealed record ErpGoldRateDailyRequest(
    int CompanyId = 0,
    string? RateDate = null,
    string? Currency = null,
    decimal BaseBuy = 0,
    decimal BaseSell = 0,
    string? Unit = null,
    string? Source = null,
    string? CreatedBy = null);

public sealed record ErpGoldRateRow(
    string RateDate,
    string Karat,
    string Currency,
    decimal BuyRate,
    decimal SellRate,
    string Unit,
    string Source,
    string CreatedAt,
    string CreatedBy);

public sealed record ErpGoldRateDayPivot(
    string RateDate,
    decimal Gold24K,
    decimal Gold22K,
    decimal Gold21K,
    decimal Gold18K,
    decimal Silver,
    decimal Change,
    string CreatedAt,
    string CreatedBy);

public sealed record ErpGoldRateBoardResult(
    IReadOnlyList<ErpGoldRateRow> Today,
    IReadOnlyList<ErpGoldRateDayPivot> History,
    string Source,
    string Message);

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

        var currency = (request.Currency ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsLetter))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Currency must be a 3-letter ISO code.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_gold_rates", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_gold_rates", "buy_rate", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Gold rate tables are not provisioned");
        }

        var written = await UpsertAsync(
            connection,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            ErpGoldRateBoard.FormatDate(request.RateDate),
            ErpGoldRateBoard.NormalizeKarat(request.Karat),
            currency,
            request.BuyRate,
            request.SellRate,
            request.Unit,
            request.Source,
            request.CreatedBy,
            cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Gold rate set for " + ErpGoldRateBoard.NormalizeKarat(request.Karat) + " " + currency, written);
    }

    public async Task<ErpSimpleWriteResult> PublishDailyAsync(
        ErpGoldRateDailyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.BaseBuy <= 0 && request.BaseSell <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A 24K buy or sell rate greater than zero is required.");
        }

        var currency = ErpGoldRateBoard.ClipCurrency(request.Currency);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_gold_rates", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Gold rate tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var date = ErpGoldRateBoard.FormatDate(request.RateDate);
        var gramBuy = ErpGoldRateBoard.ToGramRate(request.BaseBuy, request.Unit);
        var gramSell = ErpGoldRateBoard.ToGramRate(request.BaseSell, request.Unit);
        if (gramSell <= 0 && gramBuy > 0)
        {
            gramSell = gramBuy;
        }

        if (gramBuy <= 0 && gramSell > 0)
        {
            gramBuy = gramSell;
        }

        var karats = await LoadKaratSheetAsync(connection, companyId, cancellationToken).ConfigureAwait(false);
        var writes = 0L;
        foreach (var (karat, factor) in karats)
        {
            var buy = decimal.Round(gramBuy * factor, 4, MidpointRounding.AwayFromZero);
            var sell = decimal.Round(gramSell * factor, 4, MidpointRounding.AwayFromZero);
            writes += await UpsertAsync(
                connection,
                companyId,
                date,
                karat,
                currency,
                buy,
                sell,
                "gram",
                string.IsNullOrWhiteSpace(request.Source) ? "daily" : request.Source,
                request.CreatedBy,
                cancellationToken).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok(
            "Daily gold rate published for " + date + " — " + karats.Count.ToString(CultureInfo.InvariantCulture) + " karat rows apply on invoices.",
            writes);
    }

    public async Task<ErpGoldRateBoardResult> LoadBoardAsync(
        int companyId,
        string? currency = null,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], [], "migration", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpJewelSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_gold_rates", cancellationToken).ConfigureAwait(false))
        {
            return new([], [], "database", "Gold rate tables are not provisioned");
        }

        var ccy = ErpGoldRateBoard.ClipCurrency(currency);
        var co = companyId < 0 ? 0 : companyId;
        await ErpGoldRateBoard.EnsureTodayAsync(connection, co, ccy, "system", cancellationToken).ConfigureAwait(false);
        var todayDate = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var today = await ListRowsAsync(connection, co, ccy, todayDate, todayDate, cancellationToken).ConfigureAwait(false);
        var from = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 365)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var historyRows = await ListRowsAsync(connection, co, ccy, from, todayDate, cancellationToken).ConfigureAwait(false);
        return new(today, PivotHistory(historyRows), "database", string.Empty);
    }

    private static async Task<IReadOnlyList<(string Karat, decimal Factor)>> LoadKaratSheetAsync(
        DbConnection connection,
        int companyId,
        CancellationToken cancellationToken)
    {
        var sheet = new List<(string Karat, decimal Factor)>();
        if (await TableExistsAsync(connection, "epc_jewel_karat_master", cancellationToken).ConfigureAwait(false))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `karat_code`, `std_purity` FROM `epc_jewel_karat_master` WHERE ? = 0 OR `company_id` = ? ORDER BY `karat_code`");
            ErpDb.AddParameters(command, companyId, companyId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var karat = ErpGoldRateBoard.NormalizeKarat(Convert.ToString(reader[0], CultureInfo.InvariantCulture));
                var purity = reader[1] is DBNull ? 0m : Convert.ToDecimal(reader[1], CultureInfo.InvariantCulture);
                var factor = ErpGoldRateBoard.FactorFor(karat, purity);
                if (factor > 0)
                {
                    sheet.Add((karat, factor));
                }
            }
        }

        if (sheet.Count == 0)
        {
            return ErpGoldRateBoard.DefaultKarats;
        }

        if (!sheet.Any(row => string.Equals(row.Karat, "24K", StringComparison.OrdinalIgnoreCase)))
        {
            sheet.Insert(0, ("24K", 1m));
        }

        return sheet;
    }

    private static async Task<long> UpsertAsync(
        DbConnection connection,
        int companyId,
        string date,
        string karat,
        string currency,
        decimal buyRate,
        decimal sellRate,
        string? unit,
        string? source,
        string? createdBy,
        CancellationToken cancellationToken)
    {
        var unitKey = (unit ?? string.Empty).Trim().ToLowerInvariant();
        if (!Units.Contains(unitKey))
        {
            unitKey = "gram";
        }

        var sourceKey = ErpGoldRateBoard.Clip((source ?? string.Empty).Trim(), 50);
        if (sourceKey.Length == 0)
        {
            sourceKey = "manual";
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_gold_rates` (`company_id`,`rate_date`,`karat`,`currency`,`buy_rate`,`sell_rate`,`unit`,`source`,`time_created`,`created_by`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `buy_rate` = VALUES(`buy_rate`), `sell_rate` = VALUES(`sell_rate`), `source` = VALUES(`source`)"),
            cancellationToken,
            companyId,
            date,
            karat,
            currency,
            RoundNonNeg(buyRate, 4),
            RoundNonNeg(sellRate, 4),
            unitKey,
            sourceKey,
            ErpGoldRateBoard.UnixNow(),
            ErpGoldRateBoard.Clip((createdBy ?? string.Empty).Trim(), 80)).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<ErpGoldRateRow>> ListRowsAsync(
        DbConnection connection,
        int companyId,
        string currency,
        string fromDate,
        string toDate,
        CancellationToken cancellationToken)
    {
        var rows = new List<ErpGoldRateRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `rate_date`,`karat`,`currency`,`buy_rate`,`sell_rate`,`unit`,`source`,`time_created`,`created_by` FROM `epc_gold_rates` "
            + "WHERE `company_id` = ? AND `currency` = ? AND `rate_date` >= ? AND `rate_date` <= ? ORDER BY `rate_date` DESC, `karat` ASC");
        ErpDb.AddParameters(command, companyId, currency, fromDate, toDate);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpGoldRateRow(
                FormatCellDate(reader[0]),
                Convert.ToString(reader[1], CultureInfo.InvariantCulture) ?? "",
                Convert.ToString(reader[2], CultureInfo.InvariantCulture) ?? "",
                reader[3] is DBNull ? 0 : Convert.ToDecimal(reader[3], CultureInfo.InvariantCulture),
                reader[4] is DBNull ? 0 : Convert.ToDecimal(reader[4], CultureInfo.InvariantCulture),
                Convert.ToString(reader[5], CultureInfo.InvariantCulture) ?? "",
                Convert.ToString(reader[6], CultureInfo.InvariantCulture) ?? "",
                ErpJwMasterAudit.DisplayDate(reader[7]),
                ErpJwMasterAudit.DisplayUser(Convert.ToString(reader[8], CultureInfo.InvariantCulture))));
        }

        return rows;
    }

    private static IReadOnlyList<ErpGoldRateDayPivot> PivotHistory(IReadOnlyList<ErpGoldRateRow> rows)
    {
        var days = new List<ErpGoldRateDayPivot>();
        foreach (var group in rows.GroupBy(row => row.RateDate).OrderByDescending(group => group.Key))
        {
            decimal Pick(string karat) =>
                group.FirstOrDefault(row => string.Equals(row.Karat, karat, StringComparison.OrdinalIgnoreCase))?.SellRate ?? 0;
            var first = group.First();
            days.Add(new ErpGoldRateDayPivot(
                group.Key,
                Pick("24K"),
                Pick("22K"),
                Pick("21K"),
                Pick("18K"),
                Pick("SILVER") + Pick("SI") + Pick("SCA"),
                0,
                first.CreatedAt,
                first.CreatedBy));
        }

        for (var i = 0; i < days.Count - 1; i++)
        {
            var current = days[i];
            var previous = days[i + 1];
            days[i] = current with { Change = current.Gold24K - previous.Gold24K };
        }

        return days;
    }

    private static string FormatCellDate(object? value)
    {
        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? "";
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

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);
}
