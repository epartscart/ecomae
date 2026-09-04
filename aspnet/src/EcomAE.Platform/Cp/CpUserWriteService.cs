using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>ajax_set_user_comment.php</c> twin.</summary>
public interface ICpUserWriteService
{
    Task<ErpSimpleWriteResult> SetCommentAsync(long userId, string? comment, CancellationToken cancellationToken = default);
}

public sealed class CpUserWriteService : ICpUserWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpUserWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetCommentAsync(
        long userId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A user id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var text = (comment ?? string.Empty).Trim();
        if (text.Length > 4000)
        {
            text = text[..4000];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `users` SET `comment` = ? WHERE `user_id` = ?"),
            cancellationToken,
            text, userId);
        return ErpSimpleWriteResult.Ok("Staff comment saved.", userId);
    }
}
