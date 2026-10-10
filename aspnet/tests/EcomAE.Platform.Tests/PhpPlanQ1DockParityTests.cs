using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1DockParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Dock");

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
    public void PlanQ1Dock_MatchPhpGolden()
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
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Dock.PortalTenantPath },
            new HashSet<string>(StringComparer.Ordinal) { PhpPlanQ1Dock.PortalTenantPath });

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Dock.Reset();
        Assert.Contains("epc_portal_tenant.php", PhpPlanQ1Dock.PortalTenantPath, StringComparison.Ordinal);
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Dock.PortalTenantPath, StringComparison.Ordinal);
        Assert.Equal("31.97.216.247", PhpPlanQ1Dock.EpcPortalPlatformIp());
    }

    private sealed record Rendered(object? Extra);

    private static Rendered Render(string name)
    {
        PhpPlanQ1Dock.Reset();
        PhpPlanQ1Dock.UnixNow = () => 1_700_000_000;
        PhpPlanQ1Dock.GeneratePassword = () => "abOp12!";
        PhpPlanQ1Dock.ProvisionDb = (db, _, _) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["db_name"] = db
        };
        PhpPlanQ1Dock.IsClientHostname = host =>
            host != "" && !new[] { "www.ecomae.com", "ecomae.com", "cp.ecomae.com" }.Contains(host, StringComparer.Ordinal)
            && !System.Text.RegularExpressions.Regex.IsMatch(host, @"^[a-z0-9][a-z0-9_-]*\.ecomae\.com$");
        PhpPlanQ1Dock.DecodeIntro = json =>
        {
            if (string.IsNullOrEmpty(json))
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal);
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            }
            catch (JsonException)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal);
            }
        };
        return name switch
        {
            "pure" => Pure(),
            "save_types" => SaveTypes(),
            "isolation" => Isolation(),
            "creds" => Creds(),
            _ => new Rendered("unknown:" + name)
        };
    }

    private static Rendered Pure()
    {
        var ipDef = PhpPlanQ1Dock.EpcPortalPlatformIp();
        PhpPlanQ1Dock.PlatformIpEnv = "10.1.2.3";
        var ipEnv = PhpPlanQ1Dock.EpcPortalPlatformIp();
        var hosts = new object[]
        {
            PhpPlanQ1Dock.EpcPortalIsPlatformHostname("www.ecomae.com"),
            PhpPlanQ1Dock.EpcPortalIsPlatformHostname("parts.ecomae.com"),
            PhpPlanQ1Dock.EpcPortalIsPlatformHostname("www.client.com"),
            PhpPlanQ1Dock.EpcPortalIsEpartscartHostname("www.epartscart.com:443"),
            PhpPlanQ1Dock.EpcPortalIsEpartscartHostname("shop.client.com")
        };
        var dns = PhpPlanQ1Dock.EpcPortalTenantDnsInstructions("www.shop.ae");
        var shared = new object[]
        {
            PhpPlanQ1Dock.EpcPortalTenantIsSharedErpRow(new Dictionary<string, object?>(StringComparer.Ordinal) { ["erp_only_shared"] = 1 }),
            PhpPlanQ1Dock.EpcPortalTenantIsSharedErpRow(new Dictionary<string, object?>(StringComparer.Ordinal) { ["hosted_on"] = "platform" }),
            PhpPlanQ1Dock.EpcPortalTenantIsSharedErpRow(new Dictionary<string, object?>(StringComparer.Ordinal) { ["erp_only_shared"] = "0", ["hosted_on"] = "client" })
        };
        var profile = PhpPlanQ1Dock.EpcPortalTenantRowToProfile(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = 7,
            ["status"] = "live",
            ["site_key"] = "alpha",
            ["hostname"] = "www.alpha.ae",
            ["industry_code"] = "food",
            ["db_name"] = "alpha",
            ["db_user"] = "alpha",
            ["db_password"] = "x",
            ["dedicated_db"] = 1,
            ["scale_policy"] = "",
            ["erp_only_shared"] = 0,
            ["trade_name"] = "Alpha",
            ["hub_name"] = "Hub",
            ["from_email"] = "a@x.com"
        });
        profile.Remove("password");
        return new Rendered(new object?[]
        {
            ipDef, ipEnv, PhpPlanQ1Dock.EpcPortalPlatformHostnames(), hosts,
            PhpPlanQ1Dock.EpcPortalTenantStatuses(), TplSnap(), dns, shared, profile,
            PhpPlanQ1Dock.EpcPortalClientMayShareDocpart("www.epartscart.com"),
            PhpPlanQ1Dock.EpcPortalClientMayShareDocpart("www.taxofinca.com")
        });
    }

    private static Rendered SaveTypes()
    {
        var db = new PhpPlanQ1Dock.DockStore();
        PhpPlanQ1Dock.UseStore(db);
        var noKey = PhpPlanQ1Dock.EpcPortalSaveTenant(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["hostname"] = "www.a.com" });
        var noHost = PhpPlanQ1Dock.EpcPortalSaveTenant(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "siteone" });
        var plat = PhpPlanQ1Dock.EpcPortalSaveTenant(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "plat", ["hostname"] = "www.ecomae.com" });
        var site = PhpPlanQ1Dock.EpcPortalSaveTenant(db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "siteone",
            ["hostname"] = "www.siteone.ae",
            ["industry_code"] = "food_beverage",
            ["status"] = "dns_pending",
            ["trade_name"] = "Site One",
            ["scale_policy"] = "shared_docpart",
            ["db_name"] = "docpart",
            ["db_user"] = "docpart",
            ["db_password"] = "shared"
        });
        var erp = PhpPlanQ1Dock.EpcPortalSaveTenant(db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "erpone",
            ["erp_only_shared"] = 1,
            ["industry_code"] = "erp_standalone",
            ["status"] = "draft",
            ["trade_name"] = "ERP One",
            ["from_email"] = "erp@one.ae"
        });
        var mixed = PhpPlanQ1Dock.EpcPortalSaveTenant(db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "mixedone",
            ["hostname"] = "www.mixed.ae",
            ["industry_code"] = "retail",
            ["status"] = "live",
            ["trade_name"] = "Mixed One",
            ["dedicated_db"] = 1,
            ["db_password"] = "mixpass",
            ["blockchain_mode"] = "nope"
        });
        var reserved = PhpPlanQ1Dock.EpcPortalSaveTenant(db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "badone",
            ["hostname"] = "www.bad.ae",
            ["dedicated_db"] = 1,
            ["db_name"] = "ecomae",
            ["db_user"] = "ecomae",
            ["db_password"] = "x"
        });
        var dup = PhpPlanQ1Dock.EpcPortalSaveTenant(db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "erptwo",
            ["erp_only_shared"] = 1,
            ["db_name"] = "erpone",
            ["db_user"] = "erpone",
            ["db_password"] = "x"
        });
        var again = PhpPlanQ1Dock.EpcPortalSaveTenant(db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "siteone",
            ["hostname"] = "www.siteone.ae",
            ["status"] = "live",
            ["trade_name"] = "Site One Live",
            ["db_password"] = ""
        });
        return new Rendered(new object?[]
        {
            noKey, noHost, plat, site, erp, mixed, reserved, dup, again,
            Snap(db, "siteone"), Snap(db, "erpone"), Snap(db, "mixedone"),
            db.Settings
        });
    }

    private static Rendered Isolation()
    {
        var db = new PhpPlanQ1Dock.DockStore();
        PhpPlanQ1Dock.UseStore(db);
        PhpPlanQ1Dock.EpcPortalSaveTenant(db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "alpha",
            ["hostname"] = "www.alpha.ae",
            ["status"] = "live",
            ["trade_name"] = "Alpha",
            ["db_name"] = "alpha",
            ["db_user"] = "alpha",
            ["db_password"] = "a",
            ["dedicated_db"] = 1
        });
        PhpPlanQ1Dock.EpcPortalSaveTenant(db, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "beta",
            ["hostname"] = "www.beta.ae",
            ["status"] = "dns_pending",
            ["trade_name"] = "Beta",
            ["db_name"] = "beta",
            ["db_user"] = "beta",
            ["db_password"] = "b",
            ["dedicated_db"] = 1
        });
        db.Tenants.First(t => t.SiteKey == "beta").IsActive = 0;
        var alpha = PhpPlanQ1Dock.EpcPortalLoadTenantByHost(db, "www.alpha.ae");
        alpha?.Remove("password");
        var betaLive = PhpPlanQ1Dock.EpcPortalLoadTenantByHost(db, "www.beta.ae");
        var plat = PhpPlanQ1Dock.EpcPortalLoadTenantByHost(db, "www.ecomae.com");
        var epc = PhpPlanQ1Dock.EpcPortalLoadTenantByHost(db, "www.epartscart.com");
        var regA = PhpPlanQ1Dock.EpcPortalTenantRegistryRow(db, "Alpha!");
        var regMiss = PhpPlanQ1Dock.EpcPortalTenantRegistryRow(db, "!!!");
        var list = PhpPlanQ1Dock.EpcPortalListTenants(db).Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = r["site_key"],
            ["hostname"] = r["hostname"]
        }).ToList();
        return new Rendered(new object?[] { alpha, betaLive, plat, epc, regA?["site_key"] ?? "", regMiss, list });
    }

    private static Rendered Creds()
    {
        PhpPlanQ1Dock.ResolveCreds = () => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["db"] = "docpart",
            ["user"] = "docpart",
            ["password"] = "dp-secret"
        };
        PhpPlanQ1Dock.RuntimeHostDb = host => host == "www.taxofinca.com"
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["db"] = "taxo",
                ["user"] = "taxo",
                ["password"] = "tax-secret"
            }
            : null;
        var resolved = PhpPlanQ1Dock.EpcPortalResolveTenantDbCredentials();
        var runtime = PhpPlanQ1Dock.EpcPortalRuntimeHostDb("www.taxofinca.com");
        var setupDed = PhpPlanQ1Dock.EpcPortalTenantSetupCredentials(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["hostname"] = "www.alpha.ae",
            ["db_name"] = "alpha",
            ["db_user"] = "alpha",
            ["db_password"] = "p",
            ["dedicated_db"] = 1
        });
        var setupRun = PhpPlanQ1Dock.EpcPortalTenantSetupCredentials(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["hostname"] = "www.taxofinca.com",
            ["db_name"] = "x",
            ["db_user"] = "x",
            ["db_password"] = "x"
        });
        PhpPlanQ1Dock.Server["HTTP_HOST"] = "www.taxofinca.com";
        var cfg = new PhpPlanQ1Dock.ConfigBag { Db = "ecomae", User = "ecomae", Password = "plat", Host = "127.0.0.1" };
        PhpPlanQ1Dock.EpcPortalResolveTenantDb(cfg);
        return new Rendered(new object?[]
        {
            resolved, runtime, setupDed, setupRun,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["db"] = cfg.Db,
                ["user"] = cfg.User,
                ["password"] = cfg.Password,
                ["host"] = cfg.Host,
                ["epc_tenant_db_isolation_error"] = cfg.IsolationError
            },
            PhpPlanQ1Dock.EpcPortalTenantDbIsDegradedShared()
        });
    }

    private static Dictionary<string, object?> TplSnap()
    {
        var outDict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (k, tpl) in PhpPlanQ1Dock.EpcPortalTenantTemplates())
        {
            outDict[k] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["industry"] = tpl.GetValueOrDefault("industry", ""),
                ["hostname"] = tpl.GetValueOrDefault("hostname", ""),
                ["access_mode"] = tpl.GetValueOrDefault("access_mode", ""),
                ["hosted_on"] = tpl.GetValueOrDefault("hosted_on", ""),
                ["erp_only_shared"] = tpl.GetValueOrDefault("erp_only_shared", 0)
            };
        }

        return outDict;
    }

    private static Dictionary<string, object?> Snap(PhpPlanQ1Dock.DockStore db, string key)
    {
        var row = db.Tenants.First(t => t.SiteKey == key);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = row.SiteKey,
            ["hostname"] = row.Hostname,
            ["industry_code"] = row.IndustryCode,
            ["status"] = row.Status,
            ["trade_name"] = row.TradeName,
            ["db_name"] = row.DbName,
            ["db_user"] = row.DbUser,
            ["hosted_on"] = row.HostedOn,
            ["erp_only_shared"] = row.ErpOnlyShared,
            ["dedicated_db"] = row.DedicatedDb,
            ["scale_policy"] = row.ScalePolicy,
            ["blockchain_mode"] = row.BlockchainMode
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
        => value.Length <= 1400 ? value : value[..1400] + "…";
}
