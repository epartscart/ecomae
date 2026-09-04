using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>content_manager.php</c> twins for single-id <c>set_published_flag</c>
/// and <c>set_main_flag</c>. Body HTML editor and system-content config stay PHP.
/// Always refuse <c>system_flag=1</c> (do not invent <c>DP_Config-&gt;allow_edit_system_content</c>).
/// </summary>
public interface ICpContentManagerWriteService
{
    Task<ErpSimpleWriteResult> SetPublishedAsync(long contentId, int publishedFlag, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetMainAsync(long contentId, int isFrontend, CancellationToken cancellationToken = default);
}

public sealed class CpContentManagerWriteService : ICpContentManagerWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpContentManagerWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetPublishedAsync(
        long contentId,
        int publishedFlag,
        CancellationToken cancellationToken = default)
    {
        if (contentId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A content page id is required.");
        }

        var flag = publishedFlag > 0 ? 1 : 0;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId);
        if (found <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Content page was not found.");
        }

        var systemFlag = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `system_flag` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId);
        if (systemFlag > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "System pages cannot change publish state.");
        }

        if (flag == 0)
        {
            var mainFlag = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `main_flag` FROM `content` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                contentId);
            if (mainFlag > 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "The main page cannot be unpublished.");
            }
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `content` SET `published_flag` = ? WHERE `id` = ?"),
            cancellationToken,
            flag, contentId);
        return ErpSimpleWriteResult.Ok("Publish flag updated.", contentId);
    }

    public async Task<ErpSimpleWriteResult> SetMainAsync(
        long contentId,
        int isFrontend,
        CancellationToken cancellationToken = default)
    {
        if (contentId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A content page id is required.");
        }

        var frontend = isFrontend > 0 ? 1 : 0;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var found = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `id` FROM `content` WHERE `id` = ? AND `is_frontend` = ? LIMIT 1"),
            cancellationToken,
            contentId, frontend);
        if (found <= 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Content page was not found.");
        }

        var level = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `level` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId);
        if (level > 1)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Nested pages cannot be set as the main page.");
        }

        var published = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `published_flag` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId);
        if (published == 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Unpublished pages cannot be set as the main page.");
        }

        var alreadyMain = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `main_flag` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId);
        if (alreadyMain > 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "This page is already the main page.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `content` SET `main_flag` = 1 WHERE `id` = ?"),
            cancellationToken,
            contentId);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `content` SET `main_flag` = 0 WHERE `id` != ? AND `is_frontend` = ? AND `main_flag` = 1"),
            cancellationToken,
            contentId, frontend);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Main page updated.", contentId);
    }
}
