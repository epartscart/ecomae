using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_karat_save</c> / <c>epc_jewel_karat_save</c> twin.
/// Schema-ensure and karat seed stay PHP — missing <c>epc_jewel_karat_master</c> refuses instead of CREATE.
/// </summary>
public interface IErpJwKaratWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwKaratSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwKaratSaveRequest(
    int CompanyId = 0,
    string? KaratCode = null,
    string? Description = null,
    decimal StdPurity = 0,
    decimal RangeFrom = 0,
    decimal RangeTo = 0,
    decimal SpGravity = 0,
    decimal PosRateMinMax = 0,
    string? Division = null);

public sealed class ErpJwKaratWriteService : IErpJwKaratWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwKaratWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwKaratSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.KaratCode ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Karat code is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_karat_master", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery karat tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        code = Clip(code, 10);
        var description = Clip((request.Description ?? string.Empty).Trim(), 60);
        var division = (request.Division ?? string.Empty).Trim().ToUpperInvariant();
        if (division.Length == 0)
        {
            division = "G";
        }

        division = Clip(division, 2);
        var stdPurity = RoundNonNeg(request.StdPurity, 6);
        var rangeFrom = RoundNonNeg(request.RangeFrom, 6);
        var rangeTo = RoundNonNeg(request.RangeTo, 6);
        var spGravity = RoundNonNeg(request.SpGravity, 4);
        var posRate = RoundNonNeg(request.PosRateMinMax, 2);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_karat_master` (`company_id`,`karat_code`,`description`,`std_purity`,`range_from`,`range_to`,`sp_gravity`,`pos_rate_min_max`,`division`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `description` = VALUES(`description`), `std_purity` = VALUES(`std_purity`), `range_from` = VALUES(`range_from`), `range_to` = VALUES(`range_to`), `sp_gravity` = VALUES(`sp_gravity`), `pos_rate_min_max` = VALUES(`pos_rate_min_max`), `division` = VALUES(`division`)"),
            cancellationToken,
            companyId,
            code,
            description,
            stdPurity,
            rangeFrom,
            rangeTo,
            spGravity,
            posRate,
            division).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_jewel_karat_master` WHERE `company_id` = ? AND `karat_code` = ? LIMIT 1"),
                cancellationToken,
                companyId,
                code).ConfigureAwait(false);
        }

        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Karat " + code + " saved", id);
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
