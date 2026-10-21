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
/// ASAU: confirmed as a business unit with its own cost centre — seeded as a BU
/// under ASAPL only. It stays out of the legal-entity register until its legal
/// classification is settled (blueprint marks it unverified).
///
/// Phase-2 hook: shared COA taxonomy via `epc_erp_org_entity_accounts` — one
/// chart, activation is what differs per entity. Posting paths should call
/// <see cref="AccountActiveForEntityAsync"/> before accepting an entity×account
/// posting. Everything is idempotent: the seed route may be re-run safely.
/// </summary>
public interface IErpShamFoundationService
{
    Task<ErpShamFoundationResult> SeedFoundationAsync(CancellationToken cancellationToken = default);
    Task<bool> AccessAllowedAsync(string userEmail, string? entityCode, string? buCode, string? action, CancellationToken cancellationToken = default);
    Task<bool> AccountActiveForEntityAsync(string entityCode, string coaCode, CancellationToken cancellationToken = default);
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

    /// <summary>
    /// ASAPL business units per blueprint §3. ASAU confirmed as a BU (with its own
    /// cost centre) — registered as a BU under ASAPL only; it stays OUT of the
    /// legal-entity register until its legal classification is settled.
    /// </summary>
    public static readonly (string Entity, string Code, string Name, string CostCentre)[] BusinessUnits =
    [
        ("ASAPL", "ASAPL-WSL", "ASAPL Wholesale", "CC-WSL"),
        ("ASAPL", "ASAPL-RTL", "ASAPL Retail", "CC-RTL"),
        ("ASAPL", "ASAPL-CLS", "ASAPL Clearance Sales", "CC-CLS"),
        ("ASAPL", "ASAPL-CLP", "ASAPL Central Logistics & Parts", "CC-CLP"),
        ("ASAPL", "ASAPL-BOOK", "ASAPL Back Office / Bookkeeping", "CC-BOOK"),
        ("ASAPL", "ASAU", "ASAU (branch operations)", "CC-ASAU"),
    ];

    public static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpLazySchema.EnsureCompanyContextAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureMultiEntityAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpGlChartOfAccountsSeeder.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);

        foreach (var sql in new[]
        {
            "ALTER TABLE `epc_erp_pm_legal_entities` ADD COLUMN `consolidated` tinyint(1) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_pm_legal_entities` ADD COLUMN `legal_reg_no` varchar(60) NOT NULL DEFAULT ''",
            "ALTER TABLE `epc_erp_pm_legal_entities` ADD COLUMN `entity_kind` varchar(24) NOT NULL DEFAULT 'llc'",
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_business_units` (`id` int(11) NOT NULL AUTO_INCREMENT, `entity_id` int(11) NOT NULL DEFAULT 0, `code` varchar(40) NOT NULL DEFAULT '', `name` varchar(160) NOT NULL DEFAULT '', `activity` varchar(80) NOT NULL DEFAULT '', `branch` varchar(80) NOT NULL DEFAULT '', `parent_bu_id` int(11) NOT NULL DEFAULT 0, `manager_name` varchar(120) NOT NULL DEFAULT '', `default_warehouse_id` int(11) NOT NULL DEFAULT 0, `cost_centre` varchar(40) NOT NULL DEFAULT '', `profit_centre` varchar(40) NOT NULL DEFAULT '', `price_group` varchar(40) NOT NULL DEFAULT '', `status` varchar(12) NOT NULL DEFAULT 'active', `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_entity_bu` (`entity_id`,`code`), KEY `x_status` (`status`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Business units per legal entity (SHAM §3)'",
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_user_access` (`id` int(11) NOT NULL AUTO_INCREMENT, `user_email` varchar(190) NOT NULL DEFAULT '', `entity_id` int(11) NOT NULL DEFAULT 0, `bu_id` int(11) NOT NULL DEFAULT 0, `role` varchar(40) NOT NULL DEFAULT 'staff', `actions_csv` varchar(500) NOT NULL DEFAULT '', `status` varchar(12) NOT NULL DEFAULT 'active', `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_access` (`user_email`,`entity_id`,`bu_id`,`role`), KEY `x_entity` (`entity_id`), KEY `x_bu` (`bu_id`), KEY `x_status` (`status`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='User x entity x BU x role x action grants (SHAM §3)'",
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_entity_accounts` (`id` int(11) NOT NULL AUTO_INCREMENT, `entity_id` int(11) NOT NULL DEFAULT 0, `coa_id` int(11) NOT NULL DEFAULT 0, `coa_code` varchar(16) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1, `suspense_only` tinyint(1) NOT NULL DEFAULT 0, `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_entity_coa` (`entity_id`,`coa_id`), KEY `x_code` (`coa_code`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Shared COA taxonomy, per-entity activation (SHAM §6)'",
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
            foreach (var (entityCode, buCode, buName, costCentre) in BusinessUnits)
            {
                var entityId = entityIds.TryGetValue(entityCode, out var eid) ? eid : 0;
                if (entityId == 0)
                {
                    continue;
                }

                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_org_business_units` (`entity_id`,`code`,`name`,`cost_centre`,`status`,`time_created`,`time_updated`) "
                        + "VALUES (?,?,?,?,'active',?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `cost_centre`=VALUES(`cost_centre`), `status`='active', `time_updated`=VALUES(`time_updated`)"),
                    cancellationToken, entityId, buCode, buName, costCentre, now, now).ConfigureAwait(false);
                bus++;
            }

            // Phase-2 foundation: shared COA taxonomy with per-entity activation —
            // activate every system account for every registered entity (blueprint §6:
            // one taxonomy, activation is what differs per entity).
            var activations = 0;
            foreach (var account in ErpGlChartOfAccountsSeeder.SystemAccounts)
            {
                var coaId = (int)await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code`=? LIMIT 1"),
                    cancellationToken, account.Code).ConfigureAwait(false);
                if (coaId == 0)
                {
                    continue;
                }

                foreach (var entityId in entityIds.Values)
                {
                    await ErpDb.ExecuteAsync(connection, null,
                        ErpDb.Positional(
                            "INSERT INTO `epc_erp_org_entity_accounts` (`entity_id`,`coa_id`,`coa_code`,`active`,`time_created`,`time_updated`) "
                            + "VALUES (?,?,?,1,?,?) ON DUPLICATE KEY UPDATE `active`=VALUES(`active`), `coa_code`=VALUES(`coa_code`)"),
                        cancellationToken, entityId, coaId, account.Code, now, now).ConfigureAwait(false);
                    activations++;
                }
            }

            return new(true,
                "SHAM foundation seeded: " + entities + " legal entities (6 consolidated), "
                + bus + " business units (incl. ASAU as a BU with its own cost centre), group SHAM with "
                + members + " members, " + activations + " entity×account activations "
                + "(shared COA taxonomy activated per entity per blueprint §6). "
                + "ASAU is a BU only — still out of the entity register pending legal classification.",
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

    /// <summary>
    /// Phase-2 gate: may `coaCode` post to `entityCode`? Shared COA taxonomy —
    /// every registered entity gets activation rows; a missing entity/activation
    /// or inactive row denies the posting. Fail-closed, mirroring the access check.
    /// </summary>
    public async Task<bool> AccountActiveForEntityAsync(string entityCode, string coaCode, CancellationToken cancellationToken = default)
    {
        var code = (entityCode ?? string.Empty).Trim().ToUpperInvariant();
        var coa = (coaCode ?? string.Empty).Trim();
        if (code.Length == 0 || coa.Length == 0 || !_connections.IsConfigured)
        {
            return false;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var allowed = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional(
                    "SELECT EXISTS(SELECT 1 FROM `epc_erp_org_entity_accounts` a "
                    + "JOIN `epc_erp_pm_legal_entities` e ON e.`id` = a.`entity_id` "
                    + "WHERE e.`code`=? AND e.`active`=1 AND a.`coa_code`=? AND a.`active`=1)"),
                cancellationToken, code, coa).ConfigureAwait(false);
            return allowed == 1;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
