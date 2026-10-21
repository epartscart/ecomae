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

        // D365 parity: ?cmp= is the legal-entity selector on operations hosts
        // (tenant.operations.uae.ecomae.com/?cmp=indj). Named keys also work, so
        // ?cmp=asag and ?cmp=2 both resolve a legal entity.
        if (request.Query.TryGetValue("cmp", out var cmpVals)
            && int.TryParse(cmpVals.FirstOrDefault(), out var cmp)
            && cmp > 0)
        {
            return cmp;
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
    /// Resolve the active legal entity honouring <c>?company=</c>/<c>?companyId=</c> ids
    /// and the D365-style <c>?cmp=</c> selector — numeric values match the entity id,
    /// anything else matches the entity code (<c>cmp=indj</c>, <c>cmp=asag</c>).
    /// Falls back to the lowest-id entity, like PHP.
    /// </summary>
    public static ErpCompanyDigest? ActiveCompanyFromQuery(
        IReadOnlyList<ErpCompanyDigest>? companies,
        HttpRequest? request)
    {
        if (companies is null || companies.Count == 0)
        {
            return null;
        }

        var id = ActiveCompanyIdFromQuery(request);
        if (id is > 0)
        {
            return companies.FirstOrDefault(c => c.Id == id.Value) ?? companies[0];
        }

        var cmp = request?.Query.TryGetValue("cmp", out var cmpVals) == true
            ? cmpVals.ToString().Trim()
            : null;
        if (!string.IsNullOrWhiteSpace(cmp))
        {
            return companies.FirstOrDefault(c =>
                       string.Equals(c.Code, cmp, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(c.Name, cmp, StringComparison.OrdinalIgnoreCase))
                   ?? companies[0];
        }

        return companies[0];
    }

    /// <summary>
    /// Business-unit selector on operations hosts: <c>?bu=ASAPL-WSL</c> (or a BU id).
    /// Mirrors <see cref="ActiveCompanyFromQuery"/> — numeric matches the BU id,
    /// anything else matches the BU code or name. null when the entity has no BUs.
    /// </summary>
    public sealed record ErpBusinessUnitDigest(int Id, string Code, string Name, int EntityId);

    public static int? ActiveBusinessUnitIdFromQuery(HttpRequest? request)
    {
        if (request is null)
        {
            return null;
        }

        if (request.Query.TryGetValue("bu", out var buVals)
            && int.TryParse(buVals.FirstOrDefault(), out var bu)
            && bu > 0)
        {
            return bu;
        }

        if (request.Query.TryGetValue("businessUnit", out var buIdVals)
            && int.TryParse(buIdVals.FirstOrDefault(), out var buId)
            && buId > 0)
        {
            return buId;
        }

        return null;
    }

    public static ErpBusinessUnitDigest? ActiveBusinessUnitFromQuery(
        IReadOnlyList<ErpBusinessUnitDigest>? units,
        HttpRequest? request)
    {
        if (units is null || units.Count == 0)
        {
            return null;
        }

        var id = ActiveBusinessUnitIdFromQuery(request);
        if (id is > 0)
        {
            return units.FirstOrDefault(u => u.Id == id.Value) ?? units[0];
        }

        var bu = request?.Query.TryGetValue("bu", out var buVals) == true
            ? buVals.ToString().Trim()
            : null;
        if (!string.IsNullOrWhiteSpace(bu))
        {
            return units.FirstOrDefault(u =>
                       string.Equals(u.Code, bu, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(u.Name, bu, StringComparison.OrdinalIgnoreCase))
                   ?? units[0];
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

    /// <summary>
    /// Entity + BU switch link carrying BOTH the numeric id and the readable
    /// code: <c>company=&lt;id&gt;&amp;cmp=&lt;code&gt;</c> (and
    /// <c>bu=&lt;id&gt;&amp;buc=&lt;code&gt;</c> for a BU). Id consumers
    /// (ActiveCompanyIdFromQuery, trial-balance BU filters) keep resolving
    /// numerics while the URL still shows the entity code. Other query
    /// params are preserved; stale scope keys are dropped.
    /// </summary>
    public static string SwitchEntityHref(HttpRequest request, long entityId, string entityCode, int? buId = null, string? buCode = null)
    {
        var path = request.Path.HasValue ? request.Path.Value! : "/erp";
        var query = new List<string>();
        foreach (var pair in request.Query)
        {
            if (pair.Key.Equals("company", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("companyId", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("cmp", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("bu", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("buc", StringComparison.OrdinalIgnoreCase)
                || pair.Key.Equals("businessUnit", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in pair.Value)
            {
                query.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(value ?? string.Empty)}");
            }
        }

        query.Add($"company={entityId}");
        if (!string.IsNullOrWhiteSpace(entityCode))
        {
            query.Add($"cmp={Uri.EscapeDataString(entityCode)}");
        }
        if (buId is > 0)
        {
            query.Add($"bu={buId.Value}");
        }
        if (!string.IsNullOrWhiteSpace(buCode))
        {
            query.Add($"buc={Uri.EscapeDataString(buCode)}");
        }
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
