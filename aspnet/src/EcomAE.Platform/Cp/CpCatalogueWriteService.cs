using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>ajax_operations_products.php</c> min-limit twins. SKU / media stay PHP.</summary>
public interface ICpCatalogueWriteService
{
    Task<ErpSimpleWriteResult> SetMinLimitEnableAsync(long productId, int enabled, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetMinLimitValueAsync(long productId, decimal value, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteCategoryTemplateAsync(long templateId, CancellationToken cancellationToken = default);
}

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
}
