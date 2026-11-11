using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// <c>/api/emsh_proxy.php</c> — UMAPI-shaped catalog drill backed by tecdoc.emshcloud.com
/// (the "auto247-Tur" catalog widget). Same action contract the UMAPI catalog widget
/// already speaks so the rendered widget is a straight swap of the proxy path:
/// manufacturers → arac-marka, models → arac-model, modifications → arac-motor,
/// categories → tecdoc-kategori, products → arac-parca (grouped by urun_tipi),
/// articles → cached arac-parca rows filtered to the group (zero extra requests).
/// Every emshcloud response is cached forever in <c>epc_emsh_cache</c> — the
/// subscription is per-request, so no request may be repeated.
/// </summary>
public static class EmshcloudCatalogProxy
{
    public const string Path = "/api/emsh_proxy.php";

    private const string BaseUrl = "https://tecdoc.emshcloud.com/v1/";
    private const string Tip = "PassengerCar";

    private static bool _tableEnsured;

    public static void Map(IEndpointRouteBuilder endpoints)
        => endpoints.MapMethods(Path, ["GET"], HandleAsync);

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        ITenantDbConnectionFactory connections,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        var action = context.Request.Query["action"].ToString().Trim().ToLowerInvariant();
        if (action is not ("manufacturers" or "models" or "modifications" or "categories" or "catparts" or "products" or "articles" or "brands" or "partbrands" or "vin" or "img" or "status"))
        {
            return Json(400, HomeCatalogWidgets.UmapiUnknownJson());
        }

        var tenant = context.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        System.Data.Common.DbConnection? connection = null;
        if (connections.IsConfigured)
        {
            try
            {
                connection = await connections.OpenForTenantAsync(tenant, cancellationToken).ConfigureAwait(false);
                await EnsureTableAsync(connection, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                connection?.Dispose();
                connection = null;
            }
        }

        try
        {
            if (action == "img")
            {
                return await ServeImageAsync(context, connection, httpClientFactory, cancellationToken).ConfigureAwait(false);
            }

            if (action == "articles")
            {
                // Served entirely from the products cache — no request spent.
                var fromCache = connection is null
                    ? null
                    : await ArticlesFromCacheAsync(connection, context, cancellationToken).ConfigureAwait(false);
                return Json(200, fromCache ?? "{\"data\":[]}");
            }

            var cacheKey = CacheKey(context, action);
            string? body = null;
            if (connection is not null && cacheKey is not null
                && !string.Equals(context.Request.Query["refresh"], "1", StringComparison.Ordinal))
            {
                body = await CacheGetAsync(connection, cacheKey, cancellationToken).ConfigureAwait(false);
            }

            if (body is null)
            {
                var fetched = await FetchAsync(context, httpClientFactory, action, cancellationToken).ConfigureAwait(false);
                body = fetched ?? (action == "categories" ? "{\"root\":[]}" : (action is "products" or "catparts" ? "{\"groups\":[],\"parts\":[]}" : "[]"));
                if (fetched is not null && connection is not null && cacheKey is not null)
                {
                    try
                    {
                        await CachePutAsync(connection, cacheKey, body, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
            }

            // products caches {groups,parts}; the widget only wants the group list.
            if (action == "products")
            {
                body = GroupsOnly(body);
            }

            return Json(200, body);
        }
        finally
        {
            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static string? CacheKey(HttpContext context, string action)
        => action switch
        {
            "manufacturers" => "emsh|marka|" + Section(context),
            "models" => "emsh|model|" + context.Request.Query["MFA_ID"].ToString(),
            "modifications" => "emsh|motor|" + context.Request.Query["MS_ID"].ToString(),
            "categories" => "emsh|kat|" + VehicleTip(context),
            "products" => "emsh|pg|" + context.Request.Query["ID"].ToString() + "|" + context.Request.Query["CATEGORY_ID"].ToString(),
            "vin" => "emsh|vin|" + context.Request.Query["vin"].ToString().Trim().ToUpperInvariant(),
            "brands" => "emsh|oem|" + NormalizeArticle(context.Request.Query["article"]),
            "partbrands" => "emsh|pmarka|all",
            "catparts" => "emsh|kp|" + context.Request.Query["CATEGORY_ID"].ToString(),
            _ => null,
        };

    private static string Section(HttpContext context)
    {
        var s = context.Request.Query["section"].ToString().Trim().ToLowerInvariant();
        return s is "commercial" or "motorbike" ? s : "passenger";
    }

    /// <summary>Composite PT_ID "<cat>:<pt>" so the articles call can find its cached group without the category param.</summary>
    private static string CompositePt(string categoryId, long ptId) => categoryId + ":" + ptId.ToString(CultureInfo.InvariantCulture);

    private static string GroupsOnly(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("groups", out var groups))
            {
                return groups.GetRawText();
            }
        }
        catch (JsonException)
        {
        }
        return "[]";
    }

    /// <summary>articles: rows of one product group read back from the products cache — zero requests spent.</summary>
    private static async Task<string?> ArticlesFromCacheAsync(
        System.Data.Common.DbConnection connection, HttpContext context, CancellationToken cancellationToken)
    {
        var ktype = context.Request.Query["ID"].ToString();
        var wanted = context.Request.Query["PT_IDS"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        if (wanted.Count == 0 || ktype.Length == 0)
        {
            return null;
        }

        // PT_ID is "<cat>:<pt>" — look up each referenced category cache entry once.
        var cats = wanted
            .Select(w => w.Split(':', 2) is { Length: 2 } s ? s[0] : null)
            .Where(c => c is { Length: > 0 })!
            .Distinct(StringComparer.Ordinal);

        var rows = new List<JsonElement>();
        foreach (var cat in cats)
        {
            var body = await CacheGetAsync(connection, "emsh|pg|" + ktype + "|" + cat, cancellationToken).ConfigureAwait(false);
            if (body is null)
            {
                continue;
            }
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in parts.EnumerateArray())
                    {
                        rows.Add(p.Clone());
                    }
                }
            }
            catch (JsonException)
            {
            }
        }

        var data = rows
            .Where(r => r.TryGetProperty("PT_ID", out var id) && wanted.Contains(id.GetString() ?? ""))
            .Select(r =>
            {
                var row = new Dictionary<string, object?>();
                foreach (var prop in r.EnumerateObject())
                {
                    row[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? (object?)prop.Value.GetString()
                        : prop.Value.Clone();
                }
                return row;
            }).ToList();
        return data.Count == 0 ? null : JsonSerializer.Serialize(new Dictionary<string, object?> { ["data"] = data });
    }

    private static async Task<string?> FetchAsync(
        HttpContext context, IHttpClientFactory factory, string action, CancellationToken cancellationToken)
    {
        var key = await EmshcloudVinDecode.ApiKeyAsync(context, cancellationToken).ConfigureAwait(false);
        if (key.Length == 0)
        {
            return null;
        }

        var tip = VehicleTip(context);
        var article = NormalizeArticle(context.Request.Query["article"]);
        // brands: supplier article numbers live under `ara` (parca_no search);
        // OE/cross codes under `oem` — try ara first, fall back to oem.
        List<(string endpoint, string shape)> calls = action switch
        {
            "manufacturers" => new() { ("arac-marka?tip=" + tip, "marka") },
            "models" => new() { ("arac-model?marka=" + Uri.EscapeDataString(context.Request.Query["MFA_ID"].ToString()), "model") },
            "modifications" => new() { ("arac-motor?model=" + Uri.EscapeDataString(context.Request.Query["MS_ID"].ToString()), "motor") },
            "categories" => new() { ("tecdoc-kategori", "kat") },
            "products" => new() { ("arac-parca?tip=" + tip
                           + "&ktype=" + Uri.EscapeDataString(context.Request.Query["ID"].ToString())
                           + "&kategori_id=" + Uri.EscapeDataString(context.Request.Query["CATEGORY_ID"].ToString())
                           + "&limit=500", "parca") },
            "vin" => new() { ("vin?sasi=" + Uri.EscapeDataString(context.Request.Query["vin"].ToString().Trim().ToUpperInvariant()), "vin") },
            "brands" => new()
            {
                ("ara?kod=" + Uri.EscapeDataString(article), "oem"),
                ("oem?kod=" + Uri.EscapeDataString(article), "oem"),
            },
            // catparts: category → articles without a vehicle (TecDoc category catalog).
            "catparts" => new() { ("kategori-parca?kategori_id=" + Uri.EscapeDataString(context.Request.Query["CATEGORY_ID"].ToString()) + "&limit=500", "parca") },
            "partbrands" => new() { ("marka-liste", "pmarka") },
            _ => new(),
        };
        if (calls.Count == 0)
        {
            return null;
        }

        var client = factory.CreateClient();
        var swallowEndpointFailure = calls.Count > 1;
        foreach (var call in calls)
        {
            string raw;
            try
            {
                var sep = call.endpoint.Contains('?') ? "&" : "?";
                using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + call.endpoint + sep + "dil=en");
                request.Headers.TryAddWithoutValidation("X-API-Key", key);
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode || raw.Length == 0)
                {
                    continue;
                }
            }
            catch (Exception ex) when (swallowEndpointFailure && ex is not OperationCanceledException)
            {
                // A failed endpoint must not block the fallback lookup.
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var sonuc = doc.RootElement.TryGetProperty("sonuc", out var s) ? s : doc.RootElement;
                var mapped = call.shape switch
                {
                    "marka" => MapMarka(sonuc, Section(context)),
                    "model" => MapModel(sonuc),
                    "motor" => MapMotor(sonuc),
                    "kat" => MapKategori(sonuc, VehicleTip(context)),
                    "parca" => MapParca(sonuc, context.Request.Query["CATEGORY_ID"].ToString()),
                    "vin" => MapVin(sonuc),
                    "oem" => MapOem(sonuc),
                    "pmarka" => MapPartBrands(sonuc),
                    _ => null,
                };
                // An empty brands list falls through to the next lookup endpoint.
                if (mapped is not null && !(call.shape == "oem" && mapped == "[]"))
                {
                    return mapped;
                }
            }
            catch (JsonException)
            {
            }
        }
        return null;
    }

    /// <summary>emshcloud ara/oem brand lookup for an article number — the same
    /// fallback the article picker and AI Parts Expert use. Returns mapped brand
    /// rows [{BRAND,SUP_BRAND,SUP_ID}] or an empty list.</summary>
    public static async Task<IReadOnlyList<Dictionary<string, object?>>> ArticleBrandsAsync(
        HttpContext context,
        IHttpClientFactory factory,
        string article,
        CancellationToken cancellationToken)
    {
        var key = await EmshcloudVinDecode.ApiKeyAsync(context, cancellationToken).ConfigureAwait(false);
        if (key.Length == 0)
        {
            return [];
        }

        var client = factory.CreateClient();
        var brands = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var endpoint in new[] { "ara", "oem" })
        {
            if (brands.Count > 0)
            {
                break;
            }

            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    BaseUrl + endpoint + "?kod=" + Uri.EscapeDataString(article) + "&dil=en");
                request.Headers.TryAddWithoutValidation("X-API-Key", key);
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                var sonuc = doc.RootElement.TryGetProperty("sonuc", out var s) ? s : doc.RootElement;
                foreach (var r in Rows(sonuc))
                {
                    var name = Text(r, "marka_adi");
                    if (name.Length > 0 && !brands.ContainsKey(name))
                    {
                        brands[name] = Num(r, "marka_id") ?? 0;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
            }
        }

        return brands.Select(b => new Dictionary<string, object?>
        {
            ["BRAND"] = b.Key,
            ["SUP_BRAND"] = b.Key,
            ["SUP_ID"] = b.Value > 0 ? b.Value.ToString(CultureInfo.InvariantCulture) : b.Key,
        }).ToList();
    }

    /// <summary>Article numbers carry no symbols anywhere in the business: IXU01-27 → IXU0127.</summary>
    private static string NormalizeArticle(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        var chars = new char[value.Length];
        var n = 0;
        foreach (var c in value.Trim())
        {
            if (char.IsLetterOrDigit(c))
            {
                chars[n++] = char.ToUpperInvariant(c);
            }
        }
        return new string(chars, 0, n);
    }

    // ---- emshcloud → umapi-shape mappers ----

    /// <summary>emshcloud arac-* endpoints carry no vehicle-type split, so the section is
    /// reflected through EPART_TYPES/POPULAR_* while the tip param still goes upstream.</summary>
    private static string VehicleTip(HttpContext context)
    {
        var vt = context.Request.Query["vehicle_type"].ToString().Trim().ToUpperInvariant();
        if (vt is "CV" or "TRUCK" or "LCV")
        {
            return "CommercialVehicle";
        }
        if (vt is "MTB" or "MOTORCYCLE" or "MOTORBIKE" or "MOTO")
        {
            return "Motorbike";
        }
        return Section(context) switch
        {
            "commercial" => "CommercialVehicle",
            "motorbike" => "Motorbike",
            _ => Tip,
        };
    }

    /// <summary>Make logo URL served by our own img action (fetch-once, cached bytes).</summary>
    private static string? LogoUrl(JsonElement r, string kind, long id)
        => Text(r, "gorsel").Length > 0 || Text(r, "logo").Length > 0
            ? Path + "?action=img&kind=" + kind + "&id=" + id.ToString(CultureInfo.InvariantCulture)
            : null;

    private static string MapMarka(JsonElement sonuc, string section)
    {
        var epart = section switch
        {
            "commercial" => "CV",
            "motorbike" => "Motorcycle",
            _ => "PC",
        };
        var popularKey = section switch
        {
            "commercial" => "POPULAR_CV",
            "motorbike" => "POPULAR_MTB",
            _ => "POPULAR_PC",
        };

        var rows = new List<Dictionary<string, object?>>();
        foreach (var r in Rows(sonuc))
        {
            var id = Num(r, "id") ?? 0;
            var logo = LogoUrl(r, "marka", id);
            var row = new Dictionary<string, object?>
            {
                ["MFA_ID"] = id,
                ["MANUFACTURER"] = Text(r, "ad"),
                ["COUNTRY"] = "",
                ["IS_LOGO"] = logo is null ? 0 : 1,
                ["EPART_TYPES"] = new[] { epart },
            };
            row[popularKey] = (Num(r, "uyan_parca_sayisi") ?? 0) >= 2000 ? 1 : 0;
            if (logo is not null)
            {
                row["EPC_LOGO"] = logo;
            }
            rows.Add(row);
        }
        return JsonSerializer.Serialize(rows);
    }

    /// <summary>/v1/vin sonuc.araclar → umapi matchingVehicles shape the widget already reads.</summary>
    private static string MapVin(JsonElement sonuc)
    {
        if (sonuc.ValueKind != JsonValueKind.Object
            || !sonuc.TryGetProperty("araclar", out var araclar)
            || araclar.ValueKind != JsonValueKind.Array)
        {
            return "{\"data\":{\"matchingVehicles\":{\"array\":[]},\"matchingVehiclesCount\":0}}";
        }

        var vehicles = new List<Dictionary<string, object?>>();
        var manus = new Dictionary<long, string>();
        var models = new Dictionary<long, string>();
        foreach (var r in araclar.EnumerateArray())
        {
            var tip = Text(r, "arac_tip");
            var linkage = tip.StartsWith("Commercial", StringComparison.OrdinalIgnoreCase) ? "V"
                : (tip.StartsWith("Motor", StringComparison.OrdinalIgnoreCase) || tip.StartsWith("Moto", StringComparison.OrdinalIgnoreCase)) ? "M"
                : "P";
            var manuId = Num(r, "arac_marka_id") ?? 0;
            var modelId = Num(r, "model_id") ?? 0;
            var manuName = Text(r, "marka_ad");
            var modelName = Text(r, "model_tam_ad") is { Length: > 0 } t ? t : Text(r, "model_ad");
            vehicles.Add(new Dictionary<string, object?>
            {
                ["manuId"] = manuId,
                ["modelId"] = modelId,
                ["carId"] = Num(r, "ktype") ?? 0,
                ["carName"] = Text(r, "arac_adi"),
                ["vehicleTypeDescription"] = Text(r, "arac_adi"),
                ["linkageTargetType"] = linkage,
            });
            if (manuId > 0) manus.TryAdd(manuId, manuName);
            if (modelId > 0) models.TryAdd(modelId, modelName);
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["data"] = new Dictionary<string, object?>
            {
                ["matchingVehicles"] = new Dictionary<string, object?> { ["array"] = vehicles },
                ["matchingManufacturers"] = new Dictionary<string, object?> { ["array"] = manus.Select(m => new Dictionary<string, object?> { ["manuId"] = m.Key, ["manuName"] = m.Value }).ToList() },
                ["matchingModels"] = new Dictionary<string, object?> { ["array"] = models.Select(m => new Dictionary<string, object?> { ["modelId"] = m.Key, ["modelName"] = m.Value }).ToList() },
                ["matchingVehiclesCount"] = vehicles.Count,
            },
        });
    }

    /// <summary>Serves a vehicle make/model image: first hit fetches emshcloud once
    /// (arac-{kind}-gorsel), bytes cached forever in epc_image_cache — repeats cost nothing.</summary>
    private static async Task<IResult> ServeImageAsync(
        HttpContext context, System.Data.Common.DbConnection? connection,
        IHttpClientFactory factory, CancellationToken cancellationToken)
    {
        var kind = context.Request.Query["kind"].ToString().Trim().ToLowerInvariant() is "marka" or "model"
            ? context.Request.Query["kind"].ToString().Trim().ToLowerInvariant()
            : null;
        var id = context.Request.Query["id"].ToString();
        if (kind is null || UmapiImageProxy.PhpIntCast(id) < 1)
        {
            return Results.Text("Bad request", "text/plain; charset=utf-8", null, 400);
        }

        var cacheKey = System.Convert.ToHexString(
            System.Security.Cryptography.SHA1.HashData(
                System.Text.Encoding.UTF8.GetBytes("emsh-img:" + kind + ":" + id))).ToLowerInvariant();

        if (connection is not null)
        {
            try
            {
                await using (var ddl = connection.CreateCommand())
                {
                    ddl.CommandText =
                        "CREATE TABLE IF NOT EXISTS `epc_image_cache` (" +
                        "`cache_key` VARCHAR(64) NOT NULL, `content_type` VARCHAR(64) NOT NULL DEFAULT 'image/png', " +
                        "`body` MEDIUMBLOB NOT NULL, `last_sync` INT NOT NULL DEFAULT 0, PRIMARY KEY (`cache_key`)) " +
                        "ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
                    await ddl.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                await using (var read = connection.CreateCommand())
                {
                    read.CommandText = "SELECT `content_type`, `body` FROM `epc_image_cache` WHERE `cache_key` = @k LIMIT 1";
                    var p = read.CreateParameter(); p.ParameterName = "@k"; p.Value = cacheKey; read.Parameters.Add(p);
                    await using var rs = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    if (await rs.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        context.Response.Headers.CacheControl = "public, max-age=604800, immutable";
                        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                        return Results.File((byte[])rs.GetValue(1), rs.GetString(0));
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        var key = await EmshcloudVinDecode.ApiKeyAsync(context, cancellationToken).ConfigureAwait(false);
        if (key.Length == 0)
        {
            return Results.Text("Not found", "text/plain; charset=utf-8", null, 404);
        }

        try
        {
            var client = factory.CreateClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get, "https://tecdoc.emshcloud.com/v1/arac-" + kind + "-gorsel?id=" + Uri.EscapeDataString(id));
            request.Headers.TryAddWithoutValidation("X-API-Key", key);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("image/*"));
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/png";
            if (!response.IsSuccessStatusCode || bytes.Length < 64 || !contentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Text("Not found", "text/plain; charset=utf-8", null, 404);
            }

            if (connection is not null)
            {
                try
                {
                    await using var put = connection.CreateCommand();
                    put.CommandText =
                        "INSERT INTO `epc_image_cache` (`cache_key`, `content_type`, `body`, `last_sync`) VALUES (@k, @t, @b, UNIX_TIMESTAMP()) " +
                        "ON DUPLICATE KEY UPDATE `content_type` = VALUES(`content_type`), `body` = VALUES(`body`), `last_sync` = VALUES(`last_sync`)";
                    var k = put.CreateParameter(); k.ParameterName = "@k"; k.Value = cacheKey; put.Parameters.Add(k);
                    var t = put.CreateParameter(); t.ParameterName = "@t"; t.Value = contentType; put.Parameters.Add(t);
                    var b = put.CreateParameter(); b.ParameterName = "@b"; b.Value = bytes; put.Parameters.Add(b);
                    await put.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }

            context.Response.Headers.CacheControl = "public, max-age=604800, immutable";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(bytes, contentType);
        }
        catch (Exception)
        {
            return Results.Text("Not found", "text/plain; charset=utf-8", null, 404);
        }
    }

    private static string MapModel(JsonElement sonuc)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (var r in Rows(sonuc))
        {
            var msId = Num(r, "id") ?? 0;
            var row = new Dictionary<string, object?>
            {
                ["MS_ID"] = msId,
                ["MODEL_SERIES"] = Text(r, "tam_ad") is { Length: > 0 } tam ? tam : Text(r, "ad"),
                ["CI_FROM"] = Text(r, "uretim_bas"),
                ["CI_TO"] = Text(r, "uretim_bit"),
            };
            var logo = LogoUrl(r, "model", msId);
            if (logo is not null)
            {
                row["EPC_LOGO"] = logo;
            }
            rows.Add(row);
        }
        return JsonSerializer.Serialize(rows);
    }

    /// <summary>emshcloud returns a few field values in Turkish even with dil=en — normalize the
    /// recurring ones so the catalog stays English-only.</summary>
    private static string En(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }
        var v = value;
        foreach (var (tr, en) in Translations)
        {
            v = System.Text.RegularExpressions.Regex.Replace(
                v, "(?<![A-Za-zÇĞİÖŞÜçğıöşü])" + tr + "(?![A-Za-zÇĞİÖŞÜçğıöşü])", en,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return v;
    }

    private static readonly (string Tr, string En)[] Translations =
    [
        ("benzin/elektrikli", "Petrol/Electric"),
        ("dizel/elektrikli", "Diesel/Electric"),
        ("benzinli", "Petrol"),
        ("benzin", "Petrol"),
        ("dizel", "Diesel"),
        ("elektrikli", "Electric"),
        ("elektrik", "Electric"),
        ("hibrit", "Hybrid"),
        ("hidrojen", "Hydrogen"),
        ("dogalgaz", "CNG"),
        ("direksiyon", "Steering"),
        ("sol direksiyon", "Left-hand drive"),
        ("sag direksiyon", "Right-hand drive"),
        ("otomatik", "Automatic"),
        ("manuel", "Manual"),
        ("kombi", "Estate"),
        ("kasali", "Box"),
        ("damperli", "Tipper"),
        ("cift kabin", "Double cab"),
        ("tek kabin", "Single cab"),
        ("kapali kasa", "Box body"),
        ("acik kasa", "Platform/chassis"),
        ("yakit", "Fuel"),
    ];

    private static string MapMotor(JsonElement sonuc)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (var r in Rows(sonuc))
        {
            var cc = Num(r, "hacim_cc") ?? 0;
            var title = Text(r, "ad");
            var motor = Text(r, "motor_kodu");
            if (motor.Length > 0)
            {
                title += " " + motor;
            }
            var fuel = En(Text(r, "yakit"));
            rows.Add(new Dictionary<string, object?>
            {
                ["PC_ID"] = Num(r, "ktype"),
                ["PASSENGER_CAR"] = En(title),
                ["MODEL_SERIES"] = Text(r, "ad"),
                ["ENGINE_TYPE"] = fuel,
                ["FUEL_TYPE"] = fuel,
                ["POWER_KW"] = Num(r, "guc_kw") ?? 0,
                ["POWER_PS"] = Num(r, "guc_hp") ?? 0,
                ["CAPACITY_TECH"] = cc,
                ["CAPACITY_LT"] = cc > 0 ? Math.Round(cc / 1000m, 1) : 0,
                ["BODY_TYPE"] = En(Text(r, "kasa_tipi")),
                ["CI_FROM"] = Text(r, "uretim_bas"),
                ["CI_TO"] = Text(r, "uretim_bit"),
            });
        }
        return JsonSerializer.Serialize(rows);
    }

    /// <summary>oem?kod → the article-search "brands" list (deduped by supplier).</summary>
    private static string MapOem(JsonElement sonuc)
    {
        var brands = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in Rows(sonuc))
        {
            var name = Text(r, "marka_adi");
            if (name.Length > 0 && !brands.ContainsKey(name))
            {
                brands[name] = Num(r, "marka_id") ?? 0;
            }
        }
        return JsonSerializer.Serialize(brands.Select(b => new Dictionary<string, object?>
        {
            ["BRAND"] = b.Key,
            ["SUP_BRAND"] = b.Key,
            ["SUP_ID"] = b.Value > 0 ? b.Value.ToString(CultureInfo.InvariantCulture) : b.Key,
        }).ToList());
    }

    /// <summary>emshcloud marka-liste → [{SUP_ID,SUP_BRAND,PARTS,LOGO}] for the Tur brands page.
    /// The marka id is the TecDoc supplier id, the same space our supplier-logo proxy serves.</summary>
    private static string MapPartBrands(JsonElement sonuc)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (var r in Rows(sonuc))
        {
            var id = Num(r, "id") ?? 0;
            var name = Text(r, "ad");
            if (id <= 0 || name.Length == 0)
            {
                continue;
            }
            var row = new Dictionary<string, object?>
            {
                ["SUP_ID"] = id,
                ["SUP_BRAND"] = name,
                ["PARTS"] = Num(r, "parca_sayisi") ?? 0,
            };
            if (Text(r, "logo").Length > 0)
            {
                row["LOGO"] = "/api/umapi_image.php?kind=supplier&id=" + id.ToString(CultureInfo.InvariantCulture);
            }
            rows.Add(row);
        }
        return JsonSerializer.Serialize(rows.OrderBy(x => x["SUP_BRAND"]));
    }

    /// <summary>Flat {id,parent_id,ad} list → nested {root:[{CATEGORY_ID,DES,CHILD}]}.
    /// The upstream list mixes several trees by agac_tipi (PassengerCar, CommercialVehicle,
    /// Axle, Engine, Universal…) — only the vehicle type's own tree has parts via arac-parca.</summary>
    private static string MapKategori(JsonElement sonuc, string tip)
    {
        var flat = new List<(long Id, long Parent, string Des)>();
        foreach (var r in Rows(sonuc))
        {
            var tree = Text(r, "agac_tipi");
            if (tree.Length > 0 && !string.Equals(tree, tip, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            flat.Add((Num(r, "id") ?? 0, Num(r, "parent_id") ?? 0, Text(r, "ad")));
        }

        // A section whose own tree is absent (e.g. Motorbike) falls back to Universal.
        if (flat.Count == 0)
        {
            foreach (var r in Rows(sonuc))
            {
                if (!string.Equals(Text(r, "agac_tipi"), "Universal", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                flat.Add((Num(r, "id") ?? 0, Num(r, "parent_id") ?? 0, Text(r, "ad")));
            }
        }

        var byParent = flat.GroupBy(f => f.Parent).ToDictionary(g => g.Key, g => g.ToList());
        List<Dictionary<string, object?>> Build(long parent)
            => byParent.TryGetValue(parent, out var kids)
                ? kids.Select(k => new Dictionary<string, object?>
                {
                    ["CATEGORY_ID"] = k.Id,
                    ["DES"] = k.Des,
                    ["CHILD"] = Build(k.Id),
                }).ToList()
                : new List<Dictionary<string, object?>>();

        return JsonSerializer.Serialize(new Dictionary<string, object?> { ["root"] = Build(0) });
    }

    /// <summary>
    /// Parts of a vehicle+category → {groups:[{PT_ID:"cat:pt",PT_DES}], parts:[article rows]}.
    /// The widget shows groups; the articles call reads parts back from this same cached body.
    /// </summary>
    private static string MapParca(JsonElement sonuc, string categoryId)
    {
        var items = sonuc.ValueKind == JsonValueKind.Object && sonuc.TryGetProperty("parcalar", out var p)
            ? p
            : sonuc;

        var groups = new Dictionary<long, string>();
        var parts = new List<Dictionary<string, object?>>();
        foreach (var r in Rows(items))
        {
            var pt = Num(r, "urun_tipi_id") ?? 0;
            var des = Text(r, "urun_tipi");
            if (des.Length == 0)
            {
                des = Text(r, "ad");
            }
            if (pt > 0 && !groups.ContainsKey(pt))
            {
                groups[pt] = des;
            }

            parts.Add(new Dictionary<string, object?>
            {
                ["PT_ID"] = CompositePt(categoryId, pt),
                ["ID"] = Num(r, "id") ?? 0,
                ["ART_ID"] = Num(r, "id") ?? 0,
                ["ARTICLE_NR"] = Text(r, "parca_no"),
                ["ART_PRODUCT_NAME"] = Text(r, "ad"),
                ["SUP_BRAND"] = Text(r, "marka_adi"),
                // Brand name — UmapiImageProxy kind=supplier resolves names against epc_umapi_brands.
                ["SUP_ID"] = Text(r, "marka_adi"),
            });
        }

        var groupRows = groups.Select(g => new Dictionary<string, object?>
        {
            ["PT_ID"] = CompositePt(categoryId, g.Key),
            ["PT_DES"] = g.Value,
        }).ToList();

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["groups"] = groupRows,
            ["parts"] = parts,
        });
    }

    // ---- JSON helpers ----

    private static IEnumerable<JsonElement> Rows(JsonElement el)
        => el.ValueKind == JsonValueKind.Array ? el.EnumerateArray() : Enumerable.Empty<JsonElement>();

    private static string Text(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

    private static long? Num(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v))
        {
            return null;
        }
        return v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;
    }

    // ---- cache ----

    private static async Task EnsureTableAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        if (_tableEnsured)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS `epc_emsh_cache` (" +
            "`cache_key` VARCHAR(191) NOT NULL PRIMARY KEY," +
            "`response_json` LONGTEXT NULL," +
            "`last_sync` INT UNSIGNED NOT NULL DEFAULT 0" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        _tableEnsured = true;
    }

    private static async Task<string?> CacheGetAsync(System.Data.Common.DbConnection connection, string key, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `response_json` FROM `epc_emsh_cache` WHERE `cache_key` = @k LIMIT 1";
        var param = command.CreateParameter();
        param.ParameterName = "@k";
        param.Value = key;
        command.Parameters.Add(param);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is string s && s.Length > 0 ? s : null;
    }

    private static async Task CachePutAsync(System.Data.Common.DbConnection connection, string key, string body, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO `epc_emsh_cache` (`cache_key`, `response_json`, `last_sync`) VALUES (@k, @b, UNIX_TIMESTAMP()) " +
            "ON DUPLICATE KEY UPDATE `response_json` = VALUES(`response_json`), `last_sync` = VALUES(`last_sync`)";
        var k = command.CreateParameter();
        k.ParameterName = "@k";
        k.Value = key;
        command.Parameters.Add(k);
        var b = command.CreateParameter();
        b.ParameterName = "@b";
        b.Value = body;
        command.Parameters.Add(b);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IResult Json(int status, string body) => new EmshJsonResult(status, body);

    private sealed class EmshJsonResult(int status, string body) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = status;
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            httpContext.Response.Headers.CacheControl = "no-store";
            await httpContext.Response.WriteAsync(body).ConfigureAwait(false);
        }
    }
}
