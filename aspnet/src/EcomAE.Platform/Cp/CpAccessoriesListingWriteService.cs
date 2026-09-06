using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>accessories_listings.php</c> save / set_status / delete twin.
/// Multipart listing photos and taxonomy writes stay Classic.
/// Schema-ensure stays PHP.
/// </summary>
public interface ICpAccessoriesListingWriteService
{
    Task<ErpSimpleWriteResult> WriteAsync(CpAccessoriesListingWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpAccessoriesListingWriteRequest(
    string? Action = null,
    long ListingId = 0,
    long CategoryId = 0,
    long SubcategoryId = 0,
    string? Title = null,
    string? Description = null,
    string? Make = null,
    string? Model = null,
    string? Year = null,
    string? City = null,
    string? ConditionType = null,
    decimal Price = 0,
    decimal ComparePrice = 0,
    string? Currency = null,
    string? ImageUrl = null,
    string? ExternalUrl = null,
    int PhotoCount = 1,
    bool Featured = false,
    int StockQty = 0,
    string? Status = null);

public sealed class CpAccessoriesListingWriteService : ICpAccessoriesListingWriteService
{
    private static readonly Regex AccessoriesBrowse = new(
        @"accessories(-spare-parts)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ListingIdQuery = new(
        @"[?&]id=\d+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpAccessoriesListingWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> WriteAsync(
        CpAccessoriesListingWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var action = NormalizeAction(request.Action);
        return action switch
        {
            "save" => await SaveAsync(request, cancellationToken).ConfigureAwait(false),
            "set_status" => await SetStatusAsync(request, cancellationToken).ConfigureAwait(false),
            "delete" => await DeleteAsync(request, cancellationToken).ConfigureAwait(false),
            _ => ErpSimpleWriteResult.Fail("invalid", "Action must be save, set_status, or delete.")
        };
    }

    private async Task<ErpSimpleWriteResult> SaveAsync(
        CpAccessoriesListingWriteRequest request,
        CancellationToken cancellationToken)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0 || request.CategoryId < 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Title and category are required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "AED" : request.Currency.Trim();
        var condition = string.IsNullOrWhiteSpace(request.ConditionType) ? "new" : request.ConditionType.Trim();
        var status = string.IsNullOrWhiteSpace(request.Status) ? "published" : request.Status.Trim();
        var photoCount = request.PhotoCount < 1 ? 1 : request.PhotoCount;
        var featured = request.Featured ? 1 : 0;
        var externalUrl = SanitizeExternalUrl(request.ExternalUrl);
        var creating = request.ListingId <= 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!creating)
        {
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

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_acc_listings` SET
                        `category_id` = ?, `subcategory_id` = ?, `title` = ?, `description` = ?,
                        `make` = ?, `model` = ?, `year` = ?, `city` = ?, `condition_type` = ?,
                        `price` = ?, `compare_price` = ?, `currency` = ?, `image_url` = ?, `external_url` = ?,
                        `photo_count` = ?, `featured` = ?, `stock_qty` = ?, `status` = ?, `updated_at` = ?
                    WHERE `id` = ?
                    """),
                cancellationToken,
                request.CategoryId,
                request.SubcategoryId < 0 ? 0 : request.SubcategoryId,
                title,
                (request.Description ?? string.Empty).Trim(),
                (request.Make ?? string.Empty).Trim(),
                (request.Model ?? string.Empty).Trim(),
                (request.Year ?? string.Empty).Trim(),
                (request.City ?? string.Empty).Trim(),
                condition,
                request.Price,
                request.ComparePrice,
                currency,
                (request.ImageUrl ?? string.Empty).Trim(),
                externalUrl,
                photoCount,
                featured,
                request.StockQty,
                status,
                now,
                request.ListingId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Listing updated.", request.ListingId);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_acc_listings`
                (`category_id`, `subcategory_id`, `title`, `description`, `make`, `model`, `year`, `city`, `condition_type`,
                 `price`, `compare_price`, `currency`, `image_url`, `external_url`, `photo_count`, `featured`,
                 `stock_qty`, `status`, `created_at`, `updated_at`)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """),
            cancellationToken,
            request.CategoryId,
            request.SubcategoryId < 0 ? 0 : request.SubcategoryId,
            title,
            (request.Description ?? string.Empty).Trim(),
            (request.Make ?? string.Empty).Trim(),
            (request.Model ?? string.Empty).Trim(),
            (request.Year ?? string.Empty).Trim(),
            (request.City ?? string.Empty).Trim(),
            condition,
            request.Price,
            request.ComparePrice,
            currency,
            (request.ImageUrl ?? string.Empty).Trim(),
            externalUrl,
            photoCount,
            featured,
            request.StockQty,
            status,
            now,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Listing created.", id);
    }

    private async Task<ErpSimpleWriteResult> SetStatusAsync(
        CpAccessoriesListingWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ListingId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A listing id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var status = string.IsNullOrWhiteSpace(request.Status) ? "draft" : request.Status.Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_acc_listings` SET `status` = ?, `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            status,
            now,
            request.ListingId).ConfigureAwait(false);
        return rows > 0
            ? ErpSimpleWriteResult.Ok("Status → " + status, request.ListingId)
            : ErpSimpleWriteResult.Fail("not_found", "Listing not found");
    }

    private async Task<ErpSimpleWriteResult> DeleteAsync(
        CpAccessoriesListingWriteRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ListingId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A listing id is required.");
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

        // PHP unlinks photo files. Disk unlink stays Classic.
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_acc_photos` WHERE `listing_id` = ?"),
            cancellationToken,
            request.ListingId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_acc_listings` WHERE `id` = ?"),
            cancellationToken,
            request.ListingId).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Listing deleted.", request.ListingId);
    }

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "save" or "create" or "edit" or "update" => "save",
            "set_status" or "status" or "set-status" => "set_status",
            "delete" or "del" => "delete",
            _ => action
        };
    }

    /// <summary>
    /// PHP <c>epc_acc_cp_payload_from_post</c> drops accessories browse URLs that are not a listing detail.
    /// </summary>
    public static string SanitizeExternalUrl(string? raw)
    {
        var url = (raw ?? string.Empty).Trim();
        if (url.Length == 0)
        {
            return string.Empty;
        }

        if (AccessoriesBrowse.IsMatch(url) && !ListingIdQuery.IsMatch(url))
        {
            return string.Empty;
        }

        return url;
    }
}
