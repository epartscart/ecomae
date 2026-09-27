using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live twin of PHP <c>sao/states_statuses_link.php</c> <c>save_action</c>.</summary>
public interface ICpSaoWriteService
{
    Task<ErpSimpleWriteResult> SaveStateLinksAsync(
        IReadOnlyDictionary<long, long> stateToStatus,
        CancellationToken cancellationToken = default);
}

public sealed class CpSaoWriteService : ICpSaoWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpSaoWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP posts one <c>select_item_status_&lt;stateId&gt;</c> field per SAO state.</summary>
    public static IReadOnlyDictionary<long, long> ParseStateFields(IEnumerable<KeyValuePair<string, string>> fields)
    {
        const string prefix = "select_item_status_";
        var map = new Dictionary<long, long>();
        foreach (var (key, value) in fields)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (!long.TryParse(key[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var stateId)
                || stateId <= 0)
            {
                continue;
            }

            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusId);
            map[stateId] = statusId < 0 ? 0 : statusId;
        }

        return map;
    }

    public async Task<ErpSimpleWriteResult> SaveStateLinksAsync(
        IReadOnlyDictionary<long, long> stateToStatus,
        CancellationToken cancellationToken = default)
    {
        if (stateToStatus.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No SAO states were submitted.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var known = new HashSet<long>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id` FROM `shop_sao_states`";
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                known.Add(Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture));
            }
        }

        var writes = 0;
        foreach (var (stateId, statusId) in stateToStatus)
        {
            if (!known.Contains(stateId))
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_sao_states` SET `status_id` = ? WHERE `id` = ?"),
                cancellationToken,
                statusId, stateId);
            writes++;
        }

        return writes == 0
            ? ErpSimpleWriteResult.Fail("missing", "None of the submitted SAO states exist.")
            : new ErpSimpleWriteResult(true, "ok", "SAO state to item-status links saved.", 0, writes);
    }
}
