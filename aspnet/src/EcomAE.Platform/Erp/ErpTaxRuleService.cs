using System.Data.Common;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Effective-dated tax rules (ecom-style "sales tax group / item group"
/// resolution reduced to what our tenants need): a rule maps a supply
/// scenario — supply kind (goods/services/real_estate), location pair
/// (mainland/designated_zone/export), optional designated-zone flags and
/// VAT-group membership — onto a VAT rate + document class, valid between
/// <c>valid_from</c>/<c>valid_to</c> (unix seconds, 0 = open-ended).
///
/// Resolution order for a posting date: most specific match wins —
/// entity-scoped > tenant-wide, item/item-group-scoped > generic, then the
/// newest valid_from. VAT-group members (<see cref="ErpIntercompanySetupService"/>
/// policy <c>vat_group</c>) can carry a rule whose <c>supply_kind</c> is
/// 'vat_group' to zero-rate intra-group supplies.
///
/// Grants: <c>tax-rule-set</c> / <c>tax-rule-view</c> via
/// <see cref="IErpShamFoundationService.AccessAllowedAsync"/>.
/// </summary>
public interface IErpTaxRuleService
{
    Task<ErpTaxRuleResult> SetAsync(string entityCode, string scopeCode, string supplyKind, string fromLocation, string toLocation, string treatment, decimal vatRate, string docClass, long validFromUnix, long validToUnix, string? note, string userEmail, CancellationToken cancellationToken = default);
    Task<ErpTaxRuleView?> ResolveAsync(string entityCode, string? buCode, string scopeCode, string supplyKind, string fromLocation, string toLocation, long atUnix, string userEmail, CancellationToken cancellationToken = default);
    /// <summary>Rules for one entity (plus tenant-wide) or all rules when
    /// <paramref name="entityCode"/> is empty — the empty scope needs a
    /// tenant-wide grant, an entity scope needs that entity's grant.</summary>
    Task<IReadOnlyList<ErpTaxRuleView>> ListAsync(string userEmail, string? entityCode = null, CancellationToken cancellationToken = default);
}

public sealed record ErpTaxRuleView(long Id, string EntityCode, string ScopeCode, string SupplyKind, string FromLocation, string ToLocation, string Treatment, decimal VatRate, string DocClass, long ValidFrom, long ValidTo, string Note);
public sealed record ErpTaxRuleResult(bool Ok, string Message, long RuleId);

public sealed class ErpTaxRuleService : IErpTaxRuleService
{
    private static readonly HashSet<string> SupplyKinds = new(StringComparer.OrdinalIgnoreCase)
        { "goods", "services", "real_estate", "vat_group" };
    private static readonly HashSet<string> Locations = new(StringComparer.OrdinalIgnoreCase)
        { "mainland", "designated_zone", "export", "any" };
    private static readonly HashSet<string> Treatments = new(StringComparer.OrdinalIgnoreCase)
        { "standard", "zero_rated", "outside_scope", "exempt" };

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;

    public ErpTaxRuleService(IErpWriteConnectionFactory connections, IErpShamFoundationService foundation)
    {
        _connections = connections;
        _foundation = foundation;
    }

    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
        => await ErpDb.TryExecuteAsync(connection, """
            CREATE TABLE IF NOT EXISTS `epc_erp_tax_rules` (
              `id` int(11) NOT NULL AUTO_INCREMENT,
              `entity_id` int(11) NOT NULL DEFAULT 0,
              `scope_code` varchar(64) NOT NULL DEFAULT '',
              `supply_kind` varchar(20) NOT NULL DEFAULT 'goods',
              `from_location` varchar(24) NOT NULL DEFAULT 'any',
              `to_location` varchar(24) NOT NULL DEFAULT 'any',
              `treatment` varchar(16) NOT NULL DEFAULT 'standard',
              `vat_rate` decimal(6,3) NOT NULL DEFAULT 5.000,
              `doc_class` varchar(24) NOT NULL DEFAULT 'tax_invoice',
              `valid_from` int(11) NOT NULL DEFAULT 0,
              `valid_to` int(11) NOT NULL DEFAULT 0,
              `active` tinyint(1) NOT NULL DEFAULT 1,
              `note` varchar(255) NOT NULL DEFAULT '',
              `set_by` varchar(190) NOT NULL DEFAULT '',
              `time_created` int(11) NOT NULL DEFAULT 0,
              PRIMARY KEY (`id`),
              KEY `x_scope` (`entity_id`,`supply_kind`,`valid_from`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Effective-dated tax rules (ecom tax-group model, §8)'
            """, cancellationToken).ConfigureAwait(false);

    public async Task<ErpTaxRuleResult> SetAsync(string entityCode, string scopeCode, string supplyKind, string fromLocation, string toLocation, string treatment, decimal vatRate, string docClass, long validFromUnix, long validToUnix, string? note, string userEmail, CancellationToken cancellationToken = default)
    {
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, null, "tax-rule-set", cancellationToken).ConfigureAwait(false))
        {
            return new ErpTaxRuleResult(false, "tax-rule-set grant required.", 0);
        }

        supplyKind = (supplyKind ?? string.Empty).Trim().ToLowerInvariant();
        fromLocation = (fromLocation ?? string.Empty).Trim().ToLowerInvariant();
        toLocation = (toLocation ?? string.Empty).Trim().ToLowerInvariant();
        treatment = (treatment ?? string.Empty).Trim().ToLowerInvariant();
        if (!SupplyKinds.Contains(supplyKind) || !Locations.Contains(fromLocation) || !Locations.Contains(toLocation) || !Treatments.Contains(treatment))
        {
            return new ErpTaxRuleResult(false, "Unknown supply_kind/from_location/to_location/treatment value.", 0);
        }
        if (vatRate is < 0 or > 100)
        {
            return new ErpTaxRuleResult(false, "vat_rate must be 0–100.", 0);
        }
        // Non-standard treatments carry no output VAT — normalize so a
        // zero_rated rule never resolves a stray positive rate.
        if (treatment != "standard")
        {
            vatRate = 0m;
        }
        if (validFromUnix > 0 && validToUnix > 0 && validToUnix <= validFromUnix)
        {
            return new ErpTaxRuleResult(false, "valid_to must be after valid_from.", 0);
        }

        var entityId = entityCode is { Length: > 0 }
            ? await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false)
            : 0;
        if (entityCode is { Length: > 0 } && entityId <= 0)
        {
            return new ErpTaxRuleResult(false, "Unknown entity code.", 0);
        }

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(conn, null,
            "INSERT INTO `epc_erp_tax_rules` (`entity_id`,`scope_code`,`supply_kind`,`from_location`,`to_location`,`treatment`,`vat_rate`,`doc_class`,`valid_from`,`valid_to`,`active`,`note`,`set_by`,`time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,1,?,?,?)",
            cancellationToken,
            entityId, (scopeCode ?? string.Empty).Trim(), supplyKind, fromLocation, toLocation,
            treatment, vatRate, (docClass ?? string.Empty).Trim() is { Length: > 0 } dc ? dc : "tax_invoice",
            validFromUnix, validToUnix, note ?? string.Empty, userEmail, now).ConfigureAwait(false);
        var ruleId = await ErpDb.LongAsync(conn, null, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false);
        return new ErpTaxRuleResult(true, $"Tax rule #{ruleId} saved ({supplyKind} {fromLocation}→{toLocation}, {vatRate}% {treatment}).", ruleId);
    }

    public async Task<ErpTaxRuleView?> ResolveAsync(string entityCode, string? buCode, string scopeCode, string supplyKind, string fromLocation, string toLocation, long atUnix, string userEmail, CancellationToken cancellationToken = default)
    {
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, buCode, "tax-rule-view", cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var entityId = entityCode is { Length: > 0 }
            ? await EntityIdAsync(entityCode, cancellationToken).ConfigureAwait(false)
            : 0;
        if (atUnix <= 0)
        {
            atUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        await using var command = conn.CreateCommand();
        // Most specific first: entity rule > tenant-wide, exact scope > wildcard,
        // exact location pair > 'any', newest valid_from wins within a tier.
        command.CommandText = ErpDb.Positional(
            "SELECT r.`id`, r.`entity_id`, IFNULL(e.`code`, '') AS entity_code, r.`scope_code`, r.`supply_kind`, r.`from_location`, r.`to_location`, r.`treatment`, r.`vat_rate`, r.`doc_class`, r.`valid_from`, r.`valid_to`, r.`note`"
            + " FROM `epc_erp_tax_rules` r LEFT JOIN `epc_erp_pm_legal_entities` e ON e.`id` = r.`entity_id`"
            + " WHERE r.`active` = 1 AND r.`supply_kind` = ?"
            + " AND (r.`entity_id` = ? OR r.`entity_id` = 0)"
            + " AND (r.`scope_code` = ? OR r.`scope_code` = '')"
            + " AND (r.`from_location` = ? OR r.`from_location` = 'any')"
            + " AND (r.`to_location` = ? OR r.`to_location` = 'any')"
            + " AND (r.`valid_from` = 0 OR r.`valid_from` <= ?)"
            + " AND (r.`valid_to` = 0 OR r.`valid_to` >= ?)"
            + " ORDER BY (r.`entity_id` > 0) DESC, (r.`scope_code` <> '') DESC,"
            + "   (r.`from_location` <> 'any') DESC, (r.`to_location` <> 'any') DESC,"
            + "   r.`valid_from` DESC, r.`id` DESC LIMIT 1");
        ErpDb.AddParameters(command,
            (supplyKind ?? string.Empty).Trim().ToLowerInvariant(), entityId,
            (scopeCode ?? string.Empty).Trim(),
            (fromLocation ?? string.Empty).Trim().ToLowerInvariant(),
            (toLocation ?? string.Empty).Trim().ToLowerInvariant(), atUnix, atUnix);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }
        return MapRow(reader);
    }

    public async Task<IReadOnlyList<ErpTaxRuleView>> ListAsync(string userEmail, string? entityCode = null, CancellationToken cancellationToken = default)
    {
        var entity = (entityCode ?? string.Empty).Trim();
        if (!await _foundation.AccessAllowedAsync(userEmail, entity.Length > 0 ? entity : null, null, "tax-rule-view", cancellationToken).ConfigureAwait(false))
        {
            return [];
        }
        var entityId = entity.Length > 0
            ? await EntityIdAsync(entity, cancellationToken).ConfigureAwait(false)
            : 0;
        if (entity.Length > 0 && entityId <= 0)
        {
            return [];
        }

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        var list = new List<ErpTaxRuleView>();
        await using var command = conn.CreateCommand();
        command.CommandText =
            "SELECT r.`id`, r.`entity_id`, IFNULL(e.`code`, '') AS entity_code, r.`scope_code`, r.`supply_kind`, r.`from_location`, r.`to_location`, r.`treatment`, r.`vat_rate`, r.`doc_class`, r.`valid_from`, r.`valid_to`, r.`note`"
            + " FROM `epc_erp_tax_rules` r LEFT JOIN `epc_erp_pm_legal_entities` e ON e.`id` = r.`entity_id`"
            + " WHERE r.`active` = 1"
            + (entityId > 0 ? " AND (r.`entity_id` = " + entityId.ToString(System.Globalization.CultureInfo.InvariantCulture) + " OR r.`entity_id` = 0)" : "")
            + " ORDER BY r.`entity_id`, r.`supply_kind`, r.`valid_from` DESC, r.`id` DESC LIMIT 500";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(MapRow(reader));
        }
        return list;
    }

    private static ErpTaxRuleView MapRow(DbDataReader reader)
        => new(reader.GetInt64(0), reader.GetString(2), reader.GetString(3), reader.GetString(4),
            reader.GetString(5), reader.GetString(6), reader.GetString(7),
            reader.GetDecimal(8), reader.GetString(9), reader.GetInt64(10), reader.GetInt64(11), reader.GetString(12));

    private async Task<long> EntityIdAsync(string entityCode, CancellationToken cancellationToken)
    {
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ErpDb.LongAsync(conn, null,
            "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1",
            cancellationToken, entityCode.Trim()).ConfigureAwait(false);
    }
}
