using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Super-CP directory of every tenant's admin / operator logins, how they sign in,
/// open sessions, and <c>epc_erp_audit_log</c>. Passwords are never selected.
/// Tenant admins control their own users on <c>/erp/users-app</c> (this company only).
/// </summary>
public interface ICpTenantAdminDirectoryService
{
    Task<CpTenantAdminDirectory> ListAsync(
        string? siteKey,
        string? query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<CpTenantAdminUserDetail> OpenAsync(
        string siteKey,
        long userId,
        CancellationToken cancellationToken = default);
}

public sealed record CpTenantAdminTenantOption(
    string SiteKey,
    string TradeName,
    string TenantType,
    string TenantTypeLabel,
    string DatabaseName,
    string LoginUrl,
    string UsersAppHref);

public sealed record CpTenantAdminUserRow(
    string SiteKey,
    string TradeName,
    string TenantType,
    string TenantTypeLabel,
    string LoginUrl,
    string UsersAppHref,
    long UserId,
    string Email,
    string DisplayName,
    string DepartmentName,
    bool Unlocked,
    bool Administrator,
    bool ErpTeam,
    bool RegistryOperator,
    int OpenSessions,
    long LastVisit,
    string HowTheyLogin,
    string Status);

public sealed record CpTenantAdminDirectory(
    IReadOnlyList<CpTenantAdminUserRow> Users,
    IReadOnlyList<CpTenantAdminTenantOption> Tenants,
    int TenantCount,
    int AdminCount,
    int OpenSessions,
    int Locked,
    string Source,
    string Message);

public sealed record CpTenantAdminUserDetail(
    CpTenantAdminUserRow? User,
    IReadOnlyList<ErpTenantUserSessionRow> Sessions,
    IReadOnlyList<ErpTenantUserAuditRow> Audit,
    string Source,
    string Message);

public sealed class CpTenantAdminDirectoryService : ICpTenantAdminDirectoryService
{
    public const int DirectoryLimit = 400;
    public const int LogLimit = 80;

    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly ITenantDbConnectionFactory _connections;

    public CpTenantAdminDirectoryService(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? siteKey)
        => SiteKeySafe.Replace((siteKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string LoginUrl(string tenantType, string siteKey, string? hostname)
    {
        if (IsErpOnlyType(tenantType) && siteKey.Length > 0)
        {
            return "/cp/client-erp/" + siteKey + "/";
        }

        var host = CpTenantControlCenterService.CommerceHost(hostname);
        return host.Length == 0 ? "/erp/login" : "https://" + host + "/erp/login";
    }

    public static string UsersAppHref(string tenantType, string siteKey, string? hostname)
    {
        if (IsErpOnlyType(tenantType) && siteKey.Length > 0)
        {
            return "/cp/client-erp/" + siteKey + "/erp/users-app";
        }

        var host = CpTenantControlCenterService.CommerceHost(hostname);
        return host.Length == 0 ? "/erp/users-app" : "https://" + host + "/erp/users-app";
    }

    public static string HowTheyLogin(string tenantType, string siteKey, string? hostname, string email)
    {
        var url = LoginUrl(tenantType, siteKey, hostname);
        var who = string.IsNullOrWhiteSpace(email) ? "Email + password" : email + " + password";
        return who + " at " + url;
    }

    public static bool IsErpOnlyType(string tenantType)
        => tenantType is "erp_only" or "demo_erp_only";

    public static IReadOnlyList<CpTenantAdminUserRow> Filter(
        IReadOnlyList<CpTenantAdminUserRow> rows,
        string? siteKey,
        string? query,
        int take)
    {
        var key = NormalizeSiteKey(siteKey);
        var needle = (query ?? string.Empty).Trim();
        var limit = Math.Clamp(take, 1, DirectoryLimit);
        IEnumerable<CpTenantAdminUserRow> filtered = rows;
        if (key.Length > 0)
        {
            filtered = filtered.Where(row =>
                string.Equals(row.SiteKey, key, StringComparison.OrdinalIgnoreCase));
        }

        if (needle.Length > 0)
        {
            filtered = filtered.Where(row => Matches(row, needle));
        }

        return filtered.Take(limit).ToArray();
    }

    public static bool Matches(CpTenantAdminUserRow row, string query)
    {
        var needle = (query ?? string.Empty).Trim();
        if (needle.Length == 0)
        {
            return true;
        }

        if (long.TryParse(needle, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            && row.UserId == id)
        {
            return true;
        }

        return Contains(row.Email, needle)
               || Contains(row.DisplayName, needle)
               || Contains(row.SiteKey, needle)
               || Contains(row.TradeName, needle)
               || Contains(row.DepartmentName, needle)
               || Contains(row.HowTheyLogin, needle);
    }

    public static bool AuditLooksSafe(string? detailJson)
    {
        var json = detailJson ?? string.Empty;
        return json.IndexOf("\"password\"", StringComparison.OrdinalIgnoreCase) < 0
               && json.IndexOf("password=", StringComparison.OrdinalIgnoreCase) < 0;
    }

    public static CpTenantAdminTenantOption PublicTenant(TenantWalk walk)
        => new(
            walk.SiteKey,
            walk.TradeName,
            walk.TenantType,
            walk.TenantTypeLabel,
            walk.DatabaseName,
            walk.LoginUrl,
            walk.UsersAppHref);

    public async Task<CpTenantAdminDirectory> ListAsync(
        string? siteKey,
        string? query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], [], 0, 0, 0, 0, "n/a", "TenantRegistry DB is not configured.");
        }

        try
        {
            var walks = await ListWalksAsync(cancellationToken).ConfigureAwait(false);
            var key = NormalizeSiteKey(siteKey);
            var scan = key.Length == 0
                ? walks
                : walks.Where(t => string.Equals(t.SiteKey, key, StringComparison.OrdinalIgnoreCase)).ToArray();
            var users = new List<CpTenantAdminUserRow>();
            foreach (var tenant in scan)
            {
                users.AddRange(await LoadTenantAdminsAsync(tenant, cancellationToken).ConfigureAwait(false));
            }

            var shown = Filter(users, siteKey, query, limit);
            return new(
                shown,
                walks.Select(PublicTenant).ToArray(),
                walks.Count,
                users.Count,
                users.Sum(row => row.OpenSessions),
                users.Count(row => !row.Unlocked),
                "tenant-admins",
                users.Count == 0
                    ? "No tenant admin users yet. Open a company Users & credentials page to prepare one."
                    : string.Empty);
        }
        catch (DbException ex)
        {
            return new([], [], 0, 0, 0, 0, "error", ex.Message);
        }
    }

    public async Task<CpTenantAdminUserDetail> OpenAsync(
        string siteKey,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeSiteKey(siteKey);
        if (key.Length == 0)
        {
            return new(null, [], [], "invalid", "A tenant site key is required.");
        }

        if (!_connections.IsConfigured)
        {
            return new(null, [], [], "n/a", "TenantRegistry DB is not configured.");
        }

        var walks = await ListWalksAsync(cancellationToken).ConfigureAwait(false);
        var tenant = walks.FirstOrDefault(row =>
            string.Equals(row.SiteKey, key, StringComparison.OrdinalIgnoreCase));
        if (tenant is null)
        {
            return new(null, [], [], "not_found", "Tenant not found.");
        }

        var users = await LoadTenantAdminsAsync(tenant, cancellationToken).ConfigureAwait(false);
        var user = userId > 0
            ? users.FirstOrDefault(row => row.UserId == userId)
            : users.FirstOrDefault();
        if (user is null)
        {
            return new(null, [], [], "not_found", "Admin user not found on this tenant.");
        }

        if (user.UserId <= 0 || tenant.DatabaseName.Length == 0)
        {
            return new(user, [], [], "registry", "This login is on the portal registry. Open the company Users & credentials page after they sign in.");
        }

        try
        {
            await using var connection = await OpenTenantDbAsync(tenant, cancellationToken).ConfigureAwait(false);
            if (connection is null)
            {
                return new(user, [], [], "unreachable", "Company database is not reachable.");
            }

            await ErpLazySchema.EnsureAuditLogAsync(connection, cancellationToken).ConfigureAwait(false);
            var sessions = await LoadSessionsAsync(connection, user.UserId, LogLimit, cancellationToken).ConfigureAwait(false);
            var audit = await LoadAuditAsync(connection, user.UserId, LogLimit, cancellationToken).ConfigureAwait(false);
            return new(user, sessions, audit, "tenant-db", string.Empty);
        }
        catch (DbException)
        {
            return new(user, [], [], "error", "Could not read this tenant's login logs.");
        }
    }

    public sealed record TenantWalk(
        string SiteKey,
        string TradeName,
        string TenantType,
        string TenantTypeLabel,
        string DatabaseName,
        string LoginUrl,
        string UsersAppHref,
        string Hostname,
        string? DbUser,
        string? DbPassword,
        IReadOnlyList<string> OperatorEmails,
        bool IsActive);

    private async Task<IReadOnlyList<TenantWalk>> ListWalksAsync(CancellationToken cancellationToken)
    {
        await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT `site_key`, IFNULL(`hostname`,''), IFNULL(`trade_name`,''), IFNULL(`industry_code`,''),
                   IFNULL(`hosted_on`,''), IFNULL(`db_name`,''), IFNULL(`db_user`,''), IFNULL(`db_password`,''),
                   IFNULL(`is_demo`,0), IFNULL(`erp_only_shared`,0), IFNULL(`is_active`,1),
                   IFNULL(`intro_json`,''), IFNULL(`from_email`,''), IFNULL(`demo_contact_email`,'')
            FROM `epc_portal_tenants`
            WHERE IFNULL(`site_key`,'') <> ''
            ORDER BY `is_demo` DESC, `erp_only_shared` DESC, `site_key` ASC
            """;

        var rows = new List<TenantWalk>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var siteKey = NormalizeSiteKey(Text(reader, 0));
            if (siteKey.Length == 0)
            {
                continue;
            }

            var hostname = Text(reader, 1);
            var trade = Text(reader, 2);
            var type = CpTenantControlCenterService.TenantType(
                Flag(reader, 8),
                Flag(reader, 9),
                Text(reader, 3),
                Text(reader, 4));
            rows.Add(new TenantWalk(
                siteKey,
                trade.Length > 0 ? trade : siteKey,
                type,
                CpTenantControlCenterService.TypeLabel(type),
                Text(reader, 5),
                LoginUrl(type, siteKey, hostname),
                UsersAppHref(type, siteKey, hostname),
                hostname,
                EmptyToNull(Text(reader, 6)),
                EmptyToNull(Text(reader, 7)),
                OperatorEmails(Flag(reader, 8), Text(reader, 13), Text(reader, 11), Text(reader, 12)),
                Flag(reader, 10)));
        }

        return rows;
    }

    private async Task<IReadOnlyList<CpTenantAdminUserRow>> LoadTenantAdminsAsync(
        TenantWalk tenant,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpTenantAdminUserRow>();
        if (tenant.DatabaseName.Length > 0)
        {
            try
            {
                await using var connection = await OpenTenantDbAsync(tenant, cancellationToken).ConfigureAwait(false);
                if (connection is not null)
                {
                    rows.AddRange(await ReadAdminUsersAsync(connection, tenant, cancellationToken).ConfigureAwait(false));
                }
            }
            catch (DbException)
            {
                // Registry operator rows still surface below when the dedicated DB is down.
            }
        }

        foreach (var email in tenant.OperatorEmails)
        {
            if (rows.Any(row => string.Equals(row.Email, email, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            rows.Add(RegistryOperatorRow(tenant, email));
        }

        if (rows.Count == 0)
        {
            rows.Add(RegistryOperatorRow(tenant, string.Empty));
        }

        return rows;
    }

    private async Task<DbConnection?> OpenTenantDbAsync(TenantWalk tenant, CancellationToken cancellationToken)
    {
        if (tenant.DatabaseName.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(tenant.DbUser) && !string.IsNullOrWhiteSpace(tenant.DbPassword))
        {
            return await _connections
                .OpenAsync(tenant.DatabaseName, tenant.DbUser, tenant.DbPassword, cancellationToken)
                .ConfigureAwait(false);
        }

        return await _connections.OpenAsync(tenant.DatabaseName, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<CpTenantAdminUserRow>> ReadAdminUsersAsync(
        DbConnection connection,
        TenantWalk tenant,
        CancellationToken cancellationToken)
    {
        var groupMap = await LoadUserGroupsAsync(connection, cancellationToken).ConfigureAwait(false);
        var sessionMap = await LoadOpenSessionCountsAsync(connection, cancellationToken).ConfigureAwait(false);
        var adminGroupIds = groupMap.Values
            .SelectMany(pairs => pairs)
            .Where(pair => IsAdministratorGroup(pair.Value))
            .Select(pair => pair.Id)
            .ToHashSet();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT u.`user_id`, IFNULL(u.`email`,''), IFNULL(u.`unlocked`,0),
                   IFNULL(u.`time_last_visit`,0),
                   IFNULL(p.`display_name`,''), IFNULL(p.`department_code`,'')
            FROM `users` u
            LEFT JOIN `epc_erp_staff_profiles` p ON p.`user_id` = u.`user_id`
            ORDER BY u.`user_id` DESC
            """;

        var rows = new List<CpTenantAdminUserRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var userId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            var email = Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
            var groups = groupMap.TryGetValue(userId, out var bound) ? bound : [];
            var administrator = groups.Any(g => adminGroupIds.Contains(g.Id) || IsAdministratorGroup(g.Value));
            var erpTeam = groups.Any(g =>
                string.Equals(g.Value, ErpUserAccess.TeamGroupValue, StringComparison.Ordinal));
            var registryOperator = tenant.OperatorEmails.Any(op =>
                string.Equals(op, email, StringComparison.OrdinalIgnoreCase));
            if (!administrator && !erpTeam && !registryOperator)
            {
                continue;
            }

            var display = Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty;
            var department = Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture) ?? string.Empty;
            var unlocked = Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture) == 1;
            var lastVisit = Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture);
            var open = sessionMap.TryGetValue(userId, out var count) ? count : 0;
            rows.Add(new CpTenantAdminUserRow(
                tenant.SiteKey,
                tenant.TradeName,
                tenant.TenantType,
                tenant.TenantTypeLabel,
                tenant.LoginUrl,
                tenant.UsersAppHref,
                userId,
                email,
                display.Length > 0 ? display : email,
                ErpStaffDepartmentCatalog.NameFor(department),
                unlocked,
                administrator,
                erpTeam,
                registryOperator,
                open,
                lastVisit,
                HowTheyLogin(tenant.TenantType, tenant.SiteKey, tenant.Hostname, email),
                StatusLabel(unlocked, open, lastVisit, registryOperator)));
        }

        return rows;
    }

    private static CpTenantAdminUserRow RegistryOperatorRow(TenantWalk tenant, string email)
        => new(
            tenant.SiteKey,
            tenant.TradeName,
            tenant.TenantType,
            tenant.TenantTypeLabel,
            tenant.LoginUrl,
            tenant.UsersAppHref,
            0,
            email,
            email.Length > 0 ? email : tenant.TradeName + " operator",
            string.Empty,
            tenant.IsActive,
            true,
            false,
            true,
            0,
            0,
            HowTheyLogin(tenant.TenantType, tenant.SiteKey, tenant.Hostname, email),
            tenant.IsActive ? "registry operator" : "disabled");

    private static async Task<IReadOnlyList<ErpTenantUserSessionRow>> LoadSessionsAsync(
        DbConnection connection,
        long userId,
        int limit,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, LogLimit);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT s.`id`, s.`user_id`, IFNULL(u.`email`,''), IFNULL(s.`type`,0), IFNULL(s.`time`,0),
                       IFNULL(s.`last_activiti_time`,0), IFNULL(s.`contact_type`,'')
                FROM `sessions` s
                LEFT JOIN `users` u ON u.`user_id` = s.`user_id`
                WHERE s.`user_id` = ?
                ORDER BY s.`last_activiti_time` DESC, s.`id` DESC
                LIMIT ?
                """);
            ErpDb.AddParameters(command, userId, safeLimit);
            var rows = new List<ErpTenantUserSessionRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpTenantUserSessionRow(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture) ?? string.Empty));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    private static async Task<IReadOnlyList<ErpTenantUserAuditRow>> LoadAuditAsync(
        DbConnection connection,
        long userId,
        int limit,
        CancellationToken cancellationToken)
    {
        var safeLimit = Math.Clamp(limit, 1, LogLimit);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT `id`,`time`,`admin_id`,`action`,`entity_type`,`entity_id`,
                       IFNULL(`summary`,''), IFNULL(`detail_json`,''), IFNULL(`ip_address`,'')
                FROM `epc_erp_audit_log`
                WHERE (`entity_id` = ? OR `admin_id` = ? OR IFNULL(`detail_json`,'') LIKE ?)
                ORDER BY `time` DESC, `id` DESC
                LIMIT ?
                """);
            var userKey = userId.ToString(CultureInfo.InvariantCulture);
            ErpDb.AddParameters(command, userId, userId, "%\"user_id\":\"" + userKey + "\"%", safeLimit);
            var rows = new List<ErpTenantUserAuditRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var detail = Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? string.Empty;
                rows.Add(new ErpTenantUserAuditRow(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture) ?? string.Empty,
                    AuditLooksSafe(detail) ? detail : "{\"redacted\":\"credential\"}",
                    Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture) ?? string.Empty));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    private static async Task<Dictionary<long, List<(long Id, string Value)>>> LoadUserGroupsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, List<(long Id, string Value)>>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT b.`user_id`, g.`id`, IFNULL(g.`value`,'')
                FROM `users_groups_bind` b
                INNER JOIN `groups` g ON g.`id` = b.`group_id`
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var userId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                if (!map.TryGetValue(userId, out var list))
                {
                    list = [];
                    map[userId] = list;
                }

                list.Add((
                    Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty));
            }
        }
        catch (DbException)
        {
            return map;
        }

        return map;
    }

    private static async Task<Dictionary<long, int>> LoadOpenSessionCountsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, int>();
        try
        {
            var cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (8 * 3600);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT `user_id`, COUNT(*)
                FROM `sessions`
                WHERE `user_id` > 0 AND GREATEST(IFNULL(`last_activiti_time`,0), IFNULL(`time`,0)) >= ?
                GROUP BY `user_id`
                """);
            ErpDb.AddParameters(command, cutoff);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                map[Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture)] =
                    Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
            }
        }
        catch (DbException)
        {
            return map;
        }

        return map;
    }

    private static IReadOnlyList<string> OperatorEmails(
        bool isDemo,
        string? demoContact,
        string? introJson,
        string? fromEmail)
    {
        var emails = new List<string>();
        var primary = CpTenantControlCenterService.AdminEmail(isDemo, demoContact, introJson, fromEmail);
        if (primary.Length > 0)
        {
            emails.Add(primary);
        }

        foreach (var extra in CpTenantControlCenterService.ExtraLoginEmailsFromIntro(introJson))
        {
            if (!emails.Contains(extra, StringComparer.OrdinalIgnoreCase))
            {
                emails.Add(extra);
            }
        }

        return emails;
    }

    private static string StatusLabel(bool unlocked, int openSessions, long lastVisit, bool registryOperator)
    {
        if (!unlocked)
        {
            return "locked";
        }

        if (openSessions > 0)
        {
            return "signed in";
        }

        if (lastVisit > 0)
        {
            return "last visit recorded";
        }

        return registryOperator ? "registry operator" : "unlocked";
    }

    private static bool IsAdministratorGroup(string value)
        => value.Contains("Administrator", StringComparison.OrdinalIgnoreCase)
           || value.Contains("Администратор", StringComparison.Ordinal);

    private static bool Contains(string haystack, string needle)
        => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal)
            ? string.Empty
            : (Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty).Trim();

    private static bool Flag(DbDataReader reader, int ordinal)
        => !reader.IsDBNull(ordinal)
           && Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture) != 0;

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
