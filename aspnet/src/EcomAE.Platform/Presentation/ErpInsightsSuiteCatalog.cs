using System.Globalization;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Presentation;

/// <summary>PHP <c>epc_insights_suite_build</c> / <c>epc_insights_suite_render</c> for the ERP home.</summary>
public static class ErpInsightsSuiteCatalog
{
    public static ErpInsightsCommerceStats EmptyCommerce { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public static ErpInsightsSuite Build(
        ErpWorkspacePeriodKpis cur,
        ErpWorkspacePeriodKpis prev,
        ErpInsightsCommerceStats commerce,
        decimal arGrand,
        IReadOnlyList<decimal> arTotals,
        string currency,
        string periodFrom,
        string periodTo,
        bool autoParts)
    {
        var curCode = string.IsNullOrWhiteSpace(currency) ? "AED" : currency.Trim();
        var ar = cur.Receivables != 0 ? cur.Receivables : cur.ArBalance;
        var ap = cur.Payables != 0 ? cur.Payables : cur.ApBalance;
        var days = ErpNetsuiteDashboardCatalog.PeriodDaysInclusive(periodFrom, periodTo);
        var revenue = cur.RevenueExVat;
        var margin = revenue > 0.005m ? (cur.ProfitExVat / revenue) * 100m : 0m;
        var dso = revenue > 0 ? ar / (revenue / days) : 0m;
        var dpo = cur.PurchaseExVat > 0 ? ap / (cur.PurchaseExVat / days) : 0m;
        var currentRatio = ap > 0 ? (cur.CashPosition + ar + cur.StockValue) / ap : 0m;
        var wc = cur.CashPosition + ar + cur.StockValue - ap;
        var overduePct = OverduePct(arGrand, arTotals);
        var aov = commerce.OrdersWeek > 0 && revenue > 0
            ? revenue / Math.Max(1, cur.OrdersCount > 0 ? cur.OrdersCount : commerce.OrdersWeek)
            : 0m;

        var financial = new List<ErpInsightsCard>
        {
            Card("revenue", "Revenue (MTD)", revenue, "money", prev.RevenueExVat, true,
                revenue > 0 ? "good" : "warn",
                revenue > 0
                    ? "Period sales excl. VAT — compare to last month for momentum."
                    : "No MTD sales yet — check price lists and channel sync.",
                "/erp/report-center-app?tab=pl", "Open P&L", "fa-money"),
            Card("margin", "Gross margin", margin, "pct", null, true,
                Health(margin, 25, 12, true),
                margin >= 25 ? "Healthy margin for the period."
                    : margin >= 12 ? "Margin is soft — review cost and discounting."
                    : "Margin pressure — investigate COGS and pricing.",
                "/erp/report-center-app?tab=pl", "Review income statement", "fa-percent"),
            Card("cash", "Cash & bank", cur.CashPosition, "money", prev.CashPosition, true,
                cur.CashPosition >= 0 ? "good" : "bad",
                ap > 0
                    ? "Liquidity covers ~" + Math.Max(0, (int)Math.Round(cur.CashPosition / Math.Max(1m, ap / 30m))).ToString(CultureInfo.InvariantCulture) + " days of payables at current AP run-rate."
                    : "Cash position across bank accounts.",
                "/erp/cash-accounts-app", "Cash & bank", "fa-university"),
            Card("ar_dso", "Receivables / DSO", ar, "money", null, false,
                Health(dso, 30, 60, false),
                "DSO " + dso.ToString("N0", CultureInfo.InvariantCulture) + " days"
                    + (overduePct is { } pct ? " · " + pct.ToString("N0", CultureInfo.InvariantCulture) + "% of AR past due" : "")
                    + ". Faster collection lifts cash.",
                "/erp/aging-app", "AR aging", "fa-handshake-o"),
            Card("ap_dpo", "Payables / DPO", ap, "money", null, false,
                "info",
                "DPO " + dpo.ToString("N0", CultureInfo.InvariantCulture) + " days — balance supplier terms with cash preservation.",
                "/erp/aging-app", "AP aging", "fa-credit-card"),
            Card("working_capital", "Working capital (approx)", wc, "money", null, true,
                Health(currentRatio, 1.5m, 1.0m, true),
                "Current ratio ~" + currentRatio.ToString("N2", CultureInfo.InvariantCulture) + "x (cash + AR + inventory ÷ AP).",
                "/erp", "ERP home", "fa-balance-scale"),
        };

        var business = new List<ErpInsightsCard>
        {
            Card("orders_week", "Orders (7 days)", commerce.OrdersWeek, "number", commerce.OrdersPrevWeek, true,
                commerce.OrdersWeek >= commerce.OrdersPrevWeek ? "good" : "warn",
                commerce.OrdersPrevWeek > 0
                    ? "Volume " + (commerce.OrdersWeek >= commerce.OrdersPrevWeek ? "up" : "down") + " vs prior week."
                    : "Track weekly order velocity as your demand pulse.",
                "/cp/orders", "Open OMS", "fa-shopping-cart"),
            Card("orders_today", "Orders today", commerce.OrdersToday, "number", null, true,
                commerce.OrdersToday > 0 ? "good" : "info",
                commerce.OrdersToday > 0 ? "New demand landed today — keep fulfilment moving." : "No orders yet today.",
                "/cp/orders", "Orders", "fa-bolt"),
            Card("open_orders", "Open fulfilment", commerce.OpenOrders, "number", null, false,
                commerce.OpenOrders > 20 ? "warn" : commerce.OpenOrders > 0 ? "info" : "good",
                commerce.OpenOrders > 0
                    ? "Backlog needs attention — prioritise pick/pack/ship."
                    : "No open fulfilment backlog.",
                "/cp/fulfillment-queue-app", "Fulfilment queue", "fa-truck"),
            Card("aov", "Avg order value (MTD)", aov, "money", null, true,
                aov > 0 ? "info" : "warn",
                aov > 0 ? "Basket size indicator from period sales." : "AOV appears once orders post revenue.",
                "/cp/orders", "Orders", "fa-bar-chart"),
            Card("returns", "Open returns", commerce.ReturnsOpen, "number", null, false,
                commerce.ReturnsOpen > 5 ? "warn" : "info",
                commerce.ReturnsOpen > 0 ? "Resolve returns to protect margin and stock accuracy." : "Returns queue is clear.",
                "/cp/returns-rma-app", "Returns", "fa-undo"),
        };

        var cp = new List<ErpInsightsCard>
        {
            Card("catalogue", "Published products", commerce.Products, "number", null, true,
                commerce.Products > 0 ? "good" : "warn",
                commerce.Products > 0 ? "Catalogue depth available on the storefront." : "Publish products or sync price-list SKUs.",
                "/cp/product-catalogue-app", "Catalogue", "fa-cube"),
            Card("clients", "Customers", commerce.Clients, "number", null, true,
                commerce.Clients > 0 ? "good" : "info",
                "Active storefront / CRM customer base.",
                "/erp/receivables-app", "Customers", "fa-users"),
            Card("warehouses", "Warehouses", commerce.Warehouses, "number", null, true,
                commerce.Warehouses > 0 ? "good" : "warn",
                commerce.Warehouses > 0 ? "Logistics nodes ready for stock & delivery." : "Create warehouses for inventory & multivendor.",
                "/cp/storages-app", "Warehouses", "fa-industry"),
            Card("price_lists", "Price lists", commerce.PriceLists, "number", null, true,
                commerce.PriceLists > 0 ? "good" : "warn",
                commerce.PriceLists > 0 ? "Pricing channels configured." : "Upload commerce or multivendor prices.",
                "/cp/price-lists-app", "Price lists", "fa-tags"),
        };

        if (autoParts || commerce.VinOpen > 0)
        {
            cp.Add(Card("vin", "VIN / parts requests", commerce.VinOpen, "number", null, false,
                commerce.VinOpen > 0 ? "warn" : "good",
                commerce.VinOpen > 0 ? "Unread requests waiting for a quote response." : "No open VIN requests.",
                "/cp/quote-requests-app", "Requests", "fa-car"));
        }

        cp.Add(Card("sku_media", "SKU media ready", 1, "text", null, true, "info",
            "Enrich SKUs with photos & multi-type specs for higher conversion.",
            "/cp/product-catalogue-app", "SKU photos & specs", "fa-camera"));

        var alerts = BuildAlerts(financial, business, cp);
        return new(
            curCode,
            "MTD",
            periodFrom,
            periodTo,
            alerts,
            [
                new("financial", "Financial insights", "fa-line-chart", financial),
                new("business", "Business insights", "fa-briefcase", business),
                new("cp", "Control panel insights", "fa-th-large", cp),
            ]);
    }

    public static string FormatValue(ErpInsightsCard card, string currency)
        => card.Format switch
        {
            "text" => "Ready",
            "pct" => card.Value.ToString("N1", CultureInfo.InvariantCulture) + "%",
            "number" => card.Value.ToString("N0", CultureInfo.InvariantCulture),
            "days" => card.Value.ToString("N0", CultureInfo.InvariantCulture) + " d",
            _ => card.Value.ToString("N2", CultureInfo.InvariantCulture) + " " + currency,
        };

    public static double MeterWidth(ErpInsightsCard card)
    {
        if (card.Format == "pct")
        {
            return Math.Clamp((double)card.Value, 0, 100);
        }

        if (card.DeltaPct is { } pct)
        {
            return Math.Clamp(Math.Abs(pct), 0, 100);
        }

        return 28;
    }

    public static bool MeterGhost(ErpInsightsCard card)
        => card.Format != "pct" && card.DeltaPct is null;

    public static string DeltaCss(ErpInsightsCard card)
    {
        if (string.IsNullOrWhiteSpace(card.DeltaLabel)) return "";
        var up = card.DeltaPct is { } p ? p >= 0 : card.DeltaLabel == "new";
        return up == card.GoodWhenUp ? "is-good" : "is-bad";
    }

    public static bool DeltaUp(ErpInsightsCard card)
        => card.DeltaPct is { } p ? p >= 0 : card.DeltaLabel == "new";

    private static ErpInsightsCard Card(
        string key, string label, decimal value, string format, decimal? previous, bool goodUp,
        string health, string narrative, string href, string action, string icon)
    {
        string? deltaLabel = null;
        double? deltaPct = null;
        if (previous is { } prev)
        {
            if (Math.Abs((double)prev) > 0.0005)
            {
                deltaPct = ((double)(value - prev) / Math.Abs((double)prev)) * 100.0;
                deltaLabel = (deltaPct >= 0 ? "+" : "") + (deltaPct ?? 0).ToString("N1", CultureInfo.InvariantCulture) + "%";
            }
            else if (value != 0)
            {
                deltaLabel = "new";
                deltaPct = 100;
            }
        }

        return new(key, label, value, format, previous, deltaLabel, deltaPct, goodUp, health, narrative, href, action, icon);
    }

    private static IReadOnlyList<ErpInsightsAlert> BuildAlerts(
        IReadOnlyList<ErpInsightsCard> financial,
        IReadOnlyList<ErpInsightsCard> business,
        IReadOnlyList<ErpInsightsCard> cp)
    {
        var byKey = financial.Concat(business).Concat(cp).ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
        var alerts = new List<ErpInsightsAlert>();
        if (byKey.TryGetValue("ar_dso", out var ar) && (ar.Health is "warn" or "bad"))
        {
            alerts.Add(new("Collections focus", ar.Narrative, "/erp/aging-app", "warn"));
        }

        if (byKey.TryGetValue("margin", out var margin) && (margin.Health is "warn" or "bad"))
        {
            alerts.Add(new("Margin watch", margin.Narrative, "/erp/report-center-app?tab=pl", "warn"));
        }

        if (byKey.TryGetValue("open_orders", out var open) && open.Value > 0)
        {
            alerts.Add(new("Fulfilment backlog", open.Narrative, "/cp/fulfillment-queue-app", "info"));
        }

        if (byKey.TryGetValue("cash", out var cash) && cash.Value < 0)
        {
            alerts.Add(new("Negative cash", "Bank position is negative — reconcile and review outflows.", "/erp/cash-accounts-app", "bad"));
        }

        if (byKey.TryGetValue("price_lists", out var prices) && prices.Value <= 0)
        {
            alerts.Add(new("Pricing not ready", "No price lists yet — upload commerce or multivendor data.", "/cp/price-lists-app", "warn"));
        }

        return alerts.Take(4).ToList();
    }

    private static decimal? OverduePct(decimal arGrand, IReadOnlyList<decimal> arTotals)
    {
        if (arGrand <= 0 || arTotals.Count == 0) return arGrand <= 0 ? 0 : null;
        var overdue = 0m;
        for (var i = 1; i < arTotals.Count; i++) overdue += arTotals[i];
        return (overdue / arGrand) * 100m;
    }

    private static string Health(decimal value, decimal good, decimal warn, bool higherBetter)
    {
        if (higherBetter)
        {
            if (value >= good) return "good";
            if (value >= warn) return "warn";
            return "bad";
        }

        if (value <= good) return "good";
        if (value <= warn) return "warn";
        return "bad";
    }
}
