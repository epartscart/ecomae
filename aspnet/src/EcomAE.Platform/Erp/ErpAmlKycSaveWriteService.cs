using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_aml_kyc_save</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpAmlKycSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpAmlKycSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpAmlKycSaveWriteRequest(
    long Id = 0,
    int CompanyId = 0,
    long CustomerId = 0,
    string? CustomerName = null,
    string? IdType = null,
    string? IdNumber = null,
    string? IdExpiry = null,
    string? Nationality = null,
    string? RiskLevel = null,
    bool PepStatus = false,
    bool SanctionsChecked = false,
    bool SanctionsMatch = false,
    string? VerificationStatus = null,
    string? NextReview = null,
    string? Notes = null);

public sealed class ErpAmlKycSaveWriteService : IErpAmlKycSaveWriteService
{
    private static readonly HashSet<string> Risks = new(StringComparer.OrdinalIgnoreCase)
    {
        "low", "medium", "high", "very_high"
    };

    private static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending", "verified", "rejected", "expired"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpAmlKycSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpAmlKycSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = Clip((request.CustomerName ?? string.Empty).Trim(), 200);
        if (name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Customer name is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_aml_kyc", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_aml_kyc", "customer_name", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "AML KYC tables are not provisioned");
        }

        var risk = Normalize(request.RiskLevel, Risks, "low");
        var status = Normalize(request.VerificationStatus, Statuses, "pending");
        var now = UnixNow();
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var customerId = request.CustomerId < 0 ? 0 : request.CustomerId;
        var idType = Clip((request.IdType ?? string.Empty).Trim(), 50);
        var idNumber = Clip((request.IdNumber ?? string.Empty).Trim(), 80);
        var expiry = FormatDateOrNull(request.IdExpiry);
        var nationality = Clip((request.Nationality ?? string.Empty).Trim(), 80);
        var nextReview = FormatDateOrNull(request.NextReview);
        var notes = request.Notes ?? string.Empty;
        var pep = request.PepStatus ? 1 : 0;
        var checkedFlag = request.SanctionsChecked ? 1 : 0;
        var match = request.SanctionsMatch ? 1 : 0;

        if (request.Id > 0)
        {
            var updated = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_aml_kyc` SET `company_id`=?, `customer_id`=?, `customer_name`=?, `id_type`=?, `id_number`=?, `id_expiry`=?, `nationality`=?, `risk_level`=?, `pep_status`=?, `sanctions_checked`=?, `sanctions_match`=?, `verification_status`=?, `next_review`=?, `notes`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                companyId, customerId, name, idType, idNumber, expiry, nationality, risk, pep, checkedFlag, match, status, nextReview, notes, now, request.Id).ConfigureAwait(false);
            if (updated <= 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "KYC record not found");
            }

            return ErpSimpleWriteResult.Ok("KYC record updated", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_aml_kyc` (`company_id`,`customer_id`,`customer_name`,`id_type`,`id_number`,`id_expiry`,`nationality`,`risk_level`,`pep_status`,`sanctions_checked`,`sanctions_match`,`verification_status`,`next_review`,`notes`,`time_created`,`time_updated`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            companyId, customerId, name, idType, idNumber, expiry, nationality, risk, pep, checkedFlag, match, status, nextReview, notes, now, now).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("KYC record created", id);
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

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
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

    private static string Normalize(string? raw, HashSet<string> allowed, string fallback)
    {
        var value = (raw ?? string.Empty).Trim();
        return allowed.Contains(value) ? value : fallback;
    }

    private static string? FormatDateOrNull(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : null;
    }

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

    private static long UnixNow()
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
