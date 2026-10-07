using System.Data.Common;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp.PriceImport;

public sealed record CpPriceCronSchedule(long Id, bool Active, IReadOnlyList<int> Days, int Hour, int Minute, IReadOnlyList<long> PriceIds)
{
    public string Time => Hour.ToString("00", CultureInfo.InvariantCulture) + ":" + Minute.ToString("00", CultureInfo.InvariantCulture);
}

public sealed record CpPriceCronLaunch(long LaunchId, long CrontabTaskId);

/// <summary>
/// Scheduled supplier updates: <c>for_cron/create_edit_cron_task.php</c> / <c>cron_tasks_actions.php</c> (schedule CRUD on
/// <c>shop_docpart_pyprices_crontab</c> + <c>_crontab_prices</c>), <c>cron_crutch.php</c> (which schedules are due this
/// minute) and <c>cron_task_executor.php</c> (launch row in <c>shop_docpart_prices_cron_executor_launches</c>, then the
/// FTP / e-mail / URL import of every linked list).
/// </summary>
public interface ICpPriceCronService
{
    Task<IReadOnlyList<CpPriceCronSchedule>> ListAsync(long priceId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(long? id, IReadOnlyList<long> priceIds, string? active, IReadOnlyList<string> days, string? time, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Opens a launch row for every schedule due at <paramref name="now"/> that is not already running.</summary>
    Task<IReadOnlyList<CpPriceCronLaunch>> StartDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Runs one opened launch to completion.</summary>
    Task<IReadOnlyList<CpPriceImportResult>> ExecuteAsync(CpPriceCronLaunch launch, CancellationToken cancellationToken = default);
}

public sealed class CpPriceCronService : ICpPriceCronService
{
    /// <summary>A launch older than this no longer blocks the next run (PHP <c>time_start + 1800</c>).</summary>
    public const int StaleLaunchSeconds = 1800;

    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpPriceImportService _imports;

    public CpPriceCronService(IErpWriteConnectionFactory connections, ICpPriceImportService imports)
    {
        _connections = connections;
        _imports = imports;
    }

    public async Task<IReadOnlyList<CpPriceCronSchedule>> ListAsync(long priceId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        var sql = priceId > 0
            ? "SELECT `id`, `active`, `day_week`, `hour`, `minute` FROM `shop_docpart_pyprices_crontab` WHERE `id` IN (SELECT `crontab_task_id` FROM `shop_docpart_pyprices_crontab_prices` WHERE `price_id` = @p0) ORDER BY `id`"
            : "SELECT `id`, `active`, `day_week`, `hour`, `minute` FROM `shop_docpart_pyprices_crontab` ORDER BY `id`";
        var rows = new List<(long Id, bool Active, string Days, int Hour, int Minute)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;
            if (priceId > 0)
            {
                ErpDb.AddParameters(command, priceId);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) != 0,
                    reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture)));
            }
        }

        var schedules = new List<CpPriceCronSchedule>(rows.Count);
        foreach (var row in rows)
        {
            var prices = new List<long>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional("SELECT `price_id` FROM `shop_docpart_pyprices_crontab_prices` WHERE `crontab_task_id` = ? ORDER BY `id`");
                ErpDb.AddParameters(command, row.Id);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    prices.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }

            var days = row.Days.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => int.TryParse(d, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .Where(d => d is >= 1 and <= 7)
                .ToList();
            schedules.Add(new CpPriceCronSchedule(row.Id, row.Active, days, row.Hour, row.Minute, prices));
        }

        return schedules;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return [];
        }
    }

    /// <summary>PHP <c>create_edit_cron_task.php</c> validation ("Validation error 1…8") and write.</summary>
    public async Task<ErpSimpleWriteResult> SaveAsync(long? id, IReadOnlyList<long> priceIds, string? active, IReadOnlyList<string> days, string? time, CancellationToken cancellationToken = default)
    {
        var prices = priceIds.Where(p => p > 0).Distinct().ToList();
        if (prices.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Validation error 1");
        }

        var activeText = (active ?? string.Empty).Trim();
        if (activeText is not ("0" or "1"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Validation error 3");
        }

        var dayList = days.Select(d => (d ?? string.Empty).Trim()).Where(d => d.Length > 0).ToList();
        if (dayList.Count == 0 || dayList.Count > 7)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Validation error 4");
        }

        if (dayList.Any(d => d is not ("1" or "2" or "3" or "4" or "5" or "6" or "7")))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Validation error 5");
        }

        var timeText = (time ?? string.Empty).Trim();
        if (timeText.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Validation error 6");
        }

        var parts = timeText.Split(':');
        if (parts.Length != 2)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Validation error 7");
        }

        var hour = PhpInt(parts[0]);
        var minute = PhpInt(parts[1]);
        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Validation error 8");
        }

        var dayWeek = string.Join(",", dayList);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        var placeholders = string.Join(",", prices.Select(_ => "?"));
        var found = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_prices` WHERE `id` IN (" + placeholders + ")"),
            cancellationToken,
            prices.Cast<object?>().ToArray()).ConfigureAwait(false);
        if (found != prices.Count)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Validation error 2");
        }

        long scheduleId;
        string message;
        if (id is null or <= 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `shop_docpart_pyprices_crontab` (`active`, `day_week`, `month`, `day_month`, `hour`, `minute`) VALUES (?,?,?,?,?,?)"),
                cancellationToken,
                int.Parse(activeText, CultureInfo.InvariantCulture), dayWeek, "*", "*", hour, minute).ConfigureAwait(false);
            scheduleId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            message = "Scheduled update created";
        }
        else
        {
            scheduleId = id.Value;
            var exists = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab` WHERE `id` = ?"),
                cancellationToken,
                scheduleId).ConfigureAwait(false);
            if (exists == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("not_found", "Error - cron task not found");
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `shop_docpart_pyprices_crontab` SET `active` = ?, `day_week` = ?, `month` = ?, `day_month` = ?, `hour` = ?, `minute` = ? WHERE `id` = ?"),
                cancellationToken,
                int.Parse(activeText, CultureInfo.InvariantCulture), dayWeek, "*", "*", hour, minute, scheduleId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `shop_docpart_pyprices_crontab_prices` WHERE `crontab_task_id` = ?"),
                cancellationToken,
                scheduleId).ConfigureAwait(false);
            message = "Scheduled update saved";
        }

        foreach (var priceId in prices)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `shop_docpart_pyprices_crontab_prices` (`price_id`, `crontab_task_id`) VALUES (?, ?)"),
                cancellationToken,
                priceId,
                scheduleId).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "ok", message, scheduleId, 1 + prices.Count);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Price schedules are not in this database.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "cron_task_id is required");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var deleted = await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("DELETE FROM `shop_docpart_pyprices_crontab` WHERE `id` = ?"), cancellationToken, id).ConfigureAwait(false);
            var links = await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("DELETE FROM `shop_docpart_pyprices_crontab_prices` WHERE `crontab_task_id` = ?"), cancellationToken, id).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "OK", id, deleted + links);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Price schedules are not in this database.");
        }
    }

    /// <summary>PHP <c>cron_crutch.php</c>: active, not running (a launch younger than 30 min without <c>time_end</c>), due now.</summary>
    public async Task<IReadOnlyList<CpPriceCronLaunch>> StartDueAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var dayOfWeek = now.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)now.DayOfWeek;
        var unix = now.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
        var due = new List<long>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id` FROM `shop_docpart_pyprices_crontab` WHERE `active` = ?"
                + " AND (SELECT COUNT(*) FROM `shop_docpart_prices_cron_executor_launches` WHERE `crontab_task_id` = `shop_docpart_pyprices_crontab`.`id` AND ISNULL(`time_end`) AND ? < (`time_start` + "
                + StaleLaunchSeconds.ToString(CultureInfo.InvariantCulture) + ")) = ?"
                + " AND `day_week` LIKE ? AND `hour` = ? AND `minute` = ?");
            ErpDb.AddParameters(command, 1, unix, 0, "%" + dayOfWeek.ToString(CultureInfo.InvariantCulture) + "%", now.Hour, now.Minute);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                due.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }

        var launches = new List<CpPriceCronLaunch>(due.Count);
        foreach (var crontabId in due)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_docpart_prices_cron_executor_launches` (`crontab_task_id`, `time_start`, `have_pyprices_query`) VALUES (?, ?, ?)"),
                cancellationToken,
                crontabId, unix, 0).ConfigureAwait(false);
            launches.Add(new CpPriceCronLaunch(await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false), crontabId));
        }

        return launches;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return [];
        }
    }

    /// <summary>PHP <c>cron_task_executor.php</c> after its launch row exists.</summary>
    public async Task<IReadOnlyList<CpPriceImportResult>> ExecuteAsync(CpPriceCronLaunch launch, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var handled = new List<Dictionary<string, object?>>();
        IReadOnlyList<CpPriceImportResult> results = [];
        await using (var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false))
        {
            var lists = await CpPriceListConfig.LoadManyAsync(
                connection,
                ErpDb.Positional("SELECT * FROM `shop_docpart_prices` WHERE `load_mode` != ? AND `id` IN (SELECT `price_id` FROM `shop_docpart_pyprices_crontab_prices` WHERE `crontab_task_id` = ?)"),
                cancellationToken,
                CpPriceListConfig.LoadModePc,
                launch.CrontabTaskId).ConfigureAwait(false);
            foreach (var list in lists)
            {
                handled.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["price_id"] = list.Id,
                    ["price_name"] = list.Name,
                    ["source"] = list.RemoteChannel,
                });
            }
        }

        if (handled.Count == 0)
        {
            errors.Add("No tasks for pyprices. Query will not executed");
        }
        else
        {
            try
            {
                results = await _imports.ImportRemoteAsync(handled.Select(h => (long)h["price_id"]!).ToList(), 0, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is DbException or IOException or InvalidOperationException)
            {
                errors.Add(ex.Message);
            }
        }

        var json = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        await using (var connection = await _connections.OpenAsync(CancellationToken.None).ConfigureAwait(false))
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_docpart_prices_cron_executor_launches` SET `time_end` = ?, `list_to_handle` = ?, `error_messages` = ?, `have_pyprices_query` = ?, `pyprices_answer` = ? WHERE `id` = ?"),
                CancellationToken.None,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                JsonSerializer.Serialize(handled, json),
                JsonSerializer.Serialize(errors, json),
                handled.Count > 0 ? 1 : 0,
                handled.Count > 0 ? JsonSerializer.Serialize(new { list_to_handle = results.Select(r => r.ToPayload()) }, json) : null,
                launch.LaunchId).ConfigureAwait(false);
        }

        return results;
    }

    /// <summary>PHP <c>(int)"07"</c>: leading integer, else 0.</summary>
    private static int PhpInt(string raw)
    {
        var text = raw.Trim();
        var end = 0;
        if (end < text.Length && (text[end] == '-' || text[end] == '+'))
        {
            end++;
        }

        while (end < text.Length && char.IsAsciiDigit(text[end]))
        {
            end++;
        }

        return int.TryParse(text[..end], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
