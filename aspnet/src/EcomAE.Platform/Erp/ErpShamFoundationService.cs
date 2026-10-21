using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM (Al Shamali Auto Parts Group) Phase-1 foundation from
/// SHAM_ERP_Development_Blueprint.docx — organization, security and master governance:
///
/// - Legal-entity register: all 11 registered entities live in the shared
///   <c>epc_erp_pm_legal_entities</c> master, flagged <c>consolidated</c>=1 for the
///   six in the IFRS consolidation perimeter (ASAPL, CENT, INDP, IMPE, ALMA, SPDC)
///   and 0 for the five excluded-but-managed entities (PTAP, ALTC, AAPF, AAAR, RVAS).
/// - Consolidation perimeter: group <c>SHAM</c> in <c>epc_entity_groups</c> /
///   <c>epc_entity_members</c> (existing consolidation engine — 'full' members are
///   consolidated, 'excluded' appear in management reporting only).
/// - Business units: <c>epc_erp_org_business_units</c> (entity × BU, e.g. ASAPL-WSL).
/// - Access: <c>epc_erp_org_user_access</c> — user × entity × BU × role × action,
///   checked by <see cref="AccessAllowedAsync"/> on the backend (never UI-only).
///
/// ASAU from the blueprint is intentionally NOT seeded — the spec says its legal
/// status is unverified; add it as entity/branch/BU once management confirms.
/// Everything is idempotent: the seed route may be re-run safely.
/// </summary>
public interface IErpShamFoundationService
{
    Task<ErpShamFoundationResult> SeedFoundationAsync(CancellationToken cancellationToken = default);
    Task<bool> AccessAllowedAsync(string userEmail, string? entityCode, string? buCode, string? action, CancellationToken cancellationToken = default);
}

public sealed record ErpShamFoundationResult(bool Ok, string Message, int Entities, int BusinessUnits, int Members);

public sealed class ErpShamFoundationService : IErpShamFoundationService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpShamFoundationService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>(code, name, consolidated?) — entity register per blueprint §2.</summary>
    public static readonly (string Code, string Name, bool Consolidated)[] Entities =
    [
        ("ASAPL", "Al Shamali Auto Parts LLC", true),
        ("CENT", "Century Auto Parts LLC", true),
        ("INDP", "Indus Auto Parts LLC", true),
        ("IMPE", "Imperial Auto Parts LLC", true),
        ("ALMA", "Al Mayyad Auto Parts LLC", true),
        ("SPDC", "Shamali Parts Distribution FZE", true),
        ("PTAP", "Parts and Tools Auto Parts Trading LLC", false),
        ("ALTC", "Arab Land Auto Parts Trading", false),
        ("AAPF", "Arabain Auto Parts LLC", false),
        ("AAAR", "Al Arabi Auto Parts LLC", false),
        ("RVAS", "Red Vision Auto Spare Parts LLC", false),
    ];

    /// <summary>ASAPL business units per blueprint §3 (ASAU pending verification).</summary>
    public static readonly (string Entity, string Code, string Name)[] BusinessUnits =
    [
        ("ASAPL", "ASAPL-WSL", "ASAPL Wholesale"),
        ("ASAPL", "ASAPL-RTL", "ASAPL Retail"),
        ("ASAPL", "ASAPL-CLS", "ASAPL Clearance Sales"),
        ("ASAPL", "ASAPL-CLP", "ASAPL Central Logistics & Parts"),
        ("ASAPL", "ASAPL-BOOK", "ASAPL Back Office / Bookkeeping"),
    ];

    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpLazySchema.EnsureCompanyContextAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureMultiEntityAsync(connection, cancellationToken).ConfigureAwait(false);

        foreach (var sql in new[]
        {
            "ALTER TABLE `epc_erp_pm_legal_entities` ADD COLUMN `consolidated` tinyint(1) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_pm_legal_entities` ADD COLUMN `legal_reg_no` varchar(60) NOT NULL DEFAULT ''",
            "ALTER TABLE `epc_erp_pm_legal_entities` ADD COLUMN `entity_kind` varchar(24) NOT NULL DEFAULT 'llc'",
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_business_units` (`id` int(11) NOT NULL AUTO_INCREMENT, `entity_id` int(11) NOT NULL DEFAULT 0, `code` varchar(40) NOT NULL DEFAULT '', `name` varchar(160) NOT NULL DEFAULT '', `activity` varchar(80) NOT NULL DEFAULT '', `branch` varchar(80) NOT NULL DEFAULT '', `parent_bu_id` int(11) NOT NULL DEFAULT 0, `manager_name` varchar(120) NOT NULL DEFAULT '', `default_warehouse_id` int(11) NOT NULL DEFAULT 0, `cost_centre` varchar(40) NOT NULL DEFAULT '', `profit_centre` varchar(40) NOT NULL DEFAULT '', `price_group` varchar(40) NOT NULL DEFAULT '', `status` varchar(12) NOT NULL DEFAULT 'active', `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_entity_bu` (`entity_id`,`code`), KEY `x_status` (`status`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Business units per legal entity (SHAM §3)'",
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_user_access` (`id` int(11) NOT NULL AUTO_INCREMENT, `user_email` varchar(190) NOT NULL DEFAULT '', `entity_id` int(11) NOT NULL DEFAULT 0, `bu_id` int(11) NOT NULL DEFAULT 0, `role` varchar(40) NOT NULL DEFAULT 'staff', `actions_csv` varchar(500) NOT NULL DEFAULT '', `status` varchar(12) NOT NULL DEFAULT 'active', `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_access` (`user_email`,`entity_id`,`bu_id`,`role`), KEY `x_entity` (`entity_id`), KEY `x_bu` (`bu_id`), KEY `x_status` (`status`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='User x entity x BU x role x action grants (SHAM §3)'",
        })
        {
            await ErpDb.TryExecuteAsync(connection, sql, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<ErpShamFoundationResult> SeedFoundationAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(false, "TenantRegistry DB is not configured.", 0, 0, 0);
        }

        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var entities = 0;
            var entityIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (code, name, consolidated) in Entities)
            {
                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_pm_legal_entities` (`code`,`name`,`country_code`,`currency_code`,`consolidated`,`active`,`time_created`,`time_updated`) "
                        + "SELECT ?,?,'AE','AED',?,1,?,? FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `epc_erp_pm_legal_entities` WHERE `code`=?)"),
                    cancellationToken, code, name, consolidated ? 1 : 0, now, now, code).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "UPDATE `epc_erp_pm_legal_entities` SET `name`=?, `consolidated`=?, `active`=1, `time_updated`=? WHERE `code`=?"),
                    cancellationToken, name, consolidated ? 1 : 0, now, code).ConfigureAwait(false);
                var id = (int)await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? LIMIT 1"),
                    cancellationToken, code).ConfigureAwait(false);
                entityIds[code] = id;
                entities++;
            }

            var groupId = (int)await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_entity_groups` WHERE `group_code`='SHAM' LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
            if (groupId == 0)
            {
                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_entity_groups` (`group_code`,`group_name`,`parent_entity`,`base_currency`,`fiscal_year_end`,`status`) VALUES ('SHAM','Al Shamali Auto Parts Group','ASAPL','AED','12-31','active')"),
                    cancellationToken).ConfigureAwait(false);
                groupId = (int)await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_entity_groups` WHERE `group_code`='SHAM' LIMIT 1"),
                    cancellationToken).ConfigureAwait(false);
            }

            var members = 0;
            foreach (var (code, name, consolidated) in Entities)
            {
                var method = consolidated ? "full" : "excluded";
                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_entity_members` (`group_id`,`site_key`,`entity_name`,`ownership_pct`,`local_currency`,`consolidation`) "
                        + "VALUES (?,?,?,100.00,'AED',?) ON DUPLICATE KEY UPDATE `entity_name`=VALUES(`entity_name`), `consolidation`=VALUES(`consolidation`)"),
                    cancellationToken, groupId, code, name, method).ConfigureAwait(false);
                members++;
            }

            var bus = 0;
            foreach (var (entityCode, buCode, buName) in BusinessUnits)
            {
                var entityId = entityIds.TryGetValue(entityCode, out var eid) ? eid : 0;
                if (entityId == 0)
                {
                    continue;
                }

                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_org_business_units` (`entity_id`,`code`,`name`,`status`,`time_created`,`time_updated`) "
                        + "VALUES (?,?,?,'active',?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `status`='active', `time_updated`=VALUES(`time_updated`)"),
                    cancellationToken, entityId, buCode, buName, now, now).ConfigureAwait(false);
                bus++;
            }

            return new(true,
                "SHAM foundation seeded: " + entities + " legal entities (6 consolidated), "
                + bus + " ASAPL business units, group SHAM with " + members + " members "
                + "(consolidation='full' for the 6-entity perimeter, 'excluded' for management-only entities). "
                + "ASAU skipped pending legal classification per the blueprint.",
                entities, bus, members);
        }
        catch (DbException ex)
        {
            return new(false, "Could not seed the SHAM foundation: " + ex.Message, 0, 0, 0);
        }
    }

    /// <summary>
    /// Backend access check for the user × legal-entity × BU × role × action model.
    /// Any active grant row where entity_id matches (0 = group-wide grant) and bu_id
    /// matches (0 = entity-wide grant) and the action appears in actions_csv (or
    /// actions_csv='*') allows the operation. No grant rows = closed by default for
    /// tenants that have adopted the matrix; when the table is empty for the user we
    /// fail closed — operators grant explicitly.
    /// </summary>
    public async Task<bool> AccessAllowedAsync(string userEmail, string? entityCode, string? buCode, string? action, CancellationToken cancellationToken = default)
    {
        var email = (userEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (email.Length == 0 || !_connections.IsConfigured)
        {
            return false;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var entityId = 0;
            var buId = 0;
            if (!string.IsNullOrWhiteSpace(entityCode))
            {
                entityId = (int)await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? LIMIT 1"),
                    cancellationToken, entityCode!.Trim().ToUpperInvariant()).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(buCode))
            {
                buId = (int)await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_org_business_units` WHERE `code`=? LIMIT 1"),
                    cancellationToken, buCode!.Trim().ToUpperInvariant()).ConfigureAwait(false);
            }

            var wanted = (action ?? string.Empty).Trim().ToLowerInvariant();
            var allowed = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional(
                    "SELECT EXISTS(SELECT 1 FROM `epc_erp_org_user_access` WHERE `user_email`=? AND `status`='active' "
                    + "AND (`entity_id`=0 OR `entity_id`=?) AND (`bu_id`=0 OR `bu_id`=?) "
                    + "AND (`actions_csv`='*' OR FIND_IN_SET(?, `actions_csv`)))"),
                cancellationToken, email, entityId, buId, wanted).ConfigureAwait(false);
            return allowed == 1;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
