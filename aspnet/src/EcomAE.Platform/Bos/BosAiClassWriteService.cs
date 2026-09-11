using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>ai_classification</c> <c>review</c> / <c>epc_ai_review</c>.
/// Classify-and-store, batch, seed_hs, and schema-ensure stay Classic.
/// This service does not invent a send. It does not emit CREATE/ALTER.
/// PHP always returns <c>{ok:true}</c> — no invented not-found when <c>classification_id</c> is 0.
/// </summary>
public interface IBosAiClassWriteService
{
    Task<ErpSimpleWriteResult> ReviewAsync(
        long classificationId,
        string? category,
        string? subcategory,
        string? hsCode,
        long reviewerId,
        CancellationToken cancellationToken = default);
}

public sealed class BosAiClassWriteService : IBosAiClassWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosAiClassWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>(int)</c> on a leading optional sign + digits token.</summary>
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

    public async Task<ErpSimpleWriteResult> ReviewAsync(
        long classificationId,
        string? category,
        string? subcategory,
        string? hsCode,
        long reviewerId,
        CancellationToken cancellationToken = default)
    {
        var cat = category ?? string.Empty;
        var sub = subcategory ?? string.Empty;
        var hs = hsCode ?? string.Empty;

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
                    UPDATE `epc_ai_classifications`
                    SET `category` = ?, `subcategory` = ?, `hs_code` = ?, `reviewed` = 1, `reviewed_by` = ?, `method` = 'manual'
                    WHERE `id` = ?
                    """),
                cancellationToken, cat, sub, hs, reviewerId, classificationId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("AI classification reviewed", classificationId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "AI classifications table is missing — schema-ensure stays Classic.");
        }
    }
}
