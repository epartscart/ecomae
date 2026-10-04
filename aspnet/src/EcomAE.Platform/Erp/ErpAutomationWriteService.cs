using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_automation_catalogue.php</c> twins for the ajax cases
/// <c>automation_activate</c>, <c>automation_install_template</c>,
/// <c>automation_enable_category</c> and <c>automation_tick</c>.
/// Settings land on <c>epc_price_settings</c> (<c>erp_auto_{setting_key}</c>), template installs and
/// scheduled runs go through the workflow tables via <see cref="IErpWorkflowWriteService"/>, and the
/// collections_dunning automation advances <c>epc_dunning_queue</c>/<c>epc_dunning_log</c> exactly as
/// <c>epc_dunning_process</c>.
/// </summary>
public interface IErpAutomationWriteService
{
    Task<ErpAutomationActivateResult> ActivateAsync(string? id, int userId, CancellationToken cancellationToken = default);

    Task<ErpAutomationInstallResult> InstallTemplateAsync(string? templateId, int userId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> EnableCategoryAsync(string? category, int userId, CancellationToken cancellationToken = default);

    Task<ErpAutomationTickResult> TickAsync(int userId, CancellationToken cancellationToken = default);
}

public sealed record ErpAutomationActivateResult(bool Ok, string Message, long WorkflowId);

public sealed record ErpAutomationInstallResult(bool Ok, string? Error, long WorkflowId, bool Created);

public sealed record ErpAutomationTickEntry(
    long WorkflowId,
    string? Name,
    string? Automation,
    ErpWorkflowRunResult? Result,
    string? Error,
    int? Processed,
    int? Actioned);

public sealed record ErpAutomationTickResult(bool Ok, int Ran, IReadOnlyList<ErpAutomationTickEntry> Results);

public sealed class ErpAutomationWriteService : IErpAutomationWriteService
{
    /// <summary>PHP catalogue id → (<c>category</c>, <c>workflow_template</c>, <c>default_on</c>).</summary>
    public sealed record ErpAutomationCatalogueEntry(string Category, string WorkflowTemplate, bool DefaultOn);

    /// <summary>PHP <c>epc_erp_automation_catalogue()</c> metadata needed by the write paths (22 items).</summary>
    public static readonly IReadOnlyDictionary<string, ErpAutomationCatalogueEntry> Catalogue =
        new Dictionary<string, ErpAutomationCatalogueEntry>(StringComparer.Ordinal)
        {
            ["order_to_erp"] = new("accounting", "", true),
            ["period_close"] = new("accounting", "", true),
            ["year_end_close"] = new("accounting", "", true),
            ["bank_recon"] = new("accounting", "", true),
            ["collections_dunning"] = new("accounting", "", true),
            ["report_scheduler"] = new("accounting", "", true),
            ["vat_reminder"] = new("accounting", "vat_filing_reminder", false),
            ["gl_auto_post"] = new("accounting", "", true),
            ["payment_reminder"] = new("accounting", "ap_payment_due", false),
            ["depreciation_run"] = new("accounting", "", true),
            ["po_approval"] = new("process", "po_approval_chain", true),
            ["invoice_autosend"] = new("process", "invoice_auto_send", false),
            ["low_stock_alert"] = new("process", "low_stock_alert", false),
            ["employee_onboarding"] = new("process", "employee_onboarding", false),
            ["daily_sales_summary"] = new("process", "daily_sales_summary", false),
            ["aml_alert"] = new("process", "aml_compliance_alert", true),
            ["process_flow_routing"] = new("process", "", true),
            ["three_way_match"] = new("process", "", true),
            ["subscription_billing"] = new("process", "", true),
            ["rma_warranty"] = new("process", "", true),
            ["credit_check"] = new("process", "credit_limit_gate", true),
            ["goods_receipt_notify"] = new("process", "grn_notify", false),
        };

    /// <summary>PHP <c>epc_erp_automation_workflow_templates()</c> (12 installable templates).</summary>
    public static readonly IReadOnlyDictionary<string, ErpWorkflowSaveInput> WorkflowTemplates =
        BuildTemplates();

    /// <summary>PHP <c>epc_dunning_default_steps()</c>.</summary>
    public static readonly (int Day, string Action, string Subject)[] DunningDefaultSteps =
    [
        (1, "email", "Friendly Payment Reminder"),
        (7, "email", "Payment Notice — Invoice Overdue"),
        (14, "email", "Second Payment Notice — Urgent"),
        (21, "call", "Phone Follow-Up Required"),
        (30, "letter", "Formal Demand for Payment"),
        (45, "escalation", "Account Escalated to Collections"),
        (60, "letter", "Final Notice Before Legal Action"),
    ];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpWorkflowWriteService _workflows;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<ErpAutomationWriteService>? _logger;

    public ErpAutomationWriteService(
        IErpWriteConnectionFactory connections,
        IErpWorkflowWriteService workflows,
        IHttpContextAccessor? httpContextAccessor = null,
        ILogger<ErpAutomationWriteService>? logger = null)
    {
        _connections = connections;
        _workflows = workflows;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<ErpAutomationActivateResult> ActivateAsync(string? id, int userId, CancellationToken cancellationToken = default)
    {
        var autoId = (id ?? string.Empty).Trim();
        if (!TryResolveCatalogueEntry(autoId, out var entry, out var settingKey))
        {
            return new ErpAutomationActivateResult(false, "Unknown automation", 0);
        }

        if (!_connections.IsConfigured)
        {
            return new ErpAutomationActivateResult(false, "TenantRegistry DB is not configured.", 0);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSettingsSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await SetEnabledAsync(connection, settingKey, true, cancellationToken).ConfigureAwait(false);

        var workflowId = 0L;
        if (entry.WorkflowTemplate.Length > 0)
        {
            var installed = await InstallTemplateOnConnectionAsync(connection, entry.WorkflowTemplate, userId, cancellationToken).ConfigureAwait(false);
            if (installed.Ok)
            {
                workflowId = installed.WorkflowId;
            }
        }

        return new ErpAutomationActivateResult(true, "Automation enabled", workflowId);
    }

    public async Task<ErpAutomationInstallResult> InstallTemplateAsync(string? templateId, int userId, CancellationToken cancellationToken = default)
    {
        var tplId = (templateId ?? string.Empty).Trim();
        if (!WorkflowTemplates.ContainsKey(tplId))
        {
            return new ErpAutomationInstallResult(false, "Unknown template: " + tplId, 0, false);
        }

        if (!_connections.IsConfigured)
        {
            return new ErpAutomationInstallResult(false, "TenantRegistry DB is not configured.", 0, false);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await InstallTemplateOnConnectionAsync(connection, tplId, userId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ErpAutomationInstallResult> InstallTemplateOnConnectionAsync(
        DbConnection connection,
        string templateId,
        int userId,
        CancellationToken cancellationToken)
    {
        var template = WorkflowTemplates[templateId];
        var siteKey = ResolveSiteKey();
        await ErpWorkflowWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        // Idempotent by name (PHP strcasecmp over epc_workflow_list for the site).
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT `id`, `name` FROM `epc_workflows` WHERE `site_key` = ?");
            ErpDb.AddParameters(command, siteKey);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(1)
                    && string.Equals(reader.GetString(1), template.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var existingId = reader.GetInt64(0);
                    return new ErpAutomationInstallResult(true, null, existingId, false);
                }
            }
        }

        var created = await _workflows.CreateAsync(
            new ErpWorkflowSaveInput(0, template.Name, template.Description, template.TriggerType, template.TriggerConfigJson, true, template.Steps),
            userId,
            cancellationToken).ConfigureAwait(false);
        return new ErpAutomationInstallResult(created.Ok, created.Ok ? null : created.Message, created.WorkflowId, created.Ok);
    }

    public async Task<ErpSimpleWriteResult> EnableCategoryAsync(string? category, int userId, CancellationToken cancellationToken = default)
    {
        var cat = (category ?? string.Empty).Trim();
        if (cat.Length == 0)
        {
            cat = "accounting";
        }

        var enabled = 0;
        foreach (var (id, entry) in Catalogue)
        {
            if (!string.Equals(entry.Category, cat, StringComparison.Ordinal))
            {
                continue;
            }

            var result = await ActivateAsync(id, userId, cancellationToken).ConfigureAwait(false);
            if (result.Ok)
            {
                enabled++;
            }
        }

        return ErpSimpleWriteResult.Ok(enabled.ToString(CultureInfo.InvariantCulture) + " automations enabled", enabled);
    }

    public async Task<ErpAutomationTickResult> TickAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new ErpAutomationTickResult(false, 0, []);
        }

        var siteKey = ResolveSiteKey();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpWorkflowWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var scheduled = new List<(long Id, string Name, JsonObject TriggerConfig, string? LastRunAt)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`, `name`, `trigger_config`, `last_run_at` FROM `epc_workflows` WHERE `site_key` = ? AND `active` = 1 AND `trigger_type` = 'schedule'");
            ErpDb.AddParameters(command, siteKey);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var cfgText = reader.IsDBNull(2) ? null : reader.GetString(2);
                scheduled.Add((
                    reader.GetInt64(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    ParseConfig(cfgText),
                    reader.IsDBNull(3) ? null : Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture)));
            }
        }

        var ran = 0;
        var results = new List<ErpAutomationTickEntry>();
        foreach (var workflow in scheduled)
        {
            if (!ScheduleDue(workflow.TriggerConfig, workflow.LastRunAt))
            {
                continue;
            }

            var triggerData = new JsonObject
            {
                ["source"] = "schedule_tick",
                ["site_key"] = siteKey,
            };
            var run = await _workflows.RunAsync(workflow.Id, userId, triggerData, cancellationToken).ConfigureAwait(false);
            results.Add(new ErpAutomationTickEntry(workflow.Id, workflow.Name, null, run, null, null, null));
            ran++;
        }

        // PHP also advances collections/dunning when that automation is enabled (wrapped in try/catch).
        if (await IsEnabledAsync(connection, "collections_dunning", cancellationToken).ConfigureAwait(false))
        {
            try
            {
                var (processed, actioned) = await DunningProcessAsync(connection, siteKey, cancellationToken).ConfigureAwait(false);
                results.Add(new ErpAutomationTickEntry(0, null, "collections_dunning", null, null, processed, actioned));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogWarning(ex, "automation_tick dunning process failed");
                results.Add(new ErpAutomationTickEntry(0, null, "collections_dunning", null, ex.Message, null, null));
            }
        }

        return new ErpAutomationTickResult(true, ran, results);
    }

    /// <summary>PHP <c>epc_erp_automation_schedule_due()</c>: cron hour gate + same-day check (server-local time).</summary>
    public static bool ScheduleDue(JsonObject? triggerConfig, string? lastRunAt, DateTime? now = null)
    {
        var cron = "0 9 * * *";
        if (triggerConfig is not null
            && triggerConfig["cron_expression"] is JsonValue cronValue
            && cronValue.TryGetValue<string>(out var cronText)
            && !string.IsNullOrWhiteSpace(cronText))
        {
            cron = cronText.Trim();
        }

        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hour = 9;
        if (parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedHour))
        {
            hour = parsedHour;
        }

        var current = now ?? DateTime.Now;
        if (current.Hour < hour)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(lastRunAt))
        {
            return true;
        }

        if (!DateTime.TryParse(lastRunAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var last))
        {
            return true;
        }

        return last.Date != current.Date;
    }

    /// <summary>PHP <c>epc_dunning_process()</c>: age open queue items and advance their current step.</summary>
    private static async Task<(int Processed, int Actioned)> DunningProcessAsync(
        DbConnection connection,
        string siteKey,
        CancellationToken cancellationToken)
    {
        await EnsureDunningSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var items = new List<(long Id, DateTime DueDate, int CurrentStep, IReadOnlyList<(int Day, string Action, string Subject)> Steps)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT q.`id`, q.`due_date`, q.`dunning_step`, p.`steps` FROM `epc_dunning_queue` q LEFT JOIN `epc_dunning_profiles` p ON q.`profile_id` = p.`id` WHERE q.`site_key` = ? AND q.`status` IN ('open', 'in_progress')");
            ErpDb.AddParameters(command, siteKey);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var stepsJson = reader.IsDBNull(3) ? null : reader.GetString(3);
                items.Add((
                    reader.GetInt64(0),
                    reader.GetDateTime(1),
                    reader.GetInt32(2),
                    ParseDunningSteps(stepsJson)));
            }
        }

        var actioned = 0;
        var today = DateTime.Now.Date;
        foreach (var item in items)
        {
            var steps = item.Steps.Count > 0 ? item.Steps : DunningDefaultSteps;
            var itemDays = Math.Max(0, (int)((today - item.DueDate.Date).TotalDays));
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_dunning_queue` SET `days_overdue` = ? WHERE `id` = ?"),
                cancellationToken,
                itemDays, item.Id).ConfigureAwait(false);

            var currentStep = item.CurrentStep;
            if (currentStep < steps.Count && itemDays >= steps[currentStep].Day)
            {
                var step = steps[currentStep];
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `epc_dunning_queue` SET `dunning_step` = ?, `status` = 'in_progress' WHERE `id` = ?"),
                    cancellationToken,
                    currentStep + 1, item.Id).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("INSERT INTO `epc_dunning_log` (`queue_id`, `action_type`, `details`) VALUES (?, ?, ?)"),
                    cancellationToken,
                    item.Id, step.Action,
                    "Step " + (currentStep + 1).ToString(CultureInfo.InvariantCulture) + ": " + step.Subject).ConfigureAwait(false);
                actioned++;
            }
        }

        return (items.Count, actioned);
    }

    private static IReadOnlyList<(int Day, string Action, string Subject)> ParseDunningSteps(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonArray array)
            {
                return [];
            }

            var steps = new List<(int, string, string)>();
            foreach (var item in array)
            {
                if (item is not JsonObject obj)
                {
                    continue;
                }

                var day = obj["day"]?.GetValue<int>() ?? 0;
                var action = obj["action"]?.GetValue<string>() ?? "email";
                var subject = obj["subject"]?.GetValue<string>() ?? "";
                steps.Add((day, action, subject));
            }

            return steps;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<bool> IsEnabledAsync(DbConnection connection, string id, CancellationToken cancellationToken)
    {
        if (!TryResolveCatalogueEntry(id, out var entry, out var settingKey))
        {
            return false;
        }

        var value = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1"),
            cancellationToken,
            settingKey).ConfigureAwait(false);
        if (value is null)
        {
            return entry.DefaultOn;
        }

        var normalized = value.Trim();
        return string.Equals(normalized, "1", StringComparison.Ordinal)
            || string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task SetEnabledAsync(DbConnection connection, string settingKey, bool on, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`) VALUES (?, ?) ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)"),
            cancellationToken,
            settingKey,
            on ? "1" : "0").ConfigureAwait(false);
    }

    private bool TryResolveCatalogueEntry(string id, out ErpAutomationCatalogueEntry entry, out string settingKey)
    {
        if (Catalogue.TryGetValue(id, out entry!))
        {
            settingKey = "erp_auto_" + (ErpAutomationDeactivateWriteService.CatalogueSettingKeys.TryGetValue(id, out var key) ? key : id);
            return true;
        }

        settingKey = string.Empty;
        return false;
    }

    private string ResolveSiteKey()
    {
        var tenant = _httpContextAccessor?.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var siteKey = tenant?.SiteKey ?? string.Empty;
        return new string(siteKey.ToLowerInvariant().Where(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_').ToArray());
    }

    private static JsonObject ParseConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private static async Task EnsureSettingsSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_price_settings` (`setting_key` varchar(128) NOT NULL, `setting_value` text, PRIMARY KEY (`setting_key`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureDunningSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_dunning_profiles` (
                `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `site_key`        VARCHAR(64)    NOT NULL,
                `name`            VARCHAR(128)   NOT NULL,
                `steps`           JSON           NOT NULL,
                `active`          TINYINT(1)     NOT NULL DEFAULT 1,
                `created_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX `idx_site` (`site_key`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """,
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_dunning_queue` (
                `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `site_key`        VARCHAR(64)    NOT NULL,
                `customer_id`     INT UNSIGNED   NOT NULL,
                `customer_name`   VARCHAR(128)   NOT NULL DEFAULT '',
                `invoice_ref`     VARCHAR(64)    NOT NULL,
                `invoice_amount`  DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
                `amount_due`      DECIMAL(14,2)  NOT NULL DEFAULT 0.00,
                `due_date`        DATE           NOT NULL,
                `days_overdue`    INT            NOT NULL DEFAULT 0,
                `dunning_step`    TINYINT UNSIGNED NOT NULL DEFAULT 0,
                `profile_id`      INT UNSIGNED   NULL,
                `status`          ENUM('open','in_progress','promised','partial','paid','written_off','disputed') NOT NULL DEFAULT 'open',
                `next_action_date`DATE           NULL,
                `assigned_to`     INT UNSIGNED   NOT NULL DEFAULT 0,
                `notes`           TEXT           NULL,
                `updated_at`      DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX `idx_site_status` (`site_key`, `status`),
                INDEX `idx_overdue` (`days_overdue`),
                INDEX `idx_next_action` (`next_action_date`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """,
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_dunning_log` (
                `id`              BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `queue_id`        BIGINT UNSIGNED NOT NULL,
                `action_type`     ENUM('email','sms','call','letter','escalation','note','payment','write_off') NOT NULL,
                `details`         TEXT           NOT NULL,
                `performed_by`    INT UNSIGNED   NOT NULL DEFAULT 0,
                `performed_at`    DATETIME       NOT NULL DEFAULT CURRENT_TIMESTAMP,
                INDEX `idx_queue` (`queue_id`),
                INDEX `idx_type` (`action_type`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """,
            cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<string, ErpWorkflowSaveInput> BuildTemplates()
    {
        static ErpWorkflowStepInput Step(string type, string action, string label, string config)
            => new(type, action, label, config, null, 0);

        static ErpWorkflowSaveInput Tpl(string name, string desc, string trigger, string triggerConfig, params ErpWorkflowStepInput[] steps)
            => new(0, name, desc, trigger, triggerConfig, true, steps);

        return new Dictionary<string, ErpWorkflowSaveInput>(StringComparer.Ordinal)
        {
            ["po_approval_chain"] = Tpl(
                "PO Approval Chain",
                "Route POs through manager → finance → director by amount",
                "event",
                """{"event_type":"po.created"}""",
                Step("condition", "", "Under auto-approve threshold?", """{"field":"total","operator":"<","value":500}"""),
                Step("action", "update_status", "Auto-approve", """{"new_status":"approved"}"""),
                Step("action", "send_notification", "Notify requester", """{"title":"PO Auto-Approved","message":"PO #{{po_number}} auto-approved"}""")),
            ["invoice_auto_send"] = Tpl(
                "Invoice Auto-Send",
                "Email invoice when order completes",
                "event",
                """{"event_type":"invoice.posted"}""",
                Step("action", "send_email", "Email customer", """{"to":"{{customer_email}}","subject":"Invoice #{{invoice_number}}","body":"Please find your invoice attached."}"""),
                Step("action", "send_notification", "Log send", """{"title":"Invoice sent","message":"Invoice #{{invoice_number}} emailed"}""")),
            ["low_stock_alert"] = Tpl(
                "Low Stock Alert",
                "Notify procurement below reorder point",
                "event",
                """{"event_type":"stock.below"}""",
                Step("action", "send_notification", "Alert procurement", """{"title":"Low stock","message":"{{sku}} below ROP"}"""),
                Step("action", "create_task", "Reorder task", """{"title":"Reorder {{sku}}","assignee":"procurement","due_days":2}""")),
            ["vat_filing_reminder"] = Tpl(
                "VAT Filing Reminder",
                "Remind 7 days before VAT deadline",
                "schedule",
                """{"cron_expression":"0 9 * * 1","timezone":"Asia/Dubai"}""",
                Step("action", "send_notification", "VAT reminder", """{"title":"VAT filing due","message":"VAT return deadline approaching"}"""),
                Step("action", "send_email", "Email tax owner", """{"to":"{{tax_email}}","subject":"VAT filing reminder","body":"Please prepare the VAT return."}""")),
            ["overdue_escalation"] = Tpl(
                "Overdue Invoice Escalation",
                "Dunning sequence at 30/60/90 days",
                "schedule",
                """{"cron_expression":"0 10 * * *","timezone":"Asia/Dubai"}""",
                Step("condition", "", "Days overdue ≥ 7", """{"field":"days_overdue","operator":">=","value":7}"""),
                Step("action", "send_email", "Payment reminder", """{"to":"{{customer_email}}","subject":"Payment reminder","body":"Invoice #{{invoice_number}} is overdue"}"""),
                Step("action", "send_notification", "Collections queue", """{"title":"Overdue escalation","message":"Invoice #{{invoice_number}} escalated"}""")),
            ["employee_onboarding"] = Tpl(
                "Employee Onboarding",
                "Onboarding tasks for new hires",
                "event",
                """{"event_type":"employee.created"}""",
                Step("action", "create_task", "IT account", """{"title":"Provision accounts for {{employee_name}}","assignee":"it","due_days":1}"""),
                Step("action", "create_task", "HR docs", """{"title":"Collect onboarding docs","assignee":"hr","due_days":3}"""),
                Step("action", "send_notification", "Notify manager", """{"title":"New hire","message":"{{employee_name}} onboarding started"}""")),
            ["daily_sales_summary"] = Tpl(
                "Daily Sales Summary",
                "Email daily sales to management",
                "schedule",
                """{"cron_expression":"0 18 * * *","timezone":"Asia/Dubai"}""",
                Step("action", "send_email", "Email summary", """{"to":"{{mgmt_email}}","subject":"Daily sales summary","body":"Sales for today are ready in the ERP dashboard."}""")),
            ["aml_compliance_alert"] = Tpl(
                "AML Compliance Alert",
                "Flag transactions above AML threshold",
                "event",
                """{"event_type":"payment.posted"}""",
                Step("condition", "", "Above AML threshold", """{"field":"amount","operator":">=","value":55000}"""),
                Step("action", "send_notification", "Flag compliance", """{"title":"AML review required","message":"Payment {{payment_id}} exceeds threshold"}"""),
                Step("action", "create_task", "Compliance case", """{"title":"AML review {{payment_id}}","assignee":"compliance","due_days":1}""")),
            ["ap_payment_due"] = Tpl(
                "AP Payment Due Reminder",
                "Alert treasury before supplier due dates",
                "schedule",
                """{"cron_expression":"0 8 * * *","timezone":"Asia/Dubai"}""",
                Step("action", "send_notification", "Treasury alert", """{"title":"AP payments due","message":"Supplier invoices approaching due date"}""")),
            ["credit_limit_gate"] = Tpl(
                "Credit Limit Gate",
                "Hold orders that exceed credit limit",
                "event",
                """{"event_type":"order.placed"}""",
                Step("action", "credit_check", "Credit check", """{"action_on_exceed":"hold"}"""),
                Step("action", "send_notification", "Notify credit", """{"title":"Credit hold","message":"Order #{{order_number}} held for credit review"}""")),
            ["grn_notify"] = Tpl(
                "Goods Receipt Notify",
                "Notify buyer and AP on GRN",
                "event",
                """{"event_type":"grn.posted"}""",
                Step("action", "send_notification", "Notify buyer", """{"title":"GRN posted","message":"Goods received for PO #{{po_number}}"}""")),
            ["order_confirmation"] = Tpl(
                "Order Confirmation Email",
                "Confirm to customer when order is placed",
                "event",
                """{"event_type":"order.placed"}""",
                Step("action", "send_email", "Confirm email", """{"to":"{{customer_email}}","subject":"Order Confirmed #{{order_number}}","body":"Thank you for your order!"}"""),
                Step("action", "gl_journal", "Optional memo", """{"debit_account":"1100","credit_account":"4000","amount":0}""")),
        };
    }
}
