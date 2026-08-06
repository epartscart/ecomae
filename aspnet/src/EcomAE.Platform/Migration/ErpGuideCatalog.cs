using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomAE.Platform.Migration;

/// <summary>
/// PHP <c>epc_guide_modules()</c> catalog loaded from <c>Migration/Data/erp-guide-modules.json</c>.
/// </summary>
public static class ErpGuideCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Lazy<IReadOnlyList<ErpGuideModuleRecord>> Modules = new(Load);

    public static IReadOnlyList<ErpGuideModuleRecord> All => Modules.Value;

    public static ErpGuideModuleRecord? Get(string module)
    {
        if (string.IsNullOrWhiteSpace(module))
        {
            return null;
        }

        var key = module.Trim();
        foreach (var row in Modules.Value)
        {
            if (row.Module.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    public static IReadOnlyList<ErpGuideModuleRecord> Search(string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return All;
        }

        var needle = q.Trim();
        var hits = new List<ErpGuideModuleRecord>();
        foreach (var row in Modules.Value)
        {
            if (row.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || row.Module.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || row.What.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(row);
            }
        }

        return hits;
    }

    private static IReadOnlyList<ErpGuideModuleRecord> Load()
    {
        var path = ResolveJsonPath();
        if (path is null || !File.Exists(path))
        {
            return Array.Empty<ErpGuideModuleRecord>();
        }

        using var stream = File.OpenRead(path);
        var map = JsonSerializer.Deserialize<Dictionary<string, ErpGuideModuleDto>>(stream, JsonOptions);
        if (map is null || map.Count == 0)
        {
            return Array.Empty<ErpGuideModuleRecord>();
        }

        var rows = new List<ErpGuideModuleRecord>(map.Count);
        foreach (var (key, dto) in map)
        {
            if (dto is null)
            {
                continue;
            }

            var module = string.IsNullOrWhiteSpace(dto.Module) ? key : dto.Module.Trim();
            rows.Add(new ErpGuideModuleRecord(
                module,
                dto.Title?.Trim() ?? module,
                dto.What?.Trim() ?? string.Empty,
                dto.Setup ?? Array.Empty<string>(),
                dto.Daily ?? Array.Empty<string>(),
                dto.Accounting?.Trim() ?? string.Empty,
                dto.Tips ?? Array.Empty<string>()));
        }

        rows.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
        return rows;
    }

    private static string? ResolveJsonPath()
    {
        const string relative = "Migration/Data/erp-guide-modules.json";
        var fileName = "erp-guide-modules.json";
        var candidates = new List<string>();

        void AddBase(string? root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            candidates.Add(Path.Combine(root, relative));
            candidates.Add(Path.Combine(root, fileName));
            candidates.Add(Path.Combine(root, "aspnet", "src", "EcomAE.Platform", relative));
        }

        AddBase(AppContext.BaseDirectory);

        try
        {
            var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            AddBase(asmDir);
        }
        catch
        {
            // ignore assembly path failures
        }

        AddBase(Directory.GetCurrentDirectory());

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                candidates.Add(Path.Combine(dir.FullName, relative));
                candidates.Add(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", relative));
                dir = dir.Parent;
            }
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private sealed class ErpGuideModuleDto
    {
        [JsonPropertyName("module")]
        public string? Module { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("what")]
        public string? What { get; set; }

        [JsonPropertyName("setup")]
        public string[]? Setup { get; set; }

        [JsonPropertyName("daily")]
        public string[]? Daily { get; set; }

        [JsonPropertyName("accounting")]
        public string? Accounting { get; set; }

        [JsonPropertyName("tips")]
        public string[]? Tips { get; set; }
    }
}

/// <summary>One ERP guide module (PHP <c>epc_guide_entry</c> shape).</summary>
public sealed record ErpGuideModuleRecord(
    string Module,
    string Title,
    string What,
    IReadOnlyList<string> Setup,
    IReadOnlyList<string> Daily,
    string Accounting,
    IReadOnlyList<string> Tips);
