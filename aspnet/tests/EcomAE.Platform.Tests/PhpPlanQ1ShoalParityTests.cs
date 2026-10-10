using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1ShoalParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Shoal");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Shoal_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_integrations_helpers.php", PhpPlanQ1Shoal.IntegrationsHelpersPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Shoal.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Shoal.IntegrationsHelpersPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Shoal.EpcIntH("x"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Shoal.Reset();
        return name switch
        {
            "names" => Names(),
            "flags" => Flags(),
            "config" => Config(),
            "hub" => Hub(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Names()
    {
        PhpPlanQ1Shoal.BackendDir = "cp";
        var cat = PhpPlanQ1Shoal.EpcIntegrationsCatalog();
        var keys = cat.Keys.ToList();
        return new object?[]
        {
            PhpPlanQ1Shoal.EpcIntH("O'Reilly & Co"),
            PhpPlanQ1Shoal.EpcIntBackend(),
            PhpPlanQ1Shoal.EpcIntegrationsCategories().Keys.ToList(),
            PhpPlanQ1Shoal.EpcIntegrationsResolveGuide("", ""),
            PhpPlanQ1Shoal.EpcIntegrationsResolveGuide("", "pos"),
            PhpPlanQ1Shoal.EpcIntegrationsResolveGuide("/cp/x", "pos"),
            PhpPlanQ1Shoal.EpcIntegrationsResolveGuide("https://e.com/g", "pos"),
            PhpPlanQ1Shoal.EpcIntegrationsResolveGuide("docs/foo", "pos"),
            PhpPlanQ1Shoal.EpcIntegrationsResolveGuide("#bar", ""),
            PhpPlanQ1Shoal.EpcIntegrationsResolveGuide("other", ""),
            keys.Count,
            keys,
            Truthy(cat["oauth"].GetValueOrDefault("super_only_config")) ? 1 : 0,
            Convert.ToString(cat["tenant_registry"].GetValueOrDefault("tenant_url")) ?? "",
            cat["whatsapp"].GetValueOrDefault("menu_patterns"),
            cat["custom_shipping"].GetValueOrDefault("tenant_url")
        };
    }

    private static object Flags()
    {
        PhpPlanQ1Shoal.BackendDir = "CP/";
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_shoal_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            SeedTables(db);
            PhpPlanQ1Shoal.PlatformPdo = () => db;
            PhpPlanQ1Shoal.IsSuperHost = () => true;
            var superKey = PhpPlanQ1Shoal.EpcIntegrationsSiteKey(db);
            PhpPlanQ1Shoal.IsSuperHost = () => false;
            PhpPlanQ1Shoal.Host = () => "www.acme.test";
            PhpPlanQ1Shoal.Tenants.AddRange(Tenants());
            var acme = PhpPlanQ1Shoal.EpcIntegrationsSiteKey(db);
            PhpPlanQ1Shoal.Host = () => "unknown.host";
            var slug = PhpPlanQ1Shoal.EpcIntegrationsSiteKey(db);
            var defaults = PhpPlanQ1Shoal.EpcIntegrationsFeaturesForSite("platform", db);
            var unknown = PhpPlanQ1Shoal.EpcIntegrationsFeatureEnabled("nope", "acme_parts", db) ? 1 : 0;
            var before = PhpPlanQ1Shoal.EpcIntegrationsFeatureEnabled("pos", "acme_parts", db) ? 1 : 0;
            var save = PhpPlanQ1Shoal.EpcIntegrationsSaveFeatureFlags(db, "acme_parts", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pos"] = 0,
                ["nope!"] = 1,
                ["oauth"] = 1
            });
            var rows = new List<Dictionary<string, object?>>();
            using (var st = db.CreateCommand())
            {
                st.CommandText = "SELECT `feature_key`,`enabled` FROM `epc_tenant_feature_flags` WHERE `site_key`=@k ORDER BY `feature_key`";
                st.Parameters.AddWithValue("@k", "acme_parts");
                using var reader = st.ExecuteReader();
                while (reader.Read())
                {
                    rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["feature_key"] = reader.GetString(0),
                        ["enabled"] = Convert.ToInt32(reader.GetValue(1))
                    });
                }
            }

            return new object?[]
            {
                PhpPlanQ1Shoal.EpcIntBackend(),
                superKey,
                acme,
                slug,
                defaults.Count,
                defaults.TryGetValue("pos", out var posOn) && posOn ? 1 : 0,
                unknown,
                before,
                save,
                rows
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Config()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_shoal_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            SeedTables(db);
            PhpPlanQ1Shoal.PlatformPdo = () => db;
            PhpPlanQ1Shoal.Settings = new Dictionary<string, object?>(StringComparer.Ordinal);
            var empty = PhpPlanQ1Shoal.EpcIntegrationsLoadTenantConfig(db);
            var save = PhpPlanQ1Shoal.EpcIntegrationsSaveTenantConfig(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mobile"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["app_name"] = "O'Reilly",
                    ["enabled"] = true
                }
            });
            var mobile = PhpPlanQ1Shoal.EpcIntegrationsMobileConfig(db);
            var plat = PhpPlanQ1Shoal.EpcIntegrationsPlatformMobileDefaults();
            var def = PhpPlanQ1Shoal.EpcIntegrationsDefaultMobileConfig();
            PhpPlanQ1Shoal.IsSuperHost = () => true;
            var superBlock = PhpPlanQ1Shoal.EpcIntegrationsMenuBlockedByFeature("/cp/shop/pos/terminal") ? 1 : 0;
            PhpPlanQ1Shoal.IsSuperHost = () => false;
            PhpPlanQ1Shoal.Host = () => "www.acme.test";
            PhpPlanQ1Shoal.Tenants.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["hostname"] = "www.acme.test",
                ["site_key"] = "acme_parts"
            });
            PhpPlanQ1Shoal.EpcIntegrationsSaveFeatureFlags(db, "acme_parts", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pos"] = 0,
                ["web_tracker"] = 0
            });
            var pos = PhpPlanQ1Shoal.EpcIntegrationsMenuBlockedByFeature("/cp/shop/pos/terminal") ? 1 : 0;
            var pay = PhpPlanQ1Shoal.EpcIntegrationsMenuBlockedByFeature("/cp/shop/payments/payments") ? 1 : 0;
            var track = PhpPlanQ1Shoal.EpcIntegrationsMenuBlockedByFeature("/cp/control/portal/epc_web_tracker?x=1") ? 1 : 0;
            var home = PhpPlanQ1Shoal.EpcIntegrationsMenuBlockedByFeature("/cp/control/config") ? 1 : 0;
            return new object?[]
            {
                empty.Count == 0 ? Array.Empty<object>() : empty,
                save,
                mobile,
                plat,
                Truthy(def.GetValueOrDefault("pwa_enabled")) ? 1 : 0,
                superBlock,
                pos,
                pay,
                track,
                home
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Hub()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_shoal_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            SeedTables(db);
            PhpPlanQ1Shoal.IsSuperHost = () => false;
            PhpPlanQ1Shoal.Host = () => "www.acme.test";
            PhpPlanQ1Shoal.Tenants.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["hostname"] = "www.acme.test",
                ["site_key"] = "acme_parts"
            });
            PhpPlanQ1Shoal.PlatformPdo = () => db;
            PhpPlanQ1Shoal.EpcIntegrationsSaveFeatureFlags(db, "acme_parts", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["pos"] = 0
            });
            var super = PhpPlanQ1Shoal.EpcIntegrationsHubRows(db, true);
            var tenant = PhpPlanQ1Shoal.EpcIntegrationsHubRows(db, false);
            var proj = tenant.Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["key"] = r["key"],
                ["enabled"] = Truthy(r.GetValueOrDefault("enabled")) ? 1 : 0,
                ["configure_url"] = r["configure_url"],
                ["guide"] = r["guide"],
                ["super_only"] = Truthy(r.GetValueOrDefault("super_only")) ? 1 : 0
            }).ToList();
            var id = PhpPlanQ1Shoal.EpcIntegrationsRegisterCpContent(db, "epc_integrations_hub", "str_int_hub", "Integrations hub", "Интеграции", "epc_integrations_hub" + ".php", 8);
            var again = PhpPlanQ1Shoal.EpcIntegrationsRegisterCpContent(db, "epc_integrations_hub", "str_int_hub", "Integrations hub", "Интеграции", "epc_integrations_hub" + ".php", 8);
            var cnt = ScalarInt(db, "SELECT COUNT(*) FROM `content` WHERE `url`='control/portal/epc_integrations_hub'");
            var groups = ScalarInt(db, "SELECT COUNT(*) FROM `content_access` WHERE `content_id`=" + id);
            return new object?[]
            {
                super.Count,
                tenant.Count,
                super.Count > 1 ? super[1]["key"] : "",
                proj,
                id,
                again,
                cnt,
                groups
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
                ["hostname"] = "www.acme.test",
                ["site_key"] = "acme_parts"
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["hostname"] = "beta.trading",
                ["site_key"] = "beta"
            }
        ];

    private static void SeedTables(MySqlConnection db)
    {
        Exec(db, """
            CREATE TABLE `epc_portal_site_settings` (
                `id` INT NOT NULL PRIMARY KEY,
                `cp_menu_json` TEXT NULL,
                `integrations_json` TEXT NULL
            )
            """);
        Exec(db, """
            CREATE TABLE `lang_text_strings` (
                `str_key` VARCHAR(64) NOT NULL PRIMARY KEY,
                `description` VARCHAR(255) NULL,
                `same` VARCHAR(64) NULL,
                `is_error` TINYINT NOT NULL DEFAULT 0,
                `is_custom` TINYINT NOT NULL DEFAULT 0,
                `used_found` TINYINT NOT NULL DEFAULT 0
            )
            """);
        Exec(db, """
            CREATE TABLE `lang_text_strings_translation` (
                `str_key` VARCHAR(64) NOT NULL,
                `lang_code` VARCHAR(8) NOT NULL,
                `value` VARCHAR(255) NOT NULL,
                PRIMARY KEY (`str_key`, `lang_code`)
            )
            """);
        Exec(db, """
            CREATE TABLE `content` (
                `id` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                `count` INT NOT NULL DEFAULT 0,
                `url` VARCHAR(190) NOT NULL DEFAULT '',
                `level` INT NOT NULL DEFAULT 0,
                `alias` VARCHAR(64) NOT NULL DEFAULT '',
                `value` VARCHAR(64) NOT NULL DEFAULT '',
                `parent` INT NOT NULL DEFAULT 0,
                `description` VARCHAR(255) NOT NULL DEFAULT '',
                `is_frontend` TINYINT NOT NULL DEFAULT 0,
                `content_type` VARCHAR(16) NOT NULL DEFAULT '',
                `content` VARCHAR(255) NOT NULL DEFAULT '',
                `title_tag` VARCHAR(64) NOT NULL DEFAULT '',
                `description_tag` VARCHAR(16) NOT NULL DEFAULT '',
                `keywords_tag` VARCHAR(16) NOT NULL DEFAULT '',
                `author_tag` VARCHAR(16) NOT NULL DEFAULT '',
                `main_flag` TINYINT NOT NULL DEFAULT 0,
                `modules_array` VARCHAR(16) NOT NULL DEFAULT '',
                `css_js` VARCHAR(16) NOT NULL DEFAULT '',
                `robots_tag` VARCHAR(16) NOT NULL DEFAULT '',
                `system_flag` TINYINT NOT NULL DEFAULT 0,
                `published_flag` TINYINT NOT NULL DEFAULT 0,
                `open` TINYINT NOT NULL DEFAULT 0,
                `time_created` INT NOT NULL DEFAULT 0,
                `time_edited` INT NOT NULL DEFAULT 0,
                `order` INT NOT NULL DEFAULT 0
            )
            """);
        Exec(db, """
            CREATE TABLE `content_access` (
                `content_id` INT NOT NULL,
                `group_id` INT NOT NULL,
                PRIMARY KEY (`content_id`, `group_id`)
            )
            """);
        Exec(db, "INSERT INTO `content` (`url`,`level`,`is_frontend`,`alias`) VALUES ('control/config',1,0,'config'),('control/portal/industry_settings',2,0,'industry_settings')");
        Exec(db, "INSERT INTO `content_access` (`content_id`,`group_id`) VALUES (2,7),(2,9)");
    }

    private static bool Truthy(object? value)
        => value is true or 1 or 1L or 1.0 or "1";

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

    private static int ScalarInt(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
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
