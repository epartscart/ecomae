using System.Data.Common;
using System.Globalization;
using System.Net.Mail;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Tenant ERP user preparation: department users (not all administrators),
/// credential lock/password, login sessions, and <c>epc_erp_audit_log</c>.
/// </summary>
public interface IErpTenantUserAdminService
{
    Task<ErpTenantUserDirectory> ListAsync(
        string? query,
        string? department,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Company sign-in only: how many people can open this company, how many have a
    /// session active in the last 8 hours, and each person's last login. No phone,
    /// department, or credential fields.
    /// </summary>
    Task<ErpTenantLoginPresence> LoginPresenceAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ErpTenantUserSessionRow>> ListSessionsAsync(
        long userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ErpTenantUserAuditRow>> ListAuditAsync(
        long userId,
        string? query,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PrepareAsync(
        ErpTenantUserPrepareRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetCredentialsAsync(
        ErpTenantUserCredentialsRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetDepartmentAsync(
        ErpTenantUserDepartmentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpTenantUserPrepareRequest(
    string? Email,
    string? DisplayName,
    string? JobTitle,
    string? DepartmentCode,
    string? Password,
    string? Phone = null,
    bool Unlocked = true,
    bool GrantAdministrator = false,
    int ActorUserId = 0);

public sealed record ErpTenantUserCredentialsRequest(
    long UserId,
    string Action,
    string? Password = null,
    int ActorUserId = 0);

public sealed record ErpTenantUserDepartmentRequest(
    long UserId,
    string? DepartmentCode,
    string? DisplayName = null,
    string? JobTitle = null,
    int ActorUserId = 0);

public sealed record ErpTenantUserRow(
    long UserId,
    string Email,
    string Phone,
    string DisplayName,
    string JobTitle,
    string DepartmentCode,
    string DepartmentName,
    bool Unlocked,
    bool ErpTeam,
    bool Administrator,
    int OpenSessions,
    long LastVisit,
    long TimeRegistered);

public sealed record ErpTenantUserSessionRow(
    long Id,
    long UserId,
    string Email,
    int Type,
    long Time,
    long LastActivity,
    string ContactType);

public sealed record ErpTenantUserAuditRow(
    long Id,
    long Time,
    int AdminId,
    string Action,
    string EntityType,
    long EntityId,
    string Summary,
    string DetailJson,
    string IpAddress);

public sealed record ErpTenantUserDirectory(
    IReadOnlyList<ErpTenantUserRow> Users,
    int Total,
    int Locked,
    int DepartmentCount,
    int OpenSessions,
    int AuditToday,
    string Source,
    string Message);

/// <summary>One person on the super sign-in list: name, email, last login, and whether a session is open.</summary>
public sealed record ErpTenantLoginPresenceRow(string Name, string Email, long LastLoginUnix, bool SignedIn);

/// <summary>Headcount, people signed in now, and the newest last-login time for this company.</summary>
public sealed record ErpTenantLoginPresence(
    int UserCount,
    int SignedInCount,
    long LatestLoginUnix,
    IReadOnlyList<ErpTenantLoginPresenceRow> People,
    string Source,
    string Message);

public sealed class ErpTenantUserAdminService : IErpTenantUserAdminService
{
    public const string EntityType = "erp_user";
    public const string TeamGroupValue = ErpUserAccess.TeamGroupValue;
    public const int MinPasswordLength = 8;
    public const int DirectoryLimit = 200;
    public const int LogLimit = 80;

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpAuditLogWriter _audit;

    public ErpTenantUserAdminService(IErpWriteConnectionFactory connections, IErpAuditLogWriter audit)
    {
        _connections = connections;
        _audit = audit;
    }

    public static ErpSimpleWriteResult? ValidatePrepare(ErpTenantUserPrepareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var email = (request.Email ?? string.Empty).Trim();
        if (email.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Email is required.");
        }

        if (!LooksLikeEmail(email))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Enter a valid email address.");
        }

        var name = (request.DisplayName ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Display name is required.");
        }

        if (NormalizeDepartment(request.DepartmentCode).Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose a department. Department users do not need administrator rights.");
        }

        var password = request.Password ?? string.Empty;
        if (password.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Password is required.");
        }

        if (password.Length < MinPasswordLength)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Password must be at least 8 characters.");
        }

        if (password.Length > 200)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Password is too long.");
        }

        return null;
    }

    public static ErpSimpleWriteResult? ValidateCredentials(ErpTenantUserCredentialsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A user id is required.");
        }

        var action = NormalizeCredentialAction(request.Action);
        if (action.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose lock, unlock, or password.");
        }

        if (action is "lock" or "unlock"
            && request.ActorUserId > 0
            && request.ActorUserId == request.UserId)
        {
            return ErpSimpleWriteResult.Fail("self", "You cannot lock or unlock your own account.");
        }

        if (action == "password")
        {
            var password = request.Password ?? string.Empty;
            if (password.Length == 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Password is required.");
            }

            if (password.Length < MinPasswordLength)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Password must be at least 8 characters.");
            }

            if (password.Length > 200)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Password is too long.");
            }
        }

        return null;
    }

    public static ErpSimpleWriteResult? ValidateDepartment(ErpTenantUserDepartmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A user id is required.");
        }

        if (NormalizeDepartment(request.DepartmentCode).Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Choose a department.");
        }

        return null;
    }

    public static string NormalizeDepartment(string? code)
    {
        var raw = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        foreach (var department in ErpStaffDepartmentCatalog.All)
        {
            if (string.Equals(department.Code, raw, StringComparison.OrdinalIgnoreCase))
            {
                return department.Code;
            }
        }

        return string.Empty;
    }

    public static string DepartmentGroupValue(string departmentCode)
        => "EPC_ERP_DEPT_" + departmentCode.Trim().ToUpperInvariant();

    public static bool IsErpDepartmentGroup(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.StartsWith("EPC_ERP_DEPT_", StringComparison.OrdinalIgnoreCase);

    public static bool LooksLikeEmail(string email)
    {
        var value = (email ?? string.Empty).Trim();
        if (value.Length is < 5 or > 120 || value.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(value);
            return parsed.Address.Equals(value, StringComparison.OrdinalIgnoreCase)
                   && parsed.Host.Contains('.', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string NormalizeCredentialAction(string? action)
    {
        var raw = (action ?? string.Empty).Trim().ToLowerInvariant();
        return raw switch
        {
            "lock" or "locked" => "lock",
            "unlock" or "unlocked" => "unlock",
            "password" or "reset" or "set-password" or "set_password" => "password",
            _ => string.Empty,
        };
    }

    public static IReadOnlyList<ErpTenantUserRow> Filter(
        IReadOnlyList<ErpTenantUserRow> rows,
        string? query,
        string? department,
        int take)
    {
        var needle = (query ?? string.Empty).Trim();
        var dept = NormalizeDepartment(department);
        var limit = Math.Clamp(take, 1, DirectoryLimit);
        IEnumerable<ErpTenantUserRow> filtered = rows;
        if (dept.Length > 0)
        {
            filtered = filtered.Where(row =>
                string.Equals(row.DepartmentCode, dept, StringComparison.OrdinalIgnoreCase));
        }

        if (needle.Length > 0)
        {
            filtered = filtered.Where(row => Matches(row, needle));
        }

        return filtered.Take(limit).ToArray();
    }

    public static bool Matches(ErpTenantUserRow row, string query)
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
               || Contains(row.JobTitle, needle)
               || Contains(row.Phone, needle)
               || Contains(row.DepartmentCode, needle)
               || Contains(row.DepartmentName, needle);
    }

    public async Task<ErpTenantUserDirectory> ListAsync(
        string? query,
        string? department,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new ErpTenantUserDirectory([], 0, 0, 0, 0, 0, "n/a", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsurePayrollAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsureAuditLogAsync(connection, cancellationToken).ConfigureAwait(false);
            var rows = await LoadUsersAsync(connection, cancellationToken).ConfigureAwait(false);
            var shown = Filter(rows, query, department, limit);
            var locked = rows.Count(row => !row.Unlocked);
            var departments = rows
                .Select(row => row.DepartmentCode)
                .Where(code => code.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var openSessions = rows.Sum(row => row.OpenSessions);
            var dayAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86400;
            var auditToday = (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_audit_log` WHERE `time` >= ?"),
                cancellationToken,
                dayAgo).ConfigureAwait(false);
            return new ErpTenantUserDirectory(
                shown,
                rows.Count,
                locked,
                departments,
                openSessions,
                auditToday,
                "tenant-users",
                rows.Count == 0
                    ? "No ERP users yet. Prepare a department user below — they do not need administrator rights."
                    : string.Empty);
        }
        catch (DbException ex)
        {
            return new ErpTenantUserDirectory([], 0, 0, 0, 0, 0, "error", ex.Message);
        }
    }

    public async Task<ErpTenantLoginPresence> LoginPresenceAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new ErpTenantLoginPresence(0, 0, 0, [], "n/a", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var rows = await LoadUsersAsync(connection, cancellationToken).ConfigureAwait(false);
            return SummarizeLogins(rows);
        }
        catch (DbException ex)
        {
            return new ErpTenantLoginPresence(0, 0, 0, [], "error", ex.Message);
        }
    }

    /// <summary>
    /// Counts people, not session rows. Two open sessions for one person still count as one signed-in user.
    /// A session counts as open when it was active in the last 8 hours.
    /// </summary>
    public static ErpTenantLoginPresence SummarizeLogins(IReadOnlyList<ErpTenantUserRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var people = new List<ErpTenantLoginPresenceRow>(rows.Count);
        var latest = 0L;
        var signedIn = 0;
        foreach (var row in rows)
        {
            var email = (row.Email ?? string.Empty).Trim();
            var name = (row.DisplayName ?? string.Empty).Trim();
            var open = row.OpenSessions > 0;
            if (open)
            {
                signedIn++;
            }

            if (row.LastVisit > latest)
            {
                latest = row.LastVisit;
            }

            people.Add(new ErpTenantLoginPresenceRow(
                name.Length > 0 ? name : email,
                email,
                row.LastVisit,
                open));
        }

        people.Sort(static (left, right) =>
        {
            var open = right.SignedIn.CompareTo(left.SignedIn);
            if (open != 0)
            {
                return open;
            }

            var login = right.LastLoginUnix.CompareTo(left.LastLoginUnix);
            if (login != 0)
            {
                return login;
            }

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });

        return new ErpTenantLoginPresence(
            people.Count,
            signedIn,
            latest,
            people,
            "tenant-users",
            people.Count == 0 ? "No people can sign in to this company yet." : string.Empty);
    }

    public async Task<IReadOnlyList<ErpTenantUserSessionRow>> ListSessionsAsync(
        long userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        var safeLimit = Math.Clamp(limit, 1, LogLimit);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            if (userId > 0)
            {
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
            }
            else
            {
                command.CommandText = ErpDb.Positional(
                    """
                    SELECT s.`id`, s.`user_id`, IFNULL(u.`email`,''), IFNULL(s.`type`,0), IFNULL(s.`time`,0),
                           IFNULL(s.`last_activiti_time`,0), IFNULL(s.`contact_type`,'')
                    FROM `sessions` s
                    LEFT JOIN `users` u ON u.`user_id` = s.`user_id`
                    ORDER BY s.`last_activiti_time` DESC, s.`id` DESC
                    LIMIT ?
                    """);
                ErpDb.AddParameters(command, safeLimit);
            }

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

    public async Task<IReadOnlyList<ErpTenantUserAuditRow>> ListAuditAsync(
        long userId,
        string? query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        var safeLimit = Math.Clamp(limit, 1, LogLimit);
        var needle = (query ?? string.Empty).Trim();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpLazySchema.EnsureAuditLogAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            var sql =
                """
                SELECT `id`,`time`,`admin_id`,`action`,`entity_type`,`entity_id`,
                       IFNULL(`summary`,''), IFNULL(`detail_json`,''), IFNULL(`ip_address`,'')
                FROM `epc_erp_audit_log`
                WHERE 1=1
                """;
            var args = new List<object?>();
            if (userId > 0)
            {
                var userKey = userId.ToString(CultureInfo.InvariantCulture);
                sql += " AND (`entity_id` = ? OR `admin_id` = ? OR IFNULL(`detail_json`,'') LIKE ?)";
                args.Add(userId);
                args.Add(userId);
                args.Add("%\"user_id\":\"" + userKey + "\"%");
            }

            if (needle.Length > 0)
            {
                sql += " AND (`action` LIKE ? OR `entity_type` LIKE ? OR IFNULL(`summary`,'') LIKE ? OR IFNULL(`detail_json`,'') LIKE ?)";
                var like = "%" + EscapeLike(needle) + "%";
                args.Add(like);
                args.Add(like);
                args.Add(like);
                args.Add(like);
            }

            sql += " ORDER BY `time` DESC, `id` DESC LIMIT ?";
            args.Add(safeLimit);
            command.CommandText = ErpDb.Positional(sql);
            ErpDb.AddParameters(command, args.ToArray());

            var rows = new List<ErpTenantUserAuditRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new ErpTenantUserAuditRow(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
                    Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture) ?? string.Empty));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    public async Task<ErpSimpleWriteResult> PrepareAsync(
        ErpTenantUserPrepareRequest request,
        CancellationToken cancellationToken = default)
    {
        var invalid = ValidatePrepare(request);
        if (invalid is not null)
        {
            return invalid;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var email = request.Email!.Trim();
        var name = Truncate(request.DisplayName!.Trim(), 128);
        var job = Truncate((request.JobTitle ?? string.Empty).Trim(), 128);
        var phone = Truncate((request.Phone ?? string.Empty).Trim(), 64);
        var department = NormalizeDepartment(request.DepartmentCode);
        var unlocked = request.Unlocked ? 1 : 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsurePayrollAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureAuditLogAsync(connection, cancellationToken).ConfigureAwait(false);

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `users` WHERE `email` = ? LIMIT 1"),
            cancellationToken,
            email).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Fail("exists", "A user with this email already exists.");
        }

        var teamId = await EnsureGroupAsync(connection, TeamGroupValue, cancellationToken).ConfigureAwait(false);
        var deptId = await EnsureGroupAsync(connection, DepartmentGroupValue(department), cancellationToken).ConfigureAwait(false);
        if (teamId <= 0 || deptId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Could not prepare ERP department groups.");
        }

        var adminGroupId = 0L;
        if (request.GrantAdministrator)
        {
            adminGroupId = await FindAdministratorGroupAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var hash = CpUserWriteService.HashStaffPassword(request.Password ?? string.Empty);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var inserted = await TryInsertUserAsync(
                connection,
                transaction,
                email,
                phone,
                hash,
                unlocked,
                now,
                cancellationToken).ConfigureAwait(false);
            if (inserted <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Could not create the account.");
            }

            var userId = inserted;
            var writes = 1;
            writes += await BindGroupAsync(connection, transaction, userId, teamId, cancellationToken).ConfigureAwait(false);
            writes += await BindGroupAsync(connection, transaction, userId, deptId, cancellationToken).ConfigureAwait(false);
            if (adminGroupId > 0)
            {
                writes += await BindGroupAsync(connection, transaction, userId, adminGroupId, cancellationToken).ConfigureAwait(false);
            }

            writes += await UpsertStaffProfileAsync(
                connection,
                transaction,
                userId,
                department,
                name,
                job,
                email,
                phone,
                now,
                cancellationToken).ConfigureAwait(false);

            await _audit.LogAsync(
                connection,
                transaction,
                request.ActorUserId,
                "user_prepare",
                EntityType,
                userId,
                "Prepared ERP " + ErpStaffDepartmentCatalog.NameFor(department) + " user",
                new Dictionary<string, string?>
                {
                    ["user_id"] = userId.ToString(CultureInfo.InvariantCulture),
                    ["email"] = email,
                    ["department"] = department,
                    ["administrator"] = request.GrantAdministrator ? "1" : "0",
                    ["unlocked"] = unlocked.ToString(CultureInfo.InvariantCulture),
                },
                cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Department user prepared.", userId, Math.Max(writes, 1));
        }
        catch (DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not prepare the user.");
        }
    }

    public async Task<ErpSimpleWriteResult> SetCredentialsAsync(
        ErpTenantUserCredentialsRequest request,
        CancellationToken cancellationToken = default)
    {
        var invalid = ValidateCredentials(request);
        if (invalid is not null)
        {
            return invalid;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var action = NormalizeCredentialAction(request.Action);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureAuditLogAsync(connection, cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `users` WHERE `user_id` = ?"),
            cancellationToken,
            request.UserId).ConfigureAwait(false);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "User not found.");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (action == "password")
            {
                var hash = CpUserWriteService.HashStaffPassword(request.Password ?? string.Empty);
                var rows = await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("UPDATE `users` SET `password` = ? WHERE `user_id` = ?"),
                    cancellationToken,
                    hash,
                    request.UserId).ConfigureAwait(false);
                if (rows <= 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("not_found", "Account was not updated.");
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `sessions` WHERE `user_id` = ?"),
                    cancellationToken,
                    request.UserId).ConfigureAwait(false);
                await _audit.LogAsync(
                    connection,
                    transaction,
                    request.ActorUserId,
                    "user_password",
                    EntityType,
                    request.UserId,
                    "ERP user password reset",
                    new Dictionary<string, string?>
                    {
                        ["user_id"] = request.UserId.ToString(CultureInfo.InvariantCulture),
                    },
                    cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Password updated. Other sessions were signed out.", request.UserId);
            }

            var unlocked = action == "unlock" ? 1 : 0;
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `users` SET `unlocked` = ? WHERE `user_id` = ?"),
                cancellationToken,
                unlocked,
                request.UserId).ConfigureAwait(false);
            if (unlocked == 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `sessions` WHERE `user_id` = ?"),
                    cancellationToken,
                    request.UserId).ConfigureAwait(false);
            }

            await _audit.LogAsync(
                connection,
                transaction,
                request.ActorUserId,
                unlocked == 1 ? "user_unlock" : "user_lock",
                EntityType,
                request.UserId,
                unlocked == 1 ? "ERP user unlocked" : "ERP user locked",
                new Dictionary<string, string?>
                {
                    ["user_id"] = request.UserId.ToString(CultureInfo.InvariantCulture),
                    ["unlocked"] = unlocked.ToString(CultureInfo.InvariantCulture),
                },
                cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(unlocked == 1 ? "User unlocked." : "User locked. Open sessions were signed out.", request.UserId);
        }
        catch (DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not update credentials.");
        }
    }

    public async Task<ErpSimpleWriteResult> SetDepartmentAsync(
        ErpTenantUserDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var invalid = ValidateDepartment(request);
        if (invalid is not null)
        {
            return invalid;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var department = NormalizeDepartment(request.DepartmentCode);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsurePayrollAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureAuditLogAsync(connection, cancellationToken).ConfigureAwait(false);
        var email = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1"),
            cancellationToken,
            request.UserId).ConfigureAwait(false);
        if (string.IsNullOrEmpty(email))
        {
            return ErpSimpleWriteResult.Fail("not_found", "User not found.");
        }

        var currentName = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `display_name` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? LIMIT 1"),
            cancellationToken,
            request.UserId).ConfigureAwait(false);
        var currentJob = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `job_title` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? LIMIT 1"),
            cancellationToken,
            request.UserId).ConfigureAwait(false);
        var name = Truncate((request.DisplayName ?? currentName ?? email).Trim(), 128);
        if (name.Length == 0)
        {
            name = email;
        }

        var job = Truncate((request.JobTitle ?? currentJob ?? string.Empty).Trim(), 128);
        var phone = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `phone` FROM `users` WHERE `user_id` = ? LIMIT 1"),
            cancellationToken,
            request.UserId).ConfigureAwait(false) ?? string.Empty;
        var teamId = await EnsureGroupAsync(connection, TeamGroupValue, cancellationToken).ConfigureAwait(false);
        var deptId = await EnsureGroupAsync(connection, DepartmentGroupValue(department), cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await RemoveDepartmentBindsAsync(connection, transaction, request.UserId, cancellationToken).ConfigureAwait(false);
            await BindGroupAsync(connection, transaction, request.UserId, teamId, cancellationToken).ConfigureAwait(false);
            await BindGroupAsync(connection, transaction, request.UserId, deptId, cancellationToken).ConfigureAwait(false);
            await UpsertStaffProfileAsync(
                connection,
                transaction,
                request.UserId,
                department,
                name,
                job,
                email,
                phone,
                now,
                cancellationToken).ConfigureAwait(false);
            await _audit.LogAsync(
                connection,
                transaction,
                request.ActorUserId,
                "user_department",
                EntityType,
                request.UserId,
                "ERP user department set to " + ErpStaffDepartmentCatalog.NameFor(department),
                new Dictionary<string, string?>
                {
                    ["user_id"] = request.UserId.ToString(CultureInfo.InvariantCulture),
                    ["department"] = department,
                },
                cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Department updated.", request.UserId);
        }
        catch (DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not update the department.");
        }
    }

    private async Task<IReadOnlyList<ErpTenantUserRow>> LoadUsersAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var groupMap = await LoadUserGroupsAsync(connection, cancellationToken).ConfigureAwait(false);
        var sessionMap = await LoadOpenSessionCountsAsync(connection, cancellationToken).ConfigureAwait(false);
        var adminGroupIds = groupMap.Values
            .SelectMany(pairs => pairs)
            .Where(pair => ErpUserAccess.IsAdministratorGroup(pair.Value))
            .Select(pair => pair.Id)
            .ToHashSet();
        var teamId = groupMap.Values
            .SelectMany(pairs => pairs)
            .Where(pair => string.Equals(pair.Value, TeamGroupValue, StringComparison.Ordinal))
            .Select(pair => pair.Id)
            .FirstOrDefault();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT u.`user_id`, IFNULL(u.`email`,''), IFNULL(u.`phone`,''), IFNULL(u.`unlocked`,0),
                   IFNULL(u.`time_registered`,0), IFNULL(u.`time_last_visit`,0),
                   IFNULL(p.`display_name`,''), IFNULL(p.`job_title`,''), IFNULL(p.`department_code`,'')
            FROM `users` u
            LEFT JOIN `epc_erp_staff_profiles` p ON p.`user_id` = u.`user_id`
            ORDER BY u.`user_id` DESC
            """;
        var rows = new List<ErpTenantUserRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var userId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            var groups = groupMap.TryGetValue(userId, out var bound) ? bound : [];
            var hasStaff = !string.IsNullOrWhiteSpace(Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture));
            var erpTeam = teamId > 0 && groups.Any(g => g.Id == teamId);
            var departmentFromGroup = groups
                .Select(g => DepartmentCodeFromGroup(g.Value))
                .FirstOrDefault(code => code.Length > 0) ?? string.Empty;
            var department = Convert.ToString(reader.GetValue(8), CultureInfo.InvariantCulture) ?? string.Empty;
            if (department.Length == 0)
            {
                department = departmentFromGroup;
            }

            var erpCapable = groups.Any(g =>
                g.ForBackend
                || IsErpDepartmentGroup(g.Value)
                || g.Value.Equals(TeamGroupValue, StringComparison.Ordinal));
            if (!hasStaff && !erpCapable)
            {
                continue;
            }

            var display = Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture) ?? string.Empty;
            var email = Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
            rows.Add(new ErpTenantUserRow(
                userId,
                email,
                Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                display.Length > 0 ? display : email,
                Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? string.Empty,
                department,
                ErpStaffDepartmentCatalog.NameFor(department),
                Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) == 1,
                erpTeam,
                groups.Any(g => adminGroupIds.Contains(g.Id) || ErpUserAccess.IsAdministratorGroup(g.Value)),
                sessionMap.TryGetValue(userId, out var open) ? open : 0,
                Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
                Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<Dictionary<long, List<(long Id, string Value, bool ForBackend)>>> LoadUserGroupsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, List<(long Id, string Value, bool ForBackend)>>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT b.`user_id`, g.`id`, IFNULL(g.`value`,''), IFNULL(g.`for_backend`,0)
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
                    Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture) == 1));
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

    private static async Task<long> TryInsertUserAsync(
        DbConnection connection,
        DbTransaction transaction,
        string email,
        string phone,
        string hash,
        int unlocked,
        long now,
        CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    """
                    INSERT INTO `users` (`reg_variant`, `email`, `email_confirmed`, `phone`, `phone_confirmed`, `password`, `unlocked`, `time_registered`, `admin_created`)
                    VALUES (1,?,?,?,?,?,?,?,1)
                    """),
                cancellationToken,
                email, 1, phone.Length == 0 ? null : phone, phone.Length == 0 ? 0 : 1, hash, unlocked, now).ConfigureAwait(false);
        }
        catch (DbException)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    """
                    INSERT INTO `users` (`reg_variant`, `email`, `email_confirmed`, `phone`, `phone_confirmed`, `password`, `unlocked`, `time_registered`)
                    VALUES (1,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                email, 1, phone.Length == 0 ? null : phone, phone.Length == 0 ? 0 : 1, hash, unlocked, now).ConfigureAwait(false);
        }

        return await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> UpsertStaffProfileAsync(
        DbConnection connection,
        DbTransaction transaction,
        long userId,
        string department,
        string name,
        string job,
        string email,
        string phone,
        long now,
        CancellationToken cancellationToken)
    {
        var existing = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? LIMIT 1"),
            cancellationToken,
            userId).ConfigureAwait(false);
        if (existing > 0)
        {
            return await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    """
                    UPDATE `epc_erp_staff_profiles`
                    SET `department_code` = ?, `display_name` = ?, `job_title` = ?, `email` = ?, `phone` = ?, `active` = 1
                    WHERE `user_id` = ?
                    """),
                cancellationToken,
                department, name, job.Length == 0 ? null : job, email, phone.Length == 0 ? null : phone, userId).ConfigureAwait(false);
        }

        return await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                """
                INSERT INTO `epc_erp_staff_profiles`
                    (`user_id`, `department_code`, `display_name`, `job_title`, `email`, `phone`, `active`, `time_created`)
                VALUES (?,?,?,?,?,?,1,?)
                """),
            cancellationToken,
            userId, department, name, job.Length == 0 ? null : job, email, phone.Length == 0 ? null : phone, now).ConfigureAwait(false);
    }

    private static async Task<long> EnsureGroupAsync(
        DbConnection connection,
        string value,
        CancellationToken cancellationToken)
    {
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `groups` WHERE `value` = ? LIMIT 1"),
            cancellationToken,
            value).ConfigureAwait(false);
        if (existing > 0)
        {
            await ErpDb.TryExecuteAsync(
                connection,
                "UPDATE `groups` SET `for_backend` = 1, `unblocked` = 1 WHERE `id` = "
                    + existing.ToString(CultureInfo.InvariantCulture)
                    + " AND (`for_backend` = 0 OR `unblocked` = 0)",
                cancellationToken).ConfigureAwait(false);
            return existing;
        }

        var nextId = await ErpDb.LongAsync(
            connection,
            null,
            "SELECT COALESCE(MAX(`id`),0)+1 FROM `groups`",
            cancellationToken).ConfigureAwait(false);
        if (nextId <= 0)
        {
            nextId = 100;
        }

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `groups` (`id`, `value`, `count`, `level`, `parent`, `unblocked`, `for_guests`, `for_registrated`, `for_backend`)
                    VALUES (?, ?, 0, 0, 0, 1, 0, 0, 1)
                    """),
                cancellationToken,
                nextId,
                value).ConfigureAwait(false);
            return nextId;
        }
        catch (DbException)
        {
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        INSERT INTO `groups` (`value`, `count`, `level`, `parent`, `unblocked`, `for_backend`)
                        VALUES (?, 0, 0, 0, 1, 1)
                        """),
                    cancellationToken,
                    value).ConfigureAwait(false);
                return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException)
            {
                return await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `id` FROM `groups` WHERE `value` = ? LIMIT 1"),
                    cancellationToken,
                    value).ConfigureAwait(false);
            }
        }
    }

    private static async Task<long> FindAdministratorGroupAsync(DbConnection connection, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `id` FROM `groups` WHERE `value` LIKE ? OR `value` LIKE ? ORDER BY `id` LIMIT 1"),
            cancellationToken,
            "%Administrator%",
            "%Администратор%").ConfigureAwait(false);

    private static async Task<int> BindGroupAsync(
        DbConnection connection,
        DbTransaction transaction,
        long userId,
        long groupId,
        CancellationToken cancellationToken)
    {
        if (groupId <= 0)
        {
            return 0;
        }

        return await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?,?)"),
            cancellationToken,
            userId,
            groupId).ConfigureAwait(false);
    }

    private static async Task RemoveDepartmentBindsAsync(
        DbConnection connection,
        DbTransaction transaction,
        long userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(
            """
            DELETE b FROM `users_groups_bind` b
            INNER JOIN `groups` g ON g.`id` = b.`group_id`
            WHERE b.`user_id` = ? AND g.`value` LIKE 'EPC_ERP_DEPT_%'
            """);
        ErpDb.AddParameters(command, userId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string DepartmentCodeFromGroup(string value)
    {
        if (!IsErpDepartmentGroup(value))
        {
            return string.Empty;
        }

        return NormalizeDepartment(value["EPC_ERP_DEPT_".Length..]);
    }

    private static bool Contains(string haystack, string needle)
        => haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];
}
