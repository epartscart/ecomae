using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_super_cp_communication.php</c> twin of <c>epc_scp_task_save</c>,
/// <c>epc_scp_task_delete</c>, <c>epc_scp_comm_settings_save</c>, <c>epc_scp_tasks_list</c>,
/// <c>epc_scp_platform_users</c> and the comm/task part of <c>epc_scp_platform_ensure_schema</c>.
/// </summary>
public interface ICpPlatformCommunicationWriteService
{
    Task<ErpSimpleWriteResult> SaveTaskAsync(
        CpPlatformCommunicationSaveTaskRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteTaskAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> LoadSettingsAsync(
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveSettingsAsync(
        CpPlatformCommunicationSaveSettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<CpPlatformTaskList> ListTasksAsync(
        string statusFilter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpPlatformUserOption>> ListPlatformUsersAsync(
        CancellationToken cancellationToken = default);
}

public sealed record CpPlatformTaskRow(
    long Id,
    string Title,
    string Description,
    long AssignedTo,
    string AssignedEmail,
    string SiteKey,
    string Category,
    string Status,
    string Priority,
    long DueAt);

public sealed record CpPlatformTaskList(IReadOnlyList<CpPlatformTaskRow> Tasks, string Error);

public sealed record CpPlatformUserOption(long UserId, string Label);

public sealed record CpPlatformCommunicationSaveTaskRequest(
    long Id,
    string? Title,
    string? Description,
    long AssignedTo,
    string? AssignedEmail,
    string? SiteKey,
    string? Category,
    string? Status,
    string? Priority,
    long DueAt,
    long CreatedBy,
    bool KeepDescriptionWhenBlank = true);

public sealed record CpPlatformCommunicationSaveSettingsRequest(
    string? FromName,
    string? FromEmail,
    string? ReplyTo,
    string? DigestHourUtc,
    bool NotifyTenantOnboard,
    bool NotifyTenantDnsLive,
    bool NotifyDemoExpiry,
    bool NotifyTaskAssigned,
    bool NotifyDailyDigest);

public sealed class CpPlatformCommunicationWriteService : ICpPlatformCommunicationWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<string, string> Categories = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["onboarding"] = "Onboarding",
        ["support"] = "Support",
        ["billing"] = "Billing",
        ["pricing"] = "Pricing",
        ["content"] = "Content",
        ["other"] = "Other",
    };

    public static readonly IReadOnlyDictionary<string, string> Statuses = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["open"] = "Open",
        ["in_progress"] = "In progress",
        ["done"] = "Done",
        ["cancelled"] = "Cancelled",
    };

    public static readonly IReadOnlyDictionary<string, string> Priorities = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["low"] = "Low",
        ["normal"] = "Normal",
        ["high"] = "High",
        ["urgent"] = "Urgent",
    };

    public static readonly IReadOnlyDictionary<string, string> DefaultSettings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["notify_from_name"] = "ECOM AE Platform",
        ["notify_from_email"] = "noreply@ecomae.com",
        ["notify_reply_to"] = "support@ecomae.com",
        ["notify_tenant_onboard"] = "1",
        ["notify_tenant_dns_live"] = "1",
        ["notify_demo_expiry"] = "1",
        ["notify_task_assigned"] = "1",
        ["notify_daily_digest"] = "0",
        ["digest_hour_utc"] = "6",
    };

    public static readonly IReadOnlyList<string> NotifyFlagKeys =
    [
        "notify_tenant_onboard",
        "notify_tenant_dns_live",
        "notify_demo_expiry",
        "notify_task_assigned",
        "notify_daily_digest",
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public CpPlatformCommunicationWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string NormalizeCategory(string? raw)
    {
        var category = (raw ?? string.Empty).Trim();
        return Categories.ContainsKey(category) ? category : "support";
    }

    public static string NormalizeStatus(string? raw)
    {
        var status = (raw ?? string.Empty).Trim();
        return Statuses.ContainsKey(status) ? status : "open";
    }

    public static string NormalizePriority(string? raw)
    {
        var priority = (raw ?? string.Empty).Trim();
        return Priorities.ContainsKey(priority) ? priority : "normal";
    }

    public static string NormalizeDigestHour(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (!int.TryParse(text, out var hour))
        {
            return "6";
        }

        if (hour < 0)
        {
            hour = 0;
        }

        if (hour > 23)
        {
            hour = 23;
        }

        return hour.ToString();
    }

    public static bool SettingFlag(IReadOnlyDictionary<string, string> settings, string key)
    {
        if (!settings.TryGetValue(key, out var raw))
        {
            return DefaultSettings.TryGetValue(key, out var fallback) && fallback != "0" && fallback.Length > 0;
        }

        return raw.Length > 0 && raw != "0";
    }

    public async Task<ErpSimpleWriteResult> SaveTaskAsync(
        CpPlatformCommunicationSaveTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Title is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var description = request.Description ?? string.Empty;
        var assignedTo = request.AssignedTo < 0 ? 0 : request.AssignedTo;
        var assignedEmail = (request.AssignedEmail ?? string.Empty).Trim().ToLowerInvariant();
        var siteKey = NormalizeSiteKey(request.SiteKey);
        var category = NormalizeCategory(request.Category);
        var status = NormalizeStatus(request.Status);
        var priority = NormalizePriority(request.Priority);
        var dueAt = request.DueAt < 0 ? 0 : request.DueAt;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var createdBy = request.CreatedBy < 0 ? 0 : request.CreatedBy;

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var existing = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_platform_internal_tasks` WHERE `id`=? LIMIT 1"),
                    cancellationToken, request.Id).ConfigureAwait(false);
                if (existing <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Task not found");
                }

                if (description.Length == 0 && request.KeepDescriptionWhenBlank)
                {
                    description = await ErpDb.StringAsync(
                        connection, null,
                        "SELECT IFNULL(`description`, '') FROM `epc_platform_internal_tasks` WHERE `id` = @p0 LIMIT 1",
                        cancellationToken, request.Id).ConfigureAwait(false) ?? string.Empty;
                }

                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_platform_internal_tasks` SET `title`=?, `description`=?, `assigned_to`=?, `assigned_email`=?, `site_key`=?, `category`=?, `status`=?, `priority`=?, `due_at`=?, `updated_at`=? WHERE `id`=?"),
                    cancellationToken, title, description, assignedTo, assignedEmail, siteKey, category, status, priority, dueAt, now, request.Id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Task saved.", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_platform_internal_tasks` (`title`, `description`, `assigned_to`, `assigned_email`, `site_key`, `category`, `status`, `priority`, `due_at`, `updated_at`, `created_by`, `created_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken, title, description, assignedTo, assignedEmail, siteKey, category, status, priority, dueAt, now, createdBy, now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Task saved.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Platform database unavailable.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteTaskAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Task id is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `epc_platform_internal_tasks` WHERE `id`=?"),
                cancellationToken, id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Task deleted.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Platform database unavailable.");
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = new Dictionary<string, string>(DefaultSettings, StringComparer.Ordinal);
        if (!_connections.IsConfigured)
        {
            return settings;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `setting_key`, `setting_value` FROM `epc_platform_comm_settings`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = reader.IsDBNull(0) ? "" : reader.GetString(0);
                if (!settings.ContainsKey(key))
                {
                    continue;
                }

                settings[key] = reader.IsDBNull(1) ? "" : reader.GetString(1);
            }
        }
        catch (DbException)
        {
            // Database unavailable; form keeps PHP defaults.
        }

        return settings;
    }

    public async Task<ErpSimpleWriteResult> SaveSettingsAsync(
        CpPlatformCommunicationSaveSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var rows = new (string Key, string Value)[]
        {
            ("notify_from_name", (request.FromName ?? string.Empty).Trim()),
            ("notify_from_email", (request.FromEmail ?? string.Empty).Trim()),
            ("notify_reply_to", (request.ReplyTo ?? string.Empty).Trim()),
            ("digest_hour_utc", NormalizeDigestHour(request.DigestHourUtc)),
            ("notify_tenant_onboard", request.NotifyTenantOnboard ? "1" : "0"),
            ("notify_tenant_dns_live", request.NotifyTenantDnsLive ? "1" : "0"),
            ("notify_demo_expiry", request.NotifyDemoExpiry ? "1" : "0"),
            ("notify_task_assigned", request.NotifyTaskAssigned ? "1" : "0"),
            ("notify_daily_digest", request.NotifyDailyDigest ? "1" : "0"),
        };
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("INSERT INTO `epc_platform_comm_settings` (`setting_key`, `setting_value`, `updated_at`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`), `updated_at` = VALUES(`updated_at`)"),
                    cancellationToken, row.Key, row.Value, now).ConfigureAwait(false);
            }

            return ErpSimpleWriteResult.Ok("Notification settings saved.", 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Platform database unavailable.");
        }
    }

    public const string SchemaCommSql = """
        CREATE TABLE IF NOT EXISTS `epc_platform_comm_settings` (
          `setting_key` VARCHAR(64) NOT NULL PRIMARY KEY,
          `setting_value` TEXT NULL,
          `updated_at` INT NOT NULL DEFAULT 0
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """;

    public const string SchemaTasksSql = """
        CREATE TABLE IF NOT EXISTS `epc_platform_internal_tasks` (
          `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
          `title` VARCHAR(200) NOT NULL,
          `description` TEXT NULL,
          `assigned_to` INT NOT NULL DEFAULT 0,
          `assigned_email` VARCHAR(120) NOT NULL DEFAULT '',
          `site_key` VARCHAR(64) NOT NULL DEFAULT '',
          `category` VARCHAR(32) NOT NULL DEFAULT 'support',
          `status` VARCHAR(24) NOT NULL DEFAULT 'open',
          `priority` VARCHAR(16) NOT NULL DEFAULT 'normal',
          `due_at` INT NOT NULL DEFAULT 0,
          `created_by` INT NOT NULL DEFAULT 0,
          `created_at` INT NOT NULL DEFAULT 0,
          `updated_at` INT NOT NULL DEFAULT 0,
          KEY `status_priority` (`status`, `priority`),
          KEY `assigned_email` (`assigned_email`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
        """;

    private const string TaskColumns = "`id`, `title`, IFNULL(`description`,''), `assigned_to`, `assigned_email`, `site_key`, `category`, `status`, `priority`, `due_at`";

    /// <summary>PHP <c>epc_scp_tasks_list</c> ordering (filtered: priority, due, id desc; all: status, priority, due, id desc, limit 200).</summary>
    public static string TasksSql(bool filtered)
        => filtered
            ? "SELECT " + TaskColumns + " FROM `epc_platform_internal_tasks` WHERE `status` = @p0 ORDER BY FIELD(`priority`, 'urgent', 'high', 'normal', 'low'), `due_at` ASC, `id` DESC"
            : "SELECT " + TaskColumns + " FROM `epc_platform_internal_tasks` ORDER BY FIELD(`status`, 'open', 'in_progress', 'done', 'cancelled'), FIELD(`priority`, 'urgent', 'high', 'normal', 'low'), `due_at` ASC, `id` DESC LIMIT 200";

    public const string PlatformUsersSql =
        "SELECT u.`user_id`, u.`email`, MAX(CASE WHEN up.`data_key` = 'name' THEN up.`data_value` END) AS fname FROM `users` u LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id` WHERE u.`user_id` > 0 GROUP BY u.`user_id`, u.`email` ORDER BY u.`email` ASC LIMIT 100";

    /// <summary>PHP <c>strtotime</c> subset for the <c>type=date</c> due field: digits pass through, <c>yyyy-MM-dd</c> → UTC midnight.</summary>
    public static long ParseDueAt(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return 0;
        }

        if (value.All(char.IsDigit))
        {
            return long.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var unix) ? unix : 0;
        }

        return DateTime.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var date)
            ? new DateTimeOffset(date, TimeSpan.Zero).ToUnixTimeSeconds()
            : 0;
    }

    public static string DueDate(long dueAt)
        => dueAt <= 0 ? string.Empty : DateTimeOffset.FromUnixTimeSeconds(dueAt).UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public async Task<CpPlatformTaskList> ListTasksAsync(
        string statusFilter,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], "TenantRegistry DB is not configured.");
        }

        var filter = (statusFilter ?? string.Empty).Trim();
        var filtered = Statuses.ContainsKey(filter);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = TasksSql(filtered);
            if (filtered)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@p0";
                parameter.Value = filter;
                command.Parameters.Add(parameter);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<CpPlatformTaskRow>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpPlatformTaskRow(
                    Number(reader, 0), Text(reader, 1), Text(reader, 2), Number(reader, 3), Text(reader, 4),
                    Text(reader, 5), Text(reader, 6), Text(reader, 7), Text(reader, 8), Number(reader, 9)));
            }

            return new(rows, string.Empty);
        }
        catch (DbException)
        {
            return new([], "Platform database unavailable.");
        }
    }

    public async Task<IReadOnlyList<CpPlatformUserOption>> ListPlatformUsersAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = PlatformUsersSql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<CpPlatformUserOption>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = Text(reader, 2).Trim();
                rows.Add(new CpPlatformUserOption(Number(reader, 0), name.Length > 0 ? name : Text(reader, 1)));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : Convert.ToString(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    private static long Number(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt64(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);

    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(connection, null, SchemaCommSql, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, SchemaTasksSql, cancellationToken).ConfigureAwait(false);
    }
}
