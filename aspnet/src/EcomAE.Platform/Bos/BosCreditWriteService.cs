using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>credit_limit</c> <c>hold</c> / <c>epc_credit_hold</c>,
/// <c>release</c> / <c>epc_credit_release</c>, and <c>set_limit</c> / <c>epc_credit_set_limit</c>.
/// Check-order and schema-ensure stay Classic.
/// This service does not invent a send. It does not emit CREATE/ALTER.
/// </summary>
public interface IBosCreditWriteService
{
    Task<ErpSimpleWriteResult> HoldAsync(
        string? siteKey,
        long customerId,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> ReleaseAsync(
        string? siteKey,
        long customerId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetLimitAsync(
        string? siteKey,
        long customerId,
        string? creditLimit,
        string? currency,
        string? paymentTerms,
        string? notes,
        string? nextReview,
        long approvedBy,
        CancellationToken cancellationToken = default);
}

public sealed class BosCreditWriteService : IBosCreditWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosCreditWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_]/', '', strtolower($site_key))</c>.</summary>
    public static string NormalizeSiteKey(string? siteKey)
        => SiteKeySafe.Replace((siteKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    /// <summary>PHP <c>date('Y-m-d', strtotime('+90 days'))</c> for omitted <c>next_review</c>.</summary>
    public static string DefaultNextReview(DateTime? now = null)
        => (now ?? DateTime.Now).AddDays(90).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>PHP <c>(float)</c> on a leading numeric token.</summary>
    public static decimal PhpFloat(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return 0;
        }

        var text = raw.TrimStart();
        if (text.Length == 0)
        {
            return 0;
        }

        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        var sawDigit = false;
        while (i < text.Length && char.IsDigit(text[i]))
        {
            sawDigit = true;
            i++;
        }

        if (i < text.Length && text[i] == '.')
        {
            i++;
            while (i < text.Length && char.IsDigit(text[i]))
            {
                sawDigit = true;
                i++;
            }
        }

        if (!sawDigit)
        {
            return 0;
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            var exp = i + 1;
            if (exp < text.Length && text[exp] is '+' or '-')
            {
                exp++;
            }

            var expDigits = exp;
            while (expDigits < text.Length && char.IsDigit(text[expDigits]))
            {
                expDigits++;
            }

            if (expDigits > exp)
            {
                i = expDigits;
            }
        }

        return decimal.TryParse(text[..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    public async Task<ErpSimpleWriteResult> HoldAsync(
        string? siteKey,
        long customerId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeSiteKey(siteKey);
        var holdReason = reason ?? string.Empty;
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
                    UPDATE `epc_credit_limits` SET `status` = 'on_hold', `hold_reason` = ?
                    WHERE `site_key` = ? AND `customer_id` = ?
                    """),
                cancellationToken, holdReason, key, customerId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Credit hold applied", customerId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Credit limits table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> ReleaseAsync(
        string? siteKey,
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeSiteKey(siteKey);
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
                    UPDATE `epc_credit_limits` SET `status` = 'active', `hold_reason` = ''
                    WHERE `site_key` = ? AND `customer_id` = ?
                    """),
                cancellationToken, key, customerId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Credit hold released", customerId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Credit limits table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> SetLimitAsync(
        string? siteKey,
        long customerId,
        string? creditLimit,
        string? currency,
        string? paymentTerms,
        string? notes,
        string? nextReview,
        long approvedBy,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeSiteKey(siteKey);
        if (key.Length == 0 || customerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key or customer_id");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        var limit = PhpFloat(creditLimit);
        var ccy = currency is null ? "AED" : currency;
        var terms = paymentTerms is null ? "net30" : paymentTerms;
        var review = nextReview is null ? DefaultNextReview() : nextReview;
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_credit_limits`
                        (`site_key`, `customer_id`, `credit_limit`, `currency`, `payment_terms`, `approved_by`, `notes`, `last_review`, `next_review`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, CURDATE(), ?)
                    ON DUPLICATE KEY UPDATE
                        `credit_limit` = VALUES(`credit_limit`),
                        `currency` = VALUES(`currency`),
                        `payment_terms` = VALUES(`payment_terms`),
                        `approved_by` = VALUES(`approved_by`),
                        `notes` = VALUES(`notes`),
                        `last_review` = CURDATE(),
                        `next_review` = VALUES(`next_review`)
                    """),
                cancellationToken,
                key,
                customerId,
                limit,
                ccy,
                terms,
                approvedBy,
                notes ?? "",
                review).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Credit limit saved", customerId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Credit limits table is missing — schema-ensure stays Classic.");
        }
    }
}
