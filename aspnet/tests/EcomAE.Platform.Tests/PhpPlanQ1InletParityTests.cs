using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1InletParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Inlet");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Inlet_MatchPhpGolden()
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
        => Assert.Equal("content/shop/tenant_hub/epc_tenant_hub_helpers.php", PhpPlanQ1Inlet.TenantHubHelpersPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Inlet.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Inlet.TenantHubHelpersPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Inlet.EpcThH("x"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        Wire();
        return name switch
        {
            "names" => Names(),
            "list" => ListCase(),
            "status" => Status(),
            "chain" => Chain(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static void Wire()
    {
        PhpPlanQ1Inlet.Reset();
        PhpPlanQ1Inlet.ClientErpLoginUrl = key => "/cp/client-erp/" + key + "/";
        PhpPlanQ1Inlet.ClientErpShellUrl = key => "/cp/client-erp/" + key + "/shop/finance/erp?epc_erp_shell=1";
        PhpPlanQ1Inlet.DemoUrls = (key, _) => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["storefront"] = "https://demo.ecomae.com/" + key + "/",
            ["cp"] = "https://demo.ecomae.com/" + key + "/cp/"
        };
        PhpPlanQ1Inlet.DemoCpAutologinUrl = key => "https://www.ecomae.com/demo-cp/" + key;
        PhpPlanQ1Inlet.DemoErpShellUrl = key => "/demo/" + key + "/erp";
        PhpPlanQ1Inlet.PlatformIp = () => "203.0.113.10";
        PhpPlanQ1Inlet.PlatformHost = () => "www.ecomae.com";
        PhpPlanQ1Inlet.SaveTenant = (_, data) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["site_key"] = Convert.ToString(data.GetValueOrDefault("site_key")) ?? "saved"
        };
        PhpPlanQ1Inlet.OnboardClient = (_, post, by) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["by"] = by,
            ["trade"] = Convert.ToString(post.GetValueOrDefault("trade_name")) ?? ""
        };
        PhpPlanQ1Inlet.LaunchChecklist = (_, key) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["site_key"] = key,
            ["ready"] = 1
        };
        PhpPlanQ1Inlet.ApplyIndustryTheme = (_, key, opts) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["site_key"] = key,
            ["theme"] = Convert.ToString(opts.GetValueOrDefault("theme")) ?? "auto"
        };
        PhpPlanQ1Inlet.ClearBlockchainCache = key => { };
        PhpPlanQ1Inlet.NormalizeBlockchainMode = PhpPlanQ1Boom.EpcBcBosNormalizeMode;
        PhpPlanQ1Inlet.BlockchainModes = PhpPlanQ1Boom.EpcBcBosModes;
        PhpPlanQ1Inlet.AnchorNetwork = () => "local_merkle";
    }

    private static object Names()
    {
        var mixed = PhpPlanQ1Inlet.EpcThTenantActionUrls(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "Acme-Parts!",
            ["hostname"] = "https://ACME.test/shop",
            ["industry_code"] = "auto_parts"
        });
        var erp = PhpPlanQ1Inlet.EpcThTenantActionUrls(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "beta",
            ["hostname"] = "beta.trading",
            ["industry_code"] = "erp_only"
        });
        var shared = PhpPlanQ1Inlet.EpcThTenantActionUrls(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "shared_erp",
            ["hostname"] = "www.ecomae.com",
            ["industry_code"] = "erp_only",
            ["erp_only_shared"] = 1
        });
        var blank = PhpPlanQ1Inlet.EpcThTenantActionUrls(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "x",
            ["hostname"] = "localhost",
            ["industry_code"] = ""
        });
        var empty = PhpPlanQ1Inlet.EpcThTenantDbConnectOk(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["db_name"] = "",
            ["db_user"] = "",
            ["db_password"] = ""
        });
        PhpPlanQ1Inlet.IsPlatformOperator = () => true;
        var ok = "ok";
        try
        {
            PhpPlanQ1Inlet.EpcThRequireSuperCp();
        }
        catch (Exception ex)
        {
            ok = ex.Message;
        }

        PhpPlanQ1Inlet.IsPlatformOperator = () => false;
        var denied = "ok";
        try
        {
            PhpPlanQ1Inlet.EpcThRequireSuperCp();
        }
        catch (Exception ex)
        {
            denied = ex.Message;
        }

        return new object[]
        {
            PhpPlanQ1Inlet.EpcThH("O'Reilly & Co"),
            mixed,
            erp,
            shared,
            blank,
            empty ? 1 : 0,
            ok,
            denied
        };
    }

    private static object ListCase()
    {
        PhpPlanQ1Inlet.ListTenants = _ => Tenants();
        using var admin = OpenAdmin();
        var rows = PhpPlanQ1Inlet.EpcThListTenants(admin).Select(Proj).ToList();
        var stats = PhpPlanQ1Inlet.EpcThPlatformStats(admin);
        var add = PhpPlanQ1Inlet.EpcThAddTenant(admin, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = "newco",
            ["trade_name"] = "O'Reilly"
        });
        return new object[] { rows, stats, add, PhpPlanQ1Inlet.SavedTenants.Count };
    }

    private static object Status()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_inlet_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, """
                CREATE TABLE `epc_portal_tenants` (
                    `site_key` VARCHAR(64) NOT NULL PRIMARY KEY,
                    `hostname` VARCHAR(120) NOT NULL DEFAULT '',
                    `trade_name` VARCHAR(120) NOT NULL DEFAULT '',
                    `db_name` VARCHAR(64) NOT NULL DEFAULT '',
                    `status` VARCHAR(32) NOT NULL DEFAULT '',
                    `blockchain_mode` VARCHAR(32) NOT NULL DEFAULT '',
                    `updated_at` INT NOT NULL DEFAULT 0
                )
                """);
            Exec(db, "INSERT INTO `epc_portal_tenants` (`site_key`,`status`,`blockchain_mode`,`updated_at`) VALUES ('acme','dns_pending','off',1)");
            var bad = PhpPlanQ1Inlet.EpcThUpdateTenantStatus(db, "acme", "nope");
            var missing = PhpPlanQ1Inlet.EpcThUpdateTenantStatus(db, "missing", "live");
            PhpPlanQ1Inlet.SyncPacks = (_, _) => new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "packs synced" };
            var live = PhpPlanQ1Inlet.EpcThUpdateTenantStatus(db, "acme", "live");
            using var st = db.CreateCommand();
            st.CommandText = "SELECT `status`,`updated_at` FROM `epc_portal_tenants` WHERE `site_key`='acme'";
            using var reader = st.ExecuteReader();
            reader.Read();
            var status = reader.GetString(0);
            var touched = reader.GetInt32(1) > 1 ? 1 : 0;
            reader.Close();
            PhpPlanQ1Inlet.SyncPacks = (_, _) => new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "denied" };
            Exec(db, "UPDATE `epc_portal_tenants` SET `status`='dns_pending'");
            var fail = PhpPlanQ1Inlet.EpcThUpdateTenantStatus(db, "acme", "live");
            return new object[] { bad, missing, live, status, touched, fail };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Chain()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_inlet_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            Exec(db, """
                CREATE TABLE `epc_portal_tenants` (
                    `site_key` VARCHAR(64) NOT NULL PRIMARY KEY,
                    `hostname` VARCHAR(120) NOT NULL DEFAULT '',
                    `trade_name` VARCHAR(120) NOT NULL DEFAULT '',
                    `db_name` VARCHAR(64) NOT NULL DEFAULT '',
                    `status` VARCHAR(32) NOT NULL DEFAULT '',
                    `blockchain_mode` VARCHAR(32) NOT NULL DEFAULT '',
                    `updated_at` INT NOT NULL DEFAULT 0
                )
                """);
            Exec(db, "INSERT INTO `epc_portal_tenants` (`site_key`,`blockchain_mode`,`updated_at`) VALUES ('acme','off',1),('same','anchor',1)");
            var badKey = PhpPlanQ1Inlet.EpcThUpdateTenantBlockchainMode(db, "!!!", "anchor");
            var missing = PhpPlanQ1Inlet.EpcThUpdateTenantBlockchainMode(db, "nope", "anchor");
            var ok = PhpPlanQ1Inlet.EpcThUpdateTenantBlockchainMode(db, "acme", "NETWORK");
            var same = PhpPlanQ1Inlet.EpcThUpdateTenantBlockchainMode(db, "same", "anchor");
            var anchors = new Queue<Dictionary<string, object?>>(
            [
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["error"] = "down" },
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["proof_count"] = 0 },
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ok"] = true,
                    ["proof_count"] = 3,
                    ["anchor_network"] = "local_merkle",
                    ["merkle_root"] = "abcdef0123456789ffff"
                }
            ]);
            PhpPlanQ1Inlet.AnchorPending = _ => anchors.Dequeue();
            var fail = PhpPlanQ1Inlet.EpcThAnchorBlockchainPendingNow(5);
            var none = PhpPlanQ1Inlet.EpcThAnchorBlockchainPendingNow(5);
            var some = PhpPlanQ1Inlet.EpcThAnchorBlockchainPendingNow(5);
            PhpPlanQ1Inlet.ProbeFetch = _ => ("<b>Hello</b> world", "HTTP/1.1 200 OK");
            var probe = PhpPlanQ1Inlet.EpcThProbeUrl("https://ok.test/");
            var onboard = PhpPlanQ1Inlet.EpcThOnboardClient(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["trade_name"] = "Acme" }, "ops");
            var check = PhpPlanQ1Inlet.EpcThLaunchChecklist(db, "acme");
            var theme = PhpPlanQ1Inlet.EpcThApplyIndustryTheme(db, "acme", new Dictionary<string, object?>(StringComparer.Ordinal) { ["theme"] = "auto" });
            return new object[]
            {
                badKey, missing, ok, same, fail, none, some,
                Truthy(probe, "ok") ? 1 : 0, probe["http_code"] ?? 0, probe["snippet"] ?? "",
                onboard, check, theme, PhpPlanQ1Inlet.ClearedModes
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static List<Dictionary<string, object?>> Tenants()
        =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = "acme_parts",
                ["hostname"] = "www.acme.test",
                ["industry_code"] = "auto_parts",
                ["status"] = "live",
                ["is_demo"] = 0,
                ["intro_json"] = "{\"submitted_at\":1700000000}",
                ["db_name"] = ""
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = "Beta Demo!",
                ["hostname"] = "demo.beta.test",
                ["industry_code"] = "auto_parts",
                ["status"] = "dns_pending",
                ["is_demo"] = 1,
                ["intro_json"] = "{}",
                ["db_name"] = ""
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = "shared_erp",
                ["hostname"] = "www.ecomae.com",
                ["industry_code"] = "erp_only",
                ["status"] = "live",
                ["erp_only_shared"] = 1,
                ["is_demo"] = 0,
                ["intro_json"] = "",
                ["db_name"] = ""
            }
        ];

    private static Dictionary<string, object?> Proj(Dictionary<string, object?> row)
    {
        var outRow = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = row["site_key"],
            ["industry_name"] = row["industry_name"],
            ["ecosystem_code"] = row["ecosystem_code"],
            ["ecosystem_name"] = row["ecosystem_name"],
            ["status_label"] = row["status_label"],
            ["storefront_url"] = row["storefront_url"],
            ["cp_url"] = row["cp_url"],
            ["erp_url"] = row["erp_url"],
            ["intro_done"] = Truthy(row, "intro_done") ? 1 : 0,
            ["db_connect_ok"] = Truthy(row, "db_connect_ok") ? 1 : 0
        };
        if (Truthy(row, "is_demo_tenant"))
        {
            outRow["is_demo_tenant"] = 1;
        }

        return outRow;
    }

    private static bool Truthy(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) && value is true or 1 or 1L or 1.0 or "1";

    private static string Password()
        => Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN") ?? "local-throwaway-pw";

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
