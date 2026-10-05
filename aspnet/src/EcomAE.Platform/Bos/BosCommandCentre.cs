using System.Globalization;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Presentation model for the BOS attention shell. It filters rows the ERP and CRM
/// reads already returned. It does not calculate a measure the source did not return,
/// and it does not render a missing measure as zero.
/// </summary>
public static class BosCommandCentreComposer
{
    public const int RowLimit = 40;

    public static readonly string[] Roles = Presentation.OperatingNavigationContext.Roles;

    /// <summary>Directive measures this shell does not invent.</summary>
    public static readonly string[] OmittedMeasures =
    [
        "EBITDA",
        "working capital",
        "treasury forecast",
        "supplier score",
        "capacity",
    ];

    public static string? NormalizeRole(string? role)
        => Presentation.OperatingNavigationContext.NormalizeRole(role);

    public static IReadOnlySet<string> CategoriesFor(string? role)
    {
        var normalized = NormalizeRole(role);
        string[] categories = normalized switch
        {
            "cfo" => ["Finance"],
            "sales" => ["Sales", "Relationship"],
            "purchasing" => ["Procurement"],
            "operations" => ["Sales", "Procurement", "Inventory"],
            _ => ["Sales", "Procurement", "Finance", "Inventory", "Relationship"],
        };
        return new HashSet<string>(categories, StringComparer.Ordinal);
    }

    public static BosCommandCentreView Project(
        Presentation.OperatingNavigationContext context,
        BosCommandCentreLoad load,
        bool canReadErp,
        bool canReadCrm)
    {
        if (!canReadErp && !canReadCrm)
        {
            return BosCommandCentreView.Denied(context, load.ReadAtUtc);
        }

        var categories = CategoriesFor(context.Role);
        var tenant = new List<BosCommandRow>();
        var company = new List<BosCommandRow>();
        var unread = new List<string>();
        BosSourceSlice? crm = null;

        foreach (var slice in load.Slices)
        {
            if (!CanRead(slice.Source, canReadErp, canReadCrm) || !categories.Contains(slice.Category))
            {
                continue;
            }

            if (slice.Source == "crm.opportunity")
            {
                crm = slice;
            }

            if (!slice.Available)
            {
                unread.Add(UnreadLabel(slice.Source));
                continue;
            }

            foreach (var document in slice.Rows)
            {
                if (document.CompanyId is int documentCompany && context.CompanyId is int active && documentCompany != active)
                {
                    continue;
                }

                var row = ToRow(document);
                if (document.CompanyId is > 0 && context.CompanyId is > 0)
                {
                    company.Add(row);
                }
                else if (document.CompanyId is not > 0)
                {
                    tenant.Add(row);
                }
                else if (context.CompanyId is not > 0)
                {
                    tenant.Add(row);
                }
            }
        }

        decimal? weighted = null;
        if (crm is { Available: true, Complete: true }
            && canReadCrm
            && categories.Contains("Relationship")
            && crm.Rows.Count > 0
            && crm.Rows.All(row => row.Amount is not null && row.Probability is not null))
        {
            var shown = tenant.Concat(company).Where(row => row.Source == "crm.opportunity").ToList();
            if (shown.Count == crm.Rows.Count)
            {
                decimal sum = 0;
                foreach (var row in crm.Rows)
                {
                    sum += row.Amount!.Value * row.Probability!.Value / 100m;
                }

                weighted = Math.Round(sum, 2, MidpointRounding.AwayFromZero);
            }
        }

        var relevant = load.Slices.Any(slice =>
            CanRead(slice.Source, canReadErp, canReadCrm) && categories.Contains(slice.Category) && slice.Available);
        var sourcesEmpty = relevant && tenant.Count == 0 && company.Count == 0 && weighted is null && unread.Count == 0;

        return new BosCommandCentreView(
            context,
            SignedIn: true,
            ReadDenied: false,
            load.DatabaseConfigured,
            load.ReadAtUtc,
            tenant,
            company,
            unread,
            weighted,
            sourcesEmpty);
    }

    public static string DrillHref(BosSourceDocument document)
    {
        var path = document.Source switch
        {
            "erp.sales_order" => "/erp/sales-orders-app",
            "erp.purchase_order" => "/erp/purchase-orders-app",
            "erp.gl_journal" => "/erp/gl-journals-app",
            "erp.stock" => "/erp/inventory-stock-app",
            "crm.opportunity" => "/cp/crm-opportunities-app",
            _ => string.Empty,
        };
        if (path.Length == 0)
        {
            return string.Empty;
        }

        if (document.CompanyId is > 0)
        {
            return path + "?company=" + document.CompanyId.Value.ToString(CultureInfo.InvariantCulture);
        }

        return path;
    }

    public static string? FormatAmount(decimal? amount)
        => amount is null ? null : amount.Value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static BosCommandRow ToRow(BosSourceDocument document)
        => new(
            document.Source,
            document.Category,
            document.Id,
            document.Reference,
            document.Title,
            document.Status,
            FormatAmount(document.Amount),
            DrillHref(document));

    private static bool CanRead(string source, bool canReadErp, bool canReadCrm)
    {
        if (source.StartsWith("erp.", StringComparison.Ordinal))
        {
            return canReadErp;
        }

        if (source.StartsWith("crm.", StringComparison.Ordinal))
        {
            return canReadCrm;
        }

        return false;
    }

    private static string UnreadLabel(string source) => source switch
    {
        "erp.sales_order" => "Sales orders could not be read.",
        "erp.purchase_order" => "Purchase orders could not be read.",
        "erp.gl_journal" => "Journals could not be read.",
        "erp.stock" => "Stock exceptions could not be read.",
        "crm.opportunity" => "Opportunities could not be read.",
        _ => "A source could not be read.",
    };
}

public sealed record BosSourceDocument(
    string Source,
    string Category,
    long Id,
    string Reference,
    string Title,
    string Status,
    decimal? Amount,
    int? Probability,
    int? CompanyId);

public sealed record BosSourceSlice(
    string Source,
    string Category,
    bool Available,
    bool Complete,
    IReadOnlyList<BosSourceDocument> Rows);

public sealed record BosCommandCentreLoad(
    bool DatabaseConfigured,
    DateTimeOffset? ReadAtUtc,
    IReadOnlyList<BosSourceSlice> Slices)
{
    public static BosCommandCentreLoad NotConfigured { get; } = new(false, null, []);
}

public sealed record BosCommandRow(
    string Source,
    string Category,
    long Id,
    string Reference,
    string Title,
    string Status,
    string? AmountText,
    string DrillHref);

public sealed record BosCommandCentreView(
    Presentation.OperatingNavigationContext Context,
    bool SignedIn,
    bool ReadDenied,
    bool DatabaseConfigured,
    DateTimeOffset? ReadAtUtc,
    IReadOnlyList<BosCommandRow> TenantRows,
    IReadOnlyList<BosCommandRow> CompanyRows,
    IReadOnlyList<string> Unread,
    decimal? WeightedPipeline,
    bool SourcesEmpty)
{
    public string? WeightedPipelineText
        => WeightedPipeline is null
            ? null
            : WeightedPipeline.Value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    public IReadOnlyList<string> DisplayedNumbers
    {
        get
        {
            var numbers = new List<string>();
            if (WeightedPipelineText is not null)
            {
                numbers.Add(WeightedPipelineText);
            }

            foreach (var row in TenantRows.Concat(CompanyRows))
            {
                if (row.AmountText is not null)
                {
                    numbers.Add(row.AmountText);
                }
            }

            return numbers;
        }
    }

    public static BosCommandCentreView Denied(Presentation.OperatingNavigationContext context, DateTimeOffset? readAt)
        => new(context, true, true, true, readAt, [], [], [], null, false);
}
