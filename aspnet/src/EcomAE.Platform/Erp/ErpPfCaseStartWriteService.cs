using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_pf_case_start</c> / ajax <c>pf_case_start</c> twin.
/// INSERT the case and every process step, then activate step 1. Does not CREATE tables.
/// Act, seed, and schema ensure stay PHP.
/// </summary>
public interface IErpPfCaseStartWriteService
{
    Task<ErpSimpleWriteResult> StartAsync(
        ErpPfCaseStartWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPfCaseStartWriteRequest(
    long ProcessId = 0,
    string? Title = null,
    string? Reference = null,
    string? Priority = null,
    long InitiatorId = 0,
    string? SubjectType = null,
    long SubjectId = 0,
    long ActorUserId = 0);

public sealed class ErpPfCaseStartWriteService : IErpPfCaseStartWriteService
{
    private static readonly HashSet<string> Priorities = new(StringComparer.Ordinal)
    {
        "low", "normal", "high", "urgent"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpPfCaseStartWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> StartAsync(
        ErpPfCaseStartWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var title = ErpPfRouting.Clip((request.Title ?? string.Empty).Trim(), 255);
        if (title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Case title is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var initiator = request.InitiatorId > 0 ? request.InitiatorId : request.ActorUserId;
        var priority = Priorities.Contains(request.Priority ?? string.Empty) ? request.Priority! : "normal";
        var reference = ErpPfRouting.Clip(request.Reference ?? string.Empty, 120);
        var subjectType = ErpPfRouting.Clip(request.SubjectType ?? string.Empty, 40);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_pf_cases", "process_id", cancellationToken).ConfigureAwait(false)
            || !await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_pf_cases", "current_assignee_id", cancellationToken).ConfigureAwait(false)
            || !await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_pf_case_steps", "case_id", cancellationToken).ConfigureAwait(false)
            || !await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_pf_steps", "process_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Process-flow case tables are not provisioned");
        }

        var steps = await ErpPfRouting.ProcessStepsAsync(connection, null, request.ProcessId, cancellationToken).ConfigureAwait(false);
        if (steps.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This process has no steps defined yet");
        }

        var deptHeads = await ErpPfRouting.DeptHeadsAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "INSERT INTO `epc_pf_cases` (`process_id`,`title`,`reference`,`priority`,`status`,`current_step_no`,`initiator_id`,`subject_type`,`subject_id`,`started_at`,`time_created`,`time_updated`) VALUES (?,?,?,?,'open',?,?,?,?,?,?,?)"),
                cancellationToken,
                request.ProcessId,
                title,
                reference,
                priority,
                steps[0].StepNo,
                initiator,
                subjectType,
                request.SubjectId,
                now,
                now,
                now).ConfigureAwait(false);
            var caseId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);

            long firstAssignee = 0;
            var firstDept = string.Empty;
            long firstDue = 0;
            var firstLoc = string.Empty;
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var isFirst = i == 0;
                var assignee = isFirst
                    ? await ErpPfRouting.ResolveAssigneeAsync(
                        connection, tx, step, initiator, deptHeads, request.ActorUserId, cancellationToken).ConfigureAwait(false)
                    : 0;
                var loc = isFirst
                    ? await ErpPfRouting.UserLocationAsync(connection, tx, assignee, cancellationToken).ConfigureAwait(false)
                    : string.Empty;
                var slaDue = isFirst && step.SlaHours > 0 ? now + (step.SlaHours * 3600L) : 0;
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional(
                        "INSERT INTO `epc_pf_case_steps` (`case_id`,`step_no`,`name`,`assign_type`,`department`,`assignee_id`,`location`,`status`,`sla_due_at`,`activated_at`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    caseId,
                    step.StepNo,
                    step.Name,
                    step.AssignType,
                    step.AssignDepartment,
                    assignee,
                    loc,
                    isFirst ? "active" : "pending",
                    slaDue,
                    isFirst ? now : 0,
                    now).ConfigureAwait(false);
                if (isFirst)
                {
                    firstAssignee = assignee;
                    firstDept = step.AssignDepartment;
                    firstDue = slaDue;
                    firstLoc = loc;
                }
            }

            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "UPDATE `epc_pf_cases` SET `current_assignee_id`=?, `current_department`=?, `current_location`=?, `due_at`=? WHERE `id`=?"),
                cancellationToken,
                firstAssignee,
                firstDept,
                firstLoc,
                firstDue,
                caseId).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Case started — routed to first assignee", caseId);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}
