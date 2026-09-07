using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_workshop_receive_save</c> twin.
/// Writes the provisioned <c>epc_jewel_repair_workshop_receive</c> schema. Schema-ensure stays PHP.
/// </summary>
public interface IErpJwWorkshopReceiveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwWorkshopReceiveSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwWorkshopReceiveSaveRequest(
    int CompanyId = 0,
    string? Branch = null,
    string? ReceiveDate = null,
    string? VocDate = null,
    int VocNo = 0,
    string? TransferRef = null,
    string? Code = null,
    string? FromWorkshop = null,
    string? Narration = null);

public sealed class ErpJwWorkshopReceiveWriteService : IErpJwWorkshopReceiveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwWorkshopReceiveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwWorkshopReceiveSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var transferRef = FirstNonEmpty(request.TransferRef, request.Code);
        if (transferRef.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Transfer reference is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_repair_workshop_receive", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery workshop receive tables are not provisioned");
        }

        var branch = Clip(DefaultBranch(request.Branch), 10);
        var workshop = Clip((request.FromWorkshop ?? string.Empty).Trim(), 120);
        var remarks = (request.Narration ?? string.Empty).Trim();

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_repair_workshop_receive` (`company_id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`salesman`,`party_code`,`party_name`,`supp_inv_no`,`receive_remarks`,`status`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            branch,
            "RRC",
            NormalizeDate(FirstNonEmpty(request.ReceiveDate, request.VocDate)),
            request.VocNo < 0 ? 0 : request.VocNo,
            "",
            Clip(transferRef, 20),
            workshop,
            Clip(transferRef, 30),
            remarks,
            "received").ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Workshop receive saved", id);
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
