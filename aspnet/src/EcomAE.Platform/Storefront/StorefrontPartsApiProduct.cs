using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// <c>/api/partsapi_product.php?brand=&amp;number=</c> — car-mod-style article page
/// backed by the shared partsapi.ru subscription: hero card (photo, brand, article
/// number, part name), criteria/spec table, "Suitable for" vehicle applicability,
/// and the crosses/analogs table. Every response goes through
/// <see cref="IPartsApiService"/> so each fetch is cached permanently
/// (fetch-once/save — repeat views cost nothing).
/// </summary>
public static class StorefrontPartsApiProduct
{
    public const string Path = "/api/partsapi_product.php";

    public static void Map(IEndpointRouteBuilder endpoints)
        => endpoints.MapGet(Path, HandleAsync).AllowAnonymous();

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IPartsApiService partsApi,
        CancellationToken cancellationToken)
    {
        var brand = context.Request.Query["brand"].ToString().Trim();
        var number = context.Request.Query["number"].ToString().Trim();
        if (brand.Length == 0 || number.Length == 0)
        {
            return Results.Text("brand and number are required", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status400BadRequest);
        }

        var lang = context.Request.Query["lang"].ToString().Trim() is { Length: > 0 } l ? l : "16";

        // Resolve the article id (needed for media/criteria/crosses).
        var artId = "";
        string? supId = null;
        var artName = "";
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

        calls.Add(("fitment", partsApi.ExecuteAsync(new PartsApiRequest("getApplicability",
            new Dictionary<string, string> { ["sku"] = number, ["brand"] = brand }), cancellationToken)));

        var results = new Dictionary<string, PartsApiResult>();
        foreach (var (key, task) in calls)
        {
            try { results[key] = await task.ConfigureAwait(false); }
            catch (Exception) { results[key] = new PartsApiResult(false, 502, ""); }
        }

        // Assemble the card.
        var images = new List<string>();
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

        var specs = new List<(string Name, string Value)>();
        foreach (var row in Rows(results.GetValueOrDefault("criteria")))
        {
            var n = Text(row, "CRITERIA_NAME", "CRITERIA", "criteria", "name", "CRITERIA_DESCRIPTION", "title") ?? "";
            var v = Text(row, "CRITERIA_VALUE", "criteriaValue", "value", "CRITERIA_DESCRIPTION_VALUE", "description") ?? "";
            if (n.Length > 0)
            {
                specs.Add((n, v));
            }
        }

        var vehicles = new List<string[]>();
        var fitmentRows = Rows(results.GetValueOrDefault("fitment"));
        if (!fitmentRows.Any())
        {
            // getApplicability needs the brand parameter some suppliers don't
            // publish; getApplicability2 keys on the number alone and returns
            // the full linked-vehicle list.
            try
            {
                results["fitment2"] = await partsApi.ExecuteAsync(new PartsApiRequest("getApplicability2",
                    new Dictionary<string, string> { ["sku"] = number }), cancellationToken).ConfigureAwait(false);
                fitmentRows = Rows(results["fitment2"]);
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

        var html = Render(brand, number, artName, images, specs, vehicles, crosses);
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

    private static string E(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    private static string Render(string brand, string number, string name,
        List<string> images, List<(string Name, string Value)> specs,
        List<string[]> vehicles, List<string[]> crosses)
    {
        var sb = new StringBuilder(16000);
        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        sb.Append("<title>").Append(E(number)).Append(" — ").Append(E(brand)).Append(" — AUTO247</title>");
        sb.Append("<style>");
        sb.Append("body{margin:0;font:14px/1.5 Arial,Helvetica,sans-serif;background:#f4f6f8;color:#222}");
        sb.Append(".wrap{max-width:1060px;margin:24px auto;padding:0 16px}");
        sb.Append(".card{background:#fff;border-radius:10px;box-shadow:0 1px 4px rgba(0,0,0,.08);padding:22px;display:flex;gap:26px;flex-wrap:wrap}");
        sb.Append(".pic{width:280px;height:280px;object-fit:contain;border:1px solid #eee;border-radius:8px;background:#fff}");
        sb.Append(".picph{width:280px;height:280px;display:flex;align-items:center;justify-content:center;color:#aaa;border:1px dashed #ddd;border-radius:8px}");
        sb.Append("h1{font-size:24px;margin:0 0 4px}.brand{color:#0b7ed7;font-weight:600}.name{color:#555;margin:2px 0 14px}");
        sb.Append("h2{font-size:18px;margin:28px 0 12px;border-left:4px solid #0b7ed7;padding-left:10px}");
        sb.Append("table{width:100%;border-collapse:collapse;background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,.08)}");
        sb.Append("th{background:#0b7ed7;color:#fff;text-align:left;padding:10px 14px;font-weight:600}");
        sb.Append("td{padding:9px 14px;border-bottom:1px solid #f0f0f0}");
        sb.Append("tr:last-child td{border-bottom:0}tr:nth-child(even) td{background:#fafbfc}");
        sb.Append(".thumbs{display:flex;gap:8px;margin-top:10px;flex-wrap:wrap}.thumbs img{width:64px;height:64px;object-fit:contain;border:1px solid #eee;border-radius:6px}");
        sb.Append(".empty{color:#888;padding:14px;background:#fff;border-radius:10px}");
        sb.Append("</style></head><body><div class=\"wrap\">");

        sb.Append("<div class=\"card\">");
        if (images.Count > 0)
        {
            sb.Append("<div><img class=\"pic\" src=\"").Append(E(images[0])).Append("\" alt=\"").Append(E(number)).Append("\">");
            if (images.Count > 1)
            {
                sb.Append("<div class=\"thumbs\">");
                foreach (var img in images.Skip(1).Take(8))
                {
                    sb.Append("<img src=\"").Append(E(img)).Append("\" alt=\"\">");
                }

                sb.Append("</div>");
            }

            sb.Append("</div>");
        }
        else
        {
            sb.Append("<div class=\"picph\">No photo yet</div>");
        }

        sb.Append("<div><h1>").Append(E(number)).Append("</h1>");
        sb.Append("<div class=\"brand\">").Append(E(brand)).Append("</div>");
        if (name.Length > 0)
        {
            sb.Append("<div class=\"name\">").Append(E(name)).Append("</div>");
        }

        sb.Append("</div></div>");

        sb.Append("<h2>Specifications</h2>");
        if (specs.Count > 0)
        {
            sb.Append("<table><tr><th>Criteria</th><th>Value</th></tr>");
            foreach (var (n, v) in specs)
            {
                sb.Append("<tr><td>").Append(E(n)).Append("</td><td>").Append(E(v)).Append("</td></tr>");
            }

            sb.Append("</table>");
        }
        else
        {
            sb.Append("<div class=\"empty\">Specification data is loading on first request.</div>");
        }

        sb.Append("<h2>Suitable for</h2>");
        if (vehicles.Count > 0)
        {
            sb.Append("<table><tr><th>Make</th><th>Model</th><th>Modification / Engine</th><th>From</th><th>To</th></tr>");
            foreach (var v in vehicles)
            {
                sb.Append("<tr><td>").Append(E(v[0])).Append("</td><td>").Append(E(v[1]))
                    .Append("</td><td>").Append(E(v[2])).Append("</td><td>").Append(E(v[3]))
                    .Append("</td><td>").Append(E(v[4])).Append("</td></tr>");
            }

            sb.Append("</table>");
        }
        else
        {
            sb.Append("<div class=\"empty\">Vehicle applicability is loading on first request.</div>");
        }

        sb.Append("<h2>Crosses / Analogs</h2>");
        if (crosses.Count > 0)
        {
            sb.Append("<table><tr><th>Brand</th><th>Number</th><th>Name</th></tr>");
            foreach (var c in crosses)
            {
                sb.Append("<tr><td>").Append(E(c[0])).Append("</td><td>").Append(E(c[1])).Append("</td><td>").Append(E(c[2])).Append("</td></tr>");
            }

            sb.Append("</table>");
        }
        else
        {
            sb.Append("<div class=\"empty\">Cross reference data is loading on first request.</div>");
        }

        sb.Append("</div></body></html>");
        return sb.ToString();
    }
}
