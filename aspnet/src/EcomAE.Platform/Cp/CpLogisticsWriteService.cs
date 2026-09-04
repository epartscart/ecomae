using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>ajax_logistics.php</c> <c>toggle_carrier</c> twin. Seed / create-shipment stay PHP.</summary>
public interface ICpLogisticsWriteService
{
    Task<ErpSimpleWriteResult> ToggleCarrierAsync(string? code, CancellationToken cancellationToken = default);
}

public sealed class CpLogisticsWriteService : ICpLogisticsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpLogisticsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ToggleCarrierAsync(
        string? code,
        CancellationToken cancellationToken = default)
    {
        var key = CpChannelWriteService.NormalizeCode(code);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A carrier code is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_carrier_accounts` WHERE `code` = ?"),
            cancellationToken,
            key);
        if (exists != 1)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Carrier not found — seed partners first.");
        }

        var current = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `active` FROM `epc_carrier_accounts` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            key);
        var next = current == 1 ? 0 : 1;
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_carrier_accounts` SET `active` = ? WHERE `code` = ?"),
            cancellationToken,
            next, key);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_channel_sync_log` (`kind`, `channel_code`, `message`, `payload_json`, `time_created`) VALUES (?, ?, ?, ?, ?)"),
            cancellationToken,
            "carrier",
            key,
            (next == 1 ? "Enabled" : "Disabled") + " carrier " + key,
            null,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return ErpSimpleWriteResult.Ok((next == 1 ? "Enabled" : "Disabled") + " " + key, 0);
    }
}
