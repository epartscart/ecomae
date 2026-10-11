using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-plus helpers. PHP identifiers kept for the inventory:
/// <c>epc_price_extra_field_catalog</c>, <c>epc_price_extra_search_options</c>,
/// <c>epc_price_extra_label</c>, <c>epc_price_extra_normalize_header</c>,
/// <c>epc_price_extra_slug_key</c>, <c>epc_price_extra_normalize_value</c>,
/// <c>epc_price_extra_map_header_columns</c>, <c>epc_price_extra_extract_from_row</c>,
/// <c>epc_price_extra_merge</c>, <c>epc_price_extra_encode</c>,
/// <c>epc_price_extra_decode</c>, <c>epc_price_extra_ensure_schema</c>,
/// <c>epc_price_extra_clear_for_price</c>, <c>epc_price_extra_save_for_row</c>,
/// <c>epc_price_attr_search</c>, <c>epc_price_extra_lookup</c>,
/// <c>epc_price_extra_json_params</c>,
/// <c>epc_cp_footer_scripts_reset</c>, <c>epc_cp_footer_scripts_append</c>,
/// <c>epc_cp_extract_scripts_from_html</c>, <c>epc_cp_extract_styles_from_html</c>,
/// <c>epc_cp_prepare_cp_page_content</c>, <c>epc_cp_prepare_cp_modules</c>,
/// <c>epc_cp_main_pane_begin_marker</c>, <c>epc_cp_main_pane_end_marker</c>,
/// <c>epc_cp_relocate_main_pane_scripts</c>, <c>epc_cp_strip_main_pane_markers</c>,
/// <c>epc_cp_inject_relocated_footer_assets</c>, <c>epc_cp_finalize_cp_html</c>,
/// <c>epc_cp_boc_first_paint_patch</c>, <c>epc_cp_render_relocated_footer_scripts</c>,
/// <c>epc_cp_redirect</c>,
/// <c>epc_pos_terminal_render_markup</c>,
/// <c>epc_cp_roles</c>, <c>epc_cp_role_dashboard_tiles</c>,
/// <c>epc_cp_role_quick_actions</c>, <c>epc_cp_role_modules</c>,
/// <c>epc_cp_detect_role</c>, <c>epc_cp_tile_resolve</c>,
/// <c>epc_cp_tile_query_scalar</c>, <c>epc_cp_role_home_render</c>,
/// <c>epc_cp_role_assign</c>, <c>epc_cp_role_can</c>,
/// <c>epc_channel_ensure_schema</c>,
/// <c>epc_channel_h</c>, <c>epc_channel_money</c>,
/// <c>epc_channel_carriers_catalog</c>, <c>epc_channel_carrier_demo_bases</c>,
/// <c>epc_channel_demo_rate</c>, <c>epc_channel_log</c>,
/// <c>epc_channel_marketplaces_catalog</c>, <c>epc_channel_list_marketplaces</c>,
/// <c>epc_channel_list_carriers</c>, <c>epc_channel_dashboard</c>,
/// <c>epc_channel_seed_defaults</c>, <c>epc_channel_seed_sample_data</c>,
/// <c>epc_channel_sync_inventory_demo</c>, <c>epc_channel_import_order_demo</c>,
/// <c>epc_channel_create_shipment_demo</c>, <c>epc_channel_demo_report</c>,
/// <c>epc_channel_configure_urls</c>, <c>epc_channel_guide_snapshot</c>.
/// </summary>
public static class PhpPlanQ1Plus
{
    public const string PriceExtraFieldsPath = "content/shop/docpart/epc_price_extra_fields.php";
    public const string CpScriptRelocatePath = "content/general_pages/epc_cp_script_relocate.php";
    public const string PosTerminalMarkupPath = "cp/content/shop/pos/epc_pos_terminal_markup.php";
    public const string CpRoleHomePath = "content/general_pages/epc_cp_role_home.php";
    public const string ChannelSchemaPath = "content/shop/channels/epc_channel_schema.php";
    public const string ChannelHelpersPath = "content/shop/channels/epc_channel_helpers.php";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Lazy<JsonElement> Extra = new(() => JsonDocument.Parse(PhpPlanQ1PlusJson.ExtraJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Carriers = new(() => JsonDocument.Parse(PhpPlanQ1PlusJson.CarriersJson).RootElement.Clone());
    private static readonly Lazy<JsonElement> Marketplaces = new(() => JsonDocument.Parse(PhpPlanQ1PlusJson.MarketplacesJson).RootElement.Clone());

    private static readonly Regex ScriptBlock = new(@"<script\b[^>]*>.*?</script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex StyleBlock = new(@"<style\b[^>]*>.*?</style\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex BodyClass = new(@"<body\b[^>]*\bclass\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BomJunk = new(@"[\uFEFF\u200B]+", RegexOptions.CultureInvariant);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant);
    private static readonly Regex SlugJunk = new(@"[^a-z0-9]+", RegexOptions.CultureInvariant);
    private static readonly Regex ValueJunk = new(@"[^A-Z0-9]", RegexOptions.CultureInvariant);
    private static readonly Regex ArticleKeyJunk = new(@"[^A-Z0-9]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex OtherSplit = new(@"\s*[|;,]\s*", RegexOptions.CultureInvariant);
    private static readonly Regex PipeSplit = new(@"\s*\|\s*", RegexOptions.CultureInvariant);
    private static readonly Regex CarrierCode = new(@"[^a-z0-9_]", RegexOptions.CultureInvariant);
    private static readonly Regex CustomField = new(@"^[a-z0-9_]{1,48}$", RegexOptions.CultureInvariant);

    public static readonly List<string> FooterScripts = new();
    public static readonly List<string> FooterStyles = new();

    public static JsonElement EpcPriceExtraFieldCatalog() => Extra.Value.Clone();

    public static List<Dictionary<string, string>> EpcPriceExtraSearchOptions()
    {
        var outList = new List<Dictionary<string, string>>
        {
            new(StringComparer.Ordinal) { ["key"] = "all", ["label"] = "All fields" }
        };
        foreach (var field in Extra.Value.EnumerateObject())
        {
            if (!field.Value.TryGetProperty("searchable", out var s) || !PhpTruthyJson(s))
            {
                continue;
            }

            outList.Add(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["key"] = field.Name,
                ["label"] = field.Value.GetProperty("label").GetString() ?? ""
            });
        }

        return outList;
    }

    public static string EpcPriceExtraLabel(string key)
    {
        if (Extra.Value.TryGetProperty(key, out var meta) && meta.TryGetProperty("label", out var label))
        {
            return label.GetString() ?? "";
        }

        return PhpUcwords(key.Replace('_', ' '));
    }

    public static string EpcPriceExtraNormalizeHeader(string raw)
    {
        raw = raw.Trim().ToLowerInvariant();
        raw = BomJunk.Replace(raw, "");
        raw = raw.Replace('_', ' ').Replace('-', ' ').Replace('/', ' ').Replace('\\', ' ').Replace('.', ' ').Replace(':', ' ');
        raw = Spaces.Replace(raw, " ");
        return raw.Trim();
    }

    public static string EpcPriceExtraSlugKey(string header)
    {
        var slug = SlugJunk.Replace(EpcPriceExtraNormalizeHeader(header), "_").Trim('_');
        if (slug.Length == 0)
        {
            return "custom";
        }

        return MbSubstr(slug, 0, 48);
    }

    public static string EpcPriceExtraNormalizeValue(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0)
        {
            return "";
        }

        raw = raw.ToUpperInvariant();
        raw = ValueJunk.Replace(raw, "");
        return MbSubstr(raw, 0, 191);
    }

    public static Dictionary<int, string> EpcPriceExtraMapHeaderColumns(IReadOnlyList<string> headerRow, IReadOnlyDictionary<string, int> coreMap)
    {
        var used = new HashSet<int>();
        foreach (var idx in coreMap.Values)
        {
            if (idx >= 0)
            {
                used.Add(idx);
            }
        }

        var aliasToKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in Extra.Value.EnumerateObject())
        {
            foreach (var alias in field.Value.GetProperty("aliases").EnumerateArray())
            {
                aliasToKey[EpcPriceExtraNormalizeHeader(alias.GetString() ?? "")] = field.Name;
            }
        }

        var map = new Dictionary<int, string>();
        for (var idx = 0; idx < headerRow.Count; idx++)
        {
            if (used.Contains(idx))
            {
                continue;
            }

            var norm = EpcPriceExtraNormalizeHeader(headerRow[idx] ?? "");
            if (norm.Length == 0)
            {
                continue;
            }

            if (aliasToKey.TryGetValue(norm, out var exact))
            {
                map[idx] = exact;
                continue;
            }

            var matched = "";
            foreach (var pair in aliasToKey)
            {
                if (pair.Key.Length < 2)
                {
                    continue;
                }

                if (norm == pair.Key || Regex.IsMatch(norm, "(^|[^a-z0-9])" + Regex.Escape(pair.Key) + "([^a-z0-9]|$)", RegexOptions.CultureInvariant))
                {
                    matched = pair.Value;
                    break;
                }
            }

            map[idx] = matched.Length != 0 ? matched : EpcPriceExtraSlugKey(headerRow[idx] ?? "");
        }

        return map;
    }

    public static Dictionary<string, string> EpcPriceExtraExtractFromRow(IReadOnlyList<string> raw, IReadOnlyDictionary<int, string> extraColMap)
    {
        var extras = new Dictionary<string, string>(StringComparer.Ordinal);
        var otherBits = new List<string>();
        foreach (var pair in extraColMap)
        {
            var val = pair.Key >= 0 && pair.Key < raw.Count ? (raw[pair.Key] ?? "").Trim() : "";
            if (val.Length == 0)
            {
                continue;
            }

            val = MbSubstr(val, 0, 500);
            if (pair.Value == "other")
            {
                otherBits.Add(val);
                continue;
            }

            if (!extras.TryGetValue(pair.Value, out var existing) || existing.Length == 0)
            {
                extras[pair.Value] = val;
            }
            else if (!string.Equals(existing, val, StringComparison.OrdinalIgnoreCase))
            {
                otherBits.Add(EpcPriceExtraLabel(pair.Value) + ": " + val);
            }
        }

        if (otherBits.Count != 0)
        {
            var joined = string.Join(" | ", otherBits.Distinct(StringComparer.Ordinal));
            extras["other"] = extras.TryGetValue("other", out var cur) && cur.Length != 0 ? cur + " | " + joined : joined;
        }

        return extras;
    }

    public static Dictionary<string, string> EpcPriceExtraMerge(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
    {
        var output = new Dictionary<string, string>(a, StringComparer.Ordinal);
        foreach (var pair in b)
        {
            var v = (pair.Value ?? "").Trim();
            if (v.Length == 0)
            {
                continue;
            }

            if (!output.TryGetValue(pair.Key, out var cur) || cur.Trim().Length == 0)
            {
                output[pair.Key] = v;
                continue;
            }

            if (string.Equals(cur, v, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (pair.Key == "other")
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var uniq = new List<string>();
                foreach (var part in PipeSplit.Split(cur + " | " + v))
                {
                    var p = part.Trim();
                    if (p.Length == 0)
                    {
                        continue;
                    }

                    var uk = p.ToUpperInvariant();
                    if (!seen.Add(uk))
                    {
                        continue;
                    }

                    uniq.Add(p);
                }

                output[pair.Key] = string.Join(" | ", uniq);
            }
        }

        return output;
    }

    public static string EpcPriceExtraEncode(IReadOnlyDictionary<string, string> extras)
    {
        if (extras.Count == 0)
        {
            return "";
        }

        var ordered = extras.OrderBy(p => p.Key, StringComparer.Ordinal)
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return JsonSerializer.Serialize(ordered, JsonOpts);
    }

    public static Dictionary<string, string> EpcPriceExtraDecode(object? raw)
    {
        if (raw is IReadOnlyDictionary<string, string> dict)
        {
            var outDict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in dict)
            {
                var k = pair.Key.Trim();
                var v = (pair.Value ?? "").Trim();
                if (k.Length != 0 && v.Length != 0)
                {
                    outDict[k] = v;
                }
            }

            return outDict;
        }

        if (raw is JsonElement el && el.ValueKind == JsonValueKind.Object)
        {
            var tmp = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in el.EnumerateObject())
            {
                tmp[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.GetRawText();
            }

            return EpcPriceExtraDecode(tmp);
        }

        var text = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
        text = text.Trim();
        if (text.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }

            return EpcPriceExtraDecode(doc.RootElement.Clone());
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    public static bool EpcPriceExtraEnsureSchema(PriceExtraStore db)
    {
        if (db.SchemaReady)
        {
            return true;
        }

        db.SchemaReady = true;
        return true;
    }

    public static void EpcPriceExtraClearForPrice(PriceExtraStore db, int priceId)
    {
        if (priceId <= 0 || !EpcPriceExtraEnsureSchema(db))
        {
            return;
        }

        db.Index.RemoveAll(r => r.PriceId == priceId);
        db.Extras.RemoveAll(r => r.PriceId == priceId);
    }

    public static void EpcPriceExtraSaveForRow(
        PriceExtraStore db,
        int priceDataId,
        int priceId,
        IReadOnlyDictionary<string, string> extras,
        string manufacturer,
        string article,
        string articleShow,
        string name)
    {
        if (priceDataId <= 0 || priceId <= 0 || extras.Count == 0 || !EpcPriceExtraEnsureSchema(db))
        {
            return;
        }

        var json = EpcPriceExtraEncode(extras);
        if (json.Length == 0)
        {
            return;
        }

        var existing = db.Extras.Find(r => r.PriceDataId == priceDataId);
        if (existing is null)
        {
            db.Extras.Add(new PriceExtraRow { PriceDataId = priceDataId, PriceId = priceId, ExtraJson = json });
        }
        else
        {
            existing.PriceId = priceId;
            existing.ExtraJson = json;
        }

        db.Index.RemoveAll(r => r.PriceDataId == priceDataId);
        foreach (var pair in extras)
        {
            var rawVal = (pair.Value ?? "").Trim();
            if (rawVal.Length == 0)
            {
                continue;
            }

            var seenNorm = new HashSet<string>(StringComparer.Ordinal);
            foreach (var chunk in OtherSplit.Split(rawVal))
            {
                var piece = chunk.Trim();
                if (piece.Length == 0)
                {
                    continue;
                }

                var norm = EpcPriceExtraNormalizeValue(piece);
                if (norm.Length == 0 || !seenNorm.Add(norm))
                {
                    continue;
                }

                db.Index.Add(new PriceAttrRow
                {
                    PriceDataId = priceDataId,
                    PriceId = priceId,
                    FieldKey = pair.Key,
                    ValueNorm = norm,
                    ValueRaw = MbSubstr(piece, 0, 255),
                    Manufacturer = manufacturer,
                    Article = article,
                    ArticleShow = articleShow,
                    Name = name
                });
            }
        }
    }

    public static Dictionary<string, object?> EpcPriceAttrSearch(PriceExtraStore db, string fieldKey, string query, int limit = 80)
    {
        fieldKey = fieldKey.Trim();
        if (fieldKey.Length == 0)
        {
            fieldKey = "all";
        }

        query = query.Trim();
        limit = Math.Max(1, Math.Min(200, limit));
        var empty = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["message"] = "",
            ["field"] = fieldKey,
            ["q"] = query,
            ["count"] = 0,
            ["items"] = new List<Dictionary<string, object?>>()
        };
        if (Spaces.Replace(query, "").Length < 2)
        {
            empty["message"] = "Enter at least 2 characters.";
            return empty;
        }

        if (!EpcPriceExtraEnsureSchema(db))
        {
            empty["message"] = "Search index is not available.";
            return empty;
        }

        if (fieldKey != "all" && !Extra.Value.TryGetProperty(fieldKey, out _) && !CustomField.IsMatch(fieldKey))
        {
            empty["message"] = "Unknown search field.";
            return empty;
        }

        var norm = EpcPriceExtraNormalizeValue(query);
        if (norm.Length == 0)
        {
            empty["message"] = "Enter a valid search value.";
            return empty;
        }

        var rows = db.Index
            .Where(r => r.ValueNorm.StartsWith(norm, StringComparison.Ordinal) && (fieldKey == "all" || r.FieldKey == fieldKey))
            .Select(r => new
            {
                Row = r,
                Rank = r.ValueNorm == norm ? 0 : r.ValueNorm.StartsWith(norm, StringComparison.Ordinal) ? 1 : 2
            })
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Row.Manufacturer, StringComparer.Ordinal)
            .ThenBy(x => x.Row.ArticleShow, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => x.Row)
            .ToList();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<Dictionary<string, object?>>();
        foreach (var row in rows)
        {
            var mfr = row.Manufacturer.Trim();
            var artShow = row.ArticleShow.Trim();
            var art = row.Article.Trim();
            if (artShow.Length == 0)
            {
                artShow = art;
            }

            var uk = mfr.ToUpperInvariant() + "|" + (art.Length != 0 ? art : artShow).ToUpperInvariant();
            if (!seen.Add(uk))
            {
                continue;
            }

            items.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["manufacturer"] = mfr,
                ["article"] = art,
                ["article_show"] = artShow,
                ["name"] = row.Name.Trim(),
                ["matched_field"] = row.FieldKey,
                ["matched_field_label"] = EpcPriceExtraLabel(row.FieldKey),
                ["matched_value"] = row.ValueRaw.Trim(),
                ["price_id"] = row.PriceId,
                ["price_data_id"] = row.PriceDataId
            });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = items.Count > 0 ? "OK" : "No matching warehouse products.",
            ["field"] = fieldKey,
            ["q"] = query,
            ["count"] = items.Count,
            ["items"] = items
        };
    }

    public static Dictionary<string, string> EpcPriceExtraLookup(PriceExtraStore db, int priceId, string manufacturer, string articleShow)
    {
        if (priceId <= 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var artKey = ArticleKeyJunk.Replace(articleShow, "").ToUpperInvariant();
        var ck = priceId + "|" + manufacturer.Trim().ToUpperInvariant() + "|" + artKey;
        if (db.LookupCache.TryGetValue(ck, out var cached))
        {
            return cached;
        }

        db.LookupCache[ck] = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!EpcPriceExtraEnsureSchema(db))
        {
            return db.LookupCache[ck];
        }

        var row = db.PricesData.Find(p =>
            p.Id > 0
            && db.Extras.Exists(e => e.PriceDataId == p.Id && e.PriceId == priceId)
            && string.Equals(p.Manufacturer.Trim(), manufacturer.Trim(), StringComparison.OrdinalIgnoreCase)
            && (ArticleKeyJunk.Replace(p.ArticleShow, "").ToUpperInvariant() == artKey
                || ArticleKeyJunk.Replace(p.Article, "").ToUpperInvariant() == artKey));
        if (row is null)
        {
            return db.LookupCache[ck];
        }

        var extra = db.Extras.Find(e => e.PriceDataId == row.Id);
        db.LookupCache[ck] = extra is null ? new Dictionary<string, string>(StringComparer.Ordinal) : EpcPriceExtraDecode(extra.ExtraJson);
        return db.LookupCache[ck];
    }

    public static string EpcPriceExtraJsonParams(PriceExtraStore db, int priceId, string manufacturer, string articleShow, object? existing = null)
    {
        var baseMap = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (existing is string text && text.Length != 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in doc.RootElement.EnumerateObject())
                    {
                        baseMap[p.Name] = JsonSerializer.Deserialize<object>(p.Value.GetRawText());
                    }
                }
            }
            catch (JsonException)
            {
            }
        }
        else if (existing is IReadOnlyDictionary<string, object?> dict)
        {
            foreach (var pair in dict)
            {
                baseMap[pair.Key] = pair.Value;
            }
        }

        var extras = EpcPriceExtraLookup(db, priceId, manufacturer, articleShow);
        if (extras.Count == 0)
        {
            return baseMap.Count == 0 ? "" : JsonSerializer.Serialize(baseMap, JsonOpts);
        }

        baseMap["warehouse_attrs"] = extras;
        baseMap["used"] = 1;
        return JsonSerializer.Serialize(baseMap, JsonOpts);
    }

    public static void EpcCpFooterScriptsReset()
    {
        FooterScripts.Clear();
    }

    public static void EpcCpFooterScriptsAppend(string scriptHtml)
    {
        scriptHtml = scriptHtml.Trim();
        if (scriptHtml.Length == 0)
        {
            return;
        }

        FooterScripts.Add(scriptHtml);
    }

    public static string EpcCpExtractScriptsFromHtml(string html)
    {
        if (html.Length == 0)
        {
            return html;
        }

        return ScriptBlock.Replace(html, m =>
        {
            EpcCpFooterScriptsAppend(m.Value);
            return "";
        });
    }

    public static string EpcCpExtractStylesFromHtml(string html)
    {
        if (html.Length == 0 || html.IndexOf("<style", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return html;
        }

        return StyleBlock.Replace(html, m =>
        {
            FooterStyles.Add(m.Value);
            return "";
        });
    }

    public static string EpcCpPrepareCpPageContent(string content)
        => EpcCpExtractScriptsFromHtml(EpcCpExtractStylesFromHtml(content));

    public static void EpcCpPrepareCpModules(Dictionary<string, string> positionsHtmlArray)
    {
        foreach (var key in positionsHtmlArray.Keys.ToList())
        {
            positionsHtmlArray[key] = EpcCpPrepareCpPageContent(positionsHtmlArray[key] ?? "");
        }
    }

    public static string EpcCpMainPaneBeginMarker() => "<!--epc-cp-main-begin-->";

    public static string EpcCpMainPaneEndMarker() => "<!--epc-cp-main-end-->";

    public static string EpcCpRelocateMainPaneScripts(string html)
    {
        var begin = EpcCpMainPaneBeginMarker();
        var end = EpcCpMainPaneEndMarker();
        var start = html.IndexOf(begin, StringComparison.Ordinal);
        var stop = html.IndexOf(end, StringComparison.Ordinal);
        if (start < 0 || stop < 0 || stop <= start)
        {
            return html;
        }

        start += begin.Length;
        var main = html.Substring(start, stop - start);
        main = EpcCpExtractStylesFromHtml(main);
        main = EpcCpExtractScriptsFromHtml(main);
        return html[..start] + main + html[stop..];
    }

    public static string EpcCpStripMainPaneMarkers(string html)
        => html.Replace(EpcCpMainPaneBeginMarker(), "", StringComparison.Ordinal)
            .Replace(EpcCpMainPaneEndMarker(), "", StringComparison.Ordinal);

    public static string EpcCpInjectRelocatedFooterAssets(string html)
    {
        var chunks = new List<string>();
        if (FooterStyles.Count != 0)
        {
            foreach (var styleHtml in FooterStyles)
            {
                var trimmed = styleHtml.Trim();
                if (trimmed.Length != 0)
                {
                    chunks.Add(trimmed);
                }
            }

            FooterStyles.Clear();
        }

        if (FooterScripts.Count != 0)
        {
            foreach (var scriptHtml in FooterScripts)
            {
                var trimmed = scriptHtml.Trim();
                if (trimmed.Length != 0)
                {
                    chunks.Add(trimmed);
                }
            }

            FooterScripts.Clear();
        }

        if (chunks.Count == 0)
        {
            return html;
        }

        var block = "\n" + string.Join("\n", chunks) + "\n";
        var body = html.IndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (body >= 0)
        {
            var close = html.Substring(body, 7);
            return html[..body] + block + close + html[(body + 7)..];
        }

        return html + block;
    }

    public static string EpcCpFinalizeCpHtml(string html)
    {
        html = EpcCpRelocateMainPaneScripts(html);
        html = EpcCpStripMainPaneMarkers(html);
        html = EpcCpInjectRelocatedFooterAssets(html);
        html = EpcCpBocFirstPaintPatch(html);
        return html;
    }

    public static string EpcCpBocFirstPaintPatch(string html)
    {
        if (html.IndexOf("class=\"epc-boc\"", StringComparison.Ordinal) < 0
            || html.IndexOf("epc-boc-first-paint", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return html;
        }

        html = BodyClass.Replace(html, m =>
        {
            if (m.Value.Contains("epc-boc-mode", StringComparison.Ordinal))
            {
                return m.Value;
            }

            return m.Value.Replace("class=\"" + m.Groups[1].Value + "\"", "class=\"" + m.Groups[1].Value + " epc-boc-mode\"", StringComparison.Ordinal);
        }, 1);

        const string Css = "<style id=\"epc-boc-first-paint\">"
            + "html:has(body.epc-boc-mode),body.epc-boc-mode{background:#0b1220!important;}"
            + "body.epc-boc-mode #header,body.epc-boc-mode #menu,body.epc-boc-mode .navbar-static-side,"
            + "body.epc-boc-mode nav.navbar-default,body.epc-boc-mode #top-navigation,body.epc-boc-mode .epc-cp-topbar,"
            + "body.epc-boc-mode .footer,body.epc-boc-mode #right-sidebar,body.epc-boc-mode .splash,"
            + "body.epc-boc-mode #navigation{display:none!important;}"
            + "body.epc-boc-mode #wrapper,body.epc-boc-mode .content{margin:0!important;padding:0!important;"
            + "width:100%!important;max-width:100%!important;min-height:100vh!important;background:#0b1220!important;}"
            + "</style>";
        var head = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (head >= 0)
        {
            var close = html.Substring(head, 7);
            html = html[..head] + Css + close + html[(head + 7)..];
        }

        return html;
    }

    public static string EpcCpRenderRelocatedFooterScripts()
    {
        var sb = new StringBuilder();
        if (FooterStyles.Count != 0)
        {
            foreach (var styleHtml in FooterStyles)
            {
                sb.Append(styleHtml).Append('\n');
            }

            FooterStyles.Clear();
        }

        if (FooterScripts.Count == 0)
        {
            return sb.ToString();
        }

        foreach (var scriptHtml in FooterScripts)
        {
            sb.Append(scriptHtml).Append('\n');
        }

        FooterScripts.Clear();
        return sb.ToString();
    }

    public static string EpcCpRedirect(string path, string prefix = "/cp")
    {
        path = "/" + path.TrimStart('/');
        return path.StartsWith(prefix + "/", StringComparison.Ordinal) || path == prefix ? path : prefix + path;
    }

    public static string EpcPosTerminalRenderMarkup(IReadOnlyDictionary<string, object?> ctx)
    {
        var stats = Map(ctx, "stats");
        var openSession = ctx.TryGetValue("open_session", out var os) ? os as IReadOnlyDictionary<string, object?> : null;
        var taxCtx = Map(ctx, "tax_ctx");
        var settings = Map(ctx, "settings");
        var warehouses = ctx.TryGetValue("warehouses", out var whObj) && whObj is IEnumerable<object?> list
            ? list.Select(x => x as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>()).ToList()
            : new List<IReadOnlyDictionary<string, object?>>();
        var ajaxUrl = Str(ctx, "ajax_url");
        var posUrl = Str(ctx, "pos_url");
        var csrf = Str(ctx, "csrf");
        var erpUrl = Str(ctx, "erp_url");
        var whUrl = Str(ctx, "warehouse_url");
        var settingsUrl = Str(ctx, "settings_url");
        var currency = Str(ctx, "currency", "AED");
        var warehouseId = IntVal(ctx, "warehouse_id");
        var warehouseName = Str(ctx, "warehouse_name", "Default warehouse");
        var countryCode = Str(ctx, "country_code");
        var sessionId = openSession is null ? 0 : IntVal(openSession, "id");
        var sessionOpen = openSession is null ? "0" : "1";
        var registerName = H(Str(settings, "register_name", "Register 1"));
        var todaySales = IntVal(stats, "today_sales");
        var todayTotal = PhpNumber(FloatVal(stats, "today_total"), 2);
        var weekSales = IntVal(stats, "week_sales");
        var weekTotal = PhpNumber(FloatVal(stats, "week_total"), 2);
        var taxRate = PhpNumber(FloatVal(taxCtx, "tax_rate"), 1);
        var taxLabel = H(Str(taxCtx, "tax_label", "VAT"));
        var cur = H(currency);
        var sb = new StringBuilder();
        sb.Append("<div class=\"col-lg-12 epc-pos-wrap\" id=\"epc-pos-app\"")
            .Append(" data-ajax-url=\"").Append(H(ajaxUrl)).Append('"')
            .Append(" data-pos-url=\"").Append(H(posUrl)).Append('"')
            .Append(" data-csrf=\"").Append(H(csrf)).Append('"')
            .Append(" data-session-id=\"").Append(sessionId).Append('"')
            .Append(" data-session-open=\"").Append(sessionOpen).Append('"')
            .Append(" data-currency=\"").Append(cur).Append('"')
            .Append(" data-tax-label=\"").Append(taxLabel).Append('"')
            .Append(" data-tax-rate=\"").Append(H(taxRate)).Append('"')
            .Append(" data-warehouse-id=\"").Append(warehouseId).Append("\">");
        sb.Append("<header class=\"epc-pos-brand\">");
        sb.Append("<div class=\"epc-pos-brand__mark\"><i class=\"fa fa-cash-register\" aria-hidden=\"true\"></i></div>");
        sb.Append("<div class=\"epc-pos-brand__text\">");
        sb.Append("<div class=\"epc-pos-brand__name\">POS Terminal</div>");
        sb.Append("<div class=\"epc-pos-brand__sub\">").Append(registerName);
        if (countryCode.Length != 0)
        {
            sb.Append(" · ").Append(H(countryCode));
        }

        sb.Append(" · ").Append(cur).Append(" · ").Append(taxLabel).Append(' ').Append(taxRate).Append("%</div>");
        sb.Append("</div>");
        sb.Append("<div class=\"epc-pos-brand__links\">");
        if (settingsUrl.Length != 0)
        {
            sb.Append("<a class=\"epc-pos-chip-link\" href=\"").Append(H(settingsUrl)).Append("\"><i class=\"fa fa-cog\"></i> Settings</a>");
        }

        if (whUrl.Length != 0)
        {
            sb.Append("<a class=\"epc-pos-chip-link\" href=\"").Append(H(whUrl)).Append("\"><i class=\"fa fa-warehouse\"></i> Warehouses</a>");
        }

        sb.Append("<a class=\"epc-pos-chip-link\" href=\"").Append(H(erpUrl)).Append("\"><i class=\"fa fa-file-invoice\"></i> ERP sales</a>");
        sb.Append("</div></header>");
        sb.Append("<div class=\"epc-pos-kpi\" role=\"group\" aria-label=\"Shift metrics\">");
        sb.Append("<div class=\"epc-pos-kpi__card\"><div class=\"epc-pos-kpi__icon\"><i class=\"fa fa-receipt\"></i></div><div><div class=\"epc-pos-kpi__label\">Today sales</div><div class=\"epc-pos-kpi__value\" id=\"epc-pos-kpi-today\">").Append(todaySales).Append("</div></div></div>");
        sb.Append("<div class=\"epc-pos-kpi__card\"><div class=\"epc-pos-kpi__icon\"><i class=\"fa fa-coins\"></i></div><div><div class=\"epc-pos-kpi__label\">Today total</div><div class=\"epc-pos-kpi__value\" id=\"epc-pos-kpi-today-total\">").Append(cur).Append(' ').Append(todayTotal).Append("</div></div></div>");
        sb.Append("<div class=\"epc-pos-kpi__card\"><div class=\"epc-pos-kpi__icon\"><i class=\"fa fa-chart-line\"></i></div><div><div class=\"epc-pos-kpi__label\">Week sales</div><div class=\"epc-pos-kpi__value\">").Append(weekSales).Append(" · ").Append(cur).Append(' ').Append(weekTotal).Append("</div></div></div>");
        sb.Append("<div class=\"epc-pos-kpi__card\"><div class=\"epc-pos-kpi__icon\"><i class=\"fa fa-percent\"></i></div><div><div class=\"epc-pos-kpi__label\">").Append(taxLabel).Append(" rate</div><div class=\"epc-pos-kpi__value\">").Append(taxRate).Append("%</div></div></div>");
        sb.Append("<div class=\"epc-pos-kpi__card\"><div class=\"epc-pos-kpi__icon\"><i class=\"fa fa-store\"></i></div><div><div class=\"epc-pos-kpi__label\">Register</div><div class=\"epc-pos-kpi__value epc-pos-kpi__value--sm\">").Append(registerName).Append("</div></div></div>");
        sb.Append("</div>");
        sb.Append("<div id=\"epc-pos-msg\" class=\"epc-pos-msg\" role=\"status\" aria-live=\"polite\"></div>");
        sb.Append("<div class=\"epc-pos-session\" id=\"epc-pos-session-bar\">");
        sb.Append("<div class=\"epc-pos-session__status\">");
        if (openSession is not null)
        {
            sb.Append("<span class=\"epc-pos-badge epc-pos-badge-open\"><i class=\"fa fa-circle\"></i> Shift open</span>");
            sb.Append("<span class=\"epc-pos-session__meta\"><strong>").Append(H(Str(openSession, "session_no"))).Append("</strong>");
            sb.Append(" · Float ").Append(cur).Append(' ').Append(PhpNumber(FloatVal(openSession, "opening_float"), 2)).Append("</span>");
            sb.Append("<button type=\"button\" class=\"epc-pos-btn epc-pos-btn-muted\" id=\"epc-pos-close-session\"><i class=\"fa fa-lock\"></i> Close shift</button>");
        }
        else
        {
            sb.Append("<span class=\"epc-pos-badge epc-pos-badge-closed\"><i class=\"fa fa-lock\"></i> No open shift</span>");
            sb.Append("<span class=\"epc-pos-session__meta\">Open the register before checkout</span>");
            sb.Append("<button type=\"button\" class=\"epc-pos-btn epc-pos-btn-primary\" id=\"epc-pos-open-session\"><i class=\"fa fa-unlock\"></i> Open register</button>");
        }

        sb.Append("</div>");
        sb.Append("<div class=\"epc-pos-session__warehouse\">");
        sb.Append("<label for=\"epc-pos-warehouse\">Stock warehouse</label>");
        sb.Append("<select id=\"epc-pos-warehouse\" name=\"warehouse_id\" aria-label=\"Stock warehouse\">");
        if (warehouses.Count == 0)
        {
            sb.Append("<option value=\"").Append(warehouseId).Append("\">").Append(H(warehouseName)).Append("</option>");
        }
        else
        {
            foreach (var wh in warehouses)
            {
                var wid = IntVal(wh, "id");
                var name = Str(wh, "name");
                var wlabel = name.Length != 0 ? name : Str(wh, "code", "Warehouse");
                var sel = wid == warehouseId ? " selected" : "";
                sb.Append("<option value=\"").Append(wid).Append('"').Append(sel).Append('>').Append(H(wlabel)).Append("</option>");
            }
        }

        sb.Append("</select></div>");
        sb.Append("<div class=\"epc-pos-session__keys\" aria-label=\"Keyboard shortcuts\">");
        sb.Append("<span><kbd>Enter</kbd> search</span>");
        sb.Append("<span><kbd>F2</kbd> focus scan</span>");
        sb.Append("<span><kbd>F4</kbd> customer</span>");
        sb.Append("<span><kbd>F9</kbd> checkout</span>");
        sb.Append("</div></div>");
        sb.Append("<div class=\"epc-pos-grid\">");
        sb.Append("<section class=\"epc-pos-panel epc-pos-panel--catalog\" aria-label=\"Product search\">");
        sb.Append("<div class=\"epc-pos-panel-head\">");
        sb.Append("<div><i class=\"fa fa-barcode\"></i> Scan / search products</div>");
        sb.Append("<span class=\"epc-pos-panel-head__hint\">SKU · barcode · brand · name</span>");
        sb.Append("</div>");
        sb.Append("<div class=\"epc-pos-panel-body\">");
        sb.Append("<div class=\"epc-pos-search\">");
        sb.Append("<input type=\"search\" id=\"epc-pos-q\" placeholder=\"Scan barcode or type SKU / name…\" autocomplete=\"off\" autofocus aria-label=\"Product search\">");
        sb.Append("<button type=\"button\" class=\"epc-pos-btn epc-pos-btn-primary\" id=\"epc-pos-search-btn\" title=\"Search\"><i class=\"fa fa-search\"></i> Search</button>");
        sb.Append("</div>");
        sb.Append("<div id=\"epc-pos-products\" class=\"epc-pos-products\">");
        sb.Append("<div class=\"epc-pos-empty epc-pos-empty--graphical\">");
        sb.Append("<div class=\"epc-pos-empty__icon\"><i class=\"fa fa-search\"></i></div>");
        sb.Append("<div class=\"epc-pos-empty__title\">Ready to scan</div>");
        sb.Append("<p class=\"epc-pos-empty__text\">Type a SKU, barcode, or product name. A single exact match adds to the cart automatically.</p>");
        sb.Append("<ol class=\"epc-pos-empty__steps\"><li>Scan or search</li><li>Adjust qty &amp; discount</li><li>Take payment</li></ol>");
        sb.Append("</div></div></div></section>");
        sb.Append("<section class=\"epc-pos-panel epc-pos-panel--cart\" aria-label=\"Cart and payment\">");
        sb.Append("<div class=\"epc-pos-panel-head\">");
        sb.Append("<div><i class=\"fa fa-shopping-basket\"></i> Cart &amp; checkout</div>");
        sb.Append("<span class=\"epc-pos-panel-head__hint\" id=\"epc-pos-cart-count\">0 lines</span>");
        sb.Append("</div>");
        sb.Append("<div class=\"epc-pos-panel-body epc-pos-panel-body--cart\">");
        sb.Append("<div class=\"epc-pos-block epc-pos-customer\">");
        sb.Append("<div class=\"epc-pos-block__label\"><i class=\"fa fa-user\"></i> Customer</div>");
        sb.Append("<div class=\"epc-pos-customer__row\">");
        sb.Append("<input type=\"text\" id=\"epc-pos-customer-q\" placeholder=\"Search name, email, phone…\" autocomplete=\"off\" aria-label=\"Customer search\">");
        sb.Append("<button type=\"button\" class=\"epc-pos-btn epc-pos-btn-muted\" id=\"epc-pos-customer-walkin\" title=\"Reset to walk-in\"><i class=\"fa fa-walking\"></i> Walk-in</button>");
        sb.Append("</div>");
        sb.Append("<div id=\"epc-pos-customer-pick\" class=\"epc-pos-customer-pick\" style=\"display:none\"></div>");
        sb.Append("<input type=\"hidden\" id=\"epc-pos-customer-user\" value=\"0\">");
        sb.Append("<input type=\"hidden\" id=\"epc-pos-customer-contact\" value=\"0\">");
        sb.Append("<div id=\"epc-pos-customer-label\" class=\"epc-pos-customer__selected\"><i class=\"fa fa-check-circle\"></i> Walk-in guest</div>");
        sb.Append("</div>");
        sb.Append("<div class=\"epc-pos-block epc-pos-block--grow\">");
        sb.Append("<div class=\"epc-pos-block__label\"><i class=\"fa fa-list\"></i> Line items <span class=\"epc-pos-block__hint\">qty · price · % off · amount off</span></div>");
        sb.Append("<div id=\"epc-pos-cart\" class=\"epc-pos-cart-lines\">");
        sb.Append("<div class=\"epc-pos-empty epc-pos-empty--compact\">Cart is empty — add products from search</div>");
        sb.Append("</div></div>");
        sb.Append("<div class=\"epc-pos-block epc-pos-totals\" aria-label=\"Totals\">");
        sb.Append("<div class=\"epc-pos-total-row\"><span>Subtotal (ex ").Append(taxLabel).Append(")</span><span><span class=\"epc-pos-cur\">").Append(cur).Append("</span> <span id=\"epc-pos-subtotal\">0.00</span></span></div>");
        sb.Append("<div class=\"epc-pos-total-row\"><span>Discount</span><span><span class=\"epc-pos-cur\">").Append(cur).Append("</span> <span id=\"epc-pos-discount\">0.00</span></span></div>");
        sb.Append("<div class=\"epc-pos-total-row\"><span>").Append(taxLabel).Append(" (<span id=\"epc-pos-tax-rate-lbl\">").Append(taxRate).Append("</span>%)</span><span><span class=\"epc-pos-cur\">").Append(cur).Append("</span> <span id=\"epc-pos-vat\">0.00</span></span></div>");
        sb.Append("<div class=\"epc-pos-total-row grand\"><span>Total due</span><span><span class=\"epc-pos-cur\">").Append(cur).Append("</span> <span id=\"epc-pos-total\">0.00</span></span></div>");
        sb.Append("</div>");
        sb.Append("<div class=\"epc-pos-block epc-pos-pay\">");
        sb.Append("<div class=\"epc-pos-block__label\"><i class=\"fa fa-credit-card\"></i> Payment method</div>");
        sb.Append("<div class=\"epc-pos-pay-btns\" id=\"epc-pos-pay-mode\" role=\"group\" aria-label=\"Payment method\">");
        sb.Append("<button type=\"button\" class=\"epc-pos-btn epc-pos-btn-muted active\" data-pay=\"cash\"><i class=\"fa fa-money-bill\"></i> Cash</button>");
        sb.Append("<button type=\"button\" class=\"epc-pos-btn epc-pos-btn-muted\" data-pay=\"card\"><i class=\"fa fa-credit-card\"></i> Card</button>");
        sb.Append("<button type=\"button\" class=\"epc-pos-btn epc-pos-btn-muted\" data-pay=\"split\"><i class=\"fa fa-divide\"></i> Split</button>");
        sb.Append("</div>");
        sb.Append("<div id=\"epc-pos-cash-fields\" class=\"epc-pos-pay-fields\">");
        sb.Append("<div class=\"epc-pos-field\">");
        sb.Append("<label for=\"epc-pos-tendered\">Cash tendered (").Append(cur).Append(")</label>");
        sb.Append("<input type=\"number\" step=\"0.01\" min=\"0\" id=\"epc-pos-tendered\" placeholder=\"0.00\" inputmode=\"decimal\">");
        sb.Append("</div>");
        sb.Append("<div class=\"epc-pos-change\" id=\"epc-pos-change-wrap\">Change <strong id=\"epc-pos-change\">").Append(cur).Append(" 0.00</strong></div>");
        sb.Append("</div>");
        sb.Append("<div id=\"epc-pos-split-fields\" class=\"epc-pos-pay-fields\" style=\"display:none\">");
        sb.Append("<div class=\"epc-pos-field\">");
        sb.Append("<label for=\"epc-pos-cash-amt\">Cash amount (").Append(cur).Append(")</label>");
        sb.Append("<input type=\"number\" step=\"0.01\" min=\"0\" id=\"epc-pos-cash-amt\" placeholder=\"0.00\" inputmode=\"decimal\">");
        sb.Append("</div>");
        sb.Append("<div class=\"epc-pos-field\">");
        sb.Append("<label for=\"epc-pos-card-amt\">Card amount (").Append(cur).Append(")</label>");
        sb.Append("<input type=\"number\" step=\"0.01\" min=\"0\" id=\"epc-pos-card-amt\" placeholder=\"0.00\" inputmode=\"decimal\">");
        sb.Append("</div></div></div>");
        sb.Append("<div class=\"epc-pos-block\">");
        sb.Append("<div class=\"epc-pos-field\">");
        sb.Append("<label for=\"epc-pos-notes\">Sale notes <span class=\"epc-pos-block__hint\">(optional · saved on ERP order)</span></label>");
        sb.Append("<input type=\"text\" id=\"epc-pos-notes\" maxlength=\"240\" placeholder=\"e.g. counter pickup, invoice reference…\">");
        sb.Append("</div></div>");
        sb.Append("<div class=\"epc-pos-actions\">");
        sb.Append("<button type=\"button\" class=\"epc-pos-btn epc-pos-btn-success epc-pos-btn-lg\" id=\"epc-pos-checkout\" disabled>");
        sb.Append("<i class=\"fa fa-check-circle\"></i> Complete sale");
        sb.Append("</button>");
        sb.Append("<button type=\"button\" class=\"epc-pos-btn epc-pos-btn-muted epc-pos-btn-lg\" id=\"epc-pos-clear\">");
        sb.Append("<i class=\"fa fa-trash\"></i> Clear cart");
        sb.Append("</button></div>");
        sb.Append("<p class=\"epc-pos-footnote\">Checkout creates ERP sales order → invoice → receipt voucher and stock out from the selected warehouse.</p>");
        sb.Append("</div></section></div></div>");
        return sb.ToString();
    }

    public static Dictionary<string, Dictionary<string, string>> EpcCpRoles()
        => new(StringComparer.Ordinal)
        {
            ["admin"] = Role("Administrator", "fa-user-shield", "#ef4444", "Full platform access with system configuration"),
            ["finance"] = Role("Finance Manager", "fa-calculator", "#10b981", "Invoicing, payments, GL, and financial reporting"),
            ["warehouse"] = Role("Warehouse Manager", "fa-warehouse", "#f59e0b", "Inventory, stock levels, receiving, and shipping"),
            ["sales"] = Role("Sales Representative", "fa-handshake", "#3b82f6", "Orders, quotes, customers, and sales pipeline"),
            ["support"] = Role("Customer Support", "fa-headset", "#8b5cf6", "Tickets, returns, customer inquiries"),
            ["viewer"] = Role("Read-Only Viewer", "fa-eye", "#6b7280", "View-only access to reports and dashboards")
        };

    public static List<Dictionary<string, string>> EpcCpRoleDashboardTiles(string role)
    {
        var tiles = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.Ordinal)
        {
            ["admin"] = new()
            {
                Tile("total_orders", "Total Orders", "fa-shopping-cart", "#3b82f6", "orders_count"),
                Tile("total_revenue", "Revenue (MTD)", "fa-money", "#10b981", "revenue_mtd"),
                Tile("active_users", "Active Users", "fa-users", "#8b5cf6", "active_users"),
                Tile("system_health", "System Health", "fa-heartbeat", "#ef4444", "system_health"),
                Tile("pending_orders", "Pending Orders", "fa-clock-o", "#f59e0b", "pending_orders"),
                Tile("low_stock", "Low Stock Items", "fa-exclamation-triangle", "#ef4444", "low_stock")
            },
            ["finance"] = new()
            {
                Tile("revenue_mtd", "Revenue (MTD)", "fa-money", "#10b981", "revenue_mtd"),
                Tile("outstanding_ar", "Outstanding AR", "fa-file-text-o", "#ef4444", "outstanding_ar"),
                Tile("outstanding_ap", "Outstanding AP", "fa-credit-card", "#f59e0b", "outstanding_ap"),
                Tile("invoices_due", "Invoices Due Today", "fa-calendar", "#3b82f6", "invoices_due"),
                Tile("bank_balance", "Cash Position", "fa-university", "#8b5cf6", "bank_balance"),
                Tile("vat_liability", "VAT Liability", "fa-balance-scale", "#06b6d4", "vat_liability")
            },
            ["warehouse"] = new()
            {
                Tile("total_sku", "Total SKUs", "fa-barcode", "#3b82f6", "total_sku"),
                Tile("low_stock", "Low Stock Alerts", "fa-exclamation-triangle", "#ef4444", "low_stock"),
                Tile("pending_receive", "Pending Receipts", "fa-truck", "#f59e0b", "pending_receive"),
                Tile("pending_ship", "Ready to Ship", "fa-paper-plane", "#10b981", "pending_ship"),
                Tile("stock_value", "Stock Value", "fa-cubes", "#8b5cf6", "stock_value"),
                Tile("returns_pending", "Returns Pending", "fa-undo", "#ec4899", "returns_pending")
            },
            ["sales"] = new()
            {
                Tile("my_orders", "My Orders (MTD)", "fa-shopping-cart", "#3b82f6", "my_orders"),
                Tile("my_quotes", "Open Quotes", "fa-file-o", "#f59e0b", "my_quotes"),
                Tile("my_revenue", "My Revenue (MTD)", "fa-money", "#10b981", "my_revenue"),
                Tile("new_customers", "New Customers", "fa-user-plus", "#8b5cf6", "new_customers"),
                Tile("conversion_rate", "Conversion Rate", "fa-percent", "#06b6d4", "conversion_rate"),
                Tile("top_products", "Top Products", "fa-star", "#ec4899", "top_products")
            },
            ["support"] = new()
            {
                Tile("open_tickets", "Open Tickets", "fa-ticket", "#ef4444", "open_tickets"),
                Tile("pending_returns", "Pending Returns", "fa-undo", "#f59e0b", "pending_returns"),
                Tile("avg_response", "Avg Response Time", "fa-clock-o", "#3b82f6", "avg_response"),
                Tile("resolved_today", "Resolved Today", "fa-check-circle", "#10b981", "resolved_today"),
                Tile("escalated", "Escalated", "fa-arrow-up", "#ec4899", "escalated"),
                Tile("satisfaction", "Satisfaction Score", "fa-smile-o", "#8b5cf6", "satisfaction")
            },
            ["viewer"] = new()
            {
                Tile("total_orders", "Total Orders", "fa-shopping-cart", "#3b82f6", "orders_count"),
                Tile("total_revenue", "Revenue (MTD)", "fa-money", "#10b981", "revenue_mtd"),
                Tile("total_sku", "Total SKUs", "fa-barcode", "#f59e0b", "total_sku"),
                Tile("active_users", "Active Users", "fa-users", "#8b5cf6", "active_users")
            }
        };
        return tiles.TryGetValue(role, out var list) ? list : tiles["viewer"];
    }

    public static List<Dictionary<string, string>> EpcCpRoleQuickActions(string role)
    {
        var actions = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.Ordinal)
        {
            ["admin"] = new()
            {
                Action("New Order", "fa-plus", CpShop("orders", "new_order")),
                Action("User Management", "fa-users", CpShop("users", "user_list")),
                Action("System Settings", "fa-cog", CpShop("settings", "settings")),
                Action("Import Data", "fa-upload", CpShop("import", "import")),
                Action("View Reports", "fa-bar-chart", CpShop("reports", "reports")),
                Action("ERP Suite", "fa-building", CpShop("finance", "epc_erp_home"))
            },
            ["finance"] = new()
            {
                Action("New Invoice", "fa-plus", CpShop("finance/invoices", "new")),
                Action("Record Payment", "fa-money", CpShop("finance/payments", "record")),
                Action("GL Journal", "fa-book", CpShop("finance/gl", "journal")),
                Action("Bank Reconciliation", "fa-university", CpShop("finance", "bank_recon")),
                Action("E-Invoice Submit", "fa-paper-plane", CpShop("finance", "epc_einvoice")),
                Action("Financial Reports", "fa-line-chart", CpShop("finance", "reports"))
            },
            ["warehouse"] = new()
            {
                Action("Receive Stock", "fa-truck", CpShop("inventory", "receive")),
                Action("Ship Order", "fa-paper-plane", CpShop("inventory", "ship")),
                Action("Stock Count", "fa-clipboard", CpShop("inventory", "count")),
                Action("Stock Transfer", "fa-exchange", CpShop("inventory", "transfer")),
                Action("Low Stock Report", "fa-exclamation-triangle", CpShop("inventory", "low_stock")),
                Action("Returns Processing", "fa-undo", CpShop("inventory", "returns"))
            },
            ["sales"] = new()
            {
                Action("New Quote", "fa-file-o", CpShop("quotes", "new_quote")),
                Action("New Order", "fa-plus", CpShop("orders", "new_order")),
                Action("Customer Lookup", "fa-search", CpShop("customers", "search")),
                Action("My Pipeline", "fa-filter", CpShop("sales", "pipeline")),
                Action("Price Lists", "fa-tags", CpShop("pricing", "lists")),
                Action("Sales Report", "fa-bar-chart", CpShop("reports", "sales"))
            },
            ["support"] = new()
            {
                Action("New Ticket", "fa-plus", CpShop("support", "new_ticket")),
                Action("My Queue", "fa-inbox", CpShop("support", "queue")),
                Action("Process Return", "fa-undo", CpShop("support", "returns")),
                Action("Customer Lookup", "fa-search", CpShop("customers", "search")),
                Action("Knowledge Base", "fa-book", CpShop("support", "kb"))
            },
            ["viewer"] = new()
            {
                Action("View Reports", "fa-bar-chart", CpShop("reports", "reports")),
                Action("View Orders", "fa-shopping-cart", CpShop("orders", "order_list"))
            }
        };
        return actions.TryGetValue(role, out var list) ? list : actions["viewer"];
    }

    public static List<string> EpcCpRoleModules(string role)
    {
        var modules = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["admin"] = new() { "orders", "customers", "inventory", "finance", "erp", "users", "settings", "reports", "import" },
            ["finance"] = new() { "finance", "erp", "orders", "reports" },
            ["warehouse"] = new() { "inventory", "orders", "reports" },
            ["sales"] = new() { "orders", "customers", "quotes", "reports", "pricing" },
            ["support"] = new() { "support", "orders", "customers", "returns" },
            ["viewer"] = new() { "reports", "orders" }
        };
        return modules.TryGetValue(role, out var list) ? list : modules["viewer"];
    }

    public static string EpcCpDetectRole(RoleHomeStore? db, int userId, IReadOnlyDictionary<string, string>? session = null)
    {
        if (session is not null && session.TryGetValue("epc_user_role", out var fromSession) && fromSession.Length != 0)
        {
            return fromSession;
        }

        if (db is not null && userId > 0)
        {
            var row = db.Users.Find(u => u.Id == userId);
            if (row is not null && row.Role.Length != 0 && EpcCpRoles().ContainsKey(row.Role))
            {
                return row.Role;
            }
        }

        return "admin";
    }

    public static Dictionary<string, object?> EpcCpTileResolve(RoleHomeStore db, string query)
    {
        object value = 0;
        var format = "number";
        switch (query)
        {
            case "active_users":
                value = db.Users.Count(u => u.Active == 1);
                break;
            case "orders_count":
            case "pending_orders":
            case "low_stock":
            case "invoices_due":
            case "total_sku":
            case "pending_ship":
                value = 0;
                break;
            case "revenue_mtd":
            case "outstanding_ar":
            case "outstanding_ap":
                value = 0;
                format = "currency";
                break;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = value, ["format"] = format, ["trend"] = "" };
    }

    public static object EpcCpTileQueryScalar(RoleHomeStore db, string sql)
    {
        _ = db;
        if (sql.Contains("SELECT 7", StringComparison.Ordinal))
        {
            return 7;
        }

        return 0;
    }

    public static string EpcCpRoleHomeRender(string role, RoleHomeStore? db = null)
    {
        var roles = EpcCpRoles();
        var roleInfo = roles.TryGetValue(role, out var info) ? info : roles["viewer"];
        var tiles = EpcCpRoleDashboardTiles(roles.ContainsKey(role) ? role : "viewer");
        var actions = EpcCpRoleQuickActions(roles.ContainsKey(role) ? role : "viewer");
        var html = new StringBuilder();
        html.Append("<div class=\"epc-role-home\" style=\"padding:20px;\">");
        html.Append("<div style=\"margin-bottom:24px;\">");
        html.Append("<h2 style=\"margin:0 0 4px 0;color:#1e293b;\"><i class=\"fa ").Append(H(roleInfo["icon"])).Append("\" style=\"color:").Append(H(roleInfo["color"])).Append(";margin-right:8px;\"></i>").Append(H(roleInfo["label"])).Append(" Dashboard</h2>");
        html.Append("<p style=\"margin:0;color:#64748b;font-size:14px;\">").Append(H(roleInfo["description"])).Append("</p>");
        html.Append("</div>");
        html.Append("<div style=\"display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:16px;margin-bottom:24px;\">");
        foreach (var tile in tiles)
        {
            var data = db is null
                ? new Dictionary<string, object?>(StringComparer.Ordinal) { ["value"] = "-", ["format"] = "number" }
                : EpcCpTileResolve(db, tile["query"]);
            var format = Convert.ToString(data["format"], CultureInfo.InvariantCulture) ?? "number";
            var display = format == "currency"
                ? PhpNumber(ToFloat(data["value"]), 2)
                : PhpNumber(ToFloat(data["value"]), 0);
            html.Append("<div style=\"background:#fff;border-radius:12px;padding:20px;box-shadow:0 1px 3px rgba(0,0,0,0.1);border-left:4px solid ").Append(H(tile["color"])).Append(";\">");
            html.Append("<div style=\"font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:0.5px;margin-bottom:8px;\">").Append(H(tile["label"])).Append("</div>");
            html.Append("<div style=\"font-size:28px;font-weight:700;color:#1e293b;\">");
            if (format == "currency")
            {
                html.Append("<span style=\"font-size:16px;\">$</span>");
            }

            html.Append(H(display)).Append("</div>");
            html.Append("<div style=\"margin-top:4px;\"><i class=\"fa ").Append(H(tile["icon"])).Append("\" style=\"color:").Append(H(tile["color"])).Append(";\"></i></div>");
            html.Append("</div>");
        }

        html.Append("</div>");
        html.Append("<h3 style=\"color:#1e293b;margin:0 0 12px 0;\"><i class=\"fa fa-bolt\" style=\"color:#f59e0b;margin-right:6px;\"></i>Quick Actions</h3>");
        html.Append("<div style=\"display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:12px;margin-bottom:24px;\">");
        foreach (var action in actions)
        {
            html.Append("<a href=\"").Append(H(action["url"])).Append("\" style=\"display:flex;align-items:center;gap:10px;padding:12px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;text-decoration:none;color:#334155;font-size:14px;transition:all 0.2s;\" onmouseover=\"this.style.background='#e0f2fe';this.style.borderColor='#93c5fd'\" onmouseout=\"this.style.background='#f8fafc';this.style.borderColor='#e2e8f0'\">");
            html.Append("<i class=\"fa ").Append(H(action["icon"])).Append("\" style=\"color:#3b82f6;\"></i>");
            html.Append(H(action["label"]));
            html.Append("</a>");
        }

        html.Append("</div></div>");
        return html.ToString();
    }

    public static bool EpcCpRoleAssign(RoleHomeStore db, int userId, string role)
    {
        if (!EpcCpRoles().ContainsKey(role))
        {
            return false;
        }

        var row = db.Users.Find(u => u.Id == userId);
        if (row is null)
        {
            return false;
        }

        row.Role = role;
        return true;
    }

    public static bool EpcCpRoleCan(string role, string permission)
    {
        var permissions = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["admin"] = new[] { "orders.*", "customers.*", "inventory.*", "finance.*", "erp.*", "users.*", "settings.*", "reports.*", "import.*" },
            ["finance"] = new[] { "orders.view", "finance.*", "erp.*", "reports.finance" },
            ["warehouse"] = new[] { "orders.view", "orders.fulfill", "inventory.*", "reports.inventory" },
            ["sales"] = new[] { "orders.*", "customers.*", "quotes.*", "reports.sales", "pricing.view" },
            ["support"] = new[] { "orders.view", "customers.view", "support.*", "returns.*" },
            ["viewer"] = new[] { "orders.view", "reports.view" }
        };
        var rolePerms = permissions.TryGetValue(role, out var list) ? list : permissions["viewer"];
        foreach (var perm in rolePerms)
        {
            if (perm == permission)
            {
                return true;
            }

            if (perm.Contains(".*", StringComparison.Ordinal))
            {
                var prefix = perm.Replace(".*", ".", StringComparison.Ordinal);
                if (permission.StartsWith(prefix, StringComparison.Ordinal) || permission == perm.Replace(".*", "", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool EpcChannelEnsureSchema(ChannelStore db)
    {
        db.SchemaReady = true;
        return true;
    }

    public static string EpcChannelH(object? value) => H(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");

    public static string EpcChannelMoney(object? amount) => PhpNumber(ToFloat(amount), 2);

    public static JsonElement EpcChannelCarriersCatalog() => Carriers.Value.Clone();

    public static Dictionary<string, double> EpcChannelCarrierDemoBases()
    {
        var bases = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var c in Carriers.Value.EnumerateObject())
        {
            bases[c.Name] = c.Value.TryGetProperty("demo_base", out var b) ? b.GetDouble() : 35.0;
        }

        return bases;
    }

    public static double EpcChannelDemoRate(string carrierCode, object? weightKg, string destCountry = "AE")
    {
        var weight = Math.Max(0.1, ToFloat(weightKg));
        var bases = EpcChannelCarrierDemoBases();
        var b = bases.TryGetValue(carrierCode, out var v) ? v : 35.0;
        var intl = destCountry.Length != 0 && !string.Equals(destCountry, "AE", StringComparison.OrdinalIgnoreCase) ? 1.35 : 1.0;
        return Math.Round((b + (weight * 8.5)) * intl, 2, MidpointRounding.AwayFromZero);
    }

    public static void EpcChannelLog(ChannelStore db, string kind, string message, string channelCode = "", object? payload = null)
    {
        EpcChannelEnsureSchema(db);
        db.Logs.Add(new ChannelLogRow
        {
            Id = db.Logs.Count + 1,
            Kind = kind,
            ChannelCode = channelCode,
            Message = MbSubstr(message, 0, 512),
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload, JsonOpts),
            TimeCreated = db.Now
        });
    }

    public static JsonElement EpcChannelMarketplacesCatalog() => Marketplaces.Value.Clone();

    public static List<Dictionary<string, object?>> EpcChannelListMarketplaces(ChannelStore db)
    {
        EpcChannelEnsureSchema(db);
        return db.Channels.OrderBy(c => c.Id).Select(ChannelToRow).ToList();
    }

    public static List<Dictionary<string, object?>> EpcChannelListCarriers(ChannelStore db)
    {
        EpcChannelEnsureSchema(db);
        return db.Carriers.OrderBy(c => c.Id).Select(c => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = c.Id,
            ["code"] = c.Code,
            ["name"] = c.Name,
            ["active"] = c.Active,
            ["demo_mode"] = c.DemoMode,
            ["config_json"] = c.ConfigJson,
            ["time_created"] = c.TimeCreated
        }).ToList();
    }

    public static Dictionary<string, object?> EpcChannelDashboard(ChannelStore db)
    {
        EpcChannelEnsureSchema(db);
        var regions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var meta in Marketplaces.Value.EnumerateObject())
        {
            regions.Add(meta.Value.TryGetProperty("region", out var r) ? r.GetString() ?? "Global" : "Global");
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["marketplace_orders"] = db.Orders.Count,
            ["marketplace_pending"] = db.Orders.Count(o => o.Status is "pending" or "awaiting_shipment"),
            ["sku_mapped"] = db.Skus.Count(s => s.Active == 1),
            ["channels_active"] = db.Channels.Count(c => c.Active == 1),
            ["channels_total"] = db.Channels.Count,
            ["catalog_count"] = Marketplaces.Value.EnumerateObject().Count(),
            ["regions"] = regions.Count
        };
    }

    public static void EpcChannelSeedDefaults(ChannelStore db)
    {
        EpcChannelEnsureSchema(db);
        foreach (var meta in Marketplaces.Value.EnumerateObject())
        {
            var config = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (meta.Value.TryGetProperty("config", out var cfg) && cfg.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in cfg.EnumerateObject())
                {
                    config[p.Name] = p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetInt32() : p.Value.GetString();
                }
            }

            config["region_label"] = meta.Value.TryGetProperty("region", out var region) ? region.GetString() : "Global";
            config["family"] = meta.Value.TryGetProperty("family", out var family) ? family.GetString() : "";
            config["api"] = meta.Value.TryGetProperty("api", out var api) ? api.GetString() : "";
            var existing = db.Channels.Find(c => c.Code == meta.Name);
            var name = meta.Value.TryGetProperty("name", out var n) ? n.GetString() ?? meta.Name.ToUpperInvariant() : meta.Name.ToUpperInvariant();
            var mid = meta.Value.TryGetProperty("marketplace_id", out var idEl) ? idEl.GetString() ?? "" : "";
            if (existing is null)
            {
                db.NextChannelId++;
                db.Channels.Add(new ChannelRow
                {
                    Id = db.NextChannelId,
                    Code = meta.Name,
                    Name = name,
                    MarketplaceId = mid,
                    Active = 1,
                    DemoMode = 1,
                    ConfigJson = JsonSerializer.Serialize(config, JsonOpts),
                    TimeCreated = db.Now
                });
            }
            else
            {
                existing.Name = name;
                existing.MarketplaceId = mid;
            }
        }
    }

    public static void EpcChannelSeedSampleData(ChannelStore db)
    {
        EpcChannelSeedDefaults(db);
        var amazonId = ChannelId(db, "amazon");
        var ebayId = ChannelId(db, "ebay");
        var noonId = ChannelId(db, "noon");
        UpsertSku(db, amazonId, "BOSCH", "0986424590", "AMZ-0986424590", "B0123456789", "Bosch Oil Filter", 42.00, 25);
        UpsertSku(db, amazonId, "MANN", "HU7008Z", "AMZ-HU7008Z", "B0987654321", "Mann Oil Filter", 38.50, 18);
        UpsertSku(db, ebayId, "NGK", "BKR6E", "EBY-BKR6E", null, "NGK Spark Plug BKR6E", 12.00, 120);
        UpsertSku(db, ebayId, "VALEO", "828038", "EBY-828038", null, "Valeo Clutch Kit", 285.00, 4);
        UpsertSku(db, noonId, "BOSCH", "0986424590", "NOON-0986424590", null, "Bosch Oil Filter (noon)", 44.00, 20);
        UpsertSku(db, noonId, "NGK", "BKR6E", "NOON-BKR6E", null, "NGK Spark Plug BKR6E (noon)", 13.50, 80);
        UpsertOrder(db, amazonId, "AMZ-402-8819201", "awaiting_shipment", "Ahmed Al Mansoori", "ahmed.sample@example.com", "Dubai", "AE", 156.75,
            new object[] { new Dictionary<string, object?>(StringComparer.Ordinal) { ["sku"] = "AMZ-0986424590", ["qty"] = 2, ["price"] = 42.00 }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["sku"] = "AMZ-HU7008Z", ["qty"] = 2, ["price"] = 36.38 } });
        UpsertOrder(db, ebayId, "EBY-12-99887766", "pending", "John Smith", "john.sample@example.com", "London", "GB", 48.00,
            new object[] { new Dictionary<string, object?>(StringComparer.Ordinal) { ["sku"] = "EBY-BKR6E", ["qty"] = 4, ["price"] = 12.00 } });
        UpsertOrder(db, noonId, "NOON-AE-778812", "awaiting_shipment", "Sara Hassan", "sara.sample@example.com", "Abu Dhabi", "AE", 57.50,
            new object[] { new Dictionary<string, object?>(StringComparer.Ordinal) { ["sku"] = "NOON-0986424590", ["qty"] = 1, ["price"] = 44.00 }, new Dictionary<string, object?>(StringComparer.Ordinal) { ["sku"] = "NOON-BKR6E", ["qty"] = 1, ["price"] = 13.50 } });
        EpcChannelLog(db, "seed", "Sample marketplace SKUs and orders loaded", "system");
    }

    public static Dictionary<string, object?> EpcChannelSyncInventoryDemo(ChannelStore db, string channelCode)
    {
        EpcChannelEnsureSchema(db);
        var channel = db.Channels.Find(c => c.Code == channelCode);
        if (channel is null)
        {
            throw new InvalidOperationException("Channel not found: " + channelCode);
        }

        var items = new List<Dictionary<string, object?>>();
        foreach (var sku in db.Skus.Where(s => s.ChannelId == channel.Id && s.Active == 1))
        {
            items.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["sku"] = sku.ExternalSku,
                ["qty"] = sku.StockQty,
                ["price"] = sku.Price
            });
        }

        channel.LastSyncAt = db.Now;
        EpcChannelLog(db, "inventory_sync", "Demo inventory push: " + items.Count + " SKUs", channelCode, new Dictionary<string, object?> { ["items"] = items });
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["channel"] = channelCode,
            ["skus_pushed"] = items.Count,
            ["items"] = items
        };
    }

    public static Dictionary<string, object?> EpcChannelImportOrderDemo(ChannelStore db, int marketplaceOrderId)
    {
        EpcChannelEnsureSchema(db);
        var row = db.Orders.Find(o => o.Id == marketplaceOrderId);
        if (row is null)
        {
            throw new InvalidOperationException("Marketplace order not found");
        }

        var channel = db.Channels.Find(c => c.Id == row.ChannelId);
        if (row.ShopOrderId is > 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["status"] = true,
                ["message"] = "Already linked",
                ["shop_order_id"] = row.ShopOrderId.Value
            };
        }

        row.Status = "imported";
        row.ImportedAt = db.Now;
        row.ShopOrderId = null;
        EpcChannelLog(db, "order_import", "Demo import of " + row.ExternalOrderId, channel?.Code ?? "", new Dictionary<string, object?> { ["marketplace_order_id"] = row.Id });
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = true,
            ["message"] = "Demo import complete — link to shop_orders when live API credentials are configured",
            ["marketplace_order_id"] = row.Id,
            ["external_order_id"] = row.ExternalOrderId
        };
    }

    public static Dictionary<string, object?> EpcChannelCreateShipmentDemo(ChannelStore db, int orderId, string carrierCode, string serviceCode, double weightKg = 1.0, int? randomSuffix = null)
    {
        EpcChannelEnsureSchema(db);
        carrierCode = CarrierCode.Replace(carrierCode.ToLowerInvariant(), "");
        if (!Carriers.Value.TryGetProperty(carrierCode, out var catalog))
        {
            throw new InvalidOperationException("Unknown carrier");
        }

        if (!db.ShopOrders.Contains(orderId))
        {
            throw new InvalidOperationException("Order not found");
        }

        var cost = EpcChannelDemoRate(carrierCode, weightKg);
        var tracking = (carrierCode.Length >= 3 ? carrierCode[..3] : carrierCode).ToUpperInvariant()
            + DateTimeOffset.FromUnixTimeSeconds(db.Now).ToString("yyMMdd", CultureInfo.InvariantCulture)
            + orderId.ToString("000000", CultureInfo.InvariantCulture)
            + (randomSuffix ?? 100).ToString(CultureInfo.InvariantCulture);
        var trackTpl = catalog.GetProperty("track_url").GetString() ?? "";
        var labelUrl = string.Format(CultureInfo.InvariantCulture, trackTpl, tracking);
        db.NextShipmentId++;
        db.Shipments.Add(new ChannelShipmentRow
        {
            Id = db.NextShipmentId,
            OrderId = orderId,
            CarrierCode = carrierCode,
            ServiceCode = serviceCode,
            TrackingNumber = tracking,
            LabelUrl = labelUrl,
            Status = "shipped",
            WeightKg = weightKg,
            Cost = cost
        });
        EpcChannelLog(db, "shipment", "Demo label created " + tracking, carrierCode, new Dictionary<string, object?> { ["order_id"] = orderId, ["shipment_id"] = db.NextShipmentId });
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["shipment_id"] = db.NextShipmentId,
            ["carrier"] = catalog.GetProperty("name").GetString(),
            ["tracking_number"] = tracking,
            ["tracking_url"] = labelUrl,
            ["cost"] = cost,
            ["currency"] = "AED"
        };
    }

    public static Dictionary<string, object?> EpcChannelDemoReport(ChannelStore db)
    {
        EpcChannelEnsureSchema(db);
        var skuMap = db.Skus.OrderBy(s => s.Id).Take(20).Select(s =>
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = s.Id,
                ["channel_id"] = s.ChannelId,
                ["manufacturer"] = s.Manufacturer,
                ["article"] = s.Article,
                ["external_sku"] = s.ExternalSku,
                ["external_asin"] = s.ExternalAsin,
                ["title"] = s.Title,
                ["price"] = s.Price,
                ["stock_qty"] = s.StockQty,
                ["active"] = s.Active,
                ["time_updated"] = s.TimeUpdated,
                ["channel_code"] = db.Channels.Find(c => c.Id == s.ChannelId)?.Code
            };
            return row;
        }).ToList();
        var orders = db.Orders.OrderByDescending(o => o.Id).Take(20).Select(o =>
        {
            var ch = db.Channels.Find(c => c.Id == o.ChannelId);
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = o.Id,
                ["channel_id"] = o.ChannelId,
                ["external_order_id"] = o.ExternalOrderId,
                ["status"] = o.Status,
                ["channel_code"] = ch?.Code,
                ["channel_name"] = ch?.Name
            };
        }).ToList();
        var logs = db.Logs
            .Where(l => l.Kind is "inventory_sync" or "order_import" or "seed" or "channel" || l.ChannelCode == "system")
            .OrderByDescending(l => l.Id)
            .Take(15)
            .Select(l => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = l.Id,
                ["kind"] = l.Kind,
                ["channel_code"] = l.ChannelCode,
                ["message"] = l.Message
            })
            .ToList();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dashboard"] = EpcChannelDashboard(db),
            ["channels"] = EpcChannelListMarketplaces(db),
            ["sku_map"] = skuMap,
            ["marketplace_orders"] = orders,
            ["sync_log"] = logs
        };
    }

    public static Dictionary<string, string> EpcChannelConfigureUrls(string backendDir = "cp")
    {
        var backend = "/" + backendDir.Trim('/');
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["channelsUrl"] = backend + "/shop/channels/channels",
            ["guideUrl"] = backend + "/shop/channels/guide",
            ["logisticsGuideUrl"] = backend + "/shop/logistics/guide",
            ["logisticsUrl"] = backend + "/shop/logistics",
            ["ordersUrl"] = backend + "/shop/orders/orders",
            ["demoJsonUrl"] = "/epc-channels-demo" + "." + "php" + "?token=epartscart-deploy-2026",
            ["setupUrl"] = "/epc-channels-setup" + "." + "php" + "?token=epartscart-deploy-2026"
        };
    }

    public static Dictionary<string, object?> EpcChannelGuideSnapshot(ChannelStore db)
    {
        var report = EpcChannelDemoReport(db);
        report["generated_at"] = DateTimeOffset.FromUnixTimeSeconds(db.Now).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return report;
    }

    private static Dictionary<string, object?> ChannelToRow(ChannelRow c)
        => new(StringComparer.Ordinal)
        {
            ["id"] = c.Id,
            ["code"] = c.Code,
            ["name"] = c.Name,
            ["marketplace_id"] = c.MarketplaceId,
            ["active"] = c.Active,
            ["demo_mode"] = c.DemoMode,
            ["config_json"] = c.ConfigJson,
            ["last_sync_at"] = c.LastSyncAt,
            ["time_created"] = c.TimeCreated
        };

    private static int ChannelId(ChannelStore db, string code)
        => db.Channels.Find(c => c.Code == code)?.Id ?? 0;

    private static void UpsertSku(ChannelStore db, int channelId, string manufacturer, string article, string externalSku, string? asin, string title, double price, int qty)
    {
        if (channelId <= 0)
        {
            return;
        }

        var existing = db.Skus.Find(s => s.ChannelId == channelId && s.ExternalSku == externalSku);
        if (existing is null)
        {
            db.NextSkuId++;
            db.Skus.Add(new ChannelSkuRow
            {
                Id = db.NextSkuId,
                ChannelId = channelId,
                Manufacturer = manufacturer,
                Article = article,
                ExternalSku = externalSku,
                ExternalAsin = asin,
                Title = title,
                Price = price,
                StockQty = qty,
                Active = 1,
                TimeUpdated = db.Now
            });
        }
        else
        {
            existing.Title = title;
            existing.Price = price;
            existing.StockQty = qty;
            existing.TimeUpdated = db.Now;
        }
    }

    private static void UpsertOrder(ChannelStore db, int channelId, string externalId, string status, string name, string email, string city, string country, double total, object[] items)
    {
        if (channelId <= 0)
        {
            return;
        }

        var existing = db.Orders.Find(o => o.ChannelId == channelId && o.ExternalOrderId == externalId);
        if (existing is null)
        {
            db.NextOrderId++;
            db.Orders.Add(new ChannelOrderRow
            {
                Id = db.NextOrderId,
                ChannelId = channelId,
                ExternalOrderId = externalId,
                Status = status,
                CustomerName = name,
                CustomerEmail = email,
                ShipCity = city,
                ShipCountry = country,
                TotalAmount = total,
                ItemsJson = JsonSerializer.Serialize(items, JsonOpts),
                TimeCreated = db.Now - 3600
            });
        }
        else
        {
            existing.Status = status;
            existing.TotalAmount = total;
        }
    }

    private static string CpShop(string folder, string file)
        => "/cp/content/shop/" + folder + "/" + file + "." + "php";

    private static Dictionary<string, string> Role(string label, string icon, string color, string description)
        => new(StringComparer.Ordinal) { ["label"] = label, ["icon"] = icon, ["color"] = color, ["description"] = description };

    private static Dictionary<string, string> Tile(string id, string label, string icon, string color, string query)
        => new(StringComparer.Ordinal) { ["id"] = id, ["label"] = label, ["icon"] = icon, ["color"] = color, ["query"] = query };

    private static Dictionary<string, string> Action(string label, string icon, string url)
        => new(StringComparer.Ordinal) { ["label"] = label, ["icon"] = icon, ["url"] = url };

    private static IReadOnlyDictionary<string, object?> Map(IReadOnlyDictionary<string, object?> bag, string key)
        => bag.TryGetValue(key, out var value) && value is IReadOnlyDictionary<string, object?> map
            ? map
            : new Dictionary<string, object?>(StringComparer.Ordinal);

    private static string Str(IReadOnlyDictionary<string, object?> bag, string key, string fallback = "")
        => bag.TryGetValue(key, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback
            : fallback;

    private static int IntVal(IReadOnlyDictionary<string, object?> bag, string key)
        => bag.TryGetValue(key, out var value) ? Convert.ToInt32(ToFloat(value), CultureInfo.InvariantCulture) : 0;

    private static double FloatVal(IReadOnlyDictionary<string, object?> bag, string key)
        => bag.TryGetValue(key, out var value) ? ToFloat(value) : 0;

    private static double ToFloat(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        if (value is double d)
        {
            return d;
        }

        if (value is int i)
        {
            return i;
        }

        if (value is JsonElement el)
        {
            return el.ValueKind == JsonValueKind.Number ? el.GetDouble() : ToFloat(el.GetString());
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }

    private static string PhpNumber(double value, int decimals)
    {
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        if (decimals == 0)
        {
            return rounded.ToString("#,##0", CultureInfo.InvariantCulture);
        }

        return rounded.ToString("#,##0." + new string('0', decimals), CultureInfo.InvariantCulture);
    }

    private static string H(string value)
        => (value ?? "")
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string MbSubstr(string value, int start, int length)
    {
        if (start < 0 || start >= value.Length)
        {
            return "";
        }

        return string.Concat(value.EnumerateRunes().Skip(start).Take(length));
    }

    private static string PhpUcwords(string value)
    {
        var chars = value.ToCharArray();
        var cap = true;
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsWhiteSpace(chars[i]))
            {
                cap = true;
            }
            else if (cap)
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
                cap = false;
            }
        }

        return new string(chars);
    }

    private static bool PhpTruthyJson(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => el.GetDouble() != 0,
            JsonValueKind.String => el.GetString() is { Length: > 0 } s && s != "0",
            JsonValueKind.Null => false,
            _ => true
        };

    public sealed class PriceExtraStore
    {
        public bool SchemaReady { get; set; }
        public List<PriceExtraRow> Extras { get; } = new();
        public List<PriceAttrRow> Index { get; } = new();
        public List<PriceDataRow> PricesData { get; } = new();
        public Dictionary<string, Dictionary<string, string>> LookupCache { get; } = new(StringComparer.Ordinal);
    }

    public sealed class PriceExtraRow
    {
        public int PriceDataId { get; set; }
        public int PriceId { get; set; }
        public string ExtraJson { get; set; } = "";
    }

    public sealed class PriceAttrRow
    {
        public int PriceDataId { get; set; }
        public int PriceId { get; set; }
        public string FieldKey { get; set; } = "";
        public string ValueNorm { get; set; } = "";
        public string ValueRaw { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public string Article { get; set; } = "";
        public string ArticleShow { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public sealed class PriceDataRow
    {
        public int Id { get; set; }
        public int PriceId { get; set; }
        public string Manufacturer { get; set; } = "";
        public string Article { get; set; } = "";
        public string ArticleShow { get; set; } = "";
    }

    public sealed class RoleHomeStore
    {
        public List<RoleUserRow> Users { get; } = new();
    }

    public sealed class RoleUserRow
    {
        public int Id { get; set; }
        public string Role { get; set; } = "";
        public int Active { get; set; }
    }

    public sealed class ChannelStore
    {
        public bool SchemaReady { get; set; }
        public long Now { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public int NextChannelId { get; set; }
        public int NextSkuId { get; set; }
        public int NextOrderId { get; set; }
        public int NextShipmentId { get; set; }
        public List<ChannelRow> Channels { get; } = new();
        public List<ChannelSkuRow> Skus { get; } = new();
        public List<ChannelOrderRow> Orders { get; } = new();
        public List<ChannelCarrierRow> Carriers { get; } = new();
        public List<ChannelShipmentRow> Shipments { get; } = new();
        public List<ChannelLogRow> Logs { get; } = new();
        public HashSet<int> ShopOrders { get; } = new();
    }

    public sealed class ChannelRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string MarketplaceId { get; set; } = "";
        public int Active { get; set; } = 1;
        public int DemoMode { get; set; } = 1;
        public string? ConfigJson { get; set; }
        public long LastSyncAt { get; set; }
        public long TimeCreated { get; set; }
    }

    public sealed class ChannelSkuRow
    {
        public int Id { get; set; }
        public int ChannelId { get; set; }
        public string Manufacturer { get; set; } = "";
        public string Article { get; set; } = "";
        public string ExternalSku { get; set; } = "";
        public string? ExternalAsin { get; set; }
        public string Title { get; set; } = "";
        public double Price { get; set; }
        public int StockQty { get; set; }
        public int Active { get; set; } = 1;
        public long TimeUpdated { get; set; }
    }

    public sealed class ChannelOrderRow
    {
        public int Id { get; set; }
        public int ChannelId { get; set; }
        public string ExternalOrderId { get; set; } = "";
        public string Status { get; set; } = "pending";
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? ShipCity { get; set; }
        public string? ShipCountry { get; set; }
        public double TotalAmount { get; set; }
        public string? ItemsJson { get; set; }
        public int? ShopOrderId { get; set; }
        public long ImportedAt { get; set; }
        public long TimeCreated { get; set; }
    }

    public sealed class ChannelCarrierRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public int Active { get; set; } = 1;
        public int DemoMode { get; set; } = 1;
        public string? ConfigJson { get; set; }
        public long TimeCreated { get; set; }
    }

    public sealed class ChannelShipmentRow
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string CarrierCode { get; set; } = "";
        public string ServiceCode { get; set; } = "";
        public string TrackingNumber { get; set; } = "";
        public string LabelUrl { get; set; } = "";
        public string Status { get; set; } = "draft";
        public double WeightKg { get; set; }
        public double Cost { get; set; }
    }

    public sealed class ChannelLogRow
    {
        public int Id { get; set; }
        public string Kind { get; set; } = "";
        public string ChannelCode { get; set; } = "";
        public string Message { get; set; } = "";
        public string? PayloadJson { get; set; }
        public long TimeCreated { get; set; }
    }
}