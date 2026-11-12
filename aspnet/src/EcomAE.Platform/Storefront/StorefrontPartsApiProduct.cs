using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Api.Catalog;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// <c>/api/partsapi_product.php?brand=&amp;number=</c> — legacy entry point that
/// redirects to the canonical CHPU product page <c>/en/parts/{BRAND}/{ARTICLE}</c>,
/// which owns the warehouse offers table and add-to-cart actions.
/// </summary>
public static class StorefrontPartsApiProduct
{
    public const string Path = "/api/partsapi_product.php";

    /// <summary>ASP.NET-native fragment endpoint feeding the product hero on /en/parts/{BRAND}/{ARTICLE}.</summary>
    public const string FragmentPath = "/api/article_product.php";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, HandleAsync).AllowAnonymous();
        endpoints.MapGet(FragmentPath, HandleAsyncLegacy).AllowAnonymous();
        endpoints.MapGet("/api/article_product", HandleAsyncLegacy).AllowAnonymous();
        // Fitment is click-gated: the "Suitable vehicles" tab calls this once
        // per article, so product page views never burn provider quota on it.
        endpoints.MapGet("/api/article_fitment.php", HandleFitmentAsync).AllowAnonymous();
        endpoints.MapGet("/api/article_fitment", HandleFitmentAsync).AllowAnonymous();
    }

    private static IResult HandleAsync(HttpContext context)
    {
        var brand = context.Request.Query["brand"].ToString().Trim();
        var number = context.Request.Query["number"].ToString().Trim();
        if (brand.Length == 0 || number.Length == 0)
        {
            return Results.Text("brand and number are required", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status400BadRequest);
        }

        var target = "/en/parts/" + Uri.EscapeDataString(brand.ToUpperInvariant()) + "/" + Uri.EscapeDataString(number);
        return Results.Redirect(target, permanent: true);
    }

    private static async Task<IResult> HandleAsyncLegacy(
        HttpContext context,
        IPartsApiService partsApi,
        IHttpClientFactory httpClientFactory,
        ITenantDbConnectionFactory connections,
        IStorefrontPriceAccess priceAccess,
        ICatalogOfflineCacheService offlineCache,
        CancellationToken cancellationToken)
    {
        var brand = context.Request.Query["brand"].ToString().Trim();
        var number = context.Request.Query["number"].ToString().Trim();
        var lang = context.Request.Query["lang"].ToString().Trim() is { Length: > 0 } l ? l : "en";

        // Resolve the article id (needed for media/criteria/crosses).
        var artId = "";
        string? supId = null;
        var artName = "";
        var artCategory = "";
        var search = await partsApi.ExecuteAsync(new PartsApiRequest("searchArticles",
            new Dictionary<string, string> { ["SEARCH_NUMBER"] = number, ["LANG"] = lang }), cancellationToken).ConfigureAwait(false);
        var wanted = NormalizeBrand(brand);
        foreach (var row in Rows(search))
        {
            var b = NormalizeBrand(Text(row, "ART_SUP_BRAND", "SUP_BRAND", "brand", "BRAND") ?? "");
            if (artId.Length == 0 || b == wanted)
            {
                artId = Text(row, "ART_ID", "artId") ?? artId;
                supId = Text(row, "ART_SUP_ID", "SUP_ID", "supId") ?? supId;
                artName = Text(row, "ART_PRODUCT_NAME", "ART_NAME", "artName", "name") ?? artName;
                if (b == wanted)
                {
                    break;
                }
            }
        }

        var calls = new List<(string Key, Task<PartsApiResult> Task)>();
        if (artId.Length > 0)
        {
            calls.Add(("media", partsApi.ExecuteAsync(new PartsApiRequest("getArticleMedia",
                new Dictionary<string, string> { ["ART_ID"] = artId, ["LANG"] = lang }), cancellationToken)));
            calls.Add(("criteria", partsApi.ExecuteAsync(new PartsApiRequest("getArticleCriteria",
                new Dictionary<string, string> { ["ART_ID"] = artId, ["LANG"] = lang }), cancellationToken)));
            calls.Add(("crosses", partsApi.ExecuteAsync(new PartsApiRequest("getArticleCrosses",
                new Dictionary<string, string> { ["ART_ID"] = artId, ["LANG"] = lang }), cancellationToken)));
        }

        if (supId is not null && artName.Length == 0)
        {
            calls.Add(("article", partsApi.ExecuteAsync(new PartsApiRequest("getArticle",
                new Dictionary<string, string> { ["LANG"] = lang, ["ART_NUM"] = number, ["SUP_ID"] = supId }), cancellationToken)));
        }


        var results = new Dictionary<string, PartsApiResult>();
        foreach (var (key, task) in calls)
        {
            try { results[key] = await task.ConfigureAwait(false); }
            catch (Exception) { results[key] = new PartsApiResult(false, 502, ""); }
        }

        // UMAPI is the paid primary: its /Article/{id} payload carries the part
        // name, webp photo and the full criteria table in one call — prefer it
        // and let partsapi fill only what umapi lacks. Fetch-once: the saved
        // epc_umapi_cache row serves every view after the first upstream hit.
        JsonElement umapiArticle = default;
        if (int.TryParse(artId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var artIdInt))
        {
            var region = context.Request.Query["region"].ToString().Trim();
            if (region.Length == 0)
            {
                region = "WWW";
            }

            var langName = lang == "16" ? "en" : lang;
            JsonElement? cachedPayload = null;
            try
            {
                var cachedArticle = await offlineCache.LookupArticleAsync("passenger", artIdInt, langName, region, cancellationToken).ConfigureAwait(false);
                if (cachedArticle.Ok && cachedArticle.Payload is JsonElement cached && cached.ValueKind == JsonValueKind.Object)
                {
                    cachedPayload = cached;
                }
            }
            catch (Exception)
            {
                // Missing cache table or DB error — fall through to the live fetch.
            }

            // The id-matched cache query accepts LIKE prefixes: reject a hit
            // whose payload carries a different article id (e.g. 1234 for 123).
            if (cachedPayload.HasValue && !PayloadArticleIdMatches(cachedPayload.Value, artIdInt))
            {
                cachedPayload = null;
            }

            if (cachedPayload.HasValue)
            {
                umapiArticle = cachedPayload.Value.Clone();
            }
            else
            {
                try
                {
                    var http = httpClientFactory.CreateClient();
                    http.Timeout = TimeSpan.FromSeconds(12);
                    using var request = new HttpRequestMessage(HttpMethod.Get,
                        "https://api.umapi.ru/v2/autocatalog/" + langName + "-" + region.ToUpperInvariant() + "/Article/" + Uri.EscapeDataString(artId));
                    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.TryAddWithoutValidation("X-App-Key", await HomeCatalogWidgets.UmapiAppKeyAsync(context, null, cancellationToken).ConfigureAwait(false));
                    using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode && body.StartsWith('{'))
                    {
                        using var doc = JsonDocument.Parse(body);
                        umapiArticle = doc.RootElement.Clone();
                        await WriteUmapiCacheAsync(connections, context,
                            "article:" + langName + ":" + region + ":" + artIdInt,
                            "article", "passenger", langName, region, body,
                            (int)response.StatusCode, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        if (umapiArticle.ValueKind == JsonValueKind.Object)
        {
            var umapiName = Text(umapiArticle, "COMPLETE_DES", "DES", "ART_PRODUCT_NAME") ?? "";
            if (umapiName.Length > 0)
            {
                artName = umapiName;
            }

            var umapiCategory = Text(umapiArticle, "CATEGORY_NAME", "CATEGORY", "PRODUCT_GROUP", "GA_DES", "GROUP_NAME", "ART_PRODUCT_GROUP") ?? "";
            if (umapiCategory.Length > 0)
            {
                artCategory = umapiCategory;
            }
        }

        // Name fallback when partsapi is throttled: BrandRefinement returns TITLE
        // per brand. Fetch-once too — the payload is persisted so repeat views
        // spend zero UMAPI calls.
        if (artName.Length == 0)
        {
            try
            {
                var region2 = context.Request.Query["region"].ToString().Trim();
                if (region2.Length == 0)
                {
                    region2 = "WWW";
                }

                var langName2 = lang == "16" ? "en" : lang;
                var refinementKey = "brandrefinement:" + langName2 + ":" + region2 + ":" + number;
                var body = await ReadUmapiCacheAsync(connections, context, refinementKey, cancellationToken).ConfigureAwait(false);
                if (body is null)
                {
                    var http = httpClientFactory.CreateClient();
                    http.Timeout = TimeSpan.FromSeconds(12);
                    using var request = new HttpRequestMessage(HttpMethod.Get,
                        "https://api.umapi.ru/v2/autocatalog/" + langName2 + "-" + region2.ToUpperInvariant() + "/BrandRefinement/" + Uri.EscapeDataString(number));
                    request.Headers.TryAddWithoutValidation("X-App-Key", await HomeCatalogWidgets.UmapiAppKeyAsync(context, null, cancellationToken).ConfigureAwait(false));
                    using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        await WriteUmapiCacheAsync(connections, context, refinementKey,
                            "brands", "passenger", langName2, region2, body,
                            (int)response.StatusCode, cancellationToken).ConfigureAwait(false);
                    }
                }

                if (body is not null)
                {
                    using var doc = JsonDocument.Parse(body);
                    var rows = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement
                        : doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array ? d
                        : doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("rows", out var r) && r.ValueKind == JsonValueKind.Array ? r
                        : (JsonElement?)null;
                    if (rows.HasValue)
                    {
                        foreach (var row in rows.Value.EnumerateArray())
                        {
                            var b = NormalizeBrand(Text(row, "BRAND", "SUP_BRAND") ?? "");
                            var title = Text(row, "TITLE", "DES", "DISPLAY") ?? "";
                            if (title.Length > 0 && (artName.Length == 0 || b == wanted))
                            {
                                artName = title;
                                if (b == wanted)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // Assemble the card.
        var images = new List<string>();
        if (umapiArticle.ValueKind == JsonValueKind.Object)
        {
            if (umapiArticle.TryGetProperty("MEDIAS", out var medias) && medias.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in medias.EnumerateArray())
                {
                    var file = Text(m, "FILE_NAME", "MEDIA_FILE", "file") ?? "";
                    if (file.Length > 0)
                    {
                        images.Add("https://image.umapi.ru/ARTICLES/" + file);
                    }
                }
            }

            if (images.Count == 0)
            {
                var file = Text(umapiArticle, "MEDIA_FILE") ?? "";
                if (file.Length > 0)
                {
                    images.Add("https://image.umapi.ru/ARTICLES/" + file);
                }
            }
        }

        foreach (var row in Rows(results.GetValueOrDefault("media")))
        {
            var u = Text(row, "url", "URL", "IMAGE", "image", "path", "FILE_NAME") ?? "";
            if (u.Length > 0 && !u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                u = "https://partsapi.ru" + u;
            }

            if (u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                images.Add(u);
            }
        }

        images = images.Where(i => i.Length > 0).Distinct().ToList();

        var specs = new List<(string Name, string Value)>();
        foreach (var row in Rows(results.GetValueOrDefault("criteria")))
        {
            var n = Text(row, "CRITERIA_NAME", "CRITERIA", "criteria", "name", "CRI_DES", "CRI_SHORT_DES", "CRITERIA_DESCRIPTION", "title") ?? "";
            var v = Text(row, "CRITERIA_VALUE", "criteriaValue", "value", "VALUE", "DES", "CRITERIA_DESCRIPTION_VALUE", "description") ?? "";
            if (n.Length > 0)
            {
                specs.Add((n, v));
            }
        }

        // English-only rule: when the en criteria call yielded nothing (provider
        // throttled), fall back to the cached Russian payload and translate the
        // well-known criterion names/values — values like "75 [мм]" are neutral.
        if (specs.Count == 0 && artId.Length > 0)
        {
            try
            {
                var ruCriteria = await partsApi.ExecuteAsync(new PartsApiRequest("getArticleCriteria",
                    new Dictionary<string, string> { ["ART_ID"] = artId, ["LANG"] = "16" }), cancellationToken).ConfigureAwait(false);
                foreach (var row in Rows(ruCriteria))
                {
                    var (n, unit) = TranslateSpecName(Text(row, "CRITERIA_NAME", "CRITERIA", "criteria", "name") ?? "");
                    var v = TranslateSpecValue(Text(row, "CRITERIA_VALUE", "criteriaValue", "value", "VALUE") ?? "");
                    if (n.Length == 0)
                    {
                        continue;
                    }

                    if (unit.Length > 0 && v.Length > 0 && char.IsDigit(v[0]) && !v.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
                    {
                        v = v + " " + unit;
                    }

                    specs.Add((n, v));
                }
            }
            catch (Exception)
            {
            }
        }

        if (specs.Count == 0 && umapiArticle.ValueKind == JsonValueKind.Object
            && umapiArticle.TryGetProperty("CRITERIAS", out var criterias) && criterias.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in criterias.EnumerateArray())
            {
                var n = Text(c, "CRI_SHORT_DES", "CRI_DES", "name") ?? "";
                var v = Text(c, "DES", "VALUE", "value") ?? "";
                var unit = Text(c, "CRI_UNIT_DES") ?? "";
                if (v.Length > 0 && unit.Length > 0 && !v.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
                {
                    v += " " + unit;
                }

                if (n.Length > 0)
                {
                    specs.Add((n, v));
                }
            }
        }

        var crosses = new List<string[]>();
        foreach (var row in Rows(results.GetValueOrDefault("crosses")))
        {
            var cb = Text(row, "ART_BRAND", "brand", "SUP_BRAND", "crossBrand", "OEM_BRAND") ?? "";
            var cn = Text(row, "ART_DISPLAY_NR", "number", "ARTICLE_NR", "crossNumber", "ART_NUM") ?? "";
            var cname = Text(row, "ART_PRODUCT_NAME", "name", "ART_NAME", "title") ?? "";
            if ((cb + cn).Length > 0)
            {
                crosses.Add(new[] { cb, cn, cname });
            }
        }

        // Cross refs are language-neutral — serve the cached ru-locale rows when
        // the en call was throttled and nothing came back.
        if (crosses.Count == 0 && artId.Length > 0)
        {
            try
            {
                var ruCrosses = await partsApi.ExecuteAsync(new PartsApiRequest("getArticleCrosses",
                    new Dictionary<string, string> { ["ART_ID"] = artId, ["LANG"] = "16" }), cancellationToken).ConfigureAwait(false);
                foreach (var row in Rows(ruCrosses))
                {
                    var cb = Text(row, "ART_BRAND", "brand", "SUP_BRAND", "crossBrand", "OEM_BRAND") ?? "";
                    var cn = Text(row, "ART_DISPLAY_NR", "number", "ARTICLE_NR", "crossNumber", "ART_NUM") ?? "";
                    var cname = Text(row, "ART_PRODUCT_NAME", "name", "ART_NAME", "title") ?? "";
                    if ((cb + cn).Length > 0)
                    {
                        crosses.Add(new[] { cb, cn, cname });
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // Same product under another brand (brand+article = unique product):
        // when the requested article has no photo, reuse a crossed article's
        // saved catalog image — cache reads only, zero provider calls.
        if (images.Count == 0 && crosses.Count > 0)
        {
            images = await LoadCrossFallbackImagesAsync(connections, context, crosses, cancellationToken)
                .ConfigureAwait(false);
        }

        // EAN/barcode — umapi article payload first, then any row field.
        var ean = "";
        if (umapiArticle.ValueKind == JsonValueKind.Object)
        {
            ean = Text(umapiArticle, "EAN", "EAN_CODE", "GTIN", "ART_EAN") ?? "";
            if (ean.Length == 0 && umapiArticle.TryGetProperty("EANS", out var eans) && eans.ValueKind == JsonValueKind.Array)
            {
                ean = eans.EnumerateArray().Select(x => Text(x, "EAN", "VALUE") ?? (x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : ""))
                    .FirstOrDefault(x => x.Length >= 8) ?? "";
            }
        }

        if (ean.Length == 0)
        {
            // Only the requested article's own payload — never fitment/cross rows,
            // which describe other products and would mislabel the barcode.
            foreach (var row in Rows(results.GetValueOrDefault("article")))
            {
                ean = Text(row, "EAN", "EAN_CODE", "GTIN", "BARCODE", "ART_EAN") ?? "";
                if (ean.Length >= 8)
                {
                    break;
                }

                ean = "";
            }
        }

        // Warehouse: same normalized-article lookup the part search uses, gated by
        // the shared storefront price visibility rule (guest/pending redacted).
        var access = await priceAccess.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var stock = await LoadWarehouseStockAsync(connections, context, number, brand, cancellationToken)
            .ConfigureAwait(false);
        var supLogoId = supId ?? await LoadSupplierLogoIdAsync(connections, context, brand, cancellationToken).ConfigureAwait(false);
        var brandMeta = await LoadBrandMetaAsync(connections, context, httpClientFactory, supLogoId, cancellationToken).ConfigureAwait(false);
        var crossLogoIds = await LoadSupplierLogoIdsAsync(connections, context,
            crosses.Select(c => c[0]).Where(b => b.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            cancellationToken).ConfigureAwait(false);
        var waDigits = await LoadWhatsappNumberAsync(connections, context, cancellationToken).ConfigureAwait(false);
        var waHref = waDigits.Length == 0
            ? string.Empty
            : "https://wa.me/" + waDigits
                + "?text=" + Uri.EscapeDataString(
                    "Price inquiry: " + brand + " " + number + (artName.Length > 0 ? " — " + artName : "")
                    + " https://" + context.Request.Host + "/en/parts/" + Uri.EscapeDataString(brand) + "/" + Uri.EscapeDataString(number));

        var html = Render(brand, number, artName, artCategory, images, specs, crosses,
            stock, access, ean, waHref, supLogoId, crossLogoIds, brandMeta);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Text(html, "text/html; charset=utf-8", Encoding.UTF8);
    }

    private static IEnumerable<JsonElement> Rows(PartsApiResult? result)
    {
        if (result is null || !result.Ok || result.Json.Length == 0)
        {
            yield break;
        }

        JsonDocument? doc;
        try { doc = JsonDocument.Parse(result.Json); }
        catch (JsonException) { yield break; }

        using (doc)
        {
            var el = doc.RootElement.Clone();
            for (var i = 0; i < 4; i++)
            {
                if (el.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in el.EnumerateArray())
                    {
                        yield return item;
                    }

                    yield break;
                }

                if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("data", out var inner))
                {
                    el = inner;
                    continue;
                }

                yield break;
            }
        }
    }

    private static string? Text(JsonElement row, params string[] names)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            foreach (var p in row.EnumerateObject())
            {
                if (p.NameEquals(name) && p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                {
                    return p.Value.ToString();
                }
            }
        }

        return null;
    }

    private static string NormalizeBrand(string s)
    {
        var chars = new char[s.Length];
        var n = 0;
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c))
            {
                chars[n++] = char.ToUpperInvariant(c);
            }
        }

        return new string(chars, 0, n);
    }

    private sealed record WarehouseRow(
        string Manufacturer, string ArticleShow, string Name, double Exist, double Price);

    private static async Task<List<WarehouseRow>> LoadWarehouseStockAsync(
        ITenantDbConnectionFactory connections, HttpContext context, string number,
        string brand, CancellationToken cancellationToken)
    {
        var rows = new List<WarehouseRow>();
        var compact = NormalizeBrand(number);
        if (compact.Length == 0)
        {
            return rows;
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken)
                .ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT UPPER(TRIM(`manufacturer`)) AS manufacturer,
                       COALESCE(NULLIF(`article_show`, ''), `article`) AS article_show,
                       MIN(`name`) AS name,
                       SUM(IFNULL(`exist`, 0)) AS exist,
                       MIN(`price`) AS price
                FROM `shop_docpart_prices_data`
                WHERE TRIM(IFNULL(`article`, '')) != ''
                  AND IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0
                  AND (
                    REPLACE(REPLACE(REPLACE(REPLACE(UPPER(TRIM(`article`)), ' ', ''), '-', ''), '.', ''), '/', '') = @compact
                    OR REPLACE(REPLACE(REPLACE(REPLACE(UPPER(TRIM(`article_show`)), ' ', ''), '-', ''), '.', ''), '/', '') = @compact
                  )
                GROUP BY UPPER(TRIM(`manufacturer`)), COALESCE(NULLIF(`article_show`, ''), `article`)
                ORDER BY MAX(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(TRIM(`manufacturer`)), ' ', ''), '-', ''), '.', ''), '/', '') = @brand) DESC, manufacturer ASC, price ASC
                LIMIT 60
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@compact";
            parameter.Value = compact;
            command.Parameters.Add(parameter);
            var brandParameter = command.CreateParameter();
            brandParameter.ParameterName = "@brand";
            brandParameter.Value = NormalizeBrand(brand);
            command.Parameters.Add(brandParameter);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new WarehouseRow(
                    reader.IsDBNull(0) ? "" : reader.GetString(0),
                    reader.IsDBNull(1) ? "" : reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? 0 : Convert.ToDouble(reader.GetValue(3), CultureInfo.InvariantCulture),
                    reader.IsDBNull(4) ? 0 : Convert.ToDouble(reader.GetValue(4), CultureInfo.InvariantCulture)));
            }
        }
        catch (Exception)
        {
            // Missing connection string (InvalidOperationException) or absent
            // price table (DbException) — the section renders empty either way.
        }

        return rows;
    }

    // Real make logos for the Suitable-vehicles tiles — the umapi manufacturers
    // table already carries MNF ids with is_logo for synced tenants.
    private static async Task<Dictionary<string, int>> LoadMakeLogoIdsAsync(
        ITenantDbConnectionFactory connections, HttpContext context,
        List<string> makes, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (makes.Count == 0)
        {
            return map;
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken)
                .ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `manufacturer`, MIN(`mfa_id`) FROM `epc_umapi_manufacturers` WHERE `is_logo` = 1 GROUP BY `manufacturer`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var byNorm = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = reader.IsDBNull(0) ? "" : reader.GetString(0);
                if (name.Length > 0)
                {
                    byNorm[NormalizeBrand(name)] = reader.GetInt32(1);
                }
            }

            foreach (var make in makes)
            {
                if (byNorm.TryGetValue(NormalizeBrand(make), out var id))
                {
                    map[make] = id;
                }
            }
        }
        catch (Exception)
        {
        }

        return map;
    }

    private static async Task<string?> LoadSupplierLogoIdAsync(
        ITenantDbConnectionFactory connections, HttpContext context, string brand,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken)
                .ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `sup_id` FROM `epc_umapi_brands` WHERE UPPER(TRIM(`brand`)) = @brand LIMIT 1";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@brand";
            parameter.Value = brand.Trim().ToUpperInvariant();
            command.Parameters.Add(parameter);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Supplier profile line (company name · city · website) via emshcloud <c>marka-adres</c>.
    /// Fetched once per brand into epc_emsh_cache — the subscription is per-request,
    /// so a brand's profile is only ever fetched a single time.
    /// </summary>
    private static async Task<string?> LoadBrandMetaAsync(
        ITenantDbConnectionFactory connections, HttpContext context, IHttpClientFactory httpClientFactory,
        string? supId, CancellationToken cancellationToken)
    {
        if (supId is null || UmapiImageProxy.PhpIntCast(supId) < 1)
        {
            return null;
        }

        var cacheKey = "emsh|madr|" + supId;
        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
            await using (var ddl = connection.CreateCommand())
            {
                ddl.CommandText =
                    "CREATE TABLE IF NOT EXISTS `epc_emsh_cache` (" +
                    "`cache_key` VARCHAR(191) NOT NULL PRIMARY KEY," +
                    "`response_json` LONGTEXT NULL," +
                    "`last_sync` INT UNSIGNED NOT NULL DEFAULT 0" +
                    ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
                await ddl.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            string? body = null;
            await using (var get = connection.CreateCommand())
            {
                get.CommandText = "SELECT `response_json` FROM `epc_emsh_cache` WHERE `cache_key` = @k LIMIT 1";
                var p = get.CreateParameter();
                p.ParameterName = "@k";
                p.Value = cacheKey;
                get.Parameters.Add(p);
                var v = await get.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                body = v as string;
            }

            if (body is null)
            {
                var key = await EmshcloudVinDecode.ApiKeyAsync(context, cancellationToken).ConfigureAwait(false);
                if (key.Length == 0)
                {
                    return null;
                }
                var client = httpClientFactory.CreateClient();
                using var request = new HttpRequestMessage(
                    HttpMethod.Get, "https://tecdoc.emshcloud.com/v1/marka-adres?id=" + Uri.EscapeDataString(supId) + "&dil=en");
                request.Headers.TryAddWithoutValidation("X-API-Key", key);
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                await using var put = connection.CreateCommand();
                put.CommandText =
                    "INSERT INTO `epc_emsh_cache` (`cache_key`, `response_json`, `last_sync`) VALUES (@k, @b, UNIX_TIMESTAMP()) " +
                    "ON DUPLICATE KEY UPDATE `response_json` = VALUES(`response_json`), `last_sync` = VALUES(`last_sync`)";
                var k = put.CreateParameter();
                k.ParameterName = "@k";
                k.Value = cacheKey;
                put.Parameters.Add(k);
                var b = put.CreateParameter();
                b.ParameterName = "@b";
                b.Value = body;
                put.Parameters.Add(b);
                await put.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("sonuc", out var sonuc)
                || sonuc.ValueKind != JsonValueKind.Array || sonuc.GetArrayLength() == 0)
            {
                return null;
            }

            var r = sonuc[0];
            string T(string n) => r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
            // Row-wise brand profile — each line renders as its own row, country excluded.
            var lines = new List<string>();
            var company = T("ad1");
            var company2 = T("ad2");
            if (company.Length == 0)
            {
                company = company2;
                company2 = "";
            }
            var site = T("web");
            foreach (var piece in new[] { company, company2, site })
            {
                if (piece.Length > 0 && !lines.Contains(piece, StringComparer.OrdinalIgnoreCase))
                {
                    lines.Add(piece);
                }
            }
            return lines.Count == 0 ? null : "\u0001" + string.Join("\u0001", lines);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<Dictionary<string, string>> LoadSupplierLogoIdsAsync(
        ITenantDbConnectionFactory connections, HttpContext context, List<string> brands,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (brands.Count == 0)
        {
            return map;
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken)
                .ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `sup_id`, `brand` FROM `epc_umapi_brands`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var wanted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in brands)
            {
                wanted[NormalizeBrand(b)] = b;
            }

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var norm = NormalizeBrand(name);
                if (wanted.ContainsKey(norm) && !map.ContainsKey(norm))
                {
                    map[norm] = reader.GetInt32(0).ToString(CultureInfo.InvariantCulture);
                }
            }
        }
        catch (Exception)
        {
        }

        return map;
    }

    private static async Task<string> LoadWhatsappNumberAsync(
        ITenantDbConnectionFactory connections, HttpContext context, CancellationToken cancellationToken)
    {
        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken)
                .ConfigureAwait(false);
            var value = await ErpDb.StringAsync(connection, null,
                "SELECT `setting_value` FROM `epc_settings` WHERE `setting_key` IN ('whatsapp_number','whatsapp','contact_whatsapp') LIMIT 1",
                cancellationToken).ConfigureAwait(false);
            var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length >= 8)
            {
                return digits;
            }
        }
        catch (Exception)
        {
        }

        return string.Empty;
    }

    private static string E(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    // Barcode module pattern: EAN-13 (95 modules) or EAN-8 (67 modules).
    private static bool[]? Ean13Bars(string ean)
    {
        var digits = new string(ean.Where(char.IsDigit).ToArray());
        if (digits.Length == 12)
        {
            // UPC-A is exactly EAN-13 with a leading zero — prefix, never append.
            digits = "0" + digits;
        }

        if (digits.Length == 7)
        {
            var sum7 = digits.Select((c, i) => (c - '0') * (i % 2 == 0 ? 3 : 1)).Sum();
            digits += ((10 - sum7 % 10) % 10).ToString();
        }

        var ean8 = digits.Length == 8;
        if (!ean8 && digits.Length != 13)
        {
            return null;
        }

        var d = digits.Select(c => c - '0').ToArray();
        var L = new[] { "0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011" };
        var G = new[] { "0100111", "0110011", "0011011", "0100001", "0011101", "0111001", "0000101", "0010001", "0001001", "0010111" };
        var R = new[] { "1110010", "1100110", "1101100", "1000010", "1011100", "1001110", "1010000", "1000100", "1001000", "1110100" };
        var sb2 = new StringBuilder("101");
        if (ean8)
        {
            for (var i = 0; i < 4; i++)
            {
                sb2.Append(L[d[i]]);
            }

            sb2.Append("01010");
            for (var i = 4; i < 8; i++)
            {
                sb2.Append(R[d[i]]);
            }

            sb2.Append("101");
            return sb2.ToString().Select(c => c == '1').ToArray();
        }

        var firstG = new[] { "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL" };
        var pattern = firstG[d[0]];
        for (var i = 0; i < 6; i++)
        {
            sb2.Append(pattern[i] == 'L' ? L[d[i + 1]] : G[d[i + 1]]);
        }

        sb2.Append("01010");
        for (var i = 0; i < 6; i++)
        {
            sb2.Append(R[d[i + 7]]);
        }

        sb2.Append("101");
        return sb2.ToString().Select(c => c == '1').ToArray();
    }

    private static (string Name, string Unit) TranslateSpecName(string name)
    {
        var t = (name ?? "").Trim();
        var unit = "";
        // Strip "[мм]" / "(мм)" unit suffix → English unit appended to the value.
        var m = System.Text.RegularExpressions.Regex.Match(t, @"[\[\(](мм|см|м|бар|кг|л|мл|шт|Вт|В|А|л\.с\.|град|°)[\]\)]\s*$");
        if (m.Success)
        {
            unit = m.Groups[1].Value switch
            {
                "мм" => "mm", "см" => "cm", "м" => "m", "бар" => "bar", "кг" => "kg",
                "л" => "l", "мл" => "ml", "шт" => "pcs", "Вт" => "W", "В" => "V",
                "А" => "A", "л.с." => "hp", "град" or "°" => "°",
                _ => m.Groups[1].Value,
            };
            t = t[..m.Index].TrimEnd();
        }

        var en = t switch
        {
            "Высота" => "Height",
            "Ширина" => "Width",
            "Длина" => "Length",
            "Размер резьбы" => "Thread Size",
            "Диаметр прокладки" => "Seal Diameter",
            "Внутренний диаметр" => "Inner diameter",
            "Внутренний диаметр 2" => "Inner diameter 2",
            "Наружный диаметр" or "Внешний диаметр" => "Outer diameter",
            "Наружный диаметр 2" or "Внешний диаметр 2" => "Outer diameter 2",
            "Вес" => "Weight",
            "Исполнение фильтра" or "Тип фильтра" => "Filter type",
            "Направление потока" => "Flow direction",
            "Давление открытия обгонного клапана" or "Давление открытия перепускного клапана" => "Bypass Valve Opening Pressure",
            "Давление открытия обходного клапана" => "Bypass Valve Opening Pressure",
            "Пропускная способность" => "Flow rate",
            "Монтажное положение" => "Fitting position",
            "Типоразмер" => "Size",
            "Количество полюсов" => "Number of poles",
            "Напряжение" => "Voltage",
            "Мощность" => "Power",
            "Исполнение для страны" => "Country of design",
            "Дополнительный артикул / дополнительная инфор" or "Дополнительный артикул" or "Дополнительная информация" => "Additional info",
            _ => t,
        };
        return (en, unit);
    }

    private static string TranslateSpecValue(string value)
    {
        var t = (value ?? "").Trim();
        if (t.Length == 0)
        {
            return t;
        }

        // units / booleans
        if (t == "да" || t.Equals("Да", StringComparison.OrdinalIgnoreCase)) return "yes";
        if (t == "нет" || t.Equals("Нет", StringComparison.OrdinalIgnoreCase)) return "no";
        if (t == "Россия") return "Russia";
        if (t == "Накручиваемый фильтр" || t == "Резьбовой фильтр") return "Screw-on Filter";
        if (t.StartsWith("с возвратным клапаном", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("С обратным клапаном", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("с одним возвратным клапаном", StringComparison.OrdinalIgnoreCase))
        {
            return "With one anti-return valve";
        }
        if (t.StartsWith("без возвратного клапана", StringComparison.OrdinalIgnoreCase))
        {
            return "Without anti-return valve";
        }
        if (t.StartsWith("в наличии", StringComparison.OrdinalIgnoreCase)) return "in stock";
        return t;
    }

    private static string Render(string brand, string number, string name, string category,
        List<string> images, List<(string Name, string Value)> specs,
        List<string[]> crosses,
        List<WarehouseRow> stock, StorefrontPriceAccessResult access, string ean,
        string waHref, string? supId,
        Dictionary<string, string> crossLogoIds, string? brandMeta = null)
    {
        var wantedBrand = NormalizeBrand(brand);
        var inStock = stock.Exists(s => s.Exist > 0 && NormalizeBrand(s.Manufacturer) == wantedBrand);
        var oe = crosses.Take(5).Select(c => c[1]).Where(x => x.Length > 0).ToList();
        var sb = new StringBuilder(24000);
        sb.Append("<div class=\"epc-prod\"><style>");
        sb.Append(".epc-prod{font:13px/1.45 Arial,Helvetica,sans-serif;color:#333;background:#fff;border:1px solid #e4e7ec;border-radius:10px;box-shadow:0 2px 14px rgba(16,24,40,.07);overflow:hidden}");
        sb.Append(".epc-prod .epc-prod-title{background:linear-gradient(#fdeadd,#fdf4ea);border-bottom:2px solid #e8600a;color:#e8600a;font-weight:700;font-size:17px;padding:12px 24px}");
        sb.Append(".epc-prod .epc-prod-crumb{font-size:12px;color:#888;padding:8px 24px;border-bottom:1px solid #f0f2f5}");
        sb.Append(".epc-prod .epc-prod-crumb a{color:#888;text-decoration:none}");
        sb.Append(".epc-prod .epc-prod-hero{display:flex;gap:30px;background:#fff;border-bottom:1px solid #eef0f3;padding:22px 24px;flex-wrap:wrap}");
        sb.Append(".epc-prod .epc-prod-gallery{flex:0 0 300px;max-width:300px}");
        sb.Append(".epc-prod .epc-prod-pic{width:300px;height:300px;object-fit:contain;border:1px solid #eef0f3;border-radius:8px;background:#fff;padding:8px}");
        sb.Append(".epc-prod .epc-prod-thumbs{display:flex;gap:8px;margin-top:10px;flex-wrap:wrap}");
        sb.Append(".epc-prod .epc-prod-thumbs img{width:58px;height:58px;object-fit:contain;border:1px solid #e0e0e0;cursor:pointer}");
        sb.Append(".epc-prod .epc-prod-thumbs img:hover{border-color:#e8600a}");
        sb.Append(".epc-prod .epc-prod-info{flex:1 1 380px;min-width:280px}");
        sb.Append(".epc-prod .epc-prod-brand{display:flex;align-items:flex-start;gap:10px;margin:0 0 10px}");
        sb.Append(".epc-prod .epc-prod-brand img{max-height:44px;max-width:140px;object-fit:contain}");
        sb.Append(".epc-prod .epc-prod-brand .epc-prod-brand-name{display:block;font-weight:700;color:#333;font-size:14px}");
        sb.Append(".epc-prod .epc-prod-brand-meta{display:block;margin-top:3px}");
        sb.Append(".epc-prod .epc-prod-brand-meta-row{display:block;font-weight:400;color:#64748b;font-size:12px;line-height:1.5}");
        sb.Append(".epc-prod .epc-prod-name{color:#e8600a;font-size:15px;font-weight:700;margin:0 0 8px}");
        sb.Append(".epc-prod .epc-prod-bullets{margin:0 0 12px;padding-left:18px;color:#333}");
        sb.Append(".epc-prod .epc-prod-bullets b{color:#000}");
        sb.Append(".epc-prod .epc-prod-specs-title{color:#e8600a;border-bottom:2px solid #e8600a;font-weight:700;padding-bottom:3px;margin:0 0 8px}");
        sb.Append(".epc-prod .epc-prod-specs{width:100%;border-collapse:collapse}");
        sb.Append(".epc-prod .epc-prod-specs td{padding:7px 10px;text-align:center}");
        sb.Append(".epc-prod .epc-prod-specs tr:nth-child(odd) td{background:#f5f5f5}");
        sb.Append(".epc-prod .epc-prod-stock{flex:0 0 170px}");
        sb.Append(".epc-prod .epc-prod-stockbox{border:1px solid #e4e7ec;border-radius:8px;padding:14px;background:#fafbfc;text-align:center;margin-bottom:10px;box-shadow:0 1px 4px rgba(16,24,40,.04)}");
        sb.Append(".epc-prod .epc-prod-stockbox .ok{color:#2d8a2d;font-weight:700}");
        sb.Append(".epc-prod .epc-prod-stockbox .no{color:#666;font-weight:700}");
        sb.Append(".epc-prod .epc-prod-ask{display:block;border:1px solid #ddd;border-radius:8px;background:#fff;color:#2d8a2d;padding:10px;text-align:center;text-decoration:none;font-weight:700}");
        sb.Append(".epc-prod .epc-prod-ask--wa{border-color:#25d366;background:#25d366;color:#fff}");
        sb.Append(".epc-prod .epc-prod-tabs{margin-top:6px;border-bottom:1px solid #e4e7ec;display:flex;gap:4px;padding:0 24px;background:#fafbfc}");
        sb.Append(".epc-prod .epc-prod-tab{border:1px solid transparent;border-bottom:0;background:transparent;color:#777;padding:10px 20px;cursor:pointer;font-size:14px;border-top-left-radius:6px;border-top-right-radius:6px}");
        sb.Append(".epc-prod .epc-prod-tab.on{background:#fff;color:#e8600a;border-top:2px solid #e8600a;border-left:1px solid #e4e7ec;border-right:1px solid #e4e7ec;font-weight:700}");
        sb.Append(".epc-prod .epc-prod-pane{background:#fff;padding:20px 24px}");
        sb.Append(".epc-prod .epc-oe-table{width:100%;border-collapse:collapse;font-size:12px}");
        sb.Append(".epc-prod .epc-oe-scroll{max-height:380px;overflow-y:auto;border:1px solid #eef0f3;border-radius:8px;padding:4px 10px}");
        sb.Append(".epc-prod .epc-oe-table td{border:1px solid #eee;padding:6px 10px;vertical-align:top}");
        sb.Append(".epc-prod .epc-oe-table td.brand{font-weight:700;text-align:center;background:#fafafa;white-space:nowrap}");
        sb.Append(".epc-prod .epc-oe-logo{display:block;max-width:110px;max-height:28px;object-fit:contain;margin:0 auto 4px}");
        sb.Append(".epc-prod .epc-oe-num{display:inline-block;margin:0 14px 6px 0;color:#333}");
        sb.Append(".epc-prod .epc-oe-num a{color:#e8600a;text-decoration:none}");
        sb.Append(".epc-prod .epc-veh-makes{display:flex;gap:10px;flex-wrap:wrap;margin-bottom:14px}");
        sb.Append(".epc-prod .epc-veh-make{width:56px;height:56px;border:1px solid #e0e0e0;border-radius:4px;display:flex;align-items:center;justify-content:center;cursor:pointer;font-weight:700;font-size:10px;color:#555;text-align:center;background:#fff;padding:4px}");
        sb.Append(".epc-prod .epc-veh-make.on{border:2px solid #e8600a;color:#e8600a;box-shadow:0 1px 4px rgba(232,96,10,.25)}");
        sb.Append(".epc-prod .epc-veh-grid{display:grid;grid-template-columns:1fr 1fr;gap:0 30px}");
        sb.Append(".epc-prod #epcVehRows{max-height:240px;overflow-y:auto;border:1px solid #eef0f3;border-radius:8px;padding:2px 8px}");
        sb.Append(".epc-prod .epc-veh-sub{color:#e8600a;font-weight:700;margin:0 0 6px;padding-bottom:4px;border-bottom:1px solid #eee}");
        sb.Append(".epc-prod .epc-veh-row{display:grid;grid-template-columns:1fr 1fr;gap:0 30px;padding:9px 6px;font-size:13px;border-bottom:1px solid #f0f0f0;color:inherit;text-decoration:none}");
        sb.Append(".epc-prod .epc-veh-row:nth-child(even){background:#faf6f2}");
        sb.Append(".epc-prod .epc-veh-row:hover{background:#fdeadd}");
        sb.Append(".epc-prod .epc-veh-row b{color:#e8600a}");
        sb.Append(".epc-prod .epc-veh-mdl{color:#333}");
        sb.Append(".epc-prod .epc-veh-eng{color:#444}");
        sb.Append(".epc-prod .epc-ean{margin-top:18px}");
        sb.Append(".epc-prod .epc-ean-label{color:#e8600a;font-weight:700}");
        sb.Append(".epc-prod .epc-ean-code{font-size:15px;color:#333;margin:4px 0 6px}");
        sb.Append(".epc-prod .epc-ean-bars{fill:#000}");
        sb.Append(".epc-prod .epc-empty{color:#888;padding:10px}");
        sb.Append("</style>");

        // Title bar
        var title = name.Length > 0 ? brand + " " + number + " - " + name : brand + " " + number;
        if (oe.Count > 0)
        {
            title += " (" + string.Join(", ", oe) + (crosses.Count > 5 ? "…" : "") + ")";
        }
        sb.Append("<div class=\"epc-prod-title\">").Append(E(title)).Append("</div>");
        sb.Append("<div class=\"epc-prod-crumb\"><a href=\"/\">Home</a> › <a href=\"/storefront/available-brands-app\">Manufacturers</a> › <span>")
            .Append(E(brand)).Append("</span> › <span>").Append(E(number)).Append("</span></div>");

        // Hero
        sb.Append("<div class=\"epc-prod-hero\">");
        sb.Append("<div class=\"epc-prod-gallery\">");
        if (images.Count > 0)
        {
            sb.Append("<img class=\"epc-prod-pic\" id=\"epcProdPic\" src=\"").Append(E(images[0])).Append("\" alt=\"").Append(E(number)).Append("\">");
            if (images.Count > 1)
            {
                sb.Append("<div class=\"epc-prod-thumbs\">");
                foreach (var img in images)
                {
                    sb.Append("<img src=\"").Append(E(img)).Append("\" alt=\"\" onclick=\"document.getElementById('epcProdPic').src=this.src\">");
                }
                sb.Append("</div>");
            }
        }
        else
        {
            sb.Append("<div class=\"epc-prod-pic\" style=\"display:flex;align-items:center;justify-content:center;color:#aaa;border:1px dashed #ddd\">No photo yet</div>");
        }
        sb.Append("</div>");

        sb.Append("<div class=\"epc-prod-info\">");
        var metaHtml = "";
        if (brandMeta is { Length: > 0 })
        {
            metaHtml = "<span class=\"epc-prod-brand-meta\">" + string.Join("", brandMeta
                .Split('\u0001', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => "<span class=\"epc-prod-brand-meta-row\">" + E(line) + "</span>")) + "</span>";
        }
        var brandBlock = "<span><span class=\"epc-prod-brand-name\">" + E(brand) + "</span>" + metaHtml + "</span>";
        if (supId is not null && UmapiImageProxy.TryImageUrl("supplier", supId, out _))
        {
            sb.Append("<div class=\"epc-prod-brand\"><img src=\"/api/umapi_image.php?kind=supplier&id=")
                .Append(E(supId)).Append("\" alt=\"").Append(E(brand)).Append("\" loading=\"lazy\" onerror=\"this.style.display='none'\">")
                .Append(brandBlock).Append("</div>");
        }
        else
        {
            sb.Append("<div class=\"epc-prod-brand\">").Append(brandBlock).Append("</div>");
        }
        sb.Append("<div class=\"epc-prod-name\">").Append(E(name.Length > 0 ? name : "Auto part")).Append("</div>");
        sb.Append("<ul class=\"epc-prod-bullets\"><li>").Append(E(brand)).Append(" ").Append(E(number))
            .Append(name.Length > 0 ? " — " + E(name) : "").Append("</li>");
        if (category.Length > 0)
        {
            sb.Append("<li>Category: <b>").Append(E(category)).Append("</b></li>");
        }
        sb.Append("</ul>");
        if (specs.Count > 0)
        {
            sb.Append("<div class=\"epc-prod-specs-title\">Specifications:</div><table class=\"epc-prod-specs\">");
            foreach (var (n, v) in specs.Take(12))
            {
                sb.Append("<tr><td>").Append(E(n)).Append(":</td><td><b>").Append(E(v)).Append("</b></td></tr>");
            }
            sb.Append("</table>");
        }
        sb.Append("</div>");

        // Stock box
        sb.Append("<div class=\"epc-prod-stock\">");
        var totalStock = stock.Where(s => NormalizeBrand(s.Manufacturer) == wantedBrand).Sum(s => s.Exist);
        sb.Append("<div class=\"epc-prod-stockbox\">");
        if (inStock)
        {
            sb.Append(access.PricesVisible
                ? "<span class=\"ok\">In stock: " + totalStock.ToString("0.##", CultureInfo.InvariantCulture) + "</span>"
                : "<span class=\"ok\">In stock</span>");
        }
        else
        {
            sb.Append("<span class=\"no\">Out of stock</span>");
        }
        sb.Append("</div>");
        if (waHref.Length > 0)
        {
            sb.Append("<a class=\"epc-prod-ask epc-prod-ask--wa\" href=\"" + E(waHref) + "\" target=\"_blank\" rel=\"noopener\"><i class=\"fa fa-whatsapp\" aria-hidden=\"true\"></i> WhatsApp inquiry</a>");
        }
        sb.Append("</div></div>");

        // Tabs
        sb.Append("<div class=\"epc-prod-tabs\">")
            .Append("<button type=\"button\" class=\"epc-prod-tab on\" onclick=\"epcProdTab(this,'oe')\">OE Numbers</button>")
            .Append("<button type=\"button\" class=\"epc-prod-tab\" onclick=\"epcProdTab(this,'veh')\">Suitable vehicles</button>")
            .Append("<button type=\"button\" class=\"epc-prod-tab\" onclick=\"epcProdTab(this,'rev')\">Reviews (0)</button>")
            .Append("</div>");

        // OE Numbers pane
        sb.Append("<div class=\"epc-prod-pane\" data-pane=\"oe\">");
        if (crosses.Count > 0)
        {
            sb.Append("<div class=\"epc-oe-scroll\"><table class=\"epc-oe-table\">");
            foreach (var grp in crosses.GroupBy(c => c[0], StringComparer.OrdinalIgnoreCase))
            {
                sb.Append("<tr><td class=\"brand\">");
                if (crossLogoIds.TryGetValue(NormalizeBrand(grp.Key), out var crossLogoId))
                {
                    sb.Append("<img class=\"epc-oe-logo\" src=\"/api/umapi_image.php?kind=supplier&id=")
                        .Append(E(crossLogoId)).Append("\" alt=\"\" loading=\"lazy\" onerror=\"this.style.display='none'\">");
                }
                sb.Append(E(grp.Key)).Append("</td><td>");
                foreach (var c in grp)
                {
                    sb.Append("<span class=\"epc-oe-num\"><a href=\"/en/parts/").Append(Uri.EscapeDataString(c[0].ToUpperInvariant()))
                        .Append("/").Append(Uri.EscapeDataString(c[1])).Append("\">").Append(E(c[1])).Append("</a></span>");
                }
                sb.Append("</td></tr>");
            }
            sb.Append("</table></div>");
        }
        else
        {
            sb.Append("<div class=\"epc-empty\">No cross references found for this article.</div>");
        }
        if (ean.Length >= 8)
        {
            sb.Append("<div class=\"epc-ean\"><span class=\"epc-ean-label\">Barcode (EAN):</span>")
                .Append("<div class=\"epc-ean-code\">").Append(E(ean)).Append("</div>");
            var bars = Ean13Bars(ean);
            if (bars is not null)
            {
                const int w = 2, h = 46, q = 8;
                var width = (bars.Length + q * 2) * w;
                sb.Append("<svg class=\"epc-ean-bars\" width=\"").Append(width).Append("\" height=\"").Append(h + 12)
                    .Append("\" xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" aria-label=\"EAN barcode\">");
                for (var i = 0; i < bars.Length; i++)
                {
                    if (bars[i])
                    {
                        sb.Append("<rect x=\"").Append((i + q) * w).Append("\" y=\"0\" width=\"").Append(w).Append("\" height=\"").Append(h).Append("\"/>");
                    }
                }
                sb.Append("<text x=\"").Append(width / 2).Append("\" y=\"").Append(h + 10)
                    .Append("\" text-anchor=\"middle\" font-size=\"11\" font-family=\"monospace\">").Append(E(ean)).Append("</text></svg>");
            }
            sb.Append("</div>");
        }
        sb.Append("</div>");

        // Suitable vehicles pane — content loads on first tab open via
        // /api/article_fitment.php so plain page views cost no provider call.
        sb.Append("<div class=\"epc-prod-pane\" data-pane=\"veh\" hidden>")
            .Append("<div class=\"epc-veh-lazy\" data-loaded=\"0\" data-article=\"").Append(E(number))
            .Append("\" data-brand=\"").Append(E(brand)).Append("\">")
            .Append("<div class=\"epc-empty\">Open this tab to load suitable vehicles.</div>")
            .Append("</div></div>");

        // Reviews pane
        sb.Append("<div class=\"epc-prod-pane\" data-pane=\"rev\" hidden><div class=\"epc-empty\">No reviews yet for this product.</div></div>");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>
    /// <c>/api/article_fitment.php?article=&brand=</c> — click-gated fitment
    /// fragment for the "Suitable vehicles" tab. partsapi answers are cached
    /// fetch-once in <c>epc_partsapi_cache</c>, so repeat clicks are free.
    /// </summary>
    private static async Task<IResult> HandleFitmentAsync(
        HttpContext context,
        IPartsApiService partsApi,
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var brand = context.Request.Query["brand"].ToString().Trim();
        var number = context.Request.Query["article"].ToString().Trim();
        if (number.Length == 0)
        {
            number = context.Request.Query["number"].ToString().Trim();
        }

        if (brand.Length == 0 || number.Length == 0)
        {
            return Results.Text("brand and article are required", "text/plain; charset=utf-8",
                Encoding.UTF8, StatusCodes.Status400BadRequest);
        }

        var vehicles = await LoadVehiclesAsync(partsApi, number, brand, cancellationToken).ConfigureAwait(false);
        var makeLogos = await LoadMakeLogoIdsAsync(connections, context,
            vehicles.Select(v => v[0]).Where(m => m.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            cancellationToken).ConfigureAwait(false);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Text(RenderVehicleRows(vehicles, makeLogos), "text/html; charset=utf-8", Encoding.UTF8);
    }

    private static async Task<List<string[]>> LoadVehiclesAsync(
        IPartsApiService partsApi, string number, string brand,
        CancellationToken cancellationToken)
    {
        var vehicles = new List<string[]>();
        var fitment = await partsApi.ExecuteAsync(new PartsApiRequest("getApplicability",
            new Dictionary<string, string> { ["sku"] = number, ["brand"] = brand }), cancellationToken).ConfigureAwait(false);
        var fitmentRows = Rows(fitment);
        if (!fitmentRows.Any())
        {
            // getApplicability needs the brand parameter some suppliers don't
            // publish; getApplicability2 keys on the number alone and returns
            // the full linked-vehicle list.
            try
            {
                var fitment2 = await partsApi.ExecuteAsync(new PartsApiRequest("getApplicability2",
                    new Dictionary<string, string> { ["sku"] = number }), cancellationToken).ConfigureAwait(false);
                fitmentRows = Rows(fitment2);
            }
            catch (Exception)
            {
            }
        }

        foreach (var row in fitmentRows)
        {
            var carName = Text(row, "carName") ?? "";
            var sp = carName.IndexOf(' ');
            var cells = new[]
            {
                Text(row, "make", "makeName", "MFA_BRAND", "manufacturer")
                    ?? (sp > 0 ? carName[..sp] : (carName.Length > 0 ? carName : "")),
                Text(row, "model", "modelName", "MOD_CDS_TEXT")
                    ?? (sp > 0 ? carName[(sp + 1)..] : ""),
                Text(row, "modification", "type", "engine")
                    ?? (Text(row, "carName") is null ? ""
                        : (((Text(row, "CAPACITY") ?? "") is { Length: > 0 } cap ? cap + "cc " : "")
                            + ((Text(row, "POWER_KW") ?? "") is { Length: > 0 } kw ? kw + "kW " : "")
                            + (Text(row, "PC_ENG_CODES") ?? "")).Trim()),
                Text(row, "yearsFrom", "yearFrom", "yearStart", "from") ?? "",
                Text(row, "yearsTo", "yearTo", "yearEnd", "to") ?? "",
            };
            if (cells.Any(c => c.Length > 0))
            {
                vehicles.Add(cells);
            }

            if (vehicles.Count >= 300)
            {
                break;
            }
        }

        return vehicles;
    }

    private static string RenderVehicleRows(List<string[]> vehicles, Dictionary<string, int> makeLogos)
    {
        var sb = new StringBuilder(8000);
        if (vehicles.Count == 0)
        {
            sb.Append("<div class=\"epc-empty\">No vehicle applicability found for this article.</div>");
            return sb.ToString();
        }

        var makeList = vehicles.Select(v => v[0]).Where(m => m.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        sb.Append("<div class=\"epc-veh-makes\">");
        foreach (var m in makeList)
        {
            sb.Append("<button type=\"button\" class=\"epc-veh-make\" onclick=\"epcProdMake(this)\" data-make=\"")
                .Append(E(m)).Append("\" title=\"").Append(E(m)).Append("\">");
            if (makeLogos.TryGetValue(m, out var logoId))
            {
                sb.Append("<img src=\"/api/umapi_image.php?kind=manufacturer&id=").Append(logoId)
                    .Append("\" alt=\"").Append(E(m)).Append("\" style=\"max-width:100%;max-height:100%;object-fit:contain\" onerror=\"this.outerHTML='" + E((m.Length > 6 ? m[..6] : m).Replace("\\", "\\\\").Replace("'", "\\'").Replace("<", "")) + "'\">");
            }
            else
            {
                sb.Append(E(m.Length > 6 ? m[..6] : m));
            }
            sb.Append("</button>");
        }

        sb.Append("</div><div class=\"epc-veh-grid\"><div class=\"epc-veh-col\"><div class=\"epc-veh-sub\">Model, Year</div></div><div class=\"epc-veh-col\"><div class=\"epc-veh-sub\">Engine</div></div></div><div id=\"epcVehRows\">");
        foreach (var v in vehicles)
        {
            var years = (v[3] + (v[4].Length > 0 ? " - " + v[4] : "")).Trim(' ', '-');
            // car-mod: clicking a vehicle opens that car's catalog drill-down.
            var carUrl = "/en/catalog?make=" + Uri.EscapeDataString(v[0])
                + (v[1].Length > 0 ? "&model=" + Uri.EscapeDataString(v[1].Split('(')[0].Trim()) : "");
            sb.Append("<a class=\"epc-veh-row\" data-make=\"").Append(E(v[0])).Append("\" href=\"").Append(E(carUrl)).Append("\"><div class=\"epc-veh-cell\"><b>")
                .Append(E(v[1].Length > 0 ? v[1] : v[0])).Append("</b>")
                .Append(years.Length > 0 ? "<span class=\"epc-veh-mdl\">, " + E(years) + "</span>" : "")
                .Append("</div><div class=\"epc-veh-cell epc-veh-eng\">")
                .Append(v[2].Length > 0 ? E(v[2]) : "")
                .Append("</div></a>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>
    /// Finds a saved catalog photo for the same part under a crossed brand.
    /// Reads <c>epc_umapi_cache</c> only — never an upstream call.
    /// </summary>
    private static async Task<List<string>> LoadCrossFallbackImagesAsync(
        ITenantDbConnectionFactory connections, HttpContext context,
        List<string[]> crosses, CancellationToken cancellationToken)
    {
        var images = new List<string>();
        if (crosses.Count == 0)
        {
            return images;
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken)
                .ConfigureAwait(false);

            foreach (var cross in crosses.Take(12))
            {
                var crossNumber = cross[1];
                if (crossNumber.Length == 0)
                {
                    continue;
                }

                await using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT `response_json` FROM `epc_umapi_cache` WHERE `action` = 'article' AND `response_json` LIKE @like LIMIT 10";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@like";
                parameter.Value = "%" + crossNumber + "%";
                command.Parameters.Add(parameter);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var json = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    if (json.Length == 0)
                    {
                        continue;
                    }

                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement.ValueKind == JsonValueKind.Object
                            && doc.RootElement.TryGetProperty("data", out var data) ? data : doc.RootElement;
                        if (root.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var payloadNumber = Text(root, "ART_NUM", "ARTICLE_NR", "number") ?? "";
                        if (!NormalizeBrand(payloadNumber).Equals(NormalizeBrand(crossNumber), StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var payloadBrand = NormalizeBrand(Text(root, "SUP_BRAND", "BRAND", "ART_SUP_BRAND") ?? "");
                        if (payloadBrand.Length > 0 && cross[0].Length > 0
                            && !payloadBrand.Equals(NormalizeBrand(cross[0]), StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (root.TryGetProperty("MEDIAS", out var medias) && medias.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var m in medias.EnumerateArray())
                            {
                                var file = Text(m, "FILE_NAME", "MEDIA_FILE", "file") ?? "";
                                if (file.Length > 0)
                                {
                                    images.Add("https://image.umapi.ru/ARTICLES/" + file);
                                }
                            }
                        }

                        if (images.Count == 0)
                        {
                            var file = Text(root, "MEDIA_FILE") ?? "";
                            if (file.Length > 0)
                            {
                                images.Add("https://image.umapi.ru/ARTICLES/" + file);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                    }

                    if (images.Count >= 3)
                    {
                        break;
                    }
                }

                if (images.Count >= 3)
                {
                    break;
                }
            }
        }
        catch (Exception)
        {
        }

        return images.Distinct().Take(3).ToList();
    }

    /// <summary>True when the article payload's own id equals the requested id.</summary>
    private static bool PayloadArticleIdMatches(JsonElement payload, int articleId)
    {
        var root = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("data", out var data)
            ? data
            : payload;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in new[] { "ART_ID", "id", "ID" })
        {
            if (!root.TryGetProperty(name, out var el))
            {
                continue;
            }

            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n))
            {
                return n == articleId;
            }

            if (el.ValueKind == JsonValueKind.String
                && int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
            {
                return s == articleId;
            }
        }

        // No id field at all — trust the row rather than discard a good hit.
        return true;
    }

    /// <summary>Read a fetch-once UMAPI payload saved in the tenant cache.</summary>
    private static async Task<string?> ReadUmapiCacheAsync(
        ITenantDbConnectionFactory connections, HttpContext context, string cacheKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken)
                .ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `response_json` FROM `epc_umapi_cache` WHERE `cache_key` = @key LIMIT 1";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@key";
            parameter.Value = cacheKey;
            command.Parameters.Add(parameter);
            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Persist an upstream UMAPI payload so the next view is free.</summary>
    private static async Task WriteUmapiCacheAsync(
        ITenantDbConnectionFactory connections, HttpContext context, string cacheKey,
        string action, string section, string language, string region,
        string responseJson, int httpStatus, CancellationToken cancellationToken)
    {
        if (responseJson.Length == 0)
        {
            return;
        }

        try
        {
            var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            await using var connection = await connections.OpenForTenantAsync(tenant, cancellationToken)
                .ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                REPLACE INTO `epc_umapi_cache`
                (`cache_key`, `action`, `section`, `language`, `region`, `request_json`, `response_json`, `rows_count`, `http_status`, `last_sync`)
                VALUES (@key, @action, @section, @language, @region, '', @response, 0, @status, UNIX_TIMESTAMP())
                """;
            foreach (var (name, value) in new (string Name, object Value)[]
            {
                ("@key", cacheKey),
                ("@action", action),
                ("@section", section),
                ("@language", language),
                ("@region", region),
                ("@response", responseJson),
                ("@status", httpStatus),
            })
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Cache persistence is best-effort; the page already has the payload.
        }
    }
}
