using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_epc_sku_media.php</c> save_profile / ensure / delete_profile twins.
/// Photo upload, photo file unlink, and spec-sheet writes stay on the Classic twin.
/// Schema-ensure stays PHP.
/// </summary>
public interface ICpSkuMediaWriteService
{
    Task<ErpSimpleWriteResult> SaveProfileAsync(CpSkuMediaProfileRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> EnsureAsync(CpSkuMediaProfileRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteProfileAsync(long profileId, CancellationToken cancellationToken = default);
}

public sealed record CpSkuMediaProfileRequest(
    long ProfileId = 0,
    long ProductId = 0,
    string? Brand = null,
    string? Article = null,
    string? Title = null,
    string? Subtitle = null,
    string? Status = null);

public sealed class CpSkuMediaWriteService : ICpSkuMediaWriteService
{
    private static readonly Regex ArticleKeyChars = new("[^A-Z0-9]+", RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpSkuMediaWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveProfileAsync(
        CpSkuMediaProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var brand = NormalizeBrand(request.Brand);
        var article = (request.Article ?? string.Empty).Trim();
        var key = NormalizeArticle(article.Length > 0 ? article : string.Empty);
        if (request.ProfileId <= 0 && request.ProductId <= 0 && (brand.Length == 0 || key.Length == 0))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Brand and article (or catalogue product) required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var title = (request.Title ?? string.Empty).Trim();
        var subtitle = (request.Subtitle ?? string.Empty).Trim();
        var status = NormalizeStatus(request.Status);
        var articleShow = article.Length > 0 ? article : key;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        object? productId = request.ProductId > 0 ? request.ProductId : null;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var id = request.ProfileId;
        if (id <= 0)
        {
            id = await FindProfileIdAsync(connection, 0, request.ProductId, brand, articleShow, cancellationToken)
                .ConfigureAwait(false);
        }

        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_sku_profiles` SET `product_id` = ?, `brand` = ?, `article` = ?, `article_key` = ?, `title` = ?, `subtitle` = ?, `status` = ?, `updated_at` = ? WHERE `id` = ?"),
                cancellationToken,
                productId, brand, articleShow, key, title, subtitle, status, now, id);
            return ErpSimpleWriteResult.Ok("SKU profile saved.", id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_sku_profiles` (`product_id`, `brand`, `article`, `article_key`, `title`, `subtitle`, `status`, `created_at`, `updated_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            productId, brand, articleShow, key, title, subtitle, status, now, now);
        id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("SKU profile created.", id);
    }

    public async Task<ErpSimpleWriteResult> EnsureAsync(
        CpSkuMediaProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var brand = NormalizeBrand(request.Brand);
        var article = (request.Article ?? string.Empty).Trim();
        var key = NormalizeArticle(article);
        if (request.ProductId <= 0 && (brand.Length == 0 || key.Length == 0))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Brand and article (or catalogue product) required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var articleShow = article.Length > 0 ? article : key;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var existing = await FindProfileIdAsync(connection, 0, request.ProductId, brand, articleShow, cancellationToken)
            .ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Ok("SKU profile already exists.", existing);
        }

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0 && brand.Length > 0 && key.Length > 0)
        {
            title = brand + " " + articleShow;
        }

        return await SaveProfileAsync(
            request with { Title = title, Status = "active" },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> DeleteProfileAsync(
        long profileId,
        CancellationToken cancellationToken = default)
    {
        if (profileId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A SKU profile id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_sku_spec_rows` WHERE `profile_id` = ?"),
                cancellationToken,
                profileId);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_sku_spec_groups` WHERE `profile_id` = ?"),
                cancellationToken,
                profileId);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_sku_photos` WHERE `profile_id` = ?"),
                cancellationToken,
                profileId);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_sku_profiles` WHERE `id` = ?"),
                cancellationToken,
                profileId);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return ErpSimpleWriteResult.Ok("SKU profile deleted.", profileId);
    }

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "save_profile" or "save" or "create" or "edit" or "update" => "save_profile",
            "ensure" or "ensure_profile" => "ensure",
            "delete_profile" or "delete" or "del" => "delete_profile",
            _ => action
        };
    }

    public static string NormalizeBrand(string? raw)
    {
        var brand = Regex.Replace((raw ?? string.Empty).Trim(), @"\s+", " ");
        return brand.Length == 0 ? string.Empty : brand.ToUpperInvariant();
    }

    public static string NormalizeArticle(string? raw)
    {
        var article = (raw ?? string.Empty).Trim().ToUpperInvariant();
        return ArticleKeyChars.Replace(article, string.Empty);
    }

    public static string NormalizeStatus(string? raw)
    {
        var status = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return status.Length == 0 ? "active" : status;
    }

    private static async Task<long> FindProfileIdAsync(
        System.Data.Common.DbConnection connection,
        long profileId,
        long productId,
        string brand,
        string article,
        CancellationToken cancellationToken)
    {
        if (profileId > 0)
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_sku_profiles` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                profileId).ConfigureAwait(false);
        }

        if (productId > 0)
        {
            var byProduct = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_sku_profiles` WHERE `product_id` = ? ORDER BY `id` DESC LIMIT 1"),
                cancellationToken,
                productId).ConfigureAwait(false);
            if (byProduct > 0)
            {
                return byProduct;
            }
        }

        var key = NormalizeArticle(article);
        if (brand.Length > 0 && key.Length > 0)
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT `id` FROM `epc_sku_profiles` WHERE UPPER(`brand`) = ? AND `article_key` = ? ORDER BY `id` DESC LIMIT 1"),
                cancellationToken,
                brand, key).ConfigureAwait(false);
        }

        if (key.Length > 0)
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_sku_profiles` WHERE `article_key` = ? ORDER BY `id` DESC LIMIT 1"),
                cancellationToken,
                key).ConfigureAwait(false);
        }

        return 0;
    }
}
