using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpJwStockAvailabilityReadService
{
    Task<ErpJwStockAvailabilityResult> ReadAsync(
        int companyId,
        string? search,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwStockAvailabilityRow(
    long Id,
    int CompanyId,
    string TagNo,
    string Barcode,
    string ItemType,
    string Karat,
    decimal GrossWeight,
    decimal NetWeight,
    decimal SellPrice,
    string Location,
    string Status);

public sealed record ErpJwStockAvailabilityResult(
    IReadOnlyList<ErpJwStockAvailabilityRow> Rows,
    string Source,
    string Message);

public sealed class ErpJwStockAvailabilityReadService
    : IErpJwStockAvailabilityReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwStockAvailabilityReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpJwStockAvailabilityResult> ReadAsync(
        int companyId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return Empty("TenantRegistry DB is not configured.", "migration");
        }

        var scope = Math.Max(0, companyId);
        var term = (search ?? string.Empty).Trim();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("""
            SELECT `id`,`company_id`,`tag_no`,`barcode`,`item_type`,`karat`,
                   `gross_weight`,`net_weight`,`sell_price`,`location`,`status`
            FROM `epc_jw_tags`
            WHERE `status`='in_stock'
              AND (?=0 OR `company_id`=?)
              AND (?='' OR `tag_no`=? OR `barcode`=?)
            ORDER BY `id` DESC
            LIMIT 200
            """);
        ErpDb.AddParameters(command, scope, scope, term, term, term);

        var rows = new List<ErpJwStockAvailabilityRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetDecimal(6),
                    reader.GetDecimal(7),
                    reader.GetDecimal(8),
                    reader.GetString(9),
                    reader.GetString(10)));
            }
        }
        catch (DbException exception)
        {
            return Empty(exception.Message, "database-error");
        }

        return new(rows, "database", string.Empty);
    }

    private static ErpJwStockAvailabilityResult Empty(string message, string source)
        => new([], source, message);
}
