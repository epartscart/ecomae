using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>workflow_save</c>/<c>workflow_run</c> twin (ajax_erp.php +
/// <c>content/general_pages/epc_workflow_builder.php</c>): saves a builder workflow and its steps
/// on <c>epc_workflows</c>/<c>epc_workflow_steps</c>, and executes a workflow recording the run on
/// <c>epc_workflow_runs</c> with the same step/status semantics as PHP.
/// </summary>
public interface IErpWorkflowWriteService
{
    Task<ErpWorkflowSaveResult> SaveAsync(ErpWorkflowSaveInput input, int adminId, CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_workflow_create</c>: insert a workflow + steps only (no <c>workflow_save</c> audit row).</summary>
    Task<ErpWorkflowSaveResult> CreateAsync(ErpWorkflowSaveInput input, int createdBy, CancellationToken cancellationToken = default);

    Task<ErpWorkflowRunResult> RunAsync(long workflowId, int adminId, JsonObject? triggerData = null, CancellationToken cancellationToken = default);
}

public sealed record ErpWorkflowStepInput(
    string? StepType,
    string? ActionType,
    string? Label,
    string? ConfigJson,
    string? OnFailure,
    int RetryCount);

public sealed record ErpWorkflowSaveInput(
    long Id,
    string? Name,
    string? Description,
    string? TriggerType,
    string? TriggerConfigJson,
    bool Active,
    IReadOnlyList<ErpWorkflowStepInput> Steps);

public sealed record ErpWorkflowSaveResult(bool Ok, string Message, long WorkflowId);

public sealed record ErpWorkflowStepResult(int StepOrder, string ActionType, string Status, long DurationMs, JsonNode? Output);

public sealed record ErpWorkflowRunResult(
    bool Ok,
    string Message,
    long RunId,
    string Status,
    long DurationMs,
    IReadOnlyList<ErpWorkflowStepResult> Steps);

public sealed class ErpWorkflowWriteService : IErpWorkflowWriteService
{
    private static readonly string[] TriggerTypes = ["event", "schedule", "manual", "webhook"];
    private static readonly string[] StepTypes = ["condition", "action", "delay", "branch", "loop"];
    private static readonly string[] FailureModes = ["stop", "skip", "retry"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;
    private readonly IErpAuditLogWriter _audit;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<ErpWorkflowWriteService>? _logger;

    public ErpWorkflowWriteService(
        IErpWriteConnectionFactory connections,
        IErpGlPostingService gl,
        IErpAuditLogWriter? audit = null,
        IHttpContextAccessor? httpContextAccessor = null,
        ILogger<ErpWorkflowWriteService>? logger = null)
    {
        _connections = connections;
        _gl = gl;
        _audit = audit ?? new ErpAuditLogWriter(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<ErpWorkflowSaveResult> SaveAsync(ErpWorkflowSaveInput input, int adminId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Id < 0)
        {
            throw new ErpWriteException("Invalid workflow id");
        }

        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }

        var siteKey = ResolveSiteKey();
        var name = (input.Name ?? string.Empty).Trim();
        if (input.Id <= 0 && name.Length == 0)
        {
            name = "Untitled Workflow";
        }

        var description = (input.Description ?? string.Empty).Trim();
        var triggerType = NormalizeEnum(input.TriggerType, "manual", TriggerTypes);
        var triggerConfig = NormalizeJsonObject(input.TriggerConfigJson);
        var steps = NormalizeSteps(input.Steps);
        var active = input.Active ? 1 : 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        long workflowId;
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            if (input.Id > 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "UPDATE `epc_workflows` SET `name` = ?, `description` = ?, `trigger_type` = ?, `trigger_config` = ?, `active` = ?, `updated_at` = NOW() WHERE `id` = ? AND `site_key` = ?"),
                    cancellationToken,
                    name, description, triggerType, triggerConfig.ToJsonString(), active, input.Id, siteKey).ConfigureAwait(false);
                await ReplaceStepsAsync(connection, transaction, input.Id, steps, cancellationToken).ConfigureAwait(false);
                workflowId = input.Id;
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_workflows` (`site_key`, `name`, `description`, `trigger_type`, `trigger_config`, `active`, `created_by`) VALUES (?, ?, ?, ?, ?, ?, ?)"),
                    cancellationToken,
                    siteKey, name, description, triggerType, triggerConfig.ToJsonString(), active, adminId).ConfigureAwait(false);
                workflowId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                await InsertStepsAsync(connection, transaction, workflowId, steps, cancellationToken).ConfigureAwait(false);
            }

            await _audit.LogAsync(
                connection,
                transaction,
                adminId,
                "workflow_save",
                "erp_workflow",
                workflowId,
                "Workflow saved",
                new Dictionary<string, string?>
                {
                    ["name"] = name,
                    ["trigger_type"] = triggerType,
                    ["steps"] = steps.Count.ToString(CultureInfo.InvariantCulture),
                },
                cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ErpWorkflowSaveResult(workflowId > 0, "Workflow saved", workflowId);
    }

    public async Task<ErpWorkflowSaveResult> CreateAsync(ErpWorkflowSaveInput input, int createdBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Id > 0)
        {
            throw new ErpWriteException("Invalid workflow id");
        }

        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }

        var siteKey = ResolveSiteKey();
        var name = (input.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            name = "Untitled Workflow";
        }

        var description = (input.Description ?? string.Empty).Trim();
        var triggerType = NormalizeEnum(input.TriggerType, "manual", TriggerTypes);
        var triggerConfig = NormalizeJsonObject(input.TriggerConfigJson);
        var steps = NormalizeSteps(input.Steps);
        var active = input.Active ? 1 : 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        long workflowId;
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_workflows` (`site_key`, `name`, `description`, `trigger_type`, `trigger_config`, `active`, `created_by`) VALUES (?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken,
                siteKey, name, description, triggerType, triggerConfig.ToJsonString(), active, createdBy).ConfigureAwait(false);
            workflowId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await InsertStepsAsync(connection, transaction, workflowId, steps, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return new ErpWorkflowSaveResult(workflowId > 0, "Workflow saved", workflowId);
    }

    public async Task<ErpWorkflowRunResult> RunAsync(long workflowId, int adminId, JsonObject? triggerData = null, CancellationToken cancellationToken = default)
    {
        if (workflowId <= 0)
        {
            throw new ErpWriteException("Missing workflow id");
        }

        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var workflow = await LoadWorkflowAsync(connection, workflowId, cancellationToken).ConfigureAwait(false);
        if (workflow is null)
        {
            throw new ErpWriteException("Workflow not found");
        }

        triggerData ??= new JsonObject { ["source"] = "manual_ui" };
        var started = DateTimeOffset.UtcNow;

        // PHP writes the run row first and keeps it even when steps fail — no ambient transaction.
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_workflow_runs` (`workflow_id`, `site_key`, `trigger_data`) VALUES (?, ?, ?)"),
            cancellationToken,
            workflowId, workflow.SiteKey, triggerData.ToJsonString()).ConfigureAwait(false);
        var runId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        var stepResults = new List<ErpWorkflowStepResult>();
        var overallStatus = "success";
        foreach (var step in workflow.Steps)
        {
            var stepStarted = DateTimeOffset.UtcNow;
            var output = await ExecuteStepAsync(connection, step, triggerData, workflow, adminId, cancellationToken).ConfigureAwait(false);
            var stepMs = (long)(DateTimeOffset.UtcNow - stepStarted).TotalMilliseconds;
            var ok = output?["ok"]?.GetValue<bool>() ?? false;

            stepResults.Add(new ErpWorkflowStepResult(
                step.StepOrder,
                step.ActionType,
                ok ? "success" : "failed",
                stepMs,
                output));

            if (!ok)
            {
                if (step.OnFailure == "stop")
                {
                    overallStatus = "failed";
                    break;
                }
            }
        }

        var durationMs = (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds;
        var stepResultsJson = JsonSerializer.Serialize(stepResults.Select(s => new
        {
            step_order = s.StepOrder,
            action_type = s.ActionType,
            status = s.Status,
            duration_ms = s.DurationMs,
            output = s.Output,
        }));

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_workflow_runs` SET `status` = ?, `step_results` = ?, `completed_at` = NOW(), `duration_ms` = ? WHERE `id` = ?"),
            cancellationToken,
            overallStatus, stepResultsJson, durationMs, runId).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_workflows` SET `run_count` = `run_count` + 1, `last_run_at` = NOW(), `last_run_status` = ? WHERE `id` = ?"),
            cancellationToken,
            overallStatus, workflowId).ConfigureAwait(false);

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "workflow_run",
            "erp_workflow",
            workflowId,
            "Workflow run " + overallStatus,
            new Dictionary<string, string?>
            {
                ["run_id"] = runId.ToString(CultureInfo.InvariantCulture),
                ["status"] = overallStatus,
            },
            cancellationToken).ConfigureAwait(false);

        return new ErpWorkflowRunResult(true, "Run " + overallStatus, runId, overallStatus, durationMs, stepResults);
    }

    private async Task<WorkflowRow?> LoadWorkflowAsync(DbConnection connection, long workflowId, CancellationToken cancellationToken)
    {
        string? siteKey = null;
        string? name = null;
        var found = false;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `site_key`, `name` FROM `epc_workflows` WHERE `id` = ? LIMIT 1");
            ErpDb.AddParameters(command, workflowId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                found = true;
                siteKey = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            }
        }

        if (!found)
        {
            return null;
        }

        var steps = new List<WorkflowStepRow>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `step_order`, `step_type`, `action_type`, `config`, `on_failure`, `retry_count` FROM `epc_workflow_steps` WHERE `workflow_id` = ? ORDER BY `step_order`");
            ErpDb.AddParameters(command, workflowId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var config = ParseJsonObject(reader.IsDBNull(3) ? null : reader.GetString(3));
                steps.Add(new WorkflowStepRow(
                    reader.GetInt32(0),
                    reader.IsDBNull(1) ? "action" : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    config,
                    reader.IsDBNull(4) ? "stop" : reader.GetString(4),
                    reader.IsDBNull(5) ? 0 : reader.GetInt32(5)));
            }
        }

        return new WorkflowRow(workflowId, siteKey ?? string.Empty, name ?? string.Empty, steps);
    }

    private async Task<JsonObject> ExecuteStepAsync(
        DbConnection connection,
        WorkflowStepRow step,
        JsonObject triggerData,
        WorkflowRow workflow,
        int adminId,
        CancellationToken cancellationToken)
    {
        var config = step.Config;

        switch (step.StepType)
        {
            case "condition":
            {
                var field = ReadString(config["field"]);
                var op = ReadString(config["operator"], "==");
                var compare = config["value"];
                var actual = triggerData[field];
                var pass = EvaluateCondition(actual, op, compare);
                return new JsonObject
                {
                    ["ok"] = pass,
                    ["condition"] = op,
                    ["actual"] = actual?.DeepClone(),
                };
            }

            case "action":
                return await RunActionAsync(connection, step.ActionType, config, triggerData, workflow, adminId, cancellationToken).ConfigureAwait(false);

            case "delay":
                return new JsonObject
                {
                    ["ok"] = true,
                    ["delay_minutes"] = ReadInt(config["delay_minutes"] ?? config["delay"]),
                    ["note"] = "Delay registered (async execution)",
                };

            default:
                return new JsonObject { ["ok"] = true, ["step_type"] = step.StepType };
        }
    }

    private async Task<JsonObject> RunActionAsync(
        DbConnection connection,
        string actionType,
        JsonObject config,
        JsonObject triggerData,
        WorkflowRow workflow,
        int adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (actionType)
            {
                case "send_notification":
                {
                    var title = Interpolate(ReadString(config["title"], "Workflow notification"), triggerData);
                    var message = Interpolate(ReadString(config["message"]), triggerData);
                    var linkTab = ReadString(config["link_tab"], "workflow_automation");
                    await SeedNotificationAsync(connection, title, message, linkTab, cancellationToken).ConfigureAwait(false);
                    return new JsonObject { ["ok"] = true, ["action"] = actionType, ["detail"] = new JsonObject { ["title"] = title } };
                }

                case "send_email":
                {
                    var to = Interpolate(ReadString(config["to"]), triggerData);
                    var subject = Interpolate(ReadString(config["subject"], "ERP workflow"), triggerData);
                    var body = Interpolate(ReadString(config["body"]), triggerData);
                    // PHP attempts mail() (usually unavailable) then always logs an in-app
                    // notification so operators can audit; ASP.NET keeps the audit row only.
                    const bool sent = false;
                    await SeedNotificationAsync(
                        connection,
                        "Email: " + subject,
                        (sent ? "Sent to " : "Queued/logged for ") + (to.Length > 0 ? to : "(no recipient)") + " — " + body,
                        "workflow_automation",
                        cancellationToken).ConfigureAwait(false);
                    return new JsonObject { ["ok"] = true, ["action"] = actionType, ["detail"] = new JsonObject { ["to"] = to, ["sent"] = sent } };
                }

                case "create_task":
                {
                    var title = Interpolate(ReadString(config["title"], "Workflow task"), triggerData);
                    var assignee = Interpolate(ReadString(config["assignee"]), triggerData);
                    var dueDays = ReadInt(config["due_days"], 3);
                    await SeedNotificationAsync(
                        connection,
                        "Task: " + title,
                        "Assignee: " + (assignee.Length > 0 ? assignee : "unassigned") + " · due in " + dueDays.ToString(CultureInfo.InvariantCulture) + " day(s)",
                        "processflow",
                        cancellationToken).ConfigureAwait(false);
                    return new JsonObject
                    {
                        ["ok"] = true,
                        ["action"] = actionType,
                        ["detail"] = new JsonObject { ["title"] = title, ["assignee"] = assignee, ["due_days"] = dueDays },
                    };
                }

                case "update_status":
                    return new JsonObject { ["ok"] = true, ["action"] = actionType, ["detail"] = new JsonObject { ["new_status"] = ReadString(config["new_status"]) } };

                case "assign_user":
                    return new JsonObject
                    {
                        ["ok"] = true,
                        ["action"] = actionType,
                        ["detail"] = new JsonObject { ["user_id"] = ReadInt(config["user_id"]), ["role"] = ReadString(config["role"]) },
                    };

                case "credit_check":
                {
                    var limit = triggerData["credit_limit"] is JsonNode l ? ReadDecimal(l) : (decimal?)null;
                    var balance = ReadDecimal(triggerData["credit_balance"]);
                    var amount = ReadDecimal(triggerData["amount"] ?? triggerData["total"]);
                    var exceed = limit.HasValue && (balance + amount) > limit.Value;
                    var onExceed = ReadString(config["action_on_exceed"], "hold");
                    if (exceed)
                    {
                        await SeedNotificationAsync(connection, "Credit limit exceeded", "Action: " + onExceed, "collections", cancellationToken).ConfigureAwait(false);
                    }

                    return new JsonObject
                    {
                        ["ok"] = true,
                        ["action"] = actionType,
                        ["detail"] = new JsonObject { ["exceeded"] = exceed, ["action"] = onExceed },
                    };
                }

                case "gl_journal":
                {
                    var amount = ReadDecimal(config["amount"] ?? triggerData["amount"]);
                    var debit = ReadString(config["debit_account"]);
                    var credit = ReadString(config["credit_account"]);
                    if (amount <= 0m)
                    {
                        return new JsonObject
                        {
                            ["ok"] = true,
                            ["action"] = actionType,
                            ["detail"] = new JsonObject { ["skipped"] = true, ["reason"] = "zero amount" },
                        };
                    }

                    var debitCoa = await CoaIdByCodeAsync(connection, debit, cancellationToken).ConfigureAwait(false);
                    var creditCoa = await CoaIdByCodeAsync(connection, credit, cancellationToken).ConfigureAwait(false);
                    if (debitCoa <= 0 || creditCoa <= 0)
                    {
                        return new JsonObject
                        {
                            ["ok"] = false,
                            ["action"] = actionType,
                            ["error"] = "GL account not found for debit '" + debit + "' or credit '" + credit + "'",
                        };
                    }

                    await _gl.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
                    var journalId = await _gl.PostJournalAsync(
                        connection,
                        new ErpGlJournalHeader
                        {
                            Description = "Workflow automation journal",
                            SourceType = "manual",
                        },
                        new[]
                        {
                            new ErpGlLine(debitCoa, amount, 0m, "Workflow: " + workflow.Name),
                            new ErpGlLine(creditCoa, 0m, amount, "Workflow: " + workflow.Name),
                        },
                        adminId,
                        cancellationToken).ConfigureAwait(false);
                    return new JsonObject
                    {
                        ["ok"] = true,
                        ["action"] = actionType,
                        ["detail"] = new JsonObject { ["journal_id"] = journalId, ["debit"] = debit, ["credit"] = credit, ["amount"] = amount },
                    };
                }

                case "create_invoice":
                    return new JsonObject
                    {
                        ["ok"] = true,
                        ["action"] = actionType,
                        ["detail"] = new JsonObject
                        {
                            ["template"] = ReadString(config["template"], "default"),
                            ["auto_send"] = config["auto_send"]?.GetValue<bool>() ?? false,
                        },
                    };

                case "update_inventory":
                    return new JsonObject
                    {
                        ["ok"] = true,
                        ["action"] = actionType,
                        ["detail"] = new JsonObject { ["sku"] = ReadString(config["sku"]), ["qty_change"] = ReadDecimal(config["qty_change"]) },
                    };

                case "webhook_call":
                {
                    var url = ReadString(config["url"]);
                    if (url.Length == 0 || !Regex.IsMatch(url, "^https?://", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        return new JsonObject { ["ok"] = false, ["action"] = actionType, ["error"] = "Invalid webhook URL" };
                    }

                    return new JsonObject
                    {
                        ["ok"] = true,
                        ["action"] = actionType,
                        ["detail"] = new JsonObject { ["url"] = url, ["queued"] = true },
                    };
                }

                case "emit_event":
                    return new JsonObject
                    {
                        ["ok"] = true,
                        ["action"] = actionType,
                        ["detail"] = new JsonObject { ["event_type"] = ReadString(config["event_type"]), ["payload"] = config["payload"]?.DeepClone() },
                    };

                case "wait":
                    return new JsonObject
                    {
                        ["ok"] = true,
                        ["action"] = actionType,
                        ["detail"] = new JsonObject { ["delay_minutes"] = ReadInt(config["delay_minutes"]) },
                    };

                default:
                    return new JsonObject
                    {
                        ["ok"] = true,
                        ["action"] = actionType,
                        ["detail"] = new JsonObject { ["executed"] = true, ["config"] = config.DeepClone() },
                    };
            }
        }
        catch (ErpWriteException ex)
        {
            return new JsonObject { ["ok"] = false, ["action"] = actionType, ["error"] = ex.Message };
        }
        catch (DbException ex)
        {
            _logger?.LogWarning(ex, "Workflow action {ActionType} failed", actionType);
            return new JsonObject { ["ok"] = false, ["action"] = actionType, ["error"] = ex.Message };
        }
    }

    private async Task SeedNotificationAsync(DbConnection connection, string title, string body, string linkTab, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_notifications` (`user_id`, `title`, `body`, `link_tab`, `time_created`) VALUES (0, ?, ?, ?, ?)"),
            cancellationToken,
            Clip(title, 255), Clip(body, 512), Clip(linkTab, 64), DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
    }

    private static async Task<long> CoaIdByCodeAsync(DbConnection connection, string code, CancellationToken cancellationToken)
    {
        if (code.Length == 0)
        {
            return 0;
        }

        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            code).ConfigureAwait(false);
    }

    private static async Task ReplaceStepsAsync(DbConnection connection, DbTransaction transaction, long workflowId, IReadOnlyList<NormalizedStep> steps, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DELETE FROM `epc_workflow_steps` WHERE `workflow_id` = ?"),
            cancellationToken,
            workflowId).ConfigureAwait(false);
        await InsertStepsAsync(connection, transaction, workflowId, steps, cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertStepsAsync(DbConnection connection, DbTransaction transaction, long workflowId, IReadOnlyList<NormalizedStep> steps, CancellationToken cancellationToken)
    {
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_workflow_steps` (`workflow_id`, `step_order`, `step_type`, `action_type`, `config`, `on_failure`, `retry_count`) VALUES (?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken,
                workflowId, i + 1, step.StepType, step.ActionType, step.Config.ToJsonString(), step.OnFailure, step.RetryCount).ConfigureAwait(false);
        }
    }

    public static List<NormalizedStep> NormalizeSteps(IReadOnlyList<ErpWorkflowStepInput> steps)
    {
        var normalized = new List<NormalizedStep>();
        foreach (var step in steps)
        {
            var config = ParseJsonObject(step.ConfigJson);
            var label = (step.Label ?? string.Empty).Trim();
            if (label.Length > 0 && config["label"] is null)
            {
                config["label"] = label;
            }

            normalized.Add(new NormalizedStep(
                NormalizeEnum(step.StepType, "action", StepTypes),
                (step.ActionType ?? string.Empty).Trim(),
                label,
                config,
                NormalizeEnum(step.OnFailure, "stop", FailureModes),
                step.RetryCount < 0 ? 0 : step.RetryCount));
        }

        return normalized;
    }

    private static JsonObject NormalizeJsonObject(string? json)
    {
        var node = ParseJsonNode(json);
        return node as JsonObject ?? new JsonObject();
    }

    private static JsonObject ParseJsonObject(string? json)
    {
        var node = ParseJsonNode(json);
        if (node is JsonObject obj)
        {
            return obj;
        }

        var wrapped = new JsonObject();
        if (node is not null)
        {
            wrapped["value"] = node;
        }

        return wrapped;
    }

    private static JsonNode? ParseJsonNode(string? json)
    {
        var text = (json ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static string NormalizeEnum(string? value, string fallback, string[] allowed)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return allowed.Contains(normalized, StringComparer.Ordinal) ? normalized : fallback;
    }

    public static bool EvaluateCondition(JsonNode? actual, string op, JsonNode? compare)
    {
        var actualText = NodeText(actual);
        var compareText = NodeText(compare);
        switch (op)
        {
            case "==": return string.Equals(actualText, compareText, StringComparison.Ordinal);
            case "!=": return !string.Equals(actualText, compareText, StringComparison.Ordinal);
            case ">": return CompareNumbers(actualText, compareText) > 0;
            case "<": return CompareNumbers(actualText, compareText) < 0;
            case ">=": return CompareNumbers(actualText, compareText) >= 0;
            case "<=": return CompareNumbers(actualText, compareText) <= 0;
            case "contains": return actualText.Contains(compareText, StringComparison.Ordinal);
            default: return false;
        }
    }

    private static int CompareNumbers(string left, string right)
    {
        if (decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out var l)
            && decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out var r))
        {
            return l.CompareTo(r);
        }

        return string.CompareOrdinal(left, right);
    }

    private static string NodeText(JsonNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            return text ?? string.Empty;
        }

        return node.ToJsonString();
    }

    public static string Interpolate(string template, JsonObject triggerData)
    {
        if (template.Length == 0)
        {
            return template;
        }

        return Regex.Replace(template, "\\{\\{\\s*([a-zA-Z0-9_\\.]+)\\s*\\}\\}", match =>
        {
            var key = match.Groups[1].Value;
            var node = triggerData[key];
            return node is null ? match.Value : NodeText(node);
        }, RegexOptions.CultureInvariant);
    }

    private static string ReadString(JsonNode? node, string fallback = "")
    {
        var text = NodeText(node);
        return text.Length == 0 ? fallback : text;
    }

    private static int ReadInt(JsonNode? node, int fallback = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var i))
            {
                return i;
            }

            if (value.TryGetValue<long>(out var l))
            {
                return (int)Math.Min(l, int.MaxValue);
            }

            if (value.TryGetValue<string>(out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out i))
            {
                return i;
            }
        }

        return fallback;
    }

    private static decimal ReadDecimal(JsonNode? node, decimal fallback = 0m)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<decimal>(out var d))
            {
                return d;
            }

            if (value.TryGetValue<string>(out var text) && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out d))
            {
                return d;
            }
        }

        return fallback;
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private string ResolveSiteKey()
    {
        var tenant = _httpContextAccessor?.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var siteKey = tenant?.SiteKey ?? string.Empty;
        return new string(siteKey.ToLowerInvariant().Where(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_').ToArray());
    }

    public sealed record NormalizedStep(string StepType, string ActionType, string Label, JsonObject Config, string OnFailure, int RetryCount);

    private sealed record WorkflowStepRow(int StepOrder, string StepType, string ActionType, JsonObject Config, string OnFailure, int RetryCount);

    private sealed record WorkflowRow(long Id, string SiteKey, string Name, IReadOnlyList<WorkflowStepRow> Steps);

    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_workflows` ("
            + "`id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,"
            + "`site_key` VARCHAR(64) NOT NULL,"
            + "`name` VARCHAR(128) NOT NULL,"
            + "`description` VARCHAR(512) NOT NULL DEFAULT '',"
            + "`trigger_type` ENUM('event','schedule','manual','webhook') NOT NULL DEFAULT 'manual',"
            + "`trigger_config` JSON NOT NULL,"
            + "`active` TINYINT(1) NOT NULL DEFAULT 0,"
            + "`version` INT UNSIGNED NOT NULL DEFAULT 1,"
            + "`run_count` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`last_run_at` DATETIME NULL,"
            + "`last_run_status` ENUM('success','failed','partial') NULL,"
            + "`created_by` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,"
            + "`updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,"
            + "INDEX `idx_site` (`site_key`),"
            + "INDEX `idx_trigger` (`trigger_type`),"
            + "INDEX `idx_active` (`active`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_workflow_steps` ("
            + "`id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,"
            + "`workflow_id` INT UNSIGNED NOT NULL,"
            + "`step_order` SMALLINT UNSIGNED NOT NULL DEFAULT 0,"
            + "`step_type` ENUM('condition','action','delay','branch','loop') NOT NULL DEFAULT 'action',"
            + "`action_type` VARCHAR(64) NOT NULL DEFAULT '',"
            + "`config` JSON NOT NULL,"
            + "`on_failure` ENUM('stop','skip','retry') NOT NULL DEFAULT 'stop',"
            + "`retry_count` TINYINT UNSIGNED NOT NULL DEFAULT 0,"
            + "INDEX `idx_workflow` (`workflow_id`),"
            + "INDEX `idx_order` (`step_order`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_workflow_runs` ("
            + "`id` BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,"
            + "`workflow_id` INT UNSIGNED NOT NULL,"
            + "`site_key` VARCHAR(64) NOT NULL,"
            + "`status` ENUM('running','success','failed','cancelled') NOT NULL DEFAULT 'running',"
            + "`trigger_data` JSON NULL,"
            + "`step_results` JSON NULL,"
            + "`started_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,"
            + "`completed_at` DATETIME NULL,"
            + "`duration_ms` INT UNSIGNED NOT NULL DEFAULT 0,"
            + "`error_message` TEXT NULL,"
            + "INDEX `idx_workflow` (`workflow_id`),"
            + "INDEX `idx_status` (`status`),"
            + "INDEX `idx_started` (`started_at`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_notifications` ("
            + "`id` int(11) NOT NULL AUTO_INCREMENT,"
            + "`user_id` int(11) NOT NULL DEFAULT 0,"
            + "`title` varchar(255) NOT NULL,"
            + "`body` varchar(512) DEFAULT NULL,"
            + "`link_tab` varchar(64) DEFAULT NULL,"
            + "`is_read` tinyint(1) NOT NULL DEFAULT 0,"
            + "`time_created` int(11) NOT NULL DEFAULT 0,"
            + "PRIMARY KEY (`id`),"
            + "KEY `x_user` (`user_id`,`is_read`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP notification centre stub'",
            cancellationToken).ConfigureAwait(false);
    }
}
