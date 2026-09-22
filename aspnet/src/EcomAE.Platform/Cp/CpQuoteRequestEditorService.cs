using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpQuoteCustomer(
    long UserId,
    string Name,
    string Email,
    string Phone,
    string Company,
    string Label,
    string ProfileHref);

public sealed record CpQuoteListRow(
    long Id,
    string Status,
    long TimeUpdated,
    CpQuoteCustomer Customer);

public sealed record CpQuoteLine(
    long Id,
    string RequestedManufacturer,
    string RequestedArticle,
    string RequestedName,
    int CountNeed,
    decimal? QuotedPrice,
    int? QuotedTimeToExe,
    string LineAdminNote,
    bool OfferAlternative,
    string AltManufacturer,
    string AltArticle,
    string AltName,
    int? AltCountNeed,
    decimal? AltQuotedPrice,
    long AltStorageId,
    string AltStorageLabel)
{
    public string RequestedLabel
    {
        get
        {
            var l = (RequestedManufacturer + " " + RequestedArticle).Trim();
            return RequestedName.Length > 0 ? (l + " — " + RequestedName).Trim(' ', '—') : l;
        }
    }

    public string AltSummary => !OfferAlternative
        ? ""
        : (AltManufacturer + " " + AltArticle + " × " + (AltCountNeed ?? 1).ToString(CultureInfo.InvariantCulture)
            + " @ " + (AltQuotedPrice ?? 0).ToString("0.##", CultureInfo.InvariantCulture)
            + (AltStorageLabel.Length > 0 ? " · " + AltStorageLabel : "")).Trim();
}

public sealed record CpQuoteDetail(
    long Id,
    string Status,
    long TimeCreated,
    long TimeUpdated,
    long TimeSubmitted,
    long AcceptedOrderId,
    string CustomerNote,
    string AdminNote,
    CpQuoteCustomer Customer,
    IReadOnlyList<CpQuoteLine> Lines,
    string CurrencySign);

public sealed record CpQuoteAltWarehouse(
    long StorageId,
    string Label,
    decimal Price,
    decimal Qty,
    int? Delivery);

public sealed record CpQuoteAltOption(
    string Key,
    string Brand,
    string Article,
    string ArticleShow,
    string Name,
    string Source,
    IReadOnlyList<CpQuoteAltWarehouse> Warehouses)
{
    public bool InStock => Warehouses.Any(w => w.Qty > 0);
}

public sealed record CpQuoteAltOptions(
    long QuoteId,
    long LineId,
    string RequestedBrand,
    string RequestedArticle,
    string RequestedName,
    IReadOnlyList<CpQuoteAltOption> Alternatives,
    IReadOnlyList<CpQuoteAltWarehouse> WarehousesAll);

/// <summary>Read side of cp/content/shop/quote_requests (quote_requests.php list/detail + ajax_epc_quote_alt_options.php).</summary>
public interface ICpQuoteRequestEditorService
{
    Task<IReadOnlyList<CpQuoteListRow>> ListAsync(string? status, CancellationToken cancellationToken = default);

    Task<CpQuoteDetail?> OpenAsync(long quoteId, CancellationToken cancellationToken = default);

    Task<CpQuoteAltOptions?> AltOptionsAsync(long quoteId, long lineId, CancellationToken cancellationToken = default);
}

public sealed class CpQuoteRequestEditorService : ICpQuoteRequestEditorService
{
    public static readonly string[] Statuses = ["draft", "submitted", "quoted", "accepted"];

    private readonly IErpWriteConnectionFactory _connections;

    public CpQuoteRequestEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<CpQuoteListRow>> ListAsync(string? status, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        var filter = (status ?? "").Trim();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<(long Id, long UserId, string Status, long Updated)>();
            await using (var cmd = connection.CreateCommand())
            {
                var sql = "SELECT `id`, `user_id`, IFNULL(`status`,''), IFNULL(`time_updated`,0) FROM `shop_quote_requests` WHERE `user_id` > 0";
                if (filter.Length > 0)
                {
                    sql += " AND `status` = ?";
                }

                cmd.CommandText = ErpDb.Positional(sql + " ORDER BY `id` DESC LIMIT 200");
                if (filter.Length > 0)
                {
                    ErpDb.AddParameters(cmd, filter);
                }

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add((reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetInt64(3)));
                }
            }

            var customers = new Dictionary<long, CpQuoteCustomer>();
            var list = new List<CpQuoteListRow>(rows.Count);
            foreach (var r in rows)
            {
                if (!customers.TryGetValue(r.UserId, out var c))
                {
                    c = await CustomerAsync(connection, r.UserId, cancellationToken).ConfigureAwait(false);
                    customers[r.UserId] = c;
                }

                list.Add(new CpQuoteListRow(r.Id, r.Status, r.Updated, c));
            }

            return list;
        }
        catch (DbException)
        {
            return [];
        }
    }

    public async Task<CpQuoteDetail?> OpenAsync(long quoteId, CancellationToken cancellationToken = default)
    {
        if (quoteId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            long userId = 0, created = 0, updated = 0, submitted = 0, order = 0;
            string status = "", customerNote = "", adminNote = "";
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `user_id`, IFNULL(`status`,''), IFNULL(`time_created`,0), IFNULL(`time_updated`,0), IFNULL(`time_submitted`,0),"
                    + " IFNULL(`accepted_order_id`,0), IFNULL(`customer_note`,''), IFNULL(`admin_note`,'')"
                    + " FROM `shop_quote_requests` WHERE `id` = ? AND `user_id` > 0 LIMIT 1");
                ErpDb.AddParameters(cmd, quoteId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                userId = reader.GetInt64(0);
                status = reader.GetString(1);
                created = reader.GetInt64(2);
                updated = reader.GetInt64(3);
                submitted = reader.GetInt64(4);
                order = reader.GetInt64(5);
                customerNote = reader.GetString(6);
                adminNote = reader.GetString(7);
            }

            var customer = await CustomerAsync(connection, userId, cancellationToken).ConfigureAwait(false);
            var lines = await LinesAsync(connection, quoteId, cancellationToken).ConfigureAwait(false);
            var sign = await CurrencySignAsync(connection, cancellationToken).ConfigureAwait(false);
            return new CpQuoteDetail(quoteId, status, created, updated, submitted, order, customerNote, adminNote, customer, lines, sign);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpQuoteAltOptions?> AltOptionsAsync(long quoteId, long lineId, CancellationToken cancellationToken = default)
    {
        if (quoteId <= 0 || lineId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var lines = await LinesAsync(connection, quoteId, cancellationToken).ConfigureAwait(false);
            var line = lines.FirstOrDefault(l => l.Id == lineId);
            if (line is null)
            {
                return null;
            }

            var reqBrand = PrepareBrand(line.RequestedManufacturer);
            var reqArticle = NormalizeArticle(line.RequestedArticle);
            if (reqArticle.Length == 0)
            {
                return null;
            }

            var warehousesAll = await WarehousesAsync(connection, cancellationToken).ConfigureAwait(false);
            var whLabel = warehousesAll.ToDictionary(w => w.StorageId, w => w.Label);

            var byKey = new Dictionary<string, (string Brand, string Article, string Show, string Name, string Source, List<CpQuoteAltWarehouse> Wh)>(StringComparer.Ordinal);
            var order = new List<string>();
            void Ensure(string brand, string show, string name, string source)
            {
                brand = PrepareBrand(brand);
                show = show.Trim();
                var norm = NormalizeArticle(show);
                if (norm.Length == 0)
                {
                    return;
                }

                var key = brand + "|" + norm;
                if (!byKey.TryGetValue(key, out var e))
                {
                    byKey[key] = (brand, norm, show.Length > 0 ? show : norm, name.Trim(), source, new List<CpQuoteAltWarehouse>());
                    order.Add(key);
                    return;
                }

                if (e.Name.Length == 0 && name.Trim().Length > 0)
                {
                    e.Name = name.Trim();
                }

                if (source.Length > 0 && !e.Source.Contains(source, StringComparison.OrdinalIgnoreCase))
                {
                    e.Source = e.Source.Length == 0 ? source : e.Source + "+" + source;
                }

                if (show.Length > e.Show.Length)
                {
                    e.Show = show;
                }

                byKey[key] = e;
            }

            Ensure(reqBrand, line.RequestedArticle.Length > 0 ? line.RequestedArticle : reqArticle, line.RequestedName, "requested");

            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`manufacturer_analog`,''), IFNULL(`analog`,'') FROM `shop_docpart_articles_analogs_list` WHERE `article` = ?"
                    + " UNION SELECT IFNULL(`manufacturer_article`,''), IFNULL(`article`,'') FROM `shop_docpart_articles_analogs_list` WHERE `analog` = ? LIMIT 200");
                ErpDb.AddParameters(cmd, reqArticle, reqArticle);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    Ensure(reader.GetString(0), reader.GetString(1), "", "cross");
                }
            }

            var articles = byKey.Values.Select(v => v.Article).Distinct(StringComparer.Ordinal).Take(60).ToArray();
            if (articles.Length > 0)
            {
                await using var cmd = connection.CreateCommand();
                var placeholders = string.Join(",", articles.Select(_ => "?"));
                cmd.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`manufacturer`,''), IFNULL(`article`,''), IFNULL(NULLIF(`article_show`,''),`article`), IFNULL(`name`,''),"
                    + " IFNULL(`price`,0), IFNULL(`exist`,0), IFNULL(`storage`,0), IFNULL(`time_to_exe`,0)"
                    + " FROM `shop_docpart_prices_data` WHERE UPPER(REPLACE(`article`,' ','')) IN (" + placeholders + ") LIMIT 500");
                ErpDb.AddParameters(cmd, articles.Cast<object?>().ToArray());
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var brand = reader.GetString(0);
                    var show = reader.GetString(2);
                    Ensure(brand, show, reader.GetString(3), "stock");
                    var key = PrepareBrand(brand) + "|" + NormalizeArticle(show);
                    if (!byKey.TryGetValue(key, out var e))
                    {
                        continue;
                    }

                    var storageId = reader.IsDBNull(6) ? 0 : Convert.ToInt64(reader.GetValue(6), CultureInfo.InvariantCulture);
                    if (storageId <= 0)
                    {
                        continue;
                    }

                    var price = Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture);
                    var qty = Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture);
                    var delivery = Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture);
                    var label = whLabel.TryGetValue(storageId, out var l) ? l : "Warehouse #" + storageId.ToString(CultureInfo.InvariantCulture);
                    var idx = e.Wh.FindIndex(w => w.StorageId == storageId);
                    var row = new CpQuoteAltWarehouse(storageId, label, price, qty, delivery > 0 ? delivery : null);
                    if (idx < 0)
                    {
                        e.Wh.Add(row);
                    }
                    else if (e.Wh[idx].Qty <= 0 && qty > 0)
                    {
                        e.Wh[idx] = row;
                    }
                }
            }

            var alternatives = order
                .Select(k => byKey[k])
                .Select(v => new CpQuoteAltOption(
                    v.Brand + "|" + v.Article,
                    v.Brand,
                    v.Article,
                    v.Show,
                    v.Name,
                    v.Source,
                    v.Wh.OrderByDescending(w => w.Qty > 0).ThenBy(w => w.Price).ToList()))
                .ToList();
            var requested = alternatives.Take(1);
            var rest = alternatives.Skip(1).OrderByDescending(a => a.InStock).ThenBy(a => a.Brand, StringComparer.Ordinal).ThenBy(a => a.ArticleShow, StringComparer.Ordinal);
            return new CpQuoteAltOptions(
                quoteId,
                lineId,
                reqBrand,
                line.RequestedArticle.Length > 0 ? line.RequestedArticle : reqArticle,
                line.RequestedName,
                requested.Concat(rest).ToList(),
                warehousesAll);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<CpQuoteLine>> LinesAsync(DbConnection connection, long quoteId, CancellationToken cancellationToken)
    {
        var raw = new List<(long Id, string Json, int Count, decimal? Price, int? Time, string Note, bool Alt, string AltMfr, string AltArt, string AltName, int? AltQty, decimal? AltPrice, long AltStorage)>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`product_object_json`,''), IFNULL(`count_need`,0), `quoted_price`, `quoted_time_to_exe`, IFNULL(`line_admin_note`,''),"
                + " IFNULL(`offer_alternative`,0), IFNULL(`alt_manufacturer`,''), IFNULL(NULLIF(`alt_article_show`,''),IFNULL(`alt_article`,'')), IFNULL(`alt_name`,''),"
                + " `alt_count_need`, `alt_quoted_price`, IFNULL(`alt_storage_id`,0)"
                + " FROM `shop_quote_items` WHERE `quote_id` = ? ORDER BY `id` ASC");
            ErpDb.AddParameters(cmd, quoteId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    reader.IsDBNull(3) ? null : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                    reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture),
                    reader.GetString(5),
                    Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture) == 1,
                    reader.GetString(7),
                    reader.GetString(8),
                    reader.GetString(9),
                    reader.IsDBNull(10) ? null : Convert.ToInt32(reader.GetValue(10), CultureInfo.InvariantCulture),
                    reader.IsDBNull(11) ? null : Convert.ToDecimal(reader.GetValue(11), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(12), CultureInfo.InvariantCulture)));
            }
        }

        var storageLabels = new Dictionary<long, string>();
        foreach (var sid in raw.Where(r => r.AltStorage > 0).Select(r => r.AltStorage).Distinct())
        {
            var label = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COALESCE(NULLIF(TRIM(`short_name`), ''), `name`) FROM `shop_storages` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                sid).ConfigureAwait(false);
            storageLabels[sid] = string.IsNullOrWhiteSpace(label) ? "WH #" + sid.ToString(CultureInfo.InvariantCulture) : label.Trim();
        }

        return raw.Select(r =>
        {
            var (mfr, art, name) = ParseProduct(r.Json);
            return new CpQuoteLine(
                r.Id, mfr, art, name, r.Count, r.Price, r.Time, r.Note, r.Alt,
                r.AltMfr, r.AltArt, r.AltName, r.AltQty, r.AltPrice, r.AltStorage,
                r.AltStorage > 0 && storageLabels.TryGetValue(r.AltStorage, out var l) ? l : "");
        }).ToList();
    }

    private static (string Manufacturer, string Article, string Name) ParseProduct(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ("", "", "");
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ("", "", "");
            }

            var root = doc.RootElement;
            return (Str(root, "manufacturer"), Str(root, "article_show") is { Length: > 0 } s ? s : Str(root, "article"), Str(root, "name"));
        }
        catch (JsonException)
        {
            return ("", "", "");
        }
    }

    private static string Str(JsonElement root, string name)
        => root.TryGetProperty(name, out var p) && p.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? (p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : p.GetRawText())
            : "";

    private static async Task<CpQuoteCustomer> CustomerAsync(DbConnection connection, long userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return new CpQuoteCustomer(0, "", "", "", "", "Guest (not allowed)", "");
        }

        var email = "";
        var phone = "";
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional("SELECT IFNULL(`email`,''), IFNULL(`phone`,'') FROM `users` WHERE `user_id` = ? LIMIT 1");
            ErpDb.AddParameters(cmd, userId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                email = reader.GetString(0).Trim();
                phone = reader.GetString(1).Trim();
            }
        }

        var profile = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional("SELECT `data_key`, IFNULL(`data_value`,'') FROM `users_profiles` WHERE `user_id` = ?");
            ErpDb.AddParameters(cmd, userId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                profile[reader.GetString(0)] = reader.GetString(1).Trim();
            }
        }

        var name = string.Join(" ", new[] { "surname", "name", "patronymic" }
            .Select(k => profile.TryGetValue(k, out var v) ? v : "")
            .Where(v => v.Length > 0));
        if (profile.TryGetValue("email", out var pe) && pe.Length > 0)
        {
            email = pe;
        }

        if (profile.TryGetValue("phone", out var pp) && pp.Length > 0)
        {
            phone = pp;
        }
        else if (phone.Length == 0 && profile.TryGetValue("cellphone", out var pc))
        {
            phone = pc;
        }

        var company = profile.TryGetValue("company_name", out var co) ? co : "";
        var bits = new List<string>();
        if (name.Length > 0)
        {
            bits.Add(name);
        }

        if (company.Length > 0)
        {
            bits.Add(company);
        }

        if (email.Length > 0)
        {
            bits.Add(email);
        }
        else if (phone.Length > 0)
        {
            bits.Add(phone);
        }

        if (bits.Count == 0)
        {
            bits.Add("Customer #" + userId.ToString(CultureInfo.InvariantCulture));
        }

        return new CpQuoteCustomer(
            userId,
            name,
            email,
            phone,
            company,
            string.Join(" · ", bits),
            "/cp/users-app?user_id=" + userId.ToString(CultureInfo.InvariantCulture));
    }

    private static async Task<IReadOnlyList<CpQuoteAltWarehouse>> WarehousesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<CpQuoteAltWarehouse>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`short_name`,'') FROM `shop_storages` WHERE COALESCE(`hidden`,0) = 0 ORDER BY `short_name` ASC, `name` ASC, `id` ASC";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetInt64(0);
            var name = reader.GetString(1).Trim();
            var shortName = reader.GetString(2).Trim();
            var label = shortName.Length > 0 ? shortName : (name.Length > 0 ? name : "Warehouse #" + id.ToString(CultureInfo.InvariantCulture));
            if (shortName.Length > 0 && name.Length > 0 && !string.Equals(shortName, name, StringComparison.OrdinalIgnoreCase))
            {
                label = shortName + " — " + name;
            }

            list.Add(new CpQuoteAltWarehouse(id, label, 0, 0, null));
        }

        return list;
    }

    private static async Task<string> CurrencySignAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var iso = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `value` FROM `config_items` WHERE `name` = ? LIMIT 1"), cancellationToken, "shop_currency").ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(iso))
        {
            return "";
        }

        return (await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT IFNULL(`sign`,'') FROM `shop_currencies` WHERE `iso_code` = ? LIMIT 1"), cancellationToken, iso.Trim()).ConfigureAwait(false)) ?? "";
    }

    public static string NormalizeArticle(string? article)
        => Regex.Replace(article ?? "", "[^a-zA-Z0-9А-Яа-яёЁ]+", "").ToUpperInvariant();

    public static string PrepareBrand(string? brand)
        => (brand ?? "").Trim().ToUpperInvariant();

    public static string FormatTime(long unix)
        => unix > 0 ? DateTimeOffset.FromUnixTimeSeconds(unix).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "—";
}
