using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-vang POS CP install. PHP identifiers kept for the inventory:
/// <c>epc_pos_cp_lang</c>, <c>epc_pos_cp_register_content</c>, <c>epc_pos_cp_install</c>,
/// <c>epc_pos_cp_register_super_route</c>, <c>epc_pos_setup_connect</c>.
/// GET never mints a session cookie. Leftover POS helper parents stay injected.
/// </summary>
public static class PhpPlanQ1Vang
{
    public const string PosCpInstallPath = "content/shop/pos/epc_pos_cp_install.php";

    public static Func<long>? UnixNow { get; set; }
    public static Action<MySqlConnection>? EnsureSchema { get; set; }
    public static Func<MySqlConnection, int>? EnsureWalkin { get; set; }
    public static Func<MySqlConnection, Dictionary<string, object?>>? PortalMenu { get; set; }
    public static Func<MySqlConnection, Dictionary<string, object?>>? PosMenu { get; set; }
    public static Func<VangConfig, string, string, string, MySqlConnection?>? OpenPdo { get; set; }

    public sealed class VangConfig
    {
        public string Host { get; set; } = "";
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public static void Reset()
    {
        UnixNow = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        EnsureSchema = _ => { };
        EnsureWalkin = _ => 0;
        PortalMenu = _ => new Dictionary<string, object?>(StringComparer.Ordinal);
        PosMenu = _ => new Dictionary<string, object?>(StringComparer.Ordinal);
        OpenPdo = (_, _, _, _) => null;
    }

    public static void EpcPosCpLang(MySqlConnection pdo, string key, string en, string ru)
    {
        using (var ins = pdo.CreateCommand())
        {
            ins.CommandText = "INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (@k, @d, NULL, 0, 1, 1)";
            ins.Parameters.AddWithValue("@k", key);
            ins.Parameters.AddWithValue("@d", en);
            ins.ExecuteNonQuery();
        }

        UpsertTranslation(pdo, key, "en", en);
        UpsertTranslation(pdo, key, "ru", ru);
    }

    public static int EpcPosCpRegisterContent(
        MySqlConnection pdo, string parentUrl, string url, string alias, string valueKey, string phpPath, string title, int order = 88)
    {
        using var parent = pdo.CreateCommand();
        parent.CommandText = "SELECT `id`, `level` FROM `content` WHERE `url` = @u AND `is_frontend` = 0 LIMIT 1";
        parent.Parameters.AddWithValue("@u", parentUrl);
        using var parentReader = parent.ExecuteReader();
        if (!parentReader.Read())
        {
            throw new InvalidOperationException("Parent not found: " + parentUrl);
        }

        var parentId = Convert.ToInt32(parentReader["id"]);
        var level = Convert.ToInt32(parentReader["level"]) + 1;
        parentReader.Close();
        var now = UnixNow != null ? UnixNow() : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int contentId;
        using (var existing = pdo.CreateCommand())
        {
            existing.CommandText = "SELECT `id` FROM `content` WHERE `url` = @u AND `is_frontend` = 0 LIMIT 1";
            existing.Parameters.AddWithValue("@u", url);
            contentId = Convert.ToInt32(existing.ExecuteScalar() ?? 0);
        }

        if (contentId > 0)
        {
            using var upd = pdo.CreateCommand();
            upd.CommandText = "UPDATE `content` SET `published_flag` = 1, `content_type` = 'php', `content` = @c, `title_tag` = @t, `parent` = @p, `level` = @l, `alias` = @a, `value` = @v, `time_edited` = @e WHERE `id` = @id";
            upd.Parameters.AddWithValue("@c", phpPath);
            upd.Parameters.AddWithValue("@t", title);
            upd.Parameters.AddWithValue("@p", parentId);
            upd.Parameters.AddWithValue("@l", level);
            upd.Parameters.AddWithValue("@a", alias);
            upd.Parameters.AddWithValue("@v", valueKey);
            upd.Parameters.AddWithValue("@e", now);
            upd.Parameters.AddWithValue("@id", contentId);
            upd.ExecuteNonQuery();
        }
        else
        {
            using var ins = pdo.CreateCommand();
            ins.CommandText = """
                INSERT INTO `content`
                (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
                 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
                 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
                 VALUES (0, @u, @l, @a, @v, @p, @d, 0, 'php', @c, @t, '0', '0', '0', 0, '[]', '', '', 0, 1, 0, @now, @now, @o)
                """;
            ins.Parameters.AddWithValue("@u", url);
            ins.Parameters.AddWithValue("@l", level);
            ins.Parameters.AddWithValue("@a", alias);
            ins.Parameters.AddWithValue("@v", valueKey);
            ins.Parameters.AddWithValue("@p", parentId);
            ins.Parameters.AddWithValue("@d", title);
            ins.Parameters.AddWithValue("@c", phpPath);
            ins.Parameters.AddWithValue("@t", title);
            ins.Parameters.AddWithValue("@now", now);
            ins.Parameters.AddWithValue("@o", order);
            ins.ExecuteNonQuery();
            contentId = (int)ins.LastInsertedId;
        }

        using (var del = pdo.CreateCommand())
        {
            del.CommandText = "DELETE FROM `content_access` WHERE `content_id` = @id";
            del.Parameters.AddWithValue("@id", contentId);
            del.ExecuteNonQuery();
        }

        var root = 0;
        using (var st = pdo.CreateCommand())
        {
            st.CommandText = "SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1";
            root = Convert.ToInt32(st.ExecuteScalar() ?? 0);
        }

        var groups = new List<int> { root > 0 ? root : 1 };
        if (root > 0)
        {
            Collect(pdo, root, groups);
        }

        using var acc = pdo.CreateCommand();
        acc.CommandText = "INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (@c, @g)";
        foreach (var gid in groups.Distinct())
        {
            acc.Parameters.Clear();
            acc.Parameters.AddWithValue("@c", contentId);
            acc.Parameters.AddWithValue("@g", gid);
            acc.ExecuteNonQuery();
        }

        return contentId;
    }

    public static Dictionary<string, object?> EpcPosCpInstall(MySqlConnection pdo, string backendDir = "cp")
    {
        EpcPosCpLang(pdo, "epc_pos_terminal_cp", "POS Terminal", "Касса POS");
        EpcPosCpLang(pdo, "epc_cp_group_pos", "Point of Sale", "Касса");
        EpcPosCpLang(pdo, "epc_portal_pos_manage", "POS overview", "Обзор POS");
        EnsureSchema?.Invoke(pdo);
        var walkinId = EnsureWalkin != null ? EnsureWalkin(pdo) : 0;
        var hubId = EpcPosCpRegisterContent(pdo, "shop", "shop/pos", "pos_folder", "epc_cp_group_pos", "/<backend_dir>/content/shop/pos/epc_pos_hub_page.php", "Point of Sale", 86);
        var contentId = EpcPosCpRegisterContent(pdo, "shop/pos", "shop/pos/terminal", "pos_terminal", "epc_pos_terminal_cp", "/<backend_dir>/content/shop/pos/epc_pos_terminal_page.php", "POS Terminal", 87);
        var menu = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (PortalMenu != null)
        {
            menu["portal"] = PortalMenu(pdo);
        }

        if (PosMenu != null)
        {
            foreach (var kv in PosMenu(pdo))
            {
                menu[kv.Key] = kv.Value;
            }
        }

        var superContentId = 0;
        try
        {
            superContentId = EpcPosCpRegisterSuperRoute(pdo, backendDir);
        }
        catch
        {
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["hub_content_id"] = hubId,
            ["content_id"] = contentId,
            ["super_content_id"] = superContentId,
            ["menu"] = menu,
            ["walkin_user_id"] = walkinId
        };
    }

    public static int EpcPosCpRegisterSuperRoute(MySqlConnection pdo, string backendDir = "cp")
    {
        backendDir = backendDir.Trim('/');
        const string contentUrl = "control/portal/epc_pos_tenant_manage";
        var phpPath = "/<backend_dir>/content/control/portal/" + "epc_pos_tenant" + "_manage.php";
        var now = UnixNow != null ? UnixNow() : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Dictionary<string, object?>? parentRow = FetchParent(pdo, "control/config") ?? FetchParent(pdo, "control");
        if (parentRow is null)
        {
            return 0;
        }

        var parentId = Convert.ToInt32(parentRow["id"]);
        var level = Convert.ToInt32(parentRow["level"]) + 1;
        int contentId;
        using (var existing = pdo.CreateCommand())
        {
            existing.CommandText = "SELECT `id` FROM `content` WHERE `url` = @u AND `is_frontend` = 0 LIMIT 1";
            existing.Parameters.AddWithValue("@u", contentUrl);
            contentId = Convert.ToInt32(existing.ExecuteScalar() ?? 0);
        }

        if (contentId > 0)
        {
            using var upd = pdo.CreateCommand();
            upd.CommandText = "UPDATE `content` SET `published_flag` = 1, `content_type` = 'php', `content` = @c, `title_tag` = @t, `value` = @v, `parent` = @p, `level` = @l, `alias` = @a WHERE `id` = @id";
            upd.Parameters.AddWithValue("@c", phpPath);
            upd.Parameters.AddWithValue("@t", "epc_portal_pos_manage");
            upd.Parameters.AddWithValue("@v", "epc_portal_pos_manage");
            upd.Parameters.AddWithValue("@p", parentId);
            upd.Parameters.AddWithValue("@l", level);
            upd.Parameters.AddWithValue("@a", "epc_pos_manage");
            upd.Parameters.AddWithValue("@id", contentId);
            upd.ExecuteNonQuery();
        }
        else
        {
            using var ins = pdo.CreateCommand();
            ins.CommandText = """
                INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
                 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
                 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
                 VALUES (0, @u, @l, @a, @v, @p, @d, 0, 'php', @c, @t, '0', '0', '0', 0, '[]', '', '', 0, 1, 0, @now, @now, 12)
                """;
            ins.Parameters.AddWithValue("@u", contentUrl);
            ins.Parameters.AddWithValue("@l", level);
            ins.Parameters.AddWithValue("@a", "epc_pos_manage");
            ins.Parameters.AddWithValue("@v", "epc_portal_pos_manage");
            ins.Parameters.AddWithValue("@p", parentId);
            ins.Parameters.AddWithValue("@d", "Super CP — POS overview and tenant enablement");
            ins.Parameters.AddWithValue("@c", phpPath);
            ins.Parameters.AddWithValue("@t", "epc_portal_pos_manage");
            ins.Parameters.AddWithValue("@now", now);
            ins.ExecuteNonQuery();
            contentId = (int)ins.LastInsertedId;
        }

        var refId = 0;
        using (var refCmd = pdo.CreateCommand())
        {
            refCmd.CommandText = "SELECT `id` FROM `content` WHERE `url` = @u AND `is_frontend` = 0 LIMIT 1";
            refCmd.Parameters.AddWithValue("@u", "control/portal/epc_tenant_control_center");
            refId = Convert.ToInt32(refCmd.ExecuteScalar() ?? 0);
        }

        if (refId > 0 && contentId > 0)
        {
            using (var del = pdo.CreateCommand())
            {
                del.CommandText = "DELETE FROM `content_access` WHERE `content_id` = @id";
                del.Parameters.AddWithValue("@id", contentId);
                del.ExecuteNonQuery();
            }

            using var groups = pdo.CreateCommand();
            groups.CommandText = "SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = @id";
            groups.Parameters.AddWithValue("@id", refId);
            using var reader = groups.ExecuteReader();
            var ids = new List<int>();
            while (reader.Read())
            {
                ids.Add(Convert.ToInt32(reader["group_id"]));
            }

            reader.Close();
            using var insAcc = pdo.CreateCommand();
            insAcc.CommandText = "INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (@c, @g)";
            foreach (var gid in ids)
            {
                try
                {
                    insAcc.Parameters.Clear();
                    insAcc.Parameters.AddWithValue("@c", contentId);
                    insAcc.Parameters.AddWithValue("@g", gid);
                    insAcc.ExecuteNonQuery();
                }
                catch
                {
                }
            }
        }

        return contentId;
    }

    public static MySqlConnection? EpcPosSetupConnect(Dictionary<string, string> cred, VangConfig cfg)
    {
        var db = (cred.TryGetValue("db", out var rawDb) ? rawDb : "").Trim();
        if (db == "")
        {
            return null;
        }

        var user = (cred.TryGetValue("user", out var rawUser) ? rawUser : "").Trim();
        if (user == "")
        {
            user = cfg.User;
        }

        var pass = cred.TryGetValue("pass", out var rawPass) ? rawPass : "";
        if (pass == "")
        {
            pass = cfg.Password;
        }

        try
        {
            return OpenPdo != null ? OpenPdo(cfg, db, user, pass) : null;
        }
        catch
        {
            return null;
        }
    }

    private static void UpsertTranslation(MySqlConnection pdo, string key, string lang, string value)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = "INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (@k, @l, @v) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)";
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@l", lang);
        cmd.Parameters.AddWithValue("@v", value);
        cmd.ExecuteNonQuery();
    }

    private static void Collect(MySqlConnection pdo, int pid, List<int> groups)
    {
        using var ch = pdo.CreateCommand();
        ch.CommandText = "SELECT `id` FROM `groups` WHERE `parent` = @p";
        ch.Parameters.AddWithValue("@p", pid);
        using var reader = ch.ExecuteReader();
        var children = new List<int>();
        while (reader.Read())
        {
            children.Add(Convert.ToInt32(reader["id"]));
        }

        reader.Close();
        foreach (var id in children)
        {
            groups.Add(id);
            Collect(pdo, id, groups);
        }
    }

    private static Dictionary<string, object?>? FetchParent(MySqlConnection pdo, string url)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = "SELECT `id`, `level` FROM `content` WHERE `url` = @u AND `is_frontend` = 0 LIMIT 1";
        cmd.Parameters.AddWithValue("@u", url);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = Convert.ToInt32(reader["id"]),
            ["level"] = Convert.ToInt32(reader["level"])
        };
    }
}
