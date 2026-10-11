using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1IsleParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Isle");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Isle_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_api_v1.php", PhpPlanQ1Isle.ApiV1Path);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Isle.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Isle.ApiV1Path, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Isle.EpcApiV1RoutePath(), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Isle.Reset();
        PhpPlanQ1Isle.Clock = () => 1_700_000_000;
        return name switch
        {
            "names" => Names(),
            "auth" => Auth(),
            "shop" => Shop(),
            "dispatch" => Dispatch(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Names()
    {
        PhpPlanQ1Isle.RequestUri = "/epc-api/v1";
        var r1 = PhpPlanQ1Isle.EpcApiV1RoutePath();
        PhpPlanQ1Isle.RequestUri = "/epc-api/v1/";
        var r2 = PhpPlanQ1Isle.EpcApiV1RoutePath();
        PhpPlanQ1Isle.RequestUri = "/epc-api/v1/health";
        var r3 = PhpPlanQ1Isle.EpcApiV1RoutePath();
        PhpPlanQ1Isle.RequestUri = "/epc-api/v1/tenant/info?x=1";
        var r4 = PhpPlanQ1Isle.EpcApiV1RoutePath();
        PhpPlanQ1Isle.RequestUri = "/other";
        var r5 = PhpPlanQ1Isle.EpcApiV1RoutePath();
        PhpPlanQ1Isle.RequestUri = "/";
        var r6 = PhpPlanQ1Isle.EpcApiV1RoutePath();
        PhpPlanQ1Isle.HeaderXApiKey = "";
        PhpPlanQ1Isle.HeaderAuthorization = "";
        var k0 = PhpPlanQ1Isle.EpcApiV1ExtractKey();
        PhpPlanQ1Isle.HeaderXApiKey = "  key-one  ";
        var k1 = PhpPlanQ1Isle.EpcApiV1ExtractKey();
        PhpPlanQ1Isle.HeaderXApiKey = "";
        PhpPlanQ1Isle.HeaderAuthorization = "Bearer tok-99";
        var k2 = PhpPlanQ1Isle.EpcApiV1ExtractKey();
        PhpPlanQ1Isle.HeaderXApiKey = "0";
        PhpPlanQ1Isle.HeaderAuthorization = "Bearer fallback";
        var k3 = PhpPlanQ1Isle.EpcApiV1ExtractKey();
        PhpPlanQ1Isle.EpcApiV1Error(401, "missing_api_key", "O'Reilly & Co");
        var err = Payload();
        PhpPlanQ1Isle.EpcApiV1Ok(new Dictionary<string, object?>(StringComparer.Ordinal) { ["n"] = 1, ["href"] = "https://www.ecomae.com/x" });
        var ok = Payload();
        return new object?[]
        {
            r1, r2, r3, r4, r5, r6, k0, k1, k2, k3,
            PhpPlanQ1Isle.EpcApiV1ParseScopes("nope"),
            PhpPlanQ1Isle.EpcApiV1ParseScopes("[\"read:orders\",\"\",0,\"read:products\"]"),
            PhpPlanQ1Isle.EpcApiV1ScopeAllowed(["*"], "read:x") ? 1 : 0,
            PhpPlanQ1Isle.EpcApiV1ScopeAllowed(["read:*"], "read:x") ? 1 : 0,
            PhpPlanQ1Isle.EpcApiV1ScopeAllowed(["read:orders"], "read:orders") ? 1 : 0,
            PhpPlanQ1Isle.EpcApiV1ScopeAllowed(["read:orders"], "read:erp") ? 1 : 0,
            err, ok
        };
    }

    private static object Auth()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_isle_a_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            PhpPlanQ1Isle.EpcApiV1EnsureKeysTable(db);
            SeedSettings(db);
            InsertKey(db, "acme_parts", "isle-acme", "[\"read:tenant\",\"read:orders\"]", 1, "O'Reilly");
            InsertKey(db, "ghost", "isle-ghost", "[\"*\"]", 1, "Ghost");
            InsertKey(db, "acme_parts", "isle-dead", "[\"read:tenant\"]", 0, "Dead");
            BindTenants();
            PhpPlanQ1Isle.HeaderXApiKey = "";
            PhpPlanQ1Isle.HeaderAuthorization = "";
            var missRet = PhpPlanQ1Isle.EpcApiV1Auth(db, "read:tenant");
            var miss = Payload();
            PhpPlanQ1Isle.HeaderXApiKey = "nope";
            var badRet = PhpPlanQ1Isle.EpcApiV1Auth(db, "read:tenant");
            var bad = Payload();
            PhpPlanQ1Isle.HeaderXApiKey = "isle-dead";
            var deadRet = PhpPlanQ1Isle.EpcApiV1Auth(db, "read:tenant");
            var dead = Payload();
            PhpPlanQ1Isle.HeaderXApiKey = "isle-acme";
            var scopeRet = PhpPlanQ1Isle.EpcApiV1Auth(db, "read:erp");
            var scope = Payload();
            var okRet = PhpPlanQ1Isle.EpcApiV1Auth(db, "read:tenant");
            var used = ScalarInt(db, "SELECT `last_used_at` FROM `epc_api_keys` WHERE `label` = 'O\\'Reilly'");
            PhpPlanQ1Isle.HeaderXApiKey = "isle-ghost";
            var ghostRet = PhpPlanQ1Isle.EpcApiV1Auth(db);
            var ghost = Payload();
            return new object?[]
            {
                missRet is null ? 1 : 0, miss,
                badRet is null ? 1 : 0, bad,
                deadRet is null ? 1 : 0, dead,
                scopeRet is null ? 1 : 0, scope,
                okRet is not null ? 1 : 0,
                okRet is not null ? Convert.ToString(((Dictionary<string, object?>)okRet["tenant"]!)["site_key"]) : "",
                okRet is not null ? Convert.ToString(((Dictionary<string, object?>)okRet["key"]!)["label"]) : "",
                okRet?.GetValueOrDefault("scopes") ?? new List<string>(),
                used > 0 ? 1 : 0,
                ghostRet is null ? 1 : 0, ghost,
                PhpPlanQ1Isle.EpcApiV1TenantAccessMode(db, Tenants()["acme_parts"]),
                PhpPlanQ1Isle.EpcApiV1TenantAccessMode(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["hostname"] = "www.beta.test" }),
                PhpPlanQ1Isle.EpcApiV1TenantAccessMode(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["hostname"] = "" }),
                PhpPlanQ1Isle.EpcApiV1TenantAccessMode(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["hostname"] = "none.test" })
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Shop()
    {
        using var admin = OpenAdmin();
        var platSchema = "ecomae_cpw_isle_p_" + Guid.NewGuid().ToString("N")[..8];
        var acmeSchema = "ecomae_cpw_isle_t_" + Guid.NewGuid().ToString("N")[..8];
        var betaSchema = "ecomae_cpw_isle_b_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{platSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        Exec(admin, $"CREATE DATABASE `{acmeSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        Exec(admin, $"CREATE DATABASE `{betaSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var plat = OpenDb(platSchema);
            using var acme = OpenDb(acmeSchema);
            using var beta = OpenDb(betaSchema);
            PhpPlanQ1Isle.EpcApiV1EnsureKeysTable(plat);
            InsertKey(plat, "acme_parts", "isle-acme", "[\"read:orders\",\"read:products\",\"read:erp\",\"read:bi\"]");
            InsertKey(plat, "beta", "isle-beta", "[\"read:products\"]");
            foreach (var db in new[] { acme, beta })
            {
                Exec(db, """
                    CREATE TABLE `shop_catalogue_products` (
                        `id` INT NOT NULL PRIMARY KEY,
                        `caption` VARCHAR(200) NOT NULL,
                        `alias` VARCHAR(120) NOT NULL,
                        `category_id` INT NOT NULL DEFAULT 0,
                        `published_flag` TINYINT NOT NULL DEFAULT 1
                    )
                    """);
                Exec(db, """
                    CREATE TABLE `shop_orders` (
                        `id` INT NOT NULL PRIMARY KEY,
                        `time` INT NOT NULL,
                        `user_id` INT NOT NULL,
                        `paid` TINYINT NOT NULL DEFAULT 0,
                        `paid_type` INT NOT NULL DEFAULT 0,
                        `successfully_created` TINYINT NOT NULL DEFAULT 1
                    )
                    """);
            }

            Exec(acme, "INSERT INTO `shop_catalogue_products` VALUES (21,'Pixel 8','pixel-8',11,1),(22,'Hidden','hid',11,0)");
            Exec(beta, "INSERT INTO `shop_catalogue_products` VALUES (31,'Beta Cable','beta-cable',4,1)");
            Exec(acme, "INSERT INTO `shop_orders` VALUES (9,1700000000,21,1,2,1),(8,10,1,0,0,0)");
            BindTenants();
            PhpPlanQ1Isle.TenantPdo = row => Convert.ToString(row.GetValueOrDefault("site_key")) switch
            {
                "acme_parts" => acme,
                "beta" => beta,
                _ => null
            };
            PhpPlanQ1Isle.ErpOrderStatusNameSql = _ => "'open'";
            PhpPlanQ1Isle.ErpDashboard = _ => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["date_from"] = 1700000000, ["date_to"] = 1700086400, ["order_count"] = 2,
                ["revenue_ex_vat"] = 10.126, ["profit_ex_vat"] = 1, ["receivable_due_orders"] = 0,
                ["customer_ledger_balance"] = 3.1, ["payable_balance"] = 0, ["cash_bank_total"] = 8,
                ["vat_net_payable"] = 0.5, ["vat_net_status"] = "payable"
            };
            PhpPlanQ1Isle.HeaderXApiKey = "isle-acme";
            PhpPlanQ1Isle.EpcApiV1HandleProductsSearch(plat);
            var missQ = Payload();
            PhpPlanQ1Isle.Query["q"] = "Pixel";
            PhpPlanQ1Isle.EpcApiV1HandleProductsSearch(plat);
            var acmeHit = Payload();
            PhpPlanQ1Isle.HeaderXApiKey = "isle-beta";
            PhpPlanQ1Isle.EpcApiV1HandleProductsSearch(plat);
            var betaMiss = Payload();
            PhpPlanQ1Isle.Query["q"] = "Beta";
            PhpPlanQ1Isle.EpcApiV1HandleProductsSearch(plat);
            var betaHit = Payload();
            PhpPlanQ1Isle.HeaderXApiKey = "isle-acme";
            PhpPlanQ1Isle.Query["limit"] = "50";
            PhpPlanQ1Isle.EpcApiV1HandleOrders(plat);
            var orders = Payload();
            PhpPlanQ1Isle.EpcApiV1HandleErpDashboard(plat);
            var dash = Payload();
            PhpPlanQ1Isle.HeaderXApiKey = "isle-beta";
            PhpPlanQ1Isle.EpcApiV1HandleErpDashboard(plat);
            var dashDeny = Payload();
            var nodb = PhpPlanQ1Isle.EpcApiV1TenantPdo(new Dictionary<string, object?>(StringComparer.Ordinal) { ["site_key"] = "none", ["db_name"] = "" });
            return new object?[] { missQ, acmeHit, betaMiss, betaHit, orders, dash, dashDeny, nodb is null ? 1 : 0 };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{platSchema}`");
            Exec(admin, $"DROP DATABASE IF EXISTS `{acmeSchema}`");
            Exec(admin, $"DROP DATABASE IF EXISTS `{betaSchema}`");
        }
    }

    private static object Dispatch()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_isle_d_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            PhpPlanQ1Isle.EpcApiV1EnsureKeysTable(db);
            SeedSettings(db);
            InsertKey(db, "acme_parts", "isle-acme", "[\"read:tenant\",\"read:bi\",\"read:erp\"]");
            BindTenants();
            PhpPlanQ1Isle.PlatformPdo = () => db;
            PhpPlanQ1Isle.TenantPdo = row => Convert.ToString(row.GetValueOrDefault("site_key")) == "acme_parts" ? db : null;
            PhpPlanQ1Isle.CapabilityCategories = () => new Dictionary<string, object?>(StringComparer.Ordinal) { ["cp"] = 3, ["bos"] = 2 };
            PhpPlanQ1Isle.CapabilityCount = () => 5;
            PhpPlanQ1Isle.PowerBiCapabilities = () => new[] { "kpis", "orders" };
            PhpPlanQ1Isle.PowerBiDatasetCatalog = baseUrl => new object[]
            {
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = "kpis",
                    ["url"] = baseUrl.TrimEnd('/') + "/epc-api/v1/powerbi/kpis"
                }
            };
            PhpPlanQ1Isle.RequestUri = "/epc-api/v1/health";
            PhpPlanQ1Isle.EpcApiV1Dispatch();
            var health = Payload();
            if (health.TryGetValue("time", out _))
            {
                health["time"] = "ISO";
            }

            PhpPlanQ1Isle.RequestUri = "/epc-api/v1/capabilities";
            PhpPlanQ1Isle.EpcApiV1Dispatch();
            var caps = Payload();
            PhpPlanQ1Isle.RequestUri = "/epc-api/v1/nope";
            PhpPlanQ1Isle.EpcApiV1Dispatch();
            var unknown = Payload();
            PhpPlanQ1Isle.RequestUri = "/epc-api/v1/openapi.json";
            PhpPlanQ1Isle.EpcApiV1Dispatch();
            var spec = Payload();
            PhpPlanQ1Isle.RequestUri = "/epc-api/v1/tenant/info";
            PhpPlanQ1Isle.HeaderXApiKey = "";
            PhpPlanQ1Isle.EpcApiV1Dispatch();
            var noKey = Payload();
            PhpPlanQ1Isle.HeaderXApiKey = "isle-acme";
            PhpPlanQ1Isle.EpcApiV1Dispatch();
            var info = Payload();
            PhpPlanQ1Isle.RequestUri = "/epc-api/v1/powerbi/catalog";
            PhpPlanQ1Isle.EpcApiV1Dispatch();
            var bi = Payload();
            var dataset = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["headers"] = new List<string> { "a", "b" },
                ["rows"] = new List<object?[]> { new object?[] { "x", 1 } },
                ["meta"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["n"] = 1 }
            };
            PhpPlanQ1Isle.EpcApiV1PowerbiRespond(dataset, "kpis", "acme_parts");
            var rows = Payload();
            PhpPlanQ1Isle.PowerBiWantsCsv = () => true;
            PhpPlanQ1Isle.EpcApiV1PowerbiRespond(dataset, "kpis", "acme_parts");
            var csv = PhpPlanQ1Isle.LastOutput;
            var plat = PhpPlanQ1Isle.EpcApiV1PlatformPdo() is not null ? 1 : 0;
            return new object?[] { health, caps, unknown, spec, noKey, info, bi, rows, csv, plat };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static void BindTenants()
    {
        var map = Tenants();
        PhpPlanQ1Isle.GetTenant = (_, key) => map.TryGetValue(key, out var t) ? t : null;
    }

    private static Dictionary<string, Dictionary<string, object?>> Tenants()
        => new(StringComparer.Ordinal)
        {
            ["acme_parts"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = "acme_parts", ["trade_name"] = "Acme Parts", ["hostname"] = "www.acme.test",
                ["industry_code"] = "auto_parts", ["status"] = "live", ["erp_only_shared"] = 0, ["db_name"] = ""
            },
            ["beta"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = "beta", ["trade_name"] = "", ["hostname"] = "www.beta.test",
                ["industry_code"] = "auto_parts", ["status"] = "demo", ["erp_only_shared"] = 1
            }
        };

    private static Dictionary<string, object?> Payload()
        => JsonSerializer.Deserialize<Dictionary<string, object?>>(PhpPlanQ1Isle.LastOutput, new JsonSerializerOptions { PropertyNameCaseInsensitive = false })!;

    private static void SeedSettings(MySqlConnection db)
    {
        Exec(db, """
            CREATE TABLE `epc_portal_site_settings` (
                `host` VARCHAR(120) NOT NULL PRIMARY KEY,
                `access_mode` VARCHAR(24) NOT NULL DEFAULT ''
            )
            """);
        Exec(db, "INSERT INTO `epc_portal_site_settings` (`host`,`access_mode`) VALUES ('www.acme.test','catalog'), ('beta.test','')");
    }

    private static void InsertKey(MySqlConnection db, string site, string raw, string scopes, int active = 1, string label = "Ops")
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO `epc_api_keys` (`tenant_site_key`,`key_hash`,`key_prefix`,`label`,`scopes_json`,`active`,`created_at`,`last_used_at`) VALUES (@s,@h,@p,@l,@sc,@a,10,0)";
        cmd.Parameters.AddWithValue("@s", site);
        cmd.Parameters.AddWithValue("@h", hash);
        cmd.Parameters.AddWithValue("@p", raw.Length >= 8 ? raw[..8] : raw);
        cmd.Parameters.AddWithValue("@l", label);
        cmd.Parameters.AddWithValue("@sc", scopes);
        cmd.Parameters.AddWithValue("@a", active);
        cmd.ExecuteNonQuery();
    }

    private static int ScalarInt(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

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
