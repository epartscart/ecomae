using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_proc_policy_save</c> / ajax <c>proc_policy_save</c> twin.
/// UPDATE <c>epc_proc_policy</c> when <c>id</c> &gt; 0, else INSERT.
/// Category save and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpProcPolicySaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpProcPolicySaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpProcPolicySaveWriteRequest(
    long Id = 0,
    long CompanyId = 0,
    string? Name = null,
    long CategoryId = 0,
    decimal ApprovalThreshold = 0,
    string? PreferredVendor = null,
    int? Active = null);

public sealed class ErpProcPolicySaveWriteService : IErpProcPolicySaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpProcPolicySaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpProcPolicySaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Policy name is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var categoryId = request.CategoryId < 0 ? 0 : request.CategoryId;
        var vendor = request.PreferredVendor ?? string.Empty;
        var threshold = request.ApprovalThreshold;
        var active = request.Id > 0
            ? (request.Active is null || request.Active == 0 ? 0 : 1)
            : (request.Active is null ? 1 : (request.Active == 0 ? 0 : 1));

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_proc_policy", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_proc_policy", "approval_threshold", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Policy table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_proc_policy` SET `name`=?, `category_id`=?, `approval_threshold`=?, `preferred_vendor`=?, `active`=? WHERE `id`=?"),
                cancellationToken,
                name,
                categoryId,
                threshold,
                vendor,
                active,
                request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Policy saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_proc_policy` (`company_id`,`name`,`category_id`,`approval_threshold`,`preferred_vendor`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            name,
            categoryId,
            threshold,
            vendor,
            active,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Policy saved", id);
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
