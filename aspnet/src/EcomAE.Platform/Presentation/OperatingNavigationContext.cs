using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Shared navigation context for CP, ERP, and the BOS attention shell.
/// A company is shown only when its id is in the caller's company list.
/// A requested id outside that list is rejected and is not displayed.
/// </summary>
public sealed record OperatingCompanyRef(int Id, string Code, string Name);

public sealed record OperatingNavigationContext(
    string Surface,
    string ProductJob,
    string JobStatement,
    string Host,
    string TenantLabel,
    int? CompanyId,
    string? CompanyCode,
    string? CompanyName,
    string ActiveCompanyLabel,
    string? UserLabel,
    string? Role,
    bool RequestedCompanyRejected)
{
    public const string JobAdministers = "administers";
    public const string JobOperates = "operates";
    public const string JobAttention = "attention";

    public const string StatementCp = "CP administers ECOM AE.";
    public const string StatementErp = "ERP operates the business.";
    public const string StatementBos = "BOS shows what needs attention.";

    public const string NoCompanyLabel = "No legal entity selected";

    public string ActiveCompanyIdText
        => CompanyId is > 0 ? CompanyId.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;

    public static OperatingNavigationContext Resolve(
        string surface,
        string? host,
        int? requestedCompanyId,
        IReadOnlyList<OperatingCompanyRef> companies,
        string? userLabel,
        string? role)
    {
        var normalized = NormalizeSurface(surface);
        var (job, statement) = JobFor(normalized);
        var hostContext = ErpHostContext.Resolve(host);
        var selected = SelectCompany(requestedCompanyId, companies);
        var rejected = requestedCompanyId is > 0 && selected is null;
        var label = selected is null
            ? NoCompanyLabel
            : FormatCompany(selected);

        return new OperatingNavigationContext(
            normalized,
            job,
            statement,
            hostContext.Host,
            hostContext.BrandLabel,
            selected?.Id,
            selected?.Code,
            selected?.Name,
            label,
            string.IsNullOrWhiteSpace(userLabel) ? null : userLabel.Trim(),
            NormalizeRole(role),
            rejected);
    }

    /// <summary>
    /// Returns the company when <paramref name="requestedCompanyId"/> is in
    /// <paramref name="companies"/>. Does not substitute another company.
    /// </summary>
    public static OperatingCompanyRef? SelectCompany(
        int? requestedCompanyId,
        IReadOnlyList<OperatingCompanyRef> companies)
    {
        if (requestedCompanyId is not > 0 || companies.Count == 0)
        {
            return null;
        }

        foreach (var company in companies)
        {
            if (company.Id == requestedCompanyId.Value)
            {
                return company;
            }
        }

        return null;
    }

    public static string FormatCompany(OperatingCompanyRef company)
    {
        var code = (company.Code ?? string.Empty).Trim();
        var name = (company.Name ?? string.Empty).Trim();
        if (code.Length == 0 && name.Length == 0)
        {
            return "Company " + company.Id.ToString(CultureInfo.InvariantCulture);
        }

        if (code.Length == 0)
        {
            return name;
        }

        if (name.Length == 0)
        {
            return code;
        }

        return code + " · " + name;
    }

    public static readonly string[] Roles = ["ceo", "cfo", "sales", "purchasing", "operations"];

    public static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        var value = role.Trim().ToLowerInvariant();
        return Roles.Contains(value, StringComparer.Ordinal) ? value : null;
    }

    public static string NormalizeSurface(string surface)
    {
        var value = (surface ?? string.Empty).Trim().ToLowerInvariant();
        return value switch
        {
            "cp" or "erp" or "bos" => value,
            _ => throw new ArgumentException("Surface must be cp, erp, or bos.", nameof(surface)),
        };
    }

    public static string AttentionHref(int? companyId, string? role)
    {
        var parts = new List<string>();
        if (companyId is > 0)
        {
            parts.Add("company=" + companyId.Value.ToString(CultureInfo.InvariantCulture));
        }

        var normalized = NormalizeRole(role);
        if (normalized is not null)
        {
            parts.Add("role=" + Uri.EscapeDataString(normalized));
        }

        return parts.Count == 0
            ? EcomAE.Platform.Routing.EcomAeRoutes.Attention
            : EcomAE.Platform.Routing.EcomAeRoutes.Attention + "?" + string.Join('&', parts);
    }

    public static string ClearCompanyHref(HttpRequest request)
    {
        var path = request.Path.HasValue ? request.Path.Value! : "/";
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

        return query.Count == 0 ? path : path + "?" + string.Join('&', query);
    }

    private static (string Job, string Statement) JobFor(string surface) => surface switch
    {
        "cp" => (JobAdministers, StatementCp),
        "erp" => (JobOperates, StatementErp),
        _ => (JobAttention, StatementBos),
    };
}
