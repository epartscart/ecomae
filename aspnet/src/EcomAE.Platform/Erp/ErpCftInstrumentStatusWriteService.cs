using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cft_instrument_set_status</c> / ajax <c>cft_instrument_status</c> twin.
/// UPDATE <c>epc_cft_instrument.status</c> when the transition is allowed and log the event.
/// Instrument save, forecast, projection, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpCftInstrumentStatusWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(
        ErpCftInstrumentStatusWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCftInstrumentStatusWriteRequest(
    long Id = 0,
    string? TargetStatus = null,
    string? Detail = null,
    decimal Amount = 0);

public sealed class ErpCftInstrumentStatusWriteService : IErpCftInstrumentStatusWriteService
{
    public static IReadOnlyDictionary<string, string[]> Transitions { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["draft"] = ["issued", "cancelled"],
            ["issued"] = ["amended", "utilized", "expired", "closed"],
            ["amended"] = ["utilized", "expired", "closed"],
            ["utilized"] = ["closed"],
            ["expired"] = ["closed"],
            ["closed"] = [],
            ["cancelled"] = [],
        };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpCftInstrumentStatusWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        ErpCftInstrumentStatusWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Instrument not found");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var target = request.TargetStatus ?? string.Empty;
        var detail = request.Detail ?? string.Empty;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_cft_instrument", "status", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_cft_instr_event", "event_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Bank instrument table is not provisioned");
        }

        var statusObj = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_cft_instrument` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        if (statusObj is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Instrument not found");
        }

        var current = Convert.ToString(statusObj, CultureInfo.InvariantCulture) ?? string.Empty;
        var allowed = Transitions.TryGetValue(current, out var next) ? next : [];
        if (!allowed.Contains(target, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cannot move instrument from " + current + " to " + target);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var eventDetail = detail.Length > 0 ? detail : ("Status -> " + target);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_cft_instrument` SET `status`=?, `time_updated`=? WHERE `id`=?"),
            cancellationToken,
            target,
            now,
            request.Id).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_cft_instr_event` (`instrument_id`,`event_type`,`detail`,`amount`,`time_created`) VALUES (?,?,?,?,?)"),
            cancellationToken,
            request.Id,
            target,
            eventDetail,
            request.Amount,
            now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Instrument moved to " + target, request.Id);
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
