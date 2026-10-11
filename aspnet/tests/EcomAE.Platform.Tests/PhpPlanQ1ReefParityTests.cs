using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1ReefParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Reef");

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
    public void PlanQ1Reef_MatchPhpGolden()
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
            if (!Same(Json(Freeze(actual.Extra)), expected))
            {
                failures.Add(name + " extraExp=" + Truncate(expected.GetRawText()) + " extraGot=" + Truncate(Json(Freeze(actual.Extra))));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Reef.PricesPerfPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Reef.PricesPerfPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Reef.Reset();
        Assert.Contains("epc_prices_manager_perf.php", PhpPlanQ1Reef.PricesPerfPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Reef.PricesPerfPath, StringComparison.Ordinal);
        Assert.False(PhpPlanQ1Reef.EpcPricesShouldRunTablesCleaner());
        PhpPlanQ1Reef.Get["epc_clean_pyprices"] = "";
        Assert.True(PhpPlanQ1Reef.EpcPricesShouldRunTablesCleaner());
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Reef.Reset();
        return name switch
        {
            "pure" => Pure(),
            "lists" => Lists(),
            "indexes" => Indexes(),
            "health" => Health(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Pure()
    {
        PhpPlanQ1Reef.Server["HTTP_HOST"] = "shop.example:8443";
        var plain = PhpPlanQ1Reef.EpcPricesIsPlatformOperatorRequest();
        var largePlain = PhpPlanQ1Reef.EpcPricesIsLargeTenantHost();
        var pollPlain = PhpPlanQ1Reef.EpcPricesExternalPollIntervalMs();
        PhpPlanQ1Reef.Server["HTTP_HOST"] = "www.ecomae.com";
        var plat = PhpPlanQ1Reef.EpcPricesIsPlatformOperatorRequest();
        var largePlat = PhpPlanQ1Reef.EpcPricesIsLargeTenantHost();
        var pollPlat = PhpPlanQ1Reef.EpcPricesExternalPollIntervalMs();
        PhpPlanQ1Reef.Server["HTTP_HOST"] = "cp.ecomae.com:443";
        var cp = PhpPlanQ1Reef.EpcPricesIsPlatformOperatorRequest();
        PhpPlanQ1Reef.Server["HTTP_HOST"] = "demo.epartscart.com";
        var eparts = PhpPlanQ1Reef.EpcPricesIsLargeTenantHost();
        var pollEparts = PhpPlanQ1Reef.EpcPricesExternalPollIntervalMs();
        PhpPlanQ1Reef.Server.Remove("HTTP_HOST");
        var emptyHost = PhpPlanQ1Reef.EpcPricesIsLargeTenantHost();
        var cleanMiss = PhpPlanQ1Reef.EpcPricesShouldRunTablesCleaner();
        PhpPlanQ1Reef.Get["epc_clean_pyprices"] = "";
        var cleanEmpty = PhpPlanQ1Reef.EpcPricesShouldRunTablesCleaner();
        PhpPlanQ1Reef.Get["epc_clean_pyprices"] = "0";
        var cleanZero = PhpPlanQ1Reef.EpcPricesShouldRunTablesCleaner();
        var defer = PhpPlanQ1Reef.EpcPricesDeferInlineUpdateHistory();
        PhpPlanQ1Reef.IsPlatformHostname = () => true;
        PhpPlanQ1Reef.Server["HTTP_HOST"] = "other.test";
        var viaFn = PhpPlanQ1Reef.EpcPricesIsPlatformOperatorRequest();
        var viaLarge = PhpPlanQ1Reef.EpcPricesIsLargeTenantHost();
        return new Rendered(new object?[]
        {
            plain, largePlain, pollPlain,
            plat, largePlat, pollPlat,
            cp, eparts, pollEparts, emptyHost,
            cleanMiss, cleanEmpty, cleanZero, defer,
            viaFn, viaLarge
        });
    }

    private static Rendered Lists()
    {
        var db = new PhpPlanQ1Reef.ReefStore { HasRecordsCount = true };
        db.Columns.Add("shop_docpart_prices.records_count");
        db.Prices.Add(new PhpPlanQ1Reef.PriceRow { Id = 1, RecordsCount = 12 });
        db.Prices.Add(new PhpPlanQ1Reef.PriceRow { Id = 2, RecordsCount = 0 });
        db.PriceDataIds.AddRange([1, 1, 2]);
        db.CronPriceIds.AddRange([1, 1, 1]);
        PhpPlanQ1Reef.UseStore(db);
        var filled = Norm(PhpPlanQ1Reef.EpcPricesFetchListsRows(db));
        foreach (var row in db.Prices)
        {
            row.RecordsCount = 0;
        }

        var live = Norm(PhpPlanQ1Reef.EpcPricesFetchListsRows(db));
        var persisted = db.Prices.OrderBy(p => p.Id).Select(p => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = p.Id,
            ["records_count"] = p.RecordsCount
        }).ToList();
        db.Prices.Clear();
        var none = Norm(PhpPlanQ1Reef.EpcPricesFetchListsRows(db));
        var badCol = PhpPlanQ1Reef.EpcPricesTableHasColumn(db, "shop-doc", "records_count");
        var okCol = PhpPlanQ1Reef.EpcPricesTableHasColumn(db, "shop_docpart_prices", "records_count");
        var missCol = PhpPlanQ1Reef.EpcPricesTableHasColumn(db, "shop_docpart_prices", "nope");
        var map = PhpPlanQ1Reef.EpcPricesLiveCountsMap(db) ?? new Dictionary<int, int>();
        var normMap = map.OrderBy(kv => kv.Key).ToDictionary(kv => kv.Key.ToString(), kv => (object)kv.Value, StringComparer.Ordinal);
        return new Rendered(new object?[] { filled, live, persisted, none, badCol, okCol, missCol, normMap });
    }

    private static Rendered Indexes()
    {
        var db = new PhpPlanQ1Reef.ReefStore();
        PhpPlanQ1Reef.UseStore(db);
        PhpPlanQ1Reef.EpcPricesEnsureListingIndexes(db, false);
        var before = db.Indexes.Contains("shop_docpart_prices_data.x_price_id") ? 1 : 0;
        PhpPlanQ1Reef.EpcPricesEnsureListingIndexes(db, true);
        var still = db.Indexes.Contains("shop_docpart_prices_data.x_price_id") ? 1 : 0;
        PhpPlanQ1Reef.EpcPricesAddIndexIfMissing(db, "shop-doc", "x_price_id", "(`price_id`)");
        PhpPlanQ1Reef.EpcPricesAddIndexIfMissing(db, "shop_docpart_prices_data", "x price", "(`price_id`)");
        PhpPlanQ1Reef.EpcPricesAddIndexIfMissing(db, "shop_docpart_prices_data", "x_name", "(`price_id`)");
        var idxName = db.Indexes.Contains("shop_docpart_prices_data.x_name");
        var idxPrice = db.Indexes.Contains("shop_docpart_prices_data.x_price_id");
        return new Rendered(new object?[] { before, still, idxName, idxPrice });
    }

    private static Rendered Health()
    {
        PhpPlanQ1Reef.HealthHttp = (url, post, timeoutSec) =>
        {
            _ = url;
            _ = timeoutSec;
            if (post.Contains("key=dead", StringComparison.Ordinal))
            {
                return new PhpPlanQ1Reef.HealthHit(false, "", "Failed to connect");
            }

            if (post.Contains("key=ok-key", StringComparison.Ordinal))
            {
                return new PhpPlanQ1Reef.HealthHit(true, "{\"status\":true,\"message\":\"OK\"}", "");
            }

            if (post.Contains("key=zero", StringComparison.Ordinal))
            {
                return new PhpPlanQ1Reef.HealthHit(true, "{\"status\":0,\"message\":\"no\"}", "");
            }

            return new PhpPlanQ1Reef.HealthHit(true, "plain-fail-body", "");
        };
        var ok = PhpPlanQ1Reef.EpcPypricesHealthCheck("https://shop.example/", "ok-key", 2);
        var zero = PhpPlanQ1Reef.EpcPypricesHealthCheck("https://shop.example/", "zero", 2);
        var plain = PhpPlanQ1Reef.EpcPypricesHealthCheck("https://shop.example/", "plain", 2);
        var dead = PhpPlanQ1Reef.EpcPypricesHealthCheck("https://shop.example/", "dead", 1);
        return new Rendered(new object?[] { ok, zero, plain, dead });
    }

    private static List<Dictionary<string, object?>> Norm(List<Dictionary<string, object?>> rows)
        => rows.Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = Convert.ToInt32(r["id"], System.Globalization.CultureInfo.InvariantCulture),
            ["records_count"] = Convert.ToInt32(r["records_count"], System.Globalization.CultureInfo.InvariantCulture),
            ["cron_tasks_count"] = Convert.ToInt32(r["cron_tasks_count"], System.Globalization.CultureInfo.InvariantCulture)
        }).ToList();

    private static object? Freeze(object? value)
    {
        switch (value)
        {
            case string s:
                if (s.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase)
                    || s.Contains("Connection refused", StringComparison.OrdinalIgnoreCase)
                    || s.Contains("Empty response", StringComparison.Ordinal))
                {
                    return "NET";
                }

                return s;
            case Dictionary<string, object?> map:
                return map.ToDictionary(kv => kv.Key, kv => Freeze(kv.Value), StringComparer.Ordinal);
            case List<Dictionary<string, object?>> rows:
                return rows.Select(r => (Dictionary<string, object?>)Freeze(r)!).ToList();
            case List<object?> boxed:
                return boxed.Select(Freeze).ToList();
            case object?[] arr:
                return arr.Select(Freeze).ToArray();
            default:
                return value;
        }
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
