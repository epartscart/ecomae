namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cons_entity_save</c> twin. Schema ensure, figures, and IC save stay PHP.
/// </summary>
public interface IErpConsEntitySaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        string? code,
        string? name,
        string? currencyCode,
        decimal ownershipPct,
        bool isHome,
        string? parentCode,
        CancellationToken cancellationToken = default);
}

public sealed class ErpConsEntitySaveWriteService : IErpConsEntitySaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpConsEntitySaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        string? code,
        string? name,
        string? currencyCode,
        decimal ownershipPct,
        bool isHome,
        string? parentCode,
        CancellationToken cancellationToken = default)
    {
        if (id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An entity id must be >= 0.");
        }

        var nextCode = NormalizeCode(code);
        if (nextCode.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Entity code is required");
        }

        var nextName = (name ?? string.Empty).Trim();
        if (nextName.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Entity name is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        nextName = Clip(nextName, 160);
        var ccy = NormalizeCurrency(currencyCode);
        var own = ClampOwnership(ownershipPct);
        var parent = Clip(NormalizeCode(parentCode), 40);
        var home = isHome ? 1 : 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (home == 1)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_cons_entities` SET `is_home`=0"),
                cancellationToken);
        }

        if (id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_cons_entities` SET `code`=?,`name`=?,`currency_code`=?,`ownership_pct`=?,`is_home`=?,`parent_code`=? WHERE `id`=?"),
                cancellationToken,
                nextCode, nextName, ccy, own, home, parent, id);
            return ErpSimpleWriteResult.Ok("Group entity saved", id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_cons_entities` (`code`,`name`,`currency_code`,`ownership_pct`,`is_home`,`parent_code`,`active`,`time_created`) VALUES (?,?,?,?,?,?,1,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`currency_code`=VALUES(`currency_code`),`ownership_pct`=VALUES(`ownership_pct`),`is_home`=VALUES(`is_home`),`parent_code`=VALUES(`parent_code`),`active`=1"),
            cancellationToken,
            nextCode, nextName, ccy, own, home, parent, now);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (inserted <= 0)
        {
            inserted = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_cons_entities` WHERE `code`=? LIMIT 1"),
                cancellationToken,
                nextCode);
        }

        return ErpSimpleWriteResult.Ok("Group entity saved", inserted);
    }

    public static string NormalizeCode(string? raw)
        => Clip((raw ?? string.Empty).Trim().ToUpperInvariant(), 40);

    public static string NormalizeCurrency(string? raw)
    {
        var ccy = Clip((raw ?? string.Empty).Trim().ToUpperInvariant(), 8);
        return ccy.Length == 0 ? "AED" : ccy;
    }

    public static decimal ClampOwnership(decimal raw)
    {
        if (raw < 0)
        {
            raw = 0;
        }

        if (raw > 100)
        {
            raw = 100;
        }

        return decimal.Round(raw, 3, MidpointRounding.AwayFromZero);
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];
}
