using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Super CP tenant control center — typed twin of PHP <c>epc_portal_tenant_control_list_all()</c>
/// (<c>content/general_pages/epc_portal_tenant_control.php</c>) as rendered by
/// <c>cp/content/control/portal/epc_tenant_control_center.php</c>.
/// Plaintext operator passwords are never carried in the list payload, exactly like PHP.
/// </summary>
public interface ICpTenantControlCenterService
{
    Task<CpTenantControlCenterView> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed record CpTenantControlRow(
    string SiteKey,
    string Hostname,
    string TradeName,
    string TenantType,
    string TypeLabel,
    string BadgeClass,
    string DbName,
    string ErpPackLabel,
    string Status,
    string StatusDisplay,
    string AdminEmail,
    string StorefrontUrl,
    string CpUrl,
    string ErpLoginUrl,
    bool InRegistry,
    bool IsActive,
    bool DemoExpired,
    bool AccessBlocked,
    bool HasStoredPassword,
    bool IsDemo)
{
    /// <summary>PHP orders demo rows first, then ERP-only, then commerce, each by site key.</summary>
    public int TypeOrder => TenantType switch
    {
        "demo" or "demo_erp_only" => 0,
        "erp_only" => 1,
        "commerce" => 2,
        _ => 9,
    };
}

public sealed record CpTenantControlCenterView(
    IReadOnlyList<CpTenantControlRow> Tenants,
    int RegistryCount,
    int AccessibleCount,
    string PlatformDb,
    string Source,
    string Error)
{
    public static CpTenantControlCenterView Empty(string source, string error)
        => new([], 0, 0, "ecomae", source, error);
}

public sealed class CpTenantControlCenterService : ICpTenantControlCenterService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Scheme = new("^https?://", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex TrailingPath = new("/.*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LeadingWww = new(@"^www\.", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpTenantControlCenterService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>epc_portal_tenant_control_tenant_type()</c>.</summary>
    public static string TenantType(bool isDemo, bool erpOnlyShared, string industryCode, string hostedOn)
    {
        if (isDemo)
        {
            return erpOnlyShared || string.Equals(industryCode, "erp_standalone", StringComparison.Ordinal)
                ? "demo_erp_only"
                : "demo";
        }

        if (erpOnlyShared)
        {
            return "erp_only";
        }

        return string.Equals(industryCode, "erp_standalone", StringComparison.Ordinal)
            && string.Equals(hostedOn, "platform", StringComparison.Ordinal)
                ? "erp_only"
                : "commerce";
    }

    /// <summary>PHP <c>epc_portal_tenant_control_type_label()</c>.</summary>
    public static string TypeLabel(string tenantType) => tenantType switch
    {
        "demo" => "Demo",
        "demo_erp_only" => "ERP-only demo",
        "erp_only" => "ERP-only",
        _ => "Commerce",
    };

    /// <summary>PHP <c>epc_portal_tenant_control_type_badge_class()</c>.</summary>
    public static string BadgeClass(string tenantType) => tenantType switch
    {
        "commerce" => "label-primary",
        "demo" => "label-info",
        "demo_erp_only" or "erp_only" => "label-warning",
        _ => "label-default",
    };

    /// <summary>PHP <c>epc_portal_tenant_control_commerce_host()</c>.</summary>
    public static string CommerceHost(string? hostname)
    {
        var host = (hostname ?? string.Empty).Trim().ToLowerInvariant();
        if (host.Length == 0)
        {
            return string.Empty;
        }

        host = LeadingWww.Replace(TrailingPath.Replace(Scheme.Replace(host, string.Empty), string.Empty), string.Empty);
        return host.Length == 0 || !host.Contains('.', StringComparison.Ordinal) ? string.Empty : "www." + host;
    }

    /// <summary>
    /// PHP <c>epc_portal_tenant_control_admin_email()</c> — demo contact first, then the CP operator
    /// fields inside <c>intro_json</c> (a business <c>admin_email</c> is deliberately not a CP login),
    /// then <c>from_email</c>.
    /// </summary>
    public static string AdminEmail(bool isDemo, string? demoContactEmail, string? introJson, string? fromEmail)
    {
        if (isDemo && IsEmail(demoContactEmail))
        {
            return demoContactEmail!.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(introJson))
        {
            try
            {
                using var document = JsonDocument.Parse(introJson);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var key in new[] { "admin_cp_email", "operator_login_email" })
                    {
                        if (document.RootElement.TryGetProperty(key, out var value)
                            && value.ValueKind == JsonValueKind.String
                            && IsEmail(value.GetString()))
                        {
                            return value.GetString()!.Trim().ToLowerInvariant();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // PHP json_decode returns null for malformed intro payloads and falls through.
            }
        }

        return IsEmail(fromEmail) ? fromEmail!.Trim().ToLowerInvariant() : string.Empty;
    }

    public static string NormalizeSiteKey(string? siteKey)
        => SiteKeySafe.Replace((siteKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    /// <summary>PHP <c>epc_portal_tenant_control_urls()</c> for the three link buttons.</summary>
    public static (string Storefront, string Cp, string Erp) Urls(string tenantType, string siteKey, string? hostname)
    {
        if (tenantType is "erp_only" && siteKey.Length > 0)
        {
            return (string.Empty, string.Empty, "https://www.ecomae.com/cp/client-erp/" + siteKey + "/");
        }

        var host = CommerceHost(hostname);
        return host.Length == 0
            ? (string.Empty, string.Empty, string.Empty)
            : ("https://" + host + "/en/", "https://" + host + "/cp/", string.Empty);
    }

    public async Task<CpTenantControlCenterView> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpTenantControlCenterView.Empty("unconfigured", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var rows = await ListAsync(connection, cancellationToken).ConfigureAwait(false);
            return new CpTenantControlCenterView(
                rows,
                rows.Count(r => r.InRegistry),
                rows.Count(r => r.IsActive && !r.AccessBlocked),
                "ecomae",
                "database",
                string.Empty);
        }
        catch (DbException ex)
        {
            return CpTenantControlCenterView.Empty("database-error", ex.Message);
        }
    }

    private static async Task<IReadOnlyList<CpTenantControlRow>> ListAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpTenantControlRow>();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT `site_key`, `hostname`, `trade_name`, `industry_code`, `hosted_on`, `status`, `db_name`, "
            + "`is_demo`, `erp_only_shared`, `is_active`, `demo_expires_at`, `demo_contact_email`, `from_email`, "
            + "`intro_json`, `operator_temp_password` "
            + "FROM `epc_portal_tenants` WHERE `site_key` <> '' "
            + "ORDER BY `is_demo` DESC, `erp_only_shared` DESC, `site_key` ASC";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var siteKey = NormalizeSiteKey(Text(reader, "site_key"));
            var hostname = Text(reader, "hostname");
            var isDemo = Flag(reader, "is_demo", false);
            var erpOnlyShared = Flag(reader, "erp_only_shared", false);
            var industry = Text(reader, "industry_code");
            var type = TenantType(isDemo, erpOnlyShared, industry, Text(reader, "hosted_on"));
            var isActive = Flag(reader, "is_active", true);
            var expires = Number(reader, "demo_expires_at");
            var demoExpired = isDemo && expires > 0 && expires < now;
            var status = Text(reader, "status");
            var (storefront, cp, erp) = Urls(type, siteKey, hostname);

            rows.Add(new CpTenantControlRow(
                siteKey,
                hostname,
                Text(reader, "trade_name"),
                type,
                TypeLabel(type),
                BadgeClass(type),
                Text(reader, "db_name"),
                type == "erp_only" ? industry : string.Empty,
                status,
                !isActive ? "disabled" : demoExpired ? "expired" : status,
                AdminEmail(isDemo, Text(reader, "demo_contact_email"), Text(reader, "intro_json"), Text(reader, "from_email")),
                storefront,
                cp,
                erp,
                true,
                isActive,
                demoExpired,
                !isActive || demoExpired,
                Text(reader, "operator_temp_password").Trim().Length > 0,
                isDemo));
        }

        return rows
            .OrderBy(r => r.TypeOrder)
            .ThenBy(r => r.SiteKey, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsEmail(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        var at = text.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && text.IndexOf('.', at) > at + 1 && !text.EndsWith('.') && !text.Contains(' ', StringComparison.Ordinal);
    }

    private static string Text(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal)?.ToString() ?? string.Empty;
    }

    private static long Number(DbDataReader reader, string column)
        => long.TryParse(Text(reader, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static bool Flag(DbDataReader reader, string column, bool whenNull)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? whenNull : Number(reader, column) != 0;
    }
}
