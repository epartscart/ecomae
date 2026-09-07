using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_fix_unfix_create</c> / <c>epc_fix_unfix_settle</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpJwFixUnfixWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpJwFixUnfixCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SettleAsync(
        ErpJwFixUnfixSettleRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpJwFixUnfixCreateRequest(
    int CompanyId = 0,
    int PurchaseId = 0,
    int SupplierId = 0,
    string? SupplierName = null,
    string? PurchaseDate = null,
    string? StructureType = null,
    string? MetalType = null,
    string? Karat = null,
    decimal WeightGrams = 0,
    decimal FixRate = 0,
    string? FixDate = null,
    string? FixReference = null,
    decimal UnfixEstimatedRate = 0,
    decimal MarginOnFix = 0,
    decimal MarginOnUnfix = 0,
    decimal MakingCharges = 0,
    string? Notes = null);

public sealed record ErpJwFixUnfixSettleRequest(
    long Id = 0,
    decimal SettleRate = 0);

public sealed class ErpJwFixUnfixWriteService : IErpJwFixUnfixWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpJwFixUnfixWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpJwFixUnfixCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var supplier = Clip((request.SupplierName ?? string.Empty).Trim(), 200);
        if (supplier.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Supplier name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_fix_unfix_purchases", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_fix_unfix_purchases", "structure_type", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Fix/unfix tables are not provisioned");
        }

        var structure = NormalizeStructure(request.StructureType);
        var weight = RoundNonNeg(request.WeightGrams, 3);
        var making = RoundNonNeg(request.MakingCharges, 2);
        var fixRate = RoundNonNeg(request.FixRate, 4);
        var unfixEst = RoundNonNeg(request.UnfixEstimatedRate, 4);
        var rate = structure == "fix" ? fixRate : unfixEst;
        var total = decimal.Round((weight * rate) + making, 2, MidpointRounding.AwayFromZero);
        var karat = Clip((request.Karat ?? string.Empty).Trim(), 10);
        if (karat.Length == 0)
        {
            karat = "24K";
        }

        var metal = Clip((request.MetalType ?? string.Empty).Trim(), 30);
        if (metal.Length == 0)
        {
            metal = "gold";
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_fix_unfix_purchases` (`company_id`,`purchase_id`,`supplier_id`,`supplier_name`,`purchase_date`,`structure_type`,`metal_type`,`karat`,`weight_grams`,`fix_rate`,`fix_date`,`fix_reference`,`unfix_estimated_rate`,`margin_on_fix`,`margin_on_unfix`,`making_charges`,`total_value`,`status`,`notes`,`time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 'open', ?, ?)"),
            cancellationToken,
            request.CompanyId < 0 ? 0 : request.CompanyId,
            request.PurchaseId < 0 ? 0 : request.PurchaseId,
            request.SupplierId < 0 ? 0 : request.SupplierId,
            supplier,
            FormatDateOrToday(request.PurchaseDate),
            structure,
            metal,
            karat,
            weight,
            fixRate,
            FormatDateOrNull(request.FixDate),
            Clip((request.FixReference ?? string.Empty).Trim(), 100),
            unfixEst,
            RoundNonNeg(request.MarginOnFix, 2),
            RoundNonNeg(request.MarginOnUnfix, 2),
            making,
            total,
            request.Notes ?? string.Empty,
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok((structure == "fix" ? "Fix" : "Unfix") + " purchase created", id);
    }

    public async Task<ErpSimpleWriteResult> SettleAsync(
        ErpJwFixUnfixSettleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Purchase id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_fix_unfix_purchases", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_fix_unfix_purchases", "structure_type", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_fix_unfix_settlements", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Fix/unfix tables are not provisioned");
        }

        var structure = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `structure_type` FROM `epc_fix_unfix_purchases` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        if (string.IsNullOrEmpty(structure))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Purchase is missing.");
        }

        if (!string.Equals(structure, "unfix", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Not an unfix purchase");
        }

        var estimated = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `unfix_estimated_rate` FROM `epc_fix_unfix_purchases` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        var weight = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `weight_grams` FROM `epc_fix_unfix_purchases` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.Id).ConfigureAwait(false);
        var settleRate = RoundNonNeg(request.SettleRate, 4);
        var gainLoss = decimal.Round((settleRate - estimated) * weight, 2, MidpointRounding.AwayFromZero);
        var now = UnixNow();

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var updated = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "UPDATE `epc_fix_unfix_purchases` SET `unfix_settle_rate` = ?, `unfix_settle_date` = CURDATE(), `status` = 'settled', `time_updated` = ? WHERE `id` = ?"),
                cancellationToken,
                settleRate,
                now,
                request.Id).ConfigureAwait(false);
            if (updated <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Purchase is missing.");
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_fix_unfix_settlements` (`purchase_fix_id`,`settle_date`,`settle_rate`,`weight_settled`,`gain_loss`,`time_created`) VALUES (?, CURDATE(), ?, ?, ?, ?)"),
                cancellationToken,
                request.Id,
                settleRate,
                weight,
                gainLoss,
                now).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(
                "Unfix settled " + gainLoss.ToString("0.00", CultureInfo.InvariantCulture),
                request.Id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
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

    private static string NormalizeStructure(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return value == "unfix" ? "unfix" : "fix";
    }

    private static string FormatDateOrToday(string? raw)
    {
        var parsed = TryParseDate(raw);
        return (parsed ?? DateTime.Now.Date).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static object? FormatDateOrNull(string? raw)
    {
        var parsed = TryParseDate(raw);
        return parsed?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static DateTime? TryParseDate(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return null;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            return parsed.Date;
        }

        return null;
    }

    private static int UnixNow()
        => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
