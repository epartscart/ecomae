using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hr_payroll_run</c> twin. Drafts <c>epc_hr_payroll_runs</c>
/// and replaces <c>epc_hr_payslips</c> for active <c>epc_hr_employees</c>.
/// Schema ensure and the compute-only journal stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpHrPayrollRunWriteService
{
    Task<ErpSimpleWriteResult> GenerateAsync(
        ErpHrPayrollRunWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpHrPayrollDeduction(string Label, decimal Amount);

public sealed record ErpHrPayrollRunWriteRequest(
    string? Period = null,
    IReadOnlyDictionary<long, IReadOnlyList<ErpHrPayrollDeduction>>? DeductionsByEmp = null);

public sealed class ErpHrPayrollRunWriteService : IErpHrPayrollRunWriteService
{
    private static readonly Regex PeriodPattern = new(
        @"^\d{4}-(0[1-9]|1[0-2])$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrPayrollRunWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> GenerateAsync(
        ErpHrPayrollRunWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var period = NormalizePeriod(request.Period);
        if (period is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid period (use YYYY-MM)");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var deductions = request.DeductionsByEmp ?? new Dictionary<long, IReadOnlyList<ErpHrPayrollDeduction>>();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_hr_payroll_runs", "period", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_hr_payroll_runs", "gross_total", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_hr_payslips", "basic", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_hr_employees", "basic_salary", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_hr_employees", "allowances", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_hr_employees", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "HR payroll table is not provisioned");
        }

        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                "INSERT INTO `epc_hr_payroll_runs` (`period`,`status`,`time_created`) VALUES (?, 'draft', ?)"
                + " ON DUPLICATE KEY UPDATE `status`='draft'"),
            cancellationToken,
            period,
            now).ConfigureAwait(false);

        var runId = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `id` FROM `epc_hr_payroll_runs` WHERE `period`=? LIMIT 1"),
            cancellationToken,
            period).ConfigureAwait(false);
        if (runId <= 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "HR payroll table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("DELETE FROM `epc_hr_payslips` WHERE `run_id`=?"),
            cancellationToken,
            runId).ConfigureAwait(false);

        var employees = new List<(long Id, decimal Basic, decimal Allowances)>();
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = tx;
            select.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`basic_salary`,0), IFNULL(`allowances`,0) FROM `epc_hr_employees` WHERE `status`='active'");
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                if (id <= 0)
                {
                    continue;
                }

                var basic = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture);
                var allowances = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
                employees.Add((id, basic, allowances));
            }
        }

        decimal grossTotal = 0;
        decimal deductionTotal = 0;
        decimal netTotal = 0;
        foreach (var emp in employees)
        {
            deductions.TryGetValue(emp.Id, out var empDeductions);
            var slip = ComputePayslip(emp.Basic, emp.Allowances, empDeductions);
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional(
                    "INSERT INTO `epc_hr_payslips` (`run_id`,`employee_id`,`basic`,`allowances`,`gross`,`deductions`,`net`,`detail`) VALUES (?,?,?,?,?,?,?,?)"),
                cancellationToken,
                runId,
                emp.Id,
                slip.Basic,
                slip.Allowances,
                slip.Gross,
                slip.Deductions,
                slip.Net,
                JsonSerializer.Serialize(slip.DeductionDetail.Select(d => new { label = d.Label, amount = d.Amount }))).ConfigureAwait(false);
            grossTotal = decimal.Round(grossTotal + slip.Gross, 2, MidpointRounding.AwayFromZero);
            deductionTotal = decimal.Round(deductionTotal + slip.Deductions, 2, MidpointRounding.AwayFromZero);
            netTotal = decimal.Round(netTotal + slip.Net, 2, MidpointRounding.AwayFromZero);
        }

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional(
                "UPDATE `epc_hr_payroll_runs` SET `gross_total`=?, `deduction_total`=?, `net_total`=? WHERE `id`=?"),
            cancellationToken,
            grossTotal,
            deductionTotal,
            netTotal,
            runId).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(FormatGeneratedMessage(period, employees.Count, netTotal), runId);
    }

    public static string? NormalizePeriod(string? raw)
    {
        var period = (raw ?? string.Empty).Trim();
        if (period.Length == 0)
        {
            return DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        return PeriodPattern.IsMatch(period) ? period : null;
    }

    public static string FormatGeneratedMessage(string period, int employees, decimal net)
        => "HR payroll generated for " + period
           + " — " + employees.ToString(CultureInfo.InvariantCulture) + " employees, net "
           + net.ToString("#,0.00", CultureInfo.InvariantCulture) + " AED";

    public static ErpHrPayslip ComputePayslip(
        decimal basic,
        decimal allowances,
        IEnumerable<ErpHrPayrollDeduction>? deductions)
    {
        var roundedBasic = decimal.Round(basic, 2, MidpointRounding.AwayFromZero);
        var roundedAllow = decimal.Round(allowances, 2, MidpointRounding.AwayFromZero);
        var gross = decimal.Round(roundedBasic + roundedAllow, 2, MidpointRounding.AwayFromZero);
        var detail = new List<ErpHrPayrollDeduction>();
        var ded = 0m;
        if (deductions is not null)
        {
            foreach (var item in deductions)
            {
                var amount = decimal.Round(item.Amount, 2, MidpointRounding.AwayFromZero);
                ded = decimal.Round(ded + amount, 2, MidpointRounding.AwayFromZero);
                detail.Add(new ErpHrPayrollDeduction(item.Label ?? string.Empty, amount));
            }
        }

        return new ErpHrPayslip(
            roundedBasic,
            roundedAllow,
            gross,
            ded,
            decimal.Round(gross - ded, 2, MidpointRounding.AwayFromZero),
            detail);
    }

    public static IReadOnlyDictionary<long, IReadOnlyList<ErpHrPayrollDeduction>> ParseDeductions(
        string? deductionsJson,
        long employeeId,
        string? label,
        decimal amount)
    {
        var map = new Dictionary<long, List<ErpHrPayrollDeduction>>();
        if (!string.IsNullOrWhiteSpace(deductionsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(deductionsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (!long.TryParse(prop.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var empId)
                            || empId <= 0
                            || prop.Value.ValueKind != JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (var row in ReadDeductionArray(prop.Value))
                        {
                            AddDeduction(map, empId, row);
                        }
                    }
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var empId = 0L;
                        if (item.TryGetProperty("employee_id", out var empEl)
                            || item.TryGetProperty("employeeId", out empEl))
                        {
                            if (empEl.ValueKind == JsonValueKind.Number && empEl.TryGetInt64(out var n))
                            {
                                empId = n;
                            }
                            else if (empEl.ValueKind == JsonValueKind.String
                                     && long.TryParse(empEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                            {
                                empId = parsed;
                            }
                        }

                        if (empId <= 0)
                        {
                            continue;
                        }

                        AddDeduction(map, empId, ReadDeductionObject(item));
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to the single form-line deduction.
            }
        }

        if (map.Count == 0 && employeeId > 0 && amount != 0)
        {
            AddDeduction(map, employeeId, new ErpHrPayrollDeduction(label ?? string.Empty, amount));
        }

        return map.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ErpHrPayrollDeduction>)pair.Value);
    }

    public sealed record ErpHrPayslip(
        decimal Basic,
        decimal Allowances,
        decimal Gross,
        decimal Deductions,
        decimal Net,
        IReadOnlyList<ErpHrPayrollDeduction> DeductionDetail);

    private static IEnumerable<ErpHrPayrollDeduction> ReadDeductionArray(JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                yield return ReadDeductionObject(item);
            }
        }
    }

    private static ErpHrPayrollDeduction ReadDeductionObject(JsonElement item)
    {
        var label = "";
        if (item.TryGetProperty("label", out var lab) && lab.ValueKind == JsonValueKind.String)
        {
            label = lab.GetString() ?? "";
        }

        var amount = 0m;
        if (item.TryGetProperty("amount", out var amt) && amt.TryGetDecimal(out var dec))
        {
            amount = dec;
        }

        return new ErpHrPayrollDeduction(label, amount);
    }

    private static void AddDeduction(
        Dictionary<long, List<ErpHrPayrollDeduction>> map,
        long employeeId,
        ErpHrPayrollDeduction row)
    {
        if (!map.TryGetValue(employeeId, out var list))
        {
            list = [];
            map[employeeId] = list;
        }

        list.Add(row);
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
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
