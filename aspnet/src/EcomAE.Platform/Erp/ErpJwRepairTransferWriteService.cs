using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_repair_transfer_save</c> twin.
/// Writes the provisioned <c>epc_jewel_repair_transfer</c> schema. Schema-ensure stays PHP.
/// </summary>
public interface IErpJwRepairTransferWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwRepairTransferSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwRepairTransferSaveRequest(
    int CompanyId = 0,
    string? FromBranch = null,
    string? Branch = null,
    string? ToBranch = null,
    string? TransferDate = null,
    string? VocDate = null,
    int VocNo = 0,
    string? RepairNo = null,
    string? Code = null,
    string? WorkshopContact = null,
    string? ExpectedReturn = null,
    string? Narration = null);

public sealed class ErpJwRepairTransferWriteService : IErpJwRepairTransferWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwRepairTransferWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwRepairTransferSaveRequest request,
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
        if (!await TableExistsAsync(connection, "epc_jewel_repair_transfer", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery repair transfer tables are not provisioned");
        }

        var from = Clip(DefaultBranch(request.FromBranch), 10);
        var to = Clip(FirstNonEmpty(request.ToBranch, request.Branch, "WS1"), 20);
        var remarks = (request.Narration ?? string.Empty).Trim();
        if (repairNo.Length > 0)
        {
            remarks = remarks.Length == 0 ? "repair_no=" + repairNo : remarks + " | repair_no=" + repairNo;
        }

        var expected = (request.ExpectedReturn ?? string.Empty).Trim();
        if (expected.Length > 0)
        {
            remarks = remarks.Length == 0 ? "expected_return=" + expected : remarks + " | expected_return=" + expected;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_repair_transfer` (`company_id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`salesman`,`branch_to`,`party_code`,`party_name`,`division`,`transfer_remarks`,`status`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            from,
            "RET",
            NormalizeDate(FirstNonEmpty(request.TransferDate, request.VocDate)),
            request.VocNo < 0 ? 0 : request.VocNo,
            Clip((request.WorkshopContact ?? string.Empty).Trim(), 20),
            to,
            Clip(repairNo, 20),
            Clip((request.WorkshopContact ?? string.Empty).Trim(), 120),
            "",
            remarks,
            "pending").ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Repair transfer saved", id);
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
