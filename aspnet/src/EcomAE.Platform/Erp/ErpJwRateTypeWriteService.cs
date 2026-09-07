using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_rate_type_save</c> / <c>epc_jewel_rate_type_save</c> twin.
/// Schema-ensure and rate-type seed stay PHP — missing <c>epc_jewel_rate_type</c> refuses instead of CREATE.
/// </summary>
public interface IErpJwRateTypeWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwRateTypeSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwRateTypeSaveRequest(
    int CompanyId = 0,
    string? Metal = null,
    string? RateType = null,
    decimal ConvFactor = 1,
    decimal ConvFactorOz = 31.1035m,
    string? Currency = null,
    decimal CurrRate = 1,
    decimal RateVariancePct = 50,
    decimal PosMarginMin = 1,
    decimal PosMarginMax = 50,
    string? Status = null,
    bool IsDefault = false);

public sealed class ErpJwRateTypeWriteService : IErpJwRateTypeWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwRateTypeWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwRateTypeSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rateType = (request.RateType ?? string.Empty).Trim();
        if (rateType.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Rate type is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_rate_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery rate type tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var metal = (request.Metal ?? string.Empty).Trim().ToUpperInvariant();
        if (metal.Length == 0)
        {
            metal = "G";
        }

        metal = Clip(metal, 2);
        rateType = Clip(rateType, 10);
        var currency = (request.Currency ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        currency = Clip(currency, 5);
        var status = (request.Status ?? string.Empty).Trim();
        if (status.Length == 0)
        {
            status = "MULTIPLY";
        }

        status = Clip(status, 10);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_rate_type` (`company_id`,`metal`,`rate_type`,`conv_factor`,`conv_factor_oz`,`currency`,`curr_rate`,`rate_variance_pct`,`pos_margin_min`,`pos_margin_max`,`status`,`is_default`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `conv_factor` = VALUES(`conv_factor`), `conv_factor_oz` = VALUES(`conv_factor_oz`), `currency` = VALUES(`currency`), `curr_rate` = VALUES(`curr_rate`), `rate_variance_pct` = VALUES(`rate_variance_pct`), `pos_margin_min` = VALUES(`pos_margin_min`), `pos_margin_max` = VALUES(`pos_margin_max`), `status` = VALUES(`status`), `is_default` = VALUES(`is_default`)"),
            cancellationToken,
            companyId,
            metal,
            rateType,
            RoundNonNeg(request.ConvFactor, 6),
            RoundNonNeg(request.ConvFactorOz, 4),
            currency,
            RoundNonNeg(request.CurrRate, 6),
            RoundNonNeg(request.RateVariancePct, 2),
            RoundNonNeg(request.PosMarginMin, 2),
            RoundNonNeg(request.PosMarginMax, 2),
            status,
            request.IsDefault ? 1 : 0).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_jewel_rate_type` WHERE `company_id` = ? AND `metal` = ? AND `rate_type` = ? LIMIT 1"),
                cancellationToken,
                companyId,
                metal,
                rateType).ConfigureAwait(false);
        }

        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Rate type " + rateType + " saved", id);
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
