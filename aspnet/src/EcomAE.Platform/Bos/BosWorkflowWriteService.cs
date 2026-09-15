using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>workflow_builder</c> <c>toggle</c> / <c>epc_workflow_toggle</c>,
/// <c>delete</c> / <c>epc_workflow_delete</c>, and <c>create</c> / <c>epc_workflow_create</c>.
/// Execute and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. PHP always returns ok — this write does not invent id/not-found checks.
/// </summary>
public interface IBosWorkflowWriteService
{
    Task<ErpSimpleWriteResult> ToggleAsync(
        long workflowId,
        bool active,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(
        long workflowId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateAsync(
        string? siteKey,
        string? workflowDataJson,
        CancellationToken cancellationToken = default);
}

public sealed class BosWorkflowWriteService : IBosWorkflowWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosWorkflowWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>PHP <c>(bool)($_POST['active'] ?? false)</c> — any non-empty string is true, including <c>0</c>.</summary>
    public static bool PhpPostedBool(string? raw)
        => !string.IsNullOrEmpty(raw);

    /// <summary>PHP <c>(int)</c> on a leading optional sign + digits token.</summary>
    public static long PhpIntval(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var text = raw.Trim();
        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        while (i < text.Length && char.IsDigit(text[i]))
        {
            i++;
        }

        if (i == 0 || (i == 1 && text[0] is '+' or '-'))
        {
            return 0;
        }

        return long.TryParse(text[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static long PhpIntval(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var n) => n,
            JsonValueKind.Number when element.TryGetDecimal(out var d) => (long)d,
            JsonValueKind.String => PhpIntval(element.GetString()),
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => 0
        };

    private static string JsonString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "";
        }

        return el.ValueKind == JsonValueKind.String ? (el.GetString() ?? "") : el.GetRawText();
    }

    /// <summary>
    /// PHP <c>json_encode($data['trigger_config'] ?? array())</c> — missing/null/empty-object becomes <c>[]</c>
    /// because <c>json_decode(..., true)</c> turns <c>{}</c> into an empty PHP array.
    /// </summary>
    public static string EncodeJsonField(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "[]";
        }

        if (el.ValueKind == JsonValueKind.Object && !el.EnumerateObject().Any())
        {
            return "[]";
        }

        return el.GetRawText();
    }

    public readonly record struct WorkflowStepData(
        string StepType,
        string ActionType,
        string ConfigJson,
        string OnFailure,
        long RetryCount);

    public readonly record struct WorkflowData(
        string Name,
        string Description,
        string TriggerType,
        string TriggerConfigJson,
        long Active,
        long CreatedBy,
        IReadOnlyList<WorkflowStepData> Steps);

    /// <summary>PHP <c>json_decode((string)($_POST['workflow_data'] ?? '{}'), true) ?: array()</c>.</summary>
    public static WorkflowData ParseWorkflowData(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new("Untitled Workflow", "", "manual", "[]", 0, 0, Array.Empty<WorkflowStepData>());
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new("Untitled Workflow", "", "manual", "[]", 0, 0, Array.Empty<WorkflowStepData>());
            }

            var root = doc.RootElement;
            var name = root.TryGetProperty("name", out _)
                ? JsonString(root, "name")
                : "Untitled Workflow";
            var triggerType = root.TryGetProperty("trigger_type", out _)
                ? JsonString(root, "trigger_type")
                : "manual";
            var active = root.TryGetProperty("active", out var activeEl)
                        && activeEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? PhpIntval(activeEl)
                : 0;
            var createdBy = root.TryGetProperty("created_by", out var byEl)
                            && byEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? PhpIntval(byEl)
                : 0;
            return new(
                name,
                JsonString(root, "description"),
                triggerType,
                EncodeJsonField(root, "trigger_config"),
                active,
                createdBy,
                ParseSteps(root));
        }
        catch (JsonException)
        {
            return new("Untitled Workflow", "", "manual", "[]", 0, 0, Array.Empty<WorkflowStepData>());
        }
    }

    private static IReadOnlyList<WorkflowStepData> ParseSteps(JsonElement root)
    {
        if (!root.TryGetProperty("steps", out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WorkflowStepData>();
        }

        var steps = new List<WorkflowStepData>();
        foreach (var step in el.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object)
            {
                steps.Add(new("action", "", "[]", "stop", 0));
                continue;
            }

            var stepType = step.TryGetProperty("step_type", out _)
                ? JsonString(step, "step_type")
                : "action";
            var onFailure = step.TryGetProperty("on_failure", out _)
                ? JsonString(step, "on_failure")
                : "stop";
            var retry = step.TryGetProperty("retry_count", out var retryEl)
                        && retryEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                ? PhpIntval(retryEl)
                : 0;
            steps.Add(new(
                stepType,
                JsonString(step, "action_type"),
                EncodeJsonField(step, "config"),
                onFailure,
                retry));
        }

        return steps;
    }

    public async Task<ErpSimpleWriteResult> ToggleAsync(
        long workflowId,
        bool active,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_workflows` SET `active` = ? WHERE `id` = ?"),
                cancellationToken, active ? 1 : 0, workflowId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Workflow toggled", workflowId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Workflows table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long workflowId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `epc_workflow_steps` WHERE `workflow_id` = ?"),
                cancellationToken, workflowId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `epc_workflows` WHERE `id` = ?"),
                cancellationToken, workflowId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Workflow deleted", workflowId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Workflows table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        string? siteKey,
        string? workflowDataJson,
        CancellationToken cancellationToken = default)
    {
        var key = PhpBosSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        var parsed = ParseWorkflowData(workflowDataJson);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_workflows`
                        (`site_key`, `name`, `description`, `trigger_type`, `trigger_config`, `active`, `created_by`)
                    VALUES (?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                key,
                parsed.Name,
                parsed.Description,
                parsed.TriggerType,
                parsed.TriggerConfigJson,
                parsed.Active,
                parsed.CreatedBy).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            var order = 1;
            foreach (var step in parsed.Steps)
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        """
                        INSERT INTO `epc_workflow_steps`
                            (`workflow_id`, `step_order`, `step_type`, `action_type`, `config`, `on_failure`, `retry_count`)
                        VALUES (?, ?, ?, ?, ?, ?, ?)
                        """),
                    cancellationToken,
                    id,
                    order,
                    step.StepType,
                    step.ActionType,
                    step.ConfigJson,
                    step.OnFailure,
                    step.RetryCount).ConfigureAwait(false);
                order++;
            }

            return ErpSimpleWriteResult.Ok("Workflow created", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Workflows table is missing — schema-ensure stays Classic.");
        }
    }
}
