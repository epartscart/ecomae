using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_epc_accessories_photos.php</c> upload / delete / set_primary twin.
/// Filename attach writes here. Multipart file bytes and disk unlink stay Classic.
/// Schema-ensure stays PHP. Listing save/status/delete is a separate write.
/// </summary>
public interface ICpAccessoriesPhotoWriteService
{
    Task<ErpSimpleWriteResult> WriteAsync(CpAccessoriesPhotoWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpAccessoriesPhotoWriteRequest(
    string? Action = null,
    long ListingId = 0,
    long PhotoId = 0,
    string? FileName = null,
    bool AsPrimary = false);

public sealed class CpAccessoriesPhotoWriteService : ICpAccessoriesPhotoWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpAccessoriesPhotoWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> WriteAsync(
        CpAccessoriesPhotoWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var action = NormalizeAction(request.Action);
        return action switch
        {
            "upload" => await UploadAsync(request, cancellationToken).ConfigureAwait(false),
            "delete" => await DeleteAsync(request, cancellationToken).ConfigureAwait(false),
            "set_primary" => await SetPrimaryAsync(request, cancellationToken).ConfigureAwait(false),
            _ => ErpSimpleWriteResult.Fail("invalid", "Action must be upload, delete, or set_primary.")
        };
    }

    private async Task<ErpSimpleWriteResult> UploadAsync(
        CpAccessoriesPhotoWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ListingId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Save the listing first, then upload photos.");
        }

        var imageName = SanitizeImageName(request.FileName);
        if (string.IsNullOrWhiteSpace(request.FileName) || imageName.Length == 0)
        {
            return string.IsNullOrWhiteSpace(request.FileName)
                ? ErpSimpleWriteResult.Fail("invalid", "A photo file name is required.")
                : ErpSimpleWriteResult.Fail("invalid", "Image name is not valid.");
        }

        if (!HasAllowedImageExtension(imageName))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Use JPG, PNG, GIF or WEBP");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_acc_listings` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.ListingId).ConfigureAwait(false);
        if (found <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Listing not found");
        }

        var sort = (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(MAX(`sort_order`),0) FROM `epc_acc_photos` WHERE `listing_id` = ?"),
            cancellationToken,
            request.ListingId).ConfigureAwait(false) + 10;
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_acc_photos` WHERE `listing_id` = ?"),
            cancellationToken,
            request.ListingId).ConfigureAwait(false);
        var isPrimary = request.AsPrimary || count == 0 ? 1 : 0;
        if (isPrimary == 1)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_acc_photos` SET `is_primary` = 0 WHERE `listing_id` = ?"),
                cancellationToken,
                request.ListingId).ConfigureAwait(false);
        }

        var createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_acc_photos` (`listing_id`, `file_name`, `sort_order`, `is_primary`, `created_at`)
                VALUES (?, ?, ?, ?, ?)
                """),
            cancellationToken,
            request.ListingId,
            imageName,
            sort,
            isPrimary,
            createdAt).ConfigureAwait(false);
        var photoId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await SyncListingAsync(connection, request.ListingId, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Accessory photo saved.", photoId);
    }

    private async Task<ErpSimpleWriteResult> DeleteAsync(
        CpAccessoriesPhotoWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ListingId <= 0 || request.PhotoId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid photo");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var wasPrimary = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `is_primary` FROM `epc_acc_photos` WHERE `id` = ? AND `listing_id` = ? LIMIT 1"),
            cancellationToken,
            request.PhotoId,
            request.ListingId).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_acc_photos` WHERE `id` = ? AND `listing_id` = ? LIMIT 1"),
            cancellationToken,
            request.PhotoId,
            request.ListingId).ConfigureAwait(false);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Photo not found");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_acc_photos` WHERE `id` = ?"),
            cancellationToken,
            request.PhotoId).ConfigureAwait(false);

        // PHP unlinks the file. Disk unlink stays Classic.
        if (wasPrimary > 0)
        {
            var nextId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT `id` FROM `epc_acc_photos` WHERE `listing_id` = ? ORDER BY `sort_order` ASC, `id` ASC LIMIT 1"),
                cancellationToken,
                request.ListingId).ConfigureAwait(false);
            if (nextId > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `epc_acc_photos` SET `is_primary` = 1 WHERE `id` = ?"),
                    cancellationToken,
                    nextId).ConfigureAwait(false);
            }
        }

        await SyncListingAsync(connection, request.ListingId, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Accessory photo deleted.", request.PhotoId);
    }

    private async Task<ErpSimpleWriteResult> SetPrimaryAsync(
        CpAccessoriesPhotoWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ListingId <= 0 || request.PhotoId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid photo");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_acc_photos` WHERE `id` = ? AND `listing_id` = ? LIMIT 1"),
            cancellationToken,
            request.PhotoId,
            request.ListingId).ConfigureAwait(false);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Photo not found");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_acc_photos` SET `is_primary` = 0 WHERE `listing_id` = ?"),
            cancellationToken,
            request.ListingId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_acc_photos` SET `is_primary` = 1 WHERE `id` = ?"),
            cancellationToken,
            request.PhotoId).ConfigureAwait(false);
        await SyncListingAsync(connection, request.ListingId, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Accessory photo set as primary.", request.PhotoId);
    }

    private static async Task SyncListingAsync(
        System.Data.Common.DbConnection connection,
        long listingId,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_acc_photos` WHERE `listing_id` = ?"),
            cancellationToken,
            listingId).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (count <= 0)
        {
            // PHP leaves image_url and sets photo_count = 1 when the gallery is empty.
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_acc_listings` SET `photo_count` = 1, `updated_at` = ? WHERE `id` = ?"),
                cancellationToken,
                now,
                listingId).ConfigureAwait(false);
            return;
        }

        var fileName = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                SELECT `file_name` FROM `epc_acc_photos`
                WHERE `listing_id` = ?
                ORDER BY `is_primary` DESC, `sort_order` ASC, `id` ASC
                LIMIT 1
                """),
            cancellationToken,
            listingId).ConfigureAwait(false);
        var url = PublicUrl(fileName);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_acc_listings` SET `image_url` = ?, `photo_count` = ?, `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            url,
            count,
            now,
            listingId).ConfigureAwait(false);
    }

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "upload" or "add" or "save" => "upload",
            "delete" or "del" => "delete",
            "set_primary" or "primary" or "set-primary" => "set_primary",
            _ => action
        };
    }

    public static string SanitizeImageName(string? raw)
    {
        var name = Path.GetFileName((raw ?? string.Empty).Trim().Replace('\\', '/'));
        if (name.Contains("..", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return name.Replace("'", "", StringComparison.Ordinal)
            .Replace("\"", "", StringComparison.Ordinal)
            .Replace("`", "", StringComparison.Ordinal)
            .Trim();
    }

    public static bool HasAllowedImageExtension(string? raw)
    {
        var ext = Path.GetExtension(raw ?? string.Empty).TrimStart('.').ToLowerInvariant();
        return ext is "png" or "jpg" or "jpeg" or "gif" or "webp";
    }

    public static string PublicUrl(string? fileName)
    {
        var name = SanitizeImageName(fileName);
        return name.Length == 0 ? string.Empty : "/content/files/images/accessories/" + Uri.EscapeDataString(name);
    }
}
