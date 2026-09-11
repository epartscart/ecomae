using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>wps_payroll</c> <c>approve_run</c> / <c>epc_payroll_approve_run</c>
/// and <c>employee_add</c> / <c>epc_payroll_employee_add</c>.
/// Create-run, generate-sif, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosPayrollWriteService
{
    Task<ErpSimpleWriteResult> ApproveRunAsync(
        long runId,
        long approverId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddEmployeeAsync(
        string? siteKey,
        string? employeeDataJson,
        CancellationToken cancellationToken = default);
}

public sealed class BosPayrollWriteService : IBosPayrollWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosPayrollWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    /// <summary>PHP <c>date('Y-m-d')</c> for omitted <c>join_date</c>.</summary>
    public static string DefaultJoinDate(DateTime? now = null)
        => (now ?? DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>PHP <c>(float)</c> on a leading numeric token.</summary>
    public static decimal PhpFloat(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return 0;
        }

        var text = raw.TrimStart();
        if (text.Length == 0)
        {
            return 0;
        }

        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        var sawDigit = false;
        while (i < text.Length && char.IsDigit(text[i]))
        {
            sawDigit = true;
            i++;
        }

        if (i < text.Length && text[i] == '.')
        {
            i++;
            while (i < text.Length && char.IsDigit(text[i]))
            {
                sawDigit = true;
                i++;
            }
        }

        if (!sawDigit)
        {
            return 0;
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            var exp = i + 1;
            if (exp < text.Length && text[exp] is '+' or '-')
            {
                exp++;
            }

            var expDigits = exp;
            while (expDigits < text.Length && char.IsDigit(text[expDigits]))
            {
                expDigits++;
            }

            if (expDigits > exp)
            {
                i = expDigits;
            }
        }

        return decimal.TryParse(text[..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static decimal PhpFloat(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.Number when element.TryGetInt64(out var n) => n,
            JsonValueKind.String => PhpFloat(element.GetString()),
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

    /// <summary>PHP <c>json_decode((string)($_POST['employee_data'] ?? '{}'), true) ?: array()</c>.</summary>
    public static (
        string EmployeeId,
        string FullName,
        string LabourCardNo,
        string MolId,
        string BankCode,
        string Iban,
        string BankName,
        decimal BasicSalary,
        decimal Housing,
        decimal Transport,
        decimal OtherAllowance,
        string Currency,
        string Department,
        string Designation,
        string JoinDate) ParseEmployeeData(string? raw, DateTime? now = null)
    {
        var today = DefaultJoinDate(now);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ("", "", "", "", "", "", "", 0, 0, 0, 0, "AED", "", "", today);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ("", "", "", "", "", "", "", 0, 0, 0, 0, "AED", "", "", today);
            }

            var root = doc.RootElement;
            var currency = root.TryGetProperty("currency", out _)
                ? JsonString(root, "currency")
                : "AED";
            var joinDate = root.TryGetProperty("join_date", out _)
                ? JsonString(root, "join_date")
                : today;
            return (
                JsonString(root, "employee_id"),
                JsonString(root, "full_name"),
                JsonString(root, "labour_card_no"),
                JsonString(root, "mol_id"),
                JsonString(root, "bank_code"),
                JsonString(root, "iban"),
                JsonString(root, "bank_name"),
                root.TryGetProperty("basic_salary", out var salEl) && salEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? PhpFloat(salEl) : 0,
                root.TryGetProperty("housing", out var houseEl) && houseEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? PhpFloat(houseEl) : 0,
                root.TryGetProperty("transport", out var transEl) && transEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? PhpFloat(transEl) : 0,
                root.TryGetProperty("other_allowance", out var otherEl) && otherEl.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined ? PhpFloat(otherEl) : 0,
                currency,
                JsonString(root, "department"),
                JsonString(root, "designation"),
                joinDate);
        }
        catch (JsonException)
        {
            return ("", "", "", "", "", "", "", 0, 0, 0, 0, "AED", "", "", today);
        }
    }

    public async Task<ErpSimpleWriteResult> ApproveRunAsync(
        long runId,
        long approverId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var written = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_payroll_runs` SET `status` = 'approved', `approved_by` = ?, `approved_at` = NOW() WHERE `id` = ? AND `status` = 'draft'
                    """),
                cancellationToken, approverId, runId).ConfigureAwait(false);
            if (written <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Payroll run was not approved");
            }

            return ErpSimpleWriteResult.Ok("Payroll run approved", runId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Payroll runs table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> AddEmployeeAsync(
        string? siteKey,
        string? employeeDataJson,
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

        var parsed = ParseEmployeeData(employeeDataJson);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_payroll_employees`
                        (`site_key`, `employee_id`, `full_name`, `labour_card_no`, `mol_id`,
                         `bank_code`, `iban`, `bank_name`, `basic_salary`, `housing`, `transport`,
                         `other_allowance`, `currency`, `department`, `designation`, `join_date`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                key,
                parsed.EmployeeId,
                parsed.FullName,
                parsed.LabourCardNo,
                parsed.MolId,
                parsed.BankCode,
                parsed.Iban,
                parsed.BankName,
                parsed.BasicSalary,
                parsed.Housing,
                parsed.Transport,
                parsed.OtherAllowance,
                parsed.Currency,
                parsed.Department,
                parsed.Designation,
                parsed.JoinDate).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Payroll employee added", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Payroll employees table is missing — schema-ensure stays Classic.");
        }
    }
}
