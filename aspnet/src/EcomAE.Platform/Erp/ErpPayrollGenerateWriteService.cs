using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_payroll_generate_run</c> twin. INSERT/rebuild a draft
/// payroll run and lines from active staff HR records. Schema ensure stays PHP.
/// </summary>
public interface IErpPayrollGenerateWriteService
{
    Task<ErpSimpleWriteResult> GenerateAsync(
        string? periodLabel,
        long createdBy = 0,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPayrollGenerateWriteService : IErpPayrollGenerateWriteService
{
    private static readonly Regex PeriodLabelPattern = new(@"^\d{4}-\d{2}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public ErpPayrollGenerateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> GenerateAsync(
        string? periodLabel,
        long createdBy = 0,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var label = NormalizePeriodLabel(periodLabel);
        if (label is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid period (use YYYY-MM)");
        }

        var bounds = PeriodBoundsUnix(label);
        const int standardDays = 30;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_payroll_runs", "period_label", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_payroll_runs", "standard_days", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_payroll_lines", "monthly_basic", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_staff_profiles", "active", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_hr_records", "basic_salary", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Payroll table is not provisioned");
        }

        await using var existingCmd = connection.CreateCommand();
        existingCmd.CommandText = ErpDb.Positional(
            "SELECT `id`, IFNULL(`status`,'') FROM `epc_erp_payroll_runs` WHERE `period_label`=? LIMIT 1");
        ErpDb.AddParameters(existingCmd, label);
        long runId = 0;
        string existingStatus = "";
        await using (var reader = await existingCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                runId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                existingStatus = reader.IsDBNull(1) ? "" : reader.GetString(1);
            }
        }

        if (runId > 0 && string.Equals(existingStatus, "paid", StringComparison.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Payroll for " + label + " is already paid");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (runId > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("DELETE FROM `epc_erp_payroll_lines` WHERE `run_id`=?"),
                cancellationToken,
                runId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_erp_payroll_runs` SET `period_start`=?, `period_end`=?, `standard_days`=?, `status`='draft', `total_gross`=0, `total_deductions`=0, `total_net`=0, `cash_entry_id`=0, `paid_at`=0 WHERE `id`=?"),
                cancellationToken,
                bounds.Start,
                bounds.End,
                standardDays,
                runId).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_payroll_runs` (`period_label`,`period_start`,`period_end`,`standard_days`,`status`,`created_by`,`time_created`) VALUES (?,?,?,?,'draft',?,?)"),
                cancellationToken,
                label,
                bounds.Start,
                bounds.End,
                standardDays,
                createdBy,
                now).ConfigureAwait(false);
            runId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }

        await using var staffCmd = connection.CreateCommand();
        staffCmd.CommandText =
            "SELECT p.`id` AS profile_id, p.`user_id`, p.`department_code`, p.`display_name`, p.`job_title`,"
            + " h.`basic_salary`, h.`allowances`, h.`bank_account`, h.`bank_name`, h.`days_worked`"
            + " FROM `epc_erp_staff_profiles` p"
            + " INNER JOIN `epc_erp_hr_records` h ON h.`staff_profile_id` = p.`id`"
            + " WHERE p.`active` = 1"
            + " ORDER BY p.`department_code`, p.`display_name`";
        await using var staffReader = await staffCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var staff = new List<StaffRow>();
        while (await staffReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            staff.Add(new StaffRow(
                Convert.ToInt64(staffReader.GetValue(0), CultureInfo.InvariantCulture),
                staffReader.IsDBNull(1) ? 0L : Convert.ToInt64(staffReader.GetValue(1), CultureInfo.InvariantCulture),
                staffReader.IsDBNull(2) ? "" : Convert.ToString(staffReader.GetValue(2), CultureInfo.InvariantCulture) ?? "",
                staffReader.IsDBNull(3) ? "" : Convert.ToString(staffReader.GetValue(3), CultureInfo.InvariantCulture) ?? "",
                staffReader.IsDBNull(4) ? "" : Convert.ToString(staffReader.GetValue(4), CultureInfo.InvariantCulture) ?? "",
                staffReader.IsDBNull(5) ? 0m : Convert.ToDecimal(staffReader.GetValue(5), CultureInfo.InvariantCulture),
                staffReader.IsDBNull(6) ? 0m : Convert.ToDecimal(staffReader.GetValue(6), CultureInfo.InvariantCulture),
                staffReader.IsDBNull(7) ? "" : Convert.ToString(staffReader.GetValue(7), CultureInfo.InvariantCulture) ?? "",
                staffReader.IsDBNull(8) ? "" : Convert.ToString(staffReader.GetValue(8), CultureInfo.InvariantCulture) ?? "",
                staffReader.IsDBNull(9) ? 0m : Convert.ToDecimal(staffReader.GetValue(9), CultureInfo.InvariantCulture)));
        }

        await staffReader.DisposeAsync().ConfigureAwait(false);

        foreach (var row in staff)
        {
            var daysWorked = row.DaysWorked > 0 ? row.DaysWorked : standardDays;
            var calc = Calc(row.BasicSalary, row.Allowances, daysWorked, standardDays);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_payroll_lines`"
                    + " (`run_id`,`staff_profile_id`,`user_id`,`department_code`,`display_name`,`job_title`,"
                    + " `monthly_basic`,`monthly_allowances`,`standard_days`,`days_worked`,`extra_days`,`daily_rate`,"
                    + " `basic_salary`,`allowances`,`gross_pay`,`deductions`,`net_pay`,`bank_account`,`bank_name`,`status`,`time_created`)"
                    + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,'draft',?)"),
                cancellationToken,
                runId,
                row.ProfileId,
                row.UserId,
                row.DepartmentCode,
                row.DisplayName,
                row.JobTitle,
                calc.MonthlyBasic,
                calc.MonthlyAllowances,
                calc.StandardDays,
                calc.DaysWorked,
                calc.ExtraDays,
                calc.DailyRate,
                calc.EarnedBasic,
                calc.EarnedAllowances,
                calc.GrossPay,
                calc.Deductions,
                calc.NetPay,
                row.BankAccount,
                row.BankName,
                now).ConfigureAwait(false);
        }

        await RecalcTotalsAsync(connection, runId, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Payroll generated for " + label, runId);
    }

    public static string? NormalizePeriodLabel(string? periodLabel)
    {
        var raw = string.IsNullOrWhiteSpace(periodLabel)
            ? DateTimeOffset.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : periodLabel.Trim();
        return PeriodLabelPattern.IsMatch(raw) ? raw : null;
    }

    public static (long Start, long End) PeriodBoundsUnix(string label)
    {
        var year = int.Parse(label.AsSpan(0, 4), CultureInfo.InvariantCulture);
        var month = int.Parse(label.AsSpan(5, 2), CultureInfo.InvariantCulture);
        var start = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        var lastDay = DateTime.DaysInMonth(year, month);
        var end = new DateTimeOffset(year, month, lastDay, 23, 59, 59, TimeSpan.Zero);
        return (start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds());
    }

    public static PayrollCalc Calc(decimal monthlyBasic, decimal monthlyAllowances, decimal daysWorked, int standardDays)
    {
        standardDays = Math.Max(1, standardDays);
        daysWorked = Math.Max(0m, decimal.Round(daysWorked, 1, MidpointRounding.AwayFromZero));
        monthlyBasic = decimal.Round(monthlyBasic, 2, MidpointRounding.AwayFromZero);
        monthlyAllowances = decimal.Round(monthlyAllowances, 2, MidpointRounding.AwayFromZero);
        var monthlyTotal = monthlyBasic + monthlyAllowances;
        var dailyTotal = monthlyTotal / standardDays;
        var dailyBasic = monthlyBasic / standardDays;
        var dailyAllow = monthlyAllowances / standardDays;
        var earnedBasic = decimal.Round(dailyBasic * daysWorked, 2, MidpointRounding.AwayFromZero);
        var earnedAllow = decimal.Round(dailyAllow * daysWorked, 2, MidpointRounding.AwayFromZero);
        var gross = decimal.Round(earnedBasic + earnedAllow, 2, MidpointRounding.AwayFromZero);
        var ded = decimal.Round(gross * 0m, 2, MidpointRounding.AwayFromZero);
        return new PayrollCalc(
            standardDays,
            daysWorked,
            decimal.Round(Math.Max(0m, daysWorked - standardDays), 1, MidpointRounding.AwayFromZero),
            decimal.Round(dailyTotal, 4, MidpointRounding.AwayFromZero),
            monthlyBasic,
            monthlyAllowances,
            earnedBasic,
            earnedAllow,
            gross,
            ded,
            decimal.Round(gross - ded, 2, MidpointRounding.AwayFromZero));
    }

    public sealed record PayrollCalc(
        int StandardDays,
        decimal DaysWorked,
        decimal ExtraDays,
        decimal DailyRate,
        decimal MonthlyBasic,
        decimal MonthlyAllowances,
        decimal EarnedBasic,
        decimal EarnedAllowances,
        decimal GrossPay,
        decimal Deductions,
        decimal NetPay);

    private sealed record StaffRow(
        long ProfileId,
        long UserId,
        string DepartmentCode,
        string DisplayName,
        string JobTitle,
        decimal BasicSalary,
        decimal Allowances,
        string BankAccount,
        string BankName,
        decimal DaysWorked);

    private static async Task RecalcTotalsAsync(DbConnection connection, long runId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT IFNULL(SUM(COALESCE(NULLIF(`gross_pay`, 0), `basic_salary` + `allowances`)), 0),"
            + " IFNULL(SUM(`deductions`), 0), IFNULL(SUM(`net_pay`), 0) FROM `epc_erp_payroll_lines` WHERE `run_id`=?");
        ErpDb.AddParameters(cmd, runId);
        decimal gross = 0, ded = 0, net = 0;
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                gross = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0), CultureInfo.InvariantCulture);
                ded = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
                net = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
            }
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_payroll_runs` SET `total_gross`=?, `total_deductions`=?, `total_net`=? WHERE `id`=?"),
            cancellationToken,
            gross,
            ded,
            net,
            runId).ConfigureAwait(false);
    }

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
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
