using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_as_warranty_register</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpAftersalesWarrantyWriteService
{
    Task<ErpSimpleWriteResult> RegisterAsync(
        ErpAftersalesWarrantyRegisterRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpAftersalesWarrantyRegisterRequest(
    long ItemId = 0,
    string? SerialNo = null,
    long CustomerId = 0,
    string? SourceType = null,
    long SourceId = 0,
    long StartDate = 0,
    int Months = 0);

public sealed class ErpAftersalesWarrantyWriteService : IErpAftersalesWarrantyWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpAftersalesWarrantyWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RegisterAsync(
        ErpAftersalesWarrantyRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var serial = (request.SerialNo ?? string.Empty).Trim();
        if (request.ItemId <= 0 && serial.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Item id or serial is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_as_warranty", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "After-sales warranty tables are not provisioned");
        }

        var start = request.StartDate > 0 ? request.StartDate : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var months = request.Months < 0 ? 0 : request.Months;
        long expires = 0;
        if (months > 0)
        {
            expires = DateTimeOffset.FromUnixTimeSeconds(start).AddMonths(months).ToUnixTimeSeconds();
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sourceType = (request.SourceType ?? string.Empty).Trim();
        if (sourceType.Length == 0)
        {
            sourceType = "sales_order";
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_as_warranty` (`item_id`,`serial_no`,`customer_id`,`source_type`,`source_id`,`start_date`,`months`,`expires_at`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            request.ItemId < 0 ? 0 : request.ItemId,
            Clip(serial, 80),
            request.CustomerId < 0 ? 0 : request.CustomerId,
            Clip(sourceType, 40),
            request.SourceId < 0 ? 0 : request.SourceId,
            start,
            months,
            expires,
            now).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Warranty registered", id);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
