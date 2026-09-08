using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_intg_event_raise</c> / ajax <c>intg_event_raise</c> twin.
/// INSERT <c>epc_intg_event_log</c> (one <c>queued</c> row per active match, or one
/// <c>no_subscriber</c> row). Subscription save and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpIntgEventRaiseWriteService
{
    Task<ErpSimpleWriteResult> RaiseAsync(
        ErpIntgEventRaiseWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpIntgEventRaiseWriteRequest(
    long CompanyId = 0,
    string? Event = null,
    string? Payload = null);

public sealed class ErpIntgEventRaiseWriteService : IErpIntgEventRaiseWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpIntgEventRaiseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RaiseAsync(
        ErpIntgEventRaiseWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var ev = Clip(request.Event ?? string.Empty, 120);
        var payloadJson = EncodePayload(request.Payload);
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_intg_event_log", "event", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_intg_event_log", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Event log table is not provisioned");
        }

        var hasSubs = await ColumnExistsAsync(connection, "epc_intg_event_sub", "event", cancellationToken).ConfigureAwait(false)
            && await ColumnExistsAsync(connection, "epc_intg_event_sub", "active", cancellationToken).ConfigureAwait(false);
        var matched = hasSubs
            ? await LoadMatchesAsync(connection, companyId, ev, cancellationToken).ConfigureAwait(false)
            : [];

        if (matched.Count == 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_intg_event_log` (`company_id`,`event`,`payload_json`,`sub_id`,`target_type`,`target`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?)"),
                cancellationToken,
                companyId,
                ev,
                payloadJson,
                0L,
                string.Empty,
                string.Empty,
                "no_subscriber",
                now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Event raised · 0 delivery(ies) queued", id, 1);
        }

        long lastId = 0;
        foreach (var row in matched)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_intg_event_log` (`company_id`,`event`,`payload_json`,`sub_id`,`target_type`,`target`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?)"),
                cancellationToken,
                companyId,
                ev,
                payloadJson,
                row.SubId,
                row.TargetType,
                row.Target,
                "queued",
                now).ConfigureAwait(false);
            lastId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }

        return new ErpSimpleWriteResult(
            true,
            "ok",
            "Event raised · " + matched.Count.ToString(CultureInfo.InvariantCulture) + " delivery(ies) queued",
            lastId,
            matched.Count);
    }

    /// <summary>
    /// PHP ajax: <c>json_decode(payload, true)</c>; if not an array, wrap as <c>{raw: payload}</c>.
    /// Then <c>json_encode</c> for <c>payload_json</c>.
    /// </summary>
    public static string EncodePayload(string? raw)
    {
        raw ??= string.Empty;
        try
        {
            var node = JsonNode.Parse(raw);
            if (node is JsonObject or JsonArray)
            {
                return node.ToJsonString();
            }
        }
        catch (JsonException)
        {
        }

        return JsonSerializer.Serialize(new Dictionary<string, string> { ["raw"] = raw });
    }

    private static async Task<IReadOnlyList<MatchedSub>> LoadMatchesAsync(
        DbConnection connection,
        long companyId,
        string ev,
        CancellationToken cancellationToken)
    {
        var sql = companyId > 0
            ? "SELECT `id`,`target_type`,`target`,`active` FROM `epc_intg_event_sub` WHERE `event`=? AND `company_id`=? ORDER BY `event`, `id`"
            : "SELECT `id`,`target_type`,`target`,`active` FROM `epc_intg_event_sub` WHERE `event`=? ORDER BY `event`, `id`";
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        if (companyId > 0)
        {
            ErpDb.AddParameters(command, ev, companyId);
        }
        else
        {
            ErpDb.AddParameters(command, ev);
        }

        var matched = new List<MatchedSub>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!IsPhpNonemptyActive(reader.IsDBNull(3) ? null : reader.GetValue(3)))
            {
                continue;
            }

            matched.Add(new MatchedSub(
                reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(2) ? string.Empty : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return matched;
    }

    /// <summary>PHP <c>!empty($s['active'])</c> — 0 / "0" / null / "" are inactive.</summary>
    public static bool IsPhpNonemptyActive(object? raw)
    {
        if (raw is null or DBNull)
        {
            return false;
        }

        var text = Convert.ToString(raw, CultureInfo.InvariantCulture);
        if (string.IsNullOrEmpty(text) || text == "0")
        {
            return false;
        }

        try
        {
            return Convert.ToInt64(raw, CultureInfo.InvariantCulture) != 0;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return true;
        }
    }

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

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

    private sealed record MatchedSub(long SubId, string TargetType, string Target);
}
