using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>ajax_channels.php</c> <c>toggle_channel</c> twin. Seed / sync / import stay PHP.</summary>
public interface ICpChannelWriteService
{
    Task<ErpSimpleWriteResult> ToggleAsync(string? code, int? enabled, CancellationToken cancellationToken = default);
}

public sealed class CpChannelWriteService : ICpChannelWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpChannelWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ToggleAsync(
        string? code,
        int? enabled,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeCode(code);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A channel code is required.");
        }

        if (enabled is not (null or 0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Enabled must be 0 or 1.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var current = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `active` FROM `epc_marketplace_channels` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            key);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_marketplace_channels` WHERE `code` = ?"),
            cancellationToken,
            key);
        if (exists != 1)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Channel not found — sync partners first.");
        }

        var next = enabled ?? (current == 1 ? 0 : 1);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_marketplace_channels` SET `active` = ? WHERE `code` = ?"),
            cancellationToken,
            next, key);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_channel_sync_log` (`kind`, `channel_code`, `message`, `payload_json`, `time_created`) VALUES (?, ?, ?, ?, ?)"),
            cancellationToken,
            "channel",
            key,
            (next == 1 ? "Enabled" : "Disabled") + " channel " + key,
            null,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return ErpSimpleWriteResult.Ok((next == 1 ? "Enabled" : "Disabled") + " " + key, 0);
    }

    internal static string NormalizeCode(string? code)
    {
        var raw = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (raw.Length == 0 || raw.Length > 64)
        {
            return string.Empty;
        }

        return raw.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_') ? raw : string.Empty;
    }
}
