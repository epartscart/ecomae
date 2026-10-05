using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live ASP.NET ports of the PHP read assistants:
/// <c>epc_ai_assistant_query</c> (<c>content/shop/finance/epc_erp_ai_assistant.php</c>) and
/// <c>epc_bos_ai_answer</c> + <c>epc_bos_intel_kpis</c> (<c>epc_bos_ai.php</c> / <c>epc_bos_intelligence.php</c>).
/// Read-only: every branch is SELECT-only; per-branch exceptions become the PHP "Error: …" answer.
/// PHP's dead <c>epc_erp_gl_pl_report</c> call in epc_bos_intel_kpis is dropped (its result is unused).
/// </summary>
public interface IErpAiReadService
{
    Task<ErpAiReadResult> AssistantQueryAsync(string question, string? hostIndustryCode, CancellationToken cancellationToken = default);
    Task<ErpAiReadResult> BosAiAnswerAsync(string question, long dateFrom, long dateTo, CancellationToken cancellationToken = default);
}

public sealed record ErpAiReadResult(bool Ok, string Message, string Kind, string Type, object? Data);

public sealed class ErpAiReadService : IErpAiReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpDashboardReadService _dashboard;
    private readonly TimeProvider _clock;

    public ErpAiReadService(IErpWriteConnectionFactory connections, IErpDashboardReadService dashboard, TimeProvider? clock = null)
    {
        _connections = connections;
        _dashboard = dashboard;
        _clock = clock ?? TimeProvider.System;
    }

    private static string Money(double v) => v.ToString("N2", CultureInfo.InvariantCulture) + " AED";
    private static string Num(double v, int d) => v.ToString("N" + d.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public async Task<ErpAiReadResult> AssistantQueryAsync(string question, string? hostIndustryCode, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(false, "TenantRegistry DB is not configured.", "text", "error", null);
        }

        var q = (question ?? string.Empty).Trim().ToLowerInvariant();
        if (q.Length == 0)
        {
            return new(true, "Please ask a question about your ERP data.", "text", "text", null);
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var isJw = await IsJewelleryTenantAsync(c, hostIndustryCode, cancellationToken).ConfigureAwait(false);

        // --- Inventory queries ---
        if (Rx(q, @"\b(gram|weight|stock)\b.*\b(inventory|items?)\b")
            || Rx(q, @"\b(inventory|items?)\b.*\b(gram|weight|stock)\b")
            || Rx(q, @"\bwhat.*(inventory|stock|items?)\b"))
        {
            var filters = new List<string>();
            if (Rx(q, "necklace")) filters.Add("i.name LIKE '%necklace%'");
            if (Rx(q, "bangle")) filters.Add("i.name LIKE '%bangle%'");
            if (Rx(q, "ring")) filters.Add("i.name LIKE '%ring%'");
            if (Rx(q, "earring")) filters.Add("i.name LIKE '%earring%'");
            if (Rx(q, "pendant")) filters.Add("i.name LIKE '%pendant%'");
            if (Rx(q, "diamond")) filters.Add("i.name LIKE '%diamond%' OR i.jw_metal_type = 'Diamond'");
            if (Rx(q, "gold")) filters.Add("i.jw_metal_type = 'Gold'");
            if (Rx(q, "silver")) filters.Add("i.jw_metal_type = 'Silver'");
            if (Rx(q, "platinum")) filters.Add("i.jw_metal_type = 'Platinum'");
            if (Rx(q, "pearl")) filters.Add("i.jw_metal_type = 'Pearl'");
            var where = filters.Count > 0 ? " AND (" + string.Join(" OR ", filters) + ")" : string.Empty;

            try
            {
                var rows = await RowsAsync(c,
                    "SELECT i.sku, i.name, i.jw_metal_type AS metal, i.jw_karat AS karat, "
                    + "i.jw_gross_wt AS gross_wt, i.jw_net_wt AS net_wt, "
                    + "COALESCE(s.jw_weight_on_hand, i.jw_gross_wt) AS stock_weight, "
                    + "i.sales_price, i.standard_cost "
                    + "FROM epc_erp_inv_items i "
                    + "LEFT JOIN epc_erp_inv_stock s ON s.item_id = i.id "
                    + "WHERE i.active = 1" + where + " "
                    + "ORDER BY i.jw_gross_wt DESC LIMIT 50",
                    cancellationToken).ConfigureAwait(false);

                double totalWt = 0, totalVal = 0;
                foreach (var r in rows) { totalWt += Dbl(r["stock_weight"]); totalVal += Dbl(r["standard_cost"]); }

                var filterDesc = filters.Count == 0
                    ? "all items"
                    : string.Join(", ", filters).Replace("i.name LIKE '%", string.Empty, StringComparison.Ordinal)
                        .Replace("%'", string.Empty, StringComparison.Ordinal)
                        .Replace("i.jw_metal_type = '", string.Empty, StringComparison.Ordinal)
                        .Replace("'", string.Empty, StringComparison.Ordinal)
                        .Replace(" OR ", ", ", StringComparison.Ordinal).Trim();

                var sb = new StringBuilder();
                sb.Append("Found **").Append(rows.Count).Append(" items** matching \"").Append(filterDesc).Append("\".\n\n")
                    .Append("Total stock weight: **").Append(Num(totalWt, 3)).Append(" grams**\n")
                    .Append("Total value: **AED ").Append(Num(totalVal, 2)).Append("**\n\n")
                    .Append("| SKU | Item | Metal | Karat | Weight (g) | Value (AED) |\n")
                    .Append("|-----|------|-------|-------|-----------|-------------|\n");
                foreach (var r in rows)
                {
                    sb.Append("| ").Append(r["sku"]).Append(" | ").Append(r["name"]).Append(" | ")
                        .Append(r["metal"]).Append(" | ").Append(r["karat"]).Append(" | ")
                        .Append(Num(Dbl(r["stock_weight"]), 3)).Append(" | ")
                        .Append(Num(Dbl(r["standard_cost"]), 2)).Append(" |\n");
                }
                return new(true, sb.ToString(), "table", "table", rows);
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                return new(true, "Error querying inventory: " + ex.Message, "error", "error", null);
            }
        }

        // --- Total stock weight ---
        if (Rx(q, @"\b(total|sum)\b.*\b(gold|metal|weight|stock)\b"))
        {
            try
            {
                var rows = await RowsAsync(c,
                    "SELECT i.jw_metal_type AS metal, i.jw_karat AS karat, "
                    + "SUM(COALESCE(s.jw_weight_on_hand, i.jw_gross_wt)) AS total_wt, "
                    + "SUM(i.standard_cost) AS total_val, COUNT(*) AS cnt "
                    + "FROM epc_erp_inv_items i "
                    + "LEFT JOIN epc_erp_inv_stock s ON s.item_id = i.id "
                    + "WHERE i.active = 1 AND i.jw_metal_type != '' "
                    + "GROUP BY i.jw_metal_type, i.jw_karat ORDER BY total_wt DESC",
                    cancellationToken).ConfigureAwait(false);

                var sb = new StringBuilder();
                sb.Append("**Stock Summary by Metal & Karat:**\n\n")
                    .Append("| Metal | Karat | Items | Total Weight (g) | Total Value (AED) |\n")
                    .Append("|-------|-------|-------|-----------------|-------------------|\n");
                foreach (var r in rows)
                {
                    sb.Append("| ").Append(r["metal"]).Append(" | ").Append(r["karat"]).Append(" | ").Append(r["cnt"])
                        .Append(" | ").Append(Num(Dbl(r["total_wt"]), 3)).Append(" | ")
                        .Append(Num(Dbl(r["total_val"]), 2)).Append(" |\n");
                }
                return new(true, sb.ToString(), "table", "table", rows);
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                return new(true, "Error: " + ex.Message, "error", "error", null);
            }
        }

        // --- Repair queries ---
        if (Rx(q, @"\b(repair|service|fix|workshop)\b"))
        {
            try
            {
                var rows = await RowsAsync(c,
                    "SELECT status, COUNT(*) AS cnt, SUM(estimated_cost) AS est_total "
                    + "FROM epc_erp_jw_repairs GROUP BY status "
                    + "ORDER BY FIELD(status,'received','in_progress','ready','delivered','invoiced')",
                    cancellationToken).ConfigureAwait(false);
                var total = 0;
                var sb = new StringBuilder("**Repair Jobs Summary:**\n\n| Status | Count | Est. Cost (AED) |\n|--------|-------|-----------------|\n");
                foreach (var r in rows)
                {
                    total += Convert.ToInt32(r["cnt"], CultureInfo.InvariantCulture);
                    sb.Append("| ").Append(Cap(r["status"]?.ToString() ?? string.Empty)).Append(" | ").Append(r["cnt"])
                        .Append(" | ").Append(Num(Dbl(r["est_total"]), 2)).Append(" |\n");
                }
                sb.Append("\nTotal repairs: **").Append(total).Append("**");
                return new(true, sb.ToString(), "table", "table", rows);
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                return new(true, "Error: " + ex.Message, "error", "error", null);
            }
        }

        // --- Sales / revenue queries ---
        if (Rx(q, @"\b(sales?|revenue|sold|selling|invoice)\b"))
        {
            try
            {
                var rows = await RowsAsync(c,
                    "SELECT so.so_no, so.title, so.total_amount, so.status, "
                    + "COALESCE(c.name, c2.name, '') AS customer, so.time_created "
                    + "FROM epc_erp_sales_orders so "
                    + "LEFT JOIN epc_erp_contacts c ON c.id = so.contact_id "
                    + "LEFT JOIN epc_erp_contacts c2 ON c2.linked_user_id = so.customer_user_id AND so.customer_user_id > 0 "
                    + "ORDER BY so.time_created DESC LIMIT 20",
                    cancellationToken).ConfigureAwait(false);
                double totalRev = 0;
                foreach (var r in rows) { totalRev += Dbl(r["total_amount"]); }
                var sb = new StringBuilder();
                sb.Append("**Sales Orders** (latest ").Append(rows.Count).Append("):\n")
                    .Append("Total revenue: **AED ").Append(Num(totalRev, 2)).Append("**\n\n")
                    .Append("| SO # | Customer | Title | Amount (AED) | Status |\n")
                    .Append("|------|----------|-------|-------------|--------|\n");
                foreach (var r in rows)
                {
                    var cust = r["customer"]?.ToString();
                    sb.Append("| ").Append(r["so_no"]).Append(" | ").Append(string.IsNullOrEmpty(cust) ? "—" : cust)
                        .Append(" | ").Append(r["title"]).Append(" | ")
                        .Append(Num(Dbl(r["total_amount"]), 2)).Append(" | ").Append(r["status"]).Append(" |\n");
                }
                return new(true, sb.ToString(), "table", "table", rows);
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                return new(true, "Error: " + ex.Message, "error", "error", null);
            }
        }

        // --- Purchase queries ---
        if (Rx(q, @"\b(purchase|po|procure|buy|bought|supplier)\b"))
        {
            try
            {
                var rows = await RowsAsync(c,
                    "SELECT po_no, title, total_amount, jw_metal_weight_gm, jw_karat, status, time_created "
                    + "FROM epc_erp_purchase_orders ORDER BY time_created DESC LIMIT 20",
                    cancellationToken).ConfigureAwait(false);
                double totalAmt = 0, totalWt = 0;
                foreach (var r in rows) { totalAmt += Dbl(r["total_amount"]); totalWt += Dbl(r["jw_metal_weight_gm"]); }
                var sb = new StringBuilder();
                sb.Append("**Purchase Orders** (latest ").Append(rows.Count).Append("):\n")
                    .Append("Total amount: **AED ").Append(Num(totalAmt, 2)).Append("**");
                if (totalWt > 0) { sb.Append(" | Total metal weight: **").Append(Num(totalWt, 3)).Append(" g**"); }
                sb.Append("\n\n| PO # | Title | Weight (g) | Karat | Amount (AED) | Status |\n")
                    .Append("|------|-------|-----------|-------|-------------|--------|\n");
                foreach (var r in rows)
                {
                    sb.Append("| ").Append(r["po_no"]).Append(" | ").Append(r["title"]).Append(" | ")
                        .Append(Num(Dbl(r["jw_metal_weight_gm"]), 3)).Append(" | ").Append(r["jw_karat"]).Append(" | ")
                        .Append(Num(Dbl(r["total_amount"]), 2)).Append(" | ").Append(r["status"]).Append(" |\n");
                }
                return new(true, sb.ToString(), "table", "table", rows);
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                return new(true, "Error: " + ex.Message, "error", "error", null);
            }
        }

        // --- Customer queries ---
        if (Rx(q, @"\b(customer|client|buyer)\b"))
        {
            try
            {
                var rows = await RowsAsync(c,
                    "SELECT c.name, c.phone, c.email, c.country, "
                    + "COUNT(so.id) AS order_count, SUM(so.total_amount) AS total_spent "
                    + "FROM epc_erp_contacts c "
                    + "LEFT JOIN epc_erp_sales_orders so ON so.customer_user_id = c.linked_user_id "
                    + "WHERE c.contact_type = 'customer' "
                    + "GROUP BY c.id ORDER BY total_spent DESC LIMIT 20",
                    cancellationToken).ConfigureAwait(false);
                var sb = new StringBuilder();
                sb.Append("**Customers** (").Append(rows.Count).Append(" found):\n\n")
                    .Append("| Name | Phone | Country | Orders | Total Spent (AED) |\n")
                    .Append("|------|-------|---------|--------|-------------------|\n");
                foreach (var r in rows)
                {
                    sb.Append("| ").Append(r["name"]).Append(" | ").Append(r["phone"]).Append(" | ").Append(r["country"])
                        .Append(" | ").Append(r["order_count"]).Append(" | ").Append(Num(Dbl(r["total_spent"]), 2)).Append(" |\n");
                }
                return new(true, sb.ToString(), "table", "table", rows);
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                return new(true, "Error: " + ex.Message, "error", "error", null);
            }
        }

        // --- Trial balance / GL queries ---
        if (Rx(q, @"\b(trial\s*balance|gl|general\s*ledger|journal|weight.*ledger)\b"))
        {
            if (isJw)
            {
                try
                {
                    var rows = await RowsAsync(c,
                        "SELECT account_code, account_name, "
                        + "SUM(weight_in) AS wt_in, SUM(weight_out) AS wt_out, "
                        + "SUM(value_debit) AS val_dr, SUM(value_credit) AS val_cr "
                        + "FROM epc_erp_jw_weight_ledger "
                        + "GROUP BY account_code, account_name ORDER BY account_code",
                        cancellationToken).ConfigureAwait(false);
                    var sb = new StringBuilder();
                    sb.Append("**Dual Trial Balance (Weight + Value):**\n\n")
                        .Append("| Account | Name | Wt In (g) | Wt Out (g) | Debit (AED) | Credit (AED) |\n")
                        .Append("|---------|------|----------|-----------|------------|-------------|\n");
                    foreach (var r in rows)
                    {
                        sb.Append("| ").Append(r["account_code"]).Append(" | ").Append(r["account_name"]).Append(" | ")
                            .Append(Num(Dbl(r["wt_in"]), 3)).Append(" | ").Append(Num(Dbl(r["wt_out"]), 3)).Append(" | ")
                            .Append(Num(Dbl(r["val_dr"]), 2)).Append(" | ").Append(Num(Dbl(r["val_cr"]), 2)).Append(" |\n");
                    }
                    return new(true, sb.ToString(), "table", "table", rows);
                }
                catch (Exception ex) when (ex is DbException or InvalidOperationException)
                {
                    return new(true, "Error: " + ex.Message, "error", "error", null);
                }
            }
            return new(true, "Trial balance data is available for jewellery industry tenants. Please ensure your industry profile is set to \"jewellery\".", "text", "text", null);
        }

        // --- Dashboard / overview ---
        if (Rx(q, @"\b(dashboard|overview|summary|kpi|status|how.*doing)\b"))
        {
            try
            {
                var inv = await RowAsync(c, "SELECT COUNT(*) AS cnt, SUM(standard_cost) AS val FROM epc_erp_inv_items WHERE active = 1", cancellationToken).ConfigureAwait(false);
                var so = await RowAsync(c, "SELECT COUNT(*) AS cnt, SUM(total_amount) AS val FROM epc_erp_sales_orders", cancellationToken).ConfigureAwait(false);
                var po = await RowAsync(c, "SELECT COUNT(*) AS cnt, SUM(total_amount) AS val FROM epc_erp_purchase_orders", cancellationToken).ConfigureAwait(false);
                long repCnt = 0;
                try { repCnt = await ErpDb.LongAsync(c, null, "SELECT COUNT(*) FROM epc_erp_jw_repairs WHERE status IN ('received','in_progress')", cancellationToken).ConfigureAwait(false); }
                catch (DbException) { }

                var sb = new StringBuilder();
                sb.Append("**ERP Dashboard Summary:**\n\n")
                    .Append("- Inventory: **").Append(inv?["cnt"] ?? 0).Append("** items, value **AED ").Append(Num(Dbl(inv?["val"]), 2)).Append("**\n")
                    .Append("- Sales Orders: **").Append(so?["cnt"] ?? 0).Append("**, total **AED ").Append(Num(Dbl(so?["val"]), 2)).Append("**\n")
                    .Append("- Purchase Orders: **").Append(po?["cnt"] ?? 0).Append("**, total **AED ").Append(Num(Dbl(po?["val"]), 2)).Append("**\n")
                    .Append("- Open Repairs: **").Append(repCnt).Append("**\n");

                if (isJw)
                {
                    try
                    {
                        var wt = await ErpDb.DecimalAsync(c, null,
                            "SELECT SUM(COALESCE(s.jw_weight_on_hand, i.jw_gross_wt)) FROM epc_erp_inv_items i LEFT JOIN epc_erp_inv_stock s ON s.item_id = i.id WHERE i.active = 1",
                            cancellationToken).ConfigureAwait(false);
                        sb.Append("- Total Stock Weight: **").Append(Num((double)wt, 3)).Append(" grams**\n");
                    }
                    catch (DbException) { }
                }
                return new(true, sb.ToString(), "text", "text", null);
            }
            catch (Exception ex) when (ex is DbException or InvalidOperationException)
            {
                return new(true, "Error: " + ex.Message, "error", "error", null);
            }
        }

        // --- Help / capabilities ---
        if (Rx(q, @"\b(help|what can you|capabilities|commands?|how to)\b"))
        {
            return new(true,
                "**I can help you with these ERP queries:**\n\n"
                + "- **Inventory:** \"What is our gram inventory with necklaces and bangles?\"\n"
                + "- **Stock summary:** \"Show total gold stock weight\"\n"
                + "- **Sales:** \"Show me our sales orders\" or \"What's our revenue?\"\n"
                + "- **Purchases:** \"Show purchase orders\" or \"What did we buy?\"\n"
                + "- **Repairs:** \"How many open repairs?\" or \"Repair status\"\n"
                + "- **Customers:** \"Who are our top customers?\"\n"
                + "- **Trial balance:** \"Show trial balance\" or \"GL summary\"\n"
                + "- **Dashboard:** \"Give me an overview\" or \"How are we doing?\"\n\n"
                + "Just type your question naturally — I'll query your ERP data and respond.",
                "text", "text", null);
        }

        // --- Fallback ---
        return new(true,
            "I understand you're asking about: **\"" + (question ?? string.Empty).Trim() + "\"**\n\n"
            + "I can help with inventory, sales, purchases, repairs, customers, and financial queries. "
            + "Try asking:\n"
            + "- \"What is our gram inventory with necklaces?\"\n"
            + "- \"Show me sales orders\"\n"
            + "- \"How many open repairs?\"\n"
            + "- \"Give me a dashboard overview\"",
            "text", "text", null);
    }

    public async Task<ErpAiReadResult> BosAiAnswerAsync(string question, long dateFrom, long dateTo, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(false, "TenantRegistry DB is not configured.", "help", "error", null);
        }

        var q = (question ?? string.Empty).Trim().ToLowerInvariant();
        if (q.Length == 0)
        {
            return new(true, "Ask me about revenue, cash, receivables, payables, margin, forecasts, or what to reorder.", "help", "text", null);
        }

        var kpis = await BosIntelKpisAsync(dateFrom, dateTo, cancellationToken).ConfigureAwait(false);
        var by = kpis.ToDictionary(k => k.Key, StringComparer.Ordinal);

        bool Has(params string[] words)
        {
            foreach (var w in words) { if (q.Contains(w, StringComparison.Ordinal)) { return true; } }
            return false;
        }

        if (Has("forecast", "predict", "next month", "projection") && Has("cash", "liquid"))
        {
            var cf = await CashflowForecastAsync(3, cancellationToken).ConfigureAwait(false);
            var parts = cf.Points.Select(p => p.Label + ": " + Money(p.ProjectedCash));
            return new(true,
                "Projected cash — " + string.Join("; ", parts) + "." + (cf.LiquidityAlert ? " ⚠ Cash is projected to go negative." : string.Empty),
                "cashflow_forecast", "data", cf);
        }
        if (Has("forecast", "predict", "next month", "projection", "trend") && Has("revenue", "sales"))
        {
            var fc = await RevenueForecastAsync(6, 3, cancellationToken).ConfigureAwait(false);
            var parts = fc.Forecast.Select(p => p.Label + ": " + Money(p.Value));
            return new(true,
                "Revenue forecast (" + fc.Confidence + " confidence) — " + string.Join("; ", parts)
                + ". Trend " + Money(fc.TrendPerMonth) + "/month.",
                "revenue_forecast", "data", fc);
        }
        if (Has("reorder", "restock", "stock out", "stockout", "run out", "buy", "purchase"))
        {
            var inv = await InventoryPredictionsAsync(90, 30, cancellationToken).ConfigureAwait(false);
            var top = inv.Take(5).ToList();
            if (top.Count == 0)
            {
                return new(true, "No items show enough demand to recommend a reorder yet.", "inventory", "text", null);
            }
            var parts = top.Select(i => i.Sku + " (" + i.DaysCover.ToString("0.#", CultureInfo.InvariantCulture) + "d cover, order " + i.RecommendQty.ToString("0.##", CultureInfo.InvariantCulture) + " " + i.Unit + ")");
            return new(true, "Reorder priorities — " + string.Join("; ", parts) + ".", "inventory", "data", top);
        }
        if (Has("recommend", "advice", "advise", "what should", "insight", "decision", "help me"))
        {
            var rec = await RecommendationsAsync(dateFrom, dateTo, cancellationToken).ConfigureAwait(false);
            var parts = rec.Take(4).Select(r => "[" + r.Severity.ToUpperInvariant() + "] " + r.Title + " → " + r.Action);
            return new(true, string.Join(" ", parts), "recommendations", "data", rec);
        }
        if (Has("revenue", "sales", "turnover"))
        {
            return new(true, "Revenue this period is " + Money(by.GetValueOrDefault("revenue")?.Value ?? 0) + ".", "kpi", "text", null);
        }
        if (Has("profit", "margin"))
        {
            return new(true, "Gross margin is " + Math.Round(by.GetValueOrDefault("gross_margin")?.Value ?? 0, 1).ToString("0.#", CultureInfo.InvariantCulture) + "%.", "kpi", "text", null);
        }
        if (Has("receivable", "owe me", "ar ", "debtor", "collect"))
        {
            return new(true, "Receivables outstanding: " + Money(by.GetValueOrDefault("ar")?.Value ?? 0) + " (DSO " + Math.Round(by.GetValueOrDefault("dso")?.Value ?? 0).ToString(CultureInfo.InvariantCulture) + " days).", "kpi", "text", null);
        }
        if (Has("payable", "i owe", "ap ", "creditor", "supplier"))
        {
            return new(true, "Payables outstanding: " + Money(by.GetValueOrDefault("ap")?.Value ?? 0) + " (DPO " + Math.Round(by.GetValueOrDefault("dpo")?.Value ?? 0).ToString(CultureInfo.InvariantCulture) + " days).", "kpi", "text", null);
        }
        if (Has("cash", "bank", "liquid"))
        {
            return new(true, "Cash & bank position: " + Money(by.GetValueOrDefault("cash")?.Value ?? 0) + ".", "kpi", "text", null);
        }
        if (Has("inventory", "stock", "warehouse"))
        {
            return new(true, "Inventory value: " + Money(by.GetValueOrDefault("inventory")?.Value ?? 0) + " (turnover " + (by.GetValueOrDefault("inv_turnover")?.Value ?? 0).ToString("0.#", CultureInfo.InvariantCulture) + "x).", "kpi", "text", null);
        }

        return new(true,
            "I can answer questions about revenue, margin, cash, receivables, payables, inventory, forecasts (revenue/cash), reorder priorities, and recommendations. Try: \"what should I do?\" or \"forecast cash flow\".",
            "help", "text", null);
    }

    // ---- epc_bos_intel_kpis ----

    public sealed record BosKpi(string Key, string Label, double Value, string Format, string Health, string Hint);

    private async Task<List<BosKpi>> BosIntelKpisAsync(long dateFrom, long dateTo, CancellationToken ct)
    {
        var dash = await DashAsync(dateFrom, dateTo, ct).ConfigureAwait(false);
        var invValue = await InventoryValuationTotalAsync(ct).ConfigureAwait(false);

        var revenue = Get(dash, "revenue_ex_vat");
        var purchases = Get(dash, "purchase_ex_vat");
        var profit = Get(dash, "profit_ex_vat");
        var ar = Get(dash, "customer_ledger_balance");
        var ap = Get(dash, "payable_balance");
        var cash = Get(dash, "cash_bank_total");

        var days = Math.Max(1, (int)Math.Round((dateTo - dateFrom) / 86400.0));
        var grossMargin = revenue > 0 ? profit / revenue * 100 : 0.0;
        var dso = revenue > 0 ? ar / (revenue / days) : 0.0;
        var dpo = purchases > 0 ? ap / (purchases / days) : 0.0;
        var invTurnover = invValue > 0 ? purchases / invValue : 0.0;
        var currentAssets = cash + ar + invValue;
        var currentRatio = ap > 0 ? currentAssets / ap : 0.0;

        static string Flag(double val, double good, double warn, bool higherBetter = true)
        {
            if (higherBetter)
            {
                if (val >= good) { return "good"; }
                if (val >= warn) { return "warn"; }
                return "bad";
            }
            if (val <= good) { return "good"; }
            if (val <= warn) { return "warn"; }
            return "bad";
        }

        return
        [
            new("revenue", "Revenue (period)", revenue, "money", revenue > 0 ? "good" : "warn", "Net sales excl. VAT"),
            new("gross_margin", "Gross margin %", grossMargin, "pct", Flag(grossMargin, 25, 12), "Profit / revenue"),
            new("dso", "DSO (days sales outstanding)", dso, "days", Flag(dso, 30, 60, false), "Lower is faster collection"),
            new("dpo", "DPO (days payable outstanding)", dpo, "days", Flag(dpo, 45, 20), "Supplier payment cycle"),
            new("inv_turnover", "Inventory turnover", invTurnover, "x", Flag(invTurnover, 4, 2), "Purchases / inventory value"),
            new("current_ratio", "Current ratio (approx)", currentRatio, "x", Flag(currentRatio, 1.5, 1.0), "(Cash+AR+Inv) / AP"),
            new("ar", "AR outstanding", ar, "money", "info", "Customer ledger balance"),
            new("ap", "AP outstanding", ap, "money", "info", "Supplier ledger balance"),
            new("cash", "Cash & bank", cash, "money", cash >= 0 ? "good" : "bad", "Liquidity position"),
            new("inventory", "Inventory value", invValue, "money", "info", "Stock at weighted-avg cost"),
        ];
    }

    private async Task<IReadOnlyDictionary<string, object?>> DashAsync(long fromUnix, long toUnix, CancellationToken ct)
    {
        var from = DateTimeOffset.FromUnixTimeSeconds(fromUnix).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = DateTimeOffset.FromUnixTimeSeconds(toUnix).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var res = await _dashboard.DashboardAsync(from, to, ct).ConfigureAwait(false);
        return res.Data;
    }

    private static double Get(IReadOnlyDictionary<string, object?> dash, string key)
        => dash.TryGetValue(key, out var v) && v is not null ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : 0.0;

    private async Task<double> InventoryValuationTotalAsync(CancellationToken ct)
    {
        try
        {
            await using var c = await _connections.OpenAsync(ct).ConfigureAwait(false);
            var v = await ErpDb.DecimalAsync(c, null,
                "SELECT SUM(`qty_on_hand` * `avg_unit_cost`) FROM `epc_erp_inv_stock` s INNER JOIN `epc_erp_inv_items` i ON i.id = s.item_id WHERE i.active = 1",
                ct).ConfigureAwait(false);
            return Math.Round((double)v, 2);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            return 0.0;
        }
    }

    // ---- epc_bos_ai_monthly_series / linreg / forecasts ----

    public sealed record MonthlyPoint(string Label, string Ym, long Start, double Revenue, double Purchases, double Profit);

    private async Task<List<MonthlyPoint>> MonthlySeriesAsync(int months, CancellationToken ct)
    {
        months = Math.Clamp(months, 2, 24);
        var stack = new List<MonthlyPoint>();
        var cursor = _clock.GetUtcNow().ToUnixTimeSeconds();
        for (var i = 0; i < months; i++)
        {
            var (s, e) = MonthBounds(cursor);
            var dash = await DashAsync(s, e, ct).ConfigureAwait(false);
            var dt = DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime;
            stack.Add(new MonthlyPoint(
                dt.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                dt.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                s,
                Math.Round(Get(dash, "revenue_ex_vat"), 2),
                Math.Round(Get(dash, "purchase_ex_vat"), 2),
                Math.Round(Get(dash, "profit_ex_vat"), 2)));
            cursor = s - 1;
        }
        stack.Reverse();
        return stack;
    }

    private static (long Start, long End) MonthBounds(long ts)
    {
        var d = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
        var start = new DateTimeOffset(d.Year, d.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1).AddSeconds(-1);
        return (start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds());
    }

    private static (double Slope, double Intercept) Linreg(IReadOnlyList<double> y)
    {
        var n = y.Count;
        if (n < 2) { return (0.0, n > 0 ? y[0] : 0.0); }
        double sx = 0, sy = 0, sxx = 0, sxy = 0;
        for (var i = 0; i < n; i++) { sx += i; sy += y[i]; sxx += (double)i * i; sxy += i * y[i]; }
        var denom = n * sxx - sx * sx;
        if (Math.Abs(denom) < 1e-9) { return (0.0, sy / n); }
        var slope = (n * sxy - sx * sy) / denom;
        var intercept = (sy - slope * sx) / n;
        return (slope, intercept);
    }

    public sealed record ForecastPoint(string Label, double Value);
    public sealed record RevenueForecast(IReadOnlyList<MonthlyPoint> Series, IReadOnlyList<ForecastPoint> Forecast, double TrendPerMonth, string Confidence);

    private async Task<RevenueForecast> RevenueForecastAsync(int history, int ahead, CancellationToken ct)
    {
        var series = await MonthlySeriesAsync(history, ct).ConfigureAwait(false);
        var rev = series.Select(r => r.Revenue).ToList();
        var (slope, intercept) = Linreg(rev);
        var n = rev.Count;
        var avg = n > 0 ? rev.Average() : 0.0;

        var forecast = new List<ForecastPoint>();
        var cursor = n > 0 ? series[n - 1].Start : _clock.GetUtcNow().ToUnixTimeSeconds();
        for (var k = 1; k <= ahead; k++)
        {
            var trend = intercept + slope * (n - 1 + k);
            var val = Math.Max(0.0, trend * 0.7 + avg * 0.3);
            cursor = AddMonth(cursor);
            forecast.Add(new(DateTimeOffset.FromUnixTimeSeconds(cursor).UtcDateTime.ToString("MMM yyyy", CultureInfo.InvariantCulture), Math.Round(val, 2)));
        }

        var conf = "low";
        if (n >= 3)
        {
            var mean = avg != 0 ? avg : 1;
            var var = rev.Select(v => Math.Pow(v - avg, 2)).Sum() / n;
            var cv = Math.Sqrt(var) / Math.Abs(mean);
            conf = cv < 0.15 ? "high" : cv < 0.4 ? "medium" : "low";
        }

        return new(series, forecast, Math.Round(slope, 2), conf);
    }

    private static long AddMonth(long ts)
    {
        var d = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
        var next = new DateTimeOffset(d.Year, d.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        var days = Math.Min(d.Day, DateTime.DaysInMonth(next.Year, next.Month));
        return new DateTimeOffset(next.Year, next.Month, days, d.Hour, d.Minute, d.Second, TimeSpan.Zero).ToUnixTimeSeconds();
    }

    public sealed record CashflowPoint(string Label, double Net, double ExpectedCollections, double ExpectedPayments, double ProjectedCash);
    public sealed record CashflowForecast(double OpeningCash, double Ar, double Ap, double AvgOperatingCash, IReadOnlyList<CashflowPoint> Points, double MinProjectedCash, bool LiquidityAlert);

    private async Task<CashflowForecast> CashflowForecastAsync(int ahead, CancellationToken ct)
    {
        var series = await MonthlySeriesAsync(6, ct).ConfigureAwait(false);
        var (s, e) = MonthBounds(_clock.GetUtcNow().ToUnixTimeSeconds());
        var dash = await DashAsync(s - 6L * 31 * 86400, e, ct).ConfigureAwait(false);
        var cash = Get(dash, "cash_bank_total");
        var ar = Get(dash, "customer_ledger_balance");
        var ap = Get(dash, "payable_balance");

        var profits = series.Select(r => r.Profit).ToList();
        var avgOp = profits.Count > 0 ? profits.Average() : 0.0;

        var points = new List<CashflowPoint>();
        var running = cash;
        var cursor = _clock.GetUtcNow().ToUnixTimeSeconds();
        for (var k = 1; k <= ahead; k++)
        {
            cursor = AddMonth(cursor);
            var collections = k == 1 ? ar * 0.6 : k == 2 ? ar * 0.3 : 0.0;
            var payments = k == 1 ? ap * 0.6 : k == 2 ? ap * 0.3 : 0.0;
            var net = avgOp + collections - payments;
            running += net;
            points.Add(new(DateTimeOffset.FromUnixTimeSeconds(cursor).UtcDateTime.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                Math.Round(net, 2), Math.Round(collections, 2), Math.Round(payments, 2), Math.Round(running, 2)));
        }
        var minCash = points.Count > 0 ? points.Min(p => p.ProjectedCash) : cash;
        return new(Math.Round(cash, 2), Math.Round(ar, 2), Math.Round(ap, 2), Math.Round(avgOp, 2), points, Math.Round(minCash, 2), minCash < 0);
    }

    public sealed record InventoryPrediction(long ItemId, string Sku, string Name, string Unit, double OnHand, double DailyUse, double DaysCover, double RecommendQty, double ReorderValue, string Status);

    private async Task<List<InventoryPrediction>> InventoryPredictionsAsync(int windowDays, int coverTargetDays, CancellationToken ct)
    {
        await using var c = await _connections.OpenAsync(ct).ConfigureAwait(false);
        var since = _clock.GetUtcNow().ToUnixTimeSeconds() - windowDays * 86400L;

        var consumption = new Dictionary<long, double>();
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "SELECT m.`item_id`, SUM(m.`qty`) AS used_qty FROM `epc_erp_inv_movements` m "
                + "WHERE m.`active` = 1 AND m.`movement_type` IN ('sale_out','transfer_out','return_out') AND m.`movement_date` >= ? "
                + "GROUP BY m.`item_id`");
            ErpDb.AddParameters(cmd, since);
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                consumption[Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture)] = Convert.ToDouble(r.GetValue(1), CultureInfo.InvariantCulture);
            }
        }

        var onHand = new Dictionary<long, double>();
        var avgCost = new Dictionary<long, double>();
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT `item_id`, SUM(`qty_on_hand`) AS qty, AVG(`avg_unit_cost`) AS cost FROM `epc_erp_inv_stock` GROUP BY `item_id`";
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                onHand[Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture)] = Convert.ToDouble(r.GetValue(1), CultureInfo.InvariantCulture);
                avgCost[Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture)] = Convert.ToDouble(r.GetValue(2), CultureInfo.InvariantCulture);
            }
        }

        var hasReorder = await ErpDb.LongAsync(c, null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'epc_erp_inv_items' AND COLUMN_NAME = 'reorder_level'"),
            ct).ConfigureAwait(false) > 0;
        var items = await RowsAsync(c,
            "SELECT `id`, `sku`, `name`, `unit`, " + (hasReorder ? "`reorder_level`" : "0 AS `reorder_level`") + " FROM `epc_erp_inv_items` WHERE `active` = 1",
            ct).ConfigureAwait(false);

        var output = new List<InventoryPrediction>();
        foreach (var it in items)
        {
            var id = Convert.ToInt64(it["id"], CultureInfo.InvariantCulture);
            var used = consumption.GetValueOrDefault(id);
            if (used <= 0) { continue; }
            var daily = used / Math.Max(1, windowDays);
            var have = onHand.GetValueOrDefault(id);
            var cover = daily > 0 ? have / daily : 9999;
            var targetQty = daily * coverTargetDays;
            var recommend = Math.Max(0.0, targetQty - have);
            var status = cover <= 7 ? "critical" : cover <= coverTargetDays ? "reorder" : "ok";
            output.Add(new(id,
                it["sku"]?.ToString() ?? string.Empty,
                it["name"]?.ToString() ?? string.Empty,
                it["unit"]?.ToString() is { Length: > 0 } u ? u : "pcs",
                Math.Round(have, 3), Math.Round(daily, 3), Math.Round(cover, 1),
                Math.Round(recommend, 2), Math.Round(recommend * avgCost.GetValueOrDefault(id), 2), status));
        }
        output.Sort((a, b) =>
        {
            static int Rank(string s) => s == "critical" ? 0 : s == "reorder" ? 1 : 2;
            var ra = Rank(a.Status); var rb = Rank(b.Status);
            return ra != rb ? ra.CompareTo(rb) : a.DaysCover.CompareTo(b.DaysCover);
        });
        return output;
    }

    public sealed record BosRecommendation(string Severity, string Title, string Action);

    private async Task<List<BosRecommendation>> RecommendationsAsync(long dateFrom, long dateTo, CancellationToken ct)
    {
        var kpis = await BosIntelKpisAsync(dateFrom, dateTo, ct).ConfigureAwait(false);
        var by = kpis.ToDictionary(k => k.Key, StringComparer.Ordinal);
        var rec = new List<BosRecommendation>();
        void Add(string sev, string title, string action) => rec.Add(new(sev, title, action));

        var dso = by.GetValueOrDefault("dso")?.Value ?? 0;
        var dpo = by.GetValueOrDefault("dpo")?.Value ?? 0;
        var gm = by.GetValueOrDefault("gross_margin")?.Value ?? 0;
        var cr = by.GetValueOrDefault("current_ratio")?.Value ?? 0;
        var invT = by.GetValueOrDefault("inv_turnover")?.Value ?? 0;
        var cash = by.GetValueOrDefault("cash")?.Value ?? 0;

        if (dso > 60) { Add("high", "Receivables are slow (DSO " + Math.Round(dso).ToString(CultureInfo.InvariantCulture) + " days)", "Prioritise collections on the oldest AR; consider deposits or early-payment discounts."); }
        else if (dso > 45) { Add("medium", "Collection cycle creeping up (DSO " + Math.Round(dso).ToString(CultureInfo.InvariantCulture) + " days)", "Send statements and chase invoices over 45 days."); }
        if (gm > 0 && gm < 15) { Add("high", "Gross margin is thin (" + Math.Round(gm, 1).ToString("0.#", CultureInfo.InvariantCulture) + "%)", "Review pricing and supplier costs on low-margin lines."); }
        if (cr > 0 && cr < 1.0) { Add("high", "Liquidity is tight (current ratio " + Math.Round(cr, 2).ToString("0.##", CultureInfo.InvariantCulture) + ")", "Defer discretionary spend; accelerate collections and arrange a buffer facility."); }
        if (invT > 0 && invT < 2) { Add("medium", "Inventory is turning slowly (" + Math.Round(invT, 1).ToString("0.#", CultureInfo.InvariantCulture) + "x)", "Identify slow movers; run a promotion or reduce reorder quantities."); }
        if (dpo > 0 && dpo < 20) { Add("low", "Paying suppliers very fast (DPO " + Math.Round(dpo).ToString(CultureInfo.InvariantCulture) + " days)", "Negotiate longer terms to keep cash in the business."); }
        if (cash < 0) { Add("high", "Cash & bank position is negative", "Review the cash-flow forecast and prioritise inflows this week."); }

        var fc = await RevenueForecastAsync(6, 3, ct).ConfigureAwait(false);
        if (fc.TrendPerMonth < 0)
        {
            Add("medium", "Revenue trend is declining (" + Money(fc.TrendPerMonth) + "/mo)", "Review pipeline and marketing; the 3-month forecast is trending down.");
        }
        var cff = await CashflowForecastAsync(3, ct).ConfigureAwait(false);
        if (cff.LiquidityAlert)
        {
            Add("high", "Projected cash dips below zero within 3 months", "Build a collections plan and stagger supplier payments to stay liquid.");
        }
        var inv = await InventoryPredictionsAsync(90, 30, ct).ConfigureAwait(false);
        var crit = inv.Count(i => i.Status == "critical");
        if (crit > 0)
        {
            Add("high", crit + " item(s) will stock out within ~7 days", "Raise purchase orders now for the critical items in the predictive list.");
        }
        if (rec.Count == 0)
        {
            Add("low", "No red flags detected", "Core KPIs are within healthy ranges for this period.");
        }
        rec.Sort((a, b) =>
        {
            static int Rank(string s) => s == "high" ? 0 : s == "medium" ? 1 : 2;
            return Rank(a.Severity).CompareTo(Rank(b.Severity));
        });
        return rec;
    }

    // ---- epc_jw_is_jewellery_tenant (company pack/code/name → erp_industry_profile → portal industry) ----

    private async Task<bool> IsJewelleryTenantAsync(DbConnection c, string? hostIndustryCode, CancellationToken ct)
    {
        try
        {
            var companyId = await ErpFinAdvancedCompany.ResolveAsync(c, 0, ct).ConfigureAwait(false);
            if (companyId > 0)
            {
                try
                {
                    var pack = await ErpDb.StringAsync(c, null,
                        ErpDb.Positional("SELECT `setting_value` FROM `epc_org_company_settings` WHERE `company_id` = ? AND `setting_key` = 'industry_pack' LIMIT 1"),
                        ct, companyId).ConfigureAwait(false);
                    if ((pack ?? string.Empty).Trim().StartsWith("jewellery", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (DbException) { }
                try
                {
                    var platform = await ErpDb.StringAsync(c, null,
                        "SELECT `setting_value` FROM `epc_erp_platform_settings` WHERE `setting_key` = 'active_industry_pack' LIMIT 1",
                        ct).ConfigureAwait(false);
                    if ((platform ?? string.Empty).Trim().StartsWith("jewellery", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch (DbException) { }
                await using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = ErpDb.Positional("SELECT `code`, `name` FROM `epc_erp_pm_legal_entities` WHERE `id` = ? LIMIT 1");
                    ErpDb.AddParameters(cmd, companyId);
                    await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                    if (await r.ReadAsync(ct).ConfigureAwait(false))
                    {
                        var code = (r.GetValue(0)?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                        var name = (r.GetValue(1)?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                        if (code == "jewel" || code.StartsWith("jewel", StringComparison.Ordinal)
                            || name.Contains("jewell", StringComparison.Ordinal) || name.Contains("jewel", StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch (DbException) { }

        try
        {
            var profile = await ErpDb.StringAsync(c, null,
                "SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = 'erp_industry_profile' LIMIT 1",
                ct).ConfigureAwait(false);
            if (string.Equals((profile ?? string.Empty).Trim(), "jewellery", StringComparison.Ordinal))
            {
                return true;
            }
        }
        catch (DbException) { }

        var portalIndustry = (hostIndustryCode ?? string.Empty).Trim().ToLowerInvariant();
        return portalIndustry == "jewellery" || portalIndustry.StartsWith("jewellery", StringComparison.Ordinal);
    }

    // ---- helpers ----

    private static bool Rx(string q, string pattern) => Regex.IsMatch(q, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static double Dbl(object? v) => v is null or DBNull ? 0.0 : Convert.ToDouble(v, CultureInfo.InvariantCulture);
    private static string Cap(string s) => string.Concat(s.Replace('_', ' ').Select((ch, i) => i == 0 ? char.ToUpperInvariant(ch) : ch));

    private static async Task<List<Dictionary<string, object?>>> RowsAsync(DbConnection c, string sql, CancellationToken ct)
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < r.FieldCount; i++)
            {
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            }
            rows.Add(row);
        }
        return rows;
    }

    private static async Task<Dictionary<string, object?>?> RowAsync(DbConnection c, string sql, CancellationToken ct)
    {
        var rows = await RowsAsync(c, sql, ct).ConfigureAwait(false);
        return rows.Count > 0 ? rows[0] : null;
    }
}
