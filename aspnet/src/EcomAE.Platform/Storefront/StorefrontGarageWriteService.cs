using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>Live PHP <c>ajax_operations_cars.php</c> (active/delete) and <c>ajax_add_to_notepad.php</c>.</summary>
public interface IStorefrontGarageWriteService
{
    Task<ErpSimpleWriteResult> SetActiveAsync(int userId, long carId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(int userId, long carId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddNotepadAsync(
        int userId,
        long garageId,
        string? manufacturer,
        string? article,
        string? name,
        int exist,
        decimal price,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontGarageWriteService : IStorefrontGarageWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public StorefrontGarageWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetActiveAsync(int userId, long carId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        if (carId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Car is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Garage database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var owned = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_docpart_garage` WHERE `id` = ? AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            carId, userId);
        if (owned <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Car is not in your garage.");
        }

        var current = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            userId);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_garage` SET `active` = 0 WHERE `user_id` = ?"),
            cancellationToken,
            userId);
        if (current != carId)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_docpart_garage` SET `active` = 1 WHERE `id` = ? AND `user_id` = ?"),
                cancellationToken,
                carId, userId);
        }

        return ErpSimpleWriteResult.Ok("Active car updated.", carId);
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(int userId, long carId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        if (carId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Car is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Garage database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_docpart_garage` WHERE `id` = ? AND `user_id` = ?"),
            cancellationToken,
            carId, userId);
        if (rows <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Car is not in your garage.");
        }

        return ErpSimpleWriteResult.Ok("Car removed from garage.", carId);
    }

    public async Task<ErpSimpleWriteResult> AddNotepadAsync(
        int userId,
        long garageId,
        string? manufacturer,
        string? article,
        string? name,
        int exist,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return ErpSimpleWriteResult.Fail("auth", "Please log in or register to continue.");
        }

        var part = (article ?? string.Empty).Trim();
        if (part.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Article is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Garage database is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (garageId > 0)
        {
            var owned = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `shop_docpart_garage` WHERE `id` = ? AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                garageId, userId);
            if (owned <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Car is not in your garage.");
            }
        }

        var brand = (manufacturer ?? string.Empty).Trim();
        var caption = (name ?? string.Empty).Trim();
        var comment = "Added " + DateTime.UtcNow.ToString("dd-MM-yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `shop_docpart_garage_notepad`
                (`user_id`, `garage_id`, `brend`, `article`, `name`, `exist`, `price`, `comment`)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """),
            cancellationToken,
            userId, garageId, brand, part, caption, exist, price, comment);
        return ErpSimpleWriteResult.Ok("Notepad line added.", garageId);
    }
}
