using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string CrossLinksMissing = "Cross links are not in this database.";
    public const string CustomerAccountingMissing = "Customer accounting is not in this database.";
    public const string CustomersMissing = "Customers are not in this database.";
    public const string CompleteSaleNotPosted = "complete_sale was not posted";
    public const string CrmQuoteNotPosted = "ERP CRM quote was not posted";
    public const string WalkInNotCreated = "Walk-in customer was not created";
    public const string CrossDeleteNeedsFilter = "A filter is required before deleting cross links.";
    public const string PriceDeleteNeedsFilter = "A filter is required before deleting price rows.";

    public sealed record StatusOnly([property: JsonPropertyName("status")] bool Status);

    private static readonly Dictionary<string, string[]> MarketingTasks = new(StringComparer.Ordinal)
    {
        ["measurement"] = ["gsc_verify", "gsc_sitemap", "ga_conversions", "gbp_profile", "baseline_kpi", "weekly_routine"],
        ["seo"] = ["audit_titles", "top500_landing", "vehicle_pages", "content_plan", "schema_jsonld", "ar_pages", "core_web_vitals"],
        ["paid_ads"] = ["google_ads_account", "search_campaign", "negative_keywords", "meta_pixel", "whatsapp_ads", "landing_audit", "roas_sheet"],
        ["marketplaces"] = ["amazon_seller", "feed_top_sku", "channels_sku_map", "ebay_listings", "dubizzle_batch", "order_import_test", "pricing_rules"],
        ["whatsapp_social"] = ["wa_business_profile", "social_calendar", "short_video", "wa_phase2_creds", "track_wa_clicks", "influencer_garages"],
        ["trust"] = ["shipping_page", "returns_policy", "about_page", "google_reviews", "trust_badges", "stock_transparency"],
        ["international"] = ["gcc_shipping", "currency_display", "hreflang_audit", "export_docs", "region_landing", "payment_international"],
        ["email_retention"] = ["esp_setup", "cart_abandon_flow", "newsletter_signup", "back_in_stock", "segment_b2b", "unsubscribe_compliance"],
        ["partnerships"] = ["b2b_price_list", "outreach_20", "fleet_pitch", "approval_workflow", "partner_landing", "monthly_review"],
        ["quick_wins"] = ["gsc_link", "top_parts_seo", "wa_ga_event", "channels_sample", "mobile_speed", "cp_marketing_weekly"]
    };

    private static readonly Dictionary<string, string[]> MarketingKpis = new(StringComparer.Ordinal)
    {
        ["measurement"] = ["monthly_sessions", "organic_clicks", "conversion_rate", "whatsapp_clicks"],
        ["seo"] = ["indexed_pages", "avg_position", "organic_orders", "backlinks"],
        ["paid_ads"] = ["ad_spend", "cpc", "roas", "paid_orders"],
        ["marketplaces"] = ["marketplace_orders", "sku_mapped", "channel_revenue", "stock_sync_errors"],
        ["whatsapp_social"] = ["wa_inbound_chats", "wa_orders", "social_referrals", "wa_api_sent"],
        ["trust"] = ["google_rating", "review_count", "checkout_completion", "trust_bounce"],
        ["international"] = ["intl_orders_pct", "gcc_orders", "intl_shipping_time", "intl_revenue"],
        ["email_retention"] = ["email_list_size", "open_rate", "cart_recovery", "repeat_customers"],
        ["partnerships"] = ["b2b_accounts", "b2b_revenue", "b2b_orders", "partner_pipeline"],
        ["quick_wins"] = ["strategy_completion", "site_orders_30d", "registered_users", "price_rows"]
    };

    public static async Task<object> MarketingAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> fields,
        bool endpoint,
        bool isPost,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Access denied"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        fields.TryGetValue("action", out var action);
        action ??= string.Empty;
        if (endpoint && (!isPost || action.Length == 0))
        {
            return new FlagBody(false, "No action");
        }

        try
        {
            await EnsureMarketingSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            return action switch
            {
                "toggle_task" => await MarketingToggleAsync(connection, fields, PhpInt(adminUser), cancellationToken).ConfigureAwait(false),
                "save_kpi" => await MarketingKpiAsync(connection, fields, PhpInt(adminUser), cancellationToken).ConfigureAwait(false),
                "save_review" => await MarketingReviewAsync(connection, fields, PhpInt(adminUser), cancellationToken).ConfigureAwait(false),
                "snapshot" => await MarketingSnapshotAsync(connection, cancellationToken).ConfigureAwait(false),
                _ => new FlagBody(false, "Unknown action")
            };
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, "Marketing tables are not in this database.");
        }
        catch (InvalidOperationException ex)
        {
            return new FlagBody(false, ex.Message);
        }
    }

    public static async Task<object> WorkshopEndpointAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> fields,
        bool isPost,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Access denied"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        fields.TryGetValue("action", out var action);
        action ??= string.Empty;
        if (!isPost || action.Length == 0)
        {
            return new FlagBody(false, "No action");
        }

        var csrf = await ShopStoredCsrfAsync(connection, adminSession, adminUser, cancellationToken).ConfigureAwait(false);
        fields.TryGetValue("csrf_guard_key", out var posted);
        if (string.IsNullOrEmpty(csrf) || !string.Equals(csrf, posted, StringComparison.Ordinal))
        {
            return new FlagBody(false, "CSRF failed");
        }

        try
        {
            await EnsureWorkshopAsync(connection, cancellationToken).ConfigureAwait(false);
            return await WorkshopActionAsync(connection, action, fields, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, "Workshop tables are not in this database.");
        }
        catch (InvalidOperationException ex)
        {
            return new FlagBody(false, ex.Message);
        }
    }

    public static async Task<object> CrossesOperationsAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? requestObject,
        string? sortCookie,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new CodedJson(403, new JsonObject { ["status"] = false, ["message"] = "forbidden" }), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        return await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new CodedJson(403, new JsonObject { ["status"] = false, ["message"] = "forbidden" }),
            async (_, token) =>
            {
                if (!ShopJson(requestObject, out var root))
                {
                    return new JsonObject { ["status"] = false, ["message"] = "bad_request" };
                }

                var action = ShopText(root, "action");
                try
                {
                    return action switch
                    {
                        "get_table_crosses" => await CrossTableAsync(connection, root, sortCookie, token).ConfigureAwait(false),
                        "add_crosses" => await CrossAddAsync(connection, root, token).ConfigureAwait(false),
                        "save_crosses" => await CrossSaveAsync(connection, root, token).ConfigureAwait(false),
                        "del_crosses" => await CrossDeleteAsync(connection, ShopInt(root, "id"), token).ConfigureAwait(false),
                        "del_search_crosses" => await CrossDeleteSearchAsync(connection, root, token).ConfigureAwait(false),
                        "get_search_manufacturer" => await CrossManufacturersAsync(connection, root, token).ConfigureAwait(false),
                        _ => new JsonObject { ["status"] = false }
                    };
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, CrossLinksMissing);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<object> PricesEditAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? requestObject,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new StatusOnly(false), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        return await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new StatusOnly(false),
            async (_, token) =>
            {
                if (!ShopJson(requestObject, out var root))
                {
                    return new JsonObject { ["status"] = false, ["message"] = "bad_request" };
                }

                try
                {
                    return ShopText(root, "action") switch
                    {
                        "get_table" => await PriceTableAsync(connection, root, token).ConfigureAwait(false),
                        "add" => await PriceWriteAsync(connection, root, 0, token).ConfigureAwait(false),
                        "save" => await PriceWriteAsync(connection, root, ShopInt(root, "id"), token).ConfigureAwait(false),
                        "del" => await PriceDeleteAsync(connection, ShopInt(root, "id"), token).ConfigureAwait(false),
                        "del_search" => await PriceDeleteSearchAsync(connection, root, token).ConfigureAwait(false),
                        _ => new StatusOnly(false)
                    };
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, PriceRowsMissing);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    public static Task<object> LoadUserModalAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        int customerId,
        string? domain,
        string? backend,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Forbidden"),
            (_, token) => UserModalAsync(connection, customerId, domain, backend, token),
            cancellationToken);

    public static async Task<object> ShopCsvUploadAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? fileName,
        byte[]? bytes,
        string docRoot,
        string prefix,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new CodedJson(403, new JsonObject { ["status"] = false, ["message"] = "forbidden" }), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        return await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new CodedJson(403, new JsonObject { ["status"] = false, ["message"] = "forbidden" }),
            (_, _) => Task.FromResult(ShopStoreCsv(fileName, bytes, docRoot, prefix)),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<object> CrossesHandleFileAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? importOptions,
        string docRoot,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new CodedJson(403, new JsonObject { ["status"] = false, ["message"] = "forbidden" }), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        return await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new CodedJson(403, new JsonObject { ["status"] = false, ["message"] = "forbidden" }),
            (_, token) => CrossImportAsync(connection, importOptions, docRoot, token),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<object> BulkCpAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Access denied"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        fields.TryGetValue("action", out var action);
        action ??= string.Empty;
        try
        {
            return await BulkActionAsync(connection, action, fields, PhpInt(adminUser), cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, CustomersMissing);
        }
        catch (InvalidOperationException ex)
        {
            return new FlagBody(false, ex.Message);
        }
    }

    public static async Task<object> PosEndpointAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> fields,
        bool isPost,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new FlagBody(false, "Access denied"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        fields.TryGetValue("action", out var action);
        action ??= string.Empty;
        if (!isPost || action.Length == 0)
        {
            return new FlagBody(false, "No action");
        }

        if (string.Equals(action, "complete_sale", StringComparison.Ordinal))
        {
            return new FlagBody(false, CompleteSaleNotPosted);
        }

        try
        {
            return await PosActionAsync(connection, action, fields, PhpInt(adminUser), cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, PriceRowsMissing);
        }
        catch (InvalidOperationException ex)
        {
            return new FlagBody(false, ex.Message);
        }
    }

    private static async Task EnsureMarketingSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_marketing_task_progress` (
              `strategy_key` VARCHAR(64) NOT NULL,
              `task_key` VARCHAR(128) NOT NULL,
              `is_done` TINYINT(1) NOT NULL DEFAULT 0,
              `done_at` INT UNSIGNED NULL DEFAULT NULL,
              `note` TEXT NULL,
              `updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
              PRIMARY KEY (`strategy_key`, `task_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_marketing_kpi_log` (
              `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
              `strategy_key` VARCHAR(64) NOT NULL DEFAULT '',
              `kpi_key` VARCHAR(128) NOT NULL,
              `value_decimal` DECIMAL(20,4) NULL DEFAULT NULL,
              `value_text` VARCHAR(512) NOT NULL DEFAULT '',
              `note` TEXT NULL,
              `recorded_at` INT UNSIGNED NOT NULL,
              `recorded_by` INT UNSIGNED NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              KEY `kpi_key` (`kpi_key`),
              KEY `recorded_at` (`recorded_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_marketing_reviews` (
              `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
              `strategy_key` VARCHAR(64) NOT NULL,
              `review_type` VARCHAR(32) NOT NULL DEFAULT 'weekly',
              `score` TINYINT UNSIGNED NOT NULL DEFAULT 0,
              `notes` TEXT NULL,
              `created_at` INT UNSIGNED NOT NULL,
              `created_by` INT UNSIGNED NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              KEY `strategy_key` (`strategy_key`),
              KEY `created_at` (`created_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> MarketingToggleAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, int userId, CancellationToken cancellationToken)
    {
        _ = userId;
        var strategy = ShopField(fields, "strategy_key");
        var task = ShopField(fields, "task_key");
        if (!MarketingTasks.TryGetValue(strategy, out var tasks) || Array.IndexOf(tasks, task) < 0)
        {
            throw new InvalidOperationException("Invalid task");
        }

        var done = ShopTruthy(ShopField(fields, "is_done"));
        var now = UnixNow();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_marketing_task_progress` (`strategy_key`, `task_key`, `is_done`, `done_at`, `updated_at`)
                VALUES (?, ?, ?, ?, ?)
                ON DUPLICATE KEY UPDATE `is_done` = VALUES(`is_done`), `done_at` = VALUES(`done_at`), `updated_at` = VALUES(`updated_at`)
                """),
            cancellationToken,
            strategy,
            task,
            done ? 1 : 0,
            done ? now : DBNull.Value,
            now).ConfigureAwait(false);
        var stats = await MarketingStatsAsync(connection, cancellationToken).ConfigureAwait(false);
        return new JsonObject
        {
            ["status"] = true,
            ["message"] = done ? "Task marked done" : "Task reopened",
            ["completion"] = stats
        };
    }

    private static async Task<object> MarketingKpiAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, int userId, CancellationToken cancellationToken)
    {
        var strategy = ShopField(fields, "strategy_key");
        var kpi = ShopField(fields, "kpi_key");
        if (!MarketingKpis.TryGetValue(strategy, out var kpis) || Array.IndexOf(kpis, kpi) < 0)
        {
            throw new InvalidOperationException("Invalid KPI");
        }

        var value = ShopField(fields, "value");
        var numeric = decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_marketing_kpi_log`
                (`strategy_key`, `kpi_key`, `value_decimal`, `value_text`, `note`, `recorded_at`, `recorded_by`)
                VALUES (?, ?, ?, ?, ?, ?, ?)
                """),
            cancellationToken,
            strategy,
            kpi,
            numeric ? parsed : DBNull.Value,
            numeric ? value : value.Trim(),
            ShopField(fields, "note").Trim(),
            UnixNow(),
            userId).ConfigureAwait(false);
        return new FlagBody(true, "KPI recorded");
    }

    private static async Task<object> MarketingReviewAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, int userId, CancellationToken cancellationToken)
    {
        var strategy = ShopField(fields, "strategy_key");
        if (!MarketingTasks.ContainsKey(strategy))
        {
            throw new InvalidOperationException("Invalid strategy");
        }

        var score = Math.Clamp(PhpInt(ShopField(fields, "score")), 0, 5);
        var type = ShopField(fields, "review_type");
        if (type.Length == 0)
        {
            type = "weekly";
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_marketing_reviews` (`strategy_key`, `review_type`, `score`, `notes`, `created_at`, `created_by`)
                VALUES (?, ?, ?, ?, ?, ?)
                """),
            cancellationToken,
            strategy,
            type,
            score,
            ShopField(fields, "notes").Trim(),
            UnixNow(),
            userId).ConfigureAwait(false);
        return new FlagBody(true, "Review saved");
    }

    private static async Task<object> MarketingSnapshotAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var stats = await MarketingStatsAsync(connection, cancellationToken).ConfigureAwait(false);
        var live = new JsonObject
        {
            ["orders_total"] = await ShopCountOrZeroAsync(connection, "SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1", cancellationToken).ConfigureAwait(false),
            ["users_total"] = await ShopCountOrZeroAsync(connection, "SELECT COUNT(*) FROM `users`", cancellationToken).ConfigureAwait(false),
            ["price_rows"] = await ShopCountOrZeroAsync(connection, "SELECT COUNT(*) FROM `shop_docpart_prices_data`", cancellationToken).ConfigureAwait(false),
            ["ga_property"] = "G-J19D1KHXCG"
        };
        var keys = new JsonArray();
        foreach (var key in MarketingTasks.Keys)
        {
            keys.Add(key);
        }

        return new JsonObject
        {
            ["status"] = true,
            ["data"] = new JsonObject
            {
                ["status"] = true,
                ["completion"] = stats,
                ["live"] = live,
                ["strategies"] = keys
            }
        };
    }

    private static async Task<JsonObject> MarketingStatsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var done = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `strategy_key`, `task_key` FROM `epc_marketing_task_progress` WHERE `is_done` = 1";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                done.Add(reader.GetString(0) + "\n" + reader.GetString(1));
            }
        }

        var total = 0;
        var finished = 0;
        var by = new JsonObject();
        foreach (var pair in MarketingTasks)
        {
            var stDone = 0;
            foreach (var task in pair.Value)
            {
                total++;
                if (done.Contains(pair.Key + "\n" + task))
                {
                    finished++;
                    stDone++;
                }
            }

            by[pair.Key] = new JsonObject
            {
                ["total"] = pair.Value.Length,
                ["done"] = stDone,
                ["pct"] = pair.Value.Length == 0 ? 0 : (int)Math.Round(100d * stDone / pair.Value.Length)
            };
        }

        return new JsonObject
        {
            ["total"] = total,
            ["done"] = finished,
            ["pct"] = total == 0 ? 0 : (int)Math.Round(100d * finished / total),
            ["by_strategy"] = by
        };
    }

    private static async Task<object> WorkshopActionAsync(DbConnection connection, string action, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case "seed_demo":
                var seeded = await WorkshopSeedAsync(connection, cancellationToken).ConfigureAwait(false);
                return new JsonObject { ["status"] = true, ["message"] = "Demo garage data ready", ["result"] = seeded };
            case "create_job":
                var id = await WorkshopInsertJobAsync(connection, fields, string.Empty, cancellationToken).ConfigureAwait(false);
                if (ShopField(fields, "labour_desc").Length > 0)
                {
                    await WorkshopAddLineAsync(connection, id, "labour", ShopField(fields, "labour_desc"), ShopDecimal(fields, "labour_hours", 1), ShopDecimal(fields, "labour_rate", 150), cancellationToken).ConfigureAwait(false);
                }

                if (ShopField(fields, "part_desc").Length > 0)
                {
                    await WorkshopAddLineAsync(connection, id, "part", ShopField(fields, "part_desc"), ShopDecimal(fields, "part_qty", 1), ShopDecimal(fields, "part_price", 0), cancellationToken).ConfigureAwait(false);
                }

                return new JsonObject { ["status"] = true, ["message"] = "Job created", ["job"] = await WorkshopJobAsync(connection, id, cancellationToken).ConfigureAwait(false) };
            case "set_status":
                var jobId = PhpInt(ShopField(fields, "job_id"));
                var status = ShopField(fields, "status");
                if (jobId <= 0 || !JobStatuses.ContainsKey(status))
                {
                    throw new InvalidOperationException("Invalid job or status");
                }

                var approved = status is "approved" or "in_progress" or "qc" or "ready" or "delivered";
                if (approved)
                {
                    await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_ws_jobs` SET `status`=?, `estimate_approved`=1, `time_updated`=? WHERE `id`=?"), cancellationToken, status, UnixNow(), jobId).ConfigureAwait(false);
                }
                else
                {
                    await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_ws_jobs` SET `status`=?, `time_updated`=? WHERE `id`=?"), cancellationToken, status, UnixNow(), jobId).ConfigureAwait(false);
                }

                return new JsonObject { ["status"] = true, ["message"] = "Status updated", ["job"] = await WorkshopJobAsync(connection, jobId, cancellationToken).ConfigureAwait(false) };
            case "assign":
                var assignId = PhpInt(ShopField(fields, "job_id"));
                if (assignId <= 0)
                {
                    throw new InvalidOperationException("Invalid job");
                }

                await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_ws_jobs` SET `bay_id`=?, `tech_id`=?, `time_updated`=? WHERE `id`=?"), cancellationToken, PhpInt(ShopField(fields, "bay_id")), PhpInt(ShopField(fields, "tech_id")), UnixNow(), assignId).ConfigureAwait(false);
                return new JsonObject { ["status"] = true, ["message"] = "Assignment saved", ["job"] = await WorkshopJobAsync(connection, assignId, cancellationToken).ConfigureAwait(false) };
            case "add_line":
                var lineJob = PhpInt(ShopField(fields, "job_id"));
                if (lineJob <= 0)
                {
                    throw new InvalidOperationException("Invalid job");
                }

                var lineId = await WorkshopAddLineAsync(connection, lineJob, ShopField(fields, "line_type"), ShopField(fields, "description"), ShopDecimal(fields, "qty", 1), ShopDecimal(fields, "unit_price", 0), cancellationToken).ConfigureAwait(false);
                return new JsonObject { ["status"] = true, ["message"] = "Line added", ["line_id"] = lineId, ["job"] = await WorkshopJobAsync(connection, lineJob, cancellationToken).ConfigureAwait(false) };
            case "get_job":
                var got = await WorkshopJobAsync(connection, PhpInt(ShopField(fields, "job_id")), cancellationToken).ConfigureAwait(false);
                if (got is null)
                {
                    throw new InvalidOperationException("Job not found");
                }

                return new JsonObject { ["status"] = true, ["job"] = got };
            case "list_jobs":
                return new JsonObject
                {
                    ["status"] = true,
                    ["jobs"] = await WorkshopListAsync(connection, ShopField(fields, "status"), cancellationToken).ConfigureAwait(false),
                    ["dashboard"] = await WorkshopDashboardAsync(connection, cancellationToken).ConfigureAwait(false)
                };
            case "save_bay":
                return await WorkshopSaveBayAsync(connection, fields, cancellationToken).ConfigureAwait(false);
            case "save_tech":
                return await WorkshopSaveTechAsync(connection, fields, cancellationToken).ConfigureAwait(false);
            case "create_appointment":
                var appointment = await WorkshopAppointmentAsync(connection, fields, cancellationToken).ConfigureAwait(false);
                return new JsonObject { ["status"] = true, ["message"] = "Appointment scheduled", ["id"] = appointment };
            case "convert_appointment":
                var converted = await WorkshopConvertAsync(connection, PhpInt(ShopField(fields, "appointment_id")), cancellationToken).ConfigureAwait(false);
                return new JsonObject { ["status"] = true, ["message"] = "Checked in", ["job"] = await WorkshopJobAsync(connection, converted, cancellationToken).ConfigureAwait(false) };
            case "list_appointments":
                return new JsonObject { ["status"] = true, ["appointments"] = await WorkshopAppointmentsAsync(connection, cancellationToken).ConfigureAwait(false) };
            default:
                throw new InvalidOperationException("Unknown action");
        }
    }

    private static async Task<int> WorkshopInsertJobAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, string jobNo, CancellationToken cancellationToken)
    {
        if (jobNo.Length == 0)
        {
            jobNo = ShopField(fields, "job_no");
        }

        if (jobNo.Length == 0)
        {
            var day = DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture);
            var count = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_ws_jobs` WHERE `job_no` LIKE ?"), cancellationToken, "WS-" + day + "-%").ConfigureAwait(false);
            jobNo = "WS-" + day + "-" + (count + 1).ToString("000", CultureInfo.InvariantCulture);
        }

        var status = ShopField(fields, "status");
        if (!JobStatuses.ContainsKey(status))
        {
            status = "checkin";
        }

        var now = UnixNow();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_ws_jobs`
                (`job_no`,`status`,`customer_name`,`customer_phone`,`customer_email`,`customer_id`,
                 `plate`,`vin`,`make`,`model`,`year`,`odometer`,`complaint`,`bay_id`,`tech_id`,
                 `estimate_approved`,`under_warranty`,`notes`,`time_promised`,`time_created`,`time_updated`)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                """),
            cancellationToken,
            jobNo,
            status,
            ShopField(fields, "customer_name").Trim(),
            ShopField(fields, "customer_phone").Trim(),
            ShopField(fields, "customer_email").Trim(),
            PhpInt(ShopField(fields, "customer_id")),
            ShopField(fields, "plate").Trim().ToUpperInvariant(),
            ShopField(fields, "vin").Trim().ToUpperInvariant(),
            ShopField(fields, "make").Trim(),
            ShopField(fields, "model").Trim(),
            ShopField(fields, "year").Trim(),
            PhpInt(ShopField(fields, "odometer")),
            ShopField(fields, "complaint").Trim(),
            PhpInt(ShopField(fields, "bay_id")),
            PhpInt(ShopField(fields, "tech_id")),
            ShopTruthy(ShopField(fields, "estimate_approved")) ? 1 : 0,
            ShopTruthy(ShopField(fields, "under_warranty")) ? 1 : 0,
            ShopField(fields, "notes").Trim(),
            PhpInt(ShopField(fields, "time_promised")),
            now,
            now).ConfigureAwait(false);
        return (int)await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> WorkshopAddLineAsync(DbConnection connection, int jobId, string lineType, string description, decimal qty, decimal price, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_ws_job_lines` (`job_id`,`line_type`,`description`,`item_id`,`qty`,`unit_price`,`tax_percent`,`chargeable`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            jobId,
            string.Equals(lineType, "labour", StringComparison.Ordinal) ? "labour" : "part",
            description.Trim(),
            0,
            qty,
            price,
            5,
            1).ConfigureAwait(false);
        var id = (int)await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await WorkshopRecalcAsync(connection, jobId, cancellationToken).ConfigureAwait(false);
        return id;
    }

    private static async Task WorkshopRecalcAsync(DbConnection connection, int jobId, CancellationToken cancellationToken)
    {
        decimal parts = 0;
        decimal labour = 0;
        decimal tax = 0;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `line_type`, `qty`, `unit_price`, `tax_percent`, `chargeable` FROM `epc_ws_job_lines` WHERE `job_id` = ?");
            ErpDb.AddParameters(command, jobId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var charge = reader.GetValue(4);
                var chargeable = charge is bool flag ? flag : Convert.ToInt32(charge, CultureInfo.InvariantCulture) == 1;
                if (!chargeable)
                {
                    continue;
                }

                var net = reader.GetDecimal(1) * reader.GetDecimal(2);
                tax += net * (reader.GetDecimal(3) / 100m);
                if (string.Equals(reader.GetString(0), "labour", StringComparison.Ordinal))
                {
                    labour += net;
                }
                else
                {
                    parts += net;
                }
            }
        }

        parts = Math.Round(parts, 2);
        labour = Math.Round(labour, 2);
        tax = Math.Round(tax, 2);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_ws_jobs` SET `parts_total`=?, `labour_total`=?, `tax_total`=?, `grand_total`=?, `time_updated`=? WHERE `id`=?"),
            cancellationToken,
            parts,
            labour,
            tax,
            parts + labour + tax,
            UnixNow(),
            jobId).ConfigureAwait(false);
    }

    private static async Task<JsonObject?> WorkshopJobAsync(DbConnection connection, int jobId, CancellationToken cancellationToken)
    {
        JsonObject? header = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("""
                SELECT j.`id`, j.`job_no`, j.`status`, j.`plate`, j.`customer_name`, j.`complaint`
                FROM `epc_ws_jobs` j WHERE j.`id` = ? LIMIT 1
                """);
            ErpDb.AddParameters(command, jobId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                header = new JsonObject
                {
                    ["id"] = reader.GetInt32(0),
                    ["job_no"] = reader.GetString(1),
                    ["status"] = reader.GetString(2),
                    ["plate"] = reader.GetString(3),
                    ["customer_name"] = reader.GetString(4),
                    ["complaint"] = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                };
            }
        }

        if (header is null)
        {
            return null;
        }

        var lines = new JsonArray();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `id`, `line_type`, `description` FROM `epc_ws_job_lines` WHERE `job_id` = ? ORDER BY `id` ASC");
            ErpDb.AddParameters(command, jobId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add(new JsonObject
                {
                    ["id"] = reader.GetInt32(0),
                    ["line_type"] = reader.GetString(1),
                    ["description"] = reader.GetString(2)
                });
            }
        }

        return new JsonObject { ["header"] = header, ["lines"] = lines };
    }

    private static async Task<JsonArray> WorkshopListAsync(DbConnection connection, string status, CancellationToken cancellationToken)
    {
        var rows = new JsonArray();
        await using var command = connection.CreateCommand();
        if (JobStatuses.ContainsKey(status))
        {
            command.CommandText = ErpDb.Positional("SELECT `id`, `job_no`, `status`, `plate` FROM `epc_ws_jobs` WHERE `status` = ? ORDER BY `id` DESC LIMIT 200");
            ErpDb.AddParameters(command, status);
        }
        else
        {
            command.CommandText = "SELECT `id`, `job_no`, `status`, `plate` FROM `epc_ws_jobs` ORDER BY FIELD(`status`,'checkin','estimate','approved','in_progress','qc','ready','delivered','cancelled'), `id` DESC LIMIT 200";
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new JsonObject
            {
                ["id"] = reader.GetInt32(0),
                ["job_no"] = reader.GetString(1),
                ["status"] = reader.GetString(2),
                ["plate"] = reader.GetString(3)
            });
        }

        return rows;
    }

    private static async Task<JsonObject> WorkshopDashboardAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var open = 0;
        var inProgress = 0;
        var ready = 0;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `status`, COUNT(*) FROM `epc_ws_jobs` GROUP BY `status`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = reader.GetString(0);
                var count = reader.GetInt32(1);
                if (name is not ("delivered" or "cancelled"))
                {
                    open += count;
                }

                if (name is "in_progress" or "qc")
                {
                    inProgress += count;
                }

                if (name == "ready")
                {
                    ready += count;
                }
            }
        }

        var dayStart = new DateTimeOffset(DateTime.Today).ToUnixTimeSeconds();
        var delivered = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_ws_jobs` WHERE `status`='delivered' AND `time_updated` >= ?"), cancellationToken, dayStart).ConfigureAwait(false);
        return new JsonObject { ["open"] = open, ["in_progress"] = inProgress, ["ready"] = ready, ["delivered_today"] = delivered };
    }

    private static async Task<object> WorkshopSaveBayAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var code = ShopField(fields, "code").Trim().ToUpperInvariant();
        var name = ShopField(fields, "name").Trim();
        if (code.Length == 0 || name.Length == 0)
        {
            throw new InvalidOperationException("Bay code and name required");
        }

        var id = PhpInt(ShopField(fields, "id"));
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_ws_bays` SET `code`=?, `name`=?, `active`=?, `sort_order`=? WHERE `id`=?"), cancellationToken, code, name, ShopTruthy(ShopField(fields, "active")) ? 1 : 0, PhpInt(ShopField(fields, "sort_order")), id).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("INSERT INTO `epc_ws_bays` (`code`,`name`,`active`,`sort_order`) VALUES (?,?,1,?)"), cancellationToken, code, name, PhpInt(ShopField(fields, "sort_order"))).ConfigureAwait(false);
            id = (int)await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }

        return new JsonObject { ["status"] = true, ["id"] = id };
    }

    private static async Task<object> WorkshopSaveTechAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var name = ShopField(fields, "name").Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException("Technician name required");
        }

        var id = PhpInt(ShopField(fields, "id"));
        if (id > 0)
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_ws_technicians` SET `name`=?, `phone`=?, `skill`=?, `active`=? WHERE `id`=?"), cancellationToken, name, ShopField(fields, "phone").Trim(), ShopField(fields, "skill").Trim(), ShopTruthy(ShopField(fields, "active")) ? 1 : 0, id).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("INSERT INTO `epc_ws_technicians` (`name`,`phone`,`skill`,`active`) VALUES (?,?,?,1)"), cancellationToken, name, ShopField(fields, "phone").Trim(), ShopField(fields, "skill").Trim()).ConfigureAwait(false);
            id = (int)await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }

        return new JsonObject { ["status"] = true, ["id"] = id };
    }

    private static async Task<int> WorkshopAppointmentAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var day = DateTime.Now.ToString("yyMMdd", CultureInfo.InvariantCulture);
        var count = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_ws_appointments` WHERE `ref_no` LIKE ?"), cancellationToken, "AP-" + day + "-%").ConfigureAwait(false);
        var reference = "AP-" + day + "-" + (count + 1).ToString("000", CultureInfo.InvariantCulture);
        var now = UnixNow();
        var slot = PhpInt(ShopField(fields, "time_slot"));
        if (slot <= 0)
        {
            slot = (int)(now + 86400);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_ws_appointments`
                (`ref_no`,`status`,`customer_name`,`customer_phone`,`customer_email`,`customer_id`,`garage_id`,
                 `plate`,`make`,`model`,`year`,`service_type`,`notes`,`time_slot`,`job_id`,`time_created`,`time_updated`)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,0,?,?)
                """),
            cancellationToken,
            reference,
            "scheduled",
            ShopField(fields, "customer_name").Trim(),
            ShopField(fields, "customer_phone").Trim(),
            ShopField(fields, "customer_email").Trim(),
            PhpInt(ShopField(fields, "customer_id")),
            PhpInt(ShopField(fields, "garage_id")),
            ShopField(fields, "plate").Trim().ToUpperInvariant(),
            ShopField(fields, "make").Trim(),
            ShopField(fields, "model").Trim(),
            ShopField(fields, "year").Trim(),
            ShopField(fields, "service_type").Length == 0 ? "General service" : ShopField(fields, "service_type").Trim(),
            ShopField(fields, "notes").Trim(),
            slot,
            now,
            now).ConfigureAwait(false);
        return (int)await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<JsonArray> WorkshopAppointmentsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var from = UnixNow() - 86400;
        var to = UnixNow() + (21 * 86400);
        var rows = new JsonArray();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `id`, `ref_no`, `status`, `plate` FROM `epc_ws_appointments` WHERE `time_slot` >= ? AND `time_slot` <= ? ORDER BY `time_slot` ASC LIMIT 80");
        ErpDb.AddParameters(command, from, to);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new JsonObject { ["id"] = reader.GetInt32(0), ["ref_no"] = reader.GetString(1), ["status"] = reader.GetString(2), ["plate"] = reader.GetString(3) });
        }

        return rows;
    }

    private static async Task<int> WorkshopConvertAsync(DbConnection connection, int appointmentId, CancellationToken cancellationToken)
    {
        string name = string.Empty;
        string phone = string.Empty;
        string email = string.Empty;
        string plate = string.Empty;
        string make = string.Empty;
        string model = string.Empty;
        string year = string.Empty;
        string service = string.Empty;
        string notes = string.Empty;
        string reference = string.Empty;
        var garage = 0;
        var existingJob = 0;
        var customer = 0;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `job_id`, `customer_name`, `customer_phone`, `customer_email`, `customer_id`, `garage_id`, `plate`, `make`, `model`, `year`, `service_type`, `notes`, `ref_no` FROM `epc_ws_appointments` WHERE `id` = ? LIMIT 1");
            ErpDb.AddParameters(command, appointmentId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Appointment not found");
            }

            existingJob = reader.GetInt32(0);
            name = reader.GetString(1);
            phone = reader.GetString(2);
            email = reader.GetString(3);
            customer = reader.GetInt32(4);
            garage = reader.GetInt32(5);
            plate = reader.GetString(6);
            make = reader.GetString(7);
            model = reader.GetString(8);
            year = reader.GetString(9);
            service = reader.IsDBNull(10) ? string.Empty : reader.GetString(10);
            notes = reader.IsDBNull(11) ? string.Empty : reader.GetString(11);
            reference = reader.GetString(12);
        }

        if (existingJob > 0)
        {
            return existingJob;
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["status"] = "checkin",
            ["customer_name"] = name,
            ["customer_phone"] = phone,
            ["customer_email"] = email,
            ["customer_id"] = customer.ToString(CultureInfo.InvariantCulture),
            ["plate"] = plate,
            ["make"] = make,
            ["model"] = model,
            ["year"] = year,
            ["complaint"] = (service + " — " + notes).Trim(),
            ["notes"] = "From appointment " + reference
        };
        var jobId = await WorkshopInsertJobAsync(connection, fields, string.Empty, cancellationToken).ConfigureAwait(false);
        var now = UnixNow();
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_ws_jobs` SET `garage_id`=?, `appointment_id`=?, `time_updated`=? WHERE `id`=?"), cancellationToken, garage, appointmentId, now, jobId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_ws_appointments` SET `status`='converted', `job_id`=?, `time_updated`=? WHERE `id`=?"), cancellationToken, jobId, now, appointmentId).ConfigureAwait(false);
        return jobId;
    }

    private static async Task<JsonObject> WorkshopSeedAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var bays = new (string Code, string Name, int Sort)[]
        {
            ("B1", "Bay 1 — Quick service", 1),
            ("B2", "Bay 2 — Mechanical", 2),
            ("B3", "Bay 3 — Diagnostic", 3)
        };
        var bayIds = new List<int>();
        foreach (var bay in bays)
        {
            var id = (int)await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT id FROM `epc_ws_bays` WHERE `code` = ? LIMIT 1"), cancellationToken, bay.Code).ConfigureAwait(false);
            if (id <= 0)
            {
                await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("INSERT INTO `epc_ws_bays` (`code`,`name`,`active`,`sort_order`) VALUES (?,?,1,?)"), cancellationToken, bay.Code, bay.Name, bay.Sort).ConfigureAwait(false);
                id = (int)await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            }

            bayIds.Add(id);
        }

        var techs = new (string Name, string Phone, string Skill)[]
        {
            ("Ahmed Hassan", "+971501112233", "General / brakes"),
            ("Rajesh Kumar", "+971502223344", "Electrical / AC"),
            ("Omar Al Mansoori", "+971503334455", "Diagnostics")
        };
        var techIds = new List<int>();
        foreach (var tech in techs)
        {
            var id = (int)await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT id FROM `epc_ws_technicians` WHERE `name` = ? LIMIT 1"), cancellationToken, tech.Name).ConfigureAwait(false);
            if (id <= 0)
            {
                await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("INSERT INTO `epc_ws_technicians` (`name`,`phone`,`skill`,`active`) VALUES (?,?,?,1)"), cancellationToken, tech.Name, tech.Phone, tech.Skill).ConfigureAwait(false);
                id = (int)await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            }

            techIds.Add(id);
        }

        var created = 0;
        created += await WorkshopSeedJobAsync(connection, "WS-DEMO-001", "in_progress", "Fatima Al Zaabi", "+971567607011", "fatima.demo@example.com", "D-12345", "WVWZZZ3CZWE123456", "Toyota", "Land Cruiser", "2021", "68420", "Front brake noise + oil service due", bayIds[1], techIds[0], true, cancellationToken).ConfigureAwait(false);
        created += await WorkshopSeedJobAsync(connection, "WS-DEMO-002", "estimate", "Gulf Fleet Services LLC", "+97144556677", "fleet@example.ae", "DXB-88901", "JN1TANR35U0123456", "Nissan", "Patrol", "2019", "112300", "AC not cooling; intermittent compressor cut-out", bayIds[2], techIds[1], false, cancellationToken).ConfigureAwait(false);
        created += await WorkshopSeedJobAsync(connection, "WS-DEMO-003", "ready", "John Peters", "+971552223344", "john.demo@example.com", "A-7788", string.Empty, "BMW", "X5", "2020", "45110", "Battery warning light; weak start", bayIds[0], techIds[2], true, cancellationToken).ConfigureAwait(false);
        created += await WorkshopSeedJobAsync(connection, "WS-DEMO-004", "checkin", "Sara Khan", "+971501234567", string.Empty, "SHJ-4421", string.Empty, "Honda", "CR-V", "2018", "98000", "Annual service + tyre rotation", 0, 0, false, cancellationToken).ConfigureAwait(false);
        await WorkshopSeedLabourAsync(connection, cancellationToken).ConfigureAwait(false);
        return new JsonObject { ["bays"] = bayIds.Count, ["techs"] = techIds.Count, ["jobs"] = created };
    }

    private static async Task<int> WorkshopSeedJobAsync(
        DbConnection connection,
        string jobNo,
        string status,
        string name,
        string phone,
        string email,
        string plate,
        string vin,
        string make,
        string model,
        string year,
        string odometer,
        string complaint,
        int bayId,
        int techId,
        bool approved,
        CancellationToken cancellationToken)
    {
        var existing = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT id FROM `epc_ws_jobs` WHERE `job_no` = ? LIMIT 1"), cancellationToken, jobNo).ConfigureAwait(false);
        if (existing > 0)
        {
            return 0;
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["job_no"] = jobNo,
            ["status"] = status,
            ["customer_name"] = name,
            ["customer_phone"] = phone,
            ["customer_email"] = email,
            ["plate"] = plate,
            ["vin"] = vin,
            ["make"] = make,
            ["model"] = model,
            ["year"] = year,
            ["odometer"] = odometer,
            ["complaint"] = complaint,
            ["bay_id"] = bayId.ToString(CultureInfo.InvariantCulture),
            ["tech_id"] = techId.ToString(CultureInfo.InvariantCulture),
            ["estimate_approved"] = approved ? "1" : string.Empty
        };
        var id = await WorkshopInsertJobAsync(connection, fields, jobNo, cancellationToken).ConfigureAwait(false);
        if (jobNo == "WS-DEMO-001")
        {
            await WorkshopAddLineAsync(connection, id, "labour", "Brake pads replace (front)", 1.5m, 180, cancellationToken).ConfigureAwait(false);
            await WorkshopAddLineAsync(connection, id, "part", "Brake pad set — OEM", 1, 420, cancellationToken).ConfigureAwait(false);
            await WorkshopAddLineAsync(connection, id, "labour", "Engine oil & filter service", 0.8m, 150, cancellationToken).ConfigureAwait(false);
            await WorkshopAddLineAsync(connection, id, "part", "0W-20 oil 6L + filter", 1, 210, cancellationToken).ConfigureAwait(false);
        }
        else if (jobNo == "WS-DEMO-002")
        {
            await WorkshopAddLineAsync(connection, id, "labour", "AC diagnose + pressure test", 1, 200, cancellationToken).ConfigureAwait(false);
            await WorkshopAddLineAsync(connection, id, "part", "AC compressor (estimate)", 1, 1850, cancellationToken).ConfigureAwait(false);
        }
        else if (jobNo == "WS-DEMO-003")
        {
            await WorkshopAddLineAsync(connection, id, "labour", "Battery test & replace", 0.5m, 120, cancellationToken).ConfigureAwait(false);
            await WorkshopAddLineAsync(connection, id, "part", "AGM battery 95Ah", 1, 680, cancellationToken).ConfigureAwait(false);
        }

        return 1;
    }

    private static async Task WorkshopSeedLabourAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var ops = new (string Code, string Name, decimal Hours, decimal Rate)[]
        {
            ("OIL-SVC", "Engine oil & filter service", 0.8m, 150),
            ("BRAKE-F", "Front brake pads replace", 1.5m, 180),
            ("BRAKE-R", "Rear brake pads replace", 1.2m, 180),
            ("DIAG", "Computer diagnosis", 1, 200),
            ("AC-SVC", "AC diagnose & recharge", 1.5m, 200),
            ("BATTERY", "Battery test & replace", 0.5m, 120),
            ("TYRE-ROT", "Tyre rotation & balance", 0.8m, 100),
            ("ANNUAL", "Annual multi-point service", 2.5m, 160)
        };
        foreach (var op in ops)
        {
            var id = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT id FROM `epc_ws_labour_ops` WHERE `code` = ? LIMIT 1"), cancellationToken, op.Code).ConfigureAwait(false);
            if (id <= 0)
            {
                await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("INSERT INTO `epc_ws_labour_ops` (`code`,`name`,`hours`,`rate`,`active`) VALUES (?,?,?,?,1)"), cancellationToken, op.Code, op.Name, op.Hours, op.Rate).ConfigureAwait(false);
            }
        }
    }

    private static async Task<object> CrossTableAsync(DbConnection connection, JsonObject root, string? sortCookie, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, ShopInt(root, "page"));
        const int limit = 30;
        var offset = (page * limit) - limit;
        var article = ShopSweep(ShopText(root, "article"));
        var manufacturer = ShopText(root, "manufacturer").Trim().ToUpperInvariant();
        var where = new StringBuilder();
        var args = new List<object?>();
        if (article.Length > 0)
        {
            if (manufacturer.Length > 0)
            {
                where.Append(" (`article` = ? AND `manufacturer_article` = ?) OR (`analog` = ? AND `manufacturer_analog` = ?) ");
                args.AddRange([article, manufacturer, article, manufacturer]);
            }
            else
            {
                where.Append(" (`article` = ?) OR (`analog` = ?) ");
                args.AddRange([article, article]);
            }
        }

        var filtered = where.Length > 0;
        var sql = filtered
            ? "SELECT `id`, `article`, `manufacturer_article`, `analog`, `manufacturer_analog` FROM `shop_docpart_articles_analogs_list` WHERE " + where + " ORDER BY `id` DESC LIMIT " + offset.ToString(CultureInfo.InvariantCulture) + ", " + limit.ToString(CultureInfo.InvariantCulture)
            : "SELECT `id`, `article`, `manufacturer_article`, `analog`, `manufacturer_analog` FROM `shop_docpart_articles_analogs_list` ORDER BY `id` DESC LIMIT " + offset.ToString(CultureInfo.InvariantCulture) + ", " + limit.ToString(CultureInfo.InvariantCulture);
        _ = sortCookie;
        var html = new StringBuilder();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = filtered ? ErpDb.Positional(sql) : sql;
            if (filtered)
            {
                ErpDb.AddParameters(command, args.ToArray());
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                html.Append("<tr id=\"show_line_").Append(reader.GetInt32(0)).Append("\"><td>").Append(WebUtility.HtmlEncode(reader.GetString(1))).Append("</td><td>").Append(WebUtility.HtmlEncode(reader.GetString(2))).Append("</td><td>").Append(WebUtility.HtmlEncode(reader.GetString(3))).Append("</td><td>").Append(WebUtility.HtmlEncode(reader.GetString(4))).Append("</td></tr>");
            }
        }

        var banner = filtered ? "Filtered" : "Browsing latest links";
        var body = html.Length == 0
            ? "<div class=\"panel-body\">" + banner + "<p class=\"text-muted\">No records</p></div>"
            : "<div class=\"panel-body\">" + banner + "<table class=\"table_crosses\"><tbody>" + html + "</tbody></table></div>";
        return new RawHttp(body, "text/html; charset=utf-8");
    }

    private static async Task<object> CrossAddAsync(DbConnection connection, JsonObject root, CancellationToken cancellationToken)
    {
        var article = ShopSweep(ShopText(root, "article"));
        var analog = ShopSweep(ShopText(root, "analog"));
        var brand = ShopBrand(ShopText(root, "manufacturer_article"));
        var analogBrand = ShopBrand(ShopText(root, "manufacturer_analog"));
        var inserted = await CrossPersistBidirectionalAsync(connection, article, brand, analog, analogBrand, cancellationToken).ConfigureAwait(false);
        return new JsonObject { ["status"] = inserted > 0 };
    }

    private static async Task<object> CrossSaveAsync(DbConnection connection, JsonObject root, CancellationToken cancellationToken)
    {
        var article = ShopSweep(ShopText(root, "article"));
        var analog = ShopSweep(ShopText(root, "analog"));
        var brand = ShopBrand(ShopText(root, "manufacturer_article"));
        var analogBrand = ShopBrand(ShopText(root, "manufacturer_analog"));
        if (article.Length == 0 || analog.Length == 0 || brand.Length == 0 || analogBrand.Length == 0)
        {
            return new JsonObject { ["status"] = false };
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_articles_analogs_list` SET `article` = ?, `manufacturer_article` = ?, `analog` = ?, `manufacturer_analog` = ? WHERE `id` = ?"),
            cancellationToken,
            article,
            brand,
            analog,
            analogBrand,
            ShopInt(root, "id")).ConfigureAwait(false);
        return new JsonObject { ["status"] = true };
    }

    private static async Task<object> CrossDeleteAsync(DbConnection connection, int id, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `shop_docpart_articles_analogs_list` WHERE `id` = ?"), cancellationToken, id).ConfigureAwait(false);
        return new JsonObject { ["status"] = true };
    }

    private static async Task<object> CrossDeleteSearchAsync(DbConnection connection, JsonObject root, CancellationToken cancellationToken)
    {
        var article = ShopSweep(ShopText(root, "article"));
        if (article.Length == 0 && ShopInt(root, "null") != 1 && ShopInt(root, "id_from") <= 0 && ShopInt(root, "id_before") <= 0)
        {
            return new FlagBody(false, CrossDeleteNeedsFilter);
        }

        var manufacturer = ShopText(root, "manufacturer").Trim().ToUpperInvariant();
        string sql;
        object?[] args;
        if (article.Length > 0 && manufacturer.Length > 0)
        {
            sql = "DELETE FROM `shop_docpart_articles_analogs_list` WHERE (`article` = ? AND `manufacturer_article` = ?) OR (`analog` = ? AND `manufacturer_analog` = ?) LIMIT 50000";
            args = [article, manufacturer, article, manufacturer];
        }
        else if (article.Length > 0)
        {
            sql = "DELETE FROM `shop_docpart_articles_analogs_list` WHERE (`article` = ?) OR (`analog` = ?) LIMIT 50000";
            args = [article, article];
        }
        else
        {
            return new FlagBody(false, CrossDeleteNeedsFilter);
        }

        int removed;
        do
        {
            removed = await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional(sql), cancellationToken, args).ConfigureAwait(false);
        }
        while (removed > 0);

        return new JsonObject { ["status"] = true };
    }

    private static async Task<object> CrossManufacturersAsync(DbConnection connection, JsonObject root, CancellationToken cancellationToken)
    {
        var article = ShopSweep(ShopText(root, "article"));
        var names = new SortedSet<string>(StringComparer.Ordinal);
        await ShopCollectAsync(connection, "SELECT `manufacturer_article` FROM `shop_docpart_articles_analogs_list` WHERE `article` = ?", article, names, cancellationToken).ConfigureAwait(false);
        await ShopCollectAsync(connection, "SELECT `manufacturer_analog` FROM `shop_docpart_articles_analogs_list` WHERE `analog` = ?", article, names, cancellationToken).ConfigureAwait(false);
        return new JsonObject
        {
            ["status"] = true,
            ["list_manufacturer"] = JsonSerializer.Serialize(names)
        };
    }

    private static async Task<int> CrossPersistBidirectionalAsync(DbConnection connection, string article, string brand, string analog, string analogBrand, CancellationToken cancellationToken)
    {
        var inserted = 0;
        if (await CrossPersistAsync(connection, article, brand, analog, analogBrand, cancellationToken).ConfigureAwait(false))
        {
            inserted++;
        }

        if (await CrossPersistAsync(connection, analog, analogBrand, article, brand, cancellationToken).ConfigureAwait(false))
        {
            inserted++;
        }

        return inserted;
    }

    private static async Task<bool> CrossPersistAsync(DbConnection connection, string article, string brand, string analog, string analogBrand, CancellationToken cancellationToken)
    {
        article = article.Trim();
        analog = analog.Trim();
        brand = ShopBrand(brand);
        analogBrand = ShopBrand(analogBrand);
        if (article.Length == 0 || analog.Length == 0 || brand.Length == 0 || analogBrand.Length == 0)
        {
            return false;
        }

        if (string.Equals(article, analog, StringComparison.Ordinal) && string.Equals(brand, analogBrand, StringComparison.Ordinal))
        {
            return false;
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("""
                SELECT `id` FROM `shop_docpart_articles_analogs_list`
                WHERE (`article` = ? AND `analog` = ? AND UPPER(TRIM(`manufacturer_article`)) = ? AND UPPER(TRIM(`manufacturer_analog`)) = ?)
                   OR (`article` = ? AND `analog` = ? AND UPPER(TRIM(`manufacturer_article`)) = ? AND UPPER(TRIM(`manufacturer_analog`)) = ?)
                LIMIT 1
                """),
            cancellationToken,
            article,
            analog,
            brand,
            analogBrand,
            analog,
            article,
            analogBrand,
            brand).ConfigureAwait(false);
        if (existing > 0)
        {
            return false;
        }

        var search = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shop_docpart_articles_analogs_list' AND COLUMN_NAME = 'article_search'", cancellationToken).ConfigureAwait(false);
        if (search > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_docpart_articles_analogs_list` (`article`, `article_search`, `manufacturer_article`, `analog`, `analog_search`, `manufacturer_analog`) VALUES (?,?,?,?,?,?)"),
                cancellationToken,
                article,
                article,
                brand,
                analog,
                analog,
                analogBrand).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_docpart_articles_analogs_list` (`article`, `manufacturer_article`, `analog`, `manufacturer_analog`) VALUES (?,?,?,?)"),
                cancellationToken,
                article,
                brand,
                analog,
                analogBrand).ConfigureAwait(false);
        }

        return true;
    }

    private static async Task<object> PriceTableAsync(DbConnection connection, JsonObject root, CancellationToken cancellationToken)
    {
        var html = new StringBuilder();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`, `article`, `manufacturer`, `price` FROM `shop_docpart_prices_data` ORDER BY `article` LIMIT 20";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                html.Append("<tr id=\"show_line_").Append(reader.GetInt32(0)).Append("\"><td class=\"bgtd\">").Append(WebUtility.HtmlEncode(reader.GetString(1))).Append("</td><td>").Append(WebUtility.HtmlEncode(reader.GetString(2))).Append("</td><td>").Append(WebUtility.HtmlEncode(Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty)).Append("</td></tr>");
            }
        }

        _ = root;
        var body = html.Length == 0
            ? "<div class=\"panel-body\">Nothing found</div>"
            : "<div class=\"panel-body\"><table class=\"epc-prices-table\"><tbody>" + html + "</tbody></table></div>";
        return new RawHttp(body, "text/html; charset=utf-8");
    }

    private static async Task<object> PriceWriteAsync(DbConnection connection, JsonObject root, int id, CancellationToken cancellationToken)
    {
        var article = ShopArticle(ShopText(root, "article"));
        var manufacturer = WebUtility.HtmlEncode(ShopText(root, "manufacturer").Trim().ToUpperInvariant());
        var price = ShopMoney(ShopText(root, "price"));
        var name = WebUtility.HtmlEncode(ShopText(root, "name").Replace("\"", string.Empty, StringComparison.Ordinal).Replace("\\", string.Empty, StringComparison.Ordinal).Replace("'", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\t", string.Empty, StringComparison.Ordinal));
        var exist = ShopDigits(ShopText(root, "exist"));
        var lead = PhpInt(ShopText(root, "time_to_exe"));
        var storage = WebUtility.HtmlEncode(ShopText(root, "storage").Trim());
        var minOrder = PhpInt(ShopText(root, "min_order"));
        var priceId = ShopInt(root, "price_id");
        if (article.Length == 0 || manufacturer.Length == 0 || (id == 0 && price.Length == 0) || (id > 0 && priceId == 0))
        {
            return new StatusOnly(false);
        }

        if (id == 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_docpart_prices_data` (`price_id`, `manufacturer`, `article`, `article_show`, `name`, `exist`, `price`, `time_to_exe`, `storage`, `min_order`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
                cancellationToken,
                priceId,
                manufacturer,
                article,
                article,
                name,
                exist,
                price,
                lead,
                storage,
                minOrder).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_docpart_prices_data` SET `price_id`=?,`manufacturer`=?,`article`=?,`article_show`=?,`name`=?,`exist`=?,`price`=?,`time_to_exe`=?,`storage`=?,`min_order`=? WHERE `id` = ?"),
                cancellationToken,
                priceId,
                manufacturer,
                article,
                article,
                name,
                exist,
                price,
                lead,
                storage,
                minOrder,
                id).ConfigureAwait(false);
        }

        return new StatusOnly(true);
    }

    private static async Task<object> PriceDeleteAsync(DbConnection connection, int id, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `shop_docpart_prices_data` WHERE `id` = ? LIMIT 1"), cancellationToken, id).ConfigureAwait(false);
        return new StatusOnly(true);
    }

    private static Task<object> PriceDeleteSearchAsync(DbConnection connection, JsonObject root, CancellationToken cancellationToken)
    {
        _ = connection;
        _ = cancellationToken;
        var where = root["where_object"] as JsonObject;
        var hasFilter = where is not null && (ShopInt(where, "price_id") > 0 || ShopText(where, "article").Length > 0 || ShopText(where, "manufacturer").Length > 0 || ShopText(where, "search_text").Length > 0);
        if (!hasFilter)
        {
            return Task.FromResult<object>(new FlagBody(false, PriceDeleteNeedsFilter));
        }

        return PriceDeleteFilteredAsync(connection, where!, cancellationToken);
    }

    private static async Task<object> PriceDeleteFilteredAsync(DbConnection connection, JsonObject where, CancellationToken cancellationToken)
    {
        var clauses = new List<string>();
        var args = new List<object?>();
        if (ShopInt(where, "price_id") > 0)
        {
            clauses.Add("`price_id` = ?");
            args.Add(ShopInt(where, "price_id"));
        }

        var article = ShopArticle(ShopText(where, "article"));
        if (article.Length > 0)
        {
            clauses.Add("`article` LIKE ?");
            args.Add(article);
        }

        if (clauses.Count == 0)
        {
            return new FlagBody(false, PriceDeleteNeedsFilter);
        }

        int removed;
        do
        {
            removed = await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `shop_docpart_prices_data` WHERE " + string.Join(" AND ", clauses) + " LIMIT 10000"), cancellationToken, args.ToArray()).ConfigureAwait(false);
        }
        while (removed > 0);

        return new StatusOnly(true);
    }

    private static async Task<object> UserModalAsync(DbConnection connection, int customerId, string? domain, string? backend, CancellationToken cancellationToken)
    {
        decimal balance;
        try
        {
            var raw = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT CAST(IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = ? AND `income`=1 AND `active` = 1), 0) - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = ? AND `income`=0 AND `active` = 1), 0) AS CHAR)"),
                cancellationToken,
                customerId,
                customerId).ConfigureAwait(false);
            balance = decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, CustomerAccountingMissing);
        }

        var email = await ShopOptionalStringAsync(connection, "SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1", cancellationToken, customerId).ConfigureAwait(false);
        var phone = await ShopOptionalStringAsync(connection, "SELECT `phone` FROM `users` WHERE `user_id` = ? LIMIT 1", cancellationToken, customerId).ConfigureAwait(false);
        var group = await ShopOptionalStringAsync(connection, "SELECT g.`value` FROM `users_groups_bind` b INNER JOIN `groups` g ON g.`id` = b.`group_id` WHERE b.`user_id` = ? LIMIT 1", cancellationToken, customerId).ConfigureAwait(false);
        var root = string.IsNullOrWhiteSpace(domain) ? "http://local.test/" : domain;
        if (!root.EndsWith('/'))
        {
            root += "/";
        }

        var panel = string.IsNullOrWhiteSpace(backend) ? "cp" : backend.Trim('/');
        var formatted = balance.ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ", StringComparison.Ordinal);
        var html = "<div class=\"customer-modal-info-block\"><div class=\"customer-modal-info-block-header\"><div>5579</div><i class=\"far fa-window-close\" id=\"close-customer-modal-info-" + customerId.ToString(CultureInfo.InvariantCulture) + "\"></i></div><div class=\"info-title\">ID:</div><div class=\"info-value\">" + customerId.ToString(CultureInfo.InvariantCulture) + "</div><div>3664</div><div class=\"info-value\">" + WebUtility.HtmlEncode(group) + "</div><div>Email:</div><div class=\"info-value\">" + WebUtility.HtmlEncode(email) + "</div><div>1312</div><div class=\"info-value\">" + WebUtility.HtmlEncode(phone) + "</div><div>4655</div><div class=\"info-value\">" + formatted + "</div><a href=\"" + WebUtility.HtmlEncode(root + panel) + "/users/usermanager/user?user_id=" + customerId.ToString(CultureInfo.InvariantCulture) + "\">5580</a></div>";
        return new JsonObject { ["modal"] = html, ["status"] = true };
    }

    private static object ShopStoreCsv(string? fileName, byte[]? bytes, string docRoot, string prefix)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return new JsonObject { ["status"] = false, ["message"] = "No file" };
        }

        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject { ["status"] = false, ["message"] = "Use .csv files only" };
        }

        var tempRoot = Path.GetFullPath(Path.GetTempPath());
        var root = Path.GetFullPath(string.IsNullOrWhiteSpace(docRoot) ? tempRoot : docRoot);
        if (!root.StartsWith(tempRoot, StringComparison.Ordinal))
        {
            return new JsonObject { ["status"] = false, ["message"] = "Could not upload file" };
        }

        var directory = Path.Combine(root, "cp", "tmp");
        Directory.CreateDirectory(directory);
        var safe = RegexSafe(Path.GetFileName(fileName));
        var path = Path.Combine(directory, prefix + DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + "_" + safe);
        File.WriteAllBytes(path, bytes ?? []);
        return new JsonObject { ["status"] = true, ["file_full_path"] = path };
    }

    private static async Task<object> CrossImportAsync(DbConnection connection, string? importOptions, string docRoot, CancellationToken cancellationToken)
    {
        if (!ShopJson(importOptions, out var options) || ShopText(options, "file_full_path").Length == 0)
        {
            return new JsonObject { ["status"] = false, ["message"] = "bad_request" };
        }

        var requested = ShopText(options, "file_full_path");
        var tmpRoot = Path.GetFullPath(Path.Combine(string.IsNullOrWhiteSpace(docRoot) ? Path.GetTempPath() : docRoot, "cp", "tmp"));
        string full;
        try
        {
            full = Path.GetFullPath(requested);
        }
        catch (Exception)
        {
            return new JsonObject { ["status"] = false, ["message"] = "Invalid file path" };
        }

        var rootWithSep = tmpRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSep, StringComparison.Ordinal) || !File.Exists(full))
        {
            return new JsonObject { ["status"] = false, ["message"] = "Invalid file path" };
        }

        var text = await File.ReadAllTextAsync(full, cancellationToken).ConfigureAwait(false);
        if (text.Length == 0)
        {
            File.Delete(full);
            return new JsonObject { ["status"] = false, ["message"] = "Empty CSV" };
        }

        var first = text.Split('\n', 2)[0];
        var delimiter = first.Count(ch => ch == ';') >= first.Count(ch => ch == ',') ? ';' : ',';
        var inserted = 0;
        var skipped = 0;
        var errors = 0;
        var rowNum = 0;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            rowNum++;
            if (rowNum > 50000)
            {
                break;
            }

            var cols = line.Split(delimiter);
            if (cols.Length < 4)
            {
                skipped++;
                continue;
            }

            var probe = (cols[0] + cols[1] + cols[2] + cols[3]).ToLowerInvariant();
            if (rowNum == 1 && (probe.Contains("manufacturer", StringComparison.Ordinal) || probe.Contains("article", StringComparison.Ordinal)))
            {
                continue;
            }

            var brand = ShopBrand(cols[0]);
            var article = ShopSweep(cols[1]);
            var analogBrand = ShopBrand(cols[2]);
            var analog = ShopSweep(cols[3]);
            if (article.Length == 0 || analog.Length == 0 || brand.Length == 0 || analogBrand.Length == 0)
            {
                skipped++;
                continue;
            }

            try
            {
                var count = await CrossPersistBidirectionalAsync(connection, article, brand, analog, analogBrand, cancellationToken).ConfigureAwait(false);
                if (count > 0)
                {
                    inserted += count;
                }
                else
                {
                    skipped++;
                }
            }
            catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
            {
                errors++;
            }
        }

        File.Delete(full);
        return new JsonObject
        {
            ["status"] = true,
            ["message"] = "Imported " + inserted.ToString(CultureInfo.InvariantCulture) + " link(s); skipped " + skipped.ToString(CultureInfo.InvariantCulture) + "; errors " + errors.ToString(CultureInfo.InvariantCulture),
            ["inserted"] = inserted,
            ["skipped"] = skipped,
            ["errors"] = errors
        };
    }

    private static async Task EnsureBulkHistoryAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_bulk_upload_history` (
              `id` INT(11) NOT NULL AUTO_INCREMENT,
              `user_id` INT(11) NOT NULL DEFAULT 0,
              `created_by_admin` TINYINT(1) NOT NULL DEFAULT 0,
              `group_id` INT(11) NOT NULL DEFAULT 0,
              `file_name` VARCHAR(255) NOT NULL DEFAULT '',
              `priority` VARCHAR(20) NOT NULL DEFAULT 'price',
              `source` VARCHAR(32) NOT NULL DEFAULT 'storefront',
              `uploaded_count` INT(11) NOT NULL DEFAULT 0,
              `available_count` INT(11) NOT NULL DEFAULT 0,
              `cross_count` INT(11) NOT NULL DEFAULT 0,
              `short_count` INT(11) NOT NULL DEFAULT 0,
              `notfound_count` INT(11) NOT NULL DEFAULT 0,
              `result_json` LONGTEXT NULL,
              `csv_result` LONGTEXT NULL,
              `cp_reviewed_at` DATETIME NULL,
              `cp_reviewed_by` INT(11) NOT NULL DEFAULT 0,
              `cp_notes` VARCHAR(512) NOT NULL DEFAULT '',
              `shop_quote_id` INT(11) NOT NULL DEFAULT 0,
              `crm_quote_id` INT(11) NOT NULL DEFAULT 0,
              `cart_added_count` INT(11) NOT NULL DEFAULT 0,
              `created_at` DATETIME NOT NULL,
              `updated_at` DATETIME NOT NULL,
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object> BulkActionAsync(DbConnection connection, string action, IReadOnlyDictionary<string, string> fields, int adminId, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case "dashboard":
                await EnsureBulkHistoryAsync(connection, cancellationToken).ConfigureAwait(false);
                var total = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_bulk_upload_history`", cancellationToken).ConfigureAwait(false);
                var unreviewed = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_bulk_upload_history` WHERE `cp_reviewed_at` IS NULL", cancellationToken).ConfigureAwait(false);
                return new JsonObject { ["status"] = true, ["dashboard"] = new JsonObject { ["total"] = total, ["unreviewed"] = unreviewed } };
            case "list_history":
                await EnsureBulkHistoryAsync(connection, cancellationToken).ConfigureAwait(false);
                return new JsonObject { ["status"] = true, ["rows"] = await BulkRowsAsync(connection, cancellationToken).ConfigureAwait(false) };
            case "get_upload":
                await EnsureBulkHistoryAsync(connection, cancellationToken).ConfigureAwait(false);
                var upload = await BulkOneAsync(connection, PhpInt(ShopField(fields, "upload_id")), cancellationToken).ConfigureAwait(false);
                return upload is null ? new FlagBody(false, "Upload not found") : new JsonObject { ["status"] = true, ["upload"] = upload };
            case "search_customers":
                return await BulkCustomersAsync(connection, ShopField(fields, "q"), cancellationToken).ConfigureAwait(false);
            case "mark_reviewed":
                await EnsureBulkHistoryAsync(connection, cancellationToken).ConfigureAwait(false);
                var marked = await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_bulk_upload_history` SET `cp_reviewed_at` = NOW(), `cp_reviewed_by` = ?, `cp_notes` = ?, `updated_at` = NOW() WHERE `id` = ? LIMIT 1"), cancellationToken, adminId, ShopField(fields, "notes").Trim(), PhpInt(ShopField(fields, "upload_id"))).ConfigureAwait(false);
                return new JsonObject { ["status"] = marked > 0, ["message"] = marked > 0 ? "Marked reviewed" : "Update failed" };
            case "process_upload":
                if (PhpInt(ShopField(fields, "customer_user_id")) <= 0)
                {
                    return new FlagBody(false, "Select a customer first");
                }

                var group = PhpInt(ShopField(fields, "group_id"));
                if (group <= 0)
                {
                    return new FlagBody(false, "Customer has no price group — pick a price profile");
                }

                return new FlagBody(false, "Upload file is required");
            case "add_to_cart":
            case "create_shop_quote":
            case "create_crm_quote":
                await EnsureBulkHistoryAsync(connection, cancellationToken).ConfigureAwait(false);
                var found = await BulkOneAsync(connection, PhpInt(ShopField(fields, "upload_id")), cancellationToken).ConfigureAwait(false);
                if (found is null)
                {
                    return new FlagBody(false, "Upload not found");
                }

                if (string.Equals(action, "create_crm_quote", StringComparison.Ordinal))
                {
                    return new FlagBody(false, CrmQuoteNotPosted);
                }

                return new FlagBody(false, "No available lines selected");
            default:
                return new FlagBody(false, "Unknown action");
        }
    }

    private static async Task<JsonArray> BulkRowsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new JsonArray();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `file_name`, `source` FROM `epc_bulk_upload_history` ORDER BY `id` DESC LIMIT 50";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new JsonObject { ["id"] = reader.GetInt32(0), ["file_name"] = reader.GetString(1), ["source"] = reader.GetString(2) });
        }

        return rows;
    }

    private static async Task<JsonObject?> BulkOneAsync(DbConnection connection, int id, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `id`, `file_name`, `user_id` FROM `epc_bulk_upload_history` WHERE `id` = ? LIMIT 1");
        ErpDb.AddParameters(command, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new JsonObject { ["id"] = reader.GetInt32(0), ["file_name"] = reader.GetString(1), ["user_id"] = reader.GetInt32(2), ["rows"] = new JsonArray() };
    }

    private static async Task<object> BulkCustomersAsync(DbConnection connection, string query, CancellationToken cancellationToken)
    {
        try
        {
            var rows = new JsonArray();
            await using var command = connection.CreateCommand();
            if (query.Trim().Length == 0)
            {
                command.CommandText = "SELECT `user_id`, `email` FROM `users` WHERE `user_id` > 0 ORDER BY `user_id` DESC LIMIT 20";
            }
            else
            {
                command.CommandText = ErpDb.Positional("SELECT `user_id`, `email` FROM `users` WHERE `email` LIKE ? ORDER BY `user_id` DESC LIMIT 20");
                ErpDb.AddParameters(command, "%" + query.Trim() + "%");
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var userId = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
                var email = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                rows.Add(new JsonObject { ["user_id"] = userId, ["email"] = email, ["label"] = (email.Length == 0 ? string.Empty : email + " · ") + "#" + userId.ToString(CultureInfo.InvariantCulture) });
            }

            return new JsonObject { ["status"] = true, ["customers"] = rows };
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, CustomersMissing);
        }
    }

    private static async Task EnsurePosRegisterAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_pos_settings` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `pos_enabled` tinyint(1) NOT NULL DEFAULT 1,
              `register_name` varchar(64) NOT NULL DEFAULT 'Register 1',
              `default_warehouse_id` int(11) NOT NULL DEFAULT 0,
              `walkin_user_id` int(11) NOT NULL DEFAULT 0,
              `default_cash_account_id` int(11) NOT NULL DEFAULT 0,
              `default_card_account_id` int(11) NOT NULL DEFAULT 0,
              `receipt_header` varchar(512) NOT NULL DEFAULT '',
              `receipt_footer` varchar(512) NOT NULL DEFAULT 'Thank you for your purchase',
              `time_updated` int(11) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, """
            CREATE TABLE IF NOT EXISTS `epc_pos_sessions` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `session_no` varchar(32) NOT NULL,
              `register_name` varchar(64) NOT NULL DEFAULT '',
              `opened_by` int(11) NOT NULL DEFAULT 0,
              `opened_at` int(11) NOT NULL DEFAULT 0,
              `closed_at` int(11) NOT NULL DEFAULT 0,
              `opening_float` decimal(14,2) NOT NULL DEFAULT 0.00,
              `closing_cash` decimal(14,2) DEFAULT NULL,
              `expected_cash` decimal(14,2) DEFAULT NULL,
              `sales_count` int(11) NOT NULL DEFAULT 0,
              `sales_total` decimal(14,2) NOT NULL DEFAULT 0.00,
              `status` varchar(16) NOT NULL DEFAULT 'open',
              `notes` varchar(512) NOT NULL DEFAULT '',
              PRIMARY KEY (`id`),
              UNIQUE KEY `x_session_no` (`session_no`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """, cancellationToken).ConfigureAwait(false);
        var count = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_pos_settings`", cancellationToken).ConfigureAwait(false);
        if (count <= 0)
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("INSERT INTO `epc_pos_settings` (`pos_enabled`, `register_name`, `receipt_footer`, `time_updated`) VALUES (1, ?, ?, ?)"), cancellationToken, "Register 1", "Thank you for your purchase", UnixNow()).ConfigureAwait(false);
        }
    }

    private static async Task<object> PosActionAsync(DbConnection connection, string action, IReadOnlyDictionary<string, string> fields, int adminId, CancellationToken cancellationToken)
    {
        switch (action)
        {
            case "search_products":
                return new JsonObject { ["status"] = true, ["products"] = await PosProductsAsync(connection, ShopField(fields, "q"), cancellationToken).ConfigureAwait(false) };
            case "search_customers":
                return new JsonObject { ["status"] = true, ["customers"] = await PosCustomersAsync(connection, ShopField(fields, "q"), cancellationToken).ConfigureAwait(false) };
            case "calc_cart":
                if (PhpInt(ShopField(fields, "customer_user_id")) <= 0 && PhpInt(ShopField(fields, "contact_id")) <= 0)
                {
                    return new FlagBody(false, WalkInNotCreated);
                }

                return new JsonObject { ["status"] = true, ["totals"] = PosTotals(ShopField(fields, "lines")) };
            case "open_session":
                await EnsurePosRegisterAsync(connection, cancellationToken).ConfigureAwait(false);
                return await PosOpenAsync(connection, fields, adminId, cancellationToken).ConfigureAwait(false);
            case "close_session":
                await EnsurePosRegisterAsync(connection, cancellationToken).ConfigureAwait(false);
                return await PosCloseAsync(connection, fields, cancellationToken).ConfigureAwait(false);
            case "session_status":
                await EnsurePosRegisterAsync(connection, cancellationToken).ConfigureAwait(false);
                var open = await PosOpenRowAsync(connection, cancellationToken).ConfigureAwait(false);
                return new JsonObject
                {
                    ["status"] = true,
                    ["session"] = open,
                    ["stats"] = new JsonObject { ["today_sales"] = 0, ["today_total"] = 0, ["week_sales"] = 0, ["week_total"] = 0 }
                };
            case "save_settings":
                await EnsurePosRegisterAsync(connection, cancellationToken).ConfigureAwait(false);
                var enabled = ShopTruthy(ShopField(fields, "pos_enabled")) ? 1 : 0;
                var register = ShopField(fields, "register_name").Trim();
                if (register.Length == 0)
                {
                    register = "Register 1";
                }

                if (register.Length > 64)
                {
                    register = register[..64];
                }

                await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_pos_settings` SET `pos_enabled` = ?, `register_name` = ?, `time_updated` = ? ORDER BY `id` ASC LIMIT 1"), cancellationToken, enabled, register, UnixNow()).ConfigureAwait(false);
                return new JsonObject { ["status"] = true };
            default:
                return new FlagBody(false, "Unknown action");
        }
    }

    private static async Task<JsonArray> PosProductsAsync(DbConnection connection, string query, CancellationToken cancellationToken)
    {
        var rows = new JsonArray();
        var q = query.Trim();
        if (q.Length == 0)
        {
            return rows;
        }

        try
        {
            var like = "%" + q + "%";
            var exact = q.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("""
                SELECT `id`, `manufacturer`, COALESCE(NULLIF(`article_show`, ''), `article`) AS `article`, `name`, `price`, `exist`
                FROM `shop_docpart_prices_data`
                WHERE (`name` LIKE ? OR `article` LIKE ? OR `article_show` LIKE ? OR `manufacturer` LIKE ? OR UPPER(REPLACE(`article`, ' ', '')) = ?)
                  AND IFNULL(`price`, 0) > 0
                ORDER BY `name` ASC LIMIT 30
                """);
            ErpDb.AddParameters(command, like, like, like, like, exact);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new JsonObject
                {
                    ["source"] = "price_data",
                    ["ref"] = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture),
                    ["sku"] = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    ["name"] = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    ["brand"] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    ["price"] = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4)
                });
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return rows;
        }

        return rows;
    }

    private static async Task<JsonArray> PosCustomersAsync(DbConnection connection, string query, CancellationToken cancellationToken)
    {
        var rows = new JsonArray();
        var q = query.Trim();
        if (q.Length == 0)
        {
            return rows;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT `user_id`, `email` FROM `users` WHERE `email` LIKE ? ORDER BY `user_id` DESC LIMIT 15");
            ErpDb.AddParameters(command, "%" + q + "%");
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var email = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                rows.Add(new JsonObject
                {
                    ["type"] = "user",
                    ["user_id"] = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    ["label"] = email,
                    ["email"] = email
                });
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return rows;
        }

        return rows;
    }

    private static JsonObject PosTotals(string linesRaw)
    {
        decimal subtotal = 0;
        decimal discount = 0;
        if (ShopJson(linesRaw, out var root) && root["lines"] is JsonArray == false && root is JsonObject)
        {
            // posted lines may be a JSON array string, not an object
        }

        JsonArray? lines = null;
        if (!string.IsNullOrWhiteSpace(linesRaw))
        {
            try
            {
                var node = JsonNode.Parse(linesRaw);
                lines = node as JsonArray;
            }
            catch (JsonException)
            {
                lines = null;
            }
        }

        if (lines is not null)
        {
            foreach (var node in lines)
            {
                if (node is not JsonObject line || ShopText(line, "name").Length == 0)
                {
                    continue;
                }

                var qty = ShopDecimalNode(line, "qty", 1);
                var unit = ShopDecimalNode(line, "unit_price_ex", 0);
                if (unit == 0)
                {
                    unit = ShopDecimalNode(line, "price", 0);
                }

                var gross = Math.Round(qty * unit, 2);
                var disc = Math.Round(ShopDecimalNode(line, "line_discount_amt", 0), 2);
                subtotal += gross;
                discount += disc;
            }
        }

        var amount = Math.Round(subtotal - discount, 2);
        return new JsonObject
        {
            ["subtotal_ex"] = Math.Round(subtotal, 2),
            ["discount_total"] = Math.Round(discount, 2),
            ["amount_ex_vat"] = amount,
            ["vat_amount"] = 0,
            ["total_amount"] = amount,
            ["tax_rate"] = 0
        };
    }

    private static async Task<object> PosOpenAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, int adminId, CancellationToken cancellationToken)
    {
        var open = await PosOpenRowAsync(connection, cancellationToken).ConfigureAwait(false);
        if (open is not null)
        {
            throw new InvalidOperationException("A register session is already open (" + open["session_no"] + ")");
        }

        var enabled = await ErpDb.StringAsync(connection, null, "SELECT CAST(`pos_enabled` AS CHAR) FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
        if (enabled is "0")
        {
            throw new InvalidOperationException("POS is disabled for this tenant");
        }

        var register = await ErpDb.StringAsync(connection, null, "SELECT `register_name` FROM `epc_pos_settings` ORDER BY `id` ASC LIMIT 1", cancellationToken).ConfigureAwait(false) ?? "Register 1";
        var day = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var count = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_pos_sessions` WHERE `session_no` LIKE ?"), cancellationToken, "REG-" + day + "-%").ConfigureAwait(false);
        var sessionNo = "REG-" + day + "-" + (count + 1).ToString("0000", CultureInfo.InvariantCulture);
        var opening = Math.Round(ShopDecimal(fields, "opening_float", 0), 2);
        var now = UnixNow();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_pos_sessions` (`session_no`, `register_name`, `opened_by`, `opened_at`, `opening_float`, `status`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            sessionNo,
            register,
            adminId,
            now,
            opening,
            "open").ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return new JsonObject
        {
            ["status"] = true,
            ["session"] = new JsonObject
            {
                ["session_id"] = id,
                ["session_no"] = sessionNo,
                ["register_name"] = register,
                ["opening_float"] = opening
            }
        };
    }

    private static async Task<object> PosCloseAsync(DbConnection connection, IReadOnlyDictionary<string, string> fields, CancellationToken cancellationToken)
    {
        var id = PhpInt(ShopField(fields, "session_id"));
        var openingRaw = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT CAST(`opening_float` AS CHAR) FROM `epc_pos_sessions` WHERE `id` = ? AND `status` = 'open' LIMIT 1"), cancellationToken, id).ConfigureAwait(false);
        if (string.IsNullOrEmpty(openingRaw))
        {
            throw new InvalidOperationException("Open session not found");
        }

        var opening = decimal.Parse(openingRaw, CultureInfo.InvariantCulture);
        decimal cashSales = 0;
        long salesCount = 0;
        try
        {
            salesCount = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `epc_pos_sales` WHERE `session_id` = ? AND `status` = 'completed'"), cancellationToken, id).ConfigureAwait(false);
            var cashRaw = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT CAST(COALESCE(SUM(`cash_amount`),0) AS CHAR) FROM `epc_pos_sales` WHERE `session_id` = ? AND `status` = 'completed'"), cancellationToken, id).ConfigureAwait(false);
            if (decimal.TryParse(cashRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                cashSales = parsed;
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            cashSales = 0;
            salesCount = 0;
        }

        var expected = Math.Round(opening + cashSales, 2);
        var closing = Math.Round(ShopDecimal(fields, "closing_cash", 0), 2);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_pos_sessions` SET `closed_at` = ?, `closing_cash` = ?, `expected_cash` = ?, `sales_count` = ?, `sales_total` = ?, `status` = 'closed', `notes` = ? WHERE `id` = ?"),
            cancellationToken,
            UnixNow(),
            closing,
            expected,
            salesCount,
            0,
            ShopField(fields, "notes").Trim(),
            id).ConfigureAwait(false);
        return new JsonObject
        {
            ["status"] = true,
            ["result"] = new JsonObject
            {
                ["session_id"] = id,
                ["expected_cash"] = expected,
                ["closing_cash"] = closing,
                ["sales_count"] = salesCount
            }
        };
    }

    private static async Task<JsonObject?> PosOpenRowAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `session_no`, `opening_float` FROM `epc_pos_sessions` WHERE `status` = 'open' ORDER BY `opened_at` DESC LIMIT 1";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new JsonObject
        {
            ["session_id"] = reader.GetInt32(0),
            ["session_no"] = reader.GetString(1),
            ["opening_float"] = reader.GetDecimal(2)
        };
    }

    private static async Task<string?> ShopStoredCsrfAsync(DbConnection connection, string? adminSession, string? adminUser, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ? LIMIT 1"), cancellationToken, adminSession ?? string.Empty, PhpInt(adminUser)).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return null;
        }
    }

    private static async Task ShopCollectAsync(DbConnection connection, string sql, string article, SortedSet<string> names, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, article);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!reader.IsDBNull(0))
            {
                names.Add(reader.GetString(0));
            }
        }
    }

    private static async Task<long> ShopCountOrZeroAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return 0;
        }
    }

    private static async Task<string> ShopOptionalStringAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object?[] args)
    {
        try
        {
            return await ErpDb.StringAsync(connection, null, ErpDb.Positional(sql), cancellationToken, args).ConfigureAwait(false) ?? string.Empty;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return string.Empty;
        }
    }

    private static bool ShopJson(string? raw, out JsonObject root)
    {
        root = new JsonObject();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            if (JsonNode.Parse(raw) is JsonObject parsed)
            {
                root = parsed;
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static string ShopText(JsonObject root, string name)
        => root[name]?.ToString() ?? string.Empty;

    private static int ShopInt(JsonObject root, string name)
        => PhpInt(ShopText(root, name));

    private static string ShopField(IReadOnlyDictionary<string, string> fields, string name)
        => fields.TryGetValue(name, out var value) ? value : string.Empty;

    private static bool ShopTruthy(string value)
        => value.Length > 0 && !string.Equals(value, "0", StringComparison.Ordinal);

    private static decimal ShopDecimal(IReadOnlyDictionary<string, string> fields, string name, decimal fallback)
        => decimal.TryParse(ShopField(fields, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static decimal ShopDecimalNode(JsonObject root, string name, decimal fallback)
        => decimal.TryParse(ShopText(root, name), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;

    private static string ShopSweep(string value)
    {
        var upper = value.Trim().ToUpperInvariant();
        var chars = new[] { ' ', '-', '_', '`', '/', '\'', '"', '\\', '.', ',', '#', '\r', '\n', '\t' };
        foreach (var ch in chars)
        {
            upper = upper.Replace(ch.ToString(), string.Empty, StringComparison.Ordinal);
        }

        return upper;
    }

    private static string ShopBrand(string value)
    {
        var upper = value.Trim().ToUpperInvariant();
        foreach (var ch in new[] { '#', '`', '\'', '"', '\\', '\r', '\n', '\t' })
        {
            upper = upper.Replace(ch.ToString(), string.Empty, StringComparison.Ordinal);
        }

        return upper.Trim();
    }

    private static string ShopArticle(string value)
    {
        var cleaned = RegexArticle().Replace(value, string.Empty);
        return cleaned.ToUpperInvariant();
    }

    private static string ShopMoney(string value)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed.ToString("0.00", CultureInfo.InvariantCulture)
            : "0.00";

    private static int ShopDigits(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static string RegexSafe(string name)
    {
        var chars = name.Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' ? ch : '_').ToArray();
        return new string(chars);
    }

    [System.Text.RegularExpressions.GeneratedRegex("[^a-zA-Z0-9А-Яа-яёЁ]+")]
    private static partial System.Text.RegularExpressions.Regex RegexArticle();
}
