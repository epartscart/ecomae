using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// PHP <c>epc_scp_customer_board_search</c> / <c>epc_scp_tenant_options</c> twin: cross-tenant customer
/// search over the platform registry database plus every live tenant database.
/// </summary>
public interface ICpSuperCustomerBoardService
{
    Task<CpSuperCustomerBoardResult> SearchAsync(
        string? search,
        string? tenantFilter,
        int page,
        int perPage,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpSuperCustomerTenantOption>> ListTenantOptionsAsync(
        CancellationToken cancellationToken = default);
}

public sealed record CpSuperCustomerTenantOption(string SiteKey, string Label, string Hostname)
{
    /// <summary>Registry <c>db_name</c>; empty when the tenant has no bound shop database.</summary>
    public string Database { get; init; } = string.Empty;

    /// <summary>PHP selector label: <c>trade_name (site_key)</c>.</summary>
    public string DropdownLabel => Label + " (" + SiteKey + ")";
}

public sealed record CpSuperCustomerRow(
    string Source,
    string SourceLabel,
    string Hostname,
    long UserId,
    string Name,
    string Email,
    string Phone,
    long TimeRegistered,
    string CrmUrl,
    string ErpUrl,
    string CpUrl);

public sealed record CpSuperCustomerBoardResult(
    IReadOnlyList<CpSuperCustomerRow> Rows,
    int Total,
    int Page,
    int PerPage,
    int PlatformCount,
    int TenantsScanned,
    int TenantsWithHits,
    string Source,
    string Message);

public sealed class CpSuperCustomerBoardService : ICpSuperCustomerBoardService
{
    private const int PlatformLimit = 80;
    private const int TenantLimit = 25;
    private const int ResultCap = 200;

    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Scheme = new("^https?://", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const string CustomerSelect = """
        SELECT u.`user_id`, u.`email`, u.`phone`, u.`time_reg`,
               MAX(CASE WHEN up.`data_key` = 'name' THEN up.`data_value` END) AS fname,
               MAX(CASE WHEN up.`data_key` = 'surname' THEN up.`data_value` END) AS sname,
               MAX(CASE WHEN up.`data_key` = 'company' THEN up.`data_value` END) AS company
        FROM `users` u
        LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id`
        WHERE u.`user_id` > 0
        """;

    private const string SearchPredicate =
        " AND (u.`email` LIKE ? OR u.`phone` LIKE ? OR up.`data_value` LIKE ?)";

    private const string GroupOrder =
        " GROUP BY u.`user_id`, u.`email`, u.`phone`, u.`time_reg` ORDER BY u.`user_id` DESC LIMIT ";

    private const string SelectRegistryTenants = """
        SELECT `site_key`, IFNULL(`hostname`, '') AS hostname, IFNULL(`db_name`, '') AS db_name,
               IFNULL(`trade_name`, '') AS trade_name
        FROM `epc_portal_tenants`
        WHERE IFNULL(`site_key`, '') <> ''
          AND COALESCE(`is_active`, 1) = 1
          AND `status` IN ('dns_pending', 'live')
        ORDER BY `hostname` ASC
        """;

    private readonly ITenantDbConnectionFactory _connections;

    public CpSuperCustomerBoardService(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_]/', '', strtolower($tenantFilter))</c>.</summary>
    public static string NormalizeTenantFilter(string? tenantFilter)
        => SiteKeySafe.Replace((tenantFilter ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    /// <summary>PHP <c>epc_portal_tenant_control_commerce_host</c>.</summary>
    public static string CommerceHost(string? hostname)
    {
        var host = (hostname ?? string.Empty).Trim().ToLowerInvariant();
        if (host.Length == 0)
        {
            return string.Empty;
        }

        host = Scheme.Replace(host, string.Empty);
        var slash = host.IndexOf('/', StringComparison.Ordinal);
        if (slash >= 0)
        {
            host = host[..slash];
        }

        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return host.Length == 0 || !host.Contains('.', StringComparison.Ordinal) ? string.Empty : "www." + host;
    }

    /// <summary>PHP <c>epc_scp_customer_name_from_row</c>: first+surname, else company, else email.</summary>
    public static string CustomerName(string? firstName, string? surname, string? company, string? email)
    {
        var parts = new[] { (firstName ?? string.Empty).Trim(), (surname ?? string.Empty).Trim() }
            .Where(part => part.Length > 0)
            .ToArray();
        if (parts.Length > 0)
        {
            return string.Join(' ', parts);
        }

        var org = (company ?? string.Empty).Trim();
        return org.Length > 0 ? org : (email ?? string.Empty).Trim();
    }

    public async Task<IReadOnlyList<CpSuperCustomerTenantOption>> ListTenantOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            return await ReadTenantsAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return [];
        }
    }

    public async Task<CpSuperCustomerBoardResult> SearchAsync(
        string? search,
        string? tenantFilter,
        int page,
        int perPage,
        CancellationToken cancellationToken = default)
    {
        var term = (search ?? string.Empty).Trim();
        var filter = NormalizeTenantFilter(tenantFilter);
        var safePage = Math.Max(1, page);
        var safePerPage = Math.Clamp(perPage, 10, 100);

        if (!_connections.IsConfigured)
        {
            return new([], 0, safePage, safePerPage, 0, 0, 0, "migration", "TenantRegistry DB is not configured.");
        }

        var results = new List<CpSuperCustomerRow>();
        var platformCount = 0;
        var scanned = 0;
        var withHits = 0;
        var message = string.Empty;
        IReadOnlyList<CpSuperCustomerTenantOption> tenants = [];

        try
        {
            await using var registry = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var platform = await ReadCustomersAsync(
                    registry,
                    term,
                    PlatformLimit,
                    "platform",
                    "Platform (ecomae)",
                    "www.ecomae.com",
                    "/cp",
                    cancellationToken).ConfigureAwait(false);
                results.AddRange(platform);
                platformCount = platform.Count;
            }
            catch (DbException ex)
            {
                message = ex.Message;
            }

            tenants = await ReadTenantsAsync(registry, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            return new([], 0, safePage, safePerPage, 0, 0, 0, "database-error", ex.Message);
        }

        if (filter != "platform")
        {
            foreach (var tenant in tenants)
            {
                if (filter.Length > 0 && filter != tenant.SiteKey)
                {
                    continue;
                }

                var chunk = await ReadTenantCustomersAsync(tenant, term, cancellationToken).ConfigureAwait(false);
                if (chunk is null)
                {
                    continue;
                }

                scanned++;
                if (chunk.Count > 0)
                {
                    withHits++;
                }

                results.AddRange(chunk);
                if (results.Count >= ResultCap)
                {
                    break;
                }
            }
        }

        results.Sort((a, b) => string.CompareOrdinal(b.Email, a.Email));
        var total = results.Count;
        var offset = (safePage - 1) * safePerPage;
        var pageRows = offset >= total
            ? Array.Empty<CpSuperCustomerRow>()
            : results.Skip(offset).Take(safePerPage).ToArray();

        return new(
            pageRows,
            total,
            safePage,
            safePerPage,
            platformCount,
            scanned,
            withHits,
            message.Length > 0 ? "database-error" : "database",
            message);
    }

    private async Task<IReadOnlyList<CpSuperCustomerRow>?> ReadTenantCustomersAsync(
        CpSuperCustomerTenantOption tenant,
        string search,
        CancellationToken cancellationToken)
    {
        var database = tenant.Database;
        if (database.Length == 0)
        {
            return null;
        }

        var host = CommerceHost(tenant.Hostname);
        var cpBase = host.Length > 0 ? "https://" + host + "/cp" : string.Empty;

        try
        {
            await using var connection = await _connections
                .OpenAsync(database, cancellationToken)
                .ConfigureAwait(false);

            return await ReadCustomersAsync(
                connection,
                search,
                TenantLimit,
                tenant.SiteKey,
                tenant.Label,
                tenant.Hostname,
                cpBase,
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<CpSuperCustomerRow>> ReadCustomersAsync(
        DbConnection connection,
        string search,
        int limit,
        string source,
        string sourceLabel,
        string hostname,
        string cpBase,
        CancellationToken cancellationToken)
    {
        var sql = CustomerSelect
            + (search.Length > 0 ? SearchPredicate : string.Empty)
            + GroupOrder
            + limit.ToString(CultureInfo.InvariantCulture);

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        if (search.Length > 0)
        {
            var like = "%" + search + "%";
            ErpDb.AddParameters(command, like, like, like);
        }

        var rows = new List<CpSuperCustomerRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var userId = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            var email = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var trimmedBase = cpBase.TrimEnd('/');
            rows.Add(new CpSuperCustomerRow(
                source,
                sourceLabel,
                hostname,
                userId,
                CustomerName(
                    reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    email),
                email,
                reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                reader.IsDBNull(3) ? 0L : Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
                trimmedBase.Length > 0
                    ? trimmedBase + "/shop/customer_mgmt/customer_mgmt?tab=customers&user_id="
                        + userId.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
                trimmedBase.Length > 0
                    ? trimmedBase + "/shop/finance/erp?epc_erp_shell=1&area=sales"
                    : string.Empty,
                trimmedBase.Length > 0 ? trimmedBase + "/" : string.Empty));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpSuperCustomerTenantOption>> ReadTenantsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SelectRegistryTenants;

        var rows = new List<CpSuperCustomerTenantOption>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            if (key.Length == 0)
            {
                continue;
            }

            var hostname = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var database = reader.IsDBNull(2) ? string.Empty : reader.GetString(2).Trim();
            var trade = (reader.IsDBNull(3) ? string.Empty : reader.GetString(3)).Trim();
            rows.Add(new CpSuperCustomerTenantOption(key, trade.Length > 0 ? trade : key, hostname)
            {
                Database = database,
            });
        }

        return rows;
    }
}
