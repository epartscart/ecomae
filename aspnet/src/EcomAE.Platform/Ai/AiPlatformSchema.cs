using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Ai;

/// <summary>
/// Platform-level AI Management schema (registry DB — cross-tenant, never tenant data).
/// ai_* tables are created idempotently; tenant business records stay in tenant DBs and are
/// only touched through authorized services, never by agent SQL.
/// </summary>
public static class AiPlatformSchema
{
    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        foreach (var sql in Statements)
        {
            await ErpDb.TryExecuteAsync(connection, sql, cancellationToken).ConfigureAwait(false);
        }
    }

    private static readonly string[] Statements =
    [
        /// Agent templates: platform-owned, versioned — tenants get instances, never edit these.
        "CREATE TABLE IF NOT EXISTS `ai_agent_templates` ("
        + "`id` INT NOT NULL AUTO_INCREMENT,"
        + "`key` VARCHAR(64) NOT NULL,"
        + "`name` VARCHAR(190) NOT NULL,"
        + "`scope` VARCHAR(16) NOT NULL DEFAULT 'tenant',"
        + "`department` VARCHAR(48) NOT NULL DEFAULT 'general',"
        + "`instructions` TEXT NOT NULL,"
        + "`allowed_tools` TEXT NULL,"
        + "`risk_level` VARCHAR(16) NOT NULL DEFAULT 'read',"
        + "`model_pref` VARCHAR(48) NULL,"
        + "`version` INT NOT NULL DEFAULT 1,"
        + "`enabled` TINYINT NOT NULL DEFAULT 1,"
        + "`created_at` INT NOT NULL DEFAULT 0,"
        + "`updated_at` INT NOT NULL DEFAULT 0,"
        + "PRIMARY KEY(`id`), UNIQUE KEY `uq_ai_template_key` (`key`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        /// Tenant AI workspaces: one per tenant DB key.
        "CREATE TABLE IF NOT EXISTS `ai_tenant_workspaces` ("
        + "`id` INT NOT NULL AUTO_INCREMENT,"
        + "`tenant_key` VARCHAR(120) NOT NULL,"
        + "`display_name` VARCHAR(190) NOT NULL DEFAULT '',"
        + "`plan` VARCHAR(24) NOT NULL DEFAULT 'starter',"
        + "`status` VARCHAR(16) NOT NULL DEFAULT 'active',"
        + "`settings_json` TEXT NULL,"
        + "`daily_token_cap` INT NOT NULL DEFAULT 2000000,"
        + "`monthly_cost_cap_usd` DECIMAL(10,2) NOT NULL DEFAULT 50.00,"
        + "`created_at` INT NOT NULL DEFAULT 0,"
        + "`updated_at` INT NOT NULL DEFAULT 0,"
        + "PRIMARY KEY(`id`), UNIQUE KEY `uq_ai_ws_tenant` (`tenant_key`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        /// Provisioned agent instances (per workspace or platform scope when tenant_key='platform').
        "CREATE TABLE IF NOT EXISTS `ai_agents` ("
        + "`id` INT NOT NULL AUTO_INCREMENT,"
        + "`workspace_id` INT NOT NULL,"
        + "`template_key` VARCHAR(64) NOT NULL,"
        + "`name` VARCHAR(190) NOT NULL,"
        + "`status` VARCHAR(16) NOT NULL DEFAULT 'active',"
        + "`config_json` TEXT NULL,"
        + "`schedule_cron` VARCHAR(64) NULL,"
        + "`is_manager` TINYINT NOT NULL DEFAULT 0,"
        + "`created_at` INT NOT NULL DEFAULT 0,"
        + "`updated_at` INT NOT NULL DEFAULT 0,"
        + "PRIMARY KEY(`id`), KEY `ix_ai_agents_ws` (`workspace_id`), KEY `ix_ai_agents_tpl` (`template_key`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        /// Every agent run — the billable, auditable unit.
        "CREATE TABLE IF NOT EXISTS `ai_agent_executions` ("
        + "`id` BIGINT NOT NULL AUTO_INCREMENT,"
        + "`agent_id` INT NOT NULL,"
        + "`workspace_id` INT NOT NULL,"
        + "`trigger` VARCHAR(24) NOT NULL DEFAULT 'manual',"
        + "`status` VARCHAR(24) NOT NULL DEFAULT 'running',"
        + "`input_summary` TEXT NULL,"
        + "`output_summary` TEXT NULL,"
        + "`model` VARCHAR(48) NOT NULL DEFAULT '',"
        + "`tokens_in` INT NOT NULL DEFAULT 0,"
        + "`tokens_out` INT NOT NULL DEFAULT 0,"
        + "`cost_usd` DECIMAL(12,6) NOT NULL DEFAULT 0,"
        + "`correlation_id` VARCHAR(48) NOT NULL DEFAULT '',"
        + "`started_at` INT NOT NULL DEFAULT 0,"
        + "`finished_at` INT NOT NULL DEFAULT 0,"
        + "`error` TEXT NULL,"
        + "PRIMARY KEY(`id`), KEY `ix_ai_exec_agent` (`agent_id`), KEY `ix_ai_exec_ws` (`workspace_id`),"
        + " KEY `ix_ai_exec_started` (`started_at`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        /// Human approvals bound to an exact proposed action + payload hash.
        "CREATE TABLE IF NOT EXISTS `ai_approvals` ("
        + "`id` BIGINT NOT NULL AUTO_INCREMENT,"
        + "`workspace_id` INT NOT NULL,"
        + "`agent_id` INT NOT NULL,"
        + "`action` VARCHAR(120) NOT NULL,"
        + "`payload_json` TEXT NOT NULL,"
        + "`payload_hash` VARCHAR(64) NOT NULL,"
        + "`risk` VARCHAR(16) NOT NULL DEFAULT 'medium',"
        + "`reason` TEXT NULL,"
        + "`status` VARCHAR(16) NOT NULL DEFAULT 'pending',"
        + "`decided_by` VARCHAR(190) NULL,"
        + "`decided_at` INT NOT NULL DEFAULT 0,"
        + "`executed_at` INT NOT NULL DEFAULT 0,"
        + "`result` TEXT NULL,"
        + "`created_at` INT NOT NULL DEFAULT 0,"
        + "PRIMARY KEY(`id`), KEY `ix_ai_appr_ws` (`workspace_id`,`status`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        /// Immutable audit trail — every tool call, approval decision, suspension.
        "CREATE TABLE IF NOT EXISTS `ai_audit_logs` ("
        + "`id` BIGINT NOT NULL AUTO_INCREMENT,"
        + "`workspace_id` INT NOT NULL,"
        + "`agent_id` INT NULL,"
        + "`event` VARCHAR(64) NOT NULL,"
        + "`detail` TEXT NULL,"
        + "`actor` VARCHAR(190) NOT NULL DEFAULT 'system',"
        + "`correlation_id` VARCHAR(48) NOT NULL DEFAULT '',"
        + "`created_at` INT NOT NULL DEFAULT 0,"
        + "PRIMARY KEY(`id`), KEY `ix_ai_audit_ws` (`workspace_id`,`created_at`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        /// Generated reports (daily/weekly/monthly per workspace and platform).
        "CREATE TABLE IF NOT EXISTS `ai_reports` ("
        + "`id` BIGINT NOT NULL AUTO_INCREMENT,"
        + "`workspace_id` INT NOT NULL,"
        + "`kind` VARCHAR(16) NOT NULL DEFAULT 'daily',"
        + "`title` VARCHAR(190) NOT NULL DEFAULT '',"
        + "`body` MEDIUMTEXT NULL,"
        + "`metrics_json` TEXT NULL,"
        + "`created_at` INT NOT NULL DEFAULT 0,"
        + "PRIMARY KEY(`id`), KEY `ix_ai_reports_ws` (`workspace_id`,`kind`,`created_at`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        /// Tenant knowledge documents (metadata only; tenant keys keep docs tenant-scoped).
        "CREATE TABLE IF NOT EXISTS `ai_knowledge_documents` ("
        + "`id` BIGINT NOT NULL AUTO_INCREMENT,"
        + "`workspace_id` INT NOT NULL,"
        + "`title` VARCHAR(190) NOT NULL,"
        + "`kind` VARCHAR(32) NOT NULL DEFAULT 'policy',"
        + "`body` MEDIUMTEXT NULL,"
        + "`created_at` INT NOT NULL DEFAULT 0,"
        + "PRIMARY KEY(`id`), KEY `ix_ai_know_ws` (`workspace_id`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
        // Race-safe uniqueness for concurrent first provisions (no-op once applied).
        "ALTER TABLE `ai_agents` ADD UNIQUE KEY `uq_ai_agents_ws_tpl` (`workspace_id`,`template_key`)",
        // Speak layer: user<->agent chat, agent<->agent, and tenant->platform escalation (audited).
        "CREATE TABLE IF NOT EXISTS `ai_agent_messages` ("
        + "`id` BIGINT NOT NULL AUTO_INCREMENT,"
        + "`workspace_id` INT NOT NULL,"
        + "`direction` VARCHAR(16) NOT NULL DEFAULT 'inbound'," // inbound=user->agent, outbound=agent->user, internal=agent->agent, escalate=tenant->platform
        + "`from_agent_id` BIGINT NULL," // NULL = human user
        + "`from_actor` VARCHAR(120) NOT NULL DEFAULT '',"
        + "`to_agent_id` BIGINT NULL," // NULL = human inbox / platform manager
        + "`body` TEXT NOT NULL,"
        + "`reply_to_id` BIGINT NULL,"
        + "`status` VARCHAR(16) NOT NULL DEFAULT 'open'," // open/answered/closed
        + "`correlation_id` VARCHAR(64) NOT NULL DEFAULT '',"
        + "`created_at` INT NOT NULL DEFAULT 0,"
        + "PRIMARY KEY(`id`), KEY `ix_ai_msg_ws` (`workspace_id`,`status`,`id`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
    ];
}
