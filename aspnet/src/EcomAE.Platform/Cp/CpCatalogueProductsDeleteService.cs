using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>products.php</c> <c>delete_products</c> twin of
/// <c>delete_products_sub.php</c>. Image file unlink stays commented-out in PHP
/// and stays Classic. <c>OPTIMIZE TABLE</c> is skipped (MySQL implicit commit).
/// </summary>
public interface ICpCatalogueProductsDeleteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(
        CpCatalogueProductsDeleteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpCatalogueProductsDeleteRequest(
    long CategoryId = 0,
    string? ProductsJson = null);

public sealed class CpCatalogueProductsDeleteService : ICpCatalogueProductsDeleteService
{
    public const int MaxBatch = 5000;

    private static readonly string[] ChildTables =
    [
        "shop_products_images",
        "shop_products_text",
        "shop_properties_values_int",
        "shop_properties_values_float",
        "shop_properties_values_text",
        "shop_properties_values_bool",
        "shop_properties_values_list",
        "shop_properties_values_tree_list",
        "shop_main_page_products",
        "shop_products_stickers",
        "shop_products_evaluations",
        "shop_storages_data"
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public CpCatalogueProductsDeleteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeAction(string? action)
    {
        var key = (action ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "delete" or "delete_products" => "delete",
            _ => key
        };
    }

    public static (IReadOnlyList<long> Ids, string? Error) ParseIds(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], null);
        }

        if (text.Length > 20_000)
        {
            return ([], "products_list is too large.");
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "products_list must be a JSON array.");
            }

            var ids = new List<long>();
            foreach (var node in document.RootElement.EnumerateArray())
            {
                var id = node.ValueKind switch
                {
                    JsonValueKind.Number when node.TryGetInt64(out var n) => n,
                    JsonValueKind.String when long.TryParse(
                        node.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                        => parsed,
                    _ => 0
                };
                if (id > 0 && !ids.Contains(id))
                {
                    ids.Add(id);
                }
            }

            return (ids, null);
        }
        catch (JsonException)
        {
            return ([], "products_list is not valid JSON.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        CpCatalogueProductsDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var parsed = ParseIds(request.ProductsJson);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (parsed.Ids.Count == 0 && request.CategoryId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Provide products_list ids or a positive category_id.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<long> productIds = parsed.Ids;
            if (productIds.Count == 0)
            {
                productIds = await LoadCategoryProductIdsAsync(
                    connection, transaction, request.CategoryId, cancellationToken).ConfigureAwait(false);
            }

            if (productIds.Count == 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "No products to delete.");
            }

            if (productIds.Count > MaxBatch)
            {
                productIds = productIds.Take(MaxBatch).ToArray();
            }

            foreach (var table in ChildTables)
            {
                await TryDeleteByProductIdAsync(
                    connection, transaction, table, "product_id", productIds, cancellationToken)
                    .ConfigureAwait(false);
            }

            await TryDeleteByProductIdAsync(
                connection, transaction, "shop_related_products", "product_id", productIds, cancellationToken)
                .ConfigureAwait(false);
            await TryDeleteByProductIdAsync(
                connection, transaction, "shop_related_products", "product_id_related", productIds, cancellationToken)
                .ConfigureAwait(false);

            var deleted = 0;
            foreach (var id in productIds)
            {
                deleted += await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `shop_catalogue_products` WHERE `id` = ?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
            }

            if (deleted == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("not_found", "No products to delete.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(
                deleted == 1 ? "Product deleted." : "Products deleted.",
                productIds[0]);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<IReadOnlyList<long>> LoadCategoryProductIdsAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        long categoryId,
        CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(
            "SELECT `id` FROM `shop_catalogue_products` WHERE `category_id` = ? LIMIT "
            + MaxBatch.ToString(CultureInfo.InvariantCulture));
        ErpDb.AddParameters(command, categoryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(reader.GetInt64(0));
        }

        return ids;
    }

    private static async Task TryDeleteByProductIdAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string table,
        string column,
        IReadOnlyList<long> productIds,
        CancellationToken cancellationToken)
    {
        foreach (var id in productIds)
        {
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `" + table + "` WHERE `" + column + "` = ?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
            }
            catch (System.Data.Common.DbException)
            {
                return;
            }
        }
    }
}
