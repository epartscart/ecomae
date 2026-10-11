using System.Text.Encodings.Web;
using System.Text.Json;
using EcomAE.Platform.Presentation;
using MySqlConnector;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection("PlanQ1Statics")]
public sealed class PhpPlanQ1GulfParityTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PlanQ1Gulf");

    [Fact]
    public void Golden_IsPhp83RuntimeOutput()
        => Assert.StartsWith("8.3.", JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "golden.json"))).RootElement.GetProperty("php").GetString(), StringComparison.Ordinal);

    [Fact]
    public void PlanQ1Gulf_MatchPhpGolden()
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
        => Assert.Equal("content/general_pages/epc_super_cp_platform.php", PhpPlanQ1Gulf.SuperCpPlatformPath);

    [Fact]
    public void Fragments_DoNotStartASession()
    {
        PhpPlanQ1Gulf.Reset();
        Assert.DoesNotContain("PHPSESSID", PhpPlanQ1Gulf.SuperCpPlatformPath, StringComparison.Ordinal);
        Assert.DoesNotContain("Set-Cookie", PhpPlanQ1Gulf.EpcScpH("x"), StringComparison.Ordinal);
    }

    private static object Render(string name)
    {
        PhpPlanQ1Gulf.Reset();
        PhpPlanQ1Gulf.Clock = () => 1700000000;
        return name switch
        {
            "names" => Names(),
            "price" => Price(),
            "info" => Info(),
            "board" => Board(),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    private static object Names()
    {
        PhpPlanQ1Gulf.BackendDir = "CP/";
        return new object?[]
        {
            PhpPlanQ1Gulf.EpcScpH("O'Reilly & Co"),
            PhpPlanQ1Gulf.EpcScpBackend(),
            PhpPlanQ1Gulf.EpcScpPriceClientTypes().Keys.ToList(),
            PhpPlanQ1Gulf.EpcScpInfoPlacements().Keys.ToList(),
            PhpPlanQ1Gulf.EpcScpTaskCategories().Keys.ToList(),
            PhpPlanQ1Gulf.EpcScpTaskStatuses().Keys.ToList(),
            PhpPlanQ1Gulf.EpcScpTaskPriorities().Keys.ToList(),
            PhpPlanQ1Gulf.EpcScpDefaultCommSettings(),
            PhpPlanQ1Gulf.EpcScpOperatorGuideUrl(),
            PhpPlanQ1Gulf.EpcScpCustomerNameFromRow(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["fname"] = "Ada", ["sname"] = "Lovelace", ["company"] = "X", ["email"] = "ada@x.test"
            }),
            PhpPlanQ1Gulf.EpcScpCustomerNameFromRow(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["fname"] = "", ["sname"] = "", ["company"] = "O'Reilly", ["email"] = "x@y.test"
            }),
            PhpPlanQ1Gulf.EpcScpCustomerNameFromRow(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["fname"] = "", ["sname"] = "", ["company"] = "", ["email"] = "solo@y.test"
            })
        };
    }

    private static object Price()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_gulf_p_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            SeedUsers(db);
            var emptyName = PhpPlanQ1Gulf.EpcScpPriceConfigSave(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "  " });
            var save = PhpPlanQ1Gulf.EpcScpPriceConfigSave(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = "  Gulf List  ",
                ["scope"] = "nope",
                ["site_key"] = "Acme-Parts!",
                ["client_type"] = "nope",
                ["client_ref"] = " ref1 ",
                ["markup_percent"] = "12.5",
                ["markup_fixed"] = "1.25",
                ["currency"] = "aed",
                ["priority"] = 0,
                ["active"] = 0,
                ["notes"] = " n "
            });
            var upd = PhpPlanQ1Gulf.EpcScpPriceConfigSave(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = "Gulf List",
                ["scope"] = "tenant",
                ["site_key"] = "acme_parts",
                ["client_type"] = "api",
                ["markup_percent"] = 8,
                ["currency"] = "USD",
                ["priority"] = 5,
                ["active"] = 1
            }, ToInt(save["id"]));
            var second = PhpPlanQ1Gulf.EpcScpPriceConfigSave(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = "Zeta",
                ["scope"] = "platform",
                ["client_type"] = "catalog",
                ["markup_percent"] = 1,
                ["active"] = 1,
                ["priority"] = 50
            });
            var list = PhpPlanQ1Gulf.EpcScpPriceConfigsList(db).Select(ProjPrice).ToList();
            var del = PhpPlanQ1Gulf.EpcScpPriceConfigDelete(db, ToInt(second["id"])) ? 1 : 0;
            var after = PhpPlanQ1Gulf.EpcScpPriceConfigsList(db).Count;
            var defaults = PhpPlanQ1Gulf.EpcScpCommSettingsGet(db);
            PhpPlanQ1Gulf.EpcScpCommSettingsSave(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["notify_from_name"] = "Gulf Ops",
                ["notify_tenant_onboard"] = 0,
                ["notify_daily_digest"] = "1",
                ["digest_hour_utc"] = "9",
                ["nope"] = "x"
            });
            var saved = PhpPlanQ1Gulf.EpcScpCommSettingsGet(db);
            return new object?[]
            {
                emptyName,
                Truthy(save.GetValueOrDefault("ok")) ? 1 : 0,
                ToInt(save["id"]),
                upd,
                list,
                del,
                after,
                defaults["notify_from_name"],
                defaults["notify_daily_digest"],
                saved["notify_from_name"],
                saved["notify_tenant_onboard"],
                saved["notify_daily_digest"],
                saved["digest_hour_utc"],
                saved.ContainsKey("nope") ? 1 : 0
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Info()
    {
        using var admin = OpenAdmin();
        var schema = "ecomae_cpw_gulf_i_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var db = OpenDb(schema);
            SeedUsers(db);
            var bad = PhpPlanQ1Gulf.EpcScpInfoBlockSave(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["block_key"] = "", ["title"] = "X" });
            var save = PhpPlanQ1Gulf.EpcScpInfoBlockSave(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["block_key"] = "Hero-Banner!",
                ["title"] = "  Hello  ",
                ["scope"] = "tenant",
                ["site_key"] = "Acme-Parts!",
                ["placement"] = "nope",
                ["content_html"] = "<b>Hi</b>",
                ["locale"] = "",
                ["active"] = 1,
                ["sort_order"] = 3
            });
            var dup = PhpPlanQ1Gulf.EpcScpInfoBlockSave(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["block_key"] = "hero-banner",
                ["title"] = "Again",
                ["scope"] = "tenant",
                ["site_key"] = "acmeparts",
                ["locale"] = "en"
            });
            var footer = PhpPlanQ1Gulf.EpcScpInfoBlockSave(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["block_key"] = "foot",
                ["title"] = "Footer",
                ["placement"] = "footer",
                ["sort_order"] = 1
            });
            var all = PhpPlanQ1Gulf.EpcScpInfoBlocksList(db).Select(ProjBlock).ToList();
            var home = PhpPlanQ1Gulf.EpcScpInfoBlocksList(db, "homepage").Select(ProjBlock).ToList();
            var del = PhpPlanQ1Gulf.EpcScpInfoBlockDelete(db, ToInt(footer["id"])) ? 1 : 0;
            var tBad = PhpPlanQ1Gulf.EpcScpTaskSave(db, new Dictionary<string, object?>(StringComparer.Ordinal) { ["title"] = " " });
            var t1 = PhpPlanQ1Gulf.EpcScpTaskSave(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "  Fix DNS  ",
                ["description"] = "now",
                ["assigned_to"] = -3,
                ["assigned_email"] = "Ops@Ecomae.COM",
                ["site_key"] = "Acme-Parts!",
                ["category"] = "nope",
                ["status"] = "nope",
                ["priority"] = "urgent",
                ["due_at"] = -5
            }, 0, 9);
            var t2 = PhpPlanQ1Gulf.EpcScpTaskSave(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Bill",
                ["category"] = "billing",
                ["status"] = "done",
                ["priority"] = "low"
            });
            var open = PhpPlanQ1Gulf.EpcScpTasksList(db, "open").Select(ProjTask).ToList();
            var allTasks = PhpPlanQ1Gulf.EpcScpTasksList(db);
            var upd = PhpPlanQ1Gulf.EpcScpTaskSave(db, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["title"] = "Fix DNS",
                ["status"] = "in_progress",
                ["priority"] = "high",
                ["assigned_email"] = "ops@ecomae.com",
                ["site_key"] = "acme_parts",
                ["category"] = "support"
            }, ToInt(t1["id"]));
            var tdel = PhpPlanQ1Gulf.EpcScpTaskDelete(db, ToInt(t2["id"])) ? 1 : 0;
            return new object?[]
            {
                bad,
                save,
                dup,
                all,
                home,
                del,
                tBad,
                Truthy(t1.GetValueOrDefault("ok")) ? 1 : 0,
                open,
                allTasks.Count > 0 ? Convert.ToString(allTasks[0].GetValueOrDefault("status")) ?? "" : "",
                upd,
                tdel,
                PhpPlanQ1Gulf.EpcScpTasksList(db).Count
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static object Board()
    {
        using var admin = OpenAdmin();
        var platSchema = "ecomae_cpw_gulf_b_" + Guid.NewGuid().ToString("N")[..8];
        var acmeSchema = "ecomae_cpw_gulf_a_" + Guid.NewGuid().ToString("N")[..8];
        var betaSchema = "ecomae_cpw_gulf_z_" + Guid.NewGuid().ToString("N")[..8];
        Exec(admin, $"CREATE DATABASE `{platSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        Exec(admin, $"CREATE DATABASE `{acmeSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        Exec(admin, $"CREATE DATABASE `{betaSchema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        try
        {
            using var plat = OpenDb(platSchema);
            using var acme = OpenDb(acmeSchema);
            using var beta = OpenDb(betaSchema);
            SeedUsers(plat);
            SeedUsers(acme);
            SeedUsers(beta);
            SeedUser(plat, 11, "zana@ecomae.test", "1", 100, new Dictionary<string, string> { ["name"] = "Zana", ["surname"] = "Ops" });
            SeedUser(plat, 10, "ada@ecomae.test", "2", 90, new Dictionary<string, string> { ["name"] = "Ada" });
            SeedUser(acme, 21, "buyer@acme.test", "971", 50, new Dictionary<string, string> { ["name"] = "Buyer", ["company"] = "Acme" });
            SeedUser(beta, 31, "other@beta.test", "3", 40, new Dictionary<string, string> { ["name"] = "Other" });
            PhpPlanQ1Gulf.Tenants.AddRange(TenantRows());
            PhpPlanQ1Gulf.TenantPdo = row =>
            {
                var key = Convert.ToString(row.GetValueOrDefault("site_key")) ?? "";
                return key == "acme_parts" ? acme : key == "beta" ? beta : null;
            };
            PhpPlanQ1Gulf.TenantUrls = _ => new Dictionary<string, object?>(StringComparer.Ordinal) { ["cp"] = "", ["client_erp"] = "" };
            var opts = PhpPlanQ1Gulf.EpcScpTenantOptions(plat);
            var users = PhpPlanQ1Gulf.EpcScpPlatformUsers(plat)
                .Select(u => new object[] { ToInt(u.GetValueOrDefault("user_id")), Convert.ToString(u.GetValueOrDefault("email")) ?? "", Convert.ToString(u.GetValueOrDefault("fname")) ?? "" })
                .ToList();
            var chunk = PhpPlanQ1Gulf.EpcScpCustomersFromPdo(acme, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = "acme_parts",
                ["label"] = "Acme Parts (acme_parts)",
                ["hostname"] = "www.acme.test",
                ["urls"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["cp"] = "https://www.acme.test/cp",
                    ["client_erp"] = ""
                }
            }, "", 10);
            var boardAll = ProjBoard(PhpPlanQ1Gulf.EpcScpCustomerBoardSearch(plat, "", "", 1, 10));
            var boardAcme = ProjBoard(PhpPlanQ1Gulf.EpcScpCustomerBoardSearch(plat, "", "acme_parts", 1, 10));
            var boardPlat = ProjBoard(PhpPlanQ1Gulf.EpcScpCustomerBoardSearch(plat, "ada", "platform", 1, 10));
            PhpPlanQ1Gulf.IsSuperHost = () => false;
            var g1 = PhpPlanQ1Gulf.EpcScpGuardSuperAdmin() ? 1 : 0;
            var deny = PhpPlanQ1Gulf.LastOutput;
            PhpPlanQ1Gulf.IsSuperHost = () => true;
            PhpPlanQ1Gulf.IsAdmin = () => false;
            var g2 = PhpPlanQ1Gulf.EpcScpGuardSuperAdmin() ? 1 : 0;
            var login = PhpPlanQ1Gulf.LastOutput;
            PhpPlanQ1Gulf.IsAdmin = () => true;
            var g3 = PhpPlanQ1Gulf.EpcScpGuardSuperAdmin() ? 1 : 0;
            var ok = PhpPlanQ1Gulf.LastOutput;
            var hero = PhpPlanQ1Gulf.EpcScpRenderHero("Badge", "O'Reilly", "Sub & more",
            [
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["url"] = "/cp/x?a=1&b=2",
                    ["label"] = "View",
                    ["icon"] = "fa-eye",
                    ["primary"] = 1
                }
            ]);
            var none = PhpPlanQ1Gulf.EpcScpRenderWorkspaceIntro("nope");
            var intro = PhpPlanQ1Gulf.EpcScpRenderWorkspaceIntro("customer_board");
            var empty = PhpPlanQ1Gulf.EpcScpRenderEmptyState("Empty", "Nothing", []);
            return new object?[]
            {
                opts, users, chunk, boardAll, boardAcme, boardPlat,
                g1, deny, g2, login, g3, ok, hero, none, intro, empty
            };
        }
        finally
        {
            Exec(admin, $"DROP DATABASE IF EXISTS `{platSchema}`");
            Exec(admin, $"DROP DATABASE IF EXISTS `{acmeSchema}`");
            Exec(admin, $"DROP DATABASE IF EXISTS `{betaSchema}`");
        }
    }

    private static List<Dictionary<string, object?>> TenantRows()
        =>
        [
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = "acme_parts",
                ["trade_name"] = "Acme Parts",
                ["hostname"] = "www.acme.test",
                ["in_registry"] = 1,
                ["access_blocked"] = 0,
                ["urls"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["cp"] = "https://www.acme.test/cp",
                    ["client_erp"] = "https://www.acme.test/cp"
                }
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = "beta",
                ["trade_name"] = "",
                ["hostname"] = "www.beta.test",
                ["in_registry"] = 1,
                ["access_blocked"] = 1
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = "",
                ["trade_name"] = "Ghost",
                ["in_registry"] = 1
            },
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = "skip",
                ["trade_name"] = "Skip",
                ["in_registry"] = 0
            }
        ];

    private static object?[] ProjBoard(Dictionary<string, object?> pack)
    {
        var rows = ((List<Dictionary<string, object?>>)pack["rows"]!).Select(r =>
        {
            var links = (Dictionary<string, object?>)r["links"]!;
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["source"] = r["source"],
                ["email"] = r["email"],
                ["name"] = r["name"],
                ["crm"] = links.GetValueOrDefault("crm"),
                ["erp"] = links.GetValueOrDefault("erp")
            };
        }).ToList();
        return [rows, pack["total"], pack["stats"]];
    }

    private static Dictionary<string, object?> ProjPrice(Dictionary<string, object?> r)
        => new(StringComparer.Ordinal)
        {
            ["name"] = Convert.ToString(r["name"]) ?? "",
            ["scope"] = Convert.ToString(r["scope"]) ?? "",
            ["site_key"] = Convert.ToString(r["site_key"]) ?? "",
            ["client_type"] = Convert.ToString(r["client_type"]) ?? "",
            ["markup_percent"] = Convert.ToString(r["markup_percent"]) ?? "",
            ["currency"] = Convert.ToString(r["currency"]) ?? "",
            ["priority"] = ToInt(r["priority"]),
            ["active"] = ToInt(r["active"])
        };

    private static Dictionary<string, object?> ProjBlock(Dictionary<string, object?> r)
        => new(StringComparer.Ordinal)
        {
            ["block_key"] = Convert.ToString(r["block_key"]) ?? "",
            ["title"] = Convert.ToString(r["title"]) ?? "",
            ["scope"] = Convert.ToString(r["scope"]) ?? "",
            ["site_key"] = Convert.ToString(r["site_key"]) ?? "",
            ["placement"] = Convert.ToString(r["placement"]) ?? "",
            ["locale"] = Convert.ToString(r["locale"]) ?? "",
            ["active"] = ToInt(r["active"]),
            ["sort_order"] = ToInt(r["sort_order"])
        };

    private static Dictionary<string, object?> ProjTask(Dictionary<string, object?> r)
        => new(StringComparer.Ordinal)
        {
            ["title"] = Convert.ToString(r["title"]) ?? "",
            ["assigned_email"] = Convert.ToString(r["assigned_email"]) ?? "",
            ["site_key"] = Convert.ToString(r["site_key"]) ?? "",
            ["category"] = Convert.ToString(r["category"]) ?? "",
            ["status"] = Convert.ToString(r["status"]) ?? "",
            ["priority"] = Convert.ToString(r["priority"]) ?? ""
        };

    private static void SeedUsers(MySqlConnection db)
    {
        Exec(db, """
            CREATE TABLE `users` (
                `user_id` INT NOT NULL PRIMARY KEY,
                `email` VARCHAR(120) NOT NULL DEFAULT '',
                `phone` VARCHAR(64) NOT NULL DEFAULT '',
                `time_reg` INT NOT NULL DEFAULT 0
            )
            """);
        Exec(db, """
            CREATE TABLE `users_profiles` (
                `user_id` INT NOT NULL,
                `data_key` VARCHAR(64) NOT NULL,
                `data_value` VARCHAR(255) NOT NULL DEFAULT '',
                PRIMARY KEY (`user_id`, `data_key`)
            )
            """);
    }

    private static void SeedUser(MySqlConnection db, int id, string email, string phone, int reg, Dictionary<string, string> profile)
    {
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO `users` (`user_id`,`email`,`phone`,`time_reg`) VALUES (@i,@e,@p,@t)";
            cmd.Parameters.AddWithValue("@i", id);
            cmd.Parameters.AddWithValue("@e", email);
            cmd.Parameters.AddWithValue("@p", phone);
            cmd.Parameters.AddWithValue("@t", reg);
            cmd.ExecuteNonQuery();
        }

        foreach (var pair in profile)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT INTO `users_profiles` (`user_id`,`data_key`,`data_value`) VALUES (@i,@k,@v)";
            cmd.Parameters.AddWithValue("@i", id);
            cmd.Parameters.AddWithValue("@k", pair.Key);
            cmd.Parameters.AddWithValue("@v", pair.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private static bool Truthy(object? value)
        => value is true or 1 or 1L or 1.0 or "1";

    private static int ToInt(object? value)
        => value is null ? 0 : Convert.ToInt32(value);

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
