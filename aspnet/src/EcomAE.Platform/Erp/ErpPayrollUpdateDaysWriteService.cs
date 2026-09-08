using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_payroll_update_line_days</c> twin. Recalc one payroll
/// line and roll totals. Schema ensure stays PHP.
/// </summary>
public interface IErpPayrollUpdateDaysWriteService
{
    Task<ErpSimpleWriteResult> UpdateLineDaysAsync(
        long lineId,
        decimal daysWorked,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPayrollUpdateDaysWriteService : IErpPayrollUpdateDaysWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPayrollUpdateDaysWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> UpdateLineDaysAsync(
        long lineId,
        decimal daysWorked,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (lineId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cannot edit paid payroll line");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_payroll_lines", "monthly_basic", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_payroll_lines", "days_worked", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_payroll_runs", "standard_days", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_payroll_runs", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Payroll table is not provisioned");
        }

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var select = connection.CreateCommand();
        select.Transaction = tx;
        select.CommandText = ErpDb.Positional(
            "SELECT l.`run_id`, IFNULL(r.`status`,''), IFNULL(r.`standard_days`,0), IFNULL(l.`standard_days`,0),"
            + " IFNULL(l.`monthly_basic`,0), IFNULL(l.`basic_salary`,0),"
            + " IFNULL(l.`monthly_allowances`,0), IFNULL(l.`allowances`,0)"
            + " FROM `epc_erp_payroll_lines` l"
            + " INNER JOIN `epc_erp_payroll_runs` r ON r.`id` = l.`run_id`"
            + " WHERE l.`id`=? LIMIT 1");
        ErpDb.AddParameters(select, lineId);
        long runId = 0;
        string runStatus = "";
        int runStandardDays = 0;
        int lineStandardDays = 0;
        decimal monthlyBasic = 0;
        decimal basicSalary = 0;
        decimal monthlyAllowances = 0;
        decimal allowances = 0;
        var found = false;
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                found = true;
                runId = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                runStatus = reader.IsDBNull(1) ? "" : reader.GetString(1);
                runStandardDays = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture);
                lineStandardDays = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture);
                monthlyBasic = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4), CultureInfo.InvariantCulture);
                basicSalary = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture);
                monthlyAllowances = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture);
                allowances = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader.GetValue(7), CultureInfo.InvariantCulture);
            }
        }

        if (!found || string.Equals(runStatus, "paid", StringComparison.Ordinal))
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Cannot edit paid payroll line");
        }

        var standardDays = runStandardDays > 0 ? runStandardDays : lineStandardDays;
        var basic = monthlyBasic > 0 ? monthlyBasic : basicSalary;
        var allow = monthlyAllowances > 0 ? monthlyAllowances : allowances;
        var calc = Calc(basic, allow, daysWorked, standardDays);

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                "UPDATE `epc_erp_payroll_lines` SET"
                + " `days_worked`=?, `extra_days`=?, `daily_rate`=?,"
                + " `basic_salary`=?, `allowances`=?, `gross_pay`=?, `deductions`=?, `net_pay`=?"
                + " WHERE `id`=?"),
            cancellationToken,
            calc.DaysWorked,
            calc.ExtraDays,
            calc.DailyRate,
            calc.EarnedBasic,
            calc.EarnedAllowances,
            calc.GrossPay,
            calc.Deductions,
            calc.NetPay,
            lineId).ConfigureAwait(false);

        await RecalcTotalsAsync(connection, tx, runId, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

        var net = calc.NetPay.ToString("N2", CultureInfo.InvariantCulture);
        return ErpSimpleWriteResult.Ok("Days updated — net " + net + " AED", lineId);
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

    private static async Task RecalcTotalsAsync(
        DbConnection connection,
        DbTransaction transaction,
        long runId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
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
            transaction,
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
