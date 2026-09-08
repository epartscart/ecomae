using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Shared PHP <c>epc_pf_resolve_assignee</c> / location / name helpers.
/// Does not CREATE tables. Schema ensure stays PHP.
/// </summary>
internal static class ErpPfRouting
{
    internal sealed record StepDef(
        long StepNo,
        string Name,
        string AssignType,
        long AssignUserId,
        string AssignDepartment,
        int SlaHours);

    internal static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

    internal static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }

    internal static async Task<IReadOnlyList<StepDef>> ProcessStepsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long processId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(
            "SELECT `step_no`,`name`,`assign_type`,`assign_user_id`,`assign_department`,`sla_hours` FROM `epc_pf_steps` WHERE `process_id` = ? ORDER BY `step_no`, `id`");
        ErpDb.AddParameters(command, processId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var steps = new List<StepDef>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            steps.Add(new StepDef(
                reader.IsDBNull(0) ? 0 : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(2) ? "dept_head" : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "dept_head",
                reader.IsDBNull(3) ? 0 : Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture),
                reader.IsDBNull(4) ? string.Empty : Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture)));
        }

        return steps;
    }

    internal static async Task<IReadOnlyDictionary<string, long>> DeptHeadsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var heads = new Dictionary<string, long>(StringComparer.Ordinal);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT `department_code`, `head_user_id` FROM `epc_pf_dept_heads`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var code = reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty;
                var userId = reader.IsDBNull(1) ? 0 : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
                if (code.Length > 0)
                {
                    heads[code] = userId;
                }
            }
        }
        catch (DbException)
        {
            // PHP try/catch around optional staff/dept tables.
        }

        return heads;
    }

    internal static async Task<long> ResolveAssigneeAsync(
        DbConnection connection,
        DbTransaction? transaction,
        StepDef step,
        long initiatorId,
        IReadOnlyDictionary<string, long> deptHeads,
        long fallbackUserId,
        CancellationToken cancellationToken)
    {
        var type = step.AssignType ?? "dept_head";
        var dept = step.AssignDepartment ?? string.Empty;
        if (string.Equals(type, "user", StringComparison.Ordinal) && step.AssignUserId > 0)
        {
            return step.AssignUserId;
        }

        if (string.Equals(type, "initiator", StringComparison.Ordinal) && initiatorId > 0)
        {
            return initiatorId;
        }

        if (string.Equals(type, "dept_head", StringComparison.Ordinal)
            && dept.Length > 0
            && deptHeads.TryGetValue(dept, out var head)
            && head > 0)
        {
            return head;
        }

        if (string.Equals(type, "department", StringComparison.Ordinal) && dept.Length > 0)
        {
            try
            {
                var picked = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "SELECT `user_id` FROM `epc_erp_staff_profiles` WHERE `department_code` = ? AND `active` = 1 ORDER BY RAND() LIMIT 1"),
                    cancellationToken,
                    dept).ConfigureAwait(false);
                if (picked > 0)
                {
                    return picked;
                }
            }
            catch (DbException)
            {
                // PHP try/catch: pick any available person, then fall through.
            }
        }

        if (dept.Length > 0 && deptHeads.TryGetValue(dept, out var fallbackHead) && fallbackHead > 0)
        {
            return fallbackHead;
        }

        if (initiatorId > 0)
        {
            return initiatorId;
        }

        return fallbackUserId;
    }

    internal static async Task<string> UserLocationAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long userId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return string.Empty;
        }

        try
        {
            return await ErpDb.StringAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `location` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? LIMIT 1"),
                cancellationToken,
                userId).ConfigureAwait(false) ?? string.Empty;
        }
        catch (DbException)
        {
            return string.Empty;
        }
    }

    internal static async Task<string> UserNameAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long userId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return "—";
        }

        try
        {
            var name = await ErpDb.StringAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `display_name` FROM `epc_erp_staff_profiles` WHERE `user_id` = ? LIMIT 1"),
                cancellationToken,
                userId).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }
        catch (DbException)
        {
            // PHP try/catch, then users.email.
        }

        try
        {
            var email = await ErpDb.StringAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `email` FROM `users` WHERE `user_id` = ? LIMIT 1"),
                cancellationToken,
                userId).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(email))
            {
                return email;
            }
        }
        catch (DbException)
        {
            // PHP try/catch.
        }

        return "User #" + userId.ToString(CultureInfo.InvariantCulture);
    }
}
