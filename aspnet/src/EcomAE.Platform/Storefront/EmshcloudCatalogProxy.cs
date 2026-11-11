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
        if (action is not ("manufacturers" or "models" or "modifications" or "categories" or "products" or "articles" or "status"))
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
                body = fetched ?? (action == "categories" ? "{\"root\":[]}" : (action == "products" ? "{\"groups\":[],\"parts\":[]}" : "[]"));
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
            "manufacturers" => "emsh|marka",
            "models" => "emsh|model|" + context.Request.Query["MFA_ID"].ToString(),
            "modifications" => "emsh|motor|" + context.Request.Query["MS_ID"].ToString(),
            "categories" => "emsh|kat",
            "products" => "emsh|pg|" + context.Request.Query["ID"].ToString() + "|" + context.Request.Query["CATEGORY_ID"].ToString(),
            _ => null,
        };

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

        (string endpoint, string shape) call = action switch
        {
            "manufacturers" => ("arac-marka?tip=" + Tip, "marka"),
            "models" => ("arac-model?marka=" + Uri.EscapeDataString(context.Request.Query["MFA_ID"].ToString()), "model"),
            "modifications" => ("arac-motor?model=" + Uri.EscapeDataString(context.Request.Query["MS_ID"].ToString()), "motor"),
            "categories" => ("tecdoc-kategori", "kat"),
            "products" => ("arac-parca?tip=" + Tip
                           + "&ktype=" + Uri.EscapeDataString(context.Request.Query["ID"].ToString())
                           + "&kategori_id=" + Uri.EscapeDataString(context.Request.Query["CATEGORY_ID"].ToString())
                           + "&limit=500", "parca"),
            _ => ("", ""),
        };
        if (call.endpoint.Length == 0)
        {
            return null;
        }

        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + call.endpoint + "&dil=en");
        request.Headers.TryAddWithoutValidation("X-API-Key", key);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || raw.Length == 0)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var sonuc = doc.RootElement.TryGetProperty("sonuc", out var s) ? s : doc.RootElement;
            return call.shape switch
            {
                "marka" => MapMarka(sonuc),
                "model" => MapModel(sonuc),
                "motor" => MapMotor(sonuc),
                "kat" => MapKategori(sonuc),
                "parca" => MapParca(sonuc, context.Request.Query["CATEGORY_ID"].ToString()),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ---- emshcloud → umapi-shape mappers ----

    private static string MapMarka(JsonElement sonuc)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (var r in Rows(sonuc))
        {
            rows.Add(new Dictionary<string, object?>
            {
                ["MFA_ID"] = Num(r, "id"),
                ["MANUFACTURER"] = Text(r, "ad"),
                ["COUNTRY"] = "",
                ["IS_LOGO"] = 0,
            });
        }
        return JsonSerializer.Serialize(rows);
    }

    private static string MapModel(JsonElement sonuc)
    {
        var rows = new List<Dictionary<string, object?>>();
        foreach (var r in Rows(sonuc))
        {
            rows.Add(new Dictionary<string, object?>
            {
                ["MS_ID"] = Num(r, "id"),
                ["MODEL_SERIES"] = Text(r, "tam_ad") is { Length: > 0 } tam ? tam : Text(r, "ad"),
                ["CI_FROM"] = Text(r, "uretim_bas"),
                ["CI_TO"] = Text(r, "uretim_bit"),
            });
        }
        return JsonSerializer.Serialize(rows);
    }

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
            rows.Add(new Dictionary<string, object?>
            {
                ["PC_ID"] = Num(r, "ktype"),
                ["PASSENGER_CAR"] = title,
                ["MODEL_SERIES"] = Text(r, "ad"),
                ["ENGINE_TYPE"] = Text(r, "yakit"),
                ["FUEL_TYPE"] = Text(r, "yakit"),
                ["POWER_KW"] = Num(r, "guc_kw") ?? 0,
                ["POWER_PS"] = Num(r, "guc_hp") ?? 0,
                ["CAPACITY_TECH"] = cc,
                ["CAPACITY_LT"] = cc > 0 ? Math.Round(cc / 1000m, 1) : 0,
                ["BODY_TYPE"] = Text(r, "kasa_tipi"),
                ["CI_FROM"] = Text(r, "uretim_bas"),
                ["CI_TO"] = Text(r, "uretim_bit"),
            });
        }
        return JsonSerializer.Serialize(rows);
    }

    /// <summary>Flat {id,parent_id,ad} list → nested {root:[{CATEGORY_ID,DES,CHILD}]}.</summary>
    private static string MapKategori(JsonElement sonuc)
    {
        var flat = new List<(long Id, long Parent, string Des)>();
        foreach (var r in Rows(sonuc))
        {
            flat.Add((Num(r, "id") ?? 0, Num(r, "parent_id") ?? 0, Text(r, "ad")));
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
