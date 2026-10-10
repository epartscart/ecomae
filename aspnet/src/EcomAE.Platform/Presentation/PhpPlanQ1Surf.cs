using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-surf BOC advanced control. PHP identifiers kept for the inventory:
/// <c>epc_boc_adv_money</c>, <c>epc_boc_adv_table_exists</c>, <c>epc_boc_adv_scalar</c>,
/// <c>epc_boc_vendor_rollup</c>, <c>epc_boc_warehouse_rollup</c>, <c>epc_boc_channel_rollup</c>,
/// <c>epc_boc_collect_vendor</c>, <c>epc_boc_collect_warehouse</c>, <c>epc_boc_collect_channel</c>,
/// <c>epc_boc_adv_fleet_metrics</c>, <c>epc_boc_adv_tile</c>, <c>epc_boc_adv_rag_chip</c>,
/// <c>epc_boc_adv_yn</c>, <c>epc_boc_adv_hero</c>, <c>epc_boc_render_vendor_control</c>,
/// <c>epc_boc_render_warehouse_control</c>, <c>epc_boc_render_channel_control</c>,
/// <c>epc_boc_h</c>, <c>epc_boc_classify_tenant</c>, <c>epc_boc_type_label</c>.
/// </summary>
public static class PhpPlanQ1Surf
{
    public const string BocAdvancedPath = "content/general_pages/epc_boc_advanced.php";

    public static Func<IReadOnlyList<Dictionary<string, object?>>>? ListAll { get; set; }
    public static Func<Dictionary<string, object?>, SurfStore?>? TenantPdo { get; set; }
    public static Func<string, object?>? MarketplaceChannels { get; set; }
    public static Func<string, bool>? ArbitrageEnabled { get; set; }

    public sealed class SurfStore
    {
        public HashSet<string> Tables { get; } = new(StringComparer.Ordinal);
        public List<Dictionary<string, object?>> Suppliers { get; } = [];
        public List<Dictionary<string, object?>> Rfq { get; } = [];
        public List<Dictionary<string, object?>> Purchases { get; } = [];
        public List<Dictionary<string, object?>> Warehouses { get; } = [];
        public List<Dictionary<string, object?>> Items { get; } = [];
        public List<Dictionary<string, object?>> Stock { get; } = [];
        public List<Dictionary<string, object?>> Planning { get; } = [];
        public List<Dictionary<string, object?>> PosRegisters { get; } = [];
        public List<Dictionary<string, object?>> ApiClients { get; } = [];
        public bool ThrowOnQuery { get; set; }
        public bool ThrowOnShow { get; set; }
    }

    private static SurfStore Store { get; set; } = new();

    public static void UseStore(SurfStore store) => Store = store;

    public static void Reset()
    {
        ListAll = null;
        TenantPdo = null;
        MarketplaceChannels = null;
        ArbitrageEnabled = null;
        Store = new();
    }

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            0d => true,
            0f => true,
            0m => true,
            "" => true,
            "0" => true,
            JsonElement je when je.ValueKind is JsonValueKind.Null or JsonValueKind.False => true,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.GetDouble() == 0 => true,
            JsonElement je when je.ValueKind == JsonValueKind.String && (je.GetString() is "" or "0") => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static int PhpInt(object? value)
    {
        switch (value)
        {
            case null:
                return 0;
            case bool b:
                return b ? 1 : 0;
            case int n:
                return n;
            case long l:
                return (int)l;
            case double d:
                return (int)d;
            case decimal m:
                return (int)m;
            default:
                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                _ = int.TryParse(text, NumberStyles.Integer | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed);
                return parsed;
        }
    }

    private static double PhpFloat(object? value)
    {
        switch (value)
        {
            case null:
                return 0;
            case bool b:
                return b ? 1 : 0;
            case int n:
                return n;
            case long l:
                return l;
            case double d:
                return d;
            case float f:
                return f;
            case decimal m:
                return (double)m;
            default:
                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                _ = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed);
                return parsed;
        }
    }

    private static string PhpString(object? value)
        => value switch
        {
            null => "",
            bool b => b ? "1" : "",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };

    private static object? Field(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    /// <summary>PHP <c>epc_boc_h</c>: <c>htmlspecialchars(..., ENT_QUOTES, 'UTF-8')</c>.</summary>
    public static string EpcBocH(object? value)
        => ErpDocumentControlRender.H(PhpString(value));

    /// <summary>PHP <c>epc_boc_classify_tenant</c>.</summary>
    public static string EpcBocClassifyTenant(Dictionary<string, object?> tenant)
    {
        if (!PhpEmpty(Field(tenant, "is_demo")) || !PhpEmpty(Field(tenant, "is_demo_tenant")))
        {
            return "demo";
        }

        var ind = PhpString(Field(tenant, "industry_code") ?? Field(tenant, "industry")).ToLowerInvariant();
        if (ind is "erp_standalone" or "erp_only" || ind.StartsWith("erp", StringComparison.Ordinal))
        {
            return "erp_only";
        }

        return "commerce";
    }

    /// <summary>PHP <c>epc_boc_type_label</c>.</summary>
    public static string EpcBocTypeLabel(string type)
        => type switch
        {
            "demo" => "Demo",
            "erp_only" => "ERP-only",
            "commerce" => "Commerce",
            _ => type.Length == 0 ? "" : char.ToUpperInvariant(type[0]) + type[1..]
        };

    /// <summary>PHP <c>epc_boc_adv_money</c>.</summary>
    public static string EpcBocAdvMoney(double value, string cur = "AED")
    {
        var abs = Math.Abs(value);
        var sfx = "";
        var n = value;
        if (abs >= 1_000_000)
        {
            n = value / 1_000_000;
            sfx = "M";
        }
        else if (abs >= 1000)
        {
            n = value / 1000;
            sfx = "K";
        }

        var num = sfx == ""
            ? FreeToolsPhp.NumberFormat(n, 0)
            : FreeToolsPhp.NumberFormat(n, 1).TrimEnd('0').TrimEnd('.') + sfx;
        return cur + " " + num;
    }

    /// <summary>PHP <c>epc_boc_adv_table_exists</c>.</summary>
    public static bool EpcBocAdvTableExists(SurfStore db, string table)
    {
        try
        {
            if (db.ThrowOnShow)
            {
                throw new InvalidOperationException("show");
            }

            return db.Tables.Contains(table);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>PHP <c>epc_boc_adv_scalar</c>.</summary>
    public static double EpcBocAdvScalar(SurfStore db, string sql, IReadOnlyList<object?>? args = null, double fallback = 0)
    {
        try
        {
            if (db.ThrowOnQuery)
            {
                throw new InvalidOperationException("query");
            }

            var siteKey = args is { Count: > 0 } ? PhpString(args[0]) : "";
            if (sql.Contains("`epc_erp_suppliers`", StringComparison.Ordinal) && sql.Contains("`active`", StringComparison.Ordinal))
            {
                return db.Suppliers.Count(s => PhpInt(Field(s, "active")) == 1);
            }

            if (sql.Contains("`epc_erp_suppliers`", StringComparison.Ordinal))
            {
                return db.Suppliers.Count;
            }

            if (sql.Contains("`epc_scm_rfq`", StringComparison.Ordinal))
            {
                var open = new HashSet<string>(StringComparer.Ordinal) { "draft", "sent", "open", "responded" };
                return db.Rfq.Count(r => open.Contains(PhpString(Field(r, "status"))));
            }

            if (sql.Contains("`epc_erp_purchases`", StringComparison.Ordinal))
            {
                return db.Purchases.Sum(p => PhpFloat(Field(p, "total_amount")));
            }

            if (sql.Contains("`epc_erp_inv_warehouses`", StringComparison.Ordinal))
            {
                return db.Warehouses.Count(w => PhpInt(Field(w, "active")) == 1);
            }

            if (sql.Contains("`epc_erp_inv_items`", StringComparison.Ordinal))
            {
                return db.Items.Count(i => PhpInt(Field(i, "active")) == 1);
            }

            if (sql.Contains("`epc_scm_item_planning`", StringComparison.Ordinal))
            {
                var qoh = QtyByItem(db);
                return db.Planning.Count(p =>
                {
                    var item = PhpInt(Field(p, "item_id"));
                    var rp = PhpFloat(Field(p, "reorder_point"));
                    var q = qoh.TryGetValue(item, out var v) ? v : 0;
                    return rp > 0 && q > 0 && q <= rp;
                });
            }

            if (sql.Contains("HAVING SUM(`qty_on_hand`) <= 0", StringComparison.Ordinal))
            {
                return QtyByItem(db).Count(kv => kv.Value <= 0);
            }

            if (sql.Contains("`epc_erp_inv_stock`", StringComparison.Ordinal) && sql.Contains("avg_unit_cost", StringComparison.Ordinal))
            {
                return db.Stock.Sum(s => PhpFloat(Field(s, "qty_on_hand")) * PhpFloat(Field(s, "avg_unit_cost")));
            }

            if (sql.Contains("`epc_pos_registers`", StringComparison.Ordinal))
            {
                if (!db.Tables.Contains("epc_pos_registers"))
                {
                    throw new InvalidOperationException("missing registers");
                }

                return db.PosRegisters.Count;
            }

            if (sql.Contains("`epc_api_clients`", StringComparison.Ordinal))
            {
                return db.ApiClients.Count(c => PhpString(Field(c, "site_key")) == siteKey);
            }

            throw new InvalidOperationException("unhandled");
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static Dictionary<int, double> QtyByItem(SurfStore db)
    {
        var map = new Dictionary<int, double>();
        foreach (var row in db.Stock)
        {
            var item = PhpInt(Field(row, "item_id"));
            map[item] = map.TryGetValue(item, out var q) ? q + PhpFloat(Field(row, "qty_on_hand")) : PhpFloat(Field(row, "qty_on_hand"));
        }

        return map;
    }

    /// <summary>PHP <c>epc_boc_vendor_rollup</c>.</summary>
    public static Dictionary<string, object?> EpcBocVendorRollup(IReadOnlyList<Dictionary<string, object?>> per)
    {
        var tVendors = 0;
        var tActive = 0;
        var tRfq = 0;
        var tSpend = 0d;
        var reachable = 0;
        var rows = new List<Dictionary<string, object?>>();
        foreach (var p in per)
        {
            var ok = !PhpEmpty(Field(p, "ok"));
            if (ok)
            {
                reachable++;
            }

            tVendors += PhpInt(Field(p, "vendors"));
            tActive += PhpInt(Field(p, "active_vendors"));
            tRfq += PhpInt(Field(p, "rfq_open"));
            tSpend += PhpFloat(Field(p, "spend"));
            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = PhpString(Field(p, "site_key")),
                ["label"] = PhpString(Field(p, "label")),
                ["type"] = PhpString(Field(p, "type") ?? "commerce"),
                ["ok"] = ok,
                ["vendors"] = PhpInt(Field(p, "vendors")),
                ["active_vendors"] = PhpInt(Field(p, "active_vendors")),
                ["rfq_open"] = PhpInt(Field(p, "rfq_open")),
                ["spend"] = PhpFloat(Field(p, "spend")),
                ["currency"] = PhpString(Field(p, "currency") ?? "AED"),
                ["note"] = PhpString(Field(p, "note"))
            });
        }

        rows.Sort((a, b) => PhpInt(b["vendors"]).CompareTo(PhpInt(a["vendors"])));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["totals"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tenants"] = per.Count,
                ["reachable"] = reachable,
                ["vendors"] = tVendors,
                ["active_vendors"] = tActive,
                ["rfq_open"] = tRfq,
                ["spend"] = tSpend
            },
            ["rows"] = rows
        };
    }

    /// <summary>PHP <c>epc_boc_warehouse_rollup</c>.</summary>
    public static Dictionary<string, object?> EpcBocWarehouseRollup(IReadOnlyList<Dictionary<string, object?>> per)
    {
        var tWh = 0;
        var tValue = 0d;
        var tLow = 0;
        var tOut = 0;
        var tSkus = 0;
        var reachable = 0;
        var rows = new List<Dictionary<string, object?>>();
        foreach (var p in per)
        {
            var ok = !PhpEmpty(Field(p, "ok"));
            if (ok)
            {
                reachable++;
            }

            var wh = PhpInt(Field(p, "warehouses"));
            var low = PhpInt(Field(p, "low_stock"));
            var outOf = PhpInt(Field(p, "out_of_stock"));
            tWh += wh;
            tValue += PhpFloat(Field(p, "stock_value"));
            tLow += low;
            tOut += outOf;
            tSkus += PhpInt(Field(p, "skus"));
            var rag = "green";
            if (!ok || outOf > 0)
            {
                rag = "red";
            }
            else if (low > 0)
            {
                rag = "amber";
            }

            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = PhpString(Field(p, "site_key")),
                ["label"] = PhpString(Field(p, "label")),
                ["type"] = PhpString(Field(p, "type") ?? "commerce"),
                ["ok"] = ok,
                ["warehouses"] = wh,
                ["skus"] = PhpInt(Field(p, "skus")),
                ["stock_value"] = PhpFloat(Field(p, "stock_value")),
                ["low_stock"] = low,
                ["out_of_stock"] = outOf,
                ["currency"] = PhpString(Field(p, "currency") ?? "AED"),
                ["rag"] = rag,
                ["note"] = PhpString(Field(p, "note"))
            });
        }

        rows.Sort((a, b) => PhpFloat(b["stock_value"]).CompareTo(PhpFloat(a["stock_value"])));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["totals"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tenants"] = per.Count,
                ["reachable"] = reachable,
                ["warehouses"] = tWh,
                ["stock_value"] = tValue,
                ["low_stock"] = tLow,
                ["out_of_stock"] = tOut,
                ["skus"] = tSkus
            },
            ["rows"] = rows
        };
    }

    /// <summary>PHP <c>epc_boc_channel_rollup</c>.</summary>
    public static Dictionary<string, object?> EpcBocChannelRollup(IReadOnlyList<Dictionary<string, object?>> per)
    {
        var tChannels = 0;
        var tWeb = 0;
        var tPos = 0;
        var tApi = 0;
        var tMkt = 0;
        var tArb = 0;
        var reachable = 0;
        var rows = new List<Dictionary<string, object?>>();
        foreach (var p in per)
        {
            var ok = !PhpEmpty(Field(p, "ok"));
            if (ok)
            {
                reachable++;
            }

            var web = !PhpEmpty(Field(p, "web")) ? 1 : 0;
            var pos = !PhpEmpty(Field(p, "pos")) ? 1 : 0;
            var api = !PhpEmpty(Field(p, "api")) ? 1 : 0;
            var mkt = PhpInt(Field(p, "marketplaces"));
            var arb = !PhpEmpty(Field(p, "arbitrage")) ? 1 : 0;
            var count = web + pos + api + mkt;
            tChannels += count;
            tWeb += web;
            tPos += pos;
            tApi += api;
            tMkt += mkt;
            tArb += arb;
            rows.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = PhpString(Field(p, "site_key")),
                ["label"] = PhpString(Field(p, "label")),
                ["type"] = PhpString(Field(p, "type") ?? "commerce"),
                ["ok"] = ok,
                ["web"] = web != 0,
                ["pos"] = pos != 0,
                ["api"] = api != 0,
                ["marketplaces"] = mkt,
                ["arbitrage"] = arb != 0,
                ["channels"] = count,
                ["note"] = PhpString(Field(p, "note"))
            });
        }

        rows.Sort((a, b) => PhpInt(b["channels"]).CompareTo(PhpInt(a["channels"])));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["totals"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tenants"] = per.Count,
                ["reachable"] = reachable,
                ["channels"] = tChannels,
                ["web"] = tWeb,
                ["pos"] = tPos,
                ["api"] = tApi,
                ["marketplaces"] = tMkt,
                ["arbitrage"] = tArb
            },
            ["rows"] = rows
        };
    }

    /// <summary>PHP <c>epc_boc_collect_vendor</c>.</summary>
    public static Dictionary<string, object?> EpcBocCollectVendor(SurfStore db)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["vendors"] = 0,
            ["active_vendors"] = 0,
            ["rfq_open"] = 0,
            ["spend"] = 0d,
            ["has_erp"] = false
        };
        if (EpcBocAdvTableExists(db, "epc_erp_suppliers"))
        {
            output["has_erp"] = true;
            output["vendors"] = (int)EpcBocAdvScalar(db, "SELECT COUNT(*) FROM `epc_erp_suppliers`");
            output["active_vendors"] = (int)EpcBocAdvScalar(db, "SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `active` = 1");
        }

        if (EpcBocAdvTableExists(db, "epc_scm_rfq"))
        {
            output["rfq_open"] = (int)EpcBocAdvScalar(db, "SELECT COUNT(*) FROM `epc_scm_rfq` WHERE `status` IN ('draft','sent','open','responded')");
        }

        if (EpcBocAdvTableExists(db, "epc_erp_purchases"))
        {
            output["spend"] = EpcBocAdvScalar(db, "SELECT COALESCE(SUM(`total_amount`),0) FROM `epc_erp_purchases`");
        }

        return output;
    }

    /// <summary>PHP <c>epc_boc_collect_warehouse</c>.</summary>
    public static Dictionary<string, object?> EpcBocCollectWarehouse(SurfStore db)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["warehouses"] = 0,
            ["skus"] = 0,
            ["stock_value"] = 0d,
            ["low_stock"] = 0,
            ["out_of_stock"] = 0,
            ["has_erp"] = false
        };
        if (EpcBocAdvTableExists(db, "epc_erp_inv_warehouses"))
        {
            output["has_erp"] = true;
            output["warehouses"] = (int)EpcBocAdvScalar(db, "SELECT COUNT(*) FROM `epc_erp_inv_warehouses` WHERE `active` = 1");
        }

        if (EpcBocAdvTableExists(db, "epc_erp_inv_items"))
        {
            output["skus"] = (int)EpcBocAdvScalar(db, "SELECT COUNT(*) FROM `epc_erp_inv_items` WHERE `active` = 1");
        }

        if (EpcBocAdvTableExists(db, "epc_erp_inv_stock"))
        {
            output["stock_value"] = EpcBocAdvScalar(db, "SELECT COALESCE(SUM(`qty_on_hand` * `avg_unit_cost`),0) FROM `epc_erp_inv_stock`");
            output["out_of_stock"] = (int)EpcBocAdvScalar(db, "SELECT COUNT(*) FROM (SELECT `item_id` FROM `epc_erp_inv_stock` GROUP BY `item_id` HAVING SUM(`qty_on_hand`) <= 0) t");
            if (EpcBocAdvTableExists(db, "epc_scm_item_planning"))
            {
                output["low_stock"] = (int)EpcBocAdvScalar(
                    db,
                    "SELECT COUNT(*) FROM `epc_scm_item_planning` p "
                    + "JOIN (SELECT `item_id`, SUM(`qty_on_hand`) qoh FROM `epc_erp_inv_stock` GROUP BY `item_id`) s ON s.`item_id` = p.`item_id` "
                    + "WHERE p.`reorder_point` > 0 AND s.qoh > 0 AND s.qoh <= p.`reorder_point`");
            }
        }

        return output;
    }

    /// <summary>PHP <c>epc_boc_collect_channel</c>.</summary>
    public static Dictionary<string, object?> EpcBocCollectChannel(SurfStore? platformDb, SurfStore tenantDb, string siteKey, string type)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["web"] = false,
            ["pos"] = false,
            ["api"] = false,
            ["marketplaces"] = 0,
            ["arbitrage"] = false
        };
        output["web"] = type is "commerce" or "demo";
        if (EpcBocAdvTableExists(tenantDb, "epc_pos_registers") || EpcBocAdvTableExists(tenantDb, "epc_pos_sales"))
        {
            output["pos"] = EpcBocAdvScalar(tenantDb, "SELECT COUNT(*) FROM `epc_pos_registers`") != 0;
        }

        if (platformDb != null && EpcBocAdvTableExists(platformDb, "epc_api_clients"))
        {
            output["api"] = EpcBocAdvScalar(platformDb, "SELECT COUNT(*) FROM `epc_api_clients` WHERE `site_key` = ?", [siteKey]) != 0;
        }

        if (MarketplaceChannels != null && platformDb != null)
        {
            try
            {
                var ch = MarketplaceChannels(siteKey);
                var sell = SellList(ch);
                output["marketplaces"] = sell.Count;
            }
            catch (Exception)
            {
            }
        }

        if (ArbitrageEnabled != null && platformDb != null)
        {
            try
            {
                output["arbitrage"] = ArbitrageEnabled(siteKey);
            }
            catch (Exception)
            {
            }
        }

        return output;
    }

    private static IReadOnlyList<object?> SellList(object? channels)
    {
        if (channels is Dictionary<string, object?> map && map.TryGetValue("sell", out var sell))
        {
            return sell switch
            {
                IReadOnlyList<object?> list => list,
                System.Collections.IEnumerable e and not string => e.Cast<object?>().ToList(),
                _ => []
            };
        }

        return [];
    }

    /// <summary>PHP <c>epc_boc_adv_fleet_metrics</c>.</summary>
    public static List<Dictionary<string, object?>> EpcBocAdvFleetMetrics(
        SurfStore? platformDb,
        Func<SurfStore, Dictionary<string, object?>, Dictionary<string, object?>> collector)
    {
        _ = platformDb;
        var rows = new List<Dictionary<string, object?>>();
        if (ListAll == null)
        {
            return rows;
        }

        IReadOnlyList<Dictionary<string, object?>> tenants;
        try
        {
            tenants = ListAll();
        }
        catch (Exception)
        {
            return rows;
        }

        foreach (var tenant in tenants)
        {
            var siteKey = PhpString(Field(tenant, "site_key"));
            var type = EpcBocClassifyTenant(tenant);
            var label = PhpString(Field(tenant, "trade_name") ?? Field(tenant, "system_name") ?? siteKey);
            var row = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["site_key"] = siteKey,
                ["label"] = label,
                ["type"] = type,
                ["ok"] = false,
                ["note"] = ""
            };
            try
            {
                if (TenantPdo == null)
                {
                    throw new InvalidOperationException("connect");
                }

                var pdo = TenantPdo(tenant);
                if (pdo == null)
                {
                    row["note"] = "DB unreachable";
                    rows.Add(row);
                    continue;
                }

                var metrics = collector(pdo, tenant);
                row["ok"] = true;
                foreach (var kv in metrics)
                {
                    row[kv.Key] = kv.Value;
                }

                rows.Add(row);
            }
            catch (Exception)
            {
                row["note"] = "Collect error";
                rows.Add(row);
            }
        }

        return rows;
    }

    /// <summary>PHP <c>epc_boc_adv_tile</c>.</summary>
    public static string EpcBocAdvTile(string label, string value, string tone = "", string hint = "")
    {
        var cls = "epc-boc__tile" + (tone != "" ? " epc-boc__tile--" + tone : "");
        var h = hint != "" ? "<div class=\"epc-boc__tile-hint\">" + EpcBocH(hint) + "</div>" : "";
        return "<div class=\"" + cls + "\"><div class=\"epc-boc__tile-label\">" + EpcBocH(label)
            + "</div><div class=\"epc-boc__tile-val\">" + EpcBocH(value) + "</div>" + h + "</div>";
    }

    /// <summary>PHP <c>epc_boc_adv_rag_chip</c>.</summary>
    public static string EpcBocAdvRagChip(string rag)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["green"] = "OK",
            ["amber"] = "ATTENTION",
            ["red"] = "CRITICAL"
        };
        var label = map.TryGetValue(rag, out var known) ? known : rag.ToUpperInvariant();
        return "<span class=\"epc-boc__chip epc-boc__chip--" + EpcBocH(rag) + "\">" + EpcBocH(label) + "</span>";
    }

    /// <summary>PHP <c>epc_boc_adv_yn</c>.</summary>
    public static string EpcBocAdvYn(bool value)
        => value
            ? "<span class=\"epc-boc__chip epc-boc__chip--green\">on</span>"
            : "<span class=\"epc-boc__chip epc-boc__chip--type\" style=\"opacity:.45\">—</span>";

    /// <summary>PHP <c>epc_boc_adv_hero</c>.</summary>
    public static string EpcBocAdvHero(string badge, string icon, string title, string sub)
        => "<div class=\"epc-boc__hero\"><div>"
            + "<span class=\"epc-boc__env\" style=\"background:rgba(255,255,255,.15);color:#fff;border-color:rgba(255,255,255,.25)\">"
            + EpcBocH(badge) + "</span>"
            + "<h2><i class=\"fa " + EpcBocH(icon) + "\"></i> " + EpcBocH(title) + "</h2>"
            + "<p>" + EpcBocH(sub) + "</p>"
            + "</div></div>";

    /// <summary>PHP <c>epc_boc_render_vendor_control</c>.</summary>
    public static string EpcBocRenderVendorControl(SurfStore? db, string unusedBase, Dictionary<string, object?>? rollup = null)
    {
        _ = unusedBase;
        rollup ??= EpcBocVendorRollup(EpcBocAdvFleetMetrics(db, (pdo, _) => EpcBocCollectVendor(pdo)));
        var totals = (Dictionary<string, object?>)rollup["totals"]!;
        var rows = (List<Dictionary<string, object?>>)rollup["rows"]!;
        var html = EpcBocAdvHero("MULTI-VENDOR", "fa-truck", "Vendor & Sourcing Control", "Every supplier, RFQ and purchase commitment across the fleet — one sourcing spine.");
        html += "<div class=\"epc-boc__tiles\">";
        html += EpcBocAdvTile("Vendors", FreeToolsPhp.NumberFormat(PhpFloat(totals["vendors"]), 0));
        html += EpcBocAdvTile("Active", FreeToolsPhp.NumberFormat(PhpFloat(totals["active_vendors"]), 0), "green");
        html += EpcBocAdvTile("Open RFQs", FreeToolsPhp.NumberFormat(PhpFloat(totals["rfq_open"]), 0), "amber");
        html += EpcBocAdvTile("Purchase spend", EpcBocAdvMoney(PhpFloat(totals["spend"])));
        html += EpcBocAdvTile("Reachable units", PhpString(totals["reachable"]) + " / " + PhpString(totals["tenants"]));
        html += "</div>";
        html += "<div class=\"epc-boc__panel\"><div class=\"epc-boc__panel-h\"><i class=\"fa fa-list\"></i> By tenant</div>";
        html += "<table class=\"epc-boc__grid--metric\"><thead><tr><th>Tenant</th><th>Type</th><th>Vendors</th><th>Active</th><th>Open RFQs</th><th>Spend</th><th>Status</th></tr></thead><tbody>";
        foreach (var row in rows)
        {
            html += "<tr><td><strong>" + EpcBocH(row["label"]) + "</strong><br><code>" + EpcBocH(row["site_key"]) + "</code></td>";
            html += "<td>" + EpcBocH(EpcBocTypeLabel(PhpString(row["type"]))) + "</td>";
            html += "<td>" + FreeToolsPhp.NumberFormat(PhpFloat(row["vendors"]), 0) + "</td>";
            html += "<td>" + FreeToolsPhp.NumberFormat(PhpFloat(row["active_vendors"]), 0) + "</td>";
            html += "<td>" + FreeToolsPhp.NumberFormat(PhpFloat(row["rfq_open"]), 0) + "</td>";
            html += "<td>" + EpcBocH(EpcBocAdvMoney(PhpFloat(row["spend"]), PhpString(row["currency"]))) + "</td>";
            html += "<td>" + ((bool)row["ok"]!
                ? EpcBocAdvRagChip("green")
                : EpcBocAdvRagChip("red") + " <span style=\"color:#94a3b8\">" + EpcBocH(row["note"]) + "</span>") + "</td></tr>";
        }

        if (PhpEmpty(rows))
        {
            html += "<tr><td colspan=\"7\" style=\"color:#94a3b8\">No tenants in registry.</td></tr>";
        }

        html += "</tbody></table></div>";
        return html;
    }

    /// <summary>PHP <c>epc_boc_render_warehouse_control</c>.</summary>
    public static string EpcBocRenderWarehouseControl(SurfStore? db, string unusedBase, Dictionary<string, object?>? rollup = null)
    {
        _ = unusedBase;
        rollup ??= EpcBocWarehouseRollup(EpcBocAdvFleetMetrics(db, (pdo, _) => EpcBocCollectWarehouse(pdo)));
        var totals = (Dictionary<string, object?>)rollup["totals"]!;
        var rows = (List<Dictionary<string, object?>>)rollup["rows"]!;
        var html = EpcBocAdvHero("MULTI-WAREHOUSE", "fa-cubes", "Warehouse & Inventory Control", "Stock value, locations and replenishment risk across every tenant warehouse.");
        html += "<div class=\"epc-boc__tiles\">";
        html += EpcBocAdvTile("Warehouses", FreeToolsPhp.NumberFormat(PhpFloat(totals["warehouses"]), 0));
        html += EpcBocAdvTile("Stock value", EpcBocAdvMoney(PhpFloat(totals["stock_value"])));
        html += EpcBocAdvTile("SKUs", FreeToolsPhp.NumberFormat(PhpFloat(totals["skus"]), 0));
        html += EpcBocAdvTile("Low stock", FreeToolsPhp.NumberFormat(PhpFloat(totals["low_stock"]), 0), PhpFloat(totals["low_stock"]) > 0 ? "amber" : "green");
        html += EpcBocAdvTile("Out of stock", FreeToolsPhp.NumberFormat(PhpFloat(totals["out_of_stock"]), 0), PhpFloat(totals["out_of_stock"]) > 0 ? "red" : "green");
        html += "</div>";
        html += "<div class=\"epc-boc__panel\"><div class=\"epc-boc__panel-h\"><i class=\"fa fa-list\"></i> By tenant</div>";
        html += "<table class=\"epc-boc__grid--metric\"><thead><tr><th>Tenant</th><th>Type</th><th>Warehouses</th><th>SKUs</th><th>Stock value</th><th>Low</th><th>Out</th><th>Health</th></tr></thead><tbody>";
        foreach (var row in rows)
        {
            html += "<tr><td><strong>" + EpcBocH(row["label"]) + "</strong><br><code>" + EpcBocH(row["site_key"]) + "</code></td>";
            html += "<td>" + EpcBocH(EpcBocTypeLabel(PhpString(row["type"]))) + "</td>";
            html += "<td>" + FreeToolsPhp.NumberFormat(PhpFloat(row["warehouses"]), 0) + "</td>";
            html += "<td>" + FreeToolsPhp.NumberFormat(PhpFloat(row["skus"]), 0) + "</td>";
            html += "<td>" + EpcBocH(EpcBocAdvMoney(PhpFloat(row["stock_value"]), PhpString(row["currency"]))) + "</td>";
            html += "<td>" + FreeToolsPhp.NumberFormat(PhpFloat(row["low_stock"]), 0) + "</td>";
            html += "<td>" + FreeToolsPhp.NumberFormat(PhpFloat(row["out_of_stock"]), 0) + "</td>";
            var note = PhpString(row["note"]);
            html += "<td>" + EpcBocAdvRagChip(PhpString(row["rag"]))
                + (note != "" ? " <span style=\"color:#94a3b8\">" + EpcBocH(note) + "</span>" : "") + "</td></tr>";
        }

        if (PhpEmpty(rows))
        {
            html += "<tr><td colspan=\"8\" style=\"color:#94a3b8\">No tenants in registry.</td></tr>";
        }

        html += "</tbody></table></div>";
        return html;
    }

    /// <summary>PHP <c>epc_boc_render_channel_control</c>.</summary>
    public static string EpcBocRenderChannelControl(SurfStore? db, string unusedBase, Dictionary<string, object?>? rollup = null)
    {
        _ = unusedBase;
        rollup ??= EpcBocChannelRollup(EpcBocAdvFleetMetrics(db, (pdo, tenant) =>
        {
            var siteKey = PhpString(Field(tenant, "site_key"));
            var type = EpcBocClassifyTenant(tenant);
            return EpcBocCollectChannel(db, pdo, siteKey, type);
        }));
        var totals = (Dictionary<string, object?>)rollup["totals"]!;
        var rows = (List<Dictionary<string, object?>>)rollup["rows"]!;
        var html = EpcBocAdvHero("MULTICHANNEL · OMS", "fa-sitemap", "Channel & Order Control", "Every selling surface — web, POS, API and marketplaces — across the fleet.");
        html += "<div class=\"epc-boc__tiles\">";
        html += EpcBocAdvTile("Channels", FreeToolsPhp.NumberFormat(PhpFloat(totals["channels"]), 0));
        html += EpcBocAdvTile("Web", FreeToolsPhp.NumberFormat(PhpFloat(totals["web"]), 0), "green");
        html += EpcBocAdvTile("POS", FreeToolsPhp.NumberFormat(PhpFloat(totals["pos"]), 0));
        html += EpcBocAdvTile("API", FreeToolsPhp.NumberFormat(PhpFloat(totals["api"]), 0));
        html += EpcBocAdvTile("Marketplaces", FreeToolsPhp.NumberFormat(PhpFloat(totals["marketplaces"]), 0), "amber");
        html += EpcBocAdvTile("Arbitrage on", FreeToolsPhp.NumberFormat(PhpFloat(totals["arbitrage"]), 0));
        html += "</div>";
        html += "<div class=\"epc-boc__panel\"><div class=\"epc-boc__panel-h\"><i class=\"fa fa-list\"></i> By tenant</div>";
        html += "<table class=\"epc-boc__grid--metric\"><thead><tr><th>Tenant</th><th>Type</th><th>Web</th><th>POS</th><th>API</th><th>Marketplaces</th><th>Arbitrage</th><th>Channels</th></tr></thead><tbody>";
        foreach (var row in rows)
        {
            html += "<tr><td><strong>" + EpcBocH(row["label"]) + "</strong><br><code>" + EpcBocH(row["site_key"]) + "</code></td>";
            html += "<td>" + EpcBocH(EpcBocTypeLabel(PhpString(row["type"]))) + "</td>";
            html += "<td>" + EpcBocAdvYn((bool)row["web"]!) + "</td>";
            html += "<td>" + EpcBocAdvYn((bool)row["pos"]!) + "</td>";
            html += "<td>" + EpcBocAdvYn((bool)row["api"]!) + "</td>";
            html += "<td>" + FreeToolsPhp.NumberFormat(PhpFloat(row["marketplaces"]), 0) + "</td>";
            html += "<td>" + EpcBocAdvYn((bool)row["arbitrage"]!) + "</td>";
            html += "<td><strong>" + FreeToolsPhp.NumberFormat(PhpFloat(row["channels"]), 0) + "</strong></td></tr>";
        }

        if (PhpEmpty(rows))
        {
            html += "<tr><td colspan=\"8\" style=\"color:#94a3b8\">No tenants in registry.</td></tr>";
        }

        html += "</tbody></table></div>";
        return html;
    }
}
