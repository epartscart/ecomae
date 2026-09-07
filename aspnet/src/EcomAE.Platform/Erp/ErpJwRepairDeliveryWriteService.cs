using System.Data.Common;
using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_repair_delivery_save</c> twin.
/// Writes the provisioned <c>epc_jewel_repair_delivery</c> schema. Schema-ensure stays PHP.
/// </summary>
public interface IErpJwRepairDeliveryWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwRepairDeliverySaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwRepairDeliverySaveRequest(
    int CompanyId = 0,
    string? Branch = null,
    string? DeliveryDate = null,
    string? VocDate = null,
    int VocNo = 0,
    string? RepairNo = null,
    string? Code = null,
    string? CustomerCode = null,
    string? CustomerName = null,
    string? Mobile = null,
    decimal TotalCharge = 0,
    decimal AdvancePaid = 0,
    decimal BalanceDue = 0,
    string? PayMode = null,
    decimal AmountPaid = 0);

public sealed class ErpJwRepairDeliveryWriteService : IErpJwRepairDeliveryWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwRepairDeliveryWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwRepairDeliverySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var repairNo = FirstNonEmpty(request.RepairNo, request.Code);
        if (repairNo.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Repair number is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_repair_delivery", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery repair delivery tables are not provisioned");
        }

        var customerId = 0;
        if (int.TryParse((request.CustomerCode ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId) && parsedId > 0)
        {
            customerId = parsedId;
        }

        var remarks = BuildRemarks(repairNo, request.CustomerCode, request.PayMode, request.AdvancePaid, request.BalanceDue, request.AmountPaid);
        var charge = RoundNonNeg(request.TotalCharge);
        var paid = RoundNonNeg(request.AmountPaid);
        if (paid == 0)
        {
            paid = charge;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_repair_delivery` (`company_id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`customer_id`,`customer_name`,`mobile`,`delivery_remarks`,`repair_amount`,`sub_total`,`net_total`,`status`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            Clip(DefaultBranch(request.Branch), 10),
            "RTD",
            NormalizeDate(FirstNonEmpty(request.DeliveryDate, request.VocDate)),
            request.VocNo < 0 ? 0 : request.VocNo,
            customerId,
            Clip((request.CustomerName ?? string.Empty).Trim(), 120),
            Clip((request.Mobile ?? string.Empty).Trim(), 20),
            remarks,
            charge,
            charge,
            paid,
            "pending").ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Repair delivery saved", id);
    }

    private static string BuildRemarks(string repairNo, string? customerCode, string? payMode, decimal advance, decimal balance, decimal paid)
    {
        var sb = new StringBuilder("repair_no=").Append(repairNo);
        var code = (customerCode ?? string.Empty).Trim();
        if (code.Length > 0)
        {
            sb.Append(" | customer_code=").Append(code);
        }

        var mode = (payMode ?? string.Empty).Trim();
        if (mode.Length > 0)
        {
            sb.Append(" | pay_mode=").Append(mode);
        }

        if (advance != 0 || balance != 0 || paid != 0)
        {
            sb.Append(" | advance=").Append(advance.ToString("0.00", CultureInfo.InvariantCulture));
            sb.Append(" balance=").Append(balance.ToString("0.00", CultureInfo.InvariantCulture));
            sb.Append(" paid=").Append(paid.ToString("0.00", CultureInfo.InvariantCulture));
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

    private static decimal RoundNonNeg(decimal value)
        => decimal.Round(value < 0 ? 0 : value, 2, MidpointRounding.AwayFromZero);

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
