using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Cp;

/// <summary>PHP <c>prices_send.php</c> recipient filter (cookie <c>users_filter_send_prices</c>).</summary>
public sealed record CpPricesSendUserFilter(string UserId, int GroupId, string Email, string Cellphone, string Surname)
{
    public static readonly CpPricesSendUserFilter Empty = new(string.Empty, -1, string.Empty, string.Empty, string.Empty);

    public bool IsEmpty => UserId.Length == 0 && GroupId == -1 && Email.Length == 0 && Cellphone.Length == 0 && Surname.Length == 0;

    public static CpPricesSendUserFilter FromQuery(IQueryCollection query)
    {
        var groupRaw = query["group_id"].ToString().Trim();
        var group = int.TryParse(groupRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var g) ? g : -1;
        return new CpPricesSendUserFilter(
            query["user_id"].ToString().Trim(),
            group,
            query["email"].ToString().Trim(),
            query["cellphone"].ToString().Trim(),
            query["surname"].ToString().Trim());
    }

    public static CpPricesSendUserFilter FromCookieJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Empty;
            }

            string Str(string name) => doc.RootElement.TryGetProperty(name, out var el)
                ? el.ValueKind switch
                {
                    JsonValueKind.String => (el.GetString() ?? string.Empty).Trim(),
                    JsonValueKind.Number => el.GetRawText(),
                    _ => string.Empty,
                }
                : string.Empty;

            var group = int.TryParse(Str("group_id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var g) ? g : -1;
            return new CpPricesSendUserFilter(Str("user_id"), group, Str("email"), Str("cellphone"), Str("surname"));
        }
        catch (JsonException)
        {
            return Empty;
        }
    }
}

/// <summary>PHP <c>users_sort_send_prices</c>: field ∈ {user_id, email, fio}, direction asc/desc (default user_id desc).</summary>
public sealed record CpPricesSendUserSort(string Field, bool Ascending)
{
    public static readonly string[] Fields = ["user_id", "email", "fio"];

    public static readonly CpPricesSendUserSort Default = new("user_id", false);

    /// <summary>PHP cookie <c>users_sort_send_prices</c> = <c>{"field":"email","asc_desc":"asc"}</c>.</summary>
    public static CpPricesSendUserSort FromCookieJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Default;
            }

            string Str(string name) => doc.RootElement.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString() ?? string.Empty
                : string.Empty;

            return Normalize(Str("field"), Str("asc_desc"));
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    public static CpPricesSendUserSort Normalize(string? field, string? direction)
    {
        var f = (field ?? string.Empty).Trim().ToLowerInvariant();
        if (Array.IndexOf(Fields, f) < 0)
        {
            f = "user_id";
        }

        return new CpPricesSendUserSort(f, string.Equals((direction ?? string.Empty).Trim(), "asc", StringComparison.OrdinalIgnoreCase));
    }

    public string Direction => Ascending ? "asc" : "desc";

    /// <summary>PHP <c>sortUsers(field)</c>: same field toggles direction, new field starts asc.</summary>
    public CpPricesSendUserSort Toggle(string field)
    {
        var next = Normalize(field, "asc");
        return next.Field == Field ? new CpPricesSendUserSort(Field, !Ascending) : next;
    }
}

public sealed record CpPricesSendGroup(int Id, string Name);

public sealed record CpPricesSendUserRow(long UserId, string Email, bool EmailConfirmed, string Fio, IReadOnlyList<string> Groups);

public sealed record CpPricesSendOffice(int Id, string Caption);

public sealed record CpPricesSendStorage(int Id, string Name, int InterfaceType, string InterfaceTypeName, bool Linked);

public sealed record CpPricesSendCategoryNode(long Id, long Parent, string Value, IReadOnlyList<CpPricesSendCategoryNode> Children);

public sealed record CpPricesSendBrand(string Brand, int Count);

/// <summary>Full page state for <c>CpPricesSendApp.razor</c>.</summary>
public sealed record CpPricesSendDesk(
    bool DatabaseAvailable,
    string Notice,
    CpPricesSendUserFilter Filter,
    CpPricesSendUserSort Sort,
    IReadOnlyList<CpPricesSendGroup> Groups,
    IReadOnlyList<CpPricesSendUserRow> Users,
    IReadOnlyList<CpPricesSendOffice> Offices,
    IReadOnlyList<CpPricesSendStorage> Storages,
    IReadOnlyList<CpPricesSendStorage> CatalogueStorages,
    IReadOnlyList<CpPricesSendCategoryNode> CatalogueTree,
    IReadOnlyDictionary<string, string> Labels)
{
    public static CpPricesSendDesk Unavailable(string notice) => new(
        false, notice, CpPricesSendUserFilter.Empty, CpPricesSendUserSort.Default, [], [], [], [], [], [], CpPricesSendLabels.Defaults);

    public string Label(string key) => Labels.TryGetValue(key, out var v) && v.Length > 0 ? v : CpPricesSendLabels.Defaults[key];
}

/// <summary>PHP <c>translate_str_by_id(...)</c> keys used by prices_send.php with their English fallbacks.</summary>
public static class CpPricesSendLabels
{
    public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["3662"] = "Recipients",
        ["3664"] = "Group",
        ["1312"] = "Cellphone",
        ["3665"] = "Surname",
        ["2232"] = "Filter",
        ["2555"] = "Reset",
        ["2094"] = "All",
        ["3666"] = "Customers",
        ["3667"] = "Name",
        ["3668"] = "My e-mail list",
        ["3669"] = "Group for e-mail list",
        ["3670"] = "Shop",
        ["3671"] = "Sources",
        ["2277"] = "Name",
        ["3474"] = "Interface type",
        ["3672"] = "Catalogue groups",
        ["2293"] = "Check all",
        ["2294"] = "Uncheck all",
        ["2750"] = "Storage",
        ["2755"] = "Generate",
        ["3673"] = "Generate price lists",
        ["3674"] = "Send price lists",
        ["3675"] = "Select customers or enter e-mails",
        ["3676"] = "Price lists sent",
        ["3677"] = "Sending failed",
        ["3678"] = "Select at least one storage",
        ["3679"] = "Storages are not linked to the shop",
        ["3680"] = "Link them in Offices → Storages",
        ["3681"] = "Price lists generated",
        ["3682"] = "Generation failed",
        ["3546"] = "E-mail confirmed",
        ["3545"] = "E-mail not confirmed",
        ["3253"] = "not specified",
    };
}

/// <summary>
/// PHP <c>ajax_operations.php</c> <c>request_object</c> for the prices-send actions. Bound from the JSON blob the PHP JS
/// posts (<c>request_object=&lt;json&gt;</c>), from plain form fields, or from a JSON body.
/// </summary>
public sealed record CpPricesSendRequest(
    string Action,
    int OfficeId,
    IReadOnlyList<int> StorageIds,
    IReadOnlyList<long> CategoryIds,
    IReadOnlyList<long> UserIds,
    string EmailsList,
    int GroupIdMyListEmails,
    IReadOnlyList<int> ProfileGroupIds,
    IReadOnlyList<int> GroupIds,
    string FilterBrand,
    string FilterArticle,
    int CatalogueStorageId,
    string? PatternName,
    int Limit)
{
    public static readonly string[] Actions = ["list_brands", "ensure_office_storage_links", "check_office_storages_map", "create_prices", "send_prices"];

    /// <summary>PHP action aliases accepted by the ASP.NET dispatcher (canonical → PHP name).</summary>
    public static string Canonical(string? action)
    {
        var a = (action ?? string.Empty).Trim();
        if (a.StartsWith("prices_send_", StringComparison.Ordinal))
        {
            a = a.Substring("prices_send_".Length);
        }

        return a switch
        {
            "brands" or "list_brands" => "list_brands",
            "link" or "link_storages" or "ensure_office_storage_links" => "ensure_office_storage_links",
            "check" or "check_links" or "check_office_storages_map" => "check_office_storages_map",
            "generate" or "create" or "create_prices" => "create_prices",
            "send" or "send_prices" => "send_prices",
            _ => a,
        };
    }

    public bool IsKnown => Array.IndexOf(Actions, Action) >= 0;

    public IReadOnlyList<string> Emails => EmailsList
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(e => e.Length > 0)
        .ToArray();

    public static CpPricesSendRequest FromInput(CpCrmActionInput input)
    {
        var raw = input.Text("request_object");
        if (raw.Length > 0)
        {
            var parsed = FromRequestObjectJson(raw);
            if (parsed is not null)
            {
                return parsed with { Action = Canonical(parsed.Action.Length > 0 ? parsed.Action : input.Text("action")) };
            }
        }

        return new CpPricesSendRequest(
            Canonical(input.Text("action")),
            input.Int("offices", "office_id"),
            Ints(input.Text("arr_storages", "storage_ids")).Select(v => (int)v).ToArray(),
            Ints(input.Text("arr_category", "category_ids")),
            Ints(input.Text("users_list", "user_ids")),
            input.Text("emails_list"),
            input.Int("group_id_my_list_emails"),
            Ints(input.Text("profile_group_ids")).Select(v => (int)v).ToArray(),
            Ints(input.Text("group_ids")).Select(v => (int)v).ToArray(),
            input.Text("filter_brand"),
            input.Text("filter_article"),
            input.Int("storages"),
            input.TextOrNull("pattern_name"),
            input.Has("limit") ? input.Int("limit") : 30);
    }

    /// <summary>PHP: <c>json_decode($raw)</c>, falling back to <c>json_decode(urldecode($raw))</c>.</summary>
    public static CpPricesSendRequest? FromRequestObjectJson(string raw)
    {
        return Parse(raw) ?? Parse(Uri.UnescapeDataString(raw));

        static CpPricesSendRequest? Parse(string text)
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                return new CpPricesSendRequest(
                    Canonical(Str(root, "action")),
                    (int)Num(root, "offices"),
                    Arr(root, "arr_storages").Select(v => (int)v).ToArray(),
                    Arr(root, "arr_category"),
                    Arr(root, "users_list"),
                    Str(root, "emails_list"),
                    (int)Num(root, "group_id_my_list_emails"),
                    Arr(root, "profile_group_ids").Select(v => (int)v).ToArray(),
                    Arr(root, "group_ids").Select(v => (int)v).ToArray(),
                    Str(root, "filter_brand").Trim(),
                    Str(root, "filter_article").Trim(),
                    (int)Num(root, "storages"),
                    root.TryGetProperty("pattern_name", out var pn) && pn.ValueKind == JsonValueKind.String ? pn.GetString() : null,
                    root.TryGetProperty("limit", out _) ? (int)Num(root, "limit") : 30);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    private static string Str(JsonElement root, string name)
        => root.TryGetProperty(name, out var el)
            ? el.ValueKind switch
            {
                JsonValueKind.String => el.GetString() ?? string.Empty,
                JsonValueKind.Number => el.GetRawText(),
                _ => string.Empty,
            }
            : string.Empty;

    private static long Num(JsonElement root, string name)
    {
        var s = Str(root, name);
        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static IReadOnlyList<long> Arr(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
        {
            return [];
        }

        if (el.ValueKind == JsonValueKind.Array)
        {
            var list = new List<long>();
            foreach (var item in el.EnumerateArray())
            {
                var s = item.ValueKind == JsonValueKind.Number ? item.GetRawText() : item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : string.Empty;
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
                {
                    list.Add(v);
                }
            }

            return list;
        }

        return Ints(Str(root, name));
    }

    /// <summary>Comma/space separated integers, or a JSON array literal (form posts of arrays).</summary>
    public static IReadOnlyList<long> Ints(string raw)
    {
        raw = (raw ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return [];
        }

        return raw.Trim('[', ']')
            .Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => long.TryParse(s.Trim().Trim('"'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (long?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToArray();
    }
}

public sealed record CpPricesSendGeneratedFile(int GroupId, string File, int Rows, string Url);

/// <summary>Uniform JSON answer for the prices-send dispatcher (mirrors PHP <c>$answer</c>).</summary>
public sealed record CpPricesSendAnswer(
    bool Status,
    string Message,
    string Code,
    bool? CanLink = null,
    int? Linked = null,
    int? Sent = null,
    IReadOnlyList<CpPricesSendBrand>? Brands = null,
    IReadOnlyList<CpPricesSendGeneratedFile>? Files = null,
    int? RowsTotal = null)
{
    public static CpPricesSendAnswer Ok(string message = "", string code = "ok") => new(true, message, code);

    public static CpPricesSendAnswer Fail(string code, string message) => new(false, message, code);

    public Dictionary<string, object?> ToPayload()
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = Status,
            ["ok"] = Status,
            ["message"] = Message,
            ["validation_code"] = Code,
        };
        if (CanLink is not null) map["can_link"] = CanLink;
        if (Linked is not null) map["linked"] = Linked;
        if (Sent is not null) map["sent"] = Sent;
        if (Brands is not null) map["brands"] = Brands.Select(b => new Dictionary<string, object?>(StringComparer.Ordinal) { ["brand"] = b.Brand, ["count"] = b.Count }).ToArray();
        if (Files is not null) map["files"] = Files.Select(f => new Dictionary<string, object?>(StringComparer.Ordinal) { ["group_id"] = f.GroupId, ["file"] = f.File, ["rows"] = f.Rows, ["url"] = f.Url }).ToArray();
        if (RowsTotal is not null) map["rows_total"] = RowsTotal;
        return map;
    }
}

/// <summary>Pure row/price helpers mirroring <c>prices_send_helper.php</c> (unit-testable without a DB).</summary>
public static class CpPricesSendCsv
{
    public const string PublicDir = "/content/files/Documents/prices_tmp/";

    /// <summary>PHP default column order (1-based): manufacturer, article, name, exist, time_to_exe, price, min_order.</summary>
    public static readonly string[] DefaultColumns = ["manufacturer", "article", "name", "exist", "time_to_exe", "price", "min_order"];

    /// <summary>Header captions (PHP <c>translate_str_by_key</c> ids 2070/2071/2102/4324/2751/3433/3661, English defaults).</summary>
    public static readonly IReadOnlyDictionary<string, string> HeaderCaptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["manufacturer"] = "Manufacturer",
        ["article"] = "Article",
        ["name"] = "Name",
        ["exist"] = "Quantity",
        ["time_to_exe"] = "Delivery, days",
        ["price"] = "Price",
        ["min_order"] = "Min. order",
    };

    private static readonly Regex NonAlnum = new("[^a-zA-Z0-9А-Яа-яёЁ]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>PHP <c>mb_strtoupper(preg_replace('/[^a-zA-Z0-9А-Яа-яёЁ]+/u','',$s))</c>.</summary>
    public static string NormalizeArticle(string? value) => NonAlnum.Replace(value ?? string.Empty, string.Empty).ToUpperInvariant();

    /// <summary>PHP <c>str_replace(["&amp;","&frasl;","frasl;","&",";"], " ", $s)</c>.</summary>
    public static string StripEntities(string? value)
        => (value ?? string.Empty).Replace("&amp;", " ").Replace("&frasl;", " ").Replace("frasl;", " ").Replace("&", " ").Replace(";", " ");

    /// <summary>PHP <c>htmlentities(mb_strtoupper(trim($s)), ENT_QUOTES)</c> — after StripEntities only quotes/&lt;/&gt; remain.</summary>
    public static string ManufacturerCell(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant()
            .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#039;");

    public static string NameCell(string? value, bool docpart)
    {
        var s = (value ?? string.Empty).Replace(";", docpart ? ", " : " ");
        if (docpart)
        {
            s = s.Replace("\t", string.Empty);
        }

        return s.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
    }

    /// <summary>Currency conversion, group markup and PHP <c>price_rounding</c> (0 none, 1 ceil, 2 up to 5, 3 up to 10).</summary>
    public static decimal FinalPrice(decimal price, decimal currencyRate, decimal markupFraction, int roundingMode)
    {
        var work = price * currencyRate;
        work += work * markupFraction;
        switch (roundingMode)
        {
            case 1:
                work = decimal.Truncate(work) + (work > decimal.Truncate(work) ? 1 : 0);
                break;
            case 2:
            {
                var n = (long)decimal.Truncate(work);
                var last = Math.Abs(n % 10);
                if (last is > 0 and < 5) n += 5 - last;
                else if (last is > 5 and <= 9) n += 10 - last;
                work = n;
                break;
            }

            case 3:
            {
                var n = (long)decimal.Truncate(work);
                var last = Math.Abs(n % 10);
                if (last != 0) n += 10 - last;
                work = n;
                break;
            }
        }

        return work;
    }

    /// <summary>PHP <c>(float)number_format($p, 2, '.', '')</c> printed with PHP float semantics (no trailing zeros).</summary>
    public static string PriceCell(decimal price)
    {
        var rounded = Math.Round(price, 2, MidpointRounding.AwayFromZero);
        return ((double)rounded).ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static string HeaderLine(IReadOnlyList<string> columns)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < columns.Count; i++)
        {
            sb.Append(HeaderCaptions.TryGetValue(columns[i], out var c) ? c : columns[i]);
            if (i < columns.Count - 1)
            {
                sb.Append(';');
            }
        }

        sb.Append(";;;;");
        return sb.ToString();
    }

    public sealed record Row(
        string Manufacturer,
        string Article,
        string Name,
        string Exist,
        int TimeToExe,
        decimal Price,
        string MinOrder,
        string Url = "",
        string Img = "",
        string Content = "");

    /// <summary>One CSV line: the configured columns, then the four extra columns (blank key, article_MANUFACTURER, url, img, content).</summary>
    public static string RowLine(IReadOnlyList<string> columns, Row row, bool docpart)
    {
        var sb = new StringBuilder();
        var articleStr = string.Empty;
        var manufacturerStr = string.Empty;
        for (var i = 0; i < columns.Count; i++)
        {
            switch (columns[i])
            {
                case "price":
                    sb.Append(PriceCell(row.Price));
                    break;
                case "time_to_exe":
                    sb.Append(row.TimeToExe.ToString(CultureInfo.InvariantCulture));
                    break;
                case "name":
                    sb.Append(NameCell(row.Name, docpart));
                    break;
                case "article":
                    articleStr = NormalizeArticle(row.Article);
                    sb.Append(articleStr).Append('\t');
                    break;
                case "manufacturer":
                    manufacturerStr = ManufacturerCell(row.Manufacturer);
                    sb.Append(manufacturerStr);
                    break;
                case "min_order":
                    sb.Append(string.IsNullOrWhiteSpace(row.MinOrder) || row.MinOrder == "0" ? "1" : row.MinOrder);
                    break;
                case "exist":
                    sb.Append(row.Exist);
                    break;
            }

            if (i < columns.Count - 1)
            {
                sb.Append(';');
            }
        }

        sb.Append(';');
        sb.Append((articleStr + "_" + manufacturerStr).Replace(' ', '_')).Append(';');
        sb.Append(row.Url.Trim()).Append(';');
        sb.Append(row.Img.Trim()).Append(';');
        sb.Append(row.Content.Trim()).Append(';');
        return sb.ToString();
    }

    /// <summary>PHP: <c>prices_&lt;group&gt;.csv</c>, or <c>&lt;pattern_name&gt;.csv</c>; pattern restricted to a safe file stem.</summary>
    public static string FileName(int groupId, string? patternName)
    {
        var stem = (patternName ?? string.Empty).Trim();
        if (stem.Length > 0 && IsSafeStem(stem))
        {
            return stem + ".csv";
        }

        return "prices_" + groupId.ToString(CultureInfo.InvariantCulture) + ".csv";
    }

    public static bool IsSafeStem(string stem)
        => stem.Length is > 0 and <= 80 && stem.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.') && !stem.Contains("..", StringComparison.Ordinal) && stem[0] != '.';

    public static string PublicUrl(string fileName) => PublicDir + Uri.EscapeDataString(fileName);

    /// <summary>Docpart rows: time_to_exe 0 → additional days only, else time + additional.</summary>
    public static int DocpartDays(int timeToExe, int additionalDays) => timeToExe == 0 ? additionalDays : timeToExe + additionalDays;

    /// <summary>Catalogue rows: arrival in the future → days until arrival + additional, else time_to_exe + additional.</summary>
    public static int CatalogueDays(int timeToExe, long arrivalTimeUnix, long nowUnix, int additionalDays)
        => arrivalTimeUnix < nowUnix ? timeToExe + additionalDays : (int)((arrivalTimeUnix - nowUnix) / 86400) + additionalDays;

    /// <summary>PHP <c>(int)($additional_time/24)</c>.</summary>
    public static int AdditionalDays(long additionalHours) => (int)(additionalHours / 24);

    public static bool BrandMatches(string manufacturer, string filterBrand)
        => filterBrand.Length == 0 || manufacturer.Contains(filterBrand, StringComparison.OrdinalIgnoreCase);

    public static bool ArticleMatches(string article, string filterArticleNorm)
    {
        if (filterArticleNorm.Length == 0)
        {
            return true;
        }

        var norm = NormalizeArticle(article);
        return norm.Length > 0 && norm.Contains(filterArticleNorm, StringComparison.Ordinal);
    }

    public static string ProductUrl(string domainPath, string productUrlMode, string categoryUrl, long productId, string alias)
        => domainPath + categoryUrl + "/" + (productUrlMode == "id" ? productId.ToString(CultureInfo.InvariantCulture) : alias);

    public static string ImageUrl(string domainPath, string? img)
    {
        var s = (img ?? string.Empty).Trim();
        if (s.Length > 0 && !s.Contains('/'))
        {
            return domainPath + "content/files/images/products_images/" + s;
        }

        return s;
    }

    public static string ContentCell(string? content)
        => (content ?? string.Empty).Replace("\n", ", ").Replace("\r", ", ").Replace("\t", ", ").Replace(";", ", ");
}
