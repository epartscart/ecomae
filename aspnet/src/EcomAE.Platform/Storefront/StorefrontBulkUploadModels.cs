using System.Text.Json.Serialization;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

/// <summary>One requested line from PHP <c>epc_bulk_read_input_lines</c>.</summary>
public sealed record StorefrontBulkUploadLine(
    [property: JsonPropertyName("brand")] string Brand,
    [property: JsonPropertyName("article")] string Article,
    [property: JsonPropertyName("qty")] int Qty,
    [property: JsonPropertyName("target_price")] string TargetPrice,
    [property: JsonPropertyName("delivery")] string Delivery,
    [property: JsonPropertyName("comment")] string Comment);

/// <summary>PHP <c>epc_bulk_marked_product</c> option (exact or cross).</summary>
public sealed record StorefrontBulkUploadOffer(
    [property: JsonPropertyName("manufacturer")] string Manufacturer,
    [property: JsonPropertyName("article")] string Article,
    [property: JsonPropertyName("article_show")] string ArticleShow,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("exist")] int Exist,
    [property: JsonPropertyName("price")] decimal Price,
    [property: JsonPropertyName("time_to_exe")] int TimeToExe,
    [property: JsonPropertyName("match_type")] string MatchType,
    [property: JsonPropertyName("match_label")] string MatchLabel,
    [property: JsonPropertyName("product_object")] IReadOnlyDictionary<string, object?> ProductObject,
    [property: JsonPropertyName("selected")] bool Selected = false);

/// <summary>PHP ajax_process result row.</summary>
public sealed record StorefrontBulkUploadRow(
    [property: JsonPropertyName("input")] StorefrontBulkUploadLine Input,
    [property: JsonPropertyName("exact")] StorefrontBulkUploadOffer? Exact,
    [property: JsonPropertyName("cross")] StorefrontBulkUploadOffer? Cross,
    [property: JsonPropertyName("available")] bool Available,
    [property: JsonPropertyName("cross_found")] bool CrossFound,
    [property: JsonPropertyName("short_qty")] bool ShortQty,
    [property: JsonPropertyName("status_label")] string StatusLabel,
    [property: JsonPropertyName("cross_checked")] bool CrossChecked);

public sealed record StorefrontBulkUploadSummary(
    [property: JsonPropertyName("uploaded")] int Uploaded,
    [property: JsonPropertyName("available")] int Available,
    [property: JsonPropertyName("cross")] int Cross,
    [property: JsonPropertyName("short")] int Short,
    [property: JsonPropertyName("notfound")] int Notfound);

public sealed record StorefrontBulkUploadCheckResult(
    bool Status,
    string? Message,
    IReadOnlyList<StorefrontBulkUploadRow> Rows,
    StorefrontBulkUploadSummary Summary,
    string Csv,
    string Source,
    int UploadId = 0);

public sealed record StorefrontBulkUploadCrossResult(
    bool Status,
    string? Message,
    StorefrontBulkUploadOffer? Exact,
    StorefrontBulkUploadOffer? Cross);

public sealed record StorefrontBulkUploadAddSelectedResult(
    bool Status,
    string? Message,
    int Added,
    int Failed,
    IReadOnlyList<string> Errors);

/// <summary>Shared ranking used by process + cross (PHP <c>epc_bulk_find_options</c> sort).</summary>
public static class StorefrontBulkUploadMatcher
{
    public const int MaxRows = 2000;

    public static StorefrontPartOfferDigest? PickBest(
        IReadOnlyList<StorefrontPartOfferDigest> rows,
        string priority)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        var inStock = rows.Where(r => r.Exist > 0).ToList();
        var pool = inStock.Count > 0 ? inStock : rows.ToList();
        var deliveryFirst = string.Equals(priority, "delivery", StringComparison.OrdinalIgnoreCase);
        return pool
            .OrderBy(r => deliveryFirst ? ParseDays(r.TimeToExe) : 0)
            .ThenBy(r => r.Price)
            .ThenBy(r => ParseDays(r.TimeToExe))
            .ThenBy(r => r.Manufacturer, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    public static bool SameOffer(StorefrontPartOfferDigest a, StorefrontPartOfferDigest? b)
    {
        if (b is null)
        {
            return false;
        }

        return string.Equals(a.Manufacturer, b.Manufacturer, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Article, b.Article, StringComparison.OrdinalIgnoreCase)
            && a.OfficeId == b.OfficeId
            && a.StorageId == b.StorageId
            && a.Price == b.Price;
    }

    public static int ParseDays(string? timeToExe)
    {
        if (string.IsNullOrWhiteSpace(timeToExe))
        {
            return 0;
        }

        var digits = new string(timeToExe.TakeWhile(ch => char.IsDigit(ch) || ch == '-').ToArray());
        return int.TryParse(digits, out var days) ? Math.Max(0, days) : 0;
    }

    public static StorefrontBulkUploadOffer ToOffer(
        StorefrontPartOfferDigest row,
        int qty,
        string matchType,
        string matchLabel,
        bool selected)
    {
        var countNeed = Math.Max(1, qty);
        if (row.Exist > 0)
        {
            countNeed = Math.Min(countNeed, row.Exist);
        }

        var minOrder = row.MinOrder > 0 ? row.MinOrder : 1;
        if (countNeed < minOrder)
        {
            countNeed = minOrder;
        }

        var days = ParseDays(row.TimeToExe);
        var guaranteed = ParseDays(string.IsNullOrWhiteSpace(row.TimeToExeGuaranteed) ? row.TimeToExe : row.TimeToExeGuaranteed);
        var product = new Dictionary<string, object?>
        {
            ["product_type"] = row.ProductType == 0 ? 2 : row.ProductType,
            ["manufacturer"] = row.Manufacturer,
            ["article"] = row.Article,
            ["article_show"] = string.IsNullOrWhiteSpace(row.ArticleShow) ? row.Article : row.ArticleShow,
            ["name"] = row.Name,
            ["exist"] = row.Exist,
            ["price"] = row.Price,
            ["time_to_exe"] = days,
            ["time_to_exe_guaranteed"] = guaranteed,
            ["storage"] = row.Storage,
            ["min_order"] = minOrder,
            ["probability"] = row.Probability <= 0 ? 100 : row.Probability,
            ["office_id"] = row.OfficeId,
            ["storage_id"] = row.StorageId,
            ["price_purchase"] = row.PricePurchase,
            ["markup"] = row.Markup,
            ["json_params"] = row.JsonParams ?? "",
            ["count_need"] = countNeed,
            ["check_hash"] = row.CheckHash ?? ""
        };

        return new StorefrontBulkUploadOffer(
            row.Manufacturer,
            row.Article,
            string.IsNullOrWhiteSpace(row.ArticleShow) ? row.Article : row.ArticleShow,
            row.Name,
            row.Exist,
            row.Price,
            days,
            matchType,
            matchLabel,
            product,
            selected);
    }

    public static StorefrontBulkUploadRow BuildRow(
        StorefrontBulkUploadLine item,
        StorefrontBulkUploadOffer? exact,
        StorefrontBulkUploadOffer? cross,
        bool crossChecked)
    {
        var selected = exact ?? cross;
        var available = exact is not null || cross is not null;
        var shortQty = selected is not null && selected.Exist < item.Qty;
        var status = available
            ? (shortQty ? "Available but short quantity" : "Available")
            : (crossChecked ? "No cross availability found" : "Not found - click to check cross reference");
        return new StorefrontBulkUploadRow(
            item,
            exact,
            cross,
            available,
            cross is not null,
            shortQty,
            status,
            crossChecked);
    }

    public static (StorefrontBulkUploadSummary Summary, string Csv) Summarize(
        IReadOnlyList<StorefrontBulkUploadRow> rows)
    {
        var uploaded = rows.Count;
        var available = rows.Count(r => r.Available);
        var cross = rows.Count(r => r.CrossFound);
        var shortQty = rows.Count(r => r.ShortQty);
        var notfound = uploaded - available;
        var csv = BuildCsv(rows);
        return (new StorefrontBulkUploadSummary(uploaded, available, cross, shortQty, notfound), csv);
    }

    public static string BuildCsv(IReadOnlyList<StorefrontBulkUploadRow> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("Brand,Requested Article,Qty,Exact Brand,Exact Article,Exact Price,Exact Qty,Cross Brand,Cross Article,Cross Price,Cross Qty,Status\n");
        foreach (var row in rows)
        {
            sb.Append(Csv(row.Input.Brand)).Append(',')
                .Append(Csv(row.Input.Article)).Append(',')
                .Append(row.Input.Qty).Append(',')
                .Append(Csv(row.Exact?.Manufacturer)).Append(',')
                .Append(Csv(row.Exact?.ArticleShow)).Append(',')
                .Append(row.Exact is null ? "" : row.Exact.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(row.Exact is null ? "" : row.Exact.Exist.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(row.Cross?.Manufacturer)).Append(',')
                .Append(Csv(row.Cross?.ArticleShow)).Append(',')
                .Append(row.Cross is null ? "" : row.Cross.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(row.Cross is null ? "" : row.Cross.Exist.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(row.StatusLabel)).Append('\n');
        }

        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        var text = value ?? "";
        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
