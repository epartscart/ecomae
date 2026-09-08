using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_pf_case_act</c> / ajax <c>pf_case_act</c> twin.
/// Approve hands the case to the next step; reject stops it. Does not CREATE tables.
/// Start, seed, and schema ensure stay PHP.
/// </summary>
public interface IErpPfCaseActWriteService
{
    Task<ErpPfCaseActWriteResult> ActAsync(
        ErpPfCaseActWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPfCaseActWriteRequest(
    long CaseId = 0,
    string? Decision = null,
    string? Comment = null,
    long ActorUserId = 0);

public sealed record ErpPfCaseActWriteResult(
    bool Succeeded,
    string Code,
    string Message,
    long Id,
    int Writes,
    string CaseStatus,
    long NextAssignee)
{
    public static ErpPfCaseActWriteResult Ok(string message, long caseId, string caseStatus, long nextAssignee)
        => new(true, "ok", message, caseId, 1, caseStatus, nextAssignee);

    public static ErpPfCaseActWriteResult Fail(string code, string message)
        => new(false, code, message, 0, 0, string.Empty, 0);
}

public sealed class ErpPfCaseActWriteService : IErpPfCaseActWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPfCaseActWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpPfCaseActWriteResult> ActAsync(
        ErpPfCaseActWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId <= 0)
        {
            return ErpPfCaseActWriteResult.Fail("invalid", "Case not found");
        }

        if (!_connections.IsConfigured)
        {
            return ErpPfCaseActWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var actorId = request.ActorUserId;
        var comment = request.Comment ?? string.Empty;
        var reject = string.Equals(request.Decision, "reject", StringComparison.Ordinal);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_pf_cases", "status", cancellationToken).ConfigureAwait(false)
            || !await ErpPfRouting.ColumnExistsAsync(connection, null, "epc_pf_case_steps", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpPfCaseActWriteResult.Fail("invalid", "Process-flow case tables are not provisioned");
        }

        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_pf_cases` WHERE `id`=?"),
            cancellationToken,
            request.CaseId).ConfigureAwait(false);
        if (string.IsNullOrEmpty(status))
        {
            return ErpPfCaseActWriteResult.Fail("invalid", "Case not found");
        }

        if (!string.Equals(status, "open", StringComparison.Ordinal))
        {
            return ErpPfCaseActWriteResult.Fail("invalid", "This case is already " + status);
        }

        var curNo = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `current_step_no` FROM `epc_pf_cases` WHERE `id`=?"),
            cancellationToken,
            request.CaseId).ConfigureAwait(false);
        var processId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `process_id` FROM `epc_pf_cases` WHERE `id`=?"),
            cancellationToken,
            request.CaseId).ConfigureAwait(false);
        var initiatorId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `initiator_id` FROM `epc_pf_cases` WHERE `id`=?"),
            cancellationToken,
            request.CaseId).ConfigureAwait(false);

        var stepId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `id` FROM `epc_pf_case_steps` WHERE `case_id` = ? AND `step_no` = ? AND `status` = 'active' LIMIT 1"),
            cancellationToken,
            request.CaseId,
            curNo).ConfigureAwait(false);
        if (stepId <= 0)
        {
            return ErpPfCaseActWriteResult.Fail("invalid", "No active step on this case");
        }

        if (reject)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_pf_case_steps` SET `status`='rejected', `comment`=?, `completed_at`=?, `acted_by`=? WHERE `id`=?"),
                cancellationToken,
                comment,
                now,
                actorId,
                stepId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_pf_cases` SET `status`='rejected', `completed_at`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                now,
                now,
                request.CaseId).ConfigureAwait(false);
            return ErpPfCaseActWriteResult.Ok(
                "Case rejected at step " + curNo.ToString(CultureInfo.InvariantCulture),
                request.CaseId,
                "rejected",
                0);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_pf_case_steps` SET `status`='approved', `comment`=?, `completed_at`=?, `acted_by`=? WHERE `id`=?"),
            cancellationToken,
            comment,
            now,
            actorId,
            stepId).ConfigureAwait(false);

        var nextStepId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `id` FROM `epc_pf_case_steps` WHERE `case_id` = ? AND `step_no` > ? ORDER BY `step_no` LIMIT 1"),
            cancellationToken,
            request.CaseId,
            curNo).ConfigureAwait(false);
        if (nextStepId <= 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_pf_cases` SET `status`='done', `completed_at`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                now,
                now,
                request.CaseId).ConfigureAwait(false);
            return ErpPfCaseActWriteResult.Ok("Final step approved — case complete", request.CaseId, "done", 0);
        }

        var nextStepNo = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `step_no` FROM `epc_pf_case_steps` WHERE `id`=?"),
            cancellationToken,
            nextStepId).ConfigureAwait(false);
        var assignType = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `assign_type` FROM `epc_pf_case_steps` WHERE `id`=?"),
            cancellationToken,
            nextStepId).ConfigureAwait(false) ?? "dept_head";
        var assignUserId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `assignee_id` FROM `epc_pf_case_steps` WHERE `id`=?"),
            cancellationToken,
            nextStepId).ConfigureAwait(false);
        var assignDepartment = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `department` FROM `epc_pf_case_steps` WHERE `id`=?"),
            cancellationToken,
            nextStepId).ConfigureAwait(false) ?? string.Empty;
        var slaHours = 24;

        var tplId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_pf_steps` WHERE `process_id` = ? AND `step_no` = ? LIMIT 1"),
            cancellationToken,
            processId,
            nextStepNo).ConfigureAwait(false);
        if (tplId > 0)
        {
            assignType = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `assign_type` FROM `epc_pf_steps` WHERE `id`=?"),
                cancellationToken,
                tplId).ConfigureAwait(false) ?? assignType;
            assignUserId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `assign_user_id` FROM `epc_pf_steps` WHERE `id`=?"),
                cancellationToken,
                tplId).ConfigureAwait(false);
            assignDepartment = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `assign_department` FROM `epc_pf_steps` WHERE `id`=?"),
                cancellationToken,
                tplId).ConfigureAwait(false) ?? string.Empty;
            slaHours = (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `sla_hours` FROM `epc_pf_steps` WHERE `id`=?"),
                cancellationToken,
                tplId).ConfigureAwait(false);
        }

        var deptHeads = await ErpPfRouting.DeptHeadsAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var stepDef = new ErpPfRouting.StepDef(nextStepNo, string.Empty, assignType, assignUserId, assignDepartment, slaHours);
        var assignee = await ErpPfRouting.ResolveAssigneeAsync(
            connection, null, stepDef, initiatorId, deptHeads, actorId, cancellationToken).ConfigureAwait(false);
        var assigneeLoc = await ErpPfRouting.UserLocationAsync(connection, null, assignee, cancellationToken).ConfigureAwait(false);
        var slaDue = slaHours > 0 ? now + (slaHours * 3600L) : 0;

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_pf_case_steps` SET `status`='active', `assignee_id`=?, `location`=?, `sla_due_at`=?, `activated_at`=? WHERE `id`=?"),
            cancellationToken,
            assignee,
            assigneeLoc,
            slaDue,
            now,
            nextStepId).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_pf_cases` SET `current_step_no`=?, `current_assignee_id`=?, `current_department`=?, `current_location`=?, `due_at`=?, `time_updated`=? WHERE `id`=?"),
            cancellationToken,
            nextStepNo,
            assignee,
            assignDepartment,
            assigneeLoc,
            slaDue,
            now,
            request.CaseId).ConfigureAwait(false);

        var name = await ErpPfRouting.UserNameAsync(connection, null, assignee, cancellationToken).ConfigureAwait(false);
        return ErpPfCaseActWriteResult.Ok(
            "Approved — routed to " + name + " (step " + nextStepNo.ToString(CultureInfo.InvariantCulture) + ")",
            request.CaseId,
            "open",
            assignee);
    }
}
