using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Multi-company / multi-industry ERP host context (PHP parity helpers).
/// PHP resolves legal entities + per-company industry packs; ASP.NET chrome must
/// at least label the correct tenant/industry and expose company switch + PHP ERP.
/// </summary>
public static class ErpHostContext
{
    public sealed record Context(
        string Host,
        string WorkspaceTitle,
        string BrandLabel,
        string IndustryCode,
        string IndustryLabel,
        bool IsIndustryShowcase,
        bool IsProductTenant,
        string PhpErpShellHref);

    private static readonly IReadOnlyDictionary<string, (string Industry, string Label)> ProductTenantIndustry =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["epartscart.com"] = ("auto_parts", "Automotive spare parts"),
            ["electronicae.com"] = ("electronics", "Electronics & gaming"),
            ["stylenlook.com"] = ("fashion", "Fashion & beauty"),
            ["thejewellerytrend.com"] = ("jewellery", "Jewellery & luxury"),
            ["taxofinca.com"] = ("finance", "Tax & accounting"),
        };

    public static Context Resolve(string? host)
    {
        var normalized = NormalizeHost(host);
        var industryCode = StorefrontIndustryHostResolver.ResolveIndustryCode(normalized);
        var industryLabel = IndustryLabelFor(industryCode);
        var isShowcase = EcomaeIndustryShowcaseHosts.All.Any(h =>
            normalized.Equals(h.Slug + ".ecomae.com", StringComparison.OrdinalIgnoreCase));
        var isProduct = LiveTenantPresentationLock.IsProductTenantHost(normalized)
            || LiveTenantPresentationLock.IsProductTenantHost("www." + normalized);

        if (ProductTenantIndustry.TryGetValue(normalized, out var tenantIndustry))
        {
            industryCode = tenantIndustry.Industry;
            industryLabel = tenantIndustry.Label;
        }

        var brand = BrandLabelFor(normalized, isShowcase, isProduct);
        var title = $"ERP Finance — {brand}";
        return new Context(
            Host: normalized,
            WorkspaceTitle: title,
            BrandLabel: brand,
            IndustryCode: industryCode,
            IndustryLabel: industryLabel,
            IsIndustryShowcase: isShowcase,
            IsProductTenant: isProduct,
            PhpErpShellHref: "/php-reference/erp");
    }

    public static int? ActiveCompanyIdFromQuery(HttpRequest? request)
    {
        if (request is null)
        {
            return null;
        }

        if (request.Query.TryGetValue("company", out var companyVals)
            && int.TryParse(companyVals.FirstOrDefault(), out var company)
            && company > 0)
        {
            return company;
        }

        if (request.Query.TryGetValue("companyId", out var companyIdVals)
            && int.TryParse(companyIdVals.FirstOrDefault(), out var companyId)
            && companyId > 0)
        {
            return companyId;
        }

        return null;
    }

    /// <summary>
    /// PHP <c>epc_erp_gl_resolve_company_id</c>: requested <c>?company=</c> when it
    /// belongs to the tenant list, otherwise the lowest-id legal entity.
    /// Returns 0 when the tenant has no companies (unscoped / consolidated).
    /// </summary>
    public static int ResolveErpGlCompanyId(int? requestedCompanyId, IReadOnlyList<int> companyIds)
    {
        if (companyIds.Count == 0)
        {
            return 0;
        }

        if (requestedCompanyId is > 0)
        {
            foreach (var id in companyIds)
            {
                if (id == requestedCompanyId.Value)
                {
                    return id;
                }
            }
        }

        return companyIds[0];
    }

    /// <summary>
    /// PHP <c>epc_erp_gl_backfill_company_id</c> assigns <c>company_id = 0</c>
    /// journals to the lowest-id legal entity. Read path includes those rows
    /// only when the resolved company is that default (no writes).
    /// </summary>
    public static bool IncludeUnassignedGlJournals(int resolvedCompanyId, IReadOnlyList<int> companyIds)
        => resolvedCompanyId > 0 && companyIds.Count > 0 && resolvedCompanyId == companyIds[0];

    public static string SwitchCompanyHref(HttpRequest request, int companyId)
    {
        var path = request.Path.HasValue ? request.Path.Value! : "/erp";
        var query = new List<string>();
        foreach (var pair in request.Query)
        {
            if (pair.Key.Equals("company", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("companyId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in pair.Value)
            {
                query.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(value ?? string.Empty)}");
            }
        }

        query.Add($"company={companyId}");
        return path + "?" + string.Join('&', query);
    }

    private static string BrandLabelFor(string normalized, bool isShowcase, bool isProduct)
    {
        if (isShowcase)
        {
            var slug = normalized.EndsWith(".ecomae.com", StringComparison.Ordinal)
                ? normalized[..^".ecomae.com".Length]
                : normalized;
            var showcase = EcomaeIndustryShowcaseHosts.All.FirstOrDefault(h =>
                h.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            return showcase?.Title ?? slug;
        }

        var tenant = LiveTenantPresentationLock.Tenants.FirstOrDefault(t =>
            t.Hosts.Any(h => NormalizeHost(h).Equals(normalized, StringComparison.OrdinalIgnoreCase)));
        if (tenant is not null)
        {
            return tenant.Label;
        }

        if (normalized is "ecomae.com" or "cp.ecomae.com")
        {
            return "ECOM AE";
        }

        return isProduct ? normalized : "ECOM AE";
    }

    private static string IndustryLabelFor(string industryCode) => industryCode switch
    {
        "auto_parts" or "automotive" => "Automotive spare parts",
        "electronics" => "Electronics",
        "fashion" => "Fashion & apparel",
        "jewellery" => "Jewellery & luxury",
        "finance" => "Finance & tax",
        "food_beverage" => "Food & beverage",
        "it_software" => "Technology",
        "home_living" => "Home & living",
        _ => industryCode.Replace('_', ' '),
    };

    private static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return "localhost";
        }

        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (normalized.StartsWith("www.", StringComparison.Ordinal))
        {
            normalized = normalized[4..];
        }

        return normalized;
    }
}
