using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpPromoSaveRequest(
    long Id,
    string? Code,
    string? Name,
    string? Type,
    decimal Value,
    decimal MinSpend,
    long ValidFrom,
    long ValidTo,
    int Active);

public interface ICpPromoWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(CpPromoSaveRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Live PHP <c>epc_promo_save()</c> on <c>epc_promo_promotions</c>.
/// INSERT <c>id==0</c> always <c>active=1</c>. UPDATE <c>id&gt;0</c> does not change <c>code</c>.
/// Apply / loyalty / <c>epc_promotions</c> stay Classic. Schema-ensure stays Classic.
/// </summary>
public sealed class CpPromoWriteService : ICpPromoWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPromoWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeType(string? type)
    {
        var raw = (type ?? string.Empty).Trim();
        return raw.Length == 0 ? "percent" : (raw.Length > 16 ? raw[..16] : raw);
    }

    public static string NormalizeCode(string? code)
    {
        var raw = (code ?? string.Empty).Trim();
        return raw.Length > 40 ? raw[..40] : raw;
    }

    public static string NormalizeName(string? name)
    {
        var raw = (name ?? string.Empty).Trim();
        return raw.Length > 160 ? raw[..160] : raw;
    }

    public static int NormalizeActive(int active)
        => active == 0 ? 0 : 1;

    public async Task<ErpSimpleWriteResult> SaveAsync(CpPromoSaveRequest request, CancellationToken cancellationToken = default)
    {
        var id = request.Id;
        var code = NormalizeCode(request.Code);
        var name = NormalizeName(request.Name);
        var type = NormalizeType(request.Type);
        var active = NormalizeActive(request.Active);
        var validFrom = request.ValidFrom < 0 ? 0 : request.ValidFrom;
        var validTo = request.ValidTo < 0 ? 0 : request.ValidTo;

        if (id <= 0 && code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A promotion code is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "epc_promo_promotions", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("db", "epc_promo_promotions table is not provisioned. Schema-ensure stays Classic.");
            }

            if (id > 0)
            {
                var exists = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_promo_promotions` WHERE `id` = ?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Promotion was not found.");
                }

                var writes = await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        "UPDATE `epc_promo_promotions` SET `name`=?, `type`=?, `value`=?, `min_spend`=?, `valid_from`=?, `valid_to`=?, `active`=? WHERE `id`=?"),
                    cancellationToken,
                    name,
                    type,
                    request.Value,
                    request.MinSpend,
                    validFrom,
                    validTo,
                    active,
                    id).ConfigureAwait(false);
                if (writes <= 0)
                {
                    return ErpSimpleWriteResult.Fail("unchanged", "Promotion was not updated.");
                }

                return ErpSimpleWriteResult.Ok("Promotion saved.", id);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `epc_promo_promotions` (`code`,`name`,`type`,`value`,`min_spend`,`valid_from`,`valid_to`,`active`) VALUES (?,?,?,?,?,?,?,1)"),
                cancellationToken,
                code,
                name,
                type,
                request.Value,
                request.MinSpend,
                validFrom,
                validTo).ConfigureAwait(false);
            var created = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Promotion saved.", created);
        }
        catch (DbException ex) when (ex.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Promotion code already exists.");
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "epc_promo_promotions write failed. Schema-ensure stays Classic.");
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
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
}
