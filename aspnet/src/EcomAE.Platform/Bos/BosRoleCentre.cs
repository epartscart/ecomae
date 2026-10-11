using System.Globalization;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Role-centre presentation over the same ERP/CRM slices the attention shell already reads.
/// A centre ranks and filters. It does not invent a measure the source did not return.
/// </summary>
public static class BosRoleCentreComposer
{
    public static readonly IReadOnlyList<string> Roles = OperatingNavigationContext.Roles;

    public static readonly IReadOnlyDictionary<string, BosRoleCentreSpec> Specs =
        new Dictionary<string, BosRoleCentreSpec>(StringComparer.Ordinal)
        {
            ["ceo"] = new(
                "ceo",
                "CEO",
                "Revenue through approvals — sourced exceptions only.",
                ["draft_sales_orders", "open_purchase_orders", "draft_journals", "stock_exceptions", "open_opportunities", "weighted_pipeline"],
                ["EBITDA", "working capital", "treasury forecast", "supplier score", "capacity", "cash", "receivables", "payables", "margin"]),
            ["cfo"] = new(
                "cfo",
                "CFO",
                "Cash through treasury — sourced finance exceptions only.",
                ["draft_journals"],
                ["cash", "working capital", "receivables", "payables", "treasury forecast"]),
            ["sales"] = new(
                "sales",
                "Sales",
                "Revenue through customer risk — sourced sales and CRM only.",
                ["draft_sales_orders", "open_opportunities", "weighted_pipeline"],
                ["margin", "customer risk"]),
            ["purchasing"] = new(
                "purchasing",
                "Purchasing",
                "Demand through stock-out risk — sourced procurement and stock only.",
                ["open_purchase_orders", "stock_exceptions"],
                ["demand", "supplier score", "lead time"]),
            ["operations"] = new(
                "operations",
                "Operations",
                "Orders through capacity — sourced order and stock exceptions only.",
                ["draft_sales_orders", "open_purchase_orders", "stock_exceptions"],
                ["capacity", "deliveries"]),
        };

    public static BosRoleCentreSpec SpecFor(string? role)
    {
        var normalized = OperatingNavigationContext.NormalizeRole(role) ?? "ceo";
        return Specs[normalized];
    }

    public static BosRoleCentreView Project(
        string? role,
        OperatingNavigationContext context,
        BosCommandCentreLoad load,
        BosCommandCentreView attention)
    {
        var spec = SpecFor(role);
        var rows = attention.TenantRows.Concat(attention.CompanyRows).ToList();
        var kpis = new List<BosRoleKpi>();
        foreach (var key in spec.SourcedKeys)
        {
            var kpi = BuildKpi(key, spec.Role, load, attention, rows, context.CompanyId);
            if (kpi is not null)
            {
                kpis.Add(kpi);
            }
        }

        return new BosRoleCentreView(spec.Role, spec.Title, spec.Job, attention, kpis, spec.NotSourcedLabels);
    }

    private static BosRoleKpi? BuildKpi(
        string key,
        string role,
        BosCommandCentreLoad load,
        BosCommandCentreView attention,
        IReadOnlyList<BosCommandRow> rows,
        int? companyId)
    {
        return key switch
        {
            "weighted_pipeline" => Weighted(attention, role),
            "draft_sales_orders" => FromSource("erp.sales_order", "Draft sales orders", "/erp/sales-orders-app", load, rows, companyId),
            "open_purchase_orders" => FromSource("erp.purchase_order", "Open purchase orders", "/erp/purchase-orders-app", load, rows, companyId),
            "draft_journals" => FromSource("erp.gl_journal", "Draft journals", "/erp/gl-journals-app", load, rows, companyId),
            "stock_exceptions" => FromSource("erp.stock", "Stock below reorder", "/erp/inventory-stock-app", load, rows, companyId),
            "open_opportunities" => FromSource("crm.opportunity", "Open opportunities", "/cp/crm-opportunities-app", load, rows, companyId),
            _ => null,
        };
    }

    private static BosRoleKpi? Weighted(BosCommandCentreView attention, string role)
    {
        if (role is not ("ceo" or "sales"))
        {
            return null;
        }

        if (attention.WeightedPipelineText is null)
        {
            return new BosRoleKpi(
                "weighted_pipeline",
                "Weighted pipeline",
                null,
                false,
                "CRM open opportunities were incomplete or unread.",
                "/cp/crm-board-app?tab=pipeline");
        }

        return new BosRoleKpi(
            "weighted_pipeline",
            "Weighted pipeline",
            attention.WeightedPipelineText,
            true,
            "Sum of open opportunity amount × probability.",
            "/cp/crm-board-app?tab=pipeline");
    }

    private static BosRoleKpi? FromSource(
        string source,
        string label,
        string path,
        BosCommandCentreLoad load,
        IReadOnlyList<BosCommandRow> projected,
        int? companyId)
    {
        var slice = load.Slices.FirstOrDefault(item => string.Equals(item.Source, source, StringComparison.Ordinal));
        if (slice is null || !slice.Available)
        {
            return new BosRoleKpi(
                source,
                label,
                null,
                false,
                "This source could not be read.",
                Href(path, companyId));
        }

        IReadOnlyList<BosCommandRow> shown = projected.Where(row => string.Equals(row.Source, source, StringComparison.Ordinal)).ToList();
        if (source == "erp.stock" && shown.Count == 0)
        {
            shown = FilterStock(slice, companyId);
        }

        decimal sum = 0;
        var hasAmount = false;
        foreach (var row in shown)
        {
            if (row.AmountText is not null
                && decimal.TryParse(row.AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                sum += amount;
                hasAmount = true;
            }
        }

        var value = hasAmount
            ? shown.Count.ToString(CultureInfo.InvariantCulture) + " · " + sum.ToString("#,##0.00", CultureInfo.InvariantCulture)
            : shown.Count.ToString(CultureInfo.InvariantCulture);

        return new BosRoleKpi(source, label, value, true, SourceNote(source), Href(path, companyId));
    }

    private static IReadOnlyList<BosCommandRow> FilterStock(BosSourceSlice slice, int? companyId)
    {
        var rows = new List<BosCommandRow>();
        foreach (var document in slice.Rows)
        {
            if (companyId is > 0 && document.CompanyId is int documentCompany && documentCompany != companyId)
            {
                continue;
            }

            rows.Add(new BosCommandRow(
                document.Source,
                document.Category,
                document.Id,
                document.Reference,
                document.Title,
                document.Status,
                BosCommandCentreComposer.FormatAmount(document.Amount),
                BosCommandCentreComposer.DrillHref(document)));
        }

        return rows;
    }

    private static string SourceNote(string source) => source switch
    {
        "erp.sales_order" => "Draft ERP sales orders.",
        "erp.purchase_order" => "Draft and pending ERP purchase orders.",
        "erp.gl_journal" => "Draft ERP journals.",
        "erp.stock" => "ERP stock at or below reorder.",
        "crm.opportunity" => "Open CRM opportunities.",
        _ => "Sourced ERP or CRM rows.",
    };

    private static string Href(string path, int? companyId)
        => companyId is > 0
            ? path + "?company=" + companyId.Value.ToString(CultureInfo.InvariantCulture)
            : path;
}

public sealed record BosRoleCentreSpec(
    string Role,
    string Title,
    string Job,
    IReadOnlyList<string> SourcedKeys,
    IReadOnlyList<string> NotSourcedLabels);

public sealed record BosRoleKpi(
    string Key,
    string Label,
    string? ValueText,
    bool Sourced,
    string SourceLabel,
    string DrillHref);

public sealed record BosRoleCentreView(
    string Role,
    string Title,
    string Job,
    BosCommandCentreView Attention,
    IReadOnlyList<BosRoleKpi> Kpis,
    IReadOnlyList<string> NotSourced);
