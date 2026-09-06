using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_epc_sku_media.php</c> save_profile / ensure / delete_profile and spec-sheet / photo-meta twins.
/// Photo file upload and photo file unlink stay on the Classic twin. Schema-ensure stays PHP.
/// </summary>
public interface ICpSkuMediaWriteService
{
    Task<ErpSimpleWriteResult> SaveProfileAsync(CpSkuMediaProfileRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> EnsureAsync(CpSkuMediaProfileRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteProfileAsync(long profileId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddSpecGroupAsync(CpSkuMediaSpecGroupRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteSpecGroupAsync(long groupId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddSpecRowAsync(CpSkuMediaSpecRowRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> UpdateSpecRowAsync(CpSkuMediaSpecRowRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteSpecRowAsync(long rowId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> UpdatePhotoAsync(CpSkuMediaPhotoMetaRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeletePhotoAsync(long photoId, CancellationToken cancellationToken = default);
}

public sealed record CpSkuMediaProfileRequest(
    long ProfileId = 0,
    long ProductId = 0,
    string? Brand = null,
    string? Article = null,
    string? Title = null,
    string? Subtitle = null,
    string? Status = null);

public sealed record CpSkuMediaSpecGroupRequest(
    long ProfileId = 0,
    string? Name = null,
    string? Code = null,
    string? Icon = null,
    int? SortOrder = null);

public sealed record CpSkuMediaSpecRowRequest(
    long GroupId = 0,
    long RowId = 0,
    string? Label = null,
    string? Value = null,
    string? ValueType = null,
    string? Unit = null,
    int? SortOrder = null);

public sealed record CpSkuMediaPhotoMetaRequest(
    long PhotoId = 0,
    string? Alt = null,
    string? Caption = null,
    string? PhotoType = null,
    int? SortOrder = null,
    int? IsPrimary = null);

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

    public async Task<ErpSimpleWriteResult> AddSpecGroupAsync(
        CpSkuMediaSpecGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProfileId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing profile");
        }

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Group name required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            code = NormalizeGroupCode(name);
        }

        var icon = (request.Icon ?? string.Empty).Trim();
        if (icon.Length == 0)
        {
            icon = "fa-list";
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var sort = request.SortOrder is > 0
            ? request.SortOrder.Value
            : (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COALESCE(MAX(`sort_order`),0) FROM `epc_sku_spec_groups` WHERE `profile_id` = ?"),
                cancellationToken,
                request.ProfileId).ConfigureAwait(false) + 10;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_sku_spec_groups` (`profile_id`, `name`, `code`, `icon`, `sort_order`, `created_at`) VALUES (?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            request.ProfileId, name, code, icon, sort, now);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await TouchProfileAsync(connection, request.ProfileId, now, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("SKU spec group created.", id);
    }

    public async Task<ErpSimpleWriteResult> DeleteSpecGroupAsync(
        long groupId,
        CancellationToken cancellationToken = default)
    {
        if (groupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A spec group id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var profileId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `profile_id` FROM `epc_sku_spec_groups` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            groupId).ConfigureAwait(false);
        if (profileId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Group not found");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_sku_spec_rows` WHERE `group_id` = ?"),
            cancellationToken,
            groupId);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_sku_spec_groups` WHERE `id` = ?"),
            cancellationToken,
            groupId);
        await TouchProfileAsync(connection, profileId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), cancellationToken)
            .ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("SKU spec group deleted.", groupId);
    }

    public async Task<ErpSimpleWriteResult> AddSpecRowAsync(
        CpSkuMediaSpecRowRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.GroupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A spec group id is required.");
        }

        var label = (request.Label ?? string.Empty).Trim();
        if (label.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Label required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var profileId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `profile_id` FROM `epc_sku_spec_groups` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.GroupId).ConfigureAwait(false);
        if (profileId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Group not found");
        }

        var valueType = NormalizeValueType(request.ValueType, "text");
        var value = request.Value ?? string.Empty;
        if (valueType == "bool")
        {
            value = NormalizeBoolValue(value);
        }

        var unit = (request.Unit ?? string.Empty).Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sort = request.SortOrder is > 0
            ? request.SortOrder.Value
            : (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COALESCE(MAX(`sort_order`),0) FROM `epc_sku_spec_rows` WHERE `group_id` = ?"),
                cancellationToken,
                request.GroupId).ConfigureAwait(false) + 10;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_sku_spec_rows` (`group_id`, `profile_id`, `label`, `value`, `value_type`, `unit`, `sort_order`, `created_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            request.GroupId, profileId, label, value, valueType, unit, sort, now);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await TouchProfileAsync(connection, profileId, now, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("SKU spec row created.", id);
    }

    public async Task<ErpSimpleWriteResult> UpdateSpecRowAsync(
        CpSkuMediaSpecRowRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RowId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A spec row id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var profileId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `profile_id` FROM `epc_sku_spec_rows` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.RowId).ConfigureAwait(false);
        if (profileId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Spec row was not found.");
        }

        var label = request.Label is null
            ? await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `label` FROM `epc_sku_spec_rows` WHERE `id` = ?"),
                cancellationToken,
                request.RowId).ConfigureAwait(false) ?? string.Empty
            : request.Label.Trim();
        var existingType = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `value_type` FROM `epc_sku_spec_rows` WHERE `id` = ?"),
            cancellationToken,
            request.RowId).ConfigureAwait(false) ?? "text";
        var valueType = request.ValueType is null ? existingType : NormalizeValueType(request.ValueType, existingType);
        var value = request.Value ?? await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `value` FROM `epc_sku_spec_rows` WHERE `id` = ?"),
            cancellationToken,
            request.RowId).ConfigureAwait(false) ?? string.Empty;
        if (valueType == "bool")
        {
            value = NormalizeBoolValue(value);
        }

        var unit = request.Unit is null
            ? await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `unit` FROM `epc_sku_spec_rows` WHERE `id` = ?"),
                cancellationToken,
                request.RowId).ConfigureAwait(false) ?? string.Empty
            : request.Unit.Trim();
        var sort = request.SortOrder ?? (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `sort_order` FROM `epc_sku_spec_rows` WHERE `id` = ?"),
            cancellationToken,
            request.RowId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_sku_spec_rows` SET `label` = ?, `value` = ?, `value_type` = ?, `unit` = ?, `sort_order` = ? WHERE `id` = ?"),
            cancellationToken,
            label, value, valueType, unit, sort, request.RowId);
        await TouchProfileAsync(connection, profileId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), cancellationToken)
            .ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("SKU spec row saved.", request.RowId);
    }

    public async Task<ErpSimpleWriteResult> DeleteSpecRowAsync(
        long rowId,
        CancellationToken cancellationToken = default)
    {
        if (rowId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A spec row id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var profileId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `profile_id` FROM `epc_sku_spec_rows` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            rowId).ConfigureAwait(false);
        if (profileId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Spec row was not found.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_sku_spec_rows` WHERE `id` = ?"),
            cancellationToken,
            rowId);
        await TouchProfileAsync(connection, profileId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), cancellationToken)
            .ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("SKU spec row deleted.", rowId);
    }

    public async Task<ErpSimpleWriteResult> UpdatePhotoAsync(
        CpSkuMediaPhotoMetaRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PhotoId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A photo id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var profileId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `profile_id` FROM `epc_sku_photos` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.PhotoId).ConfigureAwait(false);
        if (profileId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Photo was not found.");
        }

        var alt = request.Alt is null
            ? await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `alt` FROM `epc_sku_photos` WHERE `id` = ?"),
                cancellationToken,
                request.PhotoId).ConfigureAwait(false) ?? string.Empty
            : request.Alt.Trim();
        var caption = request.Caption is null
            ? await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `caption` FROM `epc_sku_photos` WHERE `id` = ?"),
                cancellationToken,
                request.PhotoId).ConfigureAwait(false) ?? string.Empty
            : request.Caption.Trim();
        var existingType = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `photo_type` FROM `epc_sku_photos` WHERE `id` = ?"),
            cancellationToken,
            request.PhotoId).ConfigureAwait(false) ?? "product";
        var photoType = request.PhotoType is null ? existingType : NormalizePhotoType(request.PhotoType, existingType);
        var sort = request.SortOrder ?? (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `sort_order` FROM `epc_sku_photos` WHERE `id` = ?"),
            cancellationToken,
            request.PhotoId).ConfigureAwait(false);
        var isPrimary = request.IsPrimary ?? (int)await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `is_primary` FROM `epc_sku_photos` WHERE `id` = ?"),
            cancellationToken,
            request.PhotoId).ConfigureAwait(false);
        isPrimary = isPrimary != 0 ? 1 : 0;
        if (isPrimary == 1)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_sku_photos` SET `is_primary` = 0 WHERE `profile_id` = ?"),
                cancellationToken,
                profileId);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_sku_photos` SET `alt` = ?, `caption` = ?, `photo_type` = ?, `sort_order` = ?, `is_primary` = ? WHERE `id` = ?"),
            cancellationToken,
            alt, caption, photoType, sort, isPrimary, request.PhotoId);
        await TouchProfileAsync(connection, profileId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), cancellationToken)
            .ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("SKU photo saved.", request.PhotoId);
    }

    public async Task<ErpSimpleWriteResult> DeletePhotoAsync(
        long photoId,
        CancellationToken cancellationToken = default)
    {
        if (photoId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A photo id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var profileId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `profile_id` FROM `epc_sku_photos` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            photoId).ConfigureAwait(false);
        if (profileId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Photo was not found.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_sku_photos` WHERE `id` = ?"),
            cancellationToken,
            photoId);
        await TouchProfileAsync(connection, profileId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), cancellationToken)
            .ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("SKU photo deleted.", photoId);
    }

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "add_spec_group" or "create_spec_group" => "add_spec_group",
            "delete_spec_group" => "delete_spec_group",
            "add_spec_row" or "create_spec_row" => "add_spec_row",
            "update_spec_row" or "save_spec_row" => "update_spec_row",
            "delete_spec_row" => "delete_spec_row",
            "update_photo" or "save_photo" => "update_photo",
            "delete_photo" => "delete_photo",
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

    public static string NormalizeGroupCode(string? raw)
    {
        var code = Regex.Replace((raw ?? string.Empty).Trim(), "[^a-zA-Z0-9]+", "_").Trim('_').ToLowerInvariant();
        return code.Length == 0 ? "custom" : code;
    }

    public static string NormalizeValueType(string? raw, string fallback = "text")
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return value is "text" or "number" or "bool" or "list" or "rich" ? value : fallback;
    }

    public static string NormalizePhotoType(string? raw, string fallback = "product")
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return value is "product" or "packaging" or "detail" or "diagram" or "install" or "datasheet" or "other"
            ? value
            : fallback;
    }

    public static string NormalizeBoolValue(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0 || value == "0" || value.Equals("no", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return "0";
        }

        return "1";
    }

    private static async Task TouchProfileAsync(
        System.Data.Common.DbConnection connection,
        long profileId,
        long now,
        CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_sku_profiles` SET `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            now, profileId).ConfigureAwait(false);
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
