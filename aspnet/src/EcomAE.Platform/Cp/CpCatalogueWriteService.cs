using System.Net;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_operations_products.php</c> min-limit and <c>ajax_templates_actions.php</c> create/delete twins.
/// File image upload and from-category disk copy stay on the Classic twin. SKU / media stay PHP.
/// </summary>
public interface ICpCatalogueWriteService
{
    Task<ErpSimpleWriteResult> SetMinLimitEnableAsync(long productId, int enabled, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetMinLimitValueAsync(long productId, decimal value, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateCategoryTemplateAsync(CpCategoryTemplateCreateRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteCategoryTemplateAsync(long templateId, CancellationToken cancellationToken = default);
}

public sealed record CpCategoryTemplateCreateRequest(
    string? Caption = null,
    string? CategoryObject = null,
    string? ImageBase64 = null,
    string? ImageName = null,
    string? ImageType = null);

public sealed class CpCatalogueWriteService : ICpCatalogueWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpCatalogueWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetMinLimitEnableAsync(
        long productId,
        int enabled,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0 || enabled is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A product id and min-limit flag of 0 or 1 are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_catalogue_products` SET `min_limit_enable` = ? WHERE `id` = ?"),
            cancellationToken,
            enabled, productId);
        return ErpSimpleWriteResult.Ok("Product min-limit flag saved.", productId);
    }

    public async Task<ErpSimpleWriteResult> SetMinLimitValueAsync(
        long productId,
        decimal value,
        CancellationToken cancellationToken = default)
    {
        if (productId <= 0 || value < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A product id and a non-negative min-limit value are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_catalogue_products` SET `min_limit` = ? WHERE `id` = ?"),
            cancellationToken,
            value, productId);
        return ErpSimpleWriteResult.Ok("Product min-limit value saved.", productId);
    }

    public async Task<ErpSimpleWriteResult> CreateCategoryTemplateAsync(
        CpCategoryTemplateCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var caption = HtmlEncode(request.Caption);
        if (string.IsNullOrWhiteSpace(caption))
        {
            return ErpSimpleWriteResult.Fail("invalid", "A category template caption is required.");
        }

        var parsed = TryParseCategoryObject(request.CategoryObject);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        var image = TryDecodeImage(request.ImageBase64);
        if (image.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", image.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var imageName = HtmlEncode(request.ImageName);
        if (image.Bytes is not null && string.IsNullOrWhiteSpace(imageName))
        {
            imageName = "image.bin";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_catalogue_categories_templates` (`caption`, `category_object`, `image`, `image_name`) VALUES (?, ?, ?, ?)"),
            cancellationToken,
            caption,
            parsed.Json,
            image.Bytes,
            string.IsNullOrWhiteSpace(imageName) ? null : imageName);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Category template created.", id);
    }

    public async Task<ErpSimpleWriteResult> DeleteCategoryTemplateAsync(
        long templateId,
        CancellationToken cancellationToken = default)
    {
        if (templateId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A category template id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_catalogue_categories_templates` WHERE `id` = ?"),
            cancellationToken,
            templateId);
        return ErpSimpleWriteResult.Ok("Category template deleted.", templateId);
    }

    public static string HtmlEncode(string? raw)
        => WebUtility.HtmlEncode((raw ?? string.Empty).Trim());

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "create" or "add" or "save_create" or "save_action_create" => "create",
            "delete" or "del" or "remove" => "delete",
            _ => action
        };
    }

    public static (string? Json, string? Error) TryParseCategoryObject(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return (null, "A category_object JSON object is required.");
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (null, "category_object must be a JSON object.");
            }

            return (text, null);
        }
        catch (JsonException)
        {
            return (null, "category_object is not valid JSON.");
        }
    }

    public static (byte[]? Bytes, string? Error) TryDecodeImage(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return (null, null);
        }

        var comma = text.IndexOf(',');
        if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
        {
            text = text[(comma + 1)..];
        }

        try
        {
            return (Convert.FromBase64String(text), null);
        }
        catch (FormatException)
        {
            return (null, "image must be base64.");
        }
    }
}
