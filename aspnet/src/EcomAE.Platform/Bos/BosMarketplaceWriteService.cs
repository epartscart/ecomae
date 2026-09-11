using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>marketplace</c> <c>uninstall</c> / <c>epc_marketplace_uninstall</c>,
/// <c>review</c> / <c>epc_marketplace_add_review</c>, and <c>install</c> / <c>epc_marketplace_install</c>.
/// Seed and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. Uninstall and review always return ok — this write does not invent extra id/site-key checks.
/// </summary>
public interface IBosMarketplaceWriteService
{
    Task<ErpSimpleWriteResult> UninstallAsync(
        long appId,
        string? siteKey,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddReviewAsync(
        long appId,
        string? siteKey,
        string? reviewData,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> InstallAsync(
        long appId,
        string? siteKey,
        long installedBy,
        CancellationToken cancellationToken = default);
}

public sealed class BosMarketplaceWriteService : IBosMarketplaceWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosMarketplaceWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>PHP <c>(int)</c> / <c>intval</c> on a token (leading optional sign + digits).</summary>
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

        return long.TryParse(text[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>PHP <c>max(1, min(5, (int)($data['rating'] ?? 5)))</c>.</summary>
    public static int PhpClampRating(long raw)
    {
        if (raw < 1)
        {
            return 1;
        }

        return raw > 5 ? 5 : (int)raw;
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

    /// <summary>PHP <c>json_decode((string)($_POST['review_data'] ?? '{}'), true) ?: array()</c>.</summary>
    public static (int Rating, string Title, string ReviewText, string ReviewerName) ParseReviewData(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (5, "", "", "");
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (5, "", "", "");
            }

            var ratingPresent = doc.RootElement.TryGetProperty("rating", out var ratingEl)
                                && ratingEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
            return (
                ratingPresent ? PhpClampRating(PhpIntval(ratingEl)) : 5,
                JsonString(doc.RootElement, "title"),
                JsonString(doc.RootElement, "review_text"),
                JsonString(doc.RootElement, "reviewer_name"));
        }
        catch (JsonException)
        {
            return (5, "", "", "");
        }
    }

    private static string JsonString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "";
        }

        return el.ValueKind == JsonValueKind.String ? (el.GetString() ?? "") : el.GetRawText();
    }

    public async Task<ErpSimpleWriteResult> UninstallAsync(
        long appId,
        string? siteKey,
        CancellationToken cancellationToken = default)
    {
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
                    UPDATE `epc_marketplace_installs` SET `status`='uninstalled' WHERE `app_id`=? AND `site_key`=?
                    """),
                cancellationToken, appId, PhpBosSiteKey(siteKey)).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Marketplace app uninstalled", appId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Marketplace table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> AddReviewAsync(
        long appId,
        string? siteKey,
        string? reviewData,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        var parsed = ParseReviewData(reviewData);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_marketplace_reviews` (`app_id`,`site_key`,`rating`,`title`,`review_text`,`reviewer_name`) VALUES (?,?,?,?,?,?)
                    """),
                cancellationToken,
                appId,
                PhpBosSiteKey(siteKey),
                parsed.Rating,
                parsed.Title,
                parsed.ReviewText,
                parsed.ReviewerName).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            var avg = await ErpDb.DecimalAsync(
                connection, null,
                ErpDb.Positional("SELECT AVG(`rating`) AS `avg` FROM `epc_marketplace_reviews` WHERE `app_id`=?"),
                cancellationToken, appId).ConfigureAwait(false);
            var count = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT COUNT(*) AS `cnt` FROM `epc_marketplace_reviews` WHERE `app_id`=?"),
                cancellationToken, appId).ConfigureAwait(false);
            var avgRounded = Math.Round(avg, 1, MidpointRounding.AwayFromZero);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_marketplace_apps` SET `avg_rating`=?, `review_count`=? WHERE `id`=?"),
                cancellationToken, avgRounded, count, appId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Marketplace review added", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Marketplace table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> InstallAsync(
        long appId,
        string? siteKey,
        long installedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var version = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `version` FROM `epc_marketplace_apps` WHERE `id`=? AND `status`='published'"),
                cancellationToken, appId).ConfigureAwait(false);
            if (string.IsNullOrEmpty(version))
            {
                return ErpSimpleWriteResult.Fail("invalid", "App not found or not published");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_marketplace_installs` (`app_id`,`site_key`,`installed_version`,`installed_by`) VALUES (?,?,?,?) ON DUPLICATE KEY UPDATE `status`='active', `installed_version`=VALUES(`installed_version`), `updated_at`=NOW()
                    """),
                cancellationToken, appId, PhpBosSiteKey(siteKey), version, installedBy).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_marketplace_apps` SET `downloads`=`downloads`+1 WHERE `id`=?"),
                cancellationToken, appId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Marketplace app installed", appId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Marketplace table is missing — schema-ensure stays Classic.");
        }
    }
}
