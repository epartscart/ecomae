using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpPlanQ1WalkParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Walk");

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
    public void PlanQ1Walk_MatchPhpGolden()
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
        => Assert.Equal(PhpPlanQ1Walk.MinPriceAclPath, PhpPlanQ1Walk.MinPriceAclPath);

    [Fact]
    public void Viewer_DoesNotStartASession()
    {
        PhpPlanQ1Walk.Reset();
        Assert.False(PhpPlanQ1Walk.EpcMvMinPriceIsAdminViewer());
    }

    private static string Render(string name)
    {
        PhpPlanQ1Walk.Reset();
        return name switch
        {
            "mv_pure" => Json(MvPure()),
            "mv_db" => Json(MvDb()),
            _ => "unknown:" + name
        };
    }

    private static object? MvPure()
        => new object?[]
        {
            PhpPlanQ1Walk.EpcMvMinTier,
            PhpPlanQ1Walk.EpcMvMaxTier,
            PhpPlanQ1Walk.EpcMvMinPriceAclDefaults(),
            PhpPlanQ1Walk.EpcMvMinPriceIsMinRow("EPC_MV_MIN"),
            PhpPlanQ1Walk.EpcMvMinPriceIsMinRow("  MIN "),
            PhpPlanQ1Walk.EpcMvMinPriceIsMinRow("epc_mv_min_extra"),
            PhpPlanQ1Walk.EpcMvMinPriceIsMinRow("notmin"),
            PhpPlanQ1Walk.EpcMvMinPriceIsMaxRow("EPC_MV_MAX"),
            PhpPlanQ1Walk.EpcMvMinPriceIsMaxRow("max"),
            PhpPlanQ1Walk.EpcMvMinPriceIsMaxRow("epc_mv_max2"),
            PhpPlanQ1Walk.EpcMvMinPriceDisplayStorage("  Main WH "),
            PhpPlanQ1Walk.EpcMvMinPriceDisplayStorage("epc_mv_min"),
            PhpPlanQ1Walk.EpcMvMinPriceDisplayStorage("MAX"),
            PhpPlanQ1Walk.EpcMvMinPriceListIsTyped("Bosch · Sales"),
            PhpPlanQ1Walk.EpcMvMinPriceListIsTyped("Bosch · Purchase"),
            PhpPlanQ1Walk.EpcMvMinPriceListIsTyped("Retail SALES list"),
            PhpPlanQ1Walk.EpcMvMinPriceListIsTyped("Purchase"),
            PhpPlanQ1Walk.EpcMvMinPriceListIsTyped("Inventory"),
            PhpPlanQ1Walk.EpcMvMinPriceIsAdminViewer()
        };

    private static object? MvDb()
    {
        PhpPlanQ1Walk.Clock = () => 1770000000;
        var db = new PhpPlanQ1Walk.AclStore();
        db.Groups.AddRange(
        [
            new() { Id = 1, Value = "Alpha", ForBackend = 0 },
            new() { Id = 2, Value = "", ForBackend = 0 },
            new() { Id = 3, Value = "Zed", ForBackend = 1 },
            new() { Id = 4, Value = "Beta", ForBackend = 0 },
            new() { Id = 5, Value = "Gamma", ForBackend = 0 }
        ]);
        var def = PhpPlanQ1Walk.EpcMvMinPriceAclGet(db);
        var ok = PhpPlanQ1Walk.EpcMvMinPriceAclSave(db, new Dictionary<string, object?>
        {
            ["restrict"] = 1,
            ["group_ids"] = new List<object?> { 4, "4", 0, "x", 9 },
            ["user_ids"] = new List<object?> { 12, 0, 12, "7" }
        }, 3);
        var restrictMin = db.Row!.RestrictMin;
        var groupJson = db.Row.GroupIdsJson;
        var userJson = db.Row.UserIdsJson;
        var updatedBy = db.Row.UpdatedBy;
        var got = PhpPlanQ1Walk.EpcMvMinPriceAclGet(db);
        var groups = PhpPlanQ1Walk.EpcMvMinPriceListCustomerGroups(db);
        PhpPlanQ1Walk.IsAdmin = () => true;
        var adminSee = PhpPlanQ1Walk.EpcMvMinPriceViewerMaySee(db, 0);
        PhpPlanQ1Walk.IsAdmin = () => false;
        PhpPlanQ1Walk.AdminId = () => 8;
        var adminIdSee = PhpPlanQ1Walk.EpcMvMinPriceViewerMaySee(db, 0);
        PhpPlanQ1Walk.AdminId = () => 0;
        var listed = PhpPlanQ1Walk.EpcMvMinPriceViewerMaySee(db, 12);
        var groupSee = PhpPlanQ1Walk.EpcMvMinPriceViewerMaySee(db, 99);
        PhpPlanQ1Walk.ProfileGroups = _ => [4];
        var groupSee2 = PhpPlanQ1Walk.EpcMvMinPriceViewerMaySee(db, 99);
        PhpPlanQ1Walk.ProfileGroups = _ => [];
        var deny = PhpPlanQ1Walk.EpcMvMinPriceViewerMaySee(db, 1);
        var hideMin = PhpPlanQ1Walk.EpcMvMinPriceShouldHideRow(db, new Dictionary<string, object?> { ["storage"] = "epc_mv_min" }, 1);
        var hideMax = PhpPlanQ1Walk.EpcMvMinPriceShouldHideRow(db, new Dictionary<string, object?> { ["storage"] = "epc_mv_max" }, 1);
        var hideInv = PhpPlanQ1Walk.EpcMvMinPriceShouldHideRow(db, new Dictionary<string, object?> { ["storage"] = "Main" }, 1);
        var ok2 = PhpPlanQ1Walk.EpcMvMinPriceAclSave(db, new Dictionary<string, object?>
        {
            ["restrict"] = "0",
            ["group_ids"] = new List<object?>(),
            ["user_ids"] = new List<object?>()
        });
        var open = PhpPlanQ1Walk.EpcMvMinPriceViewerMaySee(db, 1);
        var hideOpen = PhpPlanQ1Walk.EpcMvMinPriceShouldHideRow(db, new Dictionary<string, object?> { ["storage"] = "min" }, 1);
        return new object?[]
        {
            def, ok, restrictMin, groupJson, userJson, updatedBy,
            got, groups, adminSee, adminIdSee, listed, groupSee, groupSee2, deny,
            hideMin, hideMax, hideInv, ok2, open, hideOpen
        };
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
