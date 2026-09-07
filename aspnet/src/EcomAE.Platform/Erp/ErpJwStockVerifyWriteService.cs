using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_stock_verification_save</c> twin.
/// Writes the provisioned <c>epc_jewel_stock_verification</c> schema. Schema-ensure stays PHP.
/// </summary>
public interface IErpJwStockVerifyWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwStockVerifySaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwStockVerifySaveRequest(
    int CompanyId = 0,
    string? Branch = null,
    string? CountDate = null,
    string? VocDate = null,
    int VocNo = 0,
    string? Division = null,
    string? Counter = null,
    string? Location = null,
    string? Code = null,
    string? Supervisor = null,
    string? VerifiedBy = null,
    string? Narration = null);

public sealed class ErpJwStockVerifyWriteService : IErpJwStockVerifyWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwStockVerifyWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwStockVerifySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var location = FirstNonEmpty(request.Location, request.Counter, request.Code);
        if (location.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Location is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_stock_verification", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery stock verification tables are not provisioned");
        }

        var verifiedBy = FirstNonEmpty(request.VerifiedBy, request.Supervisor);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_stock_verification` (`company_id`,`branch`,`voc_type`,`voc_date`,`voc_no`,`verified_by`,`location`,`metal_stone`,`division`,`remarks`,`status`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            Clip(DefaultBranch(request.Branch), 10),
            "MSV",
            NormalizeDate(FirstNonEmpty(request.CountDate, request.VocDate)),
            request.VocNo < 0 ? 0 : request.VocNo,
            Clip(verifiedBy, 40),
            Clip(location, 40),
            "M",
            Clip((request.Division ?? string.Empty).Trim(), 5),
            (request.Narration ?? string.Empty).Trim(),
            "in_progress").ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Stock verification saved", id);
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
