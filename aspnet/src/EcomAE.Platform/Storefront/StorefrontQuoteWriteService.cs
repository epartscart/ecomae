using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>Live PHP <c>ajax_quote_submit.php</c> (draft → submitted).</summary>
public interface IStorefrontQuoteWriteService
{
    Task<ErpSimpleWriteResult> SubmitAsync(int userId, long quoteId, string? customerNote, CancellationToken cancellationToken = default);
}

public sealed class StorefrontQuoteWriteService : IStorefrontQuoteWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public StorefrontQuoteWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SubmitAsync(
        int userId,
        long quoteId,
        string? customerNote,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Quote database is not configured.");
        }

        if (quoteId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quote is required.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `shop_quote_requests` WHERE `id` = ? AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            quoteId, userId);
        if (string.IsNullOrWhiteSpace(status))
        {
            return ErpSimpleWriteResult.Fail("not_found", "Quote not found or already submitted.");
        }

        if (!string.Equals(status, "draft", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("quote_not_draft", "Quote not found or already submitted.");
        }

        var lines = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_quote_items` WHERE `quote_id` = ?"),
            cancellationToken,
            quoteId);
        if (lines < 1)
        {
            return ErpSimpleWriteResult.Fail("empty", "Add at least one line before submitting.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_quote_requests` SET `status` = 'submitted', `time_submitted` = ?, `time_updated` = ?, `customer_note` = ? WHERE `id` = ? AND `user_id` = ?"),
            cancellationToken,
            now, now, customerNote ?? string.Empty, quoteId, userId);
        if (rows <= 0)
        {
            return ErpSimpleWriteResult.Fail("update_failed", "Could not submit quote.");
        }

        return ErpSimpleWriteResult.Ok("Quote submitted.", quoteId);
    }
}
