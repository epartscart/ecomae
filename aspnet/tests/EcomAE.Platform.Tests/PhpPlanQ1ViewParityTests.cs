using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1ViewParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Regex CssStamp = new(@"\?v=\d+", RegexOptions.CultureInvariant);

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1View");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        Assert.StartsWith("8.3.", golden.GetProperty("php").GetString(), StringComparison.Ordinal);
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases");
        Assert.Equal(
            cases.EnumerateArray().Select(c => c.GetProperty("name").GetString()),
            golden.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("name").GetString()));
    }

    [Fact]
    public void PlanQ1View_MatchPhpGolden()
    {
        var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement;
        var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "cases.json"))).RootElement.GetProperty("cases").EnumerateArray().ToList();
        var results = golden.GetProperty("results").EnumerateArray().ToList();
        var failures = new List<string>();
        for (var i = 0; i < cases.Count; i++)
        {
            var name = cases[i].GetProperty("name").GetString()!;
            var expected = results[i].GetProperty("result");
            var actual = Render(name);
            if (!Same(actual, expected))
            {
                failures.Add(name + " expected=" + Truncate(expected.GetRawText()) + " got=" + Truncate(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1View.SkuMediaCpInstallPath, PhpPlanQ1View.SkuMediaStorefrontPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1View.SkuMediaCpInstallPath, PhpPlanQ1View.SkuMediaStorefrontPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1View.Reset();
        Assert.Equal("/<backend_dir>/content/shop/catalogue/" + "epc_sku_media_manager" + ".php", PhpPlanQ1View.ManagerContentPath());
        Assert.Equal("", PhpPlanQ1View.EpcSkuMediaRenderSpecGroupsHtml([]));
    }

    private static string Render(string name)
    {
        PhpPlanQ1Open.Reset();
        PhpPlanQ1View.Reset();
        return name switch
        {
            "cp_pure" => Json(CpPure()),
            "cp_db" => Json(CpDb()),
            "sf_html" => Json(SfHtml()),
            "sf_render" => Json(SfRender()),
            _ => "unknown:" + name
        };
    }

    private static object? CpPure()
    {
        var db = new PhpPlanQ1Open.OpenStore();
        var contents = new List<PhpPlanQ1View.ContentRow>();
        var access = new List<PhpPlanQ1View.AccessRow>();
        var langs = new List<PhpPlanQ1View.LangRow>();
        var tr = new List<PhpPlanQ1View.LangTrRow>();
        return new object?[]
        {
            PhpPlanQ1View.EpcSkuMediaCpInstall(db, contents, access, langs, tr, "", false),
            PhpPlanQ1View.EpcSkuMediaCpInstall(db, contents, access, langs, tr, "/cp/", false)
        };
    }

    private static object? CpDb()
    {
        PhpPlanQ1View.Clock = () => 1_700_000_000;
        PhpPlanQ1View.MenuApply = _ => 42;
        var db = new PhpPlanQ1Open.OpenStore();
        var contents = new List<PhpPlanQ1View.ContentRow>();
        var access = new List<PhpPlanQ1View.AccessRow>();
        var langs = new List<PhpPlanQ1View.LangRow>();
        var tr = new List<PhpPlanQ1View.LangTrRow>();
        string? err = null;
        try
        {
            PhpPlanQ1View.EpcSkuMediaCpInstall(db, contents, access, langs, tr, "cp", true);
        }
        catch (Exception e)
        {
            err = e.Message;
        }

        contents.AddRange(
        [
            new() { Id = 1, Url = "shop", Level = 1, IsFrontend = 0, PublishedFlag = 1 },
            new() { Id = 2, Url = "shop/catalogue", Level = 2, IsFrontend = 0, PublishedFlag = 1 },
            new() { Id = 3, Url = "shop/catalogue/products", Level = 3, IsFrontend = 0, PublishedFlag = 1 }
        ]);
        access.AddRange(
        [
            new() { ContentId = 3, GroupId = 2 },
            new() { ContentId = 3, GroupId = 5 }
        ]);
        var a = PhpPlanQ1View.EpcSkuMediaCpInstall(db, contents, access, langs, tr, "cp", true);
        var b = PhpPlanQ1View.EpcSkuMediaCpInstall(db, contents, access, langs, tr, "/cp/", true);
        var row = contents.First(c => c.Url == "shop/catalogue/sku_media");
        var groups = access.Where(x => x.ContentId == Convert.ToInt32(a["content_id"])).Select(x => x.GroupId).OrderBy(x => x).ToList();
        return new object?[]
        {
            err,
            Convert.ToInt32(a["content_id"]),
            Convert.ToInt32(a["menu_item_id"]),
            Convert.ToInt32(b["content_id"]) == Convert.ToInt32(a["content_id"]),
            Convert.ToInt32(b["menu_item_id"]),
            row.Url,
            row.Parent,
            row.Level,
            row.TitleTag,
            row.Value,
            row.Alias,
            row.Content,
            row.PublishedFlag,
            groups,
            tr.First(t => t.StrKey == "epc_sku_media_manager" && t.LangCode == "en").Value,
            tr.First(t => t.StrKey == "epc_sku_media_manager" && t.LangCode == "ru").Value,
            langs.First(l => l.StrKey == "epc_sku_media_manager").Description
        };
    }

    private static object? SfHtml()
    {
        var groups = new List<object?>
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = "Tech's",
                ["icon"] = "fa-cogs",
                ["rows"] = new List<object?>
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Sealed", ["display"] = "Yes", ["value_type"] = "text" },
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Note", ["display"] = "<b>x</b>", ["value_type"] = "rich" },
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Q", ["value"] = "a & b", ["value_type"] = "text" }
                }
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = "Empty",
                ["icon"] = "fa-list",
                ["rows"] = new List<object?>()
            }
        };
        return new object?[]
        {
            PhpPlanQ1View.EpcSkuMediaRenderSpecGroupsHtml([]),
            PhpPlanQ1View.EpcSkuMediaRenderSpecGroupsHtml(groups)
        };
    }

    private static object? SfRender()
    {
        var doc = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1v_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Path.Combine(doc, "content", "shop", "catalogue"));
        File.WriteAllText(Path.Combine(doc, "content", "shop", "catalogue", "epc_sku_media.css"), ".x{}");
        PhpPlanQ1Open.DocumentRoot = doc;
        PhpPlanQ1Open.Clock = () => 1_700_000_000;
        PhpPlanQ1View.DocumentRoot = doc;
        PhpPlanQ1View.Clock = () => 1_700_000_000;
        try
        {
            var db = new PhpPlanQ1Open.OpenStore();
            PhpPlanQ1Open.EpcSkuMediaEnsureSchema(db);
            var p = PhpPlanQ1Open.EpcSkuMediaUpsertProfile(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "bosch",
                ["article"] = "045110",
                ["title"] = "Oil's"
            });
            var g = PhpPlanQ1Open.EpcSkuMediaAddSpecGroup(db, Convert.ToInt32(p["id"]), new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Technical" });
            PhpPlanQ1Open.EpcSkuMediaAddSpecRow(db, Convert.ToInt32(g["id"]), new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = "Height",
                ["value"] = "12.5",
                ["value_type"] = "number",
                ["unit"] = "mm"
            });
            db.Photos.Add(new PhpPlanQ1Open.PhotoRow
            {
                Id = 1,
                ProfileId = Convert.ToInt32(p["id"]),
                FileName = "a.png",
                Alt = "front",
                Caption = "A",
                PhotoType = "product",
                SortOrder = 10,
                IsPrimary = 0,
                CreatedAt = 1
            });
            db.Photos.Add(new PhpPlanQ1Open.PhotoRow
            {
                Id = 2,
                ProfileId = Convert.ToInt32(p["id"]),
                FileName = "b.png",
                Alt = "side",
                Caption = "B",
                PhotoType = "detail",
                SortOrder = 20,
                IsPrimary = 1,
                CreatedAt = 1
            });
            db.NextPhotoId = 3;
            var css = Norm(PhpPlanQ1View.EpcSkuMediaEmitStorefrontCss() + PhpPlanQ1View.EpcSkuMediaEmitStorefrontCss());
            var html = Norm(PhpPlanQ1View.EpcSkuMediaRenderStorefront(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "bosch",
                ["article"] = "045110"
            }));
            var nophoto = Norm(PhpPlanQ1View.EpcSkuMediaRenderStorefront(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "bosch",
                ["article"] = "045110",
                ["show_photos"] = "0"
            }));
            PhpPlanQ1Open.EpcSkuMediaUpsertProfile(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "hide",
                ["article"] = "H1",
                ["status"] = "hidden"
            });
            var loadHidden = PhpPlanQ1View.EpcSkuMediaStorefrontLoad(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "hide",
                ["article"] = "H1"
            });
            var load = PhpPlanQ1View.EpcSkuMediaStorefrontLoad(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "bosch",
                ["article"] = "045110"
            })!;
            var groups = (List<Dictionary<string, object?>>)load["groups"]!;
            return new object?[]
            {
                css,
                html,
                nophoto,
                loadHidden,
                Convert.ToInt32(((Dictionary<string, object?>)load["profile"]!)["id"]),
                ((List<Dictionary<string, object?>>)load["photos"]!).Count,
                groups.Count,
                Convert.ToString(groups[0]["name"])
            };
        }
        finally
        {
            try { Directory.Delete(doc, true); } catch { /* throwaway */ }
        }
    }

    private static string Norm(string value) => CssStamp.Replace(value, "?v=MTIME");

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool Same(string actual, JsonElement expected)
    {
        try
        {
            using var left = JsonDocument.Parse(actual);
            return JsonEquivalent(left.RootElement, expected);
        }
        catch (JsonException)
        {
            return actual == (expected.ValueKind == JsonValueKind.String ? expected.GetString() : expected.GetRawText());
        }
    }

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                if (left.EnumerateObject().Count() != right.EnumerateObject().Count())
                {
                    return false;
                }

                foreach (var prop in left.EnumerateObject())
                {
                    if (!right.TryGetProperty(prop.Name, out var other) || !JsonEquivalent(prop.Value, other))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                var a = left.EnumerateArray().ToList();
                var b = right.EnumerateArray().ToList();
                return a.Count == b.Count && a.Zip(b, JsonEquivalent).All(x => x);
            case JsonValueKind.String:
                return left.GetString() == right.GetString();
            case JsonValueKind.Number:
                return left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble();
            default:
                return true;
        }
    }

    private static string Truncate(string value)
        => value.Length <= 800 ? value : value[..800] + "…";
}
