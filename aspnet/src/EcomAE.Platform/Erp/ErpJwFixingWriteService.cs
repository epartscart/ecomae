using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_purchase_fixing_save</c> / <c>jw_sales_fixing_save</c> / <c>epc_jewel_fixing_save</c> twin.
/// Writes the provisioned <c>epc_jewel_fixing</c> schema (digest columns). Schema-ensure stays PHP.
/// </summary>
public interface IErpJwFixingWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwFixingSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwFixingSaveRequest(
    int CompanyId = 0,
    string? FixType = null,
    string? FixDirection = null,
    string? Branch = null,
    string? FixDate = null,
    int FixNo = 0,
    string? PartyCode = null,
    string? PartyName = null,
    string? Metal = null,
    string? Karat = null,
    string? RateType = null,
    decimal FixingWt = 0,
    decimal FixingRate = 0,
    decimal FixingAmount = 0,
    string? RefVoucher = null,
    string? Narration = null);

public sealed class ErpJwFixingWriteService : IErpJwFixingWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwFixingWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwFixingSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var partyCode = (request.PartyCode ?? string.Empty).Trim();
        var partyName = (request.PartyName ?? string.Empty).Trim();
        if (partyCode.Length == 0 && partyName.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Party is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_fixing", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery fixing tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var branch = (request.Branch ?? string.Empty).Trim();
        if (branch.Length == 0)
        {
            branch = "HO";
        }

        branch = Clip(branch, 10);
        var fixType = NormalizeFixType(request.FixType, request.FixDirection);
        var fixDate = NormalizeDate(request.FixDate);
        var metal = NormalizeMetal(request.Metal);
        var karat = Clip((request.Karat ?? string.Empty).Trim(), 10);
        if (karat.Length == 0)
        {
            karat = "24";
        }

        var rateType = (request.RateType ?? string.Empty).Trim();
        if (rateType.Length == 0)
        {
            rateType = "GMS";
        }

        rateType = Clip(rateType, 10);
        var qty = RoundNonNeg(request.FixingWt, 4);
        var rate = RoundNonNeg(request.FixingRate, 5);
        var amount = RoundNonNeg(request.FixingAmount, 2);
        if (amount == 0 && qty > 0 && rate > 0)
        {
            amount = RoundNonNeg(qty * rate, 2);
        }

        partyCode = Clip(partyCode, 20);
        partyName = Clip(partyName, 120);
        var reference = Clip((request.RefVoucher ?? string.Empty).Trim(), 30);
        var remarks = (request.Narration ?? string.Empty).Trim();
        var fixNo = request.FixNo < 0 ? 0 : request.FixNo;

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_fixing` (`company_id`,`branch`,`fix_type`,`fix_date`,`fix_no`,`party_code`,`party_name`,`metal`,`karat`,`rate_type`,`fix_rate`,`fix_qty_gms`,`fix_amount`,`unfixed_qty`,`reference_voc`,`status`,`remarks`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            companyId,
            branch,
            fixType,
            fixDate,
            fixNo,
            partyCode,
            partyName,
            metal,
            karat,
            rateType,
            rate,
            qty,
            amount,
            qty,
            reference,
            "open",
            remarks).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Fixing " + id.ToString(CultureInfo.InvariantCulture) + " saved", id);
    }

    private static string NormalizeFixType(string? fixType, string? direction)
    {
        var raw = (fixType ?? string.Empty).Trim().ToUpperInvariant();
        if (raw is "PF" or "SF")
        {
            return raw;
        }

        var dir = (direction ?? string.Empty).Trim().ToLowerInvariant();
        if (dir.Contains("sale", StringComparison.Ordinal))
        {
            return "SF";
        }

        return "PF";
    }

    private static string NormalizeMetal(string? metal)
    {
        var raw = (metal ?? string.Empty).Trim();
        if (raw.Equals("Gold", StringComparison.OrdinalIgnoreCase) || raw.Equals("G", StringComparison.OrdinalIgnoreCase))
        {
            return "G";
        }

        if (raw.Equals("Silver", StringComparison.OrdinalIgnoreCase) || raw.Equals("S", StringComparison.OrdinalIgnoreCase))
        {
            return "S";
        }

        if (raw.Equals("Platinum", StringComparison.OrdinalIgnoreCase) || raw.Equals("T", StringComparison.OrdinalIgnoreCase))
        {
            return "T";
        }

        raw = raw.ToUpperInvariant();
        return raw.Length == 0 ? "G" : Clip(raw, 2);
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

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
