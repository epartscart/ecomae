using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live port of PHP <c>epc_erp_gl_sync_unposted</c>: posts every active purchase and cash/bank entry
/// whose <c>gl_journal_id</c> is still 0 through the shared GL posting path. As in PHP, a failing
/// item is skipped and the returned count covers successful postings only. Schema stays PHP-owned.
/// </summary>
public interface IErpGlSyncUnpostedWriteService
{
    Task<int> SyncAsync(int adminId, CancellationToken cancellationToken = default);
}

public sealed class ErpGlSyncUnpostedWriteService : IErpGlSyncUnpostedWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;

    public ErpGlSyncUnpostedWriteService(IErpWriteConnectionFactory connections, IErpGlPostingService gl)
    {
        _connections = connections;
        _gl = gl;
    }

    public async Task<int> SyncAsync(int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        List<long> purchases;
        List<long> entries;
        try
        {
            purchases = await IdsAsync(c, "SELECT `id` FROM `epc_erp_purchases` WHERE `active` = 1 AND `gl_journal_id` = 0", cancellationToken).ConfigureAwait(false);
            entries = await IdsAsync(c, "SELECT `id` FROM `epc_erp_cash_bank_entries` WHERE `active` = 1 AND `gl_journal_id` = 0", cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            throw new ErpWriteException("GL sync requires the PHP epc_erp_purchases/epc_erp_cash_bank_entries schema: " + ex.Message);
        }

        var count = 0;
        foreach (var id in purchases)
        {
            try
            {
                await _gl.PostPurchaseAsync(c, id, adminId, cancellationToken).ConfigureAwait(false);
                count++;
            }
            catch (Exception ex) when (ex is DbException or ErpWriteException)
            {
            }
        }

        foreach (var id in entries)
        {
            try
            {
                await _gl.PostCashEntryAsync(c, id, adminId, cancellationToken).ConfigureAwait(false);
                count++;
            }
            catch (Exception ex) when (ex is DbException or ErpWriteException)
            {
            }
        }

        return count;
    }

    private static async Task<List<long>> IdsAsync(DbConnection c, string sql, CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(Convert.ToInt64(r.GetValue(0), System.Globalization.CultureInfo.InvariantCulture));
        }

        return ids;
    }
}
