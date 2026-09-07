using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>slider.php</c> settings / move / delete / path-add twins. File upload stays Classic.</summary>
public interface ICpSliderWriteService
{
    Task<ErpSimpleWriteResult> SaveSettingsAsync(int connected, int cntImg, int cntImgNext, int timeNextSeconds, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> MoveAsync(long imageId, bool up, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(long imageId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddAsync(string? href, string? link, CancellationToken cancellationToken = default);
}

public sealed class CpSliderWriteService : ICpSliderWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpSliderWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveSettingsAsync(
        int connected,
        int cntImg,
        int cntImgNext,
        int timeNextSeconds,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var visible = cntImg <= 0 ? 1 : cntImg;
        var scroll = cntImgNext <= 0 ? 1 : cntImgNext;
        var timeMs = timeNextSeconds <= 0 ? 1000 : timeNextSeconds * 1000;
        var flag = connected == 1 ? 1 : 0;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `slider_setings` SET `cnt_img` = ?, `cnt_img_next` = ?, `time_next` = ?, `connected` = ?"),
            cancellationToken,
            visible,
            scroll,
            timeMs,
            flag).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Slider settings saved.", 0);
    }

    public async Task<ErpSimpleWriteResult> MoveAsync(long imageId, bool up, CancellationToken cancellationToken = default)
    {
        if (imageId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A slider image id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var order = (int)await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `orders` FROM `slider_images` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                imageId).ConfigureAwait(false);
            if (order <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("not_found", "Slider image was not found.");
            }

            var neighbor = up ? order - 1 : order + 1;
            if (up && order <= 1)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Slider image already first.", imageId);
            }

            if (!up)
            {
                var max = (int)await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT IFNULL(MAX(`orders`),0) FROM `slider_images`"),
                    cancellationToken).ConfigureAwait(false);
                if (order >= max)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Ok("Slider image already last.", imageId);
                }
            }

            var otherId = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `id` FROM `slider_images` WHERE `orders` = ? LIMIT 1"),
                cancellationToken,
                neighbor).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `slider_images` SET `orders` = ? WHERE `id` = ?"),
                cancellationToken,
                neighbor,
                imageId).ConfigureAwait(false);
            if (otherId > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("UPDATE `slider_images` SET `orders` = ? WHERE `id` = ?"),
                    cancellationToken,
                    order,
                    otherId).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(up ? "Slider image moved up." : "Slider image moved down.", imageId);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not move the slider image.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long imageId, CancellationToken cancellationToken = default)
    {
        if (imageId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A slider image id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `slider_images` WHERE `id` = ?"),
                cancellationToken,
                imageId).ConfigureAwait(false);
            if (rows <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("not_found", "Slider image was not found.");
            }

            var remaining = new List<long>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "SELECT `id` FROM `slider_images` ORDER BY `orders` ASC, `id` ASC";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    remaining.Add(reader.GetInt64(0));
                }
            }

            for (var i = 0; i < remaining.Count; i++)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("UPDATE `slider_images` SET `orders` = ? WHERE `id` = ?"),
                    cancellationToken,
                    i + 1,
                    remaining[i]).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Slider image deleted.", imageId);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not delete the slider image.");
        }
    }

    public async Task<ErpSimpleWriteResult> AddAsync(string? href, string? link, CancellationToken cancellationToken = default)
    {
        var path = (href ?? string.Empty).Trim();
        if (path.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An image path is required.");
        }

        if (path.Length > 500)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Image path is too long.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var click = (link ?? string.Empty).Trim();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var next = (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(MAX(`orders`),0) FROM `slider_images`"),
            cancellationToken).ConfigureAwait(false) + 1;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `slider_images` (`href`, `link`, `orders`) VALUES (?, ?, ?)"),
            cancellationToken,
            path,
            click,
            next).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `slider_images` WHERE `href` = ? ORDER BY `id` DESC LIMIT 1"),
            cancellationToken,
            path).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Slider image added.", id);
    }
}
