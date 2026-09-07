using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_pf_step_save</c> / ajax <c>pf_step_save</c> twin.
/// INSERT <c>epc_pf_steps</c>. Case start/act, process save, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpPfStepSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpPfStepSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPfStepSaveWriteRequest(
    long ProcessId = 0,
    string? Name = null,
    string? AssignType = null,
    long AssignUserId = 0,
    string? AssignDepartment = null,
    int StepNo = 0,
    int SlaHours = 24,
    string? Instructions = null);

public sealed class ErpPfStepSaveWriteService : IErpPfStepSaveWriteService
{
    public static readonly HashSet<string> AssignTypes = new(StringComparer.Ordinal)
    {
        "dept_head", "user", "department", "initiator",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpPfStepSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpPfStepSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var processId = request.ProcessId;
        var name = Clip((request.Name ?? string.Empty).Trim(), 160);
        var invalid = Validate(processId, name);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var type = request.AssignType ?? string.Empty;
        if (!AssignTypes.Contains(type))
        {
            type = "dept_head";
        }

        var dept = Clip(request.AssignDepartment ?? string.Empty, 32);
        var sla = Math.Max(0, request.SlaHours);
        var instructions = request.Instructions ?? string.Empty;
        var assignUser = request.AssignUserId < 0 ? 0 : request.AssignUserId;
        var stepNo = request.StepNo;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_pf_steps", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_pf_steps", "assign_type", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_pf_steps", "process_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Process step table is not provisioned");
        }

        if (stepNo <= 0)
        {
            stepNo = (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COALESCE(MAX(`step_no`),0) FROM `epc_pf_steps` WHERE `process_id` = ?"),
                cancellationToken,
                processId).ConfigureAwait(false) + 1;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_pf_steps` (`process_id`,`step_no`,`name`,`assign_type`,`assign_user_id`,`assign_department`,`sla_hours`,`instructions`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            processId,
            stepNo,
            name,
            type,
            assignUser,
            dept,
            sla,
            instructions,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Step added", id);
    }

    public static string? Validate(long processId, string name)
    {
        if (processId <= 0)
        {
            return "Process is required";
        }

        return name.Length == 0 ? "Step name is required" : null;
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
}
