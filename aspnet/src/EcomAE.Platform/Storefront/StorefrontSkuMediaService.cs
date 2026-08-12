using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Api.Catalog;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>ajax_epc_sku_media_public.php?action=lookup</c> twin for CHPU Spec + Photos.
/// Prefer CP <c>epc_sku_*</c> tables; fall back to offline UMAPI article media/criteria.
/// </summary>
public interface IStorefrontSkuMediaService
{
    Task<StorefrontSkuMediaLookupResult> LookupAsync(
        string brand,
        string article,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontSkuMediaPhoto(
    string Url,
    string Alt,
    string Caption,
    string PhotoType,
    bool IsPrimary);

public sealed record StorefrontSkuMediaSpecRow(string Label, string Value, string ValueType);

public sealed record StorefrontSkuMediaSpecGroup(
    string Name,
    string Icon,
    IReadOnlyList<StorefrontSkuMediaSpecRow> Rows);

public sealed record StorefrontSkuMediaProfile(
    int Id,
    string Brand,
    string Article,
    string Title);

public sealed record StorefrontSkuMediaLookupResult(
    bool Ok,
    string Url,
    IReadOnlyList<StorefrontSkuMediaPhoto> Photos,
    IReadOnlyList<StorefrontSkuMediaSpecGroup> Specs,
    StorefrontSkuMediaProfile? Profile,
    string Source,
    string Message);

public sealed class StorefrontSkuMediaService : IStorefrontSkuMediaService
{
    private readonly ITenantDbConnectionFactory _connections;
    private readonly ICatalogOfflineCacheService _offlineCache;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StorefrontSkuMediaService(
        ITenantDbConnectionFactory connections,
        ICatalogOfflineCacheService offlineCache,
        IHttpContextAccessor httpContextAccessor)
    {
        _connections = connections;
        _offlineCache = offlineCache;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<StorefrontSkuMediaLookupResult> LookupAsync(
        string brand,
        string article,
        CancellationToken cancellationToken = default)
    {
        var brandNorm = NormalizeBrand(brand);
        var articleKey = NormalizeArticleKey(article);
        var articleShow = (article ?? string.Empty).Trim();
        if (articleKey.Length == 0)
        {
            return Empty("rejected", "Article is required.");
        }

        if (_connections.IsConfigured)
        {
            try
            {
                await using var connection = await OpenShopAsync(cancellationToken).ConfigureAwait(false);
                var profileId = await ResolveProfileIdAsync(connection, brandNorm, articleKey, cancellationToken)
                    .ConfigureAwait(false);
                if (profileId > 0)
                {
                    var photos = await LoadPhotosAsync(connection, profileId, cancellationToken).ConfigureAwait(false);
                    var specs = await LoadSpecsAsync(connection, profileId, cancellationToken).ConfigureAwait(false);
                    if (photos.Count > 0 || specs.Count > 0)
                    {
                        var primary = photos.FirstOrDefault(p => p.IsPrimary)?.Url
                            ?? photos.FirstOrDefault()?.Url
                            ?? string.Empty;
                        return new StorefrontSkuMediaLookupResult(
                            true,
                            primary,
                            photos,
                            specs,
                            new StorefrontSkuMediaProfile(profileId, brandNorm, articleShow, string.Empty),
                            "database",
                            string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                // Fall through to UMAPI offline cache.
                _ = ex;
            }
        }

        var umapi = await TryUmapiArticleAsync(brandNorm, articleShow, articleKey, cancellationToken)
            .ConfigureAwait(false);
        if (umapi is not null)
        {
            return umapi;
        }

        return Empty("empty", "No SKU media or catalog article details for this brand/article.");
    }

    private async Task<StorefrontSkuMediaLookupResult?> TryUmapiArticleAsync(
        string brand,
        string articleShow,
        string articleKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var analogs = await _offlineCache
                .LookupAnalogsAsync("passenger", articleShow, brand, "en", "WWW", cancellationToken)
                .ConfigureAwait(false);
            if (!analogs.Ok || analogs.Payload is null)
            {
                return null;
            }

            var artId = StorefrontFitmentService.ExtractArticleId(analogs.Payload, articleShow, brand);
            if (artId is null or <= 0)
            {
                return null;
            }

            var article = await _offlineCache
                .LookupArticleAsync("passenger", artId.Value, "en", "WWW", cancellationToken)
                .ConfigureAwait(false);
            if (!article.Ok || article.Payload is null || article.Payload is not JsonElement root)
            {
                return null;
            }

            var detail = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data)
                ? data
                : root;
            if (detail.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var photos = new List<StorefrontSkuMediaPhoto>();
            var img = ArticleImageUrl(detail);
            if (!string.IsNullOrWhiteSpace(img))
            {
                photos.Add(new StorefrontSkuMediaPhoto(img, brand + " " + articleShow, string.Empty, "product", true));
            }

            var specs = ExtractCriteriaGroups(detail);
            if (photos.Count == 0 && specs.Count == 0)
            {
                return null;
            }

            return new StorefrontSkuMediaLookupResult(
                true,
                photos.FirstOrDefault()?.Url ?? string.Empty,
                photos,
                specs,
                new StorefrontSkuMediaProfile(artId.Value, brand, articleShow, ReadString(detail, "COMPLETE_DES", "DES", "ART_PRODUCT_NAME")),
                "umapi-cache",
                string.Empty);
        }
        catch
        {
            return null;
        }
    }

    private Task<DbConnection> OpenShopAsync(CancellationToken cancellationToken)
    {
        var host = _httpContextAccessor.HttpContext?.Request.Host.Host ?? string.Empty;
        var tenant = _httpContextAccessor.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        if (RouteTenantResolver.IsEpartsCartHost(host, tenant?.SiteKey))
        {
            return _connections.OpenAsync("docpart", cancellationToken);
        }

        return _connections.OpenAsync(null, cancellationToken);
    }

    private static async Task<int> ResolveProfileIdAsync(
        DbConnection connection,
        string brand,
        string articleKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontSkuProfileByBrandArticle;
        AddParam(command, "@brand", brand);
        AddParam(command, "@articleKey", articleKey);
        var scalar = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (scalar is null or DBNull)
        {
            return 0;
        }

        return Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<StorefrontSkuMediaPhoto>> LoadPhotosAsync(
        DbConnection connection,
        int profileId,
        CancellationToken cancellationToken)
    {
        var photos = new List<StorefrontSkuMediaPhoto>();
        await using var command = connection.CreateCommand();
        command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontSkuPhotos;
        AddParam(command, "@profileId", profileId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var file = Convert.ToString(reader["file_name"] is DBNull ? string.Empty : reader["file_name"], CultureInfo.InvariantCulture) ?? string.Empty;
            if (file.Length == 0)
            {
                continue;
            }

            var url = file.StartsWith("/content/", StringComparison.OrdinalIgnoreCase)
                ? file
                : "/content/files/images/sku_media/" + file.TrimStart('/');
            photos.Add(new StorefrontSkuMediaPhoto(
                url,
                Convert.ToString(reader["alt"] is DBNull ? string.Empty : reader["alt"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["caption"] is DBNull ? string.Empty : reader["caption"], CultureInfo.InvariantCulture) ?? string.Empty,
                "product",
                Convert.ToInt32(reader["is_primary"] is DBNull ? 0 : reader["is_primary"], CultureInfo.InvariantCulture) != 0));
        }

        return photos;
    }

    private static async Task<IReadOnlyList<StorefrontSkuMediaSpecGroup>> LoadSpecsAsync(
        DbConnection connection,
        int profileId,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, List<StorefrontSkuMediaSpecRow>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = LegacySurfaceDashboardSql.SelectStorefrontSkuSpecs;
        AddParam(command, "@profileId", profileId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var group = Convert.ToString(reader["group_name"] is DBNull ? "Specifications" : reader["group_name"], CultureInfo.InvariantCulture)
                ?? "Specifications";
            var label = Convert.ToString(reader["label"] is DBNull ? string.Empty : reader["label"], CultureInfo.InvariantCulture) ?? string.Empty;
            var value = Convert.ToString(reader["value"] is DBNull ? string.Empty : reader["value"], CultureInfo.InvariantCulture) ?? string.Empty;
            var unit = Convert.ToString(reader["unit"] is DBNull ? string.Empty : reader["unit"], CultureInfo.InvariantCulture) ?? string.Empty;
            var valueType = Convert.ToString(reader["value_type"] is DBNull ? "text" : reader["value_type"], CultureInfo.InvariantCulture) ?? "text";
            if (label.Length == 0 && value.Length == 0)
            {
                continue;
            }

            if (!map.TryGetValue(group, out var rows))
            {
                rows = [];
                map[group] = rows;
                order.Add(group);
            }

            var display = string.IsNullOrWhiteSpace(unit) ? value : (value + " " + unit).Trim();
            rows.Add(new StorefrontSkuMediaSpecRow(label, display, valueType));
        }

        return order.Select(g => new StorefrontSkuMediaSpecGroup(g, "fa-list", map[g])).ToList();
    }

    private static string ArticleImageUrl(JsonElement detail)
    {
        var media = ReadString(detail, "MEDIA_FILE");
        var sup = ReadString(detail, "SUP_ID");
        if (media.Length == 0 || sup.Length == 0)
        {
            return string.Empty;
        }

        return "https://image.umapi.ru/IMAGE/"
            + Uri.EscapeDataString(sup) + "/"
            + Uri.EscapeDataString(media);
    }

    private static IReadOnlyList<StorefrontSkuMediaSpecGroup> ExtractCriteriaGroups(JsonElement detail)
    {
        var rows = new List<StorefrontSkuMediaSpecRow>();
        if (detail.TryGetProperty("CRITERIA", out var criteria) && criteria.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in criteria.EnumerateArray().Take(40))
            {
                var label = ReadString(item, "CRI_SHORT_DES", "CRI_DES");
                var value = ReadString(item, "VALUE", "DES");
                var unit = ReadString(item, "CRI_UNIT_DES");
                if (label.Length == 0 && value.Length == 0)
                {
                    continue;
                }

                rows.Add(new StorefrontSkuMediaSpecRow(label, string.IsNullOrWhiteSpace(unit) ? value : (value + " " + unit).Trim(), "text"));
            }
        }

        if (rows.Count == 0)
        {
            return [];
        }

        return [new StorefrontSkuMediaSpecGroup("Specifications & details", "fa-list-alt", rows)];
    }

    private static string ReadString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var p))
            {
                if (p.ValueKind == JsonValueKind.String)
                {
                    return p.GetString()?.Trim() ?? string.Empty;
                }

                if (p.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return p.ToString();
                }
            }
        }

        return string.Empty;
    }

    private static StorefrontSkuMediaLookupResult Empty(string source, string message) =>
        new(true, string.Empty, [], [], null, source, message);

    private static string NormalizeBrand(string? brand) =>
        Regex.Replace((brand ?? string.Empty).Trim().ToUpperInvariant(), @"\s+", " ");

    private static string NormalizeArticleKey(string? article) =>
        Regex.Replace(article ?? string.Empty, "[^A-Za-z0-9]", string.Empty).ToUpperInvariant();

    private static void AddParam(DbCommand command, string name, object value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        command.Parameters.Add(p);
    }
}
