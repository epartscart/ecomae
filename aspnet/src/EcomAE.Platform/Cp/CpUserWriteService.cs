using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>ajax_set_user_comment.php</c> twin.</summary>
public interface ICpUserWriteService
{
    Task<ErpSimpleWriteResult> SetCommentAsync(long userId, string? comment, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetVinViewedAsync(IReadOnlyList<long> requestIds, int viewedFlag, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetUnlockedAsync(long userId, int unlockedFlag, long actorUserId, CancellationToken cancellationToken = default);
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

    public async Task<ErpSimpleWriteResult> SetVinViewedAsync(
        IReadOnlyList<long> requestIds,
        int viewedFlag,
        CancellationToken cancellationToken = default)
    {
        var ids = (requestIds ?? []).Where(id => id > 0).Distinct().Take(80).ToArray();
        if (ids.Length == 0 || viewedFlag is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select VIN requests and a viewed flag of 0 or 1.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var placeholders = string.Join(",", ids.Select((_, i) => "?"));
        var args = new object?[ids.Length + 1];
        args[0] = viewedFlag;
        for (var i = 0; i < ids.Length; i++)
        {
            args[i + 1] = ids[i];
        }

        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `users_vin` SET `viewed` = ? WHERE `id` IN (" + placeholders + ")"),
            cancellationToken,
            args);
        return new ErpSimpleWriteResult(true, "ok", "VIN viewed flag updated.", ids[0], Math.Max(writes, 1));
    }

    public async Task<ErpSimpleWriteResult> SetUnlockedAsync(
        long userId,
        int unlockedFlag,
        long actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || unlockedFlag is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A user id and unlocked flag of 0 or 1 are required.");
        }

        if (actorUserId > 0 && actorUserId == userId)
        {
            return ErpSimpleWriteResult.Fail("self", "You cannot lock or unlock your own account.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("UPDATE `users` SET `unlocked` = ? WHERE `user_id` = ?"),
            cancellationToken,
            unlockedFlag, userId);

        if (unlockedFlag == 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `sessions` WHERE `user_id` = ?"),
                cancellationToken,
                userId);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(unlockedFlag == 1 ? "User unlocked." : "User locked.", userId);
    }
}
