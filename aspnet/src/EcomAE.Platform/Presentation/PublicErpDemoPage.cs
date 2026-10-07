using System.Globalization;
using System.Net;
using System.Text;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Public read-only ERP demo. PHP <c>erp_demo_dashboard.php</c> plus
/// <c>epc_demo_kpis</c> in <c>epc_erp_demo.php</c>. Sample numbers only —
/// no ledger writes, no seed/clear.
/// </summary>
public static class PublicErpDemoPage
{
    public sealed record Industry(string Code, string Name);

    public sealed record Kpis(
        string Industry,
        string Company,
        string Currency,
        decimal Revenue,
        decimal Cogs,
        decimal GrossMargin,
        decimal GrossMarginPct,
        int Orders,
        int PaidOrders,
        int UnpaidOrders,
        decimal ArOutstanding,
        int Customers,
        int Products,
        decimal StockValue,
        IReadOnlyList<string> DocChain);

    private sealed record Product(string Sku, decimal Price, decimal Cost, int Stock);

    private sealed record Order(string Sku, int Qty, bool Paid);

    private sealed record Dataset(
        string Company,
        string Currency,
        IReadOnlyList<Product> Products,
        int Customers,
        IReadOnlyList<Order> Orders,
        IReadOnlyList<string> DocChain);

    public static readonly IReadOnlyList<Industry> Industries =
    [
        new("jewellery", "Jewellery & Bullion"),
        new("trading", "Trading / Import-Export"),
        new("construction", "Construction & Contracting"),
        new("retail", "Retail / POS"),
        new("manufacturing", "Manufacturing"),
    ];

    public static string NormalizeIndustry(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Industries[0].Code;
        }

        var code = new string(raw.Trim().ToLowerInvariant().Where(c => c is >= 'a' and <= 'z').ToArray());
        return Industries.Any(i => i.Code == code) ? code : Industries[0].Code;
    }

    public static Kpis Compute(string? industry)
    {
        var code = NormalizeIndustry(industry);
        var set = Datasets[code];
        var bySku = set.Products.ToDictionary(p => p.Sku, StringComparer.Ordinal);
        decimal revenue = 0, cost = 0, paidRevenue = 0, stockValue = 0;
        var paid = 0;
        var unpaid = 0;
        foreach (var product in set.Products)
        {
            stockValue += product.Cost * product.Stock;
        }

        foreach (var order in set.Orders)
        {
            if (!bySku.TryGetValue(order.Sku, out var product))
            {
                continue;
            }

            var line = product.Price * order.Qty;
            var lineCost = product.Cost * order.Qty;
            revenue += line;
            cost += lineCost;
            if (order.Paid)
            {
                paid++;
                paidRevenue += line;
            }
            else
            {
                unpaid++;
            }
        }

        var margin = revenue - cost;
        var pct = revenue > 0
            ? Math.Round(margin / revenue * 100m, 1, MidpointRounding.AwayFromZero)
            : 0m;
        return new Kpis(
            code,
            set.Company,
            set.Currency,
            Round2(revenue),
            Round2(cost),
            Round2(margin),
            pct,
            set.Orders.Count,
            paid,
            unpaid,
            Round2(revenue - paidRevenue),
            set.Customers,
            set.Products.Count,
            Round2(stockValue),
            set.DocChain);
    }

    public static string Html(string? industry)
    {
        var k = Compute(industry);
        var name = Industries.First(i => i.Code == k.Industry).Name;
        var ccy = k.Currency;
        var sb = new StringBuilder(12_000);
        sb.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Live ERP demo</title>
            <style>
            .epcdemo-wrap{--c:#fff;--b:#d6e2f5;--t:#0d1b2a;--m:#5b6b85;--a:#1a56db;--a2:#2b8fff;--up:#0f9d6b;--dn:#e23b54;background:#f3f7fd;color:var(--t);border-radius:14px;padding:22px;margin:0;font-family:system-ui,sans-serif}
            .epcdemo-bar{display:flex;flex-wrap:wrap;align-items:center;gap:12px;justify-content:space-between;margin-bottom:18px}
            .epcdemo-title{font-size:22px;font-weight:800;margin:0;color:var(--t)}
            .epcdemo-sub{color:var(--m);font-size:13px;margin-top:2px}
            .epcdemo-cred{background:var(--c);border:1px solid var(--b);border-radius:10px;padding:10px 14px;font-size:13px;color:var(--t)}
            .epcdemo-cred b{color:var(--a)}
            .epcdemo-picker{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:18px}
            .epcdemo-chip{display:inline-block;padding:8px 14px;border-radius:999px;border:1px solid var(--b);background:var(--c);color:var(--t);text-decoration:none;font-size:13px;font-weight:600}
            .epcdemo-chip.on{background:var(--a);border-color:var(--a);color:#fff}
            .epcdemo-kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:14px;margin-bottom:18px}
            .epcdemo-kpi{background:var(--c);border:1px solid var(--b);border-radius:12px;padding:16px 18px}
            .epcdemo-kpi .lab{color:var(--m);font-size:12px;text-transform:uppercase;letter-spacing:.04em;font-weight:700}
            .epcdemo-kpi .val{font-size:26px;font-weight:800;margin-top:6px}
            .epcdemo-kpi .delta{font-size:12px;margin-top:4px;color:var(--m)}
            .epcdemo-kpi .delta.up{color:var(--up)}
            .epcdemo-grid2{display:grid;grid-template-columns:1fr 1fr;gap:14px;margin-bottom:18px}
            @media(max-width:760px){.epcdemo-grid2{grid-template-columns:1fr}}
            .epcdemo-panel{background:var(--c);border:1px solid var(--b);border-radius:12px;padding:16px 18px}
            .epcdemo-panel h4{margin:0 0 12px;font-size:14px;font-weight:800}
            .epcdemo-chain{display:flex;flex-wrap:wrap;gap:8px;align-items:center}
            .epcdemo-step{background:#e9f1fc;border:1px solid var(--b);border-radius:8px;padding:7px 11px;font-size:12px;font-weight:600}
            .epcdemo-arrow{color:var(--a);font-weight:800}
            .epcdemo-cta{display:flex;flex-wrap:wrap;gap:10px;margin-top:6px}
            .epcdemo-btn{display:inline-block;padding:11px 18px;border-radius:10px;font-weight:700;text-decoration:none;font-size:14px}
            .epcdemo-btn.p{background:var(--a);color:#fff}
            .epcdemo-btn.s{background:var(--c);color:var(--a);border:1px solid var(--a)}
            body{margin:0;background:#f3f7fd}
            </style>
            </head>
            <body>
            <div class="epcdemo-wrap">
              <div class="epcdemo-bar">
                <div>
                  <h2 class="epcdemo-title">
            """);
        sb.Append(E(k.Company));
        sb.Append("""
            </h2>
                  <div class="epcdemo-sub">Live ERP demo &middot; 
            """);
        sb.Append(E(name));
        sb.Append("""
             &middot; sample data, no signup</div>
                </div>
                <div class="epcdemo-cred">Shared demo login: <b>demo@ecomae.com</b> / <b>demo1234</b> &middot; valid 3 days</div>
              </div>
              <div class="epcdemo-picker">
            """);
        foreach (var ind in Industries)
        {
            var on = ind.Code == k.Industry ? " on" : "";
            sb.Append("<a class=\"epcdemo-chip")
                .Append(on)
                .Append("\" href=\"/erp-demo?demo=1&amp;industry=")
                .Append(E(ind.Code))
                .Append("\">")
                .Append(E(ind.Name))
                .Append("</a>");
        }

        sb.Append("""
            </div>
              <div class="epcdemo-kpis">
            """);
        Kpi(sb, "Revenue", k.Revenue, ccy, "up", "Gross margin " + Pct(k.GrossMarginPct) + "%");
        Kpi(sb, "Gross margin", k.GrossMargin, ccy, "", "COGS " + Money(ccy, k.Cogs));
        Kpi(sb, "AR outstanding", k.ArOutstanding, ccy, "", k.UnpaidOrders + " unpaid invoices");
        Kpi(sb, "Stock value", k.StockValue, ccy, "", k.Products + " products");
        Kpi(sb, "Orders", k.Orders, "", "up", k.PaidOrders + " paid");
        Kpi(sb, "Customers", k.Customers, "", "", "active accounts");
        sb.Append("""
            </div>
              <div class="epcdemo-grid2">
                <div class="epcdemo-panel"><h4>Revenue &middot; COGS &middot; margin</h4><canvas id="epcDemoBar" height="170"></canvas></div>
                <div class="epcdemo-panel"><h4>Paid vs outstanding orders</h4><canvas id="epcDemoPie" height="170"></canvas></div>
              </div>
            """);
        if (k.DocChain.Count > 0)
        {
            sb.Append("<div class=\"epcdemo-panel\" style=\"margin-bottom:18px\"><h4>Document workflow (")
                .Append(E(name))
                .Append(")</h4><div class=\"epcdemo-chain\">");
            for (var i = 0; i < k.DocChain.Count; i++)
            {
                sb.Append("<span class=\"epcdemo-step\">").Append(E(k.DocChain[i])).Append("</span>");
                if (i < k.DocChain.Count - 1)
                {
                    sb.Append("<span class=\"epcdemo-arrow\">&rarr;</span>");
                }
            }

            sb.Append("</div></div>");
        }

        sb.Append("""
              <div class="epcdemo-cta">
                <a class="epcdemo-btn p" href="/?demo=1">View storefront demo</a>
                <a class="epcdemo-btn s" href="/erp">Open full ERP (sign in)</a>
                <a class="epcdemo-btn s" href="/platform/demo">Get your own 3-day demo</a>
              </div>
            </div>
            <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
            <script>
            (function(){
              function draw(){
                if(typeof Chart==='undefined') return;
                var b=document.getElementById('epcDemoBar');
                if(b) new Chart(b,{type:'bar',data:{labels:['Revenue','COGS','Margin'],datasets:[{data:[
            """);
        sb.Append(N(k.Revenue)).Append(',').Append(N(k.Cogs)).Append(',').Append(N(k.GrossMargin));
        sb.Append("""
            ],backgroundColor:['#1a56db','#2b8fff','#0f9d6b'],borderRadius:6}]},options:{plugins:{legend:{display:false}},scales:{y:{beginAtZero:true}}}});
                var p=document.getElementById('epcDemoPie');
                if(p) new Chart(p,{type:'doughnut',data:{labels:['Paid','Outstanding'],datasets:[{data:[
            """);
        sb.Append(k.PaidOrders).Append(',').Append(k.UnpaidOrders);
        sb.Append("""
            ],backgroundColor:['#0f9d6b','#e23b54']}]},options:{plugins:{legend:{position:'bottom'}}}});
              }
              if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',draw);}else{draw();}
            })();
            </script>
            </body></html>
            """);
        return sb.ToString();
    }

    private static void Kpi(StringBuilder sb, string label, decimal value, string currency, string deltaClass, string delta)
    {
        var prefix = currency.Length == 0 ? "" : currency + " ";
        var shown = currency.Length == 0
            ? ((int)value).ToString(CultureInfo.InvariantCulture)
            : Money(currency, value);
        sb.Append("<div class=\"epcdemo-kpi\"><div class=\"lab\">")
            .Append(E(label))
            .Append("</div><div class=\"val epcdemo-count\" data-to=\"")
            .Append(N(value))
            .Append('"');
        if (prefix.Length > 0)
        {
            sb.Append(" data-prefix=\"").Append(E(prefix)).Append('"');
        }

        sb.Append('>').Append(E(shown)).Append("</div><div class=\"delta");
        if (deltaClass.Length > 0)
        {
            sb.Append(' ').Append(deltaClass);
        }

        sb.Append("\">").Append(E(delta)).Append("</div></div>");
    }

    private static string Money(string ccy, decimal value)
        => ccy + " " + value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Pct(decimal value)
        => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string N(decimal value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string E(string value) => WebUtility.HtmlEncode(value);

    private static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static readonly Dictionary<string, Dataset> Datasets = new(StringComparer.Ordinal)
    {
        ["jewellery"] = new(
            "Al Noor Jewellers LLC",
            "AED",
            [new("GR-22K-01", 1800, 1500, 12), new("DP-01", 5200, 4100, 4), new("SB-925-01", 320, 210, 30)],
            3,
            [new("GR-22K-01", 2, true), new("DP-01", 1, false), new("SB-925-01", 5, true)],
            ["Purchase Requisition", "LPO/PO", "GRN (weight+purity)", "Job/Making Order", "Hallmark/QC", "Quotation", "SO", "DO", "Tax Invoice", "Receipt"]),
        ["trading"] = new(
            "Gulf Spare Imports FZE",
            "AED",
            [new("BRK-PAD-01", 220, 140, 180), new("OIL-FLT-01", 45, 22, 600), new("ALT-12V-01", 780, 560, 35)],
            3,
            [new("BRK-PAD-01", 20, true), new("ALT-12V-01", 3, true), new("OIL-FLT-01", 100, false)],
            ["PR", "RFQ", "PO", "LC", "Bill of Entry/Customs", "GRN", "Landed Cost", "Purchase Invoice", "SO", "DO", "Tax Invoice", "Receipt"]),
        ["construction"] = new(
            "Emirates BuildCo Contracting",
            "AED",
            [new("RMC-G40", 280, 210, 0), new("RBR-T12", 2600, 2200, 40), new("LBR-DAY", 180, 120, 0)],
            3,
            [new("RMC-G40", 120, false), new("RBR-T12", 8, true), new("LBR-DAY", 60, false)],
            ["BOQ", "Contract", "Subcontract/PO", "Material Requisition", "GRN", "Progress Claim (IPC)", "Retention", "Certification", "Tax Invoice", "Receipt"]),
        ["retail"] = new(
            "QuickMart Retail LLC",
            "AED",
            [new("BEV-COLA", 3, 1.6m, 5000), new("SNK-CHIP", 5, 2.8m, 3000), new("GRC-RICE", 38, 27, 800)],
            3,
            [new("BEV-COLA", 240, true), new("GRC-RICE", 10, true), new("SNK-CHIP", 200, true)],
            ["Shift Open", "POS Sale", "Tender (cash/card)", "Receipt", "Z-Report/Day-Close", "Stock Replenishment"]),
        ["manufacturing"] = new(
            "Sharjah Plastics Mfg",
            "AED",
            [new("FG-CRATE", 24, 15, 1200), new("FG-PIPE", 56, 38, 400), new("FG-SHEET", 130, 92, 150)],
            3,
            [new("FG-CRATE", 300, true), new("FG-PIPE", 80, false), new("FG-SHEET", 25, true)],
            ["Sales Forecast", "BOM", "Work Order", "Material Issue", "WIP", "FG Receipt", "QC", "SO", "DO", "Tax Invoice", "Receipt"]),
    };
}
