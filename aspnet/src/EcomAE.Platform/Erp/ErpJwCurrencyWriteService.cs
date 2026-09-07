using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>jw_currency_save</c> / <c>epc_jewel_currency_save_ajax</c> twin.
/// Schema-ensure and currency seed stay PHP — missing <c>epc_jewel_currency</c> refuses instead of CREATE.
/// </summary>
public interface IErpJwCurrencyWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwCurrencySaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwCurrencySaveRequest(
    int CompanyId = 0,
    string? CurrCode = null,
    string? Description = null,
    string? Fraction = null,
    string? Symbol = null,
    decimal ConvRate = 1,
    decimal MinConvRate = 1,
    decimal MaxConvRate = 1,
    string? Status = null);

public sealed class ErpJwCurrencyWriteService : IErpJwCurrencyWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwCurrencyWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpJwCurrencySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = (request.CurrCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Currency code is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_jewel_currency", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Jewellery currency tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        code = Clip(code, 5);
        var description = Clip((request.Description ?? string.Empty).Trim(), 60);
        var fraction = Clip((request.Fraction ?? string.Empty).Trim(), 20);
        var symbol = Clip((request.Symbol ?? string.Empty).Trim(), 5);
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
                "INSERT INTO `epc_jewel_currency` (`company_id`,`curr_code`,`description`,`fraction`,`symbol`,`conv_rate`,`min_conv_rate`,`max_conv_rate`,`status`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `description` = VALUES(`description`), `fraction` = VALUES(`fraction`), `symbol` = VALUES(`symbol`), `conv_rate` = VALUES(`conv_rate`), `status` = VALUES(`status`)"),
            cancellationToken,
            companyId,
            code,
            description,
            fraction,
            symbol,
            RoundNonNeg(request.ConvRate, 6),
            RoundNonNeg(request.MinConvRate, 6),
            RoundNonNeg(request.MaxConvRate, 6),
            status).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_jewel_currency` WHERE `company_id` = ? AND `curr_code` = ? LIMIT 1"),
                cancellationToken,
                companyId,
                code).ConfigureAwait(false);
        }

        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Currency " + code + " saved", id);
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
