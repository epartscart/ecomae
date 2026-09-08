using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_qm_test_add</c> / ajax <c>qm_test_add</c> twin.
/// INSERT <c>epc_qm_test</c>. Plan save, orders, NCR, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpQmTestAddWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpQmTestAddWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpQmTestAddWriteRequest(
    long PlanId = 0,
    string? Name = null,
    string? TestType = null,
    string? Unit = null,
    decimal? MinVal = null,
    decimal? MaxVal = null,
    string? Expected = null,
    int Sort = 0);

public sealed class ErpQmTestAddWriteService : IErpQmTestAddWriteService
{
    public static readonly HashSet<string> TestTypes = new(StringComparer.Ordinal)
    {
        "quantitative", "qualitative",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpQmTestAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpQmTestAddWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = request.TestType ?? "quantitative";
        if (!TestTypes.Contains(type))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid test type");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var planId = request.PlanId < 0 ? 0 : request.PlanId;
        var name = request.Name ?? string.Empty;
        var unit = request.Unit ?? string.Empty;
        var expected = request.Expected ?? string.Empty;
        var minVal = request.MinVal;
        var maxVal = request.MaxVal;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_qm_test", "plan_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_qm_test", "name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Test table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_qm_test` (`plan_id`,`name`,`test_type`,`unit`,`min_val`,`max_val`,`expected`,`sort`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            planId,
            name,
            type,
            unit,
            minVal,
            maxVal,
            expected,
            request.Sort).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Test added", id);
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
