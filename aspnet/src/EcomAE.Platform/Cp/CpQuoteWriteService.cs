using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>quote_requests.php</c> twins for admin_note save and send_quote.
/// Line quoting stays PHP.
/// </summary>
public interface ICpQuoteWriteService
{
    Task<ErpSimpleWriteResult> SaveAdminNoteAsync(long quoteId, string? adminNote, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SendQuoteAsync(long quoteId, CancellationToken cancellationToken = default);
}

public sealed class CpQuoteWriteService : ICpQuoteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpQuoteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAdminNoteAsync(
        long quoteId,
        string? adminNote,
        CancellationToken cancellationToken = default)
    {
        if (quoteId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A quote id is required.");
        }

        var note = (adminNote ?? string.Empty).Trim();
        if (note.Length > 4000)
        {
            note = note[..4000];
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_quote_requests` SET `admin_note` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            note, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), quoteId);
        return ErpSimpleWriteResult.Ok("Saved", quoteId);
    }

    public async Task<ErpSimpleWriteResult> SendQuoteAsync(long quoteId, CancellationToken cancellationToken = default)
    {
        if (quoteId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A quote id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var ownerId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `shop_quote_requests` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            quoteId);
        if (ownerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quotes are only for registered customers");
        }

        var lineCount = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_quote_items` WHERE `quote_id` = ?"),
            cancellationToken,
            quoteId);
        if (lineCount < 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Add at least one quote line before publishing");
        }

        var incomplete = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                SELECT COUNT(*) FROM `shop_quote_items` WHERE `quote_id` = ? AND (
                    (`offer_alternative` = 1 AND (`alt_quoted_price` IS NULL OR `alt_quoted_price` <= 0 OR `alt_manufacturer` IS NULL OR `alt_manufacturer` = '' OR `alt_article` IS NULL OR `alt_article` = '' OR `alt_storage_id` IS NULL OR `alt_storage_id` <= 0))
                    OR
                    (`offer_alternative` = 0 AND (`quoted_price` IS NULL OR `quoted_price` <= 0))
                )
                """),
            cancellationToken,
            quoteId);
        if (incomplete > 0)
        {
            return ErpSimpleWriteResult.Fail(
                "invalid",
                "Set a positive price on every line (or complete each alternative: part, warehouse, qty, price)");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_quote_requests` SET `status` = 'quoted', `time_updated` = ? WHERE `id` = ? AND `status` IN ('submitted','quoted')"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(), quoteId);
        return ErpSimpleWriteResult.Ok("Quote sent to customer", quoteId);
    }
}
