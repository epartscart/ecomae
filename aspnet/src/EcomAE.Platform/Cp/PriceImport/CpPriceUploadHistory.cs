using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp.PriceImport;

/// <summary>
/// PHP <c>content/shop/docpart/docpart_price_upload_history.php</c>: the <c>epc_price_upload_history</c> table, the
/// <c>/content/files/price_upload_history/{price_id}/</c> source-file archive and the <c>{history_id}_issues.csv</c>
/// download, shared by every upload channel so the CP history panel and the PHP downloads keep working.
/// </summary>
public static class CpPriceUploadHistory
{
    public const string RelativeRoot = "/content/files/price_upload_history";

    /// <summary>Most issue rows written to one issues CSV.</summary>
    public const int MaxIssueRows = 50000;

    private static readonly Regex UnsafeName = new("[^a-zA-Z0-9._-]+", RegexOptions.CultureInvariant);

    /// <summary>PHP <c>epc_price_history_source_label</c> keys written by the native channels.</summary>
    public static string UploadSourceForChannel(string channel) => channel switch
    {
        "pc" => "pyprices_upload",
        "wizard" => "cp_wizard",
        "ftp" => "pyprices_ftp",
        "email" => "pyprices_email",
        "url" => "pyprices_url",
        "api" => "deploy_api",
        "api_reupload" => "deploy_reupload",
        _ => "pyprices",
    };

    /// <summary>PHP <c>epc_price_history_channel_label</c>.</summary>
    public static string ChannelLabel(string channel) => channel switch
    {
        "ftp" => "FTP",
        "email" => "Email",
        "url" => "URL",
        "pc" => "PC upload",
        _ => "Pyprices",
    };

    /// <summary>PHP <c>epc_price_history_ensure_schema</c> (+ issues / active columns).</summary>
    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_price_upload_history` ("
            + " `id` INT(11) NOT NULL AUTO_INCREMENT,"
            + " `price_id` INT(11) NOT NULL DEFAULT 0,"
            + " `price_name` VARCHAR(255) NOT NULL DEFAULT '',"
            + " `upload_source` VARCHAR(32) NOT NULL DEFAULT '',"
            + " `source_ref` VARCHAR(64) NOT NULL DEFAULT '',"
            + " `original_filename` VARCHAR(255) NOT NULL DEFAULT '',"
            + " `stored_relpath` VARCHAR(512) NOT NULL DEFAULT '',"
            + " `file_size` BIGINT NOT NULL DEFAULT 0,"
            + " `rows_imported` INT(11) NOT NULL DEFAULT 0,"
            + " `rows_skipped` INT(11) NOT NULL DEFAULT 0,"
            + " `rows_in_db` INT(11) NOT NULL DEFAULT 0,"
            + " `brands_count` INT(11) NOT NULL DEFAULT 0,"
            + " `items_count` INT(11) NOT NULL DEFAULT 0,"
            + " `status` VARCHAR(16) NOT NULL DEFAULT 'ok',"
            + " `error_text` TEXT NULL,"
            + " `stats_json` LONGTEXT NULL,"
            + " `uploaded_by` INT(11) NOT NULL DEFAULT 0,"
            + " `is_active` TINYINT(1) NOT NULL DEFAULT 0,"
            + " `created_at` DATETIME NOT NULL,"
            + " PRIMARY KEY (`id`),"
            + " KEY `price_id` (`price_id`),"
            + " KEY `created_at` (`created_at`),"
            + " KEY `is_active` (`price_id`, `is_active`),"
            + " KEY `source_ref` (`upload_source`, `source_ref`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_price_upload_history` ADD COLUMN `issues_relpath` VARCHAR(512) NOT NULL DEFAULT '' AFTER `stored_relpath`",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_price_upload_history` ADD COLUMN `is_active` TINYINT(1) NOT NULL DEFAULT 0 AFTER `uploaded_by`",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_price_history_save</c> for a new row (brand / item counts read from the list).</summary>
    public static async Task<long> SaveAsync(DbConnection connection, CpPriceHistoryRow row, CancellationToken cancellationToken)
    {
        var brands = await CountBrandsAsync(connection, row.PriceId, cancellationToken).ConfigureAwait(false);
        var items = await CountItemsAsync(connection, row.PriceId, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_price_upload_history` (`price_id`,`price_name`,`upload_source`,`source_ref`,`original_filename`,`stored_relpath`,`file_size`,"
                + "`rows_imported`,`rows_skipped`,`rows_in_db`,`brands_count`,`items_count`,`status`,`error_text`,`stats_json`,`uploaded_by`,`created_at`)"
                + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,NOW())"),
            cancellationToken,
            row.PriceId,
            Clip(row.PriceName, 255),
            Clip(row.UploadSource, 32),
            Clip(row.SourceRef, 64),
            Clip(row.OriginalFilename, 255),
            Clip(row.StoredRelpath, 512),
            row.FileSize,
            row.RowsImported,
            row.RowsSkipped,
            row.RowsInDb,
            brands,
            items,
            Clip(row.Status, 16),
            row.ErrorText,
            row.StatsJson,
            row.UploadedBy).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id > 0 && row.StoredRelpath.Trim().Length > 0 && row.Status is "ok" or "partial")
        {
            await SetActiveAsync(connection, row.PriceId, id, cancellationToken).ConfigureAwait(false);
        }

        return id;
    }

    /// <summary>PHP <c>epc_price_history_update_by_id</c> with the final import outcome; activates ok/partial rows.</summary>
    public static async Task CompleteAsync(DbConnection connection, long historyId, long priceId, CpPriceHistoryOutcome outcome, CancellationToken cancellationToken)
    {
        if (historyId <= 0)
        {
            return;
        }

        var sets = new List<string>
        {
            "`rows_imported` = ?", "`rows_skipped` = ?", "`rows_in_db` = ?", "`brands_count` = ?", "`items_count` = ?",
            "`status` = ?", "`error_text` = ?", "`stats_json` = ?",
        };
        var values = new List<object?>
        {
            outcome.RowsImported, outcome.RowsSkipped, outcome.RowsInDb, outcome.BrandsCount, outcome.RowsInDb,
            Clip(outcome.Status, 16), outcome.ErrorText, outcome.StatsJson,
        };
        if (outcome.StoredRelpath is { Length: > 0 })
        {
            sets.Add("`stored_relpath` = ?");
            values.Add(Clip(outcome.StoredRelpath, 512));
            sets.Add("`file_size` = ?");
            values.Add(outcome.FileSize);
            sets.Add("`original_filename` = ?");
            values.Add(Clip(outcome.OriginalFilename ?? string.Empty, 255));
        }

        if (outcome.IssuesRelpath is { Length: > 0 })
        {
            sets.Add("`issues_relpath` = ?");
            values.Add(Clip(outcome.IssuesRelpath, 512));
        }

        values.Add(historyId);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_price_upload_history` SET " + string.Join(", ", sets) + " WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            values.ToArray()).ConfigureAwait(false);

        if (outcome.Status is "ok" or "partial")
        {
            await SetActiveAsync(connection, priceId, historyId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>PHP <c>epc_price_history_set_active</c>: one active (downloadable) upload per list.</summary>
    public static async Task SetActiveAsync(DbConnection connection, long priceId, long historyId, CancellationToken cancellationToken)
    {
        if (priceId <= 0 || historyId <= 0)
        {
            return;
        }

        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_price_upload_history` SET `is_active` = 0 WHERE `price_id` = ?"), cancellationToken, priceId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_price_upload_history` SET `is_active` = 1 WHERE `id` = ? AND `price_id` = ? LIMIT 1"), cancellationToken, historyId, priceId).ConfigureAwait(false);
    }

    public static async Task<long> CountItemsAsync(DbConnection connection, long priceId, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?"), cancellationToken, priceId).ConfigureAwait(false);

    public static async Task<long> CountBrandsAsync(DbConnection connection, long priceId, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection, null,
            ErpDb.Positional("SELECT COUNT(DISTINCT `manufacturer`) FROM `shop_docpart_prices_data` WHERE `price_id` = ? AND TRIM(`manufacturer`) <> ''"),
            cancellationToken, priceId).ConfigureAwait(false);

    /// <summary>PHP <c>epc_price_history_archive_file</c>: <c>{time}_{rand}_{safeName}</c> under the list folder.</summary>
    public static string ArchiveFile(string filesRoot, string sourcePath, long priceId, string originalFilename)
    {
        if (priceId <= 0 || !File.Exists(sourcePath))
        {
            return string.Empty;
        }

        var safeName = SafeFileName(originalFilename);
        var directory = Path.Combine(filesRoot, "price_upload_history", priceId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var name = stamp + "_" + RandomNumberGenerator.GetInt32(1000, 10000).ToString(CultureInfo.InvariantCulture) + "_" + safeName;
        var destination = Path.Combine(directory, name);
        File.Copy(sourcePath, destination, overwrite: false);
        return RelativeRoot + "/" + priceId.ToString(CultureInfo.InvariantCulture) + "/" + name;
    }

    public static string SafeFileName(string originalFilename)
    {
        var safe = UnsafeName.Replace(Path.GetFileName(originalFilename ?? string.Empty), "_");
        return safe is "" or "_" ? "upload.csv" : safe;
    }

    /// <summary>PHP <c>epc_price_history_write_issues_file</c>.</summary>
    public static string WriteIssuesFile(string filesRoot, long priceId, long historyId, IReadOnlyList<Dictionary<string, string>> issues)
    {
        if (priceId <= 0 || historyId <= 0 || issues.Count == 0)
        {
            return string.Empty;
        }

        var directory = Path.Combine(filesRoot, "price_upload_history", priceId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        var name = historyId.ToString(CultureInfo.InvariantCulture) + "_issues.csv";
        using (var writer = new StreamWriter(Path.Combine(directory, name), false, new UTF8Encoding(false)))
        {
            foreach (var row in PriceImportIssues.ToCsvRows(issues))
            {
                writer.Write(PriceImportIssues.CsvLine(row));
            }
        }

        return RelativeRoot + "/" + priceId.ToString(CultureInfo.InvariantCulture) + "/" + name;
    }

    /// <summary>PHP <c>epc_price_history_file_absolute_path</c> for a <c>/content/files/…</c> relpath; empty when it escapes the files root.</summary>
    public static string AbsolutePath(string filesRoot, string? relpath)
    {
        var rel = (relpath ?? string.Empty).Trim().Replace('\\', '/');
        const string prefix = "/content/files/";
        if (!rel.StartsWith(prefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var root = Path.GetFullPath(filesRoot);
        var full = Path.GetFullPath(Path.Combine(root, rel[prefix.Length..]));
        return full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : string.Empty;
    }

    public static async Task<Dictionary<string, object?>?> GetRowAsync(DbConnection connection, long historyId, CancellationToken cancellationToken)
    {
        var rows = await ReadRowsAsync(connection, ErpDb.Positional("SELECT * FROM `epc_price_upload_history` WHERE `id` = ? LIMIT 1"), cancellationToken, historyId).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>PHP <c>epc_price_history_get_active</c> → <c>_get_latest_with_file</c>: the newest row whose archived file still exists.</summary>
    public static async Task<Dictionary<string, object?>?> GetActiveAsync(DbConnection connection, string filesRoot, long priceId, CancellationToken cancellationToken)
    {
        if (priceId <= 0)
        {
            return null;
        }

        var active = await ReadRowsAsync(
            connection,
            ErpDb.Positional("SELECT * FROM `epc_price_upload_history` WHERE `price_id` = ? AND `is_active` = 1 ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            priceId).ConfigureAwait(false);
        foreach (var row in active.Concat(await ReadRowsAsync(
                     connection,
                     ErpDb.Positional("SELECT * FROM `epc_price_upload_history` WHERE `price_id` = ? AND TRIM(`stored_relpath`) <> '' ORDER BY `id` DESC LIMIT 20"),
                     cancellationToken,
                     priceId).ConfigureAwait(false)))
        {
            var path = AbsolutePath(filesRoot, Text(row, "stored_relpath"));
            if (path.Length > 0 && File.Exists(path))
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>PHP <c>epc_price_history_get_latest_map</c>: active row per list, else the latest row.</summary>
    public static async Task<IReadOnlyList<Dictionary<string, object?>>> LatestPerListAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, Dictionary<string, object?>>();
        void Take(IEnumerable<Dictionary<string, object?>> rows)
        {
            foreach (var row in rows)
            {
                var pid = Convert.ToInt64(row["price_id"] ?? 0, CultureInfo.InvariantCulture);
                if (pid > 0)
                {
                    map.TryAdd(pid, row);
                }
            }
        }

        Take(await ReadRowsAsync(connection, "SELECT * FROM `epc_price_upload_history` WHERE `is_active` = 1 ORDER BY `id` DESC", cancellationToken).ConfigureAwait(false));
        Take(await ReadRowsAsync(
            connection,
            "SELECT h.* FROM `epc_price_upload_history` h INNER JOIN (SELECT `price_id`, MAX(`id`) AS `max_id` FROM `epc_price_upload_history` GROUP BY `price_id`) t ON h.`id` = t.`max_id` ORDER BY h.`id` DESC",
            cancellationToken).ConfigureAwait(false));
        return map.Values.OrderByDescending(r => Convert.ToInt64(r["id"] ?? 0, CultureInfo.InvariantCulture)).ToList();
    }

    public static async Task<IReadOnlyList<Dictionary<string, object?>>> ListAsync(DbConnection connection, long priceId, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 500);
        return priceId > 0
            ? await ReadRowsAsync(connection, ErpDb.Positional("SELECT * FROM `epc_price_upload_history` WHERE `price_id` = ? ORDER BY `id` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture)), cancellationToken, priceId).ConfigureAwait(false)
            : await ReadRowsAsync(connection, "SELECT * FROM `epc_price_upload_history` ORDER BY `id` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
    }

    public static string Text(IReadOnlyDictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is not null ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] parameters)
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        ErpDb.AddParameters(command, parameters);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(row);
            }
        }
        catch (DbException)
        {
            // History table not created yet on this tenant.
        }

        return rows;
    }

    private static string Clip(string? value, int max)
    {
        value ??= string.Empty;
        return value.Length <= max ? value : value[..max];
    }
}

public sealed record CpPriceHistoryRow(
    long PriceId,
    string PriceName,
    string UploadSource,
    string SourceRef,
    string OriginalFilename,
    string StoredRelpath,
    long FileSize,
    string Status,
    long UploadedBy,
    int RowsImported = 0,
    int RowsSkipped = 0,
    long RowsInDb = 0,
    string ErrorText = "",
    string? StatsJson = null);

public sealed record CpPriceHistoryOutcome(
    int RowsImported,
    int RowsSkipped,
    long RowsInDb,
    long BrandsCount,
    string Status,
    string ErrorText,
    string StatsJson,
    string? StoredRelpath = null,
    long FileSize = 0,
    string? OriginalFilename = null,
    string? IssuesRelpath = null);
