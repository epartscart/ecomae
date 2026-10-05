using System.Data.Common;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public static object SkuMediaUnknownAction()
        => new SkuUnknownBody(false, "Unknown action");

    public static object SkuMediaNoDatabase()
        => new SkuFailedBody(false, "No database", string.Empty, [], []);

    public static object SkuMediaLookupFailed()
        => new SkuFailedBody(false, "Lookup failed", string.Empty, [], []);

    public static object SkuMediaEmpty()
        => new SkuLookupBody(true, null, string.Empty, [], [], null);

    /// <summary>
    /// PHP <c>ajax_epc_sku_media_public.php</c> lookup JSON.
    /// Does not create <c>epc_sku_*</c> tables and does not call UMAPI.
    /// </summary>
    public static async Task<object> SkuMediaLookupAsync(
        DbConnection connection,
        string? brand,
        string? article,
        int productId,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = await FindSkuProfileAsync(connection, brand, article, productId, cancellationToken).ConfigureAwait(false);
            if (profile is null)
            {
                return SkuMediaEmpty();
            }

            var status = profile.Status;
            if (string.Equals(status, "hidden", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "draft", StringComparison.OrdinalIgnoreCase))
            {
                return SkuMediaEmpty();
            }

            var photos = await LoadSkuPhotosAsync(connection, profile.Id, cancellationToken).ConfigureAwait(false);
            var specs = await LoadSkuSpecsAsync(connection, profile.Id, cancellationToken).ConfigureAwait(false);
            var primary = string.Empty;
            foreach (var photo in photos)
            {
                if (primary.Length == 0 || photo.IsPrimary)
                {
                    primary = photo.Url;
                }
            }

            return new SkuLookupBody(
                true,
                null,
                primary,
                photos,
                specs,
                new SkuProfileJson(profile.Id, profile.Brand, profile.Article, profile.Title));
        }
        catch (Exception ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new SkuFailedBody(false, SkuMediaMissing, string.Empty, [], []);
        }
        catch (Exception)
        {
            return SkuMediaLookupFailed();
        }
    }

    private static async Task<SkuProfileRow?> FindSkuProfileAsync(
        DbConnection connection,
        string? brand,
        string? article,
        int productId,
        CancellationToken cancellationToken)
    {
        if (productId > 0)
        {
            var byProduct = await ReadSkuProfileAsync(
                connection,
                "SELECT `id`, IFNULL(`brand`,''), IFNULL(`article`,''), IFNULL(`title`,''), IFNULL(`status`,'active') FROM `epc_sku_profiles` WHERE `product_id` = ? ORDER BY `id` DESC LIMIT 1",
                cancellationToken,
                productId).ConfigureAwait(false);
            if (byProduct is not null)
            {
                return byProduct;
            }
        }

        var brandNorm = NormalizeSkuBrand(brand);
        var key = NormalizeSkuArticle(article);
        if (brandNorm.Length > 0 && key.Length > 0)
        {
            return await ReadSkuProfileAsync(
                connection,
                "SELECT `id`, IFNULL(`brand`,''), IFNULL(`article`,''), IFNULL(`title`,''), IFNULL(`status`,'active') FROM `epc_sku_profiles` WHERE UPPER(`brand`) = ? AND `article_key` = ? ORDER BY `id` DESC LIMIT 1",
                cancellationToken,
                brandNorm,
                key).ConfigureAwait(false);
        }

        if (key.Length > 0)
        {
            return await ReadSkuProfileAsync(
                connection,
                "SELECT `id`, IFNULL(`brand`,''), IFNULL(`article`,''), IFNULL(`title`,''), IFNULL(`status`,'active') FROM `epc_sku_profiles` WHERE `article_key` = ? ORDER BY `id` DESC LIMIT 1",
                cancellationToken,
                key).ConfigureAwait(false);
        }

        return null;
    }

    private static async Task<SkuProfileRow?> ReadSkuProfileAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params object[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new SkuProfileRow(
            reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
            reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            reader.IsDBNull(4) ? "active" : reader.GetString(4));
    }

    private static async Task<IReadOnlyList<SkuPhotoJson>> LoadSkuPhotosAsync(
        DbConnection connection,
        int profileId,
        CancellationToken cancellationToken)
    {
        var photos = new List<SkuPhotoJson>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT IFNULL(`file_name`,''), IFNULL(`alt`,''), IFNULL(`caption`,''), IFNULL(`photo_type`,'product'), IFNULL(`is_primary`,0) FROM `epc_sku_photos` WHERE `profile_id` = ? ORDER BY `is_primary` DESC, `sort_order` ASC, `id` ASC");
        ErpDb.AddParameters(command, profileId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var file = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var url = SkuPhotoUrl(file);
            if (url.Length == 0)
            {
                continue;
            }

            var primary = !reader.IsDBNull(4) && Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture) != 0;
            photos.Add(new SkuPhotoJson(
                url,
                reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? "product" : reader.GetString(3),
                primary));
        }

        return photos;
    }

    private static async Task<IReadOnlyList<SkuSpecGroupJson>> LoadSkuSpecsAsync(
        DbConnection connection,
        int profileId,
        CancellationToken cancellationToken)
    {
        var groups = new List<SkuSpecGroupJson>();
        var currentName = string.Empty;
        var currentIcon = "fa-list";
        var rows = new List<SkuSpecRowJson>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            """
            SELECT IFNULL(g.`name`,'Specifications'), IFNULL(g.`icon`,'fa-list'),
                   IFNULL(r.`label`,''), IFNULL(r.`value`,''), IFNULL(r.`value_type`,'text'), IFNULL(r.`unit`,'')
            FROM `epc_sku_spec_rows` r
            LEFT JOIN `epc_sku_spec_groups` g ON g.`id` = r.`group_id`
            WHERE r.`profile_id` = ?
            ORDER BY IFNULL(g.`sort_order`,0) ASC, g.`id` ASC, r.`sort_order` ASC, r.`id` ASC
            """);
        ErpDb.AddParameters(command, profileId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.IsDBNull(0) || reader.GetString(0).Length == 0 ? "Specifications" : reader.GetString(0);
            var icon = reader.IsDBNull(1) || reader.GetString(1).Length == 0 ? "fa-list" : reader.GetString(1);
            if (!string.Equals(name, currentName, StringComparison.Ordinal) || rows.Count == 0 && currentName.Length == 0)
            {
                if (rows.Count > 0)
                {
                    groups.Add(new SkuSpecGroupJson(currentName, currentIcon, rows));
                    rows = [];
                }

                currentName = name;
                currentIcon = icon;
            }

            var value = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var valueType = reader.IsDBNull(4) || reader.GetString(4).Length == 0 ? "text" : reader.GetString(4);
            var unit = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
            rows.Add(new SkuSpecRowJson(
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                FormatSkuValue(value, valueType, unit),
                valueType));
        }

        if (rows.Count > 0)
        {
            groups.Add(new SkuSpecGroupJson(currentName, currentIcon, rows));
        }

        return groups;
    }

    public static string SkuPhotoUrl(string fileName)
    {
        var normalized = fileName.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        var baseName = slash >= 0 ? normalized[(slash + 1)..] : normalized;
        if (baseName.Length == 0 || baseName is "." or "..")
        {
            return string.Empty;
        }

        return "/content/files/images/sku_media/" + Uri.EscapeDataString(baseName);
    }

    internal static string NormalizeSkuBrand(string? brand)
        => Regex.Replace((brand ?? string.Empty).Trim(), @"\s+", " ").ToUpperInvariant();

    internal static string NormalizeSkuArticle(string? article)
        => Regex.Replace(article ?? string.Empty, "[^A-Za-z0-9]", string.Empty).ToUpperInvariant();

    private static string FormatSkuValue(string value, string valueType, string unit)
    {
        var output = valueType switch
        {
            "bool" => value is "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                ? "Yes"
                : "No",
            "list" => string.Join(", ", value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)),
            _ => value
        };
        if (unit.Length > 0 && valueType is not ("bool" or "rich"))
        {
            output = (output + " " + unit).Trim();
        }

        return output;
    }

    private sealed record SkuProfileRow(int Id, string Brand, string Article, string Title, string Status);

    public sealed record SkuUnknownBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string Error);

    public sealed record SkuFailedBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("photos")] IReadOnlyList<SkuPhotoJson> Photos,
        [property: JsonPropertyName("specs")] IReadOnlyList<SkuSpecGroupJson> Specs);

    public sealed record SkuLookupBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Error,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("photos")] IReadOnlyList<SkuPhotoJson> Photos,
        [property: JsonPropertyName("specs")] IReadOnlyList<SkuSpecGroupJson> Specs,
        [property: JsonPropertyName("profile")] SkuProfileJson? Profile);

    public sealed record SkuPhotoJson(
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("alt")] string Alt,
        [property: JsonPropertyName("caption")] string Caption,
        [property: JsonPropertyName("photo_type")] string PhotoType,
        [property: JsonPropertyName("is_primary")] bool IsPrimary);

    public sealed record SkuSpecGroupJson(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("icon")] string Icon,
        [property: JsonPropertyName("rows")] IReadOnlyList<SkuSpecRowJson> Rows);

    public sealed record SkuSpecRowJson(
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("value")] string Value,
        [property: JsonPropertyName("value_type")] string ValueType);

    public sealed record SkuProfileJson(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("brand")] string Brand,
        [property: JsonPropertyName("article")] string Article,
        [property: JsonPropertyName("title")] string Title);
}
