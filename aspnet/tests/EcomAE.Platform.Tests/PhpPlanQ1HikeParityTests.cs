using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1HikeParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Hike");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Hike_MatchPhpGolden()
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
                var exp = expected.GetRawText();
                var got = Json(actual);
                failures.Add(name + " exp=" + exp[..Math.Min(400, exp.Length)] + " got=" + got[..Math.Min(400, got.Length)]);
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal("cp/content/control/portal/epc_super_cp_customer_board.php", PhpPlanQ1Hike.CustomerBoardPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Hike.Reset();
        PhpPlanQ1Hike.IsSuperHost = () => true;
        PhpPlanQ1Hike.IsAdmin = () => true;
        PhpPlanQ1Hike.HasPdo = true;
        PhpPlanQ1Hike.BackendDir = "cp";
        PhpPlanQ1Hike.Board = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal)
                {
                    ["name"] = "Ada Lovelace",
                    ["user_id"] = 7,
                    ["email"] = "ada@example.com",
                    ["source_label"] = "Platform",
                    ["links"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["crm"] = "/cp/crm/ada" }
                }
            },
            ["total"] = 1,
            ["page"] = 1,
            ["per_page"] = 50,
            ["stats"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["platform"] = 1 }
        };
        var html = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Hike.CustomerBoardPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", html, StringComparison.Ordinal);
        Assert.Contains("Ada Lovelace", html, StringComparison.Ordinal);
        Assert.Contains("/cp/crm/ada", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Please <a href", html, StringComparison.Ordinal);
        PhpPlanQ1Hike.IsAdmin = () => false;
        var denied = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        Assert.Contains("log in to Super CP", denied, StringComparison.Ordinal);
        Assert.DoesNotContain("Ada Lovelace", denied, StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Hike.Reset();
        return name switch
        {
            "empty" => Empty(),
            "gates" => Gates(),
            "live" => Live(),
            "tenant" => Tenant(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static Dictionary<string, object?> EmptyBoard()
        => new(StringComparer.Ordinal)
        {
            ["rows"] = new List<Dictionary<string, object?>>(),
            ["total"] = 0,
            ["page"] = 1,
            ["per_page"] = 50,
            ["stats"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        };

    private static object Empty()
    {
        PhpPlanQ1Hike.IsSuperHost = () => false;
        PhpPlanQ1Hike.IsAdmin = () => false;
        var none = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        PhpPlanQ1Hike.BackendDir = "acme";
        PhpPlanQ1Hike.IsSuperHost = () => true;
        PhpPlanQ1Hike.IsAdmin = () => false;
        var login = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        return new object[] { none, login };
    }

    private static object Gates()
    {
        PhpPlanQ1Hike.IsSuperHost = () => false;
        PhpPlanQ1Hike.IsAdmin = () => true;
        PhpPlanQ1Hike.HasPdo = true;
        var noSuper = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        PhpPlanQ1Hike.IsSuperHost = () => true;
        PhpPlanQ1Hike.HasPdo = false;
        var noDb = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        PhpPlanQ1Hike.HasPdo = true;
        PhpPlanQ1Hike.Board = EmptyBoard();
        var emptyBoard = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        PhpPlanQ1Hike.Search = "nobody";
        PhpPlanQ1Hike.TenantFilter = "platform";
        PhpPlanQ1Hike.Tenants = [new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "acme", ["label"] = "Acme Parts" }];
        var filteredEmpty = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        PhpPlanQ1Hike.BackendDir = "acme\"><x";
        PhpPlanQ1Hike.Search = "a&b";
        PhpPlanQ1Hike.TenantFilter = "acme\"><x";
        PhpPlanQ1Hike.Tenants = [new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "acme\"><x", ["label"] = "Acme & Co" }];
        var escape = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        return new object[] { noSuper, noDb, emptyBoard, filteredEmpty, escape };
    }

    private static object Live()
    {
        PhpPlanQ1Hike.IsSuperHost = () => true;
        PhpPlanQ1Hike.IsAdmin = () => true;
        PhpPlanQ1Hike.HasPdo = true;
        PhpPlanQ1Hike.BackendDir = "cp";
        PhpPlanQ1Hike.Board = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal)
                {
                    ["name"] = "Ada Lovelace",
                    ["user_id"] = 7,
                    ["email"] = "ada@example.com",
                    ["phone"] = "+971 50 000 0007",
                    ["source_label"] = "Platform",
                    ["source"] = "platform",
                    ["hostname"] = "www.ecomae.com",
                    ["time_reg"] = 1700000000,
                    ["links"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["crm"] = "/cp/crm/ada",
                        ["erp"] = "/cp/erp/ada",
                        ["cp"] = "/cp/users/7"
                    }
                }
            },
            ["total"] = 1,
            ["page"] = 1,
            ["per_page"] = 50,
            ["stats"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["platform"] = 1,
                ["tenants_scanned"] = 2,
                ["tenants_with_hits"] = 0
            }
        };
        PhpPlanQ1Hike.Tenants =
        [
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "acme", ["label"] = "Acme Parts" },
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "beta", ["label"] = "Beta Town" }
        ];
        var defaults = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        PhpPlanQ1Hike.Search = "Ada & Co";
        PhpPlanQ1Hike.TenantFilter = "";
        PhpPlanQ1Hike.Board = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal)
                {
                    ["name"] = "Ada & Co",
                    ["user_id"] = 7,
                    ["email"] = "ada+<x>@example.com",
                    ["phone"] = "",
                    ["source_label"] = "Platform",
                    ["source"] = "platform",
                    ["hostname"] = "",
                    ["time_reg"] = 1700000000,
                    ["links"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["crm"] = "/cp/crm?q=a&b" }
                }
            },
            ["total"] = 1,
            ["page"] = 1,
            ["per_page"] = 50,
            ["stats"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["platform"] = 1,
                ["tenants_scanned"] = 1,
                ["tenants_with_hits"] = 1
            }
        };
        PhpPlanQ1Hike.Tenants = [new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "acme", ["label"] = "Acme & Co" }];
        var filled = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        PhpPlanQ1Hike.Search = "user";
        PhpPlanQ1Hike.TenantFilter = "acme";
        PhpPlanQ1Hike.PageRaw = "2";
        var pages = new List<Dictionary<string, object?>>();
        for (var i = 1; i <= 3; i++)
        {
            pages.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = "User " + i,
                ["user_id"] = i,
                ["email"] = "u" + i + "@example.com",
                ["source"] = "acme",
                ["source_label"] = "Acme",
                ["time_reg"] = 0,
                ["links"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            });
        }

        PhpPlanQ1Hike.Board = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows"] = pages,
            ["total"] = 60,
            ["page"] = 2,
            ["per_page"] = 50,
            ["stats"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["platform"] = 0,
                ["tenants_scanned"] = 1,
                ["tenants_with_hits"] = 1
            }
        };
        PhpPlanQ1Hike.Tenants = [new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "acme", ["label"] = "Acme Parts" }];
        var paged = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        PhpPlanQ1Hike.Search = "";
        PhpPlanQ1Hike.TenantFilter = "";
        PhpPlanQ1Hike.PageRaw = "";
        PhpPlanQ1Hike.Board = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal) { ["name"] = "Bea", ["user_id"] = 9, ["source"] = "beta" }
            },
            ["total"] = 1,
            ["page"] = 1,
            ["per_page"] = 50,
            ["stats"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        };
        PhpPlanQ1Hike.Tenants = [];
        var blankTime = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        return new object[] { defaults, filled, paged, blankTime };
    }

    private static object Tenant()
    {
        PhpPlanQ1Hike.IsSuperHost = () => true;
        PhpPlanQ1Hike.IsAdmin = () => true;
        PhpPlanQ1Hike.HasPdo = true;
        PhpPlanQ1Hike.BackendDir = "acme";
        PhpPlanQ1Hike.TenantFilter = "acme";
        PhpPlanQ1Hike.Board = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal)
                {
                    ["name"] = "Ada Acme",
                    ["user_id"] = 11,
                    ["email"] = "ada@acme.test",
                    ["source_label"] = "Acme",
                    ["source"] = "acme",
                    ["hostname"] = "acme.example.com",
                    ["time_reg"] = 1700000000,
                    ["links"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["crm"] = "/acme/crm/11",
                        ["erp"] = "/acme/erp/11"
                    }
                }
            },
            ["total"] = 1,
            ["page"] = 1,
            ["per_page"] = 50,
            ["stats"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["platform"] = 0,
                ["tenants_scanned"] = 1,
                ["tenants_with_hits"] = 1
            }
        };
        PhpPlanQ1Hike.Tenants = [new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "acme", ["label"] = "Acme Parts" }];
        var acme = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        PhpPlanQ1Hike.BackendDir = "beta";
        PhpPlanQ1Hike.TenantFilter = "beta";
        PhpPlanQ1Hike.Board = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows"] = new List<Dictionary<string, object?>>
            {
                new(StringComparer.Ordinal)
                {
                    ["name"] = "Bea Beta",
                    ["user_id"] = 22,
                    ["email"] = "bea@beta.test",
                    ["source_label"] = "Beta",
                    ["source"] = "beta",
                    ["hostname"] = "beta.example.com",
                    ["time_reg"] = 1700000000,
                    ["links"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["crm"] = "/beta/crm/22" }
                }
            },
            ["total"] = 1,
            ["page"] = 1,
            ["per_page"] = 50,
            ["stats"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["platform"] = 0,
                ["tenants_scanned"] = 1,
                ["tenants_with_hits"] = 1
            }
        };
        PhpPlanQ1Hike.Tenants = [new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "beta", ["label"] = "Beta Town" }];
        var beta = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        PhpPlanQ1Hike.IsAdmin = () => false;
        PhpPlanQ1Hike.BackendDir = "acme";
        PhpPlanQ1Hike.TenantFilter = "";
        PhpPlanQ1Hike.Board = EmptyBoard();
        PhpPlanQ1Hike.Tenants = [];
        var denied = PhpPlanQ1Hike.EpcSuperCpCustomerBoardPage();
        return new object[] { acme, beta, denied };
    }

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
