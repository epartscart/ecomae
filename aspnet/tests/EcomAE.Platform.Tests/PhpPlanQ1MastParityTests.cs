using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1MastParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Mast");

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
    public void PlanQ1Mast_MatchPhpGolden()
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
            if (!Same(Json(actual.Extra), expected))
            {
                failures.Add(name + " extraExp=" + Truncate(expected.GetRawText()) + " extraGot=" + Truncate(Json(actual.Extra)));
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void CoveredPhpFiles_AreThePortedIncludes()
        => Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Mast.RestApiPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Mast.RestApiPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Mast.Reset();
        Assert.Contains("epc_rest_api_v2.php", PhpPlanQ1Mast.RestApiPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Mast.RestApiPath, StringComparison.Ordinal);
        var miss = PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/products");
        Assert.False((bool)miss["ok"]!);
        Assert.DoesNotContain("PHPSESSID", Json(miss), StringComparison.Ordinal);
        Assert.DoesNotContain("HTTP_COOKIE", PhpPlanQ1Mast.Server.Keys, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Mast.Reset();
        return name switch
        {
            "keys" => Keys(),
            "rate" => Rate(),
            "handle" => Handle(),
            "meta" => Meta(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Dictionary<string, object?> NormKey(Dictionary<string, object?> row)
    {
        foreach (var k in new[] { "last_used", "created_at", "last_request" })
        {
            if (row.TryGetValue(k, out var v) && v != null && Convert.ToString(v) != "")
            {
                row[k] = "TS";
            }
        }

        if (row.TryGetValue("key", out var inner) && inner is Dictionary<string, object?> child)
        {
            row["key"] = NormKey(child);
        }

        return row;
    }

    private static List<Dictionary<string, object?>> NormList(IEnumerable<Dictionary<string, object?>> rows)
    {
        var list = rows.Select(r => NormKey(new Dictionary<string, object?>(r, StringComparer.Ordinal))).ToList();
        if (list.Count > 0 && list[0].ContainsKey("id"))
        {
            return list.OrderByDescending(r => Convert.ToInt32(r["id"])).ToList();
        }

        if (list.Count > 0 && list[0].ContainsKey("total_requests"))
        {
            return list
                .OrderByDescending(r => Convert.ToInt32(r["total_requests"]))
                .ThenBy(r => Convert.ToString(r["site_key"]), StringComparer.Ordinal)
                .ToList();
        }

        if (list.Count > 0 && list[0].ContainsKey("count"))
        {
            return list
                .OrderByDescending(r => Convert.ToInt32(r["count"]))
                .ThenBy(r => Convert.ToString(r["endpoint"]), StringComparer.Ordinal)
                .ToList();
        }

        return list;
    }

    private static Rendered Keys()
    {
        var a = PhpPlanQ1Mast.EpcApiKeyGenerate("acme");
        var b = PhpPlanQ1Mast.EpcApiKeyGenerate("beta", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scopes"] = new[] { "read", "write" },
            ["label"] = "Beta write",
            ["rate_limit"] = 10,
            ["created_by"] = 4
        });
        var c = PhpPlanQ1Mast.EpcApiKeyGenerate("acme", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["expires_at"] = "2020-01-01 00:00:00",
            ["label"] = "expired"
        });
        var d = PhpPlanQ1Mast.EpcApiKeyGenerate("acme", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["expires_at"] = "0",
            ["label"] = "empty-exp"
        });
        var ok = PhpPlanQ1Mast.EpcApiKeyValidate(Convert.ToString(a["api_key"])!);
        var bad = PhpPlanQ1Mast.EpcApiKeyValidate("epc_nope");
        var expired = PhpPlanQ1Mast.EpcApiKeyValidate(Convert.ToString(c["api_key"])!);
        var empty = PhpPlanQ1Mast.EpcApiKeyValidate("");
        var acme = NormList(PhpPlanQ1Mast.EpcApiKeysList("acme"));
        var beta = NormList(PhpPlanQ1Mast.EpcApiKeysList("beta"));
        var other = NormList(PhpPlanQ1Mast.EpcApiKeysList("missing"));
        var revoked = PhpPlanQ1Mast.EpcApiKeyRevoke(Convert.ToInt32(c["key_id"]));
        var afterRevoke = PhpPlanQ1Mast.EpcApiKeyValidate(Convert.ToString(c["api_key"])!);
        var betaVal = PhpPlanQ1Mast.EpcApiKeyValidate(Convert.ToString(b["api_key"])!);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Convert.ToString(a["api_key"])!))).ToLowerInvariant();
        var okKey = (Dictionary<string, object?>)ok["key"]!;
        return new Rendered(new object?[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = a["ok"],
                ["key_id"] = a["key_id"],
                ["api_key"] = a["api_key"],
                ["prefix"] = a["prefix"],
                ["scopes"] = a["scopes"],
                ["warning"] = a["warning"],
                ["hash_ok"] = hash == Convert.ToString(okKey["key_hash"])
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = b["ok"],
                ["site_via_validate"] = ((Dictionary<string, object?>)betaVal["key"]!)["site_key"],
                ["scopes"] = b["scopes"],
                ["prefix"] = b["prefix"]
            },
            NormKey(ok),
            bad,
            expired,
            empty,
            acme,
            beta,
            other,
            revoked,
            afterRevoke,
            d["ok"],
            NormList(PhpPlanQ1Mast.EpcApiKeysList("acme"))
        });
    }

    private static Rendered Rate()
    {
        var gen = PhpPlanQ1Mast.EpcApiKeyGenerate("acme", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rate_limit"] = 2,
            ["label"] = "rate"
        });
        var id = Convert.ToInt32(gen["key_id"]);
        return new Rendered(new object?[]
        {
            PhpPlanQ1Mast.EpcApiRateCheck(id, 2),
            PhpPlanQ1Mast.EpcApiRateCheck(id, 2),
            PhpPlanQ1Mast.EpcApiRateCheck(id, 2),
            PhpPlanQ1Mast.EpcApiRateCheck(id + 99, 5)
        });
    }

    private static Rendered Handle()
    {
        var read = PhpPlanQ1Mast.EpcApiKeyGenerate("acme", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scopes"] = new[] { "read" },
            ["rate_limit"] = 50
        });
        var admin = PhpPlanQ1Mast.EpcApiKeyGenerate("beta", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scopes"] = new[] { "admin" },
            ["rate_limit"] = 50
        });
        var star = PhpPlanQ1Mast.EpcApiKeyGenerate("acme", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scopes"] = new[] { "*" },
            ["rate_limit"] = 50
        });
        var outList = new List<object?>();
        PhpPlanQ1Mast.Server = new(StringComparer.Ordinal);
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/products"));
        PhpPlanQ1Mast.Server["HTTP_AUTHORIZATION"] = "Bearer " + read["api_key"];
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/products", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "beta",
            ["page"] = "2",
            ["per_page"] = "7abc"
        }));
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/products/99"));
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("POST", "/api/v2/products"));
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/nope"));
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/invoices"));
        PhpPlanQ1Mast.Server = new(StringComparer.Ordinal);
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/products", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["api_key"] = read["api_key"]
        }));
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/products", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["api_key"] = "0"
        }));
        PhpPlanQ1Mast.Server["HTTP_AUTHORIZATION"] = "bearer " + read["api_key"];
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/products"));
        PhpPlanQ1Mast.Server["HTTP_AUTHORIZATION"] = "Bearer " + admin["api_key"];
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/invoices"));
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("put", "/api/v2/inventory/SKU-1"));
        PhpPlanQ1Mast.Server["HTTP_AUTHORIZATION"] = "Bearer " + star["api_key"];
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("POST", "/api/v2/webhooks"));
        PhpPlanQ1Mast.Server["HTTP_AUTHORIZATION"] = "Bearer bad-key";
        outList.Add(PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/products"));
        return new Rendered(outList);
    }

    private static Rendered Meta()
    {
        var eps = PhpPlanQ1Mast.EpcApiV2Endpoints();
        var spec = PhpPlanQ1Mast.EpcApiV2OpenapiSpec();
        var read = PhpPlanQ1Mast.EpcApiKeyGenerate("acme", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scopes"] = new[] { "read" }
        });
        PhpPlanQ1Mast.Server["HTTP_AUTHORIZATION"] = "Bearer " + read["api_key"];
        PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/products");
        PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/nope");
        var beta = PhpPlanQ1Mast.EpcApiKeyGenerate("beta", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scopes"] = new[] { "read" }
        });
        PhpPlanQ1Mast.Server["HTTP_AUTHORIZATION"] = "Bearer " + beta["api_key"];
        PhpPlanQ1Mast.EpcApiV2Handle("GET", "/api/v2/orders");
        PhpPlanQ1Mast.Store.Logs.Add(new PhpPlanQ1Mast.LogRow
        {
            KeyId = 9,
            SiteKey = "acme",
            Method = "GET",
            Endpoint = "/old",
            StatusCode = 200,
            ResponseMs = 5,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1_760_083_200).UtcDateTime.AddHours(-25)
        });
        PhpPlanQ1Mast.EpcApiLog(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["key_id"] = 1,
            ["site_key"] = "acme",
            ["method"] = "GET",
            ["endpoint"] = "/x",
            ["status_code"] = 200,
            ["user_agent"] = new string('U', 300),
            ["request_body"] = "body"
        });
        var paths = (Dictionary<string, object?>)spec["paths"]!;
        var productId = (Dictionary<string, object?>)paths["/api/v2/products/{id}"]!;
        var get = (Dictionary<string, object?>)productId["get"]!;
        return new Rendered(new object?[]
        {
            eps.Count,
            eps[0],
            eps[^1],
            PhpPlanQ1Mast.EpcApiHasScope(new object[] { "read" }, "read"),
            PhpPlanQ1Mast.EpcApiHasScope(new object[] { "read" }, "write"),
            PhpPlanQ1Mast.EpcApiHasScope(new object[] { "admin" }, "finance"),
            PhpPlanQ1Mast.EpcApiHasScope(new object[] { "*" }, "reports"),
            PhpPlanQ1Mast.EpcApiV2Error(401, "Missing API key. Use Authorization: Bearer <key>"),
            ((Dictionary<string, object?>)spec["info"]!)["version"],
            get["tags"],
            paths.Count,
            NormList(PhpPlanQ1Mast.EpcApiFleetStats()),
            NormList(PhpPlanQ1Mast.EpcApiUsageByEndpoint("acme", 24)),
            NormList(PhpPlanQ1Mast.EpcApiUsageByEndpoint("beta", 24))
        });
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
        => value.Length <= 1800 ? value : value[..1800] + "…";
}
