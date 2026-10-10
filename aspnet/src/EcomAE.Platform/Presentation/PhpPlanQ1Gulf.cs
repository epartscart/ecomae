using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-gulf Super-CP platform modules. PHP identifiers kept for the inventory:
/// <c>epc_scp_h</c>, <c>epc_scp_backend</c>, <c>epc_scp_platform_ensure_schema</c>,
/// <c>epc_scp_price_client_types</c>, <c>epc_scp_info_placements</c>,
/// <c>epc_scp_task_categories</c>, <c>epc_scp_task_statuses</c>, <c>epc_scp_task_priorities</c>,
/// <c>epc_scp_default_comm_settings</c>, <c>epc_scp_comm_settings_get</c>,
/// <c>epc_scp_comm_settings_save</c>, <c>epc_scp_price_configs_list</c>,
/// <c>epc_scp_price_config_save</c>, <c>epc_scp_price_config_delete</c>,
/// <c>epc_scp_info_blocks_list</c>, <c>epc_scp_info_block_save</c>,
/// <c>epc_scp_info_block_delete</c>, <c>epc_scp_tasks_list</c>,
/// <c>epc_scp_task_save</c>, <c>epc_scp_task_delete</c>,
/// <c>epc_scp_platform_users</c>, <c>epc_scp_tenant_options</c>,
/// <c>epc_scp_customer_name_from_row</c>, <c>epc_scp_customers_from_pdo</c>,
/// <c>epc_scp_customer_board_search</c>, <c>epc_scp_render_hero</c>,
/// <c>epc_scp_operator_guide_url</c>, <c>epc_scp_render_workspace_intro</c>,
/// <c>epc_scp_render_empty_state</c>, <c>epc_scp_guard_super_admin</c>.
/// Path: <c>content/general_pages/epc_super_cp_platform.php</c>.
/// GET never mints a session cookie. Leftover portal stay injected.
/// Operator-guide URLs are path strings without leftover unique <c>.php</c> page basenames.
/// </summary>
public static class PhpPlanQ1Gulf
{
    public const string SuperCpPlatformPath = "content/general_pages/epc_super_cp_platform.php";

    public static string BackendDir { get; set; } = "cp";
    public static Func<bool>? IsSuperHost { get; set; }
    public static Func<bool>? IsAdmin { get; set; }
    public static Action<MySqlConnection>? DbEnsure { get; set; }
    public static Func<MySqlConnection, List<Dictionary<string, object?>>>? ListTenants { get; set; }
    public static Func<Dictionary<string, object?>, MySqlConnection?>? TenantPdo { get; set; }
    public static Func<Dictionary<string, object?>, Dictionary<string, object?>>? TenantUrls { get; set; }
    public static Func<long>? Clock { get; set; }
    public static List<Dictionary<string, object?>> Tenants { get; } = [];
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        BackendDir = "cp";
        IsSuperHost = null;
        IsAdmin = null;
        DbEnsure = null;
        ListTenants = null;
        TenantPdo = null;
        TenantUrls = null;
        Clock = null;
        Tenants.Clear();
        LastOutput = "";
    }

    public static string EpcScpH(object? value)
        => WebUtility.HtmlEncode(Str(value)).Replace("&#39;", "&#039;", StringComparison.Ordinal).Replace("'", "&#039;", StringComparison.Ordinal);

    public static string EpcScpBackend()
        => BackendDir.Trim('/');

    public static void EpcScpPlatformEnsureSchema(MySqlConnection pdo)
    {
        DbEnsure?.Invoke(pdo);
        Exec(pdo,
            "CREATE TABLE IF NOT EXISTS `epc_platform_price_configs` (" +
            "`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "`name` VARCHAR(120) NOT NULL," +
            "`scope` VARCHAR(16) NOT NULL DEFAULT 'platform'," +
            "`site_key` VARCHAR(64) NOT NULL DEFAULT ''," +
            "`client_type` VARCHAR(32) NOT NULL DEFAULT 'all'," +
            "`client_ref` VARCHAR(120) NOT NULL DEFAULT ''," +
            "`markup_percent` DECIMAL(8,2) NOT NULL DEFAULT 0," +
            "`markup_fixed` DECIMAL(12,4) NOT NULL DEFAULT 0," +
            "`currency` VARCHAR(8) NOT NULL DEFAULT 'AED'," +
            "`priority` INT NOT NULL DEFAULT 100," +
            "`active` TINYINT(1) NOT NULL DEFAULT 1," +
            "`notes` TEXT NULL," +
            "`created_at` INT NOT NULL DEFAULT 0," +
            "`updated_at` INT NOT NULL DEFAULT 0," +
            "KEY `scope_site` (`scope`, `site_key`)," +
            "KEY `active_priority` (`active`, `priority`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8");
        Exec(pdo,
            "CREATE TABLE IF NOT EXISTS `epc_platform_info_blocks` (" +
            "`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "`block_key` VARCHAR(64) NOT NULL," +
            "`title` VARCHAR(200) NOT NULL," +
            "`scope` VARCHAR(16) NOT NULL DEFAULT 'platform'," +
            "`site_key` VARCHAR(64) NOT NULL DEFAULT ''," +
            "`placement` VARCHAR(64) NOT NULL DEFAULT 'homepage'," +
            "`content_html` MEDIUMTEXT NULL," +
            "`locale` VARCHAR(8) NOT NULL DEFAULT 'en'," +
            "`active` TINYINT(1) NOT NULL DEFAULT 1," +
            "`sort_order` INT NOT NULL DEFAULT 0," +
            "`created_at` INT NOT NULL DEFAULT 0," +
            "`updated_at` INT NOT NULL DEFAULT 0," +
            "UNIQUE KEY `block_unique` (`block_key`, `scope`, `site_key`, `locale`)," +
            "KEY `placement_active` (`placement`, `active`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8");
        Exec(pdo,
            "CREATE TABLE IF NOT EXISTS `epc_platform_comm_settings` (" +
            "`setting_key` VARCHAR(64) NOT NULL PRIMARY KEY," +
            "`setting_value` TEXT NULL," +
            "`updated_at` INT NOT NULL DEFAULT 0" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8");
        Exec(pdo,
            "CREATE TABLE IF NOT EXISTS `epc_platform_internal_tasks` (" +
            "`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "`title` VARCHAR(200) NOT NULL," +
            "`description` TEXT NULL," +
            "`assigned_to` INT NOT NULL DEFAULT 0," +
            "`assigned_email` VARCHAR(120) NOT NULL DEFAULT ''," +
            "`site_key` VARCHAR(64) NOT NULL DEFAULT ''," +
            "`category` VARCHAR(32) NOT NULL DEFAULT 'support'," +
            "`status` VARCHAR(24) NOT NULL DEFAULT 'open'," +
            "`priority` VARCHAR(16) NOT NULL DEFAULT 'normal'," +
            "`due_at` INT NOT NULL DEFAULT 0," +
            "`created_by` INT NOT NULL DEFAULT 0," +
            "`created_at` INT NOT NULL DEFAULT 0," +
            "`updated_at` INT NOT NULL DEFAULT 0," +
            "KEY `status_priority` (`status`, `priority`)," +
            "KEY `assigned_email` (`assigned_email`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8");
    }

    public static Dictionary<string, string> EpcScpPriceClientTypes()
        => new(StringComparer.Ordinal)
        {
            ["all"] = "All clients",
            ["catalog"] = "Built-in catalogue",
            ["api"] = "API / integration",
            ["channel"] = "Sales channel",
            ["price_list"] = "Price list"
        };

    public static Dictionary<string, string> EpcScpInfoPlacements()
        => new(StringComparer.Ordinal)
        {
            ["homepage"] = "Storefront homepage",
            ["footer"] = "Storefront footer",
            ["checkout"] = "Checkout sidebar",
            ["cp_notice"] = "CP dashboard notice",
            ["product_list"] = "Product listing banner",
            ["login"] = "Login / register page"
        };

    public static Dictionary<string, string> EpcScpTaskCategories()
        => new(StringComparer.Ordinal)
        {
            ["onboarding"] = "Onboarding",
            ["support"] = "Support",
            ["billing"] = "Billing",
            ["pricing"] = "Pricing",
            ["content"] = "Content",
            ["other"] = "Other"
        };

    public static Dictionary<string, string> EpcScpTaskStatuses()
        => new(StringComparer.Ordinal)
        {
            ["open"] = "Open",
            ["in_progress"] = "In progress",
            ["done"] = "Done",
            ["cancelled"] = "Cancelled"
        };

    public static Dictionary<string, string> EpcScpTaskPriorities()
        => new(StringComparer.Ordinal)
        {
            ["low"] = "Low",
            ["normal"] = "Normal",
            ["high"] = "High",
            ["urgent"] = "Urgent"
        };

    public static Dictionary<string, string> EpcScpDefaultCommSettings()
        => new(StringComparer.Ordinal)
        {
            ["notify_from_name"] = "ECOM AE Platform",
            ["notify_from_email"] = "noreply@ecomae.com",
            ["notify_reply_to"] = "support@ecomae.com",
            ["notify_tenant_onboard"] = "1",
            ["notify_tenant_dns_live"] = "1",
            ["notify_demo_expiry"] = "1",
            ["notify_task_assigned"] = "1",
            ["notify_daily_digest"] = "0",
            ["digest_hour_utc"] = "6"
        };

    public static Dictionary<string, string> EpcScpCommSettingsGet(MySqlConnection pdo)
    {
        EpcScpPlatformEnsureSchema(pdo);
        var defaults = EpcScpDefaultCommSettings();
        foreach (var row in Query(pdo, "SELECT `setting_key`, `setting_value` FROM `epc_platform_comm_settings`"))
        {
            defaults[Str(row.GetValueOrDefault("setting_key"))] = Str(row.GetValueOrDefault("setting_value"));
        }

        return defaults;
    }

    public static void EpcScpCommSettingsSave(MySqlConnection pdo, Dictionary<string, object?> data)
    {
        EpcScpPlatformEnsureSchema(pdo);
        var now = Now();
        foreach (var key in EpcScpDefaultCommSettings().Keys)
        {
            if (!data.ContainsKey(key))
            {
                continue;
            }

            var val = Str(data[key]);
            if (key.StartsWith("notify_", StringComparison.Ordinal))
            {
                val = PhpEmpty(data[key]) ? "0" : "1";
            }

            Exec(pdo,
                "INSERT INTO `epc_platform_comm_settings` (`setting_key`, `setting_value`, `updated_at`) VALUES (@k, @v, @t) " +
                "ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`), `updated_at` = VALUES(`updated_at`)",
                ("@k", key), ("@v", val), ("@t", now));
        }
    }

    public static List<Dictionary<string, object?>> EpcScpPriceConfigsList(MySqlConnection pdo)
    {
        EpcScpPlatformEnsureSchema(pdo);
        return Query(pdo, "SELECT * FROM `epc_platform_price_configs` ORDER BY `active` DESC, `priority` ASC, `name` ASC");
    }

    public static Dictionary<string, object?> EpcScpPriceConfigSave(MySqlConnection pdo, Dictionary<string, object?> data, int id = 0)
    {
        EpcScpPlatformEnsureSchema(pdo);
        var name = Str(data.GetValueOrDefault("name")).Trim();
        if (name == "")
        {
            return Fail("Name is required");
        }

        var scope = Str(data.GetValueOrDefault("scope"));
        if (scope is not ("platform" or "tenant"))
        {
            scope = "platform";
        }

        var siteKey = Regex.Replace(Str(data.GetValueOrDefault("site_key")).ToLowerInvariant(), @"[^a-z0-9_]", "");
        var clientType = Str(data.GetValueOrDefault("client_type"), "all");
        if (!EpcScpPriceClientTypes().ContainsKey(clientType))
        {
            clientType = "all";
        }

        var now = Now();
        var currency = ByteSubstr(Str(data.GetValueOrDefault("currency"), "AED").Trim().ToUpperInvariant(), 8);
        var priority = Math.Max(1, ToInt(data.GetValueOrDefault("priority"), 100));
        var active = PhpEmpty(data.GetValueOrDefault("active")) ? 0 : 1;
        var clientRef = Str(data.GetValueOrDefault("client_ref")).Trim();
        var notes = Str(data.GetValueOrDefault("notes")).Trim();
        var percent = ToFloat(data.GetValueOrDefault("markup_percent"));
        var fixedAmt = ToFloat(data.GetValueOrDefault("markup_fixed"));
        if (id > 0)
        {
            Exec(pdo,
                "UPDATE `epc_platform_price_configs` SET `name`=@n, `scope`=@s, `site_key`=@k, `client_type`=@c, `client_ref`=@r, " +
                "`markup_percent`=@p, `markup_fixed`=@f, `currency`=@cur, `priority`=@pr, `active`=@a, `notes`=@notes, `updated_at`=@t WHERE `id`=@id",
                ("@n", name), ("@s", scope), ("@k", siteKey), ("@c", clientType), ("@r", clientRef),
                ("@p", percent), ("@f", fixedAmt), ("@cur", currency), ("@pr", priority), ("@a", active), ("@notes", notes), ("@t", now), ("@id", id));
            return Ok(id);
        }

        var newId = Insert(pdo,
            "INSERT INTO `epc_platform_price_configs` (`name`,`scope`,`site_key`,`client_type`,`client_ref`,`markup_percent`,`markup_fixed`,`currency`,`priority`,`active`,`notes`,`updated_at`,`created_at`) " +
            "VALUES (@n,@s,@k,@c,@r,@p,@f,@cur,@pr,@a,@notes,@t,@t2)",
            ("@n", name), ("@s", scope), ("@k", siteKey), ("@c", clientType), ("@r", clientRef),
            ("@p", percent), ("@f", fixedAmt), ("@cur", currency), ("@pr", priority), ("@a", active), ("@notes", notes), ("@t", now), ("@t2", now));
        return Ok(newId);
    }

    public static bool EpcScpPriceConfigDelete(MySqlConnection pdo, int id)
    {
        EpcScpPlatformEnsureSchema(pdo);
        Exec(pdo, "DELETE FROM `epc_platform_price_configs` WHERE `id`=@id", ("@id", id));
        return true;
    }

    public static List<Dictionary<string, object?>> EpcScpInfoBlocksList(MySqlConnection pdo, string placement = "")
    {
        EpcScpPlatformEnsureSchema(pdo);
        if (placement != "")
        {
            return Query(pdo, "SELECT * FROM `epc_platform_info_blocks` WHERE `placement`=@p ORDER BY `sort_order` ASC, `title` ASC", ("@p", placement));
        }

        return Query(pdo, "SELECT * FROM `epc_platform_info_blocks` ORDER BY `placement` ASC, `sort_order` ASC, `title` ASC");
    }

    public static Dictionary<string, object?> EpcScpInfoBlockSave(MySqlConnection pdo, Dictionary<string, object?> data, int id = 0)
    {
        EpcScpPlatformEnsureSchema(pdo);
        var key = Regex.Replace(Str(data.GetValueOrDefault("block_key")).Trim().ToLowerInvariant(), @"[^a-z0-9_-]", "");
        var title = Str(data.GetValueOrDefault("title")).Trim();
        if (key == "" || title == "")
        {
            return Fail("Block key and title are required");
        }

        var scope = Str(data.GetValueOrDefault("scope"));
        if (scope is not ("platform" or "tenant"))
        {
            scope = "platform";
        }

        var siteKey = Regex.Replace(Str(data.GetValueOrDefault("site_key")).ToLowerInvariant(), @"[^a-z0-9_]", "");
        var placement = Str(data.GetValueOrDefault("placement"), "homepage");
        if (!EpcScpInfoPlacements().ContainsKey(placement))
        {
            placement = "homepage";
        }

        var locale = ByteSubstr(Str(data.GetValueOrDefault("locale"), "en").Trim(), 8);
        if (locale == "")
        {
            locale = "en";
        }

        var now = Now();
        var active = PhpEmpty(data.GetValueOrDefault("active")) ? 0 : 1;
        var sort = ToInt(data.GetValueOrDefault("sort_order"));
        var html = Str(data.GetValueOrDefault("content_html"));
        if (id > 0)
        {
            Exec(pdo,
                "UPDATE `epc_platform_info_blocks` SET `block_key`=@k, `title`=@t, `scope`=@s, `site_key`=@sk, `placement`=@p, " +
                "`content_html`=@h, `locale`=@l, `active`=@a, `sort_order`=@o, `updated_at`=@u WHERE `id`=@id",
                ("@k", key), ("@t", title), ("@s", scope), ("@sk", siteKey), ("@p", placement),
                ("@h", html), ("@l", locale), ("@a", active), ("@o", sort), ("@u", now), ("@id", id));
            return Ok(id);
        }

        try
        {
            var newId = Insert(pdo,
                "INSERT INTO `epc_platform_info_blocks` (`block_key`,`title`,`scope`,`site_key`,`placement`,`content_html`,`locale`,`active`,`sort_order`,`updated_at`,`created_at`) " +
                "VALUES (@k,@t,@s,@sk,@p,@h,@l,@a,@o,@u,@c)",
                ("@k", key), ("@t", title), ("@s", scope), ("@sk", siteKey), ("@p", placement),
                ("@h", html), ("@l", locale), ("@a", active), ("@o", sort), ("@u", now), ("@c", now));
            return Ok(newId);
        }
        catch
        {
            return Fail("Duplicate block key for this scope/locale");
        }
    }

    public static bool EpcScpInfoBlockDelete(MySqlConnection pdo, int id)
    {
        EpcScpPlatformEnsureSchema(pdo);
        Exec(pdo, "DELETE FROM `epc_platform_info_blocks` WHERE `id`=@id", ("@id", id));
        return true;
    }

    public static List<Dictionary<string, object?>> EpcScpTasksList(MySqlConnection pdo, string statusFilter = "")
    {
        EpcScpPlatformEnsureSchema(pdo);
        if (statusFilter != "" && EpcScpTaskStatuses().ContainsKey(statusFilter))
        {
            return Query(pdo,
                "SELECT * FROM `epc_platform_internal_tasks` WHERE `status`=@s ORDER BY FIELD(`priority`, 'urgent', 'high', 'normal', 'low'), `due_at` ASC, `id` DESC",
                ("@s", statusFilter));
        }

        return Query(pdo,
            "SELECT * FROM `epc_platform_internal_tasks` ORDER BY FIELD(`status`, 'open', 'in_progress', 'done', 'cancelled'), FIELD(`priority`, 'urgent', 'high', 'normal', 'low'), `due_at` ASC, `id` DESC LIMIT 200");
    }

    public static Dictionary<string, object?> EpcScpTaskSave(MySqlConnection pdo, Dictionary<string, object?> data, int id = 0, int createdBy = 0)
    {
        EpcScpPlatformEnsureSchema(pdo);
        var title = Str(data.GetValueOrDefault("title")).Trim();
        if (title == "")
        {
            return Fail("Title is required");
        }

        var category = Str(data.GetValueOrDefault("category"), "support");
        if (!EpcScpTaskCategories().ContainsKey(category))
        {
            category = "support";
        }

        var status = Str(data.GetValueOrDefault("status"), "open");
        if (!EpcScpTaskStatuses().ContainsKey(status))
        {
            status = "open";
        }

        var priority = Str(data.GetValueOrDefault("priority"), "normal");
        if (!EpcScpTaskPriorities().ContainsKey(priority))
        {
            priority = "normal";
        }

        var now = Now();
        var assignedTo = Math.Max(0, ToInt(data.GetValueOrDefault("assigned_to")));
        var email = Str(data.GetValueOrDefault("assigned_email")).Trim().ToLowerInvariant();
        var siteKey = Regex.Replace(Str(data.GetValueOrDefault("site_key")).ToLowerInvariant(), @"[^a-z0-9_]", "");
        var due = Math.Max(0, ToInt(data.GetValueOrDefault("due_at")));
        var desc = Str(data.GetValueOrDefault("description")).Trim();
        if (id > 0)
        {
            Exec(pdo,
                "UPDATE `epc_platform_internal_tasks` SET `title`=@t, `description`=@d, `assigned_to`=@a, `assigned_email`=@e, `site_key`=@s, " +
                "`category`=@c, `status`=@st, `priority`=@p, `due_at`=@due, `updated_at`=@u WHERE `id`=@id",
                ("@t", title), ("@d", desc), ("@a", assignedTo), ("@e", email), ("@s", siteKey),
                ("@c", category), ("@st", status), ("@p", priority), ("@due", due), ("@u", now), ("@id", id));
            return Ok(id);
        }

        var newId = Insert(pdo,
            "INSERT INTO `epc_platform_internal_tasks` (`title`,`description`,`assigned_to`,`assigned_email`,`site_key`,`category`,`status`,`priority`,`due_at`,`updated_at`,`created_by`,`created_at`) " +
            "VALUES (@t,@d,@a,@e,@s,@c,@st,@p,@due,@u,@by,@cr)",
            ("@t", title), ("@d", desc), ("@a", assignedTo), ("@e", email), ("@s", siteKey),
            ("@c", category), ("@st", status), ("@p", priority), ("@due", due), ("@u", now),
            ("@by", Math.Max(0, createdBy)), ("@cr", now));
        return Ok(newId);
    }

    public static bool EpcScpTaskDelete(MySqlConnection pdo, int id)
    {
        EpcScpPlatformEnsureSchema(pdo);
        Exec(pdo, "DELETE FROM `epc_platform_internal_tasks` WHERE `id`=@id", ("@id", id));
        return true;
    }

    public static List<Dictionary<string, object?>> EpcScpPlatformUsers(MySqlConnection pdo)
    {
        try
        {
            return Query(pdo,
                "SELECT u.`user_id`, u.`email`, MAX(CASE WHEN up.`data_key` = 'name' THEN up.`data_value` END) AS fname " +
                "FROM `users` u LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id` " +
                "WHERE u.`user_id` > 0 GROUP BY u.`user_id`, u.`email` ORDER BY u.`email` ASC LIMIT 100");
        }
        catch
        {
            return [];
        }
    }

    public static List<Dictionary<string, object?>> EpcScpTenantOptions(MySqlConnection pdo)
    {
        var tenants = ListTenants?.Invoke(pdo) ?? Tenants;
        var output = new List<Dictionary<string, object?>>();
        foreach (var t in tenants)
        {
            if (PhpEmpty(t.GetValueOrDefault("in_registry")))
            {
                continue;
            }

            var key = Str(t.GetValueOrDefault("site_key"));
            if (key == "")
            {
                continue;
            }

            var urls = t.GetValueOrDefault("urls") is Dictionary<string, object?> map
                ? map
                : TenantUrls?.Invoke(t) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            output.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = key,
                ["label"] = Str(t.GetValueOrDefault("trade_name"), key).Trim() + " (" + key + ")",
                ["hostname"] = Str(t.GetValueOrDefault("hostname")),
                ["urls"] = urls
            });
        }

        return output;
    }

    public static string EpcScpCustomerNameFromRow(Dictionary<string, object?> row)
    {
        var parts = new[] { Str(row.GetValueOrDefault("fname")).Trim(), Str(row.GetValueOrDefault("sname")).Trim() }
            .Where(p => p != "").ToArray();
        if (parts.Length > 0)
        {
            return string.Join(" ", parts);
        }

        var company = Str(row.GetValueOrDefault("company")).Trim();
        return company != "" ? company : Str(row.GetValueOrDefault("email")).Trim();
    }

    public static List<Dictionary<string, object?>> EpcScpCustomersFromPdo(
        MySqlConnection tenantPdo,
        Dictionary<string, object?> tenantMeta,
        string search,
        int limit)
    {
        limit = Math.Max(1, Math.Min(50, limit));
        var sql = "SELECT u.`user_id`, u.`email`, u.`phone`, u.`time_reg`, " +
                  "MAX(CASE WHEN up.`data_key` = 'name' THEN up.`data_value` END) AS fname, " +
                  "MAX(CASE WHEN up.`data_key` = 'surname' THEN up.`data_value` END) AS sname, " +
                  "MAX(CASE WHEN up.`data_key` = 'company' THEN up.`data_value` END) AS company " +
                  "FROM `users` u LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id` WHERE u.`user_id` > 0";
        var args = new List<(string, object?)>();
        if (search != "")
        {
            sql += " AND (u.`email` LIKE @q OR u.`phone` LIKE @q2 OR up.`data_value` LIKE @q3)";
            var q = "%" + search + "%";
            args.Add(("@q", q));
            args.Add(("@q2", q));
            args.Add(("@q3", q));
        }

        sql += " GROUP BY u.`user_id`, u.`email`, u.`phone`, u.`time_reg` ORDER BY u.`user_id` DESC LIMIT " + limit;
        List<Dictionary<string, object?>> rows;
        try
        {
            rows = Query(tenantPdo, sql, args.ToArray());
        }
        catch
        {
            return [];
        }

        var urls = tenantMeta.GetValueOrDefault("urls") is Dictionary<string, object?> u
            ? u
            : new Dictionary<string, object?>(StringComparer.Ordinal);
        var cpBase = Str(urls.GetValueOrDefault("cp")).TrimEnd('/');
        var erpUrl = urls.ContainsKey("client_erp") ? Str(urls["client_erp"]).TrimEnd('/') : cpBase;
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var r in rows)
        {
            outRows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["source"] = Str(tenantMeta.GetValueOrDefault("site_key")),
                ["source_label"] = Str(tenantMeta.GetValueOrDefault("label")),
                ["hostname"] = Str(tenantMeta.GetValueOrDefault("hostname")),
                ["user_id"] = ToInt(r.GetValueOrDefault("user_id")),
                ["name"] = EpcScpCustomerNameFromRow(r),
                ["email"] = Str(r.GetValueOrDefault("email")),
                ["phone"] = Str(r.GetValueOrDefault("phone")),
                ["time_reg"] = ToInt(r.GetValueOrDefault("time_reg")),
                ["links"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["crm"] = cpBase != "" ? cpBase + "/shop/customer_mgmt/customer_mgmt?tab=customers&user_id=" + ToInt(r.GetValueOrDefault("user_id")) : "",
                    ["erp"] = erpUrl != "" ? erpUrl + "/shop/finance/erp?epc_erp_shell=1&area=sales" : "",
                    ["cp"] = cpBase
                }
            });
        }

        return outRows;
    }

    public static Dictionary<string, object?> EpcScpCustomerBoardSearch(
        MySqlConnection platformPdo,
        string search = "",
        string tenantFilter = "",
        int page = 1,
        int perPage = 50)
    {
        search = search.Trim();
        tenantFilter = Regex.Replace(tenantFilter.ToLowerInvariant(), @"[^a-z0-9_]", "");
        page = Math.Max(1, page);
        perPage = Math.Max(10, Math.Min(100, perPage));
        var results = new List<Dictionary<string, object?>>();
        var stats = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["platform"] = 0,
            ["tenants_scanned"] = 0,
            ["tenants_with_hits"] = 0
        };

        var platformSql = "SELECT u.`user_id`, u.`email`, u.`phone`, u.`time_reg`, " +
                          "MAX(CASE WHEN up.`data_key` = 'name' THEN up.`data_value` END) AS fname, " +
                          "MAX(CASE WHEN up.`data_key` = 'surname' THEN up.`data_value` END) AS sname, " +
                          "MAX(CASE WHEN up.`data_key` = 'company' THEN up.`data_value` END) AS company " +
                          "FROM `users` u LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id` WHERE u.`user_id` > 0";
        var args = new List<(string, object?)>();
        if (search != "")
        {
            platformSql += " AND (u.`email` LIKE @q OR u.`phone` LIKE @q2 OR up.`data_value` LIKE @q3)";
            var q = "%" + search + "%";
            args.Add(("@q", q));
            args.Add(("@q2", q));
            args.Add(("@q3", q));
        }

        platformSql += " GROUP BY u.`user_id` ORDER BY u.`user_id` DESC LIMIT 80";
        try
        {
            var backend = EpcScpBackend();
            foreach (var r in Query(platformPdo, platformSql, args.ToArray()))
            {
                results.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["source"] = "platform",
                    ["source_label"] = "Platform (ecomae)",
                    ["hostname"] = "www.ecomae.com",
                    ["user_id"] = ToInt(r.GetValueOrDefault("user_id")),
                    ["name"] = EpcScpCustomerNameFromRow(r),
                    ["email"] = Str(r.GetValueOrDefault("email")),
                    ["phone"] = Str(r.GetValueOrDefault("phone")),
                    ["time_reg"] = ToInt(r.GetValueOrDefault("time_reg")),
                    ["links"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["crm"] = "/" + backend + "/shop/customer_mgmt/customer_mgmt?tab=customers&user_id=" + ToInt(r.GetValueOrDefault("user_id")),
                        ["erp"] = "/" + backend + "/shop/finance/erp?epc_erp_shell=1&area=sales",
                        ["cp"] = "/" + backend + "/"
                    }
                });
            }

            stats["platform"] = results.Count;
        }
        catch
        {
        }

        foreach (var t in ListTenants?.Invoke(platformPdo) ?? Tenants)
        {
            if (PhpEmpty(t.GetValueOrDefault("in_registry")) || !PhpEmpty(t.GetValueOrDefault("access_blocked")))
            {
                continue;
            }

            var key = Str(t.GetValueOrDefault("site_key"));
            if (tenantFilter != "" && tenantFilter != "platform" && tenantFilter != key)
            {
                continue;
            }

            if (tenantFilter == "platform")
            {
                continue;
            }

            var tenantDb = TenantPdo?.Invoke(t);
            if (tenantDb is null)
            {
                continue;
            }

            stats["tenants_scanned"] = ToInt(stats["tenants_scanned"]) + 1;
            var urls = t.GetValueOrDefault("urls") is Dictionary<string, object?> map
                ? map
                : TenantUrls?.Invoke(t) ?? new Dictionary<string, object?>(StringComparer.Ordinal);
            var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = key,
                ["label"] = Str(t.GetValueOrDefault("trade_name"), key).Trim(),
                ["hostname"] = Str(t.GetValueOrDefault("hostname")),
                ["urls"] = urls
            };
            var chunk = EpcScpCustomersFromPdo(tenantDb, meta, search, 25);
            if (chunk.Count > 0)
            {
                stats["tenants_with_hits"] = ToInt(stats["tenants_with_hits"]) + 1;
            }

            results.AddRange(chunk);
            if (results.Count >= 200)
            {
                break;
            }
        }

        results.Sort((a, b) => string.CompareOrdinal(Str(b.GetValueOrDefault("email")), Str(a.GetValueOrDefault("email"))));
        var total = results.Count;
        var offset = (page - 1) * perPage;
        var pageRows = offset >= total
            ? new List<Dictionary<string, object?>>()
            : results.Skip(offset).Take(perPage).ToList();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["rows"] = pageRows,
            ["total"] = total,
            ["page"] = page,
            ["per_page"] = perPage,
            ["stats"] = stats
        };
    }

    public static string EpcScpRenderHero(string badge, string title, string sub, List<Dictionary<string, object?>>? actions = null)
    {
        actions ??= [];
        var sb = new StringBuilder();
        sb.Append("<div class=\"epc-scp-panel__hero\">\n\t<div>\n");
        sb.Append("\t\t<span class=\"epc-scp-dashboard__badge\">").Append(EpcScpH(badge)).Append("</span>\n");
        sb.Append("\t\t<h2 class=\"epc-scp-dashboard__title\">").Append(EpcScpH(title)).Append("</h2>\n");
        sb.Append("\t\t<p class=\"epc-scp-dashboard__sub\">").Append(EpcScpH(sub)).Append("</p>\n");
        sb.Append("\t</div>\n");
        if (actions.Count > 0)
        {
            sb.Append("\t\t<div class=\"epc-scp-dashboard__hero-actions\">\n");
            foreach (var act in actions)
            {
                var cls = PhpEmpty(act.GetValueOrDefault("primary")) ? "btn-default" : "btn-primary";
                sb.Append("\t\t\t\t<a class=\"btn btn-sm ").Append(cls).Append("\" href=\"").Append(EpcScpH(act.GetValueOrDefault("url"))).Append("\">\n");
                if (!PhpEmpty(act.GetValueOrDefault("icon")))
                {
                    sb.Append("\t\t\t<i class=\"fa ").Append(EpcScpH(act.GetValueOrDefault("icon"))).Append("\"></i> ");
                }

                sb.Append("\t\t\t").Append(EpcScpH(act.GetValueOrDefault("label"))).Append("\t\t</a>\n");
            }

            sb.Append("\t\t\t</div>\n");
        }

        sb.Append("\t</div>\n\t");
        LastOutput = sb.ToString();
        return LastOutput;
    }

    public static string EpcScpOperatorGuideUrl()
        => "/" + EpcScpBackend() + "/control/portal/epc_super_cp_operator_guide";

    public static string EpcScpRenderWorkspaceIntro(string module)
    {
        var intros = new Dictionary<string, (string Title, string Body)>(StringComparer.Ordinal)
        {
            ["customer_board"] = ("Operator workspace — Customer board",
                "Search customers across the platform registry and every live tenant database. Use CRM and ERP links for support without logging into each client CP separately."),
            ["info_blocks"] = ("Operator workspace — Info blocks",
                "Publish HTML banners and notices on platform marketing pages, tenant storefronts, checkout, and CP dashboard slots. Scope blocks platform-wide or per tenant."),
            ["price_configs"] = ("Operator workspace — Price configs",
                "Stack markup rules for catalogue, price lists, and API clients. Platform defaults apply everywhere unless a tenant override wins on priority."),
            ["communication"] = ("Operator workspace — Communication",
                "Set which platform events send email, review SMTP diagnostics, and track internal tasks assigned to ECOM AE operators.")
        };
        if (!intros.TryGetValue(module, out var row))
        {
            LastOutput = "";
            return "";
        }

        LastOutput =
            "<div class=\"epc-scp-intro-panel\">\n\t<div class=\"epc-scp-intro-panel__body\">\n\t\t<strong>" +
            EpcScpH(row.Title) + "</strong>\n\t\t<p>" + EpcScpH(row.Body) +
            "</p>\n\t</div>\n\t<a class=\"btn btn-sm btn-default\" href=\"" + EpcScpH(EpcScpOperatorGuideUrl()) +
            "\"><i class=\"fa fa-book\"></i> Operator guide</a>\n</div>\n\t";
        return LastOutput;
    }

    public static string EpcScpRenderEmptyState(string title, string body, List<Dictionary<string, object?>>? actions = null)
    {
        actions ??= [];
        var sb = new StringBuilder();
        sb.Append("<div class=\"epc-scp-empty-state\">\n");
        sb.Append("\t<div class=\"epc-scp-empty-state__icon\"><i class=\"fa fa-inbox\"></i></div>\n");
        sb.Append("\t<h4>").Append(EpcScpH(title)).Append("</h4>\n");
        sb.Append("\t<p>").Append(EpcScpH(body)).Append("</p>\n");
        if (actions.Count > 0)
        {
            sb.Append("\t<div class=\"epc-scp-empty-state__actions\">\n");
            foreach (var act in actions)
            {
                var cls = PhpEmpty(act.GetValueOrDefault("primary")) ? "btn-default" : "btn-primary";
                sb.Append("\t\t<a class=\"btn btn-sm ").Append(cls).Append("\" href=\"").Append(EpcScpH(act.GetValueOrDefault("url"))).Append("\">\n");
                if (!PhpEmpty(act.GetValueOrDefault("icon")))
                {
                    sb.Append("\t\t\t<i class=\"fa ").Append(EpcScpH(act.GetValueOrDefault("icon"))).Append("\"></i> ");
                }

                sb.Append("\t\t\t").Append(EpcScpH(act.GetValueOrDefault("label"))).Append("\n\t\t</a>\n");
            }

            sb.Append("\t</div>\n");
        }

        sb.Append("\t</div>\n\t");
        LastOutput = sb.ToString();
        return LastOutput;
    }

    public static bool EpcScpGuardSuperAdmin()
    {
        if (IsSuperHost?.Invoke() != true)
        {
            LastOutput = "<div class=\"alert alert-warning\">This module is available on ECOM AE Super CP only.</div>";
            return false;
        }

        if (IsAdmin?.Invoke() != true)
        {
            LastOutput = "<div class=\"alert alert-warning\">Please <a href=\"/" + EpcScpH(BackendDir) + "/\">log in to Super CP</a>.</div>";
            return false;
        }

        LastOutput = "";
        return true;
    }

    private static Dictionary<string, object?> Ok(int id)
        => new(StringComparer.Ordinal) { ["ok"] = true, ["id"] = id };

    private static Dictionary<string, object?> Fail(string message)
        => new(StringComparer.Ordinal) { ["ok"] = false, ["message"] = message };

    private static long Now()
        => Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string ByteSubstr(string value, int max)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length <= max)
        {
            return value;
        }

        return Encoding.UTF8.GetString(bytes, 0, max);
    }

    private static void Exec(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        cmd.ExecuteNonQuery();
    }

    private static int Insert(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        cmd.ExecuteNonQuery();
        return (int)cmd.LastInsertedId;
    }

    private static List<Dictionary<string, object?>> Query(MySqlConnection pdo, string sql, params (string Name, object? Value)[] args)
    {
        using var cmd = pdo.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        using var reader = cmd.ExecuteReader();
        var rows = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : Box(reader.GetValue(i));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static object? Box(object value)
        => value switch
        {
            decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
            bool b => b ? 1 : 0,
            sbyte sb => (int)sb,
            byte b => (int)b,
            short s => (int)s,
            ushort us => (int)us,
            uint ui => (int)ui,
            long l when l is >= int.MinValue and <= int.MaxValue => (int)l,
            ulong ul when ul <= int.MaxValue => (int)ul,
            _ => value
        };

    private static bool PhpEmpty(object? value)
    {
        if (value is null or DBNull or false)
        {
            return true;
        }

        return value switch
        {
            string s => s is "" or "0",
            bool b => !b,
            int i => i == 0,
            long l => l == 0,
            short s => s == 0,
            byte b => b == 0,
            sbyte sb => sb == 0,
            double d => d == 0,
            float f => f == 0,
            decimal m => m == 0,
            _ => false
        };
    }

    private static int ToInt(object? value, int fallback = 0)
    {
        if (value is null or DBNull or false)
        {
            return fallback;
        }

        if (value is true)
        {
            return 1;
        }

        try
        {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    private static double ToFloat(object? value)
    {
        if (value is null or DBNull)
        {
            return 0;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
        {
            return n;
        }

        return 0;
    }

    private static string Str(object? value, string fallback = "")
        => value is null or DBNull ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
}
