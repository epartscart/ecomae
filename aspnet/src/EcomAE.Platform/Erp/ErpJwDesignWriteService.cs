using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_design_save</c> / <c>epc_jewel_design_save_ajax</c> twin.
/// Schema-ensure stays PHP — missing <c>epc_jewel_design</c> refuses instead of CREATE.
/// </summary>
public interface IErpJwDesignWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwDesignSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwDesignSaveRequest(
    int CompanyId = 0,
    string? DesignCode = null,
    string? Description = null,
    string? Currency = null,
    decimal CurrencyRate = 1,
    string? CostCentre = null,
    string? Category = null,
    string? SubCategory = null,
    string? Type = null,
    string? Brand = null,
    string? Color = null,
    string? Country = null,
    string? Vendor = null,
    string? VendorRef = null,
    decimal CostAmount = 0,
    string? Price1Code = null,
    decimal Price1Pct = 0,
    decimal Price1Fc = 0,
    decimal Price1Lc = 0);

public sealed class ErpJwDesignWriteService : IErpJwDesignWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwDesignWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwDesignSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.DesignCode ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Design code is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_design", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery design tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        code = Clip(code, 20);
        var currency = (request.Currency ?? string.Empty).Trim().ToUpperInvariant();
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        currency = Clip(currency, 5);
        var price1Code = (request.Price1Code ?? string.Empty).Trim();
        if (price1Code.Length == 0)
        {
            price1Code = "GEN";
        }

        price1Code = Clip(price1Code, 5);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_jewel_design` (`company_id`,`design_code`,`description`,`currency`,`currency_rate`,`cost_centre`,`category`,`sub_category`,`type`,`brand`,`color`,`country`,`vendor`,`vendor_ref`,`cost_amount`,`price1_code`,`price1_pct`,`price1_fc`,`price1_lc`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `description` = VALUES(`description`), `category` = VALUES(`category`), `type` = VALUES(`type`), `cost_amount` = VALUES(`cost_amount`)"),
            cancellationToken,
            companyId,
            code,
            Clip((request.Description ?? string.Empty).Trim(), 120),
            currency,
            RoundNonNeg(request.CurrencyRate, 5),
            Clip((request.CostCentre ?? string.Empty).Trim(), 20),
            Clip((request.Category ?? string.Empty).Trim(), 30),
            Clip((request.SubCategory ?? string.Empty).Trim(), 30),
            Clip((request.Type ?? string.Empty).Trim(), 30),
            Clip((request.Brand ?? string.Empty).Trim(), 60),
            Clip((request.Color ?? string.Empty).Trim(), 30),
            Clip((request.Country ?? string.Empty).Trim(), 30),
            Clip((request.Vendor ?? string.Empty).Trim(), 20),
            Clip((request.VendorRef ?? string.Empty).Trim(), 20),
            RoundNonNeg(request.CostAmount, 2),
            price1Code,
            RoundNonNeg(request.Price1Pct, 2),
            RoundNonNeg(request.Price1Fc, 2),
            RoundNonNeg(request.Price1Lc, 2)).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_jewel_design` WHERE `company_id` = ? AND `design_code` = ? LIMIT 1"),
                cancellationToken,
                companyId,
                code).ConfigureAwait(false);
        }

        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Design " + code + " saved", id);
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
