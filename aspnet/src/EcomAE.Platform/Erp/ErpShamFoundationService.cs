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
/// - Business units: <c>epc_erp_pm_business_units</c> (entity × BU, e.g. ASAPL-WSL).
/// - Access: <c>epc_erp_org_user_access</c> — user × entity × BU × role × action,
///   checked by <see cref="AccessAllowedAsync"/> on the backend (never UI-only).
///
/// ASAU: a business unit under ASAPL with its own cost centre CC-ASAU (owner
/// confirmed — it is not a legal entity).
///
/// Deployment: SHAM is a hybrid tenant (cloud + on-premises) — the seed is
/// idempotent and connection-scoped so it can run against each shard.
///
/// Phase-2 hook: shared COA taxonomy via `epc_erp_org_entity_accounts` — one
/// chart, activation is what differs per entity. Posting paths should call
/// <see cref="AccountActiveForEntityAsync"/> before accepting an entity×account
/// posting.
///
/// Admin credential: `admin@alshamaligroup.com` is seeded into the tenant `users`
/// store (bcrypt via LegacyLoginSecurity) bound to the backend group, plus a
/// group-wide `*` grant in the access matrix — it governs every user × entity
/// × BU. Re-running the seed never overwrites the password: the operator
/// password-change flow stays authoritative afterwards (rotation provision).
/// Everything is idempotent: the seed route may be re-run safely.
/// </summary>
public interface IErpShamFoundationService
{
    Task<ErpShamFoundationResult> SeedFoundationAsync(CancellationToken cancellationToken = default);
    Task<bool> AccessAllowedAsync(string userEmail, string? entityCode, string? buCode, string? action, CancellationToken cancellationToken = default);
    Task<bool> AccountActiveForEntityAsync(string entityCode, string coaCode, CancellationToken cancellationToken = default);
    Task<bool> RotatePasswordAsync(string userEmail, string newPassword, CancellationToken cancellationToken = default);
    Task<(bool Ok, string Message)> LockPeriodAsync(string entityCode, int periodYm, string userEmail, int adminId, CancellationToken cancellationToken = default);
    /// <summary>Resolves a BU selector that may be a code or a numeric id to its canonical code.</summary>
    Task<string> NormalizeBuCodeAsync(long entityId, string bu, CancellationToken cancellationToken = default);
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
    /// Business units per blueprint §3. ASAU is a BU under ASAPL with its own cost
    /// centre CC-ASAU (confirmed by owner) — it is not a legal entity.
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
        // Base GL journal tables too — workspaces/statements read epc_erp_gl_journals
        // and would 500 on a fresh tenant that has not posted anything yet.
        await ErpGlPostingService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpShamMspService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpShamLandedCostService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpShamTreasuryService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpShamTransferPriceService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsurePmMastersAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureInventoryAsync(connection, cancellationToken).ConfigureAwait(false);

        // Tenant login store (PHP-compatible users/groups/bind, same DDL as
        // ErpOnlyShopBootstrap) so the SHAM admin credential can be seeded here.
        await ErpDb.TryExecuteAsync(connection, EcomAE.Platform.Cp.ErpOnlyShopBootstrap.CreateUsersSql, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, EcomAE.Platform.Cp.ErpOnlyShopBootstrap.CreateSessionsSql, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, EcomAE.Platform.Cp.ErpOnlyShopBootstrap.CreateGroupsSql, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection, EcomAE.Platform.Cp.ErpOnlyShopBootstrap.CreateGroupBindSql, cancellationToken).ConfigureAwait(false);

        foreach (var sql in new[]
        {
            // ecom F&O: a warehouse carries a financial cost centre; the BU's default
            // warehouse inherits the BU cost centre on link.
            "ALTER TABLE `epc_erp_inv_warehouses` ADD COLUMN `cost_centre` varchar(40) NOT NULL DEFAULT ''",
            // §2 GroupMembership register: control basis, accounting method,
            // effective dates and approval status per member.
            "ALTER TABLE `epc_entity_members` ADD COLUMN `control_basis` varchar(40) NOT NULL DEFAULT ''",
            "ALTER TABLE `epc_entity_members` ADD COLUMN `accounting_method` varchar(24) NOT NULL DEFAULT ''",
            "ALTER TABLE `epc_entity_members` ADD COLUMN `effective_from` int(11) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_entity_members` ADD COLUMN `effective_to` int(11) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_entity_members` ADD COLUMN `approval_status` varchar(16) NOT NULL DEFAULT ''",
            "UPDATE `epc_erp_inv_warehouses` `w` JOIN `epc_erp_pm_business_units` `bu` ON `bu`.`default_warehouse_id`=`w`.`id` SET `w`.`cost_centre`=`bu`.`cost_centre` WHERE `w`.`cost_centre`='' AND `bu`.`cost_centre`<>''",
            "ALTER TABLE `epc_erp_pm_legal_entities` ADD COLUMN `consolidated` tinyint(1) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_pm_legal_entities` ADD COLUMN `legal_reg_no` varchar(60) NOT NULL DEFAULT ''",
            "ALTER TABLE `epc_erp_pm_legal_entities` ADD COLUMN `entity_kind` varchar(24) NOT NULL DEFAULT 'llc'",
            "ALTER TABLE `epc_erp_pm_business_units` ADD UNIQUE KEY `ux_pm_bu_entity` (`legal_entity_id`,`code`)",
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_business_units` (`id` int(11) NOT NULL AUTO_INCREMENT, `entity_id` int(11) NOT NULL DEFAULT 0, `code` varchar(40) NOT NULL DEFAULT '', `name` varchar(160) NOT NULL DEFAULT '', `activity` varchar(80) NOT NULL DEFAULT '', `branch` varchar(80) NOT NULL DEFAULT '', `parent_bu_id` int(11) NOT NULL DEFAULT 0, `manager_name` varchar(120) NOT NULL DEFAULT '', `default_warehouse_id` int(11) NOT NULL DEFAULT 0, `cost_centre` varchar(40) NOT NULL DEFAULT '', `profit_centre` varchar(40) NOT NULL DEFAULT '', `price_group` varchar(40) NOT NULL DEFAULT '', `status` varchar(12) NOT NULL DEFAULT 'active', `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_entity_bu` (`entity_id`,`code`), KEY `x_status` (`status`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Business units per legal entity (SHAM §3)'",
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_user_access` (`id` int(11) NOT NULL AUTO_INCREMENT, `user_email` varchar(190) NOT NULL DEFAULT '', `entity_id` int(11) NOT NULL DEFAULT 0, `bu_id` int(11) NOT NULL DEFAULT 0, `role` varchar(40) NOT NULL DEFAULT 'staff', `actions_csv` varchar(500) NOT NULL DEFAULT '', `status` varchar(12) NOT NULL DEFAULT 'active', `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_access` (`user_email`,`entity_id`,`bu_id`,`role`), KEY `x_entity` (`entity_id`), KEY `x_bu` (`bu_id`), KEY `x_status` (`status`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='User x entity x BU x role x action grants (SHAM §3)'",
            "CREATE TABLE IF NOT EXISTS `epc_erp_org_entity_accounts` (`id` int(11) NOT NULL AUTO_INCREMENT, `entity_id` int(11) NOT NULL DEFAULT 0, `coa_id` int(11) NOT NULL DEFAULT 0, `coa_code` varchar(16) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1, `suspense_only` tinyint(1) NOT NULL DEFAULT 0, `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_entity_coa` (`entity_id`,`coa_id`), KEY `x_code` (`coa_code`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Shared COA taxonomy, per-entity activation (SHAM §6)'",
            // Phase-7 hook: one operating bank account per legal entity.
            "CREATE TABLE IF NOT EXISTS `epc_erp_cash_bank_accounts` (`id` int(11) NOT NULL AUTO_INCREMENT, `name` varchar(255) NOT NULL, `account_type` varchar(8) NOT NULL DEFAULT 'cash', `bank_name` varchar(255) DEFAULT NULL, `account_number` varchar(64) DEFAULT NULL, `currency_code` varchar(8) NOT NULL DEFAULT 'AED', `opening_balance` decimal(14,2) NOT NULL DEFAULT 0.00, `office_id` int(11) NOT NULL DEFAULT 0, `active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP cash and bank accounts'",
            "ALTER TABLE `epc_erp_cash_bank_accounts` ADD `legal_entity_id` int(11) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_cash_bank_accounts` ADD `business_unit_id` int(11) NOT NULL DEFAULT 0",
            // Lookup index only — NOT unique: other flows create cash accounts with
            // blank account_number under entity 0, and a unique key would collide.
            "ALTER TABLE `epc_erp_cash_bank_accounts` DROP INDEX `ux_sham_entity_bank`",
            "ALTER TABLE `epc_erp_cash_bank_accounts` ADD KEY `ux_sham_entity_bank` (`legal_entity_id`,`account_number`)",
            // Phase-6 hook: closed accounting periods per entity (YYYYMM).
            "CREATE TABLE IF NOT EXISTS `epc_erp_sham_period_locks` (`id` int(11) NOT NULL AUTO_INCREMENT, `entity_id` int(11) NOT NULL DEFAULT 0, `period_ym` int(6) NOT NULL DEFAULT 0, `locked_by` int(11) NOT NULL DEFAULT 0, `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_entity_period` (`entity_id`,`period_ym`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='SHAM closed accounting periods per entity'",
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

            // Phase-7 hook: one operating bank account per legal entity, tagged with
            // legal_entity_id — namespaced SHAM-<entity>-MAIN so no other account is
            // adopted, and idempotent on the entity+account-number pair.
            foreach (var (code, name, _) in Entities)
            {
                var entityId = entityIds.TryGetValue(code, out var bankEntityId) ? bankEntityId : 0;
                if (entityId == 0)
                {
                    continue;
                }

                var accountNumber = "SHAM-" + code + "-MAIN";
                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_cash_bank_accounts` (`name`,`account_type`,`bank_name`,`account_number`,`currency_code`,`opening_balance`,`legal_entity_id`,`active`,`time_created`) "
                        + "SELECT ?,'bank','Al Shamali Group Treasury',?,'AED',0,?,1,? FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `epc_erp_cash_bank_accounts` WHERE `account_number`=?)"),
                    cancellationToken, name + " — Operating Account", accountNumber, entityId, now, accountNumber).ConfigureAwait(false);
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
                var controlBasis = consolidated ? "control" : "management";
                var acctMethod = consolidated ? "full" : "cost";
                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_entity_members` (`group_id`,`site_key`,`entity_name`,`ownership_pct`,`local_currency`,`consolidation`,`control_basis`,`accounting_method`,`approval_status`) "
                        + "VALUES (?,?,?,100.00,'AED',?,?,?,'approved') ON DUPLICATE KEY UPDATE `entity_name`=VALUES(`entity_name`), `consolidation`=VALUES(`consolidation`), `control_basis`=VALUES(`control_basis`), `accounting_method`=VALUES(`accounting_method`), `approval_status`=IF(`approval_status`='','approved',`approval_status`)"),
                    cancellationToken, groupId, code, name, method, controlBasis, acctMethod).ConfigureAwait(false);
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
                        "INSERT INTO `epc_erp_pm_business_units` (`code`,`name`,`legal_entity_id`,`cost_centre`,`note`,`active`,`time_created`,`time_updated`) "
                        + "VALUES (?,?,?,?,?,1,?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `cost_centre`=VALUES(`cost_centre`), `active`=1, `time_updated`=VALUES(`time_updated`)"),
                    cancellationToken, buCode, buName, entityId, costCentre, costCentre, now, now).ConfigureAwait(false);

                // Phase-3 hook: one default warehouse per BU — codes are SHAM-<short>
                // so an unrelated tenant's WH-*/existing row can never be renamed,
                // reactivated, or adopted as a SHAM default. Stock and reservations
                // stay entity-scoped via owner_company_id.
                var whSuffix = buCode.Contains('-', StringComparison.Ordinal) ? buCode[(buCode.IndexOf('-') + 1)..] : buCode;
                var whCode = "SHAM-" + whSuffix;
                var legacyWhCode = "WH-" + whSuffix;
                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_inv_warehouses` (`code`,`name`,`active`,`cost_centre`,`time_created`) "
                        + "VALUES (?,?,1,?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `active`=1, `cost_centre`=IF(`cost_centre`='',VALUES(`cost_centre`),`cost_centre`)"),
                    cancellationToken, whCode, "Al Shamali " + buName, costCentre, now).ConfigureAwait(false);
                var whId = (int)await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `code`=? LIMIT 1"),
                    cancellationToken, whCode).ConfigureAwait(false);
                if (whId > 0)
                {
                    await ErpDb.ExecuteAsync(connection, null,
                        ErpDb.Positional(
                            "UPDATE `epc_erp_pm_business_units` `bu`"
                            + " LEFT JOIN `epc_erp_inv_warehouses` `w` ON `bu`.`default_warehouse_id` = `w`.`id`"
                            + " SET `bu`.`default_warehouse_id`=?"
                            + " WHERE `bu`.`legal_entity_id`=? AND `bu`.`code`=?"
                            + " AND (`bu`.`default_warehouse_id`=0 OR `w`.`code`=?)"),
                        cancellationToken, whId, entityId, buCode, legacyWhCode).ConfigureAwait(false);
                }

                bus++;
            }

            // Phase-2 foundation: shared COA taxonomy with per-entity activation.
            // EnsureAsync only seeds the chart when EMPTY — a tenant with custom-only
            // accounts still needs the system codes before activation, so insert them here.
            // Activate every system account for every registered entity (blueprint §6:
            // one taxonomy, activation is what differs per entity). System codes are
            // INSERT-ed only when missing — an existing account keeps its operator-set
            // name/type/side (reseed never reclasifies a customized account).
            var activations = 0;
            foreach (var account in ErpGlChartOfAccountsSeeder.SystemAccounts)
            {
                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "INSERT IGNORE INTO `epc_erp_coa_accounts` (`code`,`name`,`account_type`,`normal_side`,`description`,`system_flag`,`time_created`) "
                        + "VALUES (?,?,?,?,?,1,?)"),
                    cancellationToken, account.Code, account.Name, account.Type, account.Side, account.Description, now).ConfigureAwait(false);
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
                            + "VALUES (?,?,?,1,?,?) ON DUPLICATE KEY UPDATE `coa_code`=VALUES(`coa_code`), `time_updated`=VALUES(`time_updated`)"),
                        cancellationToken, entityId, coaId, account.Code, now, now).ConfigureAwait(false);
                    activations++;
                }
            }

            // Admin credential: bcrypt via the same LegacyLoginSecurity as the
            // operator login flow; bound to the backend group + a group-wide
            // '*' access grant. Idempotent — an existing password is NOT replaced
            // (the operator password-change flow stays authoritative).
            var adminSeeded = await SeedAdminUserAsync(connection, now, cancellationToken).ConfigureAwait(false);

            return new(true,
                "SHAM foundation seeded: " + entities + " legal entities (6 consolidated), "
                + bus + " business units (incl. ASAU under ASAPL with cost centre CC-ASAU), group SHAM with "
                + members + " members, " + activations + " entity×account activations, "
                + (adminSeeded ? "admin created" : "admin already present (password untouched)") + " — "
                + "(shared COA taxonomy activated per entity per blueprint §6; hybrid-ready, idempotent per shard).",
                entities, bus, members);
        }
        catch (DbException ex)
        {
            return new(false, "Could not seed the SHAM foundation: " + ex.Message, 0, 0, 0);
        }
    }

    /// <summary>SHAM group admin credential per owner instruction; rotate after first login.</summary>
    public const string AdminEmail = "admin@alshamaligroup.com";
    public const string InitialAdminPassword = "Admin$2026";

    /// <summary>
    /// Inserts the SHAM group admin into the tenant `users` store (bcrypt,
    /// email confirmed, backend group 3) and grants it group-wide '*' in the
    /// access matrix so it governs every user × entity × BU.
    /// Returns false when the user already existed — the stored password is
    /// left untouched (rotation provision: the operator change flow owns it).
    /// </summary>
    private static async Task<bool> SeedAdminUserAsync(DbConnection connection, int now, CancellationToken cancellationToken)
    {
        // Backend group row must exist for the bind (same INSERT the bootstrap runs).
        await ErpDb.TryExecuteAsync(connection,
            "INSERT IGNORE INTO `groups` (`id`,`value`,`for_backend`,`unblocked`) VALUES (3,'backend',1,1)",
            cancellationToken).ConfigureAwait(false);

        var userId = (int)await ErpDb.LongAsync(connection, null,
            ErpDb.Positional("SELECT `user_id` FROM `users` WHERE LOWER(`email`) = LOWER(?) LIMIT 1"),
            cancellationToken, AdminEmail).ConfigureAwait(false);

        var created = false;
        if (userId == 0)
        {
            var hash = EcomAE.Platform.Auth.LegacyLoginSecurity.HashPassword(InitialAdminPassword);
            await ErpDb.ExecuteAsync(connection, null,
                ErpDb.Positional(
                    "INSERT INTO `users` (`email`,`email_confirmed`,`phone_confirmed`,`password`,`unlocked`,`time_registered`) VALUES (?,1,0,?,1,?)"),
                cancellationToken, AdminEmail, hash, now).ConfigureAwait(false);
            userId = (int)await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT `user_id` FROM `users` WHERE LOWER(`email`) = LOWER(?) LIMIT 1"),
                cancellationToken, AdminEmail).ConfigureAwait(false);
            created = userId > 0;
        }

        if (userId > 0)
        {
            await ErpDb.TryExecuteAsync(connection,
                "INSERT IGNORE INTO `users_groups_bind` (`user_id`,`group_id`) VALUES ("
                + userId.ToString(CultureInfo.InvariantCulture) + ",3)",
                cancellationToken).ConfigureAwait(false);
        }

        // Group-wide grant: entity_id=0 + bu_id=0 + '*' governs everything.
        await ErpDb.ExecuteAsync(connection, null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_org_user_access` (`user_email`,`entity_id`,`bu_id`,`role`,`actions_csv`,`status`,`time_created`,`time_updated`) "
                + "VALUES (?,0,0,'admin','*','active',?,?) ON DUPLICATE KEY UPDATE `status`='active', `time_updated`=VALUES(`time_updated`)"),
            cancellationToken, AdminEmail, now, now).ConfigureAwait(false);

        return created;
    }

    /// <summary>
    /// Password-rotation provision: rehash a tenant user's password in place.
    /// Same bcrypt cost as the operator login flow; fails when the user or a
    /// usable new password is absent. The seeded admin is expected to rotate
    /// via this path (or the operator password-change flow) after first login.
    /// </summary>
    public async Task<bool> RotatePasswordAsync(string userEmail, string newPassword, CancellationToken cancellationToken = default)
    {
        var email = (userEmail ?? string.Empty).Trim().ToLowerInvariant();
        var password = newPassword ?? string.Empty;
        if (email.Length == 0 || password.Length < 8 || password.Length > 72 || !_connections.IsConfigured)
        {
            return false;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.TryExecuteAsync(connection, EcomAE.Platform.Cp.ErpOnlyShopBootstrap.CreateUsersSql, cancellationToken).ConfigureAwait(false);
            var hash = EcomAE.Platform.Auth.LegacyLoginSecurity.HashPassword(password);
            var updated = await ErpDb.ExecuteAsync(connection, null,
                ErpDb.Positional("UPDATE `users` SET `password`=? WHERE LOWER(`email`)=LOWER(?)"),
                cancellationToken, hash, email).ConfigureAwait(false);
            return updated > 0;
        }
        catch (DbException)
        {
            return false;
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
    public async Task<string> NormalizeBuCodeAsync(long entityId, string bu, CancellationToken cancellationToken = default)
    {
        var value = (bu ?? string.Empty).Trim();
        if (value.Length == 0 || !long.TryParse(value, out var numericId))
        {
            return value;
        }
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var code = await ErpDb.StringAsync(connection, null,
                ErpDb.Positional("SELECT `code` FROM `epc_erp_pm_business_units` WHERE `id`=? AND `legal_entity_id`=? LIMIT 1"),
                cancellationToken, numericId, entityId).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(code) ? value : code.Trim();
        }
        catch
        {
            return value;
        }
    }

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
                if (entityId == 0)
                {
                    // Unknown entity code must not fall back to a group-wide grant match.
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(buCode))
            {
                // Resolve the BU row inside the given entity when both codes are
                // supplied — two entities may share a BU code, and unscoped
                // lookups could pair id/entity from different rows.
                // buCode may arrive as the BU's numeric id (legacy/hand-typed
                // ?bu=<id> links) — match code OR id so the scope isn't lost.
                // Numeric selectors match by id ONLY — a numeric string could
                // otherwise equal a different BU's code, granting on one BU
                // while normalization targets another (SEC finding r11).
                var buIsNumeric = long.TryParse(buCode.Trim(), out var buNumeric);
                var buSql = entityId != 0
                    ? buIsNumeric
                        ? "SELECT `id`,`legal_entity_id` FROM `epc_erp_pm_business_units` WHERE `id`=? AND `legal_entity_id`=? LIMIT 1"
                        : "SELECT `id`,`legal_entity_id` FROM `epc_erp_pm_business_units` WHERE `code`=? AND `legal_entity_id`=? LIMIT 1"
                    : buIsNumeric
                        ? "SELECT `id`,`legal_entity_id` FROM `epc_erp_pm_business_units` WHERE `id`=? LIMIT 1"
                        : "SELECT `id`,`legal_entity_id` FROM `epc_erp_pm_business_units` WHERE `code`=? LIMIT 1";
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = ErpDb.Positional(buSql);
                    var p0 = command.CreateParameter();
                    p0.ParameterName = "@p0";
                    p0.Value = buIsNumeric ? (object)buNumeric : buCode!.Trim().ToUpperInvariant();
                    command.Parameters.Add(p0);
                    if (entityId != 0)
                    {
                        var p1 = command.CreateParameter();
                        p1.ParameterName = "@p1";
                        p1.Value = entityId;
                        command.Parameters.Add(p1);
                    }

                    await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        buId = reader.GetInt32(0);
                    }
                }

                if (buId == 0)
                {
                    return false;
                }
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

    /// <summary>
    /// Phase-6 close: locks a period (YYYYMM) for an entity — all postings dated
    /// in that month or earlier are refused. Grant: 'period-lock'. Idempotent.
    /// </summary>
    public async Task<(bool Ok, string Message)> LockPeriodAsync(
        string entityCode, int periodYm, string userEmail, int adminId, CancellationToken cancellationToken = default)
    {
        var entity = (entityCode ?? string.Empty).Trim().ToUpperInvariant();
        if (entity.Length == 0 || periodYm is < 200001 or > 210012 || periodYm % 100 is < 1 or > 12)
        {
            return (false, "Entity code and a valid period (YYYYMM) are required.");
        }

        if (!_connections.IsConfigured)
        {
            return (false, "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            var entityId = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1"),
                cancellationToken, entity).ConfigureAwait(false);
            if (entityId == 0)
            {
                return (false, "Unknown or inactive legal entity '" + entity + "'.");
            }

            if (!await AccessAllowedAsync(userEmail, entity, null, "period-lock", cancellationToken).ConfigureAwait(false))
            {
                return (false, "No active grant for '" + userEmail + "' to lock periods in " + entity + ".");
            }

            await ErpDb.ExecuteAsync(connection, null,
                ErpDb.Positional("INSERT IGNORE INTO `epc_erp_sham_period_locks` (`entity_id`,`period_ym`,`locked_by`,`time_created`) VALUES (?,?,?,?)"),
                cancellationToken, entityId, periodYm, adminId, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            return (true, "Period " + periodYm + " locked for " + entity + " — postings dated on/before it are refused.");
        }
        catch (DbException)
        {
            return (false, "Period lock failed.");
        }
    }

    /// <summary>
    /// Phase-6 close: true when <paramref name="journalDateTs"/> (unix seconds or
    /// YYYYMMDD) falls inside a locked period for the entity. Table-absent
    /// (pre-migration tenants) means open — the lock is opt-in per entity.
    /// </summary>
    public static async Task<bool> PeriodLockedAsync(
        DbConnection connection,
        long entityId,
        long journalDateTs,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        if (entityId <= 0 || journalDateTs <= 0)
        {
            return false;
        }

        var dt = journalDateTs >= 100000000
            ? DateTimeOffset.FromUnixTimeSeconds(journalDateTs).UtcDateTime
            : new DateTime((int)(journalDateTs / 10000), (int)(journalDateTs / 100 % 100), (int)(journalDateTs % 100), 0, 0, 0, DateTimeKind.Utc);
        var periodYm = dt.Year * 100 + dt.Month;
        try
        {
            var locked = await ErpDb.LongAsync(connection, transaction,
                ErpDb.Positional("SELECT EXISTS(SELECT 1 FROM `epc_erp_sham_period_locks` WHERE `entity_id`=? AND `period_ym`>=?)"),
                cancellationToken, entityId, periodYm).ConfigureAwait(false);
            return locked == 1;
        }
        catch (DbException)
        {
            return false;
        }
    }
}
