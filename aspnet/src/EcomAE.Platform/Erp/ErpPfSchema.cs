using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// PHP <c>epc_pf_ensure_schema</c> twin: additive CREATE TABLE IF NOT EXISTS for the process-flow tables plus the
/// staff-profile / case location columns. Errors are ignored like PHP's add-column helper.
/// </summary>
internal static class ErpPfSchema
{
    private static readonly string[] Tables =
    [
        "CREATE TABLE IF NOT EXISTS `epc_pf_processes` ("
        + " `id` int(11) NOT NULL AUTO_INCREMENT, `name` varchar(160) NOT NULL, `description` text,"
        + " `category` varchar(64) NOT NULL DEFAULT 'general', `active` tinyint(1) NOT NULL DEFAULT 1,"
        + " `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `x_active` (`active`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Process flow templates'",
        "CREATE TABLE IF NOT EXISTS `epc_pf_steps` ("
        + " `id` int(11) NOT NULL AUTO_INCREMENT, `process_id` int(11) NOT NULL, `step_no` int(11) NOT NULL DEFAULT 1,"
        + " `name` varchar(160) NOT NULL, `assign_type` varchar(16) NOT NULL DEFAULT 'dept_head',"
        + " `assign_user_id` int(11) NOT NULL DEFAULT 0, `assign_department` varchar(32) NOT NULL DEFAULT '',"
        + " `sla_hours` int(11) NOT NULL DEFAULT 24, `instructions` text, `time_created` int(11) NOT NULL DEFAULT 0,"
        + " PRIMARY KEY (`id`), KEY `x_proc` (`process_id`,`step_no`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Process step definitions'",
        "CREATE TABLE IF NOT EXISTS `epc_pf_dept_heads` ("
        + " `department_code` varchar(32) NOT NULL, `head_user_id` int(11) NOT NULL DEFAULT 0,"
        + " `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`department_code`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Department heads'",
        "CREATE TABLE IF NOT EXISTS `epc_pf_cases` ("
        + " `id` int(11) NOT NULL AUTO_INCREMENT, `process_id` int(11) NOT NULL, `title` varchar(255) NOT NULL,"
        + " `reference` varchar(120) NOT NULL DEFAULT '', `priority` enum('low','normal','high','urgent') NOT NULL DEFAULT 'normal',"
        + " `status` enum('open','done','cancelled','rejected') NOT NULL DEFAULT 'open', `current_step_no` int(11) NOT NULL DEFAULT 1,"
        + " `current_assignee_id` int(11) NOT NULL DEFAULT 0, `current_department` varchar(32) NOT NULL DEFAULT '',"
        + " `initiator_id` int(11) NOT NULL DEFAULT 0, `subject_type` varchar(40) NOT NULL DEFAULT '',"
        + " `subject_id` int(11) NOT NULL DEFAULT 0, `started_at` int(11) NOT NULL DEFAULT 0, `due_at` int(11) NOT NULL DEFAULT 0,"
        + " `completed_at` int(11) NOT NULL DEFAULT 0, `time_created` int(11) NOT NULL DEFAULT 0,"
        + " `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `x_status` (`status`),"
        + " KEY `x_assignee` (`current_assignee_id`,`status`), KEY `x_proc` (`process_id`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Process flow cases'",
        "CREATE TABLE IF NOT EXISTS `epc_pf_case_steps` ("
        + " `id` int(11) NOT NULL AUTO_INCREMENT, `case_id` int(11) NOT NULL, `step_no` int(11) NOT NULL,"
        + " `name` varchar(160) NOT NULL, `assign_type` varchar(16) NOT NULL DEFAULT 'dept_head',"
        + " `department` varchar(32) NOT NULL DEFAULT '', `assignee_id` int(11) NOT NULL DEFAULT 0,"
        + " `status` enum('pending','active','approved','rejected','skipped') NOT NULL DEFAULT 'pending', `comment` text,"
        + " `sla_due_at` int(11) NOT NULL DEFAULT 0, `activated_at` int(11) NOT NULL DEFAULT 0,"
        + " `completed_at` int(11) NOT NULL DEFAULT 0, `acted_by` int(11) NOT NULL DEFAULT 0,"
        + " `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), KEY `x_case` (`case_id`,`step_no`),"
        + " KEY `x_assignee` (`assignee_id`,`status`)"
        + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Per-case step trail'",
    ];

    private static readonly (string Table, string Column, string Definition)[] Columns =
    [
        ("epc_erp_staff_profiles", "location", "varchar(80) NOT NULL DEFAULT ''"),
        ("epc_erp_staff_profiles", "photo_url", "varchar(255) NOT NULL DEFAULT ''"),
        ("epc_erp_staff_profiles", "business_unit", "varchar(80) NOT NULL DEFAULT ''"),
        ("epc_erp_staff_profiles", "legal_entity", "varchar(120) NOT NULL DEFAULT ''"),
        ("epc_pf_cases", "current_location", "varchar(80) NOT NULL DEFAULT ''"),
        ("epc_pf_case_steps", "location", "varchar(80) NOT NULL DEFAULT ''"),
    ];

    internal static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var sql in Tables)
        {
            await ErpDb.TryExecuteAsync(connection, sql, cancellationToken).ConfigureAwait(false);
        }

        foreach (var (table, column, definition) in Columns)
        {
            try
            {
                if (!await ErpPfRouting.ColumnExistsAsync(connection, null, table, column, cancellationToken).ConfigureAwait(false))
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        "ALTER TABLE `" + table + "` ADD `" + column + "` " + definition,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (DbException)
            {
            }
        }
    }
}
