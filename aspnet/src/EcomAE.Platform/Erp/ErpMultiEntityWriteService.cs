using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_entity_create_group</c> / <c>epc_entity_add_member</c> /
/// <c>epc_entity_record_intercompany</c> / <c>epc_entity_eliminate</c> twins.
/// Schema ensure and consolidated TB stay PHP. ajax_erp multi_entity_save stays dry-run.
/// </summary>
public interface IErpMultiEntityWriteService
{
    Task<ErpSimpleWriteResult> CreateGroupAsync(
        string? groupCode,
        string? groupName,
        string? parentEntity,
        string? baseCurrency,
        string? fiscalYearEnd,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddMemberAsync(
        long groupId,
        string? siteKey,
        string? entityName,
        decimal ownershipPct,
        string? localCurrency,
        string? consolidation,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RecordIntercompanyAsync(
        long groupId,
        string? fromSiteKey,
        string? toSiteKey,
        decimal amount,
        string? description,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> EliminateAsync(long groupId, CancellationToken cancellationToken = default);
}

public sealed class ErpMultiEntityWriteService : IErpMultiEntityWriteService
{
    public static readonly string[] AllowedConsolidations = ["full", "proportional", "equity", "excluded"];

    private static readonly Regex FiscalYearEndPattern = new("^(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$", RegexOptions.CultureInvariant);

    private readonly IErpWriteConnectionFactory _connections;

    public ErpMultiEntityWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateGroupAsync(
        string? groupCode,
        string? groupName,
        string? parentEntity,
        string? baseCurrency,
        string? fiscalYearEnd,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var rawCode = (groupCode ?? string.Empty).Trim();
        var code = rawCode.Length == 0
            ? "GRP-" + Random.Shared.Next(1, 1000).ToString("000", CultureInfo.InvariantCulture)
            : NormalizeCode(rawCode);
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Group code must be 1-32 letters, numbers, dash, or underscore.");
        }

        var name = Clip(groupName, 128);
        var parent = Clip(parentEntity, 64);
        var currency = NormalizeIso(baseCurrency);
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        var fy = (fiscalYearEnd ?? string.Empty).Trim();
        if (fy.Length == 0)
        {
            fy = "12-31";
        }

        if (!FiscalYearEndPattern.IsMatch(fy))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Fiscal year end must be MM-DD.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `epc_entity_groups` (`group_code`,`group_name`,`parent_entity`,`base_currency`,`fiscal_year_end`) VALUES (?,?,?,?,?)"),
                cancellationToken,
                code, name, parent, currency, fy);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Group code already exists.");
        }

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Entity group " + code + " created.", id);
    }

    public async Task<ErpSimpleWriteResult> AddMemberAsync(
        long groupId,
        string? siteKey,
        string? entityName,
        decimal ownershipPct,
        string? localCurrency,
        string? consolidation,
        CancellationToken cancellationToken = default)
    {
        if (groupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A group id is required.");
        }

        var site = Clip(siteKey, 64);
        if (site.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A site key is required.");
        }

        var method = (consolidation ?? string.Empty).Trim().ToLowerInvariant();
        if (method.Length == 0)
        {
            method = "full";
        }

        if (!AllowedConsolidations.Contains(method, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid consolidation method.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var ownership = decimal.Round(Math.Clamp(ownershipPct <= 0 ? 100m : ownershipPct, 0, 100), 2, MidpointRounding.AwayFromZero);
        var name = Clip(entityName, 128);
        if (name.Length == 0)
        {
            name = site;
        }

        var currency = NormalizeIso(localCurrency);
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await GroupExistsAsync(connection, groupId, cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Entity group not found.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_entity_members` (`group_id`,`site_key`,`entity_name`,`ownership_pct`,`local_currency`,`consolidation`)
                VALUES (?,?,?,?,?,?)
                ON DUPLICATE KEY UPDATE `ownership_pct`=VALUES(`ownership_pct`)
                """),
            cancellationToken,
            groupId, site, name, ownership, currency, method);

        return ErpSimpleWriteResult.Ok("Member " + site + " saved on group " + groupId.ToString(CultureInfo.InvariantCulture) + ".", groupId);
    }

    public async Task<ErpSimpleWriteResult> RecordIntercompanyAsync(
        long groupId,
        string? fromSiteKey,
        string? toSiteKey,
        decimal amount,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (groupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A group id is required.");
        }

        var from = Clip(fromSiteKey, 64);
        var to = Clip(toSiteKey, 64);
        if (from.Length == 0 || to.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "From and to site keys are required.");
        }

        if (amount == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Inter-company amount cannot be zero.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await GroupExistsAsync(connection, groupId, cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Entity group not found.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_intercompany_txns` (`group_id`,`from_site_key`,`to_site_key`,`amount`,`description`) VALUES (?,?,?,?,?)"),
            cancellationToken,
            groupId, from, to, decimal.Round(amount, 2, MidpointRounding.AwayFromZero), Clip(description, 255));

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Inter-company transaction recorded.", id);
    }

    public async Task<ErpSimpleWriteResult> EliminateAsync(long groupId, CancellationToken cancellationToken = default)
    {
        if (groupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A group id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await GroupExistsAsync(connection, groupId, cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Entity group not found.");
        }

        var written = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_intercompany_txns` SET `status`='eliminated' WHERE `group_id`=? AND `status`='matched'"),
            cancellationToken,
            groupId);

        return ErpSimpleWriteResult.Ok(
            "Eliminated " + written.ToString(CultureInfo.InvariantCulture) + " matched inter-company row(s).",
            groupId);
    }

    private static async Task<bool> GroupExistsAsync(System.Data.Common.DbConnection connection, long groupId, CancellationToken cancellationToken)
    {
        var id = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_entity_groups` WHERE `id` = ?"),
            cancellationToken,
            groupId);
        return id > 0;
    }

    private static string NormalizeCode(string? value)
    {
        var raw = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.Length == 0 || raw.Length > 32)
        {
            return string.Empty;
        }

        foreach (var ch in raw)
        {
            if (ch is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_')
            {
                continue;
            }

            return string.Empty;
        }

        return raw;
    }

    private static string NormalizeIso(string? isoCode)
    {
        var raw = (isoCode ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.Length != 3)
        {
            return string.Empty;
        }

        foreach (var ch in raw)
        {
            if (ch is < 'A' or > 'Z')
            {
                return string.Empty;
            }
        }

        return raw;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
