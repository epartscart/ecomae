using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Shared supplier PDF catalog store (KYB, NPR, Teikin, Denso, ...). Each catalog
/// is a directory <see cref="RootDir"/>/<id>/ holding catalog.pdf + catalog.json
/// produced by scripts/import_supplier_catalog.py — page-level part/OEM/make
/// tokens for search plus the PDF the browser can open at any page.
/// Global data (not per tenant): same aftermarket catalogs everywhere.
/// </summary>
public static class StorefrontSupplierCatalog
{
    public const string ApiPath = "/api/supplier_catalogs.php";
    public const string FilePrefix = "/supplier-catalogs/";

    private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9\\-]{0,80}$", RegexOptions.Compiled);

    public sealed class SupplierCatalogMeta
    {
        public string Id { get; set; } = "";
        public string Supplier { get; set; } = "";
        public string Title { get; set; } = "";
        public string File { get; set; } = "";
        public int Pages { get; set; }
        public string CatNo { get; set; } = "";
        public string Version { get; set; } = "";
        public string Edition { get; set; } = "";
        public int ItemCount { get; set; }
        public string Imported { get; set; } = "";
    }

    public sealed class SupplierCatalogPage
    {
        public int N { get; set; }
        public List<string> Refs { get; set; } = new();
        public List<string> Oems { get; set; } = new();
        public List<string> Makes { get; set; } = new();
        public string Excerpt { get; set; } = "";
    }

    public sealed class SupplierCatalogDoc
    {
        public SupplierCatalogMeta Meta { get; set; } = new();
        public List<SupplierCatalogPage> Pages { get; set; } = new();
    }

    public static string RootDir
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("ECOMAE_SUPPLIER_CATALOG_DIR");
            var candidates = new[]
            {
                env,
                Path.Combine(AppContext.BaseDirectory, "supplier_catalogs"),
                "/var/www/ecomae-aspnet/shared/supplier_catalogs",
                Path.Combine(Directory.GetCurrentDirectory(), "supplier_catalogs"),
            };
            foreach (var c in candidates)
            {
                if (!string.IsNullOrWhiteSpace(c) && Directory.Exists(c))
                {
                    return c;
                }
            }
            return candidates[1]!;
        }
    }

    public static List<SupplierCatalogMeta> ListCatalogs()
    {
        var metas = new List<SupplierCatalogMeta>();
        foreach (var dir in Directory.EnumerateDirectories(RootDir))
        {
            var doc = Load(Path.GetFileName(dir));
            if (doc is not null)
            {
                metas.Add(doc.Meta);
            }
        }
        return metas.OrderBy(m => m.Supplier, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Edition, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static SupplierCatalogDoc? Load(string id)
    {
        if (!IdPattern.IsMatch(id))
        {
            return null;
        }
        var path = Path.Combine(RootDir, id, "catalog.json");
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            var json = File.ReadAllText(path);
            var doc2 = JsonSerializer.Deserialize<SupplierCatalogDoc>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (doc2?.Meta is null)
            {
                return null;
            }
            doc2.Meta.Id = id;
            return doc2;
        }
        catch
        {
            return null;
        }
    }

    private static string Norm(string s)
        => Regex.Replace(s.ToUpperInvariant(), "[^A-Z0-9]", "");

    public static IResult Api(HttpContext context)
    {
        var action = context.Request.Query["action"].ToString();
        var id = context.Request.Query["catalog_id"].ToString();
        if (string.IsNullOrWhiteSpace(id))
        {
            id = context.Request.Query["id"].ToString();
        }
        object payload = action switch
        {
            "catalog" => CatalogPayload(id),
            "page" => PagePayload(id, context.Request.Query["page"].ToString()),
            "search" => SearchPayload(id, context.Request.Query["q"].ToString()),
            _ => ListPayload(),
        };
        return Results.Json(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }

    private static object ListPayload()
    {
        var metas = ListCatalogs();
        return new
        {
            success = true,
            data = metas.Select(m => new
            {
                m.Id, m.Supplier, m.Title, m.CatNo, m.Version, m.Edition,
                m.Pages, m.ItemCount, m.Imported,
                searchable = m.ItemCount > 0,
                url = FilePrefix + m.File,
            }).ToList(),
        };
    }

    private static object CatalogPayload(string id)
    {
        var doc = Load(id);
        if (doc is null)
        {
            return new { success = false, error = "catalog not found" };
        }
        var m = doc.Meta;
        return new
        {
            success = true,
            data = new
            {
                m.Id, m.Supplier, m.Title, m.CatNo, m.Version, m.Edition,
                m.Pages, m.ItemCount, m.Imported,
                searchable = m.ItemCount > 0,
                url = FilePrefix + m.File,
            },
        };
    }

    private static object PagePayload(string id, string pageRaw)
    {
        var doc = Load(id);
        if (doc is null || !int.TryParse(pageRaw, out var n) || n < 1 || n > doc.Pages.Count)
        {
            return new { success = false, error = "page not found" };
        }
        var p = doc.Pages[n - 1];
        return new
        {
            success = true,
            data = new
            {
                page = p.N, p.Refs, p.Oems, p.Makes, p.Excerpt,
                pdf = FilePrefix + doc.Meta.File,
            },
        };
    }

    private static object SearchPayload(string id, string q)
    {
        q = (q ?? string.Empty).Trim();
        if (q.Length < 2)
        {
            return new { success = false, error = "query too short", data = Array.Empty<object>() };
        }
        var docs = string.IsNullOrWhiteSpace(id)
            ? ListCatalogs().Select(m => Load(m.Id)).Where(d => d is not null).ToList()
            : new List<SupplierCatalogDoc?> { Load(id) };
        var qNorm = Norm(q);
        var hits = new List<object>();
        const int maxHits = 120;
        foreach (var doc in docs)
        {
            if (doc is null || hits.Count >= maxHits)
            {
                continue;
            }
            foreach (var p in doc.Pages)
            {
                if (hits.Count >= maxHits)
                {
                    break;
                }
                var refHit = p.Refs.Where(r => Norm(r).Contains(qNorm)).Take(8).ToList();
                var oemHit = p.Oems.Where(o => Norm(o).Contains(qNorm)).Take(8).ToList();
                var makeHit = p.Makes.Any(mk => mk.Contains(qNorm, StringComparison.OrdinalIgnoreCase));
                var textHit = p.Excerpt.Contains(q, StringComparison.OrdinalIgnoreCase);
                if (refHit.Count == 0 && oemHit.Count == 0 && !makeHit && !textHit)
                {
                    continue;
                }
                hits.Add(new
                {
                    catalog = doc.Meta.Id,
                    doc.Meta.Supplier,
                    doc.Meta.Title,
                    doc.Meta.CatNo,
                    doc.Meta.Edition,
                    page = p.N,
                    refs = refHit,
                    oems = oemHit,
                    p.Makes,
                    p.Excerpt,
                    pdf = FilePrefix + doc.Meta.File,
                });
            }
        }
        return new { success = true, data = hits, q };
    }

    public static IResult ServeFile(HttpContext context, string id, string file)
    {
        if (!IdPattern.IsMatch(id) || file != "catalog.pdf")
        {
            return Results.NotFound();
        }
        var path = Path.Combine(RootDir, id, "catalog.pdf");
        return !File.Exists(path)
            ? Results.NotFound()
            : Results.File(path, "application/pdf", enableRangeProcessing: true);
    }
}
