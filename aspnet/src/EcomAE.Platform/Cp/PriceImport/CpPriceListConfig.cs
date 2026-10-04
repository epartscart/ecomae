using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp.PriceImport;

/// <summary>
/// One <c>shop_docpart_prices</c> row as <c>prices_manager.php</c> / <c>cron_task_executor.php</c> turn it into a
/// pyprices task (<c>load_mode</c> 1 = PC upload, 2 = FTP, 3 = e-mail, 4 = URL).
/// </summary>
public sealed record CpPriceListConfig(
    long Id,
    string Name,
    int LoadMode,
    string FtpHost,
    string FtpUser,
    string FtpPassword,
    string FtpFolder,
    string SenderEmail,
    bool NotMarkSeenEmailMessages,
    string MessageHeaderSubstring,
    int StringsToLeft,
    PriceListColumnMap Columns,
    bool CleanBefore,
    string FileNameSubstring,
    string FileNameSubstringArch,
    string Link,
    string Encoding,
    string Separator)
{
    public const int LoadModePc = 1;
    public const int LoadModeFtp = 2;
    public const int LoadModeEmail = 3;
    public const int LoadModeUrl = 4;

    /// <summary>pyprices <c>task.source</c> for the list's own load mode.</summary>
    public string RemoteChannel => LoadMode switch
    {
        LoadModeFtp => "ftp",
        LoadModeEmail => "email",
        LoadModeUrl => "url",
        _ => "pc",
    };

    public static string LoadModeLabel(int loadMode) => loadMode switch
    {
        LoadModePc => "PC upload",
        LoadModeFtp => "FTP",
        LoadModeEmail => "E-mail",
        LoadModeUrl => "URL",
        _ => "Unknown",
    };

    /// <summary>pyprices <c>item_to_handle</c> validation for the column layout and the chosen source.</summary>
    public IReadOnlyList<string> Validate(string channel)
    {
        var messages = new List<string>();
        if (Columns.Article <= 0)
        {
            messages.Add("Required parameter col_article has an incorrect value " + Columns.Article.ToString(CultureInfo.InvariantCulture));
        }

        if (Columns.Price <= 0)
        {
            messages.Add("Required parameter col_price has an incorrect value " + Columns.Price.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var (role, col) in Columns.ByRole)
        {
            if (col < 0 || col > 1000)
            {
                messages.Add("Column " + role + " has an incorrect value " + col.ToString(CultureInfo.InvariantCulture));
            }
        }

        switch (channel)
        {
            case "ftp":
                if (FtpHost.Trim().Length == 0)
                {
                    messages.Add("Parameter not found in object: ftp_host");
                }

                if (FtpUser.Trim().Length == 0)
                {
                    messages.Add("Parameter not found in object: ftp_username");
                }

                break;
            case "email":
                if (SenderEmail.Trim().Length == 0)
                {
                    messages.Add("Parameter not found in object: email_price_sender");
                }

                break;
            case "url":
                if (!Uri.TryCreate(Link.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    messages.Add("Parameter url must be an absolute http(s) link");
                }

                break;
        }

        return messages;
    }

    public static async Task<CpPriceListConfig?> LoadAsync(DbConnection connection, long priceId, CancellationToken cancellationToken)
    {
        if (priceId <= 0)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT * FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1");
        ErpDb.AddParameters(command, priceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return FromRecord(reader);
    }

    public static async Task<IReadOnlyList<CpPriceListConfig>> LoadManyAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        ErpDb.AddParameters(command, parameters);
        var lists = new List<CpPriceListConfig>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            lists.Add(FromRecord(reader));
        }

        return lists;
    }

    private static CpPriceListConfig FromRecord(DbDataReader reader)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            columns[reader.GetName(i)] = i;
        }

        string Text(string name)
        {
            if (!columns.TryGetValue(name, out var index) || reader.IsDBNull(index))
            {
                return string.Empty;
            }

            return Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty;
        }

        int Int(string name)
        {
            var raw = Text(name).Trim();
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec) ? (int)dec : 0;
        }

        var cleanRaw = Text("clean_before").Trim();
        return new CpPriceListConfig(
            Convert.ToInt64(reader.GetValue(columns["id"]), CultureInfo.InvariantCulture),
            Text("name"),
            Int("load_mode"),
            Text("ftp_host"),
            Text("ftp_user"),
            Text("ftp_password"),
            Text("ftp_folder"),
            Text("sender_email"),
            Int("not_mark_seen_email_messages") != 0,
            Text("message_header_substring"),
            Math.Max(0, Int("strings_to_left")),
            new PriceListColumnMap(
                Int("manufacturer_col"),
                Int("article_col"),
                Int("name_col"),
                Int("exist_col"),
                Int("price_col"),
                Int("time_to_exe_col"),
                Int("storage_col"),
                Int("min_order_col")),
            cleanRaw.Length == 0 || cleanRaw != "0",
            Text("file_name_substring"),
            Text("file_name_substring_arch"),
            Text("link"),
            Text("encoding"),
            Text("separator"));
    }

    /// <summary>
    /// Wizard column map (PHP price-list settings that <c>ajax_5_import_csv_to_db.php</c> reads).
    /// Only keys present in <paramref name="fields"/> are written.
    /// </summary>
    public static async Task<int> SaveLayoutAsync(DbConnection connection, long priceId, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        if (priceId <= 0 || fields.Count == 0)
        {
            return 0;
        }

        var sets = new List<string>();
        var values = new List<object?>();
        void IntCol(string key, string column)
        {
            if (!fields.TryGetValue(key, out var raw))
            {
                return;
            }

            if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return;
            }

            sets.Add("`" + column + "` = ?");
            values.Add(value);
        }

        void TextCol(string key, string column, int max)
        {
            if (!fields.TryGetValue(key, out var raw))
            {
                return;
            }

            var text = raw.Trim();
            sets.Add("`" + column + "` = ?");
            values.Add(text.Length <= max ? text : text[..max]);
        }

        IntCol("strings_to_left", "strings_to_left");
        IntCol("manufacturer_col", "manufacturer_col");
        IntCol("article_col", "article_col");
        IntCol("name_col", "name_col");
        IntCol("exist_col", "exist_col");
        IntCol("price_col", "price_col");
        IntCol("time_to_exe_col", "time_to_exe_col");
        IntCol("storage_col", "storage_col");
        IntCol("min_order_col", "min_order_col");
        IntCol("clean_before", "clean_before");
        TextCol("encoding", "encoding", 16);
        TextCol("separator", "separator", 8);
        TextCol("file_name_substring", "file_name_substring", 255);
        if (sets.Count == 0)
        {
            return 0;
        }

        values.Add(priceId);
        return await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_prices` SET " + string.Join(", ", sets) + " WHERE `id` = ?"),
            cancellationToken,
            values.ToArray()).ConfigureAwait(false);
    }
}
