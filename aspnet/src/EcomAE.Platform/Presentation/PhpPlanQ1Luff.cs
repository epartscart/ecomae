using System.Text.Json;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-luff auto-parts demo bootstrap. PHP identifiers kept for the inventory:
/// <c>epc_demo_autoparts_bootstrap_preset_path</c>, <c>epc_demo_autoparts_bootstrap_preset</c>,
/// <c>epc_demo_autoparts_bootstrap_clone_tables</c>, <c>epc_demo_autoparts_bootstrap_docpart_pdo</c>,
/// <c>epc_demo_autoparts_bootstrap_apply</c>, <c>epc_demo_autoparts_bootstrap_verify_db</c>.
/// GET never mints a session cookie. Leftover clone parents stay injected.
/// </summary>
public static class PhpPlanQ1Luff
{
    public const string DemoAutopartsBootstrapPath = "epc_demo_autoparts_bootstrap.php";

    private static readonly string[] DefaultTables =
    [
        "lang_languages", "lang_text_strings", "lang_text_strings_translation",
        "groups", "users_groups_bind",
        "shop_offices", "shop_geo", "shop_offices_geo_map",
        "menu", "shop_catalogue_categories",
        "templates", "content", "modules", "plugins",
        "shop_storages", "shop_storages_data"
    ];

    public static Func<string>? PresetPath { get; set; }
    public static Func<MySqlConnection?>? SourcePdo { get; set; }
    public static Func<MySqlConnection, MySqlConnection, IList<string>, Dictionary<string, object?>>? CloneTables { get; set; }

    public static void Reset()
    {
        PresetPath = null;
        SourcePdo = () => null;
        CloneTables = (_, _, tables) => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["tables"] = tables.ToList(),
            ["errors"] = new List<string>()
        };
    }

    public static string EpcDemoAutopartsBootstrapPresetPath()
        => PresetPath != null ? PresetPath() : "";

    public static Dictionary<string, object?> EpcDemoAutopartsBootstrapPreset()
    {
        var path = EpcDemoAutopartsBootstrapPresetPath();
        if (path == "" || !File.Exists(path))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var raw = File.ReadAllText(path);
        if (raw == "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? JsonObject(doc.RootElement)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }
    }

    public static List<string> EpcDemoAutopartsBootstrapCloneTables()
    {
        var preset = EpcDemoAutopartsBootstrapPreset();
        if (preset.TryGetValue("docpart_clone_tables", out var raw) && raw is List<object?> list && list.Count > 0)
        {
            return list.Select(v => Convert.ToString(v) ?? "").Where(v => v != "").ToList();
        }

        if (preset.TryGetValue("docpart_clone_tables", out var boxed) && boxed is JsonElement el && el.ValueKind == JsonValueKind.Array)
        {
            var fromJson = el.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : Convert.ToString(x) ?? "").Where(v => v != "").ToList();
            if (fromJson.Count > 0)
            {
                return fromJson;
            }
        }

        if (preset.TryGetValue("docpart_clone_tables", out var objs) && objs is List<string> names && names.Count > 0)
        {
            return names.Where(v => v != "").ToList();
        }

        return DefaultTables.ToList();
    }

    public static MySqlConnection? EpcDemoAutopartsBootstrapDocpartPdo()
    {
        try
        {
            return SourcePdo != null ? SourcePdo() : null;
        }
        catch
        {
            return null;
        }
    }

    public static Dictionary<string, object?> EpcDemoAutopartsBootstrapApply(MySqlConnection tenantPdo, bool force = false)
    {
        var outRow = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["force"] = force,
            ["preset"] = Path.GetFileName(EpcDemoAutopartsBootstrapPresetPath()),
            ["cloned"] = new Dictionary<string, object?>(StringComparer.Ordinal),
            ["home_modules"] = false,
            ["root_categories"] = 0,
            ["geo_nodes"] = 0,
            ["offices"] = 0,
            ["verify"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        };

        var docPdo = EpcDemoAutopartsBootstrapDocpartPdo();
        if (docPdo is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = "Storefront database PDO unavailable"
            };
        }

        var tables = EpcDemoAutopartsBootstrapCloneTables();
        Dictionary<string, object?> clone;
        if (force)
        {
            clone = CloneTables != null
                ? CloneTables(docPdo, tenantPdo, tables)
                : EmptyClone();
        }
        else
        {
            clone = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["tables"] = new List<string>(),
                ["errors"] = new List<string>()
            };
            foreach (var tbl in tables)
            {
                var tblEsc = tbl.Replace("`", "", StringComparison.Ordinal);
                int cnt;
                try
                {
                    cnt = ScalarInt(tenantPdo, "SELECT COUNT(*) FROM `" + tblEsc + "`");
                }
                catch
                {
                    cnt = 0;
                }

                if (cnt == 0)
                {
                    var part = CloneTables != null
                        ? CloneTables(docPdo, tenantPdo, new List<string> { tbl })
                        : EmptyClone();
                    var merged = (List<string>)clone["tables"]!;
                    merged.AddRange(AsStringList(part, "tables"));
                    var errors = (List<string>)clone["errors"]!;
                    errors.AddRange(AsStringList(part, "errors"));
                }
            }
        }

        outRow["cloned"] = clone;
        if (AsStringList(clone, "errors").Count > 0)
        {
            outRow["ok"] = false;
        }

        try
        {
            var srcHome = ScalarString(docPdo,
                "SELECT `modules_array` FROM `content` WHERE `main_flag` = 1 AND `published_flag` = 1 AND `is_frontend` = 1 LIMIT 1");
            if (!Empty(srcHome))
            {
                using var cmd = tenantPdo.CreateCommand();
                cmd.CommandText = "UPDATE `content` SET `modules_array` = @m WHERE `main_flag` = 1 AND `published_flag` = 1 AND `is_frontend` = 1";
                cmd.Parameters.AddWithValue("@m", srcHome);
                outRow["home_modules"] = cmd.ExecuteNonQuery() > 0;
            }
        }
        catch (Exception ex)
        {
            outRow["home_modules_error"] = ex.Message;
        }

        try
        {
            outRow["root_categories"] = ScalarInt(tenantPdo,
                "SELECT COUNT(`id`) FROM `shop_catalogue_categories` WHERE `published_flag` = 1 AND `parent` = 0");
        }
        catch
        {
            outRow["root_categories"] = 0;
        }

        try
        {
            outRow["geo_nodes"] = ScalarInt(tenantPdo, "SELECT COUNT(*) FROM `shop_geo`");
        }
        catch
        {
            outRow["geo_nodes"] = 0;
        }

        try
        {
            outRow["offices"] = ScalarInt(tenantPdo, "SELECT COUNT(*) FROM `shop_offices`");
        }
        catch
        {
            outRow["offices"] = 0;
        }

        var preset = EpcDemoAutopartsBootstrapPreset();
        var markers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["location"] = "Dubai",
            ["hours"] = "Mon-Fri from 9:00"
        };
        if (preset.TryGetValue("header_verify", out var hv) && hv is Dictionary<string, object?> hvMap)
        {
            if (hvMap.TryGetValue("location", out var loc) && loc != null)
            {
                markers["location"] = Convert.ToString(loc) ?? "Dubai";
            }

            if (hvMap.TryGetValue("hours", out var hours) && hours != null)
            {
                markers["hours"] = Convert.ToString(hours) ?? "Mon-Fri from 9:00";
            }
        }

        var verify = EpcDemoAutopartsBootstrapVerifyDb(tenantPdo, markers);
        outRow["verify"] = verify;
        if (Empty(verify.TryGetValue("ok", out var vok) ? vok : null))
        {
            outRow["ok"] = false;
        }

        outRow["message"] = (outRow["ok"] is true)
            ? "Auto-parts header bootstrap complete"
            : "Auto-parts header bootstrap incomplete";
        return outRow;
    }

    public static Dictionary<string, object?> EpcDemoAutopartsBootstrapVerifyDb(
        MySqlConnection tenantPdo, Dictionary<string, string> markers)
    {
        var locationNeedle = markers.TryGetValue("location", out var loc) && loc != "" ? loc : "Dubai";
        var hoursNeedle = markers.TryGetValue("hours", out var hours) && hours != "" ? hours : "Mon-Fri from 9:00";
        var verify = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["location"] = false,
            ["hours"] = false,
            ["geo_dubai"] = false
        };

        try
        {
            var dubaiGeo = ScalarInt(tenantPdo, "SELECT COUNT(*) FROM `shop_geo` WHERE `id` = 3");
            verify["geo_dubai"] = dubaiGeo > 0;
            var geoTotal = ScalarInt(tenantPdo, "SELECT COUNT(*) FROM `shop_geo`");
            verify["location"] = dubaiGeo > 0 || geoTotal > 1;
            if (verify["location"] is false)
            {
                using var cmd = tenantPdo.CreateCommand();
                cmd.CommandText = "SELECT `city` FROM `shop_offices` ORDER BY `id` ASC LIMIT 3";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var city = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    if (city.Contains(locationNeedle, StringComparison.OrdinalIgnoreCase))
                    {
                        verify["location"] = true;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            verify["geo_error"] = ex.Message;
        }

        try
        {
            var like = "%" + hoursNeedle.Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
            using var cmd = tenantPdo.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `value` LIKE @like";
            cmd.Parameters.AddWithValue("@like", like);
            var hoursCnt = Convert.ToInt32(cmd.ExecuteScalar());
            verify["hours"] = hoursCnt > 0;
            if (verify["hours"] is false)
            {
                using var officeCmd = tenantPdo.CreateCommand();
                officeCmd.CommandText = "SELECT `timetable` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1";
                var timetable = officeCmd.ExecuteScalar();
                var text = timetable == null || timetable is DBNull ? "" : Convert.ToString(timetable) ?? "";
                verify["hours"] = text.Trim() != "";
            }
        }
        catch (Exception ex)
        {
            verify["office_error"] = ex.Message;
        }

        if (verify["location"] is false || verify["hours"] is false)
        {
            verify["ok"] = false;
        }

        return verify;
    }

    private static Dictionary<string, object?> EmptyClone()
        => new(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["tables"] = new List<string>(),
            ["errors"] = new List<string>()
        };

    private static List<string> AsStringList(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var raw) || raw is null)
        {
            return [];
        }

        if (raw is List<string> list)
        {
            return list;
        }

        if (raw is IEnumerable<object?> objs)
        {
            return objs.Select(v => Convert.ToString(v) ?? "").ToList();
        }

        return [];
    }

    private static int ScalarInt(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        var raw = cmd.ExecuteScalar();
        return raw == null || raw is DBNull ? 0 : Convert.ToInt32(raw);
    }

    private static string ScalarString(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        var raw = cmd.ExecuteScalar();
        return raw == null || raw is DBNull ? "" : Convert.ToString(raw) ?? "";
    }

    private static bool Empty(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is bool flag)
        {
            return !flag;
        }

        var text = Convert.ToString(value) ?? "";
        return text == "" || text == "0";
    }

    private static Dictionary<string, object?> JsonObject(JsonElement el)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in el.EnumerateObject())
        {
            row[prop.Name] = JsonValue(prop.Value);
        }

        return row;
    }

    private static object? JsonValue(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.Object => JsonObject(el),
            JsonValueKind.Array => el.EnumerateArray().Select(JsonValue).ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var n) ? n : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}
