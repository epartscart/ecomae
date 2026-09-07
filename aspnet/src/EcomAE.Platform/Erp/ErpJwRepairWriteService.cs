using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_repair_create</c> / <c>jw_repair_update_status</c> twin.
/// Schema-ensure and sample seed stay PHP — missing <c>epc_erp_jw_repairs</c> refuses instead of CREATE.
/// </summary>
public interface IErpJwRepairWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpJwRepairSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetStatusAsync(long repairId, string? status, CancellationToken cancellationToken = default);
}

public sealed record ErpJwRepairSaveRequest(
    string? RepairNo = null,
    long CustomerId = 0,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? ItemDescription = null,
    string? MetalType = null,
    string? Karat = null,
    decimal GrossWtIn = 0,
    decimal NetWtIn = 0,
    string? StoneDetails = null,
    string? RepairType = null,
    decimal EstimatedCost = 0,
    long ReceivedDate = 0,
    long PromisedDate = 0,
    int AdminId = 0);

public sealed class ErpJwRepairWriteService : IErpJwRepairWriteService
{
    internal static readonly string[] AllowedStatuses =
    [
        "received",
        "in_progress",
        "ready",
        "delivered",
        "invoiced",
    ];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwRepairWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpJwRepairSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var customerName = (request.CustomerName ?? string.Empty).Trim();
        var itemDescription = (request.ItemDescription ?? string.Empty).Trim();
        if (customerName.Length == 0 || itemDescription.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Customer name and item description are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_erp_jw_repairs", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery repair tables are not provisioned");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var postedNo = (request.RepairNo ?? string.Empty).Trim();
        var repairNo = postedNo.Length > 0
            ? Clip(postedNo, 32)
            : "RPR-" + Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(now.ToString(CultureInfo.InvariantCulture))))[..6];
        var customerId = request.CustomerId < 0 ? 0 : request.CustomerId;
        var receivedDate = request.ReceivedDate > 0 ? request.ReceivedDate : now;
        var promisedDate = request.PromisedDate < 0 ? 0 : request.PromisedDate;
        var adminId = request.AdminId < 0 ? 0 : request.AdminId;

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_jw_repairs` (`repair_no`,`customer_id`,`customer_name`,`customer_phone`,`item_description`,`metal_type`,`karat`,`gross_wt_in`,`net_wt_in`,`stone_details`,`repair_type`,`estimated_cost`,`status`,`received_date`,`promised_date`,`admin_id`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'received', ?, ?, ?, ?)"),
            cancellationToken,
            repairNo,
            customerId,
            Clip(customerName, 255),
            Clip((request.CustomerPhone ?? string.Empty).Trim(), 30),
            Clip(itemDescription, 255),
            Clip((request.MetalType ?? string.Empty).Trim(), 16),
            Clip((request.Karat ?? string.Empty).Trim(), 10),
            decimal.Round(request.GrossWtIn < 0 ? 0 : request.GrossWtIn, 3, MidpointRounding.AwayFromZero),
            decimal.Round(request.NetWtIn < 0 ? 0 : request.NetWtIn, 3, MidpointRounding.AwayFromZero),
            Clip((request.StoneDetails ?? string.Empty).Trim(), 4000),
            Clip((request.RepairType ?? string.Empty).Trim(), 60),
            decimal.Round(request.EstimatedCost < 0 ? 0 : request.EstimatedCost, 2, MidpointRounding.AwayFromZero),
            receivedDate,
            promisedDate,
            adminId,
            now).ConfigureAwait(false);

        var repairId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (repairId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Repair " + repairNo + " created", repairId);
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        long repairId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (repairId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid parameters");
        }

        var next = (status ?? string.Empty).Trim();
        if (next.Length == 0 || !AllowedStatuses.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", next.Length == 0 ? "Invalid parameters" : "Invalid status");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_jw_repairs` SET `status` = ?, `updated_at` = ? WHERE `id` = ?"),
            cancellationToken,
            next, updatedAt, repairId);
        return ErpSimpleWriteResult.Ok("Status updated to " + next, repairId);
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
