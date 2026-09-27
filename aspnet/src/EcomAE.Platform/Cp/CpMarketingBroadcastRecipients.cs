using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>PHP <c>epc_mb_resolve_recipients()</c> row.</summary>
public sealed record CpMarketingRecipient(int UserId, string Email, string Phone, string Name);

/// <summary>
/// Twin of the PHP recipient/schema helpers in content/shop/marketing/epc_marketing_broadcast_helpers.php
/// (<c>epc_mb_ensure_schema</c>, <c>epc_mb_resolve_recipients</c>, <c>epc_mb_count_recipients</c>,
/// <c>epc_mb_normalize_post</c>).
/// </summary>
public static class CpMarketingBroadcastRecipients
{
    public const int MaxResolved = 2000;

    public static readonly string[] Modes = ["all", "with_orders", "group", "manual"];

    public static string NormalizeMode(string? mode)
    {
        var value = (mode ?? string.Empty).Trim().ToLowerInvariant();
        return Modes.Contains(value) ? value : "all";
    }

    public static string NormalizeChannel(string? channel)
        => (channel ?? string.Empty).Trim().ToLowerInvariant() == "whatsapp" ? "whatsapp" : "email";

    /// <summary>PHP: <c>min(100, max(1, batch_limit))</c>, default 50.</summary>
    public static int NormalizeBatchLimit(string? raw)
    {
        if (!int.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            value = 50;
        }

        return Math.Clamp(value, 1, 100);
    }

    /// <summary>PHP <c>epc_mb_normalize_post()</c>: group / manual inputs feed audience_meta.</summary>
    public static string ResolveMeta(string mode, string? meta, string? group, string? manual)
        => mode switch
        {
            "group" => (group ?? meta ?? string.Empty).Trim(),
            "manual" => manual ?? meta ?? string.Empty,
            _ => (meta ?? string.Empty).Trim(),
        };

    /// <summary>PHP <c>epc_wa_share_url()</c> (digits only, rawurlencoded text).</summary>
    public static string WhatsappShareUrl(string? phone, string text)
    {
        var digits = new string((phone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? string.Empty : "https://wa.me/" + digits + "?text=" + Uri.EscapeDataString(text);
    }

    public static string ShopUrlFromHost(string? host)
    {
        var value = (host ?? string.Empty).Trim();
        return value.Length == 0 ? "https://localhost" : "https://" + value;
    }

    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_marketing_broadcast_campaigns` ("
            + "`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,"
            + "`created_at` INT UNSIGNED NOT NULL,"
            + "`channel` ENUM('email','whatsapp') NOT NULL,"
            + "`template_key` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`subject` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`preview` VARCHAR(500) NOT NULL DEFAULT '',"
            + "`body_html` MEDIUMTEXT,"
            + "`body_text` MEDIUMTEXT,"
            + "`audience_mode` VARCHAR(32) NOT NULL DEFAULT 'all',"
            + "`audience_meta` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`total_targets` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`sent_ok` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`sent_fail` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`status` VARCHAR(24) NOT NULL DEFAULT 'draft',"
            + "`operator_id` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`), KEY `created_at` (`created_at`), KEY `channel` (`channel`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_marketing_broadcast_log` ("
            + "`id` INT UNSIGNED NOT NULL AUTO_INCREMENT,"
            + "`campaign_id` INT UNSIGNED NOT NULL,"
            + "`created_at` INT UNSIGNED NOT NULL,"
            + "`recipient` VARCHAR(255) NOT NULL DEFAULT '',"
            + "`user_id` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`status` TINYINT(1) NOT NULL DEFAULT 0,"
            + "`detail` VARCHAR(500) NOT NULL DEFAULT '',"
            + "`wa_link` VARCHAR(500) NOT NULL DEFAULT '',"
            + "PRIMARY KEY (`id`), KEY `campaign_id` (`campaign_id`), KEY `created_at` (`created_at`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP manual-list parsing: split on newline / comma / semicolon, emails must contain '@'.</summary>
    public static IReadOnlyList<CpMarketingRecipient> ManualRecipients(string meta, string channel)
    {
        var rows = new List<CpMarketingRecipient>();
        foreach (var raw in (meta ?? string.Empty).Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (channel == "email" && !line.Contains('@', StringComparison.Ordinal))
            {
                continue;
            }

            rows.Add(new CpMarketingRecipient(
                0,
                channel == "email" ? line : string.Empty,
                channel == "whatsapp" ? line : string.Empty,
                string.Empty));
        }

        return rows;
    }

    public static async Task<IReadOnlyList<CpMarketingRecipient>> ResolveAsync(
        DbConnection connection,
        string mode,
        string meta,
        string channel,
        CancellationToken cancellationToken)
    {
        mode = NormalizeMode(mode);
        channel = NormalizeChannel(channel);
        if (mode == "manual")
        {
            return ManualRecipients(meta, channel);
        }

        var sql =
            "SELECT u.`user_id`, u.`email`, u.`phone`, "
            + "MAX(CASE WHEN up.`data_key` = 'name' THEN up.`data_value` END) AS fname, "
            + "MAX(CASE WHEN up.`data_key` = 'surname' THEN up.`data_value` END) AS sname "
            + "FROM `users` u LEFT JOIN `users_profiles` up ON up.`user_id` = u.`user_id`";
        var parameters = new List<object?>();
        var where = " WHERE u.`user_id` > 0";

        if (mode == "group" && int.TryParse(meta.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var groupId) && groupId > 0)
        {
            sql += " INNER JOIN `users_groups_bind` b ON b.`user_id` = u.`user_id`";
            where += " AND b.`group_id` = ?";
            parameters.Add(groupId);
        }
        else if (mode == "with_orders")
        {
            where += " AND EXISTS (SELECT 1 FROM `shop_orders` o WHERE o.`user_id` = u.`user_id` AND o.`successfully_created` = 1)";
        }

        where += channel == "email"
            ? " AND u.`email` != '' AND u.`email` LIKE '%@%'"
            : " AND u.`phone` != ''";

        sql += where + " GROUP BY u.`user_id` ORDER BY u.`user_id` DESC LIMIT " + MaxResolved.ToString(CultureInfo.InvariantCulture);

        var rows = new List<CpMarketingRecipient>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(cmd, [.. parameters]);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var first = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var last = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
            var name = (first + " " + last).Trim();
            rows.Add(new CpMarketingRecipient(
                reader.GetInt32(0),
                (reader.IsDBNull(1) ? string.Empty : reader.GetString(1)).Trim(),
                (reader.IsDBNull(2) ? string.Empty : reader.GetString(2)).Trim(),
                name.Length > 0 ? name : "Customer"));
        }

        return rows;
    }

    public static async Task<int> CountAsync(
        DbConnection connection,
        string mode,
        string meta,
        string channel,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await ResolveAsync(connection, mode, meta, channel, cancellationToken).ConfigureAwait(false)).Count;
        }
        catch (DbException)
        {
            return 0;
        }
    }
}
