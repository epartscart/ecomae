using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-work helpers. PHP identifiers kept for the inventory:
/// <c>epc_orders_ws_h</c>, <c>epc_orders_ws_storage_label</c>,
/// <c>epc_orders_ws_usd_rate</c>, <c>epc_orders_ws_aed_usd</c>,
/// <c>epc_orders_ws_badge_class</c>, <c>epc_orders_ws_status_badge</c>,
/// <c>epc_orders_ws_paid_badge</c>, <c>epc_orders_ws_kpi</c>,
/// <c>epc_orders_ws_in_process_status_ids</c>, <c>epc_orders_ws_open_status_ids</c>,
/// <c>epc_orders_ws_completed_status_ids</c>, <c>epc_orders_ws_tab_from_cookie</c>,
/// <c>epc_orders_ws_filter_has_search</c>, <c>epc_orders_ws_normalize_filter_for_tab</c>,
/// <c>epc_orders_ws_count_by_statuses</c>,
/// <c>epc_marketing_h</c>, <c>epc_marketing_table_exists</c>,
/// <c>epc_marketing_live_snapshot</c>, <c>epc_marketing_load_progress</c>,
/// <c>epc_marketing_completion_stats</c>, <c>epc_marketing_latest_kpis</c>,
/// <c>epc_marketing_kpi_history</c>, <c>epc_marketing_recent_reviews</c>,
/// <c>epc_marketing_toggle_task</c>, <c>epc_marketing_save_kpi</c>,
/// <c>epc_marketing_save_review</c>, <c>epc_marketing_resolve_link</c>,
/// <c>epc_marketing_demo_report</c>,
/// <c>epc_cp_breadcrumb_humanize_segment</c>,
/// <c>epc_cp_breadcrumb_caption_for_node</c>,
/// <c>epc_cp_breadcrumb_ensure_folder_content</c>,
/// <c>epc_cp_breadcrumb_repair_intermediate_folders</c>.
/// </summary>
public static class PhpPlanQ1Work
{
    public const string OrdersWorkspacePath = "cp/content/shop/order_process/epc_orders_workspace_helpers.php";
    public const string MarketingHelpersPath = "content/shop/marketing/epc_marketing_helpers.php";
    public const string BreadcrumbPath = "content/general_pages/epc_cp_breadcrumb.php";

    private static readonly Regex HumanizeKeep = new(@"[^a-z0-9_-]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex HumanizeSpaces = new(@"\s+", RegexOptions.CultureInvariant);
    private static readonly Dictionary<int, string> BadgeCache = new();

    public static Func<object?, string>? Translate { get; set; }
    public static Func<IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>>? CurrencyRecords { get; set; }
    public static Func<long>? TodayStart { get; set; }
    public static Func<long>? Clock { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? MarketingStrategies { get; set; }
    public static string? OrdersTabCookie { get; set; }
    public static string BackendDir { get; set; } = "cp";

    public static void Reset()
    {
        BadgeCache.Clear();
        Translate = null;
        CurrencyRecords = null;
        TodayStart = null;
        Clock = null;
        MarketingStrategies = DefaultStrategies;
        OrdersTabCookie = null;
        BackendDir = "cp";
    }

    public static string EpcOrdersWsH(object? value)
        => PhpPlanQ1Plus.EpcChannelH(value);

    public static string EpcOrdersWsStorageLabel(object? raw)
    {
        var text = (Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "").Trim();
        if (text.Length == 0)
        {
            return "—";
        }

        if (text.All(char.IsDigit) && Translate is not null)
        {
            var tr = Translate(text);
            if (!string.IsNullOrEmpty(tr) && !tr.StartsWith("ERROR STR_KEY", StringComparison.Ordinal))
            {
                return tr;
            }
        }

        return text;
    }

    public static double EpcOrdersWsUsdRate(object? db = null, object? config = null)
    {
        var rate = 3.6725;
        try
        {
            if (db is not null && config is not null && CurrencyRecords is not null)
            {
                var records = CurrencyRecords();
                if (records.TryGetValue("840", out var row)
                    && row.TryGetValue("rate", out var raw)
                    && ToFloat(raw) > 0)
                {
                    rate = ToFloat(raw);
                }
            }
        }
        catch
        {
        }

        return rate > 0 ? rate : 3.6725;
    }

    public static string EpcOrdersWsAedUsd(double aed, double usdRate)
    {
        var usd = usdRate > 0 ? aed / usdRate : 0.0;
        return Number2(aed) + " AED / " + Number2(usd) + " USD";
    }

    public static string EpcOrdersWsBadgeClass(int statusId, WsStore db)
    {
        if (!BadgeCache.TryGetValue(statusId, out var cls))
        {
            var row = db.Statuses.Find(s => s.Id == statusId);
            if (row is null)
            {
                cls = "epc-scp-badge--normal";
            }
            else if (row.ForInverse == 1)
            {
                cls = "epc-scp-badge--urgent";
            }
            else if (row.ForFinish == 1)
            {
                cls = "epc-scp-badge--tenant";
            }
            else if (row.ForCreated == 1)
            {
                cls = "epc-scp-badge--high";
            }
            else
            {
                cls = "epc-scp-badge--normal";
            }

            BadgeCache[statusId] = cls;
        }

        return cls;
    }

    public static string EpcOrdersWsStatusBadge(int statusId, IReadOnlyDictionary<int, IReadOnlyDictionary<string, object?>> ordersStatuses, WsStore db)
    {
        var name = "";
        if (ordersStatuses.TryGetValue(statusId, out var row) && row.TryGetValue("name", out var raw))
        {
            name = Convert.ToString(raw, CultureInfo.InvariantCulture) ?? "";
        }

        var label = Translate?.Invoke(name) ?? name;
        var cls = EpcOrdersWsBadgeClass(statusId, db);
        return "<span class=\"epc-scp-badge " + EpcOrdersWsH(cls) + "\">" + EpcOrdersWsH(label) + "</span>";
    }

    public static string EpcOrdersWsPaidBadge(int paid)
    {
        if (paid == 1)
        {
            return "<span class=\"epc-scp-badge epc-scp-badge--tenant\">" + EpcOrdersWsH(Translate?.Invoke(3514) ?? "") + "</span>";
        }

        if (paid == 2)
        {
            return "<span class=\"epc-scp-badge epc-scp-badge--high\">" + EpcOrdersWsH(Translate?.Invoke(3515) ?? "") + "</span>";
        }

        return "<span class=\"epc-scp-badge epc-scp-badge--urgent\">" + EpcOrdersWsH(Translate?.Invoke(3513) ?? "") + "</span>";
    }

    public static Dictionary<string, int> EpcOrdersWsKpi(WsStore db, IReadOnlyDictionary<int, string> officesList, int managerId)
    {
        var officeIds = officesList.Keys.ToList();
        if (officeIds.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal) { ["open"] = 0, ["today"] = 0, ["pending_ship"] = 0 };
        }

        var todayStart = TodayStart?.Invoke() ?? DateTimeOffset.UtcNow.ToOffset(TimeSpan.Zero).UtcDateTime.Date
            .Subtract(DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerSecond;
        var openStatuses = EpcOrdersWsOpenStatusIds(db);
        var open = openStatuses.Count == 0
            ? 0
            : db.Orders.Count(o => officeIds.Contains(o.OfficeId) && openStatuses.Contains(o.Status));
        var today = db.Orders.Count(o => officeIds.Contains(o.OfficeId) && o.Time >= todayStart);
        var shipStatuses = db.Statuses.Where(s => s.ForFinish != 1 && s.ForInverse != 1).Select(s => s.Id).ToList();
        var pendingShip = shipStatuses.Count == 0
            ? 0
            : db.Orders.Count(o => officeIds.Contains(o.OfficeId) && shipStatuses.Contains(o.Status) && (o.Paid == 1 || o.Paid == 2));
        return new Dictionary<string, int>(StringComparer.Ordinal) { ["open"] = open, ["today"] = today, ["pending_ship"] = pendingShip };
    }

    public static List<int> EpcOrdersWsInProcessStatusIds(WsStore db)
        => db.Statuses.Where(s => s.ForInverse != 1 && s.ForFinish != 1 && s.ForCreated != 1).Select(s => s.Id).ToList();

    public static List<int> EpcOrdersWsOpenStatusIds(WsStore db)
        => db.Statuses.Where(s => s.ForInverse != 1 && s.ForFinish != 1).Select(s => s.Id).ToList();

    public static List<int> EpcOrdersWsCompletedStatusIds(WsStore db)
        => db.Statuses.Where(s => s.ForFinish == 1).Select(s => s.Id).ToList();

    public static string EpcOrdersWsTabFromCookie()
    {
        var tab = (OrdersTabCookie ?? "open").Trim().ToLowerInvariant();
        return tab is "open" or "completed" or "all" ? tab : "open";
    }

    public static bool EpcOrdersWsFilterHasSearch(IReadOnlyDictionary<string, object?> filter)
    {
        foreach (var key in new[] { "order_id", "customer", "customer_id", "phone", "article", "time_from", "time_to" })
        {
            if (filter.TryGetValue(key, out var value) && !IsEmpty(value))
            {
                return true;
            }
        }

        return false;
    }

    public static Dictionary<string, object?> EpcOrdersWsNormalizeFilterForTab(
        IReadOnlyDictionary<string, object?> filter,
        string tab,
        IReadOnlyList<int> openIds,
        IReadOnlyList<int> completedIds,
        bool forceDefaults)
    {
        var defaults = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["time_from"] = "", ["time_to"] = "", ["order_id"] = "", ["status"] = 0, ["paid"] = -1,
            ["customer"] = "", ["customer_id"] = "", ["viewed"] = -1, ["paid_type"] = -1,
            ["office"] = 0, ["phone"] = "", ["article"] = ""
        };
        var merged = new Dictionary<string, object?>(defaults, StringComparer.Ordinal);
        foreach (var kv in filter)
        {
            merged[kv.Key] = kv.Value;
        }

        List<string> StrIds(IEnumerable<int> ids) => ids.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList();
        if (tab == "open")
        {
            var status = merged["status"];
            var statusEmpty = status is null or 0 or "0" or "" || (status is IEnumerable<object?> en && !en.Any())
                || (status is System.Collections.ICollection col && col.Count == 0);
            if (forceDefaults || statusEmpty)
            {
                merged["status"] = StrIds(openIds);
            }

            if (forceDefaults)
            {
                merged["paid"] = -1;
                merged["viewed"] = -1;
                merged["paid_type"] = -1;
            }
        }
        else if (tab == "completed")
        {
            merged["status"] = StrIds(completedIds);
            if (forceDefaults)
            {
                merged["paid"] = -1;
                merged["viewed"] = -1;
                merged["paid_type"] = -1;
            }
        }
        else if (tab == "all" && forceDefaults)
        {
            merged["status"] = 0;
            merged["paid"] = -1;
            merged["viewed"] = -1;
            merged["paid_type"] = -1;
        }

        return merged;
    }

    public static int EpcOrdersWsCountByStatuses(WsStore db, IReadOnlyList<int> officeIds, IReadOnlyList<int> statusIds)
    {
        if (officeIds.Count == 0 || statusIds.Count == 0)
        {
            return 0;
        }

        return db.Orders.Count(o => officeIds.Contains(o.OfficeId) && statusIds.Contains(o.Status));
    }

    public static string EpcMarketingH(object? value) => PhpPlanQ1Plus.EpcChannelH(value);

    public static bool EpcMarketingTableExists(MktStore db, string table)
        => db.Tables.Contains(table);

    public static Dictionary<string, object?> EpcMarketingLiveSnapshot(MktStore db)
    {
        var snap = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["generated_at"] = GmDateC(Now()),
            ["orders_total"] = 0,
            ["orders_7d"] = 0,
            ["orders_30d"] = 0,
            ["users_total"] = 0,
            ["marketplace_orders"] = 0,
            ["whatsapp_api_sent"] = 0,
            ["whatsapp_api_failed"] = 0,
            ["price_rows"] = 0,
            ["brands_count"] = 0,
            ["ga_property"] = "G-J19D1KHXCG",
            ["sitemap_url"] = "",
            ["domain"] = ""
        };
        var now = Now();
        if (EpcMarketingTableExists(db, "shop_orders"))
        {
            snap["orders_total"] = db.Orders.Count(o => o.SuccessfullyCreated == 1);
            snap["orders_7d"] = db.Orders.Count(o => o.SuccessfullyCreated == 1 && o.Time >= now - 7 * 86400);
            snap["orders_30d"] = db.Orders.Count(o => o.SuccessfullyCreated == 1 && o.Time >= now - 30 * 86400);
        }

        if (EpcMarketingTableExists(db, "users"))
        {
            snap["users_total"] = db.Users;
        }

        if (EpcMarketingTableExists(db, "epc_marketplace_orders"))
        {
            snap["marketplace_orders"] = db.MarketplaceOrders;
        }

        if (EpcMarketingTableExists(db, "epc_whatsapp_notify_log"))
        {
            snap["whatsapp_api_sent"] = db.WhatsappSent;
            snap["whatsapp_api_failed"] = db.WhatsappFailed;
        }

        if (EpcMarketingTableExists(db, "shop_docpart_prices_data"))
        {
            snap["price_rows"] = db.PriceRows;
            snap["brands_count"] = db.BrandsCount;
        }

        return snap;
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcMarketingLoadProgress(MktStore db)
    {
        var progress = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        if (!EpcMarketingTableExists(db, "epc_marketing_task_progress"))
        {
            return progress;
        }

        foreach (var row in db.Tasks.OrderBy(t => t.StrategyKey, StringComparer.Ordinal).ThenBy(t => t.TaskKey, StringComparer.Ordinal))
        {
            if (!progress.TryGetValue(row.StrategyKey, out var bag))
            {
                bag = new Dictionary<string, object?>(StringComparer.Ordinal);
                progress[row.StrategyKey] = bag;
            }

            bag[row.TaskKey] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["done"] = row.IsDone == 1,
                ["done_at"] = row.DoneAt ?? 0,
                ["note"] = row.Note ?? ""
            };
        }

        return progress;
    }

    public static Dictionary<string, object?> EpcMarketingCompletionStats(
        IReadOnlyDictionary<string, Dictionary<string, object?>> strategies,
        IReadOnlyDictionary<string, Dictionary<string, object?>> progress)
    {
        var total = 0;
        var done = 0;
        var byStrategy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, str) in strategies)
        {
            var tasks = (IReadOnlyDictionary<string, object?>)str["follow_tasks"]!;
            var stTotal = tasks.Count;
            var stDone = 0;
            foreach (var taskKey in tasks.Keys)
            {
                total++;
                if (progress.TryGetValue(key, out var bag)
                    && bag.TryGetValue(taskKey, out var raw)
                    && raw is IReadOnlyDictionary<string, object?> task
                    && !IsEmpty(task.TryGetValue("done", out var d) ? d : null))
                {
                    done++;
                    stDone++;
                }
            }

            byStrategy[key] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["total"] = stTotal,
                ["done"] = stDone,
                ["pct"] = stTotal > 0 ? (int)Math.Round(100.0 * stDone / stTotal, MidpointRounding.AwayFromZero) : 0
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["total"] = total,
            ["done"] = done,
            ["pct"] = total > 0 ? (int)Math.Round(100.0 * done / total, MidpointRounding.AwayFromZero) : 0,
            ["by_strategy"] = byStrategy
        };
    }

    public static Dictionary<string, Dictionary<string, object?>> EpcMarketingLatestKpis(MktStore db, IReadOnlyDictionary<string, Dictionary<string, object?>> strategies)
    {
        var latest = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        if (!EpcMarketingTableExists(db, "epc_marketing_kpi_log"))
        {
            return latest;
        }

        foreach (var str in strategies.Values)
        {
            if (str["kpis"] is not IReadOnlyDictionary<string, object?> kpis)
            {
                continue;
            }

            foreach (var kpiKey in kpis.Keys)
            {
                var row = db.Kpis.Where(k => k.KpiKey == kpiKey).OrderByDescending(k => k.RecordedAt).ThenByDescending(k => k.Id).FirstOrDefault();
                if (row is not null)
                {
                    latest[kpiKey] = KpiDict(row);
                }
            }
        }

        return latest;
    }

    public static List<Dictionary<string, object?>> EpcMarketingKpiHistory(MktStore db, string kpiKey, int limit = 12)
    {
        if (!EpcMarketingTableExists(db, "epc_marketing_kpi_log"))
        {
            return new List<Dictionary<string, object?>>();
        }

        return db.Kpis.Where(k => k.KpiKey == kpiKey)
            .OrderByDescending(k => k.RecordedAt)
            .ThenByDescending(k => k.Id)
            .Take(limit < 0 ? 0 : limit)
            .Select(KpiDict)
            .ToList();
    }

    public static List<Dictionary<string, object?>> EpcMarketingRecentReviews(MktStore db, int limit = 20)
    {
        if (!EpcMarketingTableExists(db, "epc_marketing_reviews"))
        {
            return new List<Dictionary<string, object?>>();
        }

        return db.Reviews
            .OrderByDescending(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .Take(limit < 0 ? 0 : limit)
            .Select(r => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["strategy_key"] = r.StrategyKey,
                ["review_type"] = r.ReviewType,
                ["score"] = r.Score,
                ["notes"] = r.Notes,
                ["created_by"] = r.CreatedBy
            })
            .ToList();
    }

    public static void EpcMarketingToggleTask(MktStore db, string strategyKey, string taskKey, bool done, int userId = 0)
    {
        EpcMarketingEnsureSchema(db);
        var now = Now();
        var row = db.Tasks.Find(t => t.StrategyKey == strategyKey && t.TaskKey == taskKey);
        if (row is null)
        {
            db.Tasks.Add(new MktTaskRow
            {
                StrategyKey = strategyKey,
                TaskKey = taskKey,
                IsDone = done ? 1 : 0,
                DoneAt = done ? now : null,
                UpdatedAt = now
            });
        }
        else
        {
            row.IsDone = done ? 1 : 0;
            row.DoneAt = done ? now : null;
            row.UpdatedAt = now;
        }
    }

    public static void EpcMarketingSaveKpi(MktStore db, string strategyKey, string kpiKey, object? value, string note, int userId = 0)
    {
        EpcMarketingEnsureSchema(db);
        var numeric = IsNumeric(value);
        db.Kpis.Add(new MktKpiRow
        {
            Id = db.NextKpiId++,
            StrategyKey = strategyKey,
            KpiKey = kpiKey,
            ValueDecimal = numeric ? ToFloat(value) : null,
            ValueText = numeric ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : (Convert.ToString(value, CultureInfo.InvariantCulture) ?? "").Trim(),
            Note = note,
            RecordedAt = Now(),
            RecordedBy = userId
        });
    }

    public static void EpcMarketingSaveReview(MktStore db, string strategyKey, string reviewType, int score, string notes, int userId = 0)
    {
        EpcMarketingEnsureSchema(db);
        db.Reviews.Add(new MktReviewRow
        {
            Id = db.NextReviewId++,
            StrategyKey = strategyKey,
            ReviewType = reviewType,
            Score = Math.Max(0, Math.Min(5, score)),
            Notes = notes,
            CreatedAt = Now(),
            CreatedBy = userId
        });
    }

    public static string EpcMarketingResolveLink(string url, string backend, string domain)
    {
        if (Regex.IsMatch(url, @"^https?://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return url;
        }

        if (url.Length > 0 && url[0] == '/')
        {
            if (url.StartsWith("/cp/", StringComparison.Ordinal))
            {
                return domain.TrimEnd('/') + Regex.Replace(url, "^/cp/", "/" + backend + "/");
            }

            return domain.TrimEnd('/') + url;
        }

        return url;
    }

    public static Dictionary<string, object?> EpcMarketingDemoReport(MktStore db)
    {
        EpcMarketingEnsureSchema(db);
        var strategies = (MarketingStrategies ?? DefaultStrategies)();
        var progress = EpcMarketingLoadProgress(db);
        var live = EpcMarketingLiveSnapshot(db);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["status"] = true,
            ["generated_at"] = GmDateC(Now()),
            ["completion"] = EpcMarketingCompletionStats(strategies, progress),
            ["live"] = live,
            ["strategies"] = strategies.Keys.ToArray()
        };
    }

    public static void EpcMarketingEnsureSchema(MktStore db)
    {
        db.Tables.Add("epc_marketing_task_progress");
        db.Tables.Add("epc_marketing_kpi_log");
        db.Tables.Add("epc_marketing_reviews");
        db.SchemaReady = true;
    }

    public static string EpcCpBreadcrumbHumanizeSegment(string nodeUrl)
    {
        var part = Path.GetFileName(nodeUrl.Replace('\\', '/'));
        part = HumanizeKeep.Replace(part ?? "", " ");
        part = HumanizeSpaces.Replace(part, " ").Trim();
        if (part.Length == 0)
        {
            return "";
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part.Replace("-", " ", StringComparison.Ordinal).Replace("_", " ", StringComparison.Ordinal).ToLowerInvariant());
    }

    public static string EpcCpBreadcrumbCaptionForNode(CrumbStore db, string nodeUrl, string fallback404, bool isLast, bool pageIs404)
    {
        nodeUrl = nodeUrl.Replace('\\', '/').Trim('/');
        if (nodeUrl.Length == 0)
        {
            return "";
        }

        var node = db.Content.Find(c => c.Url == nodeUrl && c.IsFrontend == 0);
        if (node is not null)
        {
            return Translate?.Invoke(node.Value) ?? node.Value ?? "";
        }

        var captionKey = "";
        foreach (var item in db.ControlItems)
        {
            if (item.Url == "/<backend>/" + nodeUrl
                || item.Url.EndsWith("/" + nodeUrl, StringComparison.Ordinal)
                || item.Url.Contains("/" + nodeUrl + "?", StringComparison.Ordinal))
            {
                captionKey = item.Caption;
                break;
            }
        }

        if (captionKey.Length > 0)
        {
            var label = Translate?.Invoke(captionKey) ?? captionKey;
            if (label.Length > 0 && label.IndexOf("404", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return label;
            }
        }

        if (isLast && pageIs404)
        {
            return fallback404;
        }

        var human = EpcCpBreadcrumbHumanizeSegment(nodeUrl);
        if (human.Length > 0)
        {
            return human;
        }

        return isLast && pageIs404 ? fallback404 : EpcCpBreadcrumbHumanizeSegment(nodeUrl);
    }

    public static int EpcCpBreadcrumbEnsureFolderContent(CrumbStore db, string parentUrl, string url, string alias, string valueKey, string title, int order = 50)
    {
        var parent = db.Content.Find(c => c.Url == parentUrl && c.IsFrontend == 0)
            ?? throw new InvalidOperationException("Parent content not found: " + parentUrl);
        var parentId = parent.Id;
        var level = parent.Level + 1;
        var now = Now();
        db.Lang.Add(valueKey);
        var existing = db.Content.Find(c => c.Url == url && c.IsFrontend == 0);
        int contentId;
        if (existing is not null)
        {
            existing.PublishedFlag = 1;
            existing.ContentType = "php";
            existing.TitleTag = title;
            existing.Parent = parentId;
            existing.Level = level;
            existing.Alias = alias;
            existing.Value = valueKey;
            existing.TimeEdited = now;
            contentId = existing.Id;
        }
        else
        {
            contentId = db.NextContentId++;
            db.Content.Add(new CrumbContentRow
            {
                Id = contentId,
                Url = url,
                Level = level,
                Alias = alias,
                Value = valueKey,
                Parent = parentId,
                Description = title,
                TitleTag = title,
                TimeCreated = now,
                TimeEdited = now,
                Order = order,
                PublishedFlag = 1
            });
        }

        db.Access.RemoveAll(a => a.ContentId == contentId);
        var root = db.Groups.FirstOrDefault(g => g.ForBackend == 1)?.Id ?? 0;
        var groups = new List<int> { root > 0 ? root : 1 };
        if (root > 0)
        {
            CollectGroups(db, root, groups);
        }

        foreach (var gid in groups.Distinct())
        {
            db.Access.Add(new CrumbAccessRow { ContentId = contentId, GroupId = gid });
        }

        return contentId;
    }

    public static Dictionary<string, int> EpcCpBreadcrumbRepairIntermediateFolders(CrumbStore db)
    {
        var folders = new (string Parent, string Url, string Alias, string ValueKey, string Title, int Order)[]
        {
            ("shop", "shop/procurement", "procurement_folder", "epc_cp_group_procurement", "Procurement", 87),
            ("shop", "shop/marketing", "marketing_folder", "epc_cp_group_marketing", "Marketing", 89),
            ("shop", "shop/payments", "payments_folder", "epc_cp_group_payments", "Payment gateways", 88),
            ("shop", "shop/channels", "channels_folder", "epc_cp_group_channels", "Channels", 84)
        };
        var outMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in folders)
        {
            try
            {
                outMap[row.Url] = EpcCpBreadcrumbEnsureFolderContent(db, row.Parent, row.Url, row.Alias, row.ValueKey, row.Title, row.Order);
            }
            catch
            {
                outMap[row.Url] = 0;
            }
        }

        return outMap;
    }

    public static Dictionary<string, Dictionary<string, object?>> DefaultStrategies()
        => new(StringComparer.Ordinal)
        {
            ["measurement"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["follow_tasks"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["gsc_verify"] = "Verify GSC",
                    ["ga_conversions"] = "GA4"
                },
                ["kpis"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["monthly_sessions"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["label"] = "Sessions" }
                }
            },
            ["seo"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["follow_tasks"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["onpage"] = "On-page" },
                ["kpis"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            }
        };

    public static string TranslateStub(object? id)
    {
        var key = id is int or long ? Convert.ToInt32(id, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : Convert.ToString(id, CultureInfo.InvariantCulture) ?? "";
        return key switch
        {
            "10" => "New",
            "20" => "Pack",
            "30" => "Done",
            "12" => "ERROR STR_KEY 12",
            "99" => "Ninety-nine",
            "3513" => "Unpaid",
            "3514" => "Paid",
            "3515" => "Partial",
            "100" => "Shop",
            "200" => "Orders",
            "300" => "Users",
            _ => key
        };
    }

    public sealed class WsStore
    {
        public List<WsStatusRow> Statuses { get; } = new();
        public List<WsOrderRow> Orders { get; } = new();
    }

    public sealed class WsStatusRow
    {
        public int Id { get; set; }
        public int ForFinish { get; set; }
        public int ForInverse { get; set; }
        public int ForCreated { get; set; }
        public string Name { get; set; } = "";
    }

    public sealed class WsOrderRow
    {
        public int OfficeId { get; set; }
        public int Status { get; set; }
        public long Time { get; set; }
        public int Paid { get; set; }
    }

    public sealed class MktStore
    {
        public HashSet<string> Tables { get; } = new(StringComparer.Ordinal);
        public bool SchemaReady { get; set; }
        public List<MktOrderRow> Orders { get; } = new();
        public int Users { get; set; }
        public int MarketplaceOrders { get; set; }
        public int WhatsappSent { get; set; }
        public int WhatsappFailed { get; set; }
        public int PriceRows { get; set; }
        public int BrandsCount { get; set; }
        public List<MktTaskRow> Tasks { get; } = new();
        public List<MktKpiRow> Kpis { get; } = new();
        public List<MktReviewRow> Reviews { get; } = new();
        public int NextKpiId { get; set; } = 1;
        public int NextReviewId { get; set; } = 1;
    }

    public sealed class MktOrderRow
    {
        public int SuccessfullyCreated { get; set; }
        public long Time { get; set; }
    }

    public sealed class MktTaskRow
    {
        public string StrategyKey { get; set; } = "";
        public string TaskKey { get; set; } = "";
        public int IsDone { get; set; }
        public long? DoneAt { get; set; }
        public string? Note { get; set; }
        public long UpdatedAt { get; set; }
    }

    public sealed class MktKpiRow
    {
        public int Id { get; set; }
        public string StrategyKey { get; set; } = "";
        public string KpiKey { get; set; } = "";
        public double? ValueDecimal { get; set; }
        public string ValueText { get; set; } = "";
        public string Note { get; set; } = "";
        public long RecordedAt { get; set; }
        public int RecordedBy { get; set; }
    }

    public sealed class MktReviewRow
    {
        public int Id { get; set; }
        public string StrategyKey { get; set; } = "";
        public string ReviewType { get; set; } = "weekly";
        public int Score { get; set; }
        public string Notes { get; set; } = "";
        public long CreatedAt { get; set; }
        public int CreatedBy { get; set; }
    }

    public sealed class CrumbStore
    {
        public List<CrumbContentRow> Content { get; } = new();
        public List<CrumbControlRow> ControlItems { get; } = new();
        public List<CrumbGroupRow> Groups { get; } = new();
        public List<CrumbAccessRow> Access { get; } = new();
        public HashSet<string> Lang { get; } = new(StringComparer.Ordinal);
        public int NextContentId { get; set; } = 1;
    }

    public sealed class CrumbContentRow
    {
        public int Id { get; set; }
        public string Url { get; set; } = "";
        public int Level { get; set; }
        public string Alias { get; set; } = "";
        public string Value { get; set; } = "";
        public int Parent { get; set; }
        public string Description { get; set; } = "";
        public int IsFrontend { get; set; }
        public string ContentType { get; set; } = "php";
        public string TitleTag { get; set; } = "";
        public int PublishedFlag { get; set; } = 1;
        public long TimeCreated { get; set; }
        public long TimeEdited { get; set; }
        public int Order { get; set; }
    }

    public sealed class CrumbControlRow
    {
        public string Caption { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public sealed class CrumbGroupRow
    {
        public int Id { get; set; }
        public int Parent { get; set; }
        public int ForBackend { get; set; }
    }

    public sealed class CrumbAccessRow
    {
        public int ContentId { get; set; }
        public int GroupId { get; set; }
    }

    private static void CollectGroups(CrumbStore db, int parentId, List<int> groups)
    {
        foreach (var child in db.Groups.Where(g => g.Parent == parentId))
        {
            groups.Add(child.Id);
            CollectGroups(db, child.Id, groups);
        }
    }

    private static Dictionary<string, object?> KpiDict(MktKpiRow row)
        => new(StringComparer.Ordinal)
        {
            ["strategy_key"] = row.StrategyKey,
            ["kpi_key"] = row.KpiKey,
            ["value_decimal"] = row.ValueDecimal is null ? null : row.ValueDecimal.Value.ToString("0.0000", CultureInfo.InvariantCulture),
            ["value_text"] = row.ValueText,
            ["note"] = row.Note,
            ["recorded_by"] = row.RecordedBy
        };

    private static long Now() => Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string GmDateC(long unix)
        => DateTimeOffset.FromUnixTimeSeconds(unix).ToString("yyyy-MM-ddTHH:mm:ss+00:00", CultureInfo.InvariantCulture);

    private static string Number2(double n)
        => n.ToString("N2", CultureInfo.InvariantCulture);

    private static bool IsEmpty(object? value)
        => value is null or false or 0 or 0L or 0d or "" or "0";

    private static bool IsNumeric(object? value)
    {
        if (value is int or long or float or double or decimal)
        {
            return true;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }

    private static double ToFloat(object? value)
    {
        if (value is double d)
        {
            return d;
        }

        if (value is int i)
        {
            return i;
        }

        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    }
}
