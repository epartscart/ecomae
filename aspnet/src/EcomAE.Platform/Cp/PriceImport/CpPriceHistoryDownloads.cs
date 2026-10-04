using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp.PriceImport;

public sealed record CpPriceDownload(byte[] Content, string FileName, string ContentType);

/// <summary>
/// PHP <c>ajax_epc_price_upload_history.php</c> downloads: the archived source file (<c>download</c>,
/// <c>download_latest</c> — falling back to the DB export when the archive is gone), the issues CSV filtered by
/// <c>issue_type</c> (<c>download_issues</c> / <c>download_skipped</c> / <c>download_errors</c>) and <c>export_db</c>.
/// </summary>
public static class CpPriceHistoryDownloads
{
    private static readonly Regex UnsafeName = new("[^a-zA-Z0-9._-]+", RegexOptions.CultureInvariant);

    public static async Task<CpPriceDownload?> SourceFileAsync(DbConnection connection, string filesRoot, long historyId, CancellationToken cancellationToken)
    {
        var row = await CpPriceUploadHistory.GetRowAsync(connection, historyId, cancellationToken).ConfigureAwait(false);
        return row is null ? null : await FromRowAsync(row, filesRoot, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CpPriceDownload?> LatestAsync(DbConnection connection, string filesRoot, long priceId, CancellationToken cancellationToken)
    {
        var row = await CpPriceUploadHistory.GetActiveAsync(connection, filesRoot, priceId, cancellationToken).ConfigureAwait(false);
        var file = row is null ? null : await FromRowAsync(row, filesRoot, cancellationToken).ConfigureAwait(false);
        return file ?? (priceId > 0 ? await ExportDbAsync(connection, priceId, cancellationToken).ConfigureAwait(false) : null);
    }

    private static async Task<CpPriceDownload?> FromRowAsync(IReadOnlyDictionary<string, object?> row, string filesRoot, CancellationToken cancellationToken)
    {
        var path = CpPriceUploadHistory.AbsolutePath(filesRoot, CpPriceUploadHistory.Text(row, "stored_relpath"));
        if (path.Length == 0 || !File.Exists(path))
        {
            return null;
        }

        var name = CpPriceUploadHistory.Text(row, "original_filename").Replace("\"", string.Empty, StringComparison.Ordinal);
        if (name.Length == 0)
        {
            name = "price_upload_" + CpPriceUploadHistory.Text(row, "id") + ".csv";
        }

        return new CpPriceDownload(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false), name, "application/octet-stream");
    }

    /// <summary>PHP <c>epc_price_history_stream_issues_csv</c>; <paramref name="filter"/> is <c>all</c>, <c>skipped</c> or <c>error</c>.</summary>
    public static async Task<CpPriceDownload?> IssuesAsync(DbConnection connection, string filesRoot, long historyId, string filter, CancellationToken cancellationToken)
    {
        var row = await CpPriceUploadHistory.GetRowAsync(connection, historyId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var label = filter == "all" ? "issues" : filter;
        var safe = UnsafeName.Replace(CpPriceUploadHistory.Text(row, "price_name"), "_");
        var fileName = (safe.Length > 0 ? safe : "price") + "_" + label + "_upload_" + CpPriceUploadHistory.Text(row, "id") + ".csv";
        var path = CpPriceUploadHistory.AbsolutePath(filesRoot, CpPriceUploadHistory.Text(row, "issues_relpath"));
        var output = new StringBuilder();
        if (path.Length > 0 && File.Exists(path))
        {
            var text = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            var typeIndex = 1;
            var first = true;
            foreach (var record in PyCsvReader.Read(text, ','))
            {
                if (first)
                {
                    first = false;
                    var found = record.IndexOf("issue_type");
                    typeIndex = found >= 0 ? found : 1;
                    output.Append(PriceImportIssues.CsvLine(record));
                    continue;
                }

                var type = typeIndex < record.Count ? record[typeIndex] : string.Empty;
                if ((filter == "skipped" && type != "skipped") || (filter == "error" && type != "error"))
                {
                    continue;
                }

                output.Append(PriceImportIssues.CsvLine(record));
            }
        }
        else
        {
            output.Append(PriceImportIssues.CsvLine(["line_no", "issue_type", "reason_code", "why_skipped_or_error", "error_details"]));
            var errorText = CpPriceUploadHistory.Text(row, "error_text");
            if (errorText.Length > 0 && filter != "skipped")
            {
                output.Append(PriceImportIssues.CsvLine(["0", "error", "import_error", PriceImportIssues.ReasonLabel("import_error") + " — " + errorText, errorText]));
            }
        }

        return new CpPriceDownload(new UTF8Encoding(false).GetBytes(output.ToString()), fileName, "text/csv; charset=utf-8");
    }

    /// <summary>PHP <c>epc_history_stream_db_export</c>.</summary>
    public static async Task<CpPriceDownload?> ExportDbAsync(DbConnection connection, long priceId, CancellationToken cancellationToken)
    {
        if (priceId <= 0)
        {
            return null;
        }

        var priceName = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1"), cancellationToken, priceId).ConfigureAwait(false) ?? string.Empty;
        var safe = UnsafeName.Replace(priceName, "_");
        var fileName = (safe.Length > 0 ? safe : "price") + "_export_" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + ".csv";
        var output = new StringBuilder();
        output.Append(PriceImportIssues.CsvLine(["manufacturer", "article", "article_show", "name", "exist", "price", "time_to_exe", "storage", "min_order"]));
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `manufacturer`,`article`,`article_show`,`name`,`exist`,`price`,`time_to_exe`,`storage`,`min_order` FROM `shop_docpart_prices_data` WHERE `price_id` = ? ORDER BY `manufacturer`,`article`");
        ErpDb.AddParameters(command, priceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var cells = new string[9];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i] = reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
            }

            output.Append(PriceImportIssues.CsvLine(cells));
        }

        return new CpPriceDownload(new UTF8Encoding(false).GetBytes(output.ToString()), fileName, "text/csv; charset=utf-8");
    }
}
