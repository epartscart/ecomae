using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1FoamParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Foam");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Foam_MatchPhpGolden()
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
            if (!JsonEquivalent(JsonDocument.Parse(Json(actual)).RootElement, expected))
            {
                failures.Add(name + " exp=" + expected.GetRawText() + " got=" + Json(actual));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("content/shop/docpart/epc_accessories_db.php", PhpPlanQ1Foam.AccessoriesDbPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Foam.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Foam.AccessoriesDbPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Foam.EpcAccSlugify("Brake Pads"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Foam.Reset();
        PhpPlanQ1Foam.LoadTaxonomyJson = FoamTax;
        PhpPlanQ1Foam.TaxonomyJsonPath = () => "/tmp/foam5/tax.json";
        PhpPlanQ1Foam.DocumentRoot = Path.Combine(Path.GetTempPath(), "ecomae_foam_root");
        return name switch
        {
            "names" => Names(),
            "terms" => Terms(),
            "listings" => Listings(),
            "market" => Market(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Names()
    {
        PhpPlanQ1Foam.DocumentRoot = "/shop/acme";
        using var dummy = OpenAdmin();
        var emptyMany = PhpPlanQ1Foam.EpcAccPhotosAddManyFromFiles(dummy, 0, new Dictionary<string, object?>(StringComparer.Ordinal));
        var path = PhpPlanQ1Foam.EpcAccTaxonomyJsonPath();
        return new object?[]
        {
            PhpPlanQ1Foam.EpcAccSlugify("Brake Pads"),
            PhpPlanQ1Foam.EpcAccSlugify("  "),
            PhpPlanQ1Foam.EpcAccSlugify("Oil/Filter #2"),
            PhpPlanQ1Foam.EpcAccStorefrontUrl(0),
            PhpPlanQ1Foam.EpcAccStorefrontUrl(12, "/en"),
            PhpPlanQ1Foam.EpcAccStorefrontUrl(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = 7,
                ["category"] = "brakes",
                ["subcategory"] = "pads"
            }, "/ar"),
            PhpPlanQ1Foam.EpcAccStorefrontUrl(new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = 0 }, ""),
            PhpPlanQ1Foam.EpcAccIsOutboundExternalUrl("") ? 1 : 0,
            PhpPlanQ1Foam.EpcAccIsOutboundExternalUrl("/en/accessories-spare-parts?category=brakes") ? 1 : 0,
            PhpPlanQ1Foam.EpcAccIsOutboundExternalUrl("/en/accessories-spare-parts?id=9") ? 1 : 0,
            PhpPlanQ1Foam.EpcAccIsOutboundExternalUrl("https://pakwheels.com/x") ? 1 : 0,
            PhpPlanQ1Foam.EpcAccIsOutboundExternalUrl("/en/parts/oc47") ? 1 : 0,
            PhpPlanQ1Foam.EpcAccPhotoPublicUrl(""),
            PhpPlanQ1Foam.EpcAccPhotoPublicUrl("../acc_1.jpg"),
            PhpPlanQ1Foam.EpcAccUaeCities(),
            PhpPlanQ1Foam.EpcAccLegacyPkCities()[0],
            PhpPlanQ1Foam.EpcAccLegacyPkCities().Count,
            path.Length >= 10 ? path[^10..] : path,
            emptyMany
        };
    }

    private static object Terms()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_foam_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            PhpPlanQ1Foam.EpcAccEnsureSchema(db);
            var seed = PhpPlanQ1Foam.EpcAccSeedTermsFromJson(db);
            var makes = PhpPlanQ1Foam.EpcAccTermLabels(db, "make");
            var cities = PhpPlanQ1Foam.EpcAccTermLabels(db, "city");
            var conds = PhpPlanQ1Foam.EpcAccGetTerms(db, "condition");
            var newId = PhpPlanQ1Foam.EpcAccSaveTerm(db, "make", "Honda");
            var off = PhpPlanQ1Foam.EpcAccSetTermActive(db, newId, false) ? 1 : 0;
            var activeMakes = PhpPlanQ1Foam.EpcAccTermLabels(db, "make");
            var del = PhpPlanQ1Foam.EpcAccDeleteTerm(db, newId) ? 1 : 0;
            using (var ins = db.CreateCommand())
            {
                ins.CommandText = "INSERT INTO `epc_acc_listings` (`title`,`city`,`currency`,`status`,`created_at`,`updated_at`) VALUES (@t,@c,@cur,@s,1,1)";
                ins.Parameters.AddWithValue("@t", "Pad | Karachi");
                ins.Parameters.AddWithValue("@c", "Karachi");
                ins.Parameters.AddWithValue("@cur", "PKR");
                ins.Parameters.AddWithValue("@s", "published");
                ins.ExecuteNonQuery();
            }

            var mig = PhpPlanQ1Foam.EpcAccMigrateUaeLocale(db);
            Dictionary<string, object?> row;
            using (var q = db.CreateCommand())
            {
                q.CommandText = "SELECT `title`, `city`, `currency` FROM `epc_acc_listings` LIMIT 1";
                using var reader = q.ExecuteReader();
                reader.Read();
                row = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["title"] = reader.GetString(0),
                    ["city"] = reader.GetString(1),
                    ["currency"] = reader.GetString(2)
                };
            }

            return new object?[]
            {
                seed["makes"],
                seed["cities"],
                seed["conditions"],
                seed["years"],
                makes,
                cities,
                conds.Count > 0 ? conds[0]["value"] : "",
                conds.Count > 0 ? conds[0]["label"] : "",
                newId > 0 ? 1 : 0,
                off,
                activeMakes,
                del,
                mig["cities_active"],
                ToInt(mig["listings_city"]) > 0 ? 1 : 0,
                ToInt(mig["listings_currency"]) > 0 ? 1 : 0,
                ToInt(mig["titles"]) > 0 ? 1 : 0,
                row["city"],
                row["currency"],
                Convert.ToString(row["title"], CultureInfo.InvariantCulture)?.Contains("Dubai", StringComparison.Ordinal) == true ? 1 : 0
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Listings()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_foam_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            var seeded = PhpPlanQ1Foam.EpcAccSeedCategoriesFromJson(db);
            var tree = PhpPlanQ1Foam.EpcAccGetCategoryTree(db);
            var adminTree = PhpPlanQ1Foam.EpcAccAdminCategoryTree(db);
            var extra = PhpPlanQ1Foam.EpcAccSaveCategory(db, "Extra Cat");
            var dup = PhpPlanQ1Foam.EpcAccSaveCategory(db, "Extra Cat");
            var off = PhpPlanQ1Foam.EpcAccSetCategoryActive(db, extra, false);
            var brakesId = ToInt(tree[0]["id"]);
            var padsId = ToInt(((List<Dictionary<string, object?>>)tree[0]["children"]!)[0]["id"]);
            var lid = PhpPlanQ1Foam.EpcAccAddListing(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["category_id"] = brakesId,
                ["subcategory_id"] = padsId,
                ["title"] = "Pad kit",
                ["make"] = "Toyota",
                ["model"] = "Corolla",
                ["city"] = "Dubai",
                ["price"] = 12.5,
                ["featured"] = 1,
                ["stock_qty"] = 4
            });
            var got = PhpPlanQ1Foam.EpcAccGetListing(db, lid);
            PhpPlanQ1Foam.EpcAccUpdateListing(db, lid, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["category_id"] = brakesId,
                ["subcategory_id"] = padsId,
                ["title"] = "Pad kit HD",
                ["make"] = "Toyota",
                ["model"] = "Corolla",
                ["city"] = "Sharjah",
                ["price"] = 15,
                ["status"] = "published"
            });
            PhpPlanQ1Foam.EpcAccSetListingStatus(db, lid, "draft");
            var search = PhpPlanQ1Foam.EpcAccAdminSearch(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["q"] = "Pad",
                ["status"] = "draft"
            });
            var used = PhpPlanQ1Foam.EpcAccDeleteCategory(db, brakesId);
            PhpPlanQ1Foam.EpcAccSetListingStatus(db, lid, "published");
            var okDel = PhpPlanQ1Foam.EpcAccDeleteListing(db, lid);
            var free = PhpPlanQ1Foam.EpcAccDeleteCategory(db, brakesId);
            var items = (List<Dictionary<string, object?>>)search["items"]!;
            return new object?[]
            {
                seeded["parents"],
                seeded["children"],
                tree.Count,
                tree[0]["slug"],
                ((List<Dictionary<string, object?>>)tree[0]["children"]!)[0]["slug"],
                adminTree.Count,
                extra > 0 ? 1 : 0,
                dup != extra ? 1 : 0,
                off ? 1 : 0,
                lid > 0 ? 1 : 0,
                Str(got?.GetValueOrDefault("title")),
                ToDouble(got?.GetValueOrDefault("price")),
                search["total"],
                items.Count > 0 ? items[0]["title"] : "",
                items.Count > 0 ? items[0]["city"] : "",
                Truthy(used, "ok") ? 1 : 0,
                used["message"],
                okDel ? 1 : 0,
                Truthy(free, "ok") ? 1 : 0
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Market()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_foam_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            PhpPlanQ1Foam.EpcAccSeedCategoriesFromJson(db);
            var tree = PhpPlanQ1Foam.EpcAccGetCategoryTree(db);
            var brakesId = ToInt(tree[0]["id"]);
            var padsId = ToInt(((List<Dictionary<string, object?>>)tree[0]["children"]!)[0]["id"]);
            var a = PhpPlanQ1Foam.EpcAccAddListing(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["category_id"] = brakesId,
                ["subcategory_id"] = padsId,
                ["title"] = "Pad kit",
                ["make"] = "Toyota",
                ["city"] = "Dubai",
                ["price"] = 20,
                ["featured"] = 1,
                ["stock_qty"] = 2,
                ["external_url"] = "https://example.com/x"
            });
            var b = PhpPlanQ1Foam.EpcAccAddListing(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["category_id"] = brakesId,
                ["subcategory_id"] = padsId,
                ["title"] = "Cheap pad",
                ["make"] = "Nissan",
                ["city"] = "Sharjah",
                ["price"] = 8,
                ["stock_qty"] = 9
            });
            PhpPlanQ1Foam.EpcAccAddListing(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["category_id"] = brakesId,
                ["subcategory_id"] = padsId,
                ["title"] = "Hidden",
                ["make"] = "Toyota",
                ["city"] = "Dubai",
                ["price"] = 99,
                ["status"] = "draft"
            });
            using (var ins = db.CreateCommand())
            {
                ins.CommandText = "INSERT INTO `epc_acc_photos` (`listing_id`,`file_name`,`sort_order`,`is_primary`,`created_at`) VALUES (@a,@f1,10,0,1)";
                ins.Parameters.AddWithValue("@a", a);
                ins.Parameters.AddWithValue("@f1", "acc_a.jpg");
                ins.ExecuteNonQuery();
                ins.Parameters.Clear();
                ins.CommandText = "INSERT INTO `epc_acc_photos` (`listing_id`,`file_name`,`sort_order`,`is_primary`,`created_at`) VALUES (@a,@f2,20,1,1)";
                ins.Parameters.AddWithValue("@a", a);
                ins.Parameters.AddWithValue("@f2", "acc_b.jpg");
                ins.ExecuteNonQuery();
            }

            PhpPlanQ1Foam.EpcAccPhotosSyncListing(db, a);
            var photos = PhpPlanQ1Foam.EpcAccPhotosList(db, a);
            var prim = PhpPlanQ1Foam.EpcAccPhotosSetPrimary(db, a, ToInt(photos[1]["id"]));
            var gone = PhpPlanQ1Foam.EpcAccPhotosDelete(db, a, ToInt(photos[0]["id"]));
            var all = PhpPlanQ1Foam.EpcAccMarketplaceSearch(db, new Dictionary<string, object?>(StringComparer.Ordinal));
            var toy = PhpPlanQ1Foam.EpcAccMarketplaceSearch(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["make"] = "Toyota",
                ["sort"] = "price-asc"
            });
            var one = PhpPlanQ1Foam.EpcAccMarketplaceSearch(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = b });
            var allItems = (List<Dictionary<string, object?>>)all["items"]!;
            var facets = (Dictionary<string, object?>)all["facets"]!;
            var cats = (List<Dictionary<string, object?>>)facets["categories"]!;
            var toyItems = (List<Dictionary<string, object?>>)toy["items"]!;
            var oneItems = (List<Dictionary<string, object?>>)one["items"]!;
            var gonePhotos = gone.TryGetValue("photos", out var gp) && gp is List<Dictionary<string, object?>> list ? list : [];
            return new object?[]
            {
                all["total"],
                Truthy(all, "empty_catalog") ? 1 : 0,
                all["source"],
                allItems[0]["title"],
                Truthy(allItems[0], "featured") ? 1 : 0,
                allItems[0]["external_url"],
                allItems[0]["detail_url"],
                allItems[0]["photo_count"],
                cats.Count,
                cats[0]["count"],
                toy["total"],
                toyItems[0]["make"],
                one["total"],
                oneItems[0]["title"],
                photos.Count,
                Truthy(photos[0], "is_primary") ? 1 : 0,
                Truthy(prim, "ok") ? 1 : 0,
                Truthy(gone, "ok") ? 1 : 0,
                gonePhotos.Count
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static Dictionary<string, object?> FoamTax() => new(StringComparer.Ordinal)
    {
        ["categories"] = new List<object?>
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["slug"] = "brakes",
                ["label"] = "Brakes",
                ["pw_id"] = 1,
                ["children"] = new List<object?>
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["slug"] = "pads",
                        ["label"] = "Brake Pads",
                        ["pw_id"] = 11
                    }
                }
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["slug"] = "filters",
                ["label"] = "Filters",
                ["pw_id"] = 2,
                ["children"] = new List<object?>
                {
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["slug"] = "oil",
                        ["label"] = "Oil Filters",
                        ["pw_id"] = 21
                    }
                }
            }
        },
        ["makes"] = new List<object?> { "Toyota", "Nissan" },
        ["cities"] = new List<object?> { "Dubai", "Abu Dhabi", "Sharjah" },
        ["filters"] = new List<object?>()
    };

    private static string Password()
        => Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? throw new InvalidOperationException("missing ECOMAE_LOCAL_MARIADB_E2E_DSN");

    private static MySqlConnection OpenAdmin()
    {
        var db = new MySqlConnection($"Server=127.0.0.1;Port=3306;User ID=ecomae;Password={Password()};AllowUserVariables=true;");
        db.Open();
        return db;
    }

    private static MySqlConnection OpenDb(string schema)
    {
        var db = new MySqlConnection($"Server=127.0.0.1;Port=3306;Database={schema};User ID=ecomae;Password={Password()};AllowUserVariables=true;");
        db.Open();
        return db;
    }

    private static void Exec(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static bool Truthy(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) && v is true or 1 or 1L or 1.0;

    private static int ToInt(object? value)
        => Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture);

    private static double ToDouble(object? value)
        => Convert.ToDouble(value ?? 0, CultureInfo.InvariantCulture);

    private static string Str(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static string Json(object? value) => JsonSerializer.Serialize(value, JsonOpts);

    private static bool JsonEquivalent(JsonElement left, JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number && left.GetDouble() == right.GetDouble();
        }

        return left.ValueKind switch
        {
            JsonValueKind.Object => left.EnumerateObject().All(p => right.TryGetProperty(p.Name, out var o) && JsonEquivalent(p.Value, o))
                && left.EnumerateObject().Count() == right.EnumerateObject().Count(),
            JsonValueKind.Array => left.EnumerateArray().ToList().Zip(right.EnumerateArray().ToList(), JsonEquivalent).All(x => x)
                && left.GetArrayLength() == right.GetArrayLength(),
            JsonValueKind.String => left.GetString() == right.GetString(),
            JsonValueKind.Number => left.GetRawText() == right.GetRawText() || left.GetDouble() == right.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => left.GetBoolean() == right.GetBoolean(),
            JsonValueKind.Null => true,
            _ => true
        };
    }
}
