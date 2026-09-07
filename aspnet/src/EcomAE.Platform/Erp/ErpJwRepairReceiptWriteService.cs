using System.Data.Common;
using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_repair_save</c> / <c>jw_repair_receipt_save</c> twin.
/// Writes the provisioned <c>epc_jewel_repair</c> schema. Schema-ensure stays PHP.
/// </summary>
public interface IErpJwRepairReceiptWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwRepairReceiptSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwRepairReceiptSaveRequest(
    int CompanyId = 0,
    string? Branch = null,
    string? ReceiptDate = null,
    string? VocDate = null,
    int VocNo = 0,
    string? CustomerCode = null,
    string? CustomerName = null,
    string? Mobile = null,
    string? Salesman = null,
    string? PromiseDate = null,
    string? Priority = null,
    decimal TotalEstCost = 0,
    decimal AdvanceAmt = 0,
    string? Narration = null);

public sealed class ErpJwRepairReceiptWriteService : IErpJwRepairReceiptWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwRepairReceiptWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwRepairReceiptSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var customer = (request.CustomerName ?? string.Empty).Trim();
        if (customer.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Customer name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_repair", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery repair receipt tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var branch = Clip(DefaultBranch(request.Branch), 10);
        var vocDate = NormalizeDate(FirstNonEmpty(request.ReceiptDate, request.VocDate));
        var vocNo = request.VocNo < 0 ? 0 : request.VocNo;
        var customerId = 0;
        if (int.TryParse((request.CustomerCode ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId) && parsedId > 0)
        {
            customerId = parsedId;
        }

        var remarks = BuildRemarks(request.Narration, request.CustomerCode, request.Priority, request.TotalEstCost, request.AdvanceAmt);
        var delivery = NormalizeDateOrEmpty(request.PromiseDate);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_repair` (`company_id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`customer_id`,`customer_name`,`mobile`,`salesman`,`remarks`,`currency`,`delivery_date`,`repair_narration`,`status`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            companyId,
            branch,
            "REP",
            vocDate,
            vocNo,
            customerId,
            Clip(customer, 120),
            Clip((request.Mobile ?? string.Empty).Trim(), 20),
            Clip((request.Salesman ?? string.Empty).Trim(), 20),
            remarks,
            "AED",
            string.IsNullOrWhiteSpace(delivery) ? null : delivery,
            (request.Narration ?? string.Empty).Trim(),
            "received").ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Repair receipt saved", id);
    }

    private static string BuildRemarks(string? narration, string? customerCode, string? priority, decimal est, decimal advance)
    {
        var sb = new StringBuilder();
        var note = (narration ?? string.Empty).Trim();
        if (note.Length > 0) sb.Append(note);
        var code = (customerCode ?? string.Empty).Trim();
        if (code.Length > 0 && !int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("customer_code=").Append(code);
        }

        var prio = (priority ?? string.Empty).Trim();
        if (prio.Length > 0)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("priority=").Append(prio);
        }

        if (est != 0 || advance != 0)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("est=").Append(est.ToString("0.00", CultureInfo.InvariantCulture));
            sb.Append(" advance=").Append(advance.ToString("0.00", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    private static string DefaultBranch(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        return raw.Length == 0 ? "HO" : raw;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            var raw = (value ?? string.Empty).Trim();
            if (raw.Length > 0) return raw;
        }

        return string.Empty;
    }

    private static string NormalizeDate(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string NormalizeDateOrEmpty(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0) return string.Empty;
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return string.Empty;
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
