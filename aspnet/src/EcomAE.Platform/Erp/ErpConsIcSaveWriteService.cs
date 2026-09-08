using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cons_ic_save</c> twin. INSERT <c>epc_cons_ic</c>.
/// Schema ensure and figures save stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpConsIcSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpConsIcSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpConsIcSaveWriteRequest(
    string? FromEntity = null,
    string? ToEntity = null,
    string? TxnType = null,
    decimal Amount = 0,
    string? TxnDate = null,
    string? Ref = null,
    string? Memo = null);

public sealed class ErpConsIcSaveWriteService : IErpConsIcSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpConsIcSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpConsIcSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var from = Clip((request.FromEntity ?? string.Empty).Trim().ToUpperInvariant(), 40);
        var to = Clip((request.ToEntity ?? string.Empty).Trim().ToUpperInvariant(), 40);
        if (from.Length == 0 || to.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "From and to entities are required");
        }

        if (from == to)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Intercompany needs two different entities");
        }

        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        if (amount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Amount must be positive");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var type = Clip((request.TxnType ?? string.Empty).Trim(), 20);
        if (type.Length == 0)
        {
            type = "sale";
        }

        var date = Clip((request.TxnDate ?? string.Empty).Trim(), 10);
        if (date.Length == 0)
        {
            date = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var pref = Clip((request.Ref ?? string.Empty).Trim(), 60);
        if (pref.Length == 0)
        {
            pref = Clip("IC-" + from + "-" + to + "-" + now.ToString(CultureInfo.InvariantCulture), 60);
        }

        var memo = Clip((request.Memo ?? string.Empty).Trim(), 200);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_cons_ic", "from_entity", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_cons_ic", "amount", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Consolidation IC table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_cons_ic` (`ref`,`from_entity`,`to_entity`,`txn_type`,`amount`,`txn_date`,`memo`,`time_created`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            pref,
            from,
            to,
            type,
            amount,
            date,
            memo,
            now).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Intercompany transaction recorded", inserted);
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
