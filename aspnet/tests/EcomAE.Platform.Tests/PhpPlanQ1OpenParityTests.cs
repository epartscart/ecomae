using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1OpenParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Regex SkuFile = new(@"sku_\d+_\d+_[a-f0-9]+\.", RegexOptions.CultureInvariant);

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Open");

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
    public void PlanQ1Open_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Open.StorageFlagsPath, PhpPlanQ1Open.SkuMediaPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Open.StorageFlagsPath, PhpPlanQ1Open.SkuMediaPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Open.Reset();
        Assert.Contains("storefront_temp_disabled", PhpPlanQ1Open.EpcSsfStorageActiveSql(), StringComparison.Ordinal);
        Assert.Equal("AB123", PhpPlanQ1Open.EpcSkuMediaNormalizeArticle(" ab-12.3 "));
    }

    private static string Render(string name)
    {
        PhpPlanQ1Open.Reset();
        return name switch
        {
            "ssf_pure" => Json(SsfPure()),
            "ssf_db" => Json(SsfDb()),
            "sku_pure" => Json(SkuPure()),
            "sku_db" => Json(SkuDb()),
            _ => "unknown:" + name
        };
    }

    private static object? SsfPure()
        => new object?[]
        {
            PhpPlanQ1Open.EpcSsfStorageActiveSql(),
            PhpPlanQ1Open.EpcSsfStorageActiveSql("s"),
            PhpPlanQ1Open.EpcSsfPriceDataActiveSql(),
            PhpPlanQ1Open.EpcSsfPriceDataActiveSql("d")
        };

    private static object? SsfDb()
    {
        var db = SeedSsf();
        PhpPlanQ1Open.EpcSsfEnsureSchema(db, true);
        var labels = PhpPlanQ1Open.EpcSsfDisabledWarehouseLabels(db).Keys.ToList();
        var sids = PhpPlanQ1Open.EpcSsfDisabledStorageIds(db).Keys.ToList();
        var pids = PhpPlanQ1Open.EpcSsfDisabledPriceIds(db).Keys.ToList();
        var rows = PhpPlanQ1Open.EpcSsfCpListRows(db);
        var bad = PhpPlanQ1Open.EpcSsfSetToggle(db, "storage", 0, 1);
        var miss = PhpPlanQ1Open.EpcSsfSetToggle(db, "storage", 99, 1);
        var t2 = PhpPlanQ1Open.EpcSsfSetToggle(db, "storage", 2, 1, 7, "ops");
        var t12 = PhpPlanQ1Open.EpcSsfSetToggle(db, "price_list", 12, 1, 8, "ops");
        var filt = PhpPlanQ1Open.EpcSsfFilterEnabledPriceIds(db, [10, 11, 12, 13, 0, "10", 11]);
        var lines = PhpPlanQ1Open.EpcSsfFilterAgentStockLines(db,
        [
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["price_id"] = 10, ["warehouse"] = "PW" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["price_id"] = 11, ["storage"] = "EW" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["price_id"] = 13, ["warehouse"] = "X" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["price_id"] = 0, ["warehouse"] = "CW" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["price_id"] = 11, ["warehouse"] = "cw" },
            "skip",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["price_id"] = 11, ["warehouse"] = "EW" }
        ]);
        var bunches = PhpPlanQ1Open.EpcSsfFilterOfficeStorageBunches(db,
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["protocol_version"] = 3,
                ["office_storage_bunches"] = new List<object?>
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["storage_id"] = 1 },
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["storage_id"] = 3 },
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["storage_id"] = 5 }
                }
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["protocol_version"] = 3,
                ["office_storage_bunches"] = new List<object?>
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["storage_id"] = 3 }
                }
            },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["protocol_version"] = "server", ["storage_id"] = 3 },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["storage_id"] = 3 },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["storage_id"] = 1 },
            "skip"
        ]);
        return new object?[]
        {
            labels, sids, pids, rows, bad, miss, t2, t12,
            PhpPlanQ1Open.EpcSsfIsStorageDisabled(db, 2),
            PhpPlanQ1Open.EpcSsfIsStorageDisabled(db, 1),
            PhpPlanQ1Open.EpcSsfIsStorageDisabled(db, 0),
            PhpPlanQ1Open.EpcSsfIsPriceDisabled(db, 10),
            PhpPlanQ1Open.EpcSsfIsPriceDisabled(db, 13),
            PhpPlanQ1Open.EpcSsfStorageDisabledByPrice(db, 10),
            PhpPlanQ1Open.EpcSsfStorageDisabledByPrice(db, 11),
            PhpPlanQ1Open.EpcSsfStorageDisabledByPrice(db, 13),
            filt, lines, bunches, db.Audits.Count
        };
    }

    private static object? SkuPure()
    {
        var types = PhpPlanQ1Open.EpcSkuMediaDefaultSpecTypes();
        return new object?[]
        {
            PhpPlanQ1Open.EpcSkuMediaNormalizeArticle(" ab-12.3 "),
            PhpPlanQ1Open.EpcSkuMediaNormalizeArticle(""),
            PhpPlanQ1Open.EpcSkuMediaNormalizeBrand("  bosch   parts "),
            PhpPlanQ1Open.EpcSkuMediaNormalizeBrand(""),
            PhpPlanQ1Open.EpcSkuMediaPhotoTypes().Keys.ToList(),
            PhpPlanQ1Open.EpcSkuMediaValueTypes().Keys.ToList(),
            types.Count,
            types[0]["code"],
            PhpPlanQ1Open.EpcSkuMediaImagesDir(),
            PhpPlanQ1Open.EpcSkuMediaPhotoUrl("a.png"),
            PhpPlanQ1Open.EpcSkuMediaPhotoUrl("../x.png"),
            PhpPlanQ1Open.EpcSkuMediaPhotoUrl(""),
            PhpPlanQ1Open.EpcSkuMediaStorefrontPartUrl("Bosch", "045 110"),
            PhpPlanQ1Open.EpcSkuMediaStorefrontPartUrl("", "045110"),
            PhpPlanQ1Open.EpcSkuMediaStorefrontPartUrl("Bosch", ""),
            PhpPlanQ1Open.EpcSkuMediaStorefrontPartUrl("Bosch", "045110", "/"),
            PhpPlanQ1Open.EpcSkuMediaFormatValue("1", "bool", "mm"),
            PhpPlanQ1Open.EpcSkuMediaFormatValue("0", "bool"),
            PhpPlanQ1Open.EpcSkuMediaFormatValue("yes", "bool"),
            PhpPlanQ1Open.EpcSkuMediaFormatValue("a, b, ,c", "list", "pcs"),
            PhpPlanQ1Open.EpcSkuMediaFormatValue("<b>x</b>", "rich", "mm"),
            PhpPlanQ1Open.EpcSkuMediaFormatValue("12.5", "number", "mm")
        };
    }

    private static object? SkuDb()
    {
        var doc = Path.Combine(Path.GetTempPath(), "ecomae_cpw_q1o_" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(Path.Combine(doc, "content", "files", "images", "sku_media"));
        Directory.CreateDirectory(Path.Combine(doc, "content", "files", "images"));
        var png = Path.Combine(doc, "seed.png");
        File.WriteAllBytes(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        PhpPlanQ1Open.DocumentRoot = doc;
        PhpPlanQ1Open.Clock = () => 1_700_000_000;
        PhpPlanQ1Open.RandomBytes4 = () => [0xab, 0xcd, 0xef, 0x01];
        try
        {
            var db = SeedSku();
            PhpPlanQ1Open.EpcSkuMediaEnsureSchema(db);
            var p1 = PhpPlanQ1Open.EpcSkuMediaUpsertProfile(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "bosch",
                ["article"] = "045 110",
                ["title"] = "Oil filter"
            });
            var p1b = PhpPlanQ1Open.EpcSkuMediaEnsureFromIdentity(db, "BOSCH", "045 110");
            var p2 = PhpPlanQ1Open.EpcSkuMediaUpsertProfile(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["product_id"] = 7,
                ["brand"] = "BOSCH",
                ["article"] = "045110",
                ["title"] = "Linked"
            });
            var g = PhpPlanQ1Open.EpcSkuMediaAddSpecGroup(db, Convert.ToInt32(p1["id"]), new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Technical" });
            var r1 = PhpPlanQ1Open.EpcSkuMediaAddSpecRow(db, Convert.ToInt32(g["id"]), new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = "Sealed",
                ["value_type"] = "bool",
                ["value"] = "yes"
            });
            var r2 = PhpPlanQ1Open.EpcSkuMediaAddSpecRow(db, Convert.ToInt32(g["id"]), new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = "Height",
                ["value_type"] = "number",
                ["value"] = "12.5",
                ["unit"] = "mm"
            });
            var r3 = PhpPlanQ1Open.EpcSkuMediaAddSpecRow(db, Convert.ToInt32(g["id"]), new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["label"] = "Fits",
                ["value_type"] = "list",
                ["value"] = "a, b, ,c"
            });
            var upd = PhpPlanQ1Open.EpcSkuMediaUpdateSpecRow(db, Convert.ToInt32(r1["id"]), new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["value"] = "0",
                ["value_type"] = "bool"
            });
            var badg = PhpPlanQ1Open.EpcSkuMediaAddSpecGroup(db, 0, new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "X" });
            var badl = PhpPlanQ1Open.EpcSkuMediaAddSpecRow(db, Convert.ToInt32(g["id"]), new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "" });
            var att = PhpPlanQ1Open.EpcSkuMediaAttachLocalPhoto(db, Convert.ToInt32(p1["id"]), png, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["alt"] = "front",
                ["photo_type"] = "detail",
                ["is_primary"] = 1
            });
            att["file_name"] = SkuFile.Replace(Convert.ToString(att["file_name"]) ?? "", "sku_N_T_HEX.");
            att["url"] = SkuFile.Replace(Convert.ToString(att["url"]) ?? "", "sku_N_T_HEX.");
            var uph = PhpPlanQ1Open.EpcSkuMediaUpdatePhoto(db, Convert.ToInt32(att["id"]), new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["caption"] = "Front view",
                ["photo_type"] = "nope"
            });
            var payload = PhpPlanQ1Open.EpcSkuMediaFullPayload(db, Convert.ToInt32(p1["id"]))!;
            var prof = (Dictionary<string, object?>)payload["profile"]!;
            payload["profile"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = Convert.ToInt32(prof["id"]),
                ["brand"] = prof["brand"],
                ["article"] = prof["article"],
                ["article_key"] = prof["article_key"],
                ["title"] = prof["title"],
                ["status"] = prof["status"],
                ["storefront_url"] = prof["storefront_url"]
            };
            payload["photos"] = ((List<Dictionary<string, object?>>)payload["photos"]!).Select(ph => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = Convert.ToInt32(ph["id"]),
                ["alt"] = ph["alt"],
                ["caption"] = ph["caption"],
                ["photo_type"] = ph["photo_type"],
                ["is_primary"] = Convert.ToInt32(ph["is_primary"]),
                ["url"] = SkuFile.Replace(Convert.ToString(ph["url"]) ?? "", "sku_N_T_HEX.")
            }).ToList();
            payload["spec_groups"] = ((List<Dictionary<string, object?>>)payload["spec_groups"]!).Select(group => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = group["name"],
                ["code"] = group["code"],
                ["icon"] = group["icon"],
                ["rows"] = ((List<Dictionary<string, object?>>)group["rows"]!).Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["label"] = r["label"],
                    ["value"] = r["value"],
                    ["value_type"] = r["value_type"],
                    ["unit"] = r["unit"],
                    ["display"] = r["display"]
                }).ToList()
            }).ToList();
            payload.Remove("photo_types");
            payload.Remove("value_types");
            payload.Remove("default_spec_types");
            var look = PhpPlanQ1Open.EpcSkuMediaPublicLookup(db, "bosch", "045 110");
            look["url"] = SkuFile.Replace(Convert.ToString(look["url"]) ?? "", "sku_N_T_HEX.");
            look["photos"] = ((List<Dictionary<string, object?>>)look["photos"]!).Select(ph =>
            {
                ph["url"] = SkuFile.Replace(Convert.ToString(ph["url"]) ?? "", "sku_N_T_HEX.");
                return ph;
            }).ToList();
            var lib = PhpPlanQ1Open.EpcSkuMediaSearchLibrary(db, "045", 20).Select(row => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["source"] = row["source"],
                ["brand"] = row["brand"],
                ["article"] = row["article"],
                ["title"] = row["title"],
                ["warehouse"] = row.TryGetValue("warehouse", out var wh) ? wh : "",
                ["has_profile"] = Convert.ToBoolean(row["has_profile"]),
                ["status"] = row["status"],
                ["product_id"] = Convert.ToInt32(row["product_id"] ?? 0)
            }).ToList();
            var noupload = PhpPlanQ1Open.EpcSkuMediaAddPhoto(db, Convert.ToInt32(p1["id"]), new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tmp_name"] = "",
                ["name"] = "x.png"
            });
            var missp = PhpPlanQ1Open.EpcSkuMediaFindProfile(db, 0, 0, "NOPE", "ZZZ");
            PhpPlanQ1Open.EpcSkuMediaUpsertProfile(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["brand"] = "hidden",
                ["article"] = "H1",
                ["status"] = "hidden"
            });
            var hidelook = PhpPlanQ1Open.EpcSkuMediaPublicLookup(db, "hidden", "H1");
            var delr = PhpPlanQ1Open.EpcSkuMediaDeleteSpecRow(db, Convert.ToInt32(r3["id"]));
            var delg = PhpPlanQ1Open.EpcSkuMediaDeleteSpecGroup(db, Convert.ToInt32(g["id"]));
            var delph = PhpPlanQ1Open.EpcSkuMediaDeletePhoto(db, Convert.ToInt32(att["id"]));
            var delp = PhpPlanQ1Open.EpcSkuMediaDeleteProfile(db, Convert.ToInt32(p1["id"]));
            return new object?[]
            {
                Convert.ToInt32(p1["id"]),
                Convert.ToInt32(p1b["id"]) == Convert.ToInt32(p1["id"]),
                Convert.ToInt32(p2["id"]) == Convert.ToInt32(p1["id"]),
                g["ok"], r1["ok"], r2["ok"], upd, badg, badl, att["ok"], att["file_name"], uph,
                payload, look["ok"], look["url"], look["specs"], lib, noupload, missp, hidelook["profile"],
                delr, delg, delph, delp,
                PhpPlanQ1Open.EpcSkuMediaResolveForProduct(db, 0, "", ""),
                PhpPlanQ1Open.EpcSkuMediaPhotos(db, 0)
            };
        }
        finally
        {
            try { Directory.Delete(doc, true); } catch { /* throwaway */ }
        }
    }

    private static PhpPlanQ1Open.OpenStore SeedSsf()
    {
        var db = new PhpPlanQ1Open.OpenStore();
        db.Interfaces.AddRange(
        [
            new() { Id = 1, HandlerFolder = "", Name = "Warehouse" },
            new() { Id = 2, HandlerFolder = "prices", Name = "Prices" },
            new() { Id = 3, HandlerFolder = "treelax_catalogue", Name = "Catalogue" },
            new() { Id = 4, HandlerFolder = "laximo", Name = "Laximo" }
        ]);
        db.Storages.AddRange(
        [
            new() { Id = 1, Name = "Main WH", ShortName = "MW", InterfaceType = 1, ConnectionOptions = "", StorefrontTempDisabled = 0, Hidden = 0 },
            new() { Id = 2, Name = "Price WH", ShortName = "PW", InterfaceType = 2, ConnectionOptions = "{\"price_id\":10}", StorefrontTempDisabled = 0, Hidden = 0 },
            new() { Id = 3, Name = "Cat WH", ShortName = "CW", InterfaceType = 3, ConnectionOptions = "", StorefrontTempDisabled = 1, Hidden = 0 },
            new() { Id = 4, Name = "Ext WH", ShortName = "EW", InterfaceType = 4, ConnectionOptions = "{\"price_id\":11}", StorefrontTempDisabled = 0, Hidden = 1 },
            new() { Id = 5, Name = "Off WH", ShortName = "Cafe", InterfaceType = 1, ConnectionOptions = "", StorefrontTempDisabled = 1, Hidden = 0 }
        ]);
        db.Prices.AddRange(
        [
            new() { Id = 10, Name = "Linked A", StorefrontTempDisabled = 0 },
            new() { Id = 11, Name = "Linked B", StorefrontTempDisabled = 0 },
            new() { Id = 12, Name = "Unlinked C", StorefrontTempDisabled = 0 },
            new() { Id = 13, Name = "Unlinked D", StorefrontTempDisabled = 1 }
        ]);
        db.PriceData.AddRange(
        [
            new() { Id = 1, PriceId = 12, Exist = 1, Price = 4.50m },
            new() { Id = 2, PriceId = 12, Exist = 2, Price = 9.00m }
        ]);
        return db;
    }

    private static PhpPlanQ1Open.OpenStore SeedSku()
    {
        var db = new PhpPlanQ1Open.OpenStore();
        db.Storages.Add(new PhpPlanQ1Open.StorageRow
        {
            Id = 1,
            Name = "Main",
            ShortName = "MW",
            ConnectionOptions = "{\"price_id\":10}"
        });
        db.PriceData.AddRange(
        [
            new() { Id = 1, Manufacturer = "Bosch", Article = "045110", ArticleShow = "045 110", ArticleSearch = "045110", Name = "Oil filter", PriceId = 10 },
            new() { Id = 2, Manufacturer = "Mann", Article = "HU718", ArticleShow = "HU718", ArticleSearch = "HU718", Name = "Mann filter", PriceId = 10 }
        ]);
        db.Catalogue.Add(new PhpPlanQ1Open.CatalogueRow { Id = 7, Caption = "Oil Filter SKU", Alias = "oil-filter" });
        return db;
    }

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
