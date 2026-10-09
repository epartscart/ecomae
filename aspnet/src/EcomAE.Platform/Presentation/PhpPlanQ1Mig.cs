using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-mig helpers. PHP identifiers kept for the inventory:
/// <c>EPC_MIGRATIONS_VERSION</c>, <c>epc_migrations_registry</c>,
/// <c>epc_migrations_ensure_table</c>, <c>epc_migrations_applied</c>,
/// <c>epc_migrations_pending</c>, <c>epc_migration_apply</c>,
/// <c>epc_migration_rollback</c>, <c>epc_migrations_run_all</c>,
/// <c>epc_migrations_status</c>, <c>epc_migrations_verify</c>,
/// <c>epc_migrations_dry_run</c>.
/// </summary>
public static class PhpPlanQ1Mig
{
    public const string DbMigrationsPath = "content/general_pages/epc_db_migrations.php";
    public const string EpcMigrationsVersion = "1.0.0";

    private static readonly Lazy<JsonElement> Dump = new(() => JsonDocument.Parse(PhpPlanQ1MigJson.RegistryJson).RootElement.Clone());

    public sealed class AppliedRow
    {
        public string Version { get; set; } = "";
        public string Name { get; set; } = "";
        public string AppliedAt { get; set; } = "";
        public string Checksum { get; set; } = "";
    }

    public sealed class MigStore
    {
        public bool SchemaReady { get; set; }
        public List<AppliedRow> Applied { get; } = new();
        public bool FailNext { get; set; }
        public string FailMessage { get; set; } = "failed";
        public Func<string>? Clock { get; set; }
        public string Now() => Clock?.Invoke() ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static JsonElement EpcMigrationsRegistry() => Dump.Value.GetProperty("registry").Clone();

    public static void EpcMigrationsEnsureTable(MigStore store) => store.SchemaReady = true;

    public static List<Dictionary<string, object?>> EpcMigrationsApplied(MigStore store)
    {
        EpcMigrationsEnsureTable(store);
        return store.Applied
            .OrderBy(x => x.Version, StringComparer.Ordinal)
            .Select(x => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["version"] = x.Version,
                ["name"] = x.Name,
                ["applied_at"] = x.AppliedAt,
                ["checksum"] = x.Checksum
            })
            .ToList();
    }

    public static List<Dictionary<string, object?>> EpcMigrationsPending(MigStore store)
    {
        var applied = EpcMigrationsApplied(store).Select(x => (string)x["version"]!).ToHashSet(StringComparer.Ordinal);
        var pending = new List<Dictionary<string, object?>>();
        foreach (var m in EpcMigrationsRegistry().EnumerateArray())
        {
            var version = m.GetProperty("version").GetString() ?? "";
            if (!applied.Contains(version))
            {
                pending.Add(JsonToDict(m));
            }
        }

        return pending;
    }

    public static Dictionary<string, object?> EpcMigrationApply(MigStore store, Dictionary<string, object?> migration)
    {
        EpcMigrationsEnsureTable(store);
        var version = Convert.ToString(migration["version"]) ?? "";
        var name = Convert.ToString(migration["name"]) ?? "";
        if (store.FailNext)
        {
            store.FailNext = false;
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["version"] = version,
                ["error"] = store.FailMessage
            };
        }

        var up = Convert.ToString(migration["up"]) ?? "";
        store.Applied.Add(new AppliedRow
        {
            Version = version,
            Name = name,
            AppliedAt = store.Now(),
            Checksum = Md5Hex(up)
        });
        // MariaDB implicit-commits DDL (`CREATE TABLE`) so PDO commit() then throws.
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["version"] = version,
            ["error"] = "There is no active transaction"
        };
    }

    public static Dictionary<string, object?> EpcMigrationRollback(MigStore store, string version)
    {
        Dictionary<string, object?>? migration = null;
        foreach (var m in EpcMigrationsRegistry().EnumerateArray())
        {
            if ((m.GetProperty("version").GetString() ?? "") == version)
            {
                migration = JsonToDict(m);
                break;
            }
        }

        if (migration is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["error"] = "Migration not found: " + version
            };
        }

        if (store.FailNext)
        {
            store.FailNext = false;
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["version"] = version,
                ["error"] = store.FailMessage
            };
        }

        store.Applied.RemoveAll(x => x.Version == version);
        // `DROP TABLE` also implicit-commits on this MariaDB, so rollback returns ok=false.
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = false,
            ["version"] = version,
            ["error"] = "There is no active transaction"
        };
    }

    public static Dictionary<string, object?> EpcMigrationsRunAll(MigStore store)
    {
        var pending = EpcMigrationsPending(store);
        if (pending.Count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["applied"] = 0,
                ["message"] = "No pending migrations"
            };
        }

        var results = new List<Dictionary<string, object?>>();
        var applied = 0;
        foreach (var m in pending)
        {
            var result = EpcMigrationApply(store, m);
            results.Add(result);
            if (result["ok"] is true)
            {
                applied++;
            }
            else
            {
                break;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = applied == pending.Count,
            ["applied"] = applied,
            ["total"] = pending.Count,
            ["results"] = results
        };
    }

    public static Dictionary<string, object?> EpcMigrationsStatus(MigStore store)
    {
        var applied = EpcMigrationsApplied(store);
        var pending = EpcMigrationsPending(store);
        var registry = EpcMigrationsRegistry();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total"] = registry.GetArrayLength(),
            ["applied"] = applied.Count,
            ["pending"] = pending.Count,
            ["current_version"] = applied.Count > 0 ? applied[^1]["version"] : "000",
            ["latest_version"] = registry.GetArrayLength() > 0
                ? registry[registry.GetArrayLength() - 1].GetProperty("version").GetString()
                : "000",
            ["applied_list"] = applied,
            ["pending_list"] = pending.Select(m => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["version"] = m["version"],
                ["name"] = m["name"],
                ["description"] = m["description"]
            }).ToList()
        };
    }

    public static Dictionary<string, object?> EpcMigrationsVerify(MigStore store)
    {
        var applied = EpcMigrationsApplied(store);
        var registry = EpcMigrationsRegistry();
        var issues = new List<Dictionary<string, object?>>();
        foreach (var a in applied)
        {
            var found = false;
            foreach (var m in registry.EnumerateArray())
            {
                if ((m.GetProperty("version").GetString() ?? "") != (string)a["version"]!)
                {
                    continue;
                }

                found = true;
                var expected = Md5Hex(m.GetProperty("up").GetString() ?? "");
                var checksum = Convert.ToString(a["checksum"]) ?? "";
                if (checksum != "" && checksum != expected)
                {
                    issues.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["version"] = a["version"],
                        ["issue"] = "checksum_mismatch",
                        ["detail"] = "Migration SQL has changed since it was applied"
                    });
                }

                break;
            }

            if (!found)
            {
                issues.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["version"] = a["version"],
                    ["issue"] = "orphaned",
                    ["detail"] = "Applied migration not found in registry"
                });
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = issues.Count == 0,
            ["issues"] = issues
        };
    }

    public static Dictionary<string, object?> EpcMigrationsDryRun(MigStore store)
    {
        var pending = EpcMigrationsPending(store);
        var statements = pending.Select(m => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["version"] = m["version"],
            ["name"] = m["name"],
            ["description"] = m["description"],
            ["sql"] = m["up"]
        }).ToList();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["count"] = statements.Count,
            ["statements"] = statements
        };
    }

    private static Dictionary<string, object?> JsonToDict(JsonElement el)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in el.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.TryGetInt64(out var n) ? n : prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.GetRawText()
            };
        }

        return dict;
    }

    private static string Md5Hex(string value)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
