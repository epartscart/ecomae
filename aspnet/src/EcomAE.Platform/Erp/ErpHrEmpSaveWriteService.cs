using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_hr_employee_save</c> twin. INSERT/UPDATE <c>epc_hr_employees</c>.
/// Schema ensure stays PHP. Does not CREATE or ALTER tables. Optional D365-depth
/// columns are written only when the request provided them and the live table
/// already has that column.
/// </summary>
public interface IErpHrEmpSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpHrEmpSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpHrEmpSaveWriteRequest(
    long Id = 0,
    string? Code = null,
    string? Name = null,
    string? Department = null,
    long BranchId = 0,
    string? JoinDate = null,
    string? JoinDateStr = null,
    decimal BasicSalary = 0,
    decimal Allowances = 0,
    string? Currency = null,
    decimal? AnnualLeaveDays = null,
    string? Status = null,
    IReadOnlyDictionary<string, string>? ProvidedExtras = null);

public sealed class ErpHrEmpSaveWriteService : IErpHrEmpSaveWriteService
{
    public static readonly string[] ExtraStringColumns =
    [
        "first_name", "last_name", "worker_type", "employment_type", "position_title",
        "job_title", "gender", "marital_status", "nationality", "personal_email",
        "work_email", "work_phone", "mobile", "address", "city", "country_code",
        "national_id", "passport_no", "visa_no", "emergency_contact", "emergency_phone",
        "bank_name", "bank_iban", "bank_account_no",
    ];

    public static readonly string[] ExtraIntColumns = ["legal_entity_id", "business_unit_id", "manager_id"];

    public static readonly string[] ExtraDateColumns = ["termination_date", "seniority_date", "date_of_birth", "visa_expiry"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpHrEmpSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpHrEmpSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.Code ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();
        var invalid = Validate(code, name);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        code = Clip(code, 40);
        name = Clip(name, 160);
        var department = Clip((request.Department ?? string.Empty).Trim(), 80);
        var currency = Clip((request.Currency ?? string.Empty).Trim(), 3);
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        var status = Clip((request.Status ?? string.Empty).Trim(), 12);
        if (status.Length == 0)
        {
            status = "active";
        }

        var branchId = request.BranchId < 0 ? 0 : request.BranchId;
        var basic = decimal.Round(request.BasicSalary, 2, MidpointRounding.AwayFromZero);
        var allowances = decimal.Round(request.Allowances, 2, MidpointRounding.AwayFromZero);
        var leaveDays = decimal.Round(request.AnnualLeaveDays ?? 30m, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var joinUnix = ResolveJoinUnix(request.JoinDate, request.JoinDateStr, now);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var columns = await LoadColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!columns.Contains("code") || !columns.Contains("name"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "HR employee table is not provisioned");
        }

        var extraCols = new List<string>();
        var extraVals = new List<object?>();
        AppendExtras(request.ProvidedExtras, columns, extraCols, extraVals);

        if (request.Id > 0)
        {
            var set = "`name`=?, `department`=?, `branch_id`=?, `basic_salary`=?, `allowances`=?, `currency`=?, `annual_leave_days`=?, `status`=?";
            var args = new List<object?> { name, department, branchId, basic, allowances, currency, leaveDays, status };
            for (var i = 0; i < extraCols.Count; i++)
            {
                set += ", `" + extraCols[i] + "`=?";
                args.Add(extraVals[i]);
            }

            args.Add(request.Id);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_hr_employees` SET " + set + " WHERE `id`=?"),
                cancellationToken,
                args.ToArray()).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Employee saved", request.Id);
        }

        var colList = new List<string>
        {
            "code", "name", "department", "branch_id", "join_date", "basic_salary",
            "allowances", "currency", "annual_leave_days", "status", "time_created",
        };
        var vals = new List<object?>
        {
            code, name, department, branchId, joinUnix, basic, allowances, currency, leaveDays, "active", now,
        };
        for (var i = 0; i < extraCols.Count; i++)
        {
            colList.Add(extraCols[i]);
            vals.Add(extraVals[i]);
        }

        var quoted = "`" + string.Join("`,`", colList) + "`";
        var placeholders = string.Join(",", Enumerable.Repeat("?", colList.Count));
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_hr_employees` (" + quoted + ") VALUES (" + placeholders + ")"),
            cancellationToken,
            vals.ToArray()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Employee saved", id);
    }

    public static string? Validate(string code, string name)
    {
        if (code.Length == 0 || name.Length == 0)
        {
            return "Code and name are required";
        }

        return null;
    }

    public static long ResolveJoinUnix(string? joinDate, string? joinDateStr, long now)
    {
        if (TryResolveUnix(joinDate, out var fromJoin))
        {
            return fromJoin;
        }

        if (TryResolveUnix(joinDateStr, out var fromStr))
        {
            return fromStr;
        }

        return now;
    }

    public static IReadOnlyDictionary<string, string> CollectProvidedExtras(
        IFormCollection? form,
        IReadOnlyDictionary<string, JsonElement>? jsonExtra)
    {
        var extras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in ExtraStringColumns.Concat(ExtraIntColumns).Concat(ExtraDateColumns))
        {
            if (form is not null && TryFormValue(form, col, out var formValue))
            {
                extras[col] = formValue;
                continue;
            }

            if (jsonExtra is not null && TryJsonValue(jsonExtra, col, out var jsonValue))
            {
                extras[col] = jsonValue;
            }
        }

        return extras;
    }

    private static void AppendExtras(
        IReadOnlyDictionary<string, string>? provided,
        HashSet<string> columns,
        List<string> extraCols,
        List<object?> extraVals)
    {
        if (provided is null || provided.Count == 0)
        {
            return;
        }

        foreach (var col in ExtraStringColumns)
        {
            if (!provided.ContainsKey(col) || !columns.Contains(col))
            {
                continue;
            }

            extraCols.Add(col);
            extraVals.Add(Clip(provided[col] ?? string.Empty, MaxLen(col)));
        }

        foreach (var col in ExtraIntColumns)
        {
            if (!provided.ContainsKey(col) || !columns.Contains(col))
            {
                continue;
            }

            extraCols.Add(col);
            extraVals.Add(ParseInt(provided[col]));
        }

        foreach (var col in ExtraDateColumns)
        {
            if (!provided.ContainsKey(col) || !columns.Contains(col))
            {
                continue;
            }

            extraCols.Add(col);
            extraVals.Add(ResolveExtraDateUnix(provided[col]));
        }
    }

    private static bool TryResolveUnix(string? raw, out long unix)
    {
        unix = 0;
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            unix = numeric < 0 ? 0 : numeric;
            return true;
        }

        var parsed = ErpHrLeaveRequestWriteService.ResolveDateUnix(text);
        unix = parsed > 0 ? parsed : 0;
        return parsed > 0;
    }

    public static long ResolveExtraDateUnix(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric < 0 ? 0 : numeric;
        }

        return ErpHrLeaveRequestWriteService.ResolveDateUnix(text);
    }

    private static bool TryFormValue(IFormCollection form, string column, out string value)
    {
        var camel = ToCamel(column);
        if (form.ContainsKey(column))
        {
            value = form[column].ToString();
            return true;
        }

        if (form.ContainsKey(camel))
        {
            value = form[camel].ToString();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryJsonValue(IReadOnlyDictionary<string, JsonElement> extra, string column, out string value)
    {
        var camel = ToCamel(column);
        if (extra.TryGetValue(column, out var el) || extra.TryGetValue(camel, out el))
        {
            value = el.ValueKind switch
            {
                JsonValueKind.String => el.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                JsonValueKind.Number => el.GetRawText(),
                JsonValueKind.True => "1",
                JsonValueKind.False => "0",
                _ => el.ToString(),
            };
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string ToCamel(string snake)
    {
        if (!snake.Contains('_', StringComparison.Ordinal))
        {
            return snake;
        }

        var parts = snake.Split('_');
        var chars = new char[snake.Length];
        var n = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0)
            {
                continue;
            }

            if (i == 0)
            {
                part.AsSpan().CopyTo(chars.AsSpan(n));
                n += part.Length;
            }
            else
            {
                chars[n++] = char.ToUpperInvariant(part[0]);
                if (part.Length > 1)
                {
                    part.AsSpan(1).CopyTo(chars.AsSpan(n));
                    n += part.Length - 1;
                }
            }
        }

        return new string(chars, 0, n);
    }

    private static long ParseInt(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0
            ? n
            : 0;
    }

    private static int MaxLen(string column) => column switch
    {
        "first_name" or "last_name" => 80,
        "worker_type" => 24,
        "employment_type" => 32,
        "position_title" or "job_title" => 120,
        "gender" => 12,
        "marital_status" => 16,
        "nationality" or "national_id" or "passport_no" or "visa_no" or "bank_iban" or "bank_account_no" => 64,
        "personal_email" or "work_email" => 128,
        "work_phone" or "mobile" or "emergency_phone" or "country_code" => column == "country_code" ? 8 : 40,
        "address" => 512,
        "city" => 128,
        "emergency_contact" or "bank_name" => 160,
        _ => 160,
    };

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

    private static async Task<HashSet<string>> LoadColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?");
        ErpDb.AddParameters(command, "epc_hr_employees");
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }
}
