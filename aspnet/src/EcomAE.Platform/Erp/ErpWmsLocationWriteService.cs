namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_wms_location_save</c> / <c>epc_wms_location_delete</c> twins. Schema ensure stays PHP.
/// </summary>
public interface IErpWmsLocationWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        string? code,
        string? warehouse,
        string? zone,
        string? type,
        int capacity,
        int active,
        int companyId,
        long id,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(long locationId, CancellationToken cancellationToken = default);
}

public sealed class ErpWmsLocationWriteService : IErpWmsLocationWriteService
{
    public static readonly string[] AllowedTypes = ["receive", "pick", "bulk", "ship", "count"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpWmsLocationWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        string? code,
        string? warehouse,
        string? zone,
        string? type,
        int capacity,
        int active,
        int companyId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var loc = NormalizeCode(code);
        if (loc.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Location code is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var wh = NormalizeWarehouse(warehouse);
        var zn = Clip(zone, 40);
        var locType = NormalizeType(type);
        if (capacity < 0)
        {
            capacity = 0;
        }

        var isActive = active == 0 ? 0 : 1;
        if (companyId < 0)
        {
            companyId = 0;
        }

        if (id < 0)
        {
            id = 0;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (id > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        "UPDATE `epc_erp_wms_locations` SET `warehouse`=?, `zone`=?, `code`=?, `type`=?, `capacity`=?, `active`=? WHERE `id`=?"),
                    cancellationToken,
                    wh, zn, loc, locType, capacity, isActive, id);
                return ErpSimpleWriteResult.Ok("Location saved", id);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_wms_locations` (`company_id`,`warehouse`,`zone`,`code`,`type`,`capacity`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?,?)"),
                cancellationToken,
                companyId, wh, zn, loc, locType, capacity, isActive, now);
            var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Location saved", inserted);
        }
        catch (Exception ex) when (ex.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Location code already exists in this warehouse.");
        }
    }

    public static string NormalizeType(string? raw)
    {
        var type = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return AllowedTypes.Contains(type, StringComparer.Ordinal) ? type : "pick";
    }

    public static string NormalizeCode(string? raw) => Clip(raw, 60).ToUpperInvariant();

    public static string NormalizeWarehouse(string? raw)
    {
        var wh = Clip(raw, 40).ToUpperInvariant();
        return wh.Length == 0 ? "MAIN" : wh;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long locationId,
        CancellationToken cancellationToken = default)
    {
        if (locationId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A warehouse location id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var holding = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM `epc_erp_wms_lp` WHERE `location_id` = ? AND `status` = 'active' AND `qty` > 0"),
            cancellationToken,
            locationId);
        if (holding > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Location holds stock — move or close license plates first");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_erp_wms_locations` WHERE `id` = ?"),
            cancellationToken,
            locationId);
        return ErpSimpleWriteResult.Ok("Warehouse location deleted.", locationId);
    }
}
