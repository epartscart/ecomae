using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_wms_wave_create</c> + <c>epc_wms_wave_add_pick</c> twin
/// (ajax_erp <c>wms_wave_create</c>). Schema ensure, receive, and work complete stay PHP.
/// </summary>
public interface IErpWmsWaveCreateWriteService
{
    Task<ErpSimpleWriteResult> CreateWithPickAsync(
        string? item,
        decimal qty,
        string? reference,
        int companyId,
        long fromLocationId,
        long toLocationId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpWmsWaveCreateWriteService : IErpWmsWaveCreateWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWmsWaveCreateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateWithPickAsync(
        string? item,
        decimal qty,
        string? reference,
        int companyId,
        long fromLocationId,
        long toLocationId,
        CancellationToken cancellationToken = default)
    {
        var sku = (item ?? string.Empty).Trim();
        if (sku.Length == 0 || qty <= 0m)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Item and a positive qty are required.");
        }

        if (sku.Length > 120)
        {
            sku = sku[..120];
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (companyId < 0)
        {
            companyId = 0;
        }

        var note = (reference ?? string.Empty).Trim();
        if (note.Length > 120)
        {
            note = note[..120];
        }

        if (fromLocationId < 0)
        {
            fromLocationId = 0;
        }

        if (toLocationId < 0)
        {
            toLocationId = 0;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var seq = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_wms_waves` WHERE `company_id` = ?"),
            cancellationToken,
            companyId);
        var waveNo = "WAVE" + (seq + 1).ToString("00000", CultureInfo.InvariantCulture);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_wms_waves` (`company_id`,`wave_no`,`reference`,`status`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?)"),
            cancellationToken,
            companyId, waveNo, note, "open", now, now);

        var waveId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_wms_work` (`company_id`,`work_type`,`reference`,`wave_id`,`item`,`qty`,`from_location_id`,`to_location_id`,`lp_id`,`status`,`assigned_to`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId, "pick", note, waveId, sku, qty, fromLocationId, toLocationId, 0L, "open", "", now, now);

        return ErpSimpleWriteResult.Ok("Wave " + waveNo + " created with pick work.", waveId);
    }
}
