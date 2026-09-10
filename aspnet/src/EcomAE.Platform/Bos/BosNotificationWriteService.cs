using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>notifications</c> <c>mark_read</c> / <c>epc_notifications_mark_read</c>,
/// <c>dismiss</c> / <c>epc_notifications_dismiss</c>, <c>mark_all_read</c> / <c>epc_notifications_mark_all_read</c>,
/// <c>prefs_save</c> / <c>epc_notification_prefs_save</c>, and <c>cleanup</c> / <c>epc_notifications_cleanup</c>.
/// Send and broadcast stay Classic.
/// This service does not invent a send. It does not emit CREATE/ALTER.
/// </summary>
public interface IBosNotificationWriteService
{
    Task<ErpSimpleWriteResult> MarkReadAsync(
        string? idsRaw,
        string? tenantKey,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DismissAsync(
        long notificationId,
        string? tenantKey,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> MarkAllReadAsync(
        string? tenantKey,
        long userId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SavePrefsAsync(
        string? tenantKey,
        long userId,
        string? category,
        string? channelInApp,
        string? channelEmail,
        string? channelWebhook,
        string? emailDigest,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CleanupAsync(
        string? daysRaw,
        CancellationToken cancellationToken = default);
}

public sealed class BosNotificationWriteService : IBosNotificationWriteService
{
    public const string DefaultTenantKey = "__platform__";
    public const string MissingIdsMessage = "Missing ids";

    private readonly IErpWriteConnectionFactory _connections;

    public BosNotificationWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>(string) ($_POST['tenant_key'] ?? '__platform__')</c>.</summary>
    public static string ResolveTenantKey(string? tenantKey)
        => tenantKey is null ? DefaultTenantKey : tenantKey;

    /// <summary>
    /// PHP <c>json_decode</c> array + <c>array_map('intval')</c>, plus comma-separated
    /// form convenience for the native SSR field.
    /// </summary>
    public static bool TryParseIds(string? raw, out IReadOnlyList<long> ids)
    {
        ids = [];
        if (raw is null)
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed[0] is '[' or '{')
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                var parsed = new List<long>();
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    parsed.Add(PhpIntval(element));
                }

                if (parsed.Count == 0)
                {
                    return false;
                }

                ids = parsed;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        var parts = trimmed.Split([',', ' ', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        ids = parts.Select(PhpIntval).ToArray();
        return true;
    }

    public async Task<ErpSimpleWriteResult> MarkReadAsync(
        string? idsRaw,
        string? tenantKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseIds(idsRaw, out var ids))
        {
            return ErpSimpleWriteResult.Fail("invalid", MissingIdsMessage);
        }

        var key = ResolveTenantKey(tenantKey);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var placeholders = string.Join(",", Enumerable.Repeat("?", ids.Count));
            var sql = ErpDb.Positional(
                $"""
                UPDATE `epc_notifications` SET `is_read` = 1, `read_at` = NOW()
                WHERE `id` IN ({placeholders}) AND `tenant_key` = ?
                """);
            var args = ids.Cast<object?>().Append(key).ToArray();
            var marked = await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken, args)
                .ConfigureAwait(false);
            var noun = marked == 1 ? "notification" : "notifications";
            return ErpSimpleWriteResult.Ok($"Marked {marked.ToString(CultureInfo.InvariantCulture)} {noun} read", marked);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Notifications table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> DismissAsync(
        long notificationId,
        string? tenantKey,
        CancellationToken cancellationToken = default)
    {
        var key = ResolveTenantKey(tenantKey);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var marked = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_notifications` SET `dismissed` = 1
                    WHERE `id` = ? AND `tenant_key` = ?
                    """),
                cancellationToken, notificationId, key).ConfigureAwait(false);
            if (marked <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Notification not found");
            }

            return ErpSimpleWriteResult.Ok("Notification dismissed", notificationId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Notifications table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> MarkAllReadAsync(
        string? tenantKey,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var key = ResolveTenantKey(tenantKey);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var marked = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_notifications` SET `is_read` = 1, `read_at` = NOW()
                    WHERE `tenant_key` = ? AND (`user_id` = ? OR `user_id` = 0) AND `is_read` = 0
                    """),
                cancellationToken, key, userId).ConfigureAwait(false);
            var noun = marked == 1 ? "notification" : "notifications";
            return ErpSimpleWriteResult.Ok($"Marked {marked.ToString(CultureInfo.InvariantCulture)} {noun} read", marked);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Notifications table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP <c>(string) ($_POST['category'] ?? '*')</c>.</summary>
    public static string ResolveCategory(string? category)
        => category is null ? "*" : category;

    /// <summary>PHP <c>(string) ($_POST['email_digest'] ?? 'daily')</c>.</summary>
    public static string ResolveDigest(string? digest)
        => digest is null ? "daily" : digest;

    /// <summary>PHP <c>(int) ($_POST['channel_*'] ?? $default)</c>.</summary>
    public static int ResolveChannel(string? raw, int whenMissing)
        => raw is null ? whenMissing : (int)PhpIntval(raw);

    public async Task<ErpSimpleWriteResult> SavePrefsAsync(
        string? tenantKey,
        long userId,
        string? category,
        string? channelInApp,
        string? channelEmail,
        string? channelWebhook,
        string? emailDigest,
        CancellationToken cancellationToken = default)
    {
        var key = ResolveTenantKey(tenantKey);
        var cat = ResolveCategory(category);
        var digest = ResolveDigest(emailDigest);
        var inApp = ResolveChannel(channelInApp, 1);
        var email = ResolveChannel(channelEmail, 1);
        var webhook = ResolveChannel(channelWebhook, 0);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_notification_prefs`
                        (`tenant_key`, `user_id`, `category`, `channel_in_app`, `channel_email`, `channel_webhook`, `email_digest`)
                    VALUES (?, ?, ?, ?, ?, ?, ?)
                    ON DUPLICATE KEY UPDATE
                        `channel_in_app` = VALUES(`channel_in_app`),
                        `channel_email` = VALUES(`channel_email`),
                        `channel_webhook` = VALUES(`channel_webhook`),
                        `email_digest` = VALUES(`email_digest`)
                    """),
                cancellationToken, key, userId, cat, inApp, email, webhook, digest).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Notification preferences saved", userId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Notifications table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP <c>max(7, (int) ($_POST['days'] ?? 90))</c>.</summary>
    public static int ResolveDays(string? raw)
        => raw is null ? 90 : Math.Max(7, (int)PhpIntval(raw));

    public async Task<ErpSimpleWriteResult> CleanupAsync(
        string? daysRaw,
        CancellationToken cancellationToken = default)
    {
        var days = ResolveDays(daysRaw);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-days * 86400.0);
            var cutoffText = cutoff.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var deleted = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    DELETE FROM `epc_notifications` WHERE `created_at` < ? AND `is_read` = 1
                    """),
                cancellationToken, cutoffText).ConfigureAwait(false);
            var noun = deleted == 1 ? "notification" : "notifications";
            return ErpSimpleWriteResult.Ok($"Deleted {deleted.ToString(CultureInfo.InvariantCulture)} {noun}", deleted);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Notifications table is missing — schema-ensure stays Classic.");
        }
    }

    private static long PhpIntval(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var n) => n,
            JsonValueKind.Number when element.TryGetDecimal(out var d) => (long)d,
            JsonValueKind.String => PhpIntval(element.GetString()),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => 0
        };

    /// <summary>PHP <c>intval</c> on a token (leading optional sign + digits).</summary>
    public static long PhpIntval(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var text = raw.Trim();
        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        while (i < text.Length && char.IsDigit(text[i]))
        {
            i++;
        }

        if (i == 0 || (i == 1 && text[0] is '+' or '-'))
        {
            return 0;
        }

        return long.TryParse(text[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }
}
