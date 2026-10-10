using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-list helpers. PHP identifiers kept for the inventory:
/// <c>epc_agent_catalog_cache_path</c>, <c>epc_agent_catalog_norm_key</c>,
/// <c>epc_agent_catalog_match_pattern</c>, <c>epc_agent_catalog_build_index</c>,
/// <c>epc_agent_catalog_index</c>, <c>epc_agent_catalog_section_hint</c>,
/// <c>epc_agent_catalog_section_label</c>, <c>epc_agent_catalog_match_stock</c>,
/// <c>epc_agent_catalog_match_vehicle</c>, <c>epc_agent_catalog_match_model</c>,
/// <c>epc_agent_catalog_has_inquiry_intent</c>, <c>epc_agent_catalog_is_list_query</c>,
/// <c>epc_agent_catalog_resolve_query</c>, <c>epc_agent_catalog_match_name_in_text</c>.
/// </summary>
public static class PhpPlanQ1List
{
    public const string AgentCatalogPath = "content/shop/docpart/epc_agent_catalog_knowledge.php";

    private static readonly Regex CompactKeep = new("[^A-Z0-9А-ЯЁ]+", RegexOptions.CultureInvariant);
    private static readonly Regex CommercialHint = new(@"\b(commercial|truck|trucks|lcv|cv\b|bus|buses|fleet|hino|isuzu npr|fuso|scania|man truck|daf truck)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MotorbikeHint = new(@"\b(motorcycle|motorbike|motorbikes|bike|bikes|scooter|scooters|atv|dirt bike|yamaha r1|honda cb)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PassengerHint = new(@"\b(passenger|car|cars|sedan|suv|hatchback|saloon)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex InquiryIntent = new(@"\b(do you have|have you got|do you carry|do you sell|any|got|stock|available|availability|parts?|spares?|components?|brand|brands|manufacturer|maker|make|makes|vehicle|vehicles|catalog|catalogue|support|carry|sell|list|show|what|which|browse|model|models|commercial|passenger|motorcycle|motorbike|truck|bike)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ListLead = new(@"\b(brand|brands|make|makes|manufacturer|manufacturers|vehicle|vehicles|catalog|catalogue|list|what|which|how many|show|available)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ListConfirm = new(@"\b(what|which|how many|list|show|all|available|do you have|passenger|commercial|motorcycle|motorbike|truck|car|cars|brand|brands|make|makes|vehicle|vehicles|catalog)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex VinWord = new(@"\b(vin|part number|article)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex VinToken = new(@"\b([A-HJ-NPR-Z0-9]{11,17})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex NameOnly = new(@"^[A-Za-z0-9][A-Za-z0-9\-\.\s]{1,40}$", RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions CacheJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static Dictionary<string, object?>? _mem;
    private static long _memAt;

    public static string DocumentRoot { get; set; } = "";
    public static Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static string? CachePathOverride { get; set; }

    public static void Reset()
    {
        DocumentRoot = "";
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        CachePathOverride = null;
        _mem = null;
        _memAt = 0;
    }

    public sealed class ListStore
    {
        public PhpPlanQ1Text.TextStore Text { get; } = new();
        public List<UmapiMfrRow> Manufacturers { get; } = [];
        public List<UmapiModelRow> Models { get; } = [];
        public bool ManufacturersMissing { get; set; }
        public bool ModelsMissing { get; set; }
    }

    public sealed class UmapiMfrRow
    {
        public string Section { get; set; } = "";
        public int MfaId { get; set; }
        public string Manufacturer { get; set; } = "";
        public string Type { get; set; } = "";
        public string Country { get; set; } = "";
    }

    public sealed class UmapiModelRow
    {
        public string Section { get; set; } = "";
        public int MfaId { get; set; }
        public string ModelSeries { get; set; } = "";
        public int MsId { get; set; }
    }

    public static string EpcAgentCatalogCachePath()
        => CachePathOverride ?? Path.Combine(DocumentRoot.TrimEnd('/', '\\'), "content", "shop", "docpart", "cache", "epc_agent_catalog_index.json");

    public static string EpcAgentCatalogNormKey(string name)
        => CompactKeep.Replace(name.Trim().ToUpperInvariant(), "");

    public static string EpcAgentCatalogMatchPattern(string name)
    {
        var upper = name.Trim().ToUpperInvariant();
        var escaped = PhpPregQuote(upper, '/');
        escaped = escaped.Replace("\\ ", "\\s+", StringComparison.Ordinal)
            .Replace("\\-", "[-\\s]?", StringComparison.Ordinal)
            .Replace("\\.", "[\\.\\s]?", StringComparison.Ordinal);
        return "/(?:^|[^A-Z0-9])(" + escaped + ")(?:[^A-Z0-9]|$)/i";
    }

    public static Dictionary<string, object?> EpcAgentCatalogBuildIndex(ListStore db)
    {
        var stock = new List<Dictionary<string, object?>>();
        var stockByKey = new Dictionary<string, object?>(StringComparer.Ordinal);
        var priceIds = PhpPlanQ1Text.EpcStockBrandPriceIdsWithStock(db.Text);
        foreach (var row in PhpPlanQ1Text.EpcStockBrandsWithCounts(db.Text, priceIds))
        {
            var name = Str(Field(row, "name")).Trim();
            if (name == "")
            {
                continue;
            }

            var key = EpcAgentCatalogNormKey(name);
            if (key == "")
            {
                continue;
            }

            var entry = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["key"] = key,
                ["parts_count"] = IntVal(Field(row, "parts_count")),
                ["len"] = name.Length
            };
            stock.Add(entry);
            stockByKey[key] = entry;
        }

        stock.Sort(CompareLenName);

        var vehicles = new List<Dictionary<string, object?>>();
        var vehicleByKey = new Dictionary<string, object?>(StringComparer.Ordinal);
        var sectionCounts = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["passenger"] = 0,
            ["commercial"] = 0,
            ["motorbike"] = 0
        };
        try
        {
            if (!db.ManufacturersMissing)
            {
                foreach (var row in db.Manufacturers.OrderBy(r => r.Manufacturer, StringComparer.Ordinal))
                {
                    var name = row.Manufacturer.Trim();
                    var section = row.Section ?? "";
                    if (name == "" || !sectionCounts.ContainsKey(section))
                    {
                        continue;
                    }

                    var key = EpcAgentCatalogNormKey(name);
                    if (key == "")
                    {
                        continue;
                    }

                    if (!vehicleByKey.TryGetValue(key, out var boxed) || boxed is not Dictionary<string, object?> vehicle)
                    {
                        vehicle = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["name"] = name,
                            ["key"] = key,
                            ["sections"] = new List<string>(),
                            ["mfa_ids"] = new Dictionary<string, object?>(StringComparer.Ordinal),
                            ["type"] = row.Type.Trim(),
                            ["country"] = row.Country.Trim(),
                            ["len"] = name.Length
                        };
                        vehicleByKey[key] = vehicle;
                    }

                    var sections = (List<string>)vehicle["sections"]!;
                    if (!sections.Contains(section, StringComparer.Ordinal))
                    {
                        sections.Add(section);
                        sectionCounts[section] = IntVal(sectionCounts[section]) + 1;
                    }

                    ((Dictionary<string, object?>)vehicle["mfa_ids"]!)[section] = row.MfaId;
                }

                vehicles = vehicleByKey.Values.OfType<Dictionary<string, object?>>().ToList();
                vehicles.Sort(CompareLenName);
            }
        }
        catch
        {
            // PHP swallows UMAPI manufacturer query errors.
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["stock"] = stock,
            ["stock_by_key"] = stockByKey,
            ["vehicles"] = vehicles,
            ["vehicle_by_key"] = vehicleByKey,
            ["section_counts"] = sectionCounts,
            ["updated_at"] = Clock()
        };
    }

    public static Dictionary<string, object?> EpcAgentCatalogIndex(ListStore db)
    {
        var now = Clock();
        if (_mem is not null && now - _memAt < 300)
        {
            return _mem;
        }

        var path = EpcAgentCatalogCachePath();
        const long ttl = 21600;
        if (File.Exists(path))
        {
            try
            {
                var raw = File.ReadAllText(path);
                if (raw != "")
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(raw);
                    if (data is not null && !Empty(Field(data, "updated_at")) && now - IntVal(Field(data, "updated_at")) < ttl)
                    {
                        _mem = data;
                        _memAt = now;
                        return data;
                    }
                }
            }
            catch
            {
                // Fall through and rebuild.
            }
        }

        var built = EpcAgentCatalogBuildIndex(db);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(built, CacheJson));
        _mem = built;
        _memAt = now;
        return built;
    }

    public static string EpcAgentCatalogSectionHint(string text)
    {
        var lower = text.ToLowerInvariant();
        if (CommercialHint.IsMatch(lower))
        {
            return "commercial";
        }

        if (MotorbikeHint.IsMatch(lower))
        {
            return "motorbike";
        }

        if (PassengerHint.IsMatch(lower))
        {
            return "passenger";
        }

        return "";
    }

    public static string EpcAgentCatalogSectionLabel(string section)
        => section switch
        {
            "passenger" => "Passenger cars",
            "commercial" => "Commercial vehicles",
            "motorbike" => "Motorcycles",
            _ => section.Length == 0 ? "" : char.ToUpperInvariant(section[0]) + section[1..]
        };

    public static Dictionary<string, object?>? EpcAgentCatalogMatchStock(Dictionary<string, object?> index, string text)
    {
        var upper = text.Trim().ToUpperInvariant();
        foreach (var row in AsDictList(Field(index, "stock")))
        {
            if (PregMatch(EpcAgentCatalogMatchPattern(Str(Field(row, "name"))), upper))
            {
                return row;
            }
        }

        return null;
    }

    public static Dictionary<string, object?>? EpcAgentCatalogMatchVehicle(Dictionary<string, object?> index, string text, string sectionHint = "")
    {
        var upper = text.Trim().ToUpperInvariant();
        foreach (var row in AsDictList(Field(index, "vehicles")))
        {
            if (!PregMatch(EpcAgentCatalogMatchPattern(Str(Field(row, "name"))), upper))
            {
                continue;
            }

            var sections = AsStringList(Field(row, "sections"));
            if (sectionHint != "" && !sections.Contains(sectionHint, StringComparer.Ordinal))
            {
                continue;
            }

            return row;
        }

        return sectionHint != "" ? EpcAgentCatalogMatchVehicle(index, text, "") : null;
    }

    public static Dictionary<string, object?>? EpcAgentCatalogMatchModel(ListStore db, Dictionary<string, object?> vehicle, string text)
    {
        var rawIds = Field(vehicle, "mfa_ids");
        if (Empty(rawIds) || rawIds is not Dictionary<string, object?> mfaIds)
        {
            return null;
        }

        var upper = text.Trim().ToUpperInvariant();
        Dictionary<string, object?>? best = null;
        foreach (var kv in mfaIds)
        {
            var mfaId = IntVal(kv.Value);
            if (mfaId <= 0)
            {
                continue;
            }

            try
            {
                if (db.ModelsMissing)
                {
                    continue;
                }

                foreach (var row in db.Models
                    .Where(m => m.Section == kv.Key && m.MfaId == mfaId)
                    .OrderByDescending(m => m.ModelSeries.Length)
                    .Take(400))
                {
                    var model = row.ModelSeries.Trim();
                    if (model == "" || !PregMatch(EpcAgentCatalogMatchPattern(model), upper))
                    {
                        continue;
                    }

                    var len = model.Length;
                    if (best is null || len > IntVal(Field(best, "len")))
                    {
                        best = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["name"] = model,
                            ["ms_id"] = row.MsId,
                            ["section"] = kv.Key,
                            ["mfa_id"] = mfaId,
                            ["len"] = len
                        };
                    }
                }
            }
            catch
            {
                // PHP swallows model-query errors per section.
            }
        }

        return best;
    }

    public static bool EpcAgentCatalogHasInquiryIntent(string text)
        => InquiryIntent.IsMatch(text);

    public static bool EpcAgentCatalogIsListQuery(string text)
    {
        var lower = text.Trim().ToLowerInvariant();
        return ListLead.IsMatch(lower) && ListConfirm.IsMatch(lower);
    }

    public static Dictionary<string, object?>? EpcAgentCatalogResolveQuery(ListStore db, string text)
    {
        text = text.Trim();
        if (text == "")
        {
            return null;
        }

        if (VinWord.IsMatch(text) && VinToken.IsMatch(text.ToUpperInvariant()))
        {
            return null;
        }

        var index = EpcAgentCatalogIndex(db);
        var sectionHint = EpcAgentCatalogSectionHint(text);
        var stock = EpcAgentCatalogMatchStock(index, text);
        var vehicle = EpcAgentCatalogMatchVehicle(index, text, sectionHint);
        var model = vehicle is not null ? EpcAgentCatalogMatchModel(db, vehicle, text) : null;
        if (stock is null && vehicle is null && !EpcAgentCatalogIsListQuery(text))
        {
            return null;
        }

        if (stock is null && vehicle is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["action"] = "list",
                ["section_hint"] = sectionHint,
                ["index"] = index
            };
        }

        var hasIntent = EpcAgentCatalogHasInquiryIntent(text);
        var nameOnly = NameOnly.IsMatch(text);
        if (!hasIntent && !nameOnly && model is null)
        {
            return null;
        }

        var action = stock is not null && vehicle is not null ? "both" : stock is not null ? "stock_brand" : "vehicle";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["action"] = action,
            ["stock"] = stock,
            ["vehicle"] = vehicle,
            ["model"] = model,
            ["section_hint"] = sectionHint,
            ["index"] = index
        };
    }

    public static string EpcAgentCatalogMatchNameInText(ListStore db, string text, string prefer = "any")
    {
        var index = EpcAgentCatalogIndex(db);
        var sectionHint = EpcAgentCatalogSectionHint(text);
        if (prefer is "stock" or "any")
        {
            var stock = EpcAgentCatalogMatchStock(index, text);
            if (stock is not null)
            {
                return Str(Field(stock, "name"));
            }
        }

        if (prefer is "vehicle" or "any")
        {
            var vehicle = EpcAgentCatalogMatchVehicle(index, text, sectionHint);
            if (vehicle is not null)
            {
                return Str(Field(vehicle, "name"));
            }
        }

        return "";
    }

    private static string PhpPregQuote(string value, char delimiter)
    {
        var special = ".\\+*?[^]$(){}=!<>|:-#";
        var sb = new System.Text.StringBuilder();
        foreach (var ch in value)
        {
            if (special.Contains(ch, StringComparison.Ordinal) || ch == delimiter)
            {
                sb.Append('\\');
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static bool PregMatch(string phpPattern, string text)
    {
        if (phpPattern.Length < 3 || phpPattern[0] != '/')
        {
            return false;
        }

        var last = phpPattern.LastIndexOf('/');
        if (last <= 0)
        {
            return false;
        }

        var body = phpPattern[1..last];
        var flags = phpPattern[(last + 1)..];
        var opts = RegexOptions.CultureInvariant;
        if (flags.Contains('i', StringComparison.Ordinal))
        {
            opts |= RegexOptions.IgnoreCase;
        }

        return Regex.IsMatch(text, body, opts);
    }

    private static int CompareLenName(Dictionary<string, object?> a, Dictionary<string, object?> b)
    {
        var len = IntVal(Field(b, "len")).CompareTo(IntVal(Field(a, "len")));
        return len != 0 ? len : string.Compare(Str(Field(a, "name")), Str(Field(b, "name")), StringComparison.OrdinalIgnoreCase);
    }

    private static List<Dictionary<string, object?>> AsDictList(object? value)
    {
        if (value is List<Dictionary<string, object?>> list)
        {
            return list;
        }

        if (value is IEnumerable<object?> seq)
        {
            return seq.OfType<Dictionary<string, object?>>().ToList();
        }

        return [];
    }

    private static List<string> AsStringList(object? value)
        => value switch
        {
            List<string> list => list,
            IEnumerable<object?> seq => seq.Select(Str).ToList(),
            _ => []
        };

    private static object? Field(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private static string Str(object? value)
        => value switch
        {
            null => "",
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? "",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };

    private static int IntVal(object? value)
    {
        if (value is int i)
        {
            return i;
        }

        if (value is long l)
        {
            return (int)l;
        }

        if (value is JsonElement je && je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var n))
        {
            return n;
        }

        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static bool Empty(object? value)
        => value switch
        {
            null => true,
            bool b => !b,
            int i => i == 0,
            long l => l == 0,
            string s => s is "" or "0",
            System.Collections.ICollection c => c.Count == 0,
            JsonElement je => je.ValueKind is JsonValueKind.Null or JsonValueKind.False
                || (je.ValueKind == JsonValueKind.String && je.GetString() is "" or "0")
                || (je.ValueKind == JsonValueKind.Number && je.GetDouble() == 0)
                || (je.ValueKind == JsonValueKind.Array && je.GetArrayLength() == 0)
                || (je.ValueKind == JsonValueKind.Object && !je.EnumerateObject().Any()),
            _ => Str(value) is "" or "0"
        };
}
