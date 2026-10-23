using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Applies staged D365 F&amp;O payloads to the operational masters — this is where
/// the pull becomes visible data: customers land on the customer master, released
/// products on inventory items, sales/purchase order headers on the document tables,
/// main accounts on the chart of accounts. Upserts keep a D365-side unique key so a
/// re-pull refreshes rather than duplicates.
/// </summary>
public interface IErpD365MappingService
{
    /// <summary>Entities that have a staged → master mapping.</summary>
    IReadOnlyDictionary<string, string> MappedEntities { get; }
    Task<ErpD365ApplyResult> ApplyAsync(DbConnection connection, string entity, long adminId, CancellationToken cancellationToken = default);

    /// <summary>Live trial-balance compare: our GL balances per account vs balances aggregated from staged D365 posted transactions (GeneralLedgerBalanceCDREntities), keyed by dimension string for the dimension-set pivot — plus total debit/credit both sides must agree on.</summary>
    Task<ErpD365TbCompare> TrialBalanceCompareAsync(DbConnection connection, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ErpD365InvCompareRow>> InventoryCompareAsync(DbConnection connection, CancellationToken cancellationToken = default);
}

public sealed record ErpD365InvCompareRow(string Warehouse, decimal EcomAeQty, decimal EcomAeValue, decimal D365Qty, decimal D365Avail)
{
    public bool QtyMatches => Math.Abs(EcomAeQty - D365Qty) <= 0.005m;
}

public sealed record ErpD365TbCompareRow(string AccountCode, string AccountName, string Dimension, decimal EcomAeBalance, decimal D365Balance)
{
    public decimal Difference => EcomAeBalance - D365Balance;
    public bool Matches => Math.Abs(Difference) < 0.005m;
}

/// <summary>Trial-balance compare: per-account+dimension rows plus the debit/credit totals both sides must agree on (a TB match means every detail row AND the totals match).</summary>
public sealed record ErpD365TbCompare(IReadOnlyList<ErpD365TbCompareRow> Rows, decimal EcomAeDebit, decimal EcomAeCredit, decimal D365Debit, decimal D365Credit)
{
    public bool TotalsMatch => Math.Abs(EcomAeDebit - D365Debit) < 0.005m && Math.Abs(EcomAeCredit - D365Credit) < 0.005m;
}

public sealed record ErpD365ApplyResult(bool Succeeded, string Code, string Message, int Mapped, int Skipped);

public sealed class ErpD365MappingService(IErpD365DocCycleService docCycle, IErpD365GlService glService, IErpGlPostingService glPosting) : IErpD365MappingService
{
    public IReadOnlyDictionary<string, string> MappedEntities { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ReleasedProductsV2"] = "Inventory items",
            ["CustomersV3"] = "Customers",
            ["VendorsV2"] = "Suppliers",
            ["SalesOrderHeadersV2"] = "Sales orders",
            ["SalesOrderLines"] = "Sales order lines",
            ["PurchaseOrderHeadersV2"] = "Purchase orders",
            ["PurchaseOrderLinesV2"] = "Purchase order lines",
            ["SalesInvoiceHeadersV2"] = "Sales invoices",
            ["SalesInvoiceLines"] = "Sales invoice lines",
            ["VendorInvoiceHeaders"] = "Purchase invoices",
            ["VendorInvoiceLines"] = "Purchase invoice lines",
            ["CustomerPaymentJournalHeaders"] = "GL journals — receipt vouchers",
            ["CustomerPaymentJournalLines"] = "GL journal lines — receipt vouchers",
            ["VendorPaymentJournalHeaders"] = "GL journals — payment vouchers",
            ["VendorPaymentJournalLines"] = "GL journal lines — payment vouchers",
            ["LedgerJournalHeaders"] = "GL journals (headers)",
            ["LedgerJournalLines"] = "GL journal lines",
            ["MainAccounts"] = "Chart of accounts",
            ["LegalEntities"] = "Legal entities",
            ["GeneralJournalEntryBiEntities"] = "Posted GL vouchers (headers)",
            ["GeneralJournalAccountEntryBiEntities"] = "Posted GL entries (lines)",
            ["TrialBalanceFiscalYearSnapshots"] = "Trial balance snapshots",
            ["WarehousesOnHandV2"] = "Inventory on-hand by warehouse",
        };

        /// <summary>Rows processed per apply call — stays inside the web request budget; repeat to finish.</summary>
        private const int ApplyBatchSize = 400;

    public async Task<ErpD365ApplyResult> ApplyAsync(DbConnection connection, string entity, long adminId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!MappedEntities.ContainsKey(entity))
        {
            return new ErpD365ApplyResult(false, "no_mapping", $"No staged → master mapping exists for '{entity}' yet.", 0, 0);
        }

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_d365_sync_objects` (" +
            "id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "entity VARCHAR(128) NOT NULL," +
            "ext_key VARCHAR(255) NOT NULL DEFAULT ''," +
            "payload MEDIUMTEXT NOT NULL," +
            "run_id BIGINT NOT NULL DEFAULT 0," +
            "synced_at BIGINT NOT NULL," +
            "UNIQUE KEY uq_entity_ext (entity, ext_key)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_sales_invoices` (" +
            "id INT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "invoice_no VARCHAR(40) NOT NULL," +
            "company VARCHAR(16) NOT NULL DEFAULT ''," +
            "customer_account VARCHAR(40) NOT NULL DEFAULT ''," +
            "customer_name VARCHAR(190) NOT NULL DEFAULT ''," +
            "invoice_date INT NOT NULL DEFAULT 0," +
            "due_date INT NOT NULL DEFAULT 0," +
            "status VARCHAR(24) NOT NULL DEFAULT 'unpaid'," +
            "currency VARCHAR(8) NOT NULL DEFAULT 'AED'," +
            "total_amount DECIMAL(14,2) NOT NULL DEFAULT 0.00," +
            "tax_amount DECIMAL(14,2) NOT NULL DEFAULT 0.00," +
            "admin_id INT NOT NULL DEFAULT 0," +
            "time_created INT NOT NULL DEFAULT 0," +
            "time_updated INT NOT NULL DEFAULT 0," +
            "UNIQUE KEY uq_invoice_no (invoice_no)," +
            "KEY ix_customer (customer_account)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_sales_invoice_lines` (" +
            "id INT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "invoice_id INT NOT NULL," +
            "line_no INT NOT NULL DEFAULT 1," +
            "item_code VARCHAR(64) NOT NULL DEFAULT ''," +
            "description VARCHAR(255) NOT NULL DEFAULT ''," +
            "qty DECIMAL(14,3) NOT NULL DEFAULT 0.000," +
            "unit_price DECIMAL(14,4) NOT NULL DEFAULT 0.0000," +
            "line_amount DECIMAL(14,2) NOT NULL DEFAULT 0.00," +
            "UNIQUE KEY uq_inv_line (invoice_id, line_no, item_code)," +
            "KEY ix_invoice (invoice_id)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_purchase_invoice_lines` (" +
            "id INT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "purchase_id INT NOT NULL," +
            "line_no INT NOT NULL DEFAULT 1," +
            "item_code VARCHAR(64) NOT NULL DEFAULT ''," +
            "description VARCHAR(255) NOT NULL DEFAULT ''," +
            "qty DECIMAL(14,3) NOT NULL DEFAULT 0.000," +
            "unit_cost DECIMAL(14,4) NOT NULL DEFAULT 0.0000," +
            "line_amount DECIMAL(14,2) NOT NULL DEFAULT 0.00," +
            "UNIQUE KEY uq_pinv_line (purchase_id, line_no, item_code)," +
            "KEY ix_purchase (purchase_id)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_d365_object_fields` (" +
            "id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "entity VARCHAR(128) NOT NULL," +
            "ext_key VARCHAR(255) NOT NULL DEFAULT ''," +
            "field_name VARCHAR(191) NOT NULL," +
            "field_value MEDIUMTEXT NULL," +
            "UNIQUE KEY uq_entity_ext_field (entity, ext_key, field_name)," +
            "KEY ix_entity (entity, ext_key)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_contacts` (" +
            "id INT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "party_type VARCHAR(16) NOT NULL DEFAULT 'customer'," +
            "name VARCHAR(160) NOT NULL DEFAULT ''," +
            "company VARCHAR(160) NOT NULL DEFAULT ''," +
            "email VARCHAR(120) NOT NULL DEFAULT ''," +
            "phone VARCHAR(40) NOT NULL DEFAULT ''," +
            "trn VARCHAR(40) NOT NULL DEFAULT ''," +
            "account_code VARCHAR(120) NOT NULL DEFAULT ''," +
            "active TINYINT(1) NOT NULL DEFAULT 1," +
            "time_created INT NOT NULL DEFAULT 0," +
            "time_updated INT NOT NULL DEFAULT 0" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);

        // Ensure every master the mappers write exists on this database — on a fresh tenant
        // the lazy schema has never run, so the first apply must create them.
        await docCycle.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureInventoryAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpSalesWorkspaceSpecs.EnsureSalesAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpPurchasingWorkspaceSpecs.EnsurePurchasingAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpGlChartOfAccountsSeeder.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        // Base GL tables first (gl journals + lines) — the D365 GL service only adds columns and
        // dimension tables on top of them, so a fresh tenant needs the base ensure.
        await glPosting.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await glService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        // Stable D365 identity columns on GL lines — added here (pre-transaction) because ALTER
        // implicitly commits any open MySQL transaction. line_ref carries the D365 line number
        // (stable across amount/note/dimension edits); ext_entity scopes a line to its feed.
        await ErpDb.TryExecuteAsync(connection,
            "ALTER TABLE `epc_erp_gl_lines` ADD `ext_key` varchar(255) NOT NULL DEFAULT ''",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection,
            "ALTER TABLE `epc_erp_gl_lines` ADD `line_ref` varchar(64) NOT NULL DEFAULT ''",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection,
            "ALTER TABLE `epc_erp_gl_lines` ADD `ext_entity` varchar(64) NOT NULL DEFAULT ''",
            cancellationToken).ConfigureAwait(false);

        // Legal entities + business units — dimension targets on journals/lines (fresh-tenant safe).
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_pm_legal_entities` (`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '', `name` varchar(190) NOT NULL DEFAULT '', `country_code` varchar(4) NOT NULL DEFAULT '', `currency_code` varchar(8) NOT NULL DEFAULT '', `trn` varchar(40) NOT NULL DEFAULT '', `note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_le_code` (`code`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_pm_business_units` (`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '', `name` varchar(190) NOT NULL DEFAULT '', `legal_entity_id` int(11) NOT NULL DEFAULT 0, `parent_id` int(11) NOT NULL DEFAULT 0, `manager` varchar(120) NOT NULL DEFAULT '', `note` varchar(255) NOT NULL DEFAULT '', `active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_bu_code` (`code`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection,
            "ALTER TABLE `epc_erp_pm_legal_entities` ADD UNIQUE KEY `ux_le_code` (`code`)",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_pm_cost_centres` (`id` int(11) NOT NULL AUTO_INCREMENT, `code` varchar(40) NOT NULL DEFAULT '', `name` varchar(190) NOT NULL DEFAULT '', `legal_entity_id` int(11) NOT NULL DEFAULT 0, `active` tinyint(1) NOT NULL DEFAULT 1, `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`), UNIQUE KEY `ux_cc_code` (`code`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);

        // Warehouse stock positions pulled from D365 (WarehousesOnHandV2).
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_inv_onhand` (" +
            "id INT NOT NULL AUTO_INCREMENT PRIMARY KEY," +
            "data_area VARCHAR(16) NOT NULL DEFAULT ''," +
            "item_no VARCHAR(64) NOT NULL DEFAULT ''," +
            "product_name VARCHAR(190) NOT NULL DEFAULT ''," +
            "site_id VARCHAR(32) NOT NULL DEFAULT ''," +
            "warehouse_id VARCHAR(48) NOT NULL DEFAULT ''," +
            "onhand_qty DECIMAL(18,4) NOT NULL DEFAULT 0," +
            "avail_qty DECIMAL(18,4) NOT NULL DEFAULT 0," +
            "reserved_qty DECIMAL(18,4) NOT NULL DEFAULT 0," +
            "ordered_qty DECIMAL(18,4) NOT NULL DEFAULT 0," +
            "synced_at BIGINT NOT NULL DEFAULT 0," +
            "UNIQUE KEY uq_onhand (data_area, item_no, site_id, warehouse_id)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            cancellationToken).ConfigureAwait(false);
        // Apply bookkeeping columns on the staging table (outside any transaction — DDL commits).
        await ErpDb.TryExecuteAsync(connection,
            "ALTER TABLE `epc_d365_sync_objects` ADD `applied_at` BIGINT NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(connection,
            "ALTER TABLE `epc_d365_sync_objects` ADD `attempts` INT NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);

        // Batched: rows whose payload changed since last apply (or never applied), least-tried
        // first so persistently failing heads cannot starve the tail.
        var staged = new List<(long Id, string Key, string Payload)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, ext_key, payload FROM epc_d365_sync_objects WHERE entity = @entity AND applied_at < synced_at ORDER BY attempts ASC, id ASC LIMIT " + ApplyBatchSize;
            command.CommandTimeout = 300; // the staging table can exceed a million rows during pulls
            var entityParam = command.CreateParameter();
            entityParam.ParameterName = "@entity";
            entityParam.Value = entity;
            command.Parameters.Add(entityParam);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                staged.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        if (staged.Count == 0)
        {
            var total = Convert.ToInt64(await ErpDb.ScalarAsync(connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM epc_d365_sync_objects WHERE entity = ?"), cancellationToken, entity).ConfigureAwait(false) ?? 0L);
            if (total == 0)
            {
                return new ErpD365ApplyResult(false, "no_staged", $"No staged rows for {entity} — pull the entity first.", 0, 0);
            }
            return new ErpD365ApplyResult(true, "ok", $"{entity}: all {total} staged row(s) already applied.", 0, 0);
        }
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var mapped = 0;
        var skipped = 0;
        var appliedJournalLineRefs = new Dictionary<long, HashSet<string>>();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var (stagedId, extKey, payload) in staged)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(payload);
            }
            catch (JsonException)
            {
                skipped++;
                await ErpDb.ExecuteAsync(connection, transaction,
                    ErpDb.Positional("UPDATE epc_d365_sync_objects SET applied_at = ? WHERE id = ?"),
                    cancellationToken, now, stagedId).ConfigureAwait(false);
                continue;
            }

            using (doc)
            {
                var applied = entity switch
                {
                    "ReleasedProductsV2" => await ApplyProductAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "CustomersV3" => await ApplyCustomerAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "VendorsV2" => await ApplyVendorAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "SalesOrderHeadersV2" => await ApplySalesOrderAsync(connection, transaction, doc.RootElement, adminId, cancellationToken).ConfigureAwait(false),
                    "SalesOrderLines" => await ApplySalesOrderLineAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "PurchaseOrderHeadersV2" => await ApplyPurchaseOrderAsync(connection, transaction, doc.RootElement, adminId, cancellationToken).ConfigureAwait(false),
                    "PurchaseOrderLinesV2" => await ApplyPurchaseOrderLineAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "SalesInvoiceHeadersV2" => await ApplySalesInvoiceAsync(connection, transaction, doc.RootElement, adminId, cancellationToken).ConfigureAwait(false),
                    "SalesInvoiceLines" => await ApplySalesInvoiceLineAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "VendorInvoiceHeaders" => await ApplyPurchaseInvoiceAsync(connection, transaction, doc.RootElement, adminId, cancellationToken).ConfigureAwait(false),
                    "VendorInvoiceLines" => await ApplyPurchaseInvoiceLineAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "CustomerPaymentJournalHeaders" or "VendorPaymentJournalHeaders" or "LedgerJournalHeaders" => await ApplyJournalHeaderAsync(connection, transaction, doc.RootElement, adminId, cancellationToken).ConfigureAwait(false),
                    "CustomerPaymentJournalLines" or "VendorPaymentJournalLines" or "LedgerJournalLines" => await ApplyJournalLineAsync(connection, transaction, doc.RootElement, extKey, entity, appliedJournalLineRefs, cancellationToken).ConfigureAwait(false),
                    "GeneralJournalEntryBiEntities" => await ApplyGlEntryHeaderAsync(connection, transaction, doc.RootElement, adminId, cancellationToken).ConfigureAwait(false),
                    "GeneralJournalAccountEntryBiEntities" => await ApplyGlAccountEntryAsync(connection, transaction, doc.RootElement, extKey, entity, cancellationToken).ConfigureAwait(false),
                    "MainAccounts" => await ApplyMainAccountAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "LegalEntities" => await ApplyLegalEntityAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    "TrialBalanceFiscalYearSnapshots" => true, // compare-card source; fields kept on the staged row
                    "WarehousesOnHandV2" => await ApplyOnHandAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    _ => false,
                };
                if (applied)
                {
                    await StoreFieldsAsync(connection, transaction, entity, extKey, doc.RootElement, cancellationToken).ConfigureAwait(false);
                    mapped++;
                    await ErpDb.ExecuteAsync(connection, transaction,
                        ErpDb.Positional("UPDATE epc_d365_sync_objects SET applied_at = ? WHERE id = ?"),
                        cancellationToken, now, stagedId).ConfigureAwait(false);
                }
                else
                {
                    // Dependency miss (journal/account/master not synced yet) — retry next call;
                    // attempts ordering stops it from starving newer rows.
                    skipped++;
                    await ErpDb.ExecuteAsync(connection, transaction,
                        ErpDb.Positional("UPDATE epc_d365_sync_objects SET attempts = attempts + 1 WHERE id = ?"),
                        cancellationToken, stagedId).ConfigureAwait(false);
                }
            }
        }
        // Journal-line reconcile: an upstream edit changes a line's key, so a staged row's
        // removal must delete the stored GL line. The staged set is authoritative only once its
        // pull finished — reconcile is skipped while the pull cursor is mid-stream, and it joins
        // staged refs by the journal's scoped number (journal_no) so a partial apply batch still
        // sees the complete staged picture. Scoped by ext_entity so other feeds are untouched.
        var pullDone = await ErpDb.LongAsync(connection, transaction,
            ErpDb.Positional("SELECT IFNULL(done,0) FROM epc_d365_pull_cursors WHERE entity = ?"),
            cancellationToken, entity).ConfigureAwait(false);
        if (pullDone == 1)
        {
            foreach (var journalId in appliedJournalLineRefs.Keys)
            {
                var journalScoped = Convert.ToString(await ErpDb.ScalarAsync(connection, transaction,
                    ErpDb.Positional("SELECT `journal_no` FROM `epc_erp_gl_journals` WHERE `id` = ?"),
                    cancellationToken, journalId).ConfigureAwait(false)) ?? string.Empty;
                if (journalScoped.Length == 0)
                {
                    continue;
                }
                var otherFeedKeyed = await ErpDb.LongAsync(connection, transaction,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_gl_lines` WHERE `journal_id` = ? AND `line_ref` <> '' AND `ext_entity` <> ?"),
                    cancellationToken, journalId, Clip(entity, 64)).ConfigureAwait(false) > 0;
                await ErpDb.ExecuteAsync(connection, transaction,
                    "DELETE l FROM `epc_erp_gl_lines` l WHERE l.`journal_id` = ? AND l.`ext_entity` = ? AND l.`line_ref` <> '' " +
                    "AND l.`line_ref` NOT IN (SELECT `line_ref` FROM `epc_d365_sync_objects` WHERE `entity` = ? AND `journal_no` = ? AND `line_ref` <> '')",
                    cancellationToken, journalId, Clip(entity, 64), entity, journalScoped).ConfigureAwait(false);
                if (!otherFeedKeyed)
                {
                    await ErpDb.ExecuteAsync(connection, transaction,
                        ErpDb.Positional("DELETE FROM `epc_erp_gl_lines` WHERE `journal_id` = ? AND `ext_entity` = '' AND `line_ref` = ''"),
                        cancellationToken, journalId).ConfigureAwait(false);
                }
            }
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var remaining = Convert.ToInt64(await ErpDb.ScalarAsync(connection, null,
            ErpDb.Positional("SELECT COUNT(*) FROM epc_d365_sync_objects WHERE entity = ? AND applied_at < synced_at"),
            cancellationToken, entity).ConfigureAwait(false) ?? 0L);
        var label = MappedEntities[entity];
        var message = remaining > 0
            ? $"Applied {mapped} row(s) into {label} ({skipped} deferred — {remaining} staged row(s) remain; apply again)."
            : $"Applied {mapped} row(s) into {label} ({skipped} deferred).";
        return new ErpD365ApplyResult(true, "ok", message, mapped, skipped);
    }

    /// <inheritdoc />
    public async Task<ErpD365TbCompare> TrialBalanceCompareAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        // EcomAE side: posted GL lines per account + dimension string, with debit/credit totals.
        var ours = new Dictionary<string, (string Name, decimal Balance)>(StringComparer.OrdinalIgnoreCase);
        decimal ourDr = 0m, ourCr = 0m;
        var oursSql =
            "SELECT a.`code`, a.`name`, IFNULL(l.`dimension_string`,''), IFNULL(SUM(l.`debit`),0), IFNULL(SUM(l.`credit`),0) " +
            "FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.`id` = l.`coa_id` " +
            "GROUP BY a.`code`, a.`name`, IFNULL(l.`dimension_string`,'')";
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = oursSql;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = reader.GetString(0) + "\u001f" + reader.GetString(2);
                var dr = reader.GetDecimal(3);
                var cr = reader.GetDecimal(4);
                ours[key] = (reader.GetString(1), dr - cr);
                ourDr += dr;
                ourCr += cr;
            }
        }
        catch
        {
            // GL not initialized yet — our side is simply zero.
        }

        // D365 side: prefer staged TrialBalanceFiscalYearSnapshots (the actual F&O TB source —
        // debit/credit/ending balance per dimension value set per period); fall back to
        // GeneralLedgerBalanceCDREntities when snapshots were never pulled.
        var theirs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var snapshotPeriods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var snapshotSyncedAt = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        decimal theirDr = 0m, theirCr = 0m;
        var snapshotCount = Convert.ToInt64(await ErpDb.ScalarAsync(connection, null,
            "SELECT COUNT(*) FROM epc_d365_sync_objects WHERE entity = 'TrialBalanceFiscalYearSnapshots'",
            cancellationToken).ConfigureAwait(false) ?? 0L);
        var tbEntity = snapshotCount > 0 ? "TrialBalanceFiscalYearSnapshots" : "GeneralLedgerBalanceCDREntities";
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT payload, synced_at FROM epc_d365_sync_objects WHERE entity = '{tbEntity}'";
            command.CommandTimeout = 300;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var rowSyncedAt = reader.GetInt64(1);
                JsonDocument doc;
                try { doc = JsonDocument.Parse(reader.GetString(0)); }
                catch (JsonException) { continue; }
                using (doc)
                {
                    if (tbEntity == "TrialBalanceFiscalYearSnapshots")
                    {
                        // Trial-balance snapshot rows: DimensionValue1 = main account, Value2..11 the
                        // financial dimension segments for the hierarchy. Periods are cumulative —
                        // keep the latest period per account+dimension key, "Current" posting layer only.
                        var layer = Str(doc.RootElement, "PostingLayer");
                        if (layer.Length > 0 && !layer.Equals("Current", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        var seg = new List<string>();
                        for (var i = 1; i <= 11; i++)
                        {
                            var v = Str(doc.RootElement, $"DimensionValue{i}");
                            if (v.Length > 0)
                            {
                                seg.Add(v);
                            }
                        }
                        if (seg.Count == 0) continue;
                        var sCode = seg[0];
                        var sDim = string.Join('-', seg);
                        var sDr = Dec(doc.RootElement, "AmountDebit");
                        var sCr = Dec(doc.RootElement, "AmountCredit");
                        var sKey = sCode + "\u001f" + sDim;
                        var sPeriod = Str(doc.RootElement, "PeriodEndDate", "PeriodName", "FiscalPeriod", "Period");
                        // Duplicate staged rows (e.g. keyed differently across connector upgrades)
                        // resolve to the same key — the freshest sync wins within a period.
                        if (!snapshotPeriods.TryGetValue(sKey, out var sExisting) ||
                            string.CompareOrdinal(sPeriod, sExisting) > 0 ||
                            (sPeriod == sExisting && rowSyncedAt >= (snapshotSyncedAt.TryGetValue(sKey, out var lastSync) ? lastSync : 0)))
                        {
                            // Totals count the latest period only — drop the previously included row.
                            if (snapshotPeriods.TryGetValue(sKey, out var prev) && theirs.TryGetValue(sKey, out var prevNet))
                            {
                                var prevDr = prevNet > 0 ? prevNet : 0m;
                                var prevCr = prevNet < 0 ? -prevNet : 0m;
                                theirDr -= prevDr;
                                theirCr -= prevCr;
                            }
                            snapshotPeriods[sKey] = sPeriod;
                            snapshotSyncedAt[sKey] = rowSyncedAt;
                            var sNet = sDr - sCr;
                            theirs[sKey] = sNet;
                            theirDr += sDr;
                            theirCr += sCr;
                        }
                        continue;
                    }

                    var account = Str(doc.RootElement, "MainAccountId", "MainAccount", "LedgerDimension", "AccountDisplayValue", "Dimension");
                    if (account.Length == 0) continue;
                    var code = account.Split('-', '|')[0].Trim();
                    // Key on the same ledger-dimension display string ApplyJournalLineAsync stores in
                    // dimension_string, so both sides compare under identical keys.
                    var dimension = Str(doc.RootElement, "LedgerDimension", "AccountDisplayValue", "DimensionDisplayValue", "DefaultDimensionDisplayValue", "Dimension", "DimensionSet");
                    var debit = Dec(doc.RootElement, "AccountingCurrencyAmountDebit", "AmountCurDebit", "DebitAmount", "Debit");
                    var credit = Dec(doc.RootElement, "AccountingCurrencyAmountCredit", "AmountCurCredit", "CreditAmount", "Credit");
                    var isSnapshot = debit == 0m && credit == 0m;
                    if (isSnapshot)
                    {
                        // Balance entities (CDR balances, snapshots) carry a signed net amount.
                        var amount = Dec(doc.RootElement, "AccountingCurrencyAmount", "Balance", "BalanceAmount", "ClosingBalance", "Amount", "Total");
                        if (amount >= 0) { debit = amount; } else { credit = -amount; }
                    }
                    var net = debit - credit;
                    var key = code + "\u001f" + dimension;
                    theirDr += debit;
                    theirCr += credit;
                    // Period snapshots are cumulative: keep the latest period's balance rather than
                    // summing closing balances across periods (Jan 100 + Feb 140 = 140, not 240).
                    var period = Str(doc.RootElement, "FiscalPeriod", "Period", "FiscalYear", "AccountingPeriod");
                    if (isSnapshot)
                    {
                        if (!snapshotPeriods.TryGetValue(key, out var existingPeriod) ||
                            string.CompareOrdinal(period, existingPeriod) > 0 ||
                            (period == existingPeriod && rowSyncedAt >= (snapshotSyncedAt.TryGetValue(key, out var prevSync) ? prevSync : 0)))
                        {
                            snapshotPeriods[key] = period;
                            snapshotSyncedAt[key] = rowSyncedAt;
                            theirs[key] = net;
                        }
                    }
                    else
                    {
                        theirs[key] = theirs.TryGetValue(key, out var v) ? v + net : net;
                    }
                }
            }
        }

        var names = ours.GroupBy(k => k.Key.Split('\u001f')[0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value.Name, StringComparer.OrdinalIgnoreCase);
        var rows = new List<ErpD365TbCompareRow>();
        foreach (var key in ours.Keys.Union(theirs.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var parts = key.Split('\u001f');
            var code = parts[0];
            var dim = parts.Length > 1 ? parts[1] : string.Empty;
            var ourRow = ours.TryGetValue(key, out var o) ? o : (Name: names.TryGetValue(code, out var n) ? n : code, Balance: 0m);
            var theirBalance = theirs.TryGetValue(key, out var t) ? t : 0m;
            rows.Add(new ErpD365TbCompareRow(code, ourRow.Name, dim, ourRow.Balance, theirBalance));
        }
        return new ErpD365TbCompare(rows, ourDr, ourCr, theirDr, theirCr);
    }

    /// <summary>
    /// Inventory on-hand compare: our stock positions (epc_erp_inv_stock) vs D365 WarehousesOnHandV2
    /// (epc_erp_inv_onhand, applied). Warehouse key join is on the warehouse CODE — our warehouses are
    /// matched to their D365 id via epc_erp_inv_warehouses.code when present, else the numeric id.
    /// </summary>
    public async Task<IReadOnlyList<ErpD365InvCompareRow>> InventoryCompareAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        var rows = new List<ErpD365InvCompareRow>();
        // D365 side: on-hand per warehouse code (from the applied positions table).
        var theirs = new Dictionary<string, (decimal Qty, decimal Avail)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT `warehouse_id`, IFNULL(SUM(`onhand_qty`),0), IFNULL(SUM(`avail_qty`),0) FROM `epc_erp_inv_onhand` GROUP BY `warehouse_id`";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    theirs[reader.GetString(0)] = (reader.GetDecimal(1), reader.GetDecimal(2));
                }
            }
        }
        catch { /* table absent until first WarehousesOnHandV2 apply */ }
        // Ours: stock per warehouse — resolve warehouse code from epc_erp_inv_warehouses when it exists.
        var ours = new Dictionary<string, (decimal Qty, decimal Value)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var hasWh = await ErpDb.ColumnKnownAsync(connection, "epc_erp_inv_warehouses", "code", cancellationToken).ConfigureAwait(false);
            var whJoin = hasWh
                ? "LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = s.`warehouse_id` "
                : "";
            var keyExpr = hasWh ? "IFNULL(NULLIF(w.`code`,''), s.`warehouse_id`)" : "s.`warehouse_id`";
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    $"SELECT {keyExpr}, IFNULL(SUM(s.`qty_on_hand`),0), IFNULL(SUM(s.`qty_on_hand` * s.`avg_unit_cost`),0) " +
                    $"FROM `epc_erp_inv_stock` s {whJoin}GROUP BY {keyExpr}";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var key = reader.IsDBNull(0) ? "0" : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "0";
                    ours[key] = (reader.GetDecimal(1), reader.GetDecimal(2));
                }
            }
        }
        catch { /* stock table absent on a fresh tenant */ }

        foreach (var key in ours.Keys.Union(theirs.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var o = ours.TryGetValue(key, out var ov) ? ov : (Qty: 0m, Value: 0m);
            var t = theirs.TryGetValue(key, out var tv) ? tv : (Qty: 0m, Avail: 0m);
            rows.Add(new ErpD365InvCompareRow(key, o.Qty, o.Value, t.Qty, t.Avail));
        }
        return rows;
    }

    /// <summary>Stores every F&O payload field for a staged row in epc_d365_object_fields (one row per field) so the module screens can display the complete field set, not just the mapped columns.</summary>
    private static async Task StoreFieldsAsync(DbConnection connection, DbTransaction tx, string entity, string extKey, JsonElement row, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(connection, tx,
            ErpDb.Positional("DELETE FROM `epc_d365_object_fields` WHERE `entity` = ? AND `ext_key` = ?"),
            ct, entity, extKey).ConfigureAwait(false);
        // Multi-row INSERT — a released product carries ~326 fields, one round-trip
        // per field made applies take minutes at 400-row batches.
        var marks = new List<string>();
        var tuples = new List<object?>();
        foreach (var property in row.EnumerateObject())
        {
            var text = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.GetRawText();
            marks.Add("(?, ?, ?, ?)");
            tuples.Add(entity);
            tuples.Add(extKey);
            tuples.Add(Clip(property.Name, 191));
            tuples.Add(Clip(text, 60000));
        }
        for (var off = 0; off < marks.Count; off += 200)
        {
            var take = Math.Min(200, marks.Count - off);
            await ErpDb.ExecuteAsync(connection, tx,
                "INSERT INTO `epc_d365_object_fields` (`entity`, `ext_key`, `field_name`, `field_value`) VALUES " + string.Join(", ", marks.GetRange(off, take)),
                ct, tuples.GetRange(off * 4, take * 4).ToArray()).ConfigureAwait(false);
        }
    }

    /// <summary>Company-scoped master key: F&amp;O dataAreaId prefixes the natural key so two companies never overwrite each other's records (mirrors the staging key's company|key shape). When the combined key exceeds the column width, a short hash suffix keeps it unique instead of colliding after truncation. Shared masters (COA, global parties) stay unscoped.</summary>
    private static string ScopedKey(JsonElement row, string naturalKey, int max)
    {
        var company = Str(row, "dataAreaId", "CompanyId");
        var scoped = company.Length > 0 ? company + "|" + naturalKey : naturalKey;
        if (scoped.Length <= max)
        {
            return scoped;
        }
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scoped)))[..8].ToLowerInvariant();
        var room = max - hash.Length - 1;
        return room > 0 ? scoped[..room] + "-" + hash : hash;
    }

    // ---- field helpers ----

    private static string Str(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString() ?? string.Empty;
                    if (text.Trim().Length > 0)
                    {
                        return text.Trim();
                    }
                }
                else if (value.ValueKind == JsonValueKind.Number)
                {
                    // F&O sends identifiers like LineNumber as JSON numbers — keep them usable.
                    return value.GetRawText();
                }
            }
        }
        return string.Empty;
    }

    private static decimal Dec(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (!row.TryGetProperty(name, out var value))
            {
                continue;
            }
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var n))
            {
                return n;
            }
            if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s))
            {
                return s;
            }
        }
        return 0m;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    // ---- per-entity mappers ----

    /// <summary>ReleasedProductsV2 → epc_erp_inv_items (upsert by sku).</summary>
    private static async Task<bool> ApplyProductAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var naturalSku = Str(row, "ItemNumber", "ItemId", "ProductNumber");
        var sku = ScopedKey(row, naturalSku, 64);
        if (sku.Length == 0 || sku[0] == '|' || sku[^1] == '|')
        {
            return false;
        }

        // Upgrade rows applied before company-scoping: rewrite the unscoped key in place
        // so re-apply refreshes rather than duplicating.
        if (naturalSku.Length > 0 && !string.Equals(naturalSku, sku, StringComparison.Ordinal))
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_inv_items` SET `sku` = ? WHERE `sku` = ?"), ct, sku, naturalSku).ConfigureAwait(false);
        }

        var name = Str(row, "ASAP_ProductName", "ProductName", "ASAP_SearchName", "SearchName", "ItemDescription");
        if (name.Length == 0)
        {
            name = sku;
        }

        var unit = Str(row, "PrimaryUnitSymbol", "InventoryUnitSymbol", "UnitId");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_inv_items` (`sku`, `name`, `unit`, `active`, `time_created`) " +
            "VALUES (?, ?, ?, 1, ?) ON DUPLICATE KEY UPDATE `name` = VALUES(`name`)",
            ct, Clip(sku, 64), Clip(name, 255), Clip(unit.Length > 0 ? unit : "pcs", 16), now).ConfigureAwait(false);
        return true;
    }

    /// <summary>CustomersV3 → epc_erp_contacts (party_type='customer') — the real customer master the ERP workflows read.</summary>
    private static async Task<bool> ApplyCustomerAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var name = Str(row, "OrganizationName", "CustomerName", "Name", "CustomerAccount");
        if (name.Length == 0)
        {
            return false;
        }

        var account = Str(row, "CustomerAccount", "AccountNum");
        var email = Str(row, "PrimaryContactEmail", "PrimaryContactEmailAddress", "InvoiceEmail");
        var phone = Str(row, "PrimaryContactPhone", "PrimaryContactTelephone");
        var trn = Str(row, "TaxExemptNumber", "VATNum", "TaxRegistrationNumber");
        var company = Str(row, "dataAreaId", "CompanyId");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var hasEmail = await ErpDb.ColumnKnownAsync(connection, "epc_erp_contacts", "email", ct, tx).ConfigureAwait(false);
        var hasPhone = await ErpDb.ColumnKnownAsync(connection, "epc_erp_contacts", "phone", ct, tx).ConfigureAwait(false);
        var hasCompany = await ErpDb.ColumnKnownAsync(connection, "epc_erp_contacts", "company", ct, tx).ConfigureAwait(false);
        var hasTrn = await ErpDb.ColumnKnownAsync(connection, "epc_erp_contacts", "trn", ct, tx).ConfigureAwait(false);
        var hasAccountCode = await ErpDb.ColumnKnownAsync(connection, "epc_erp_contacts", "account_code", ct, tx).ConfigureAwait(false);

        // Upsert by customer account when available (global party identity), else by name.
        var existing = hasAccountCode && account.Length > 0
            ? await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_contacts` WHERE `party_type` = 'customer' AND `account_code` = ? LIMIT 1"), ct, account).ConfigureAwait(false)
            : await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_contacts` WHERE `party_type` = 'customer' AND `name` = ? LIMIT 1"), ct, name).ConfigureAwait(false);
        if (existing > 0)
        {
            var sets = new List<string>();
            var vals = new List<object?>();
            if (hasEmail) { sets.Add("`email` = ?"); vals.Add(Clip(email, 120)); }
            if (hasPhone) { sets.Add("`phone` = ?"); vals.Add(Clip(phone, 40)); }
            if (hasTrn) { sets.Add("`trn` = ?"); vals.Add(Clip(trn, 40)); }
            if (hasCompany && company.Length > 0) { sets.Add("`company` = ?"); vals.Add(Clip(company, 160)); }
            sets.Add("`name` = ?"); vals.Add(Clip(name, 160));
            sets.Add("`time_updated` = ?"); vals.Add(now);
            vals.Add(existing);
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_contacts` SET " + string.Join(", ", sets) + " WHERE `id` = ?"),
                ct, vals.ToArray()).ConfigureAwait(false);
        }
        else
        {
            var cols = new List<string> { "`party_type`", "`name`" };
            var marks = new List<string> { "'customer'", "?" };
            var vals = new List<object?> { Clip(name, 160) };
            if (hasAccountCode) { cols.Add("`account_code`"); marks.Add("?"); vals.Add(Clip(account, 120)); }
            if (hasEmail) { cols.Add("`email`"); marks.Add("?"); vals.Add(Clip(email, 120)); }
            if (hasPhone) { cols.Add("`phone`"); marks.Add("?"); vals.Add(Clip(phone, 40)); }
            if (hasTrn) { cols.Add("`trn`"); marks.Add("?"); vals.Add(Clip(trn, 40)); }
            if (hasCompany) { cols.Add("`company`"); marks.Add("?"); vals.Add(Clip(company, 160)); }
            cols.Add("`active`"); marks.Add("1");
            cols.Add("`time_created`"); marks.Add("?"); vals.Add(now);
            cols.Add("`time_updated`"); marks.Add("?"); vals.Add(now);
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("INSERT INTO `epc_erp_contacts` (" + string.Join(", ", cols) + ") VALUES (" + string.Join(", ", marks) + ")"),
                ct, vals.ToArray()).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>VendorsV2 → epc_erp_suppliers (upsert by vendor_account).</summary>
    private static async Task<bool> ApplyVendorAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var name = Str(row, "VendorOrganizationName", "VendorName", "Name", "VendorAccountNumber");
        var account = Str(row, "VendorAccountNumber", "VendorAccount", "AccountNum");
        if (name.Length == 0)
        {
            return false;
        }

        var email = Str(row, "PrimaryContactEmail", "PrimaryContactEmailAddress");
        var phone = Str(row, "PrimaryContactPhone", "PrimaryContactTelephone");
        var trn = Str(row, "TaxExemptNumber", "VATNum", "TaxRegistrationNumber");
        var currency = Str(row, "CurrencyCode", "Currency");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var hasVendorAccount = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "vendor_account", ct, tx).ConfigureAwait(false);
        var hasCurrency = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "currency_code", ct, tx).ConfigureAwait(false);
        var hasTrn = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "trn", ct, tx).ConfigureAwait(false);
        var hasPhone = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "contact_phone", ct, tx).ConfigureAwait(false);
        var existing = hasVendorAccount && account.Length > 0
            ? await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_suppliers` WHERE `vendor_account` = ? LIMIT 1"), ct, account).ConfigureAwait(false)
            : await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_suppliers` WHERE `name` = ? LIMIT 1"), ct, name).ConfigureAwait(false);
        if (existing > 0)
        {
            var sets = new List<string> { "`contact_email` = ?" };
            var vals = new List<object?> { Clip(email, 255) };
            if (hasPhone) { sets.Add("`contact_phone` = ?"); vals.Add(Clip(phone, 64)); }
            if (hasTrn) { sets.Add("`trn` = ?"); vals.Add(Clip(trn, 64)); }
            if (hasCurrency) { sets.Add("`currency_code` = ?"); vals.Add(Clip(currency.Length > 0 ? currency : "AED", 8)); }
            vals.Add(existing);
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_suppliers` SET " + string.Join(", ", sets) + " WHERE `id` = ?"),
                ct, vals.ToArray()).ConfigureAwait(false);
        }
        else
        {
            var cols = new List<string> { "`name`", "`contact_email`" };
            var marks = new List<string> { "?", "?" };
            var vals = new List<object?> { Clip(name, 255), Clip(email, 255) };
            if (hasPhone) { cols.Add("`contact_phone`"); marks.Add("?"); vals.Add(Clip(phone, 64)); }
            if (hasTrn) { cols.Add("`trn`"); marks.Add("?"); vals.Add(Clip(trn, 64)); }
            if (hasCurrency) { cols.Add("`currency_code`"); marks.Add("?"); vals.Add(Clip(currency.Length > 0 ? currency : "AED", 8)); }
            if (hasVendorAccount) { cols.Add("`vendor_account`"); marks.Add("?"); vals.Add(Clip(account, 32)); }
            cols.Add("`active`"); marks.Add("1");
            cols.Add("`time_created`"); marks.Add("?"); vals.Add(now);
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("INSERT INTO `epc_erp_suppliers` (" + string.Join(", ", cols) + ") VALUES (" + string.Join(", ", marks) + ")"),
                ct, vals.ToArray()).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>SalesOrderHeadersV2 → epc_erp_sales_orders (upsert by so_no).</summary>
    private static async Task<bool> ApplySalesOrderAsync(DbConnection connection, DbTransaction tx, JsonElement row, long adminId, CancellationToken ct)
    {
        var naturalSoNo = Str(row, "SalesOrderNumber", "SalesOrderId", "OrderId");
        var soNo = ScopedKey(row, naturalSoNo, 32);
        if (soNo.Length == 0 || soNo[^1] == '|')
        {
            return false;
        }

        var statusRaw = Str(row, "SalesOrderStatus", "DocumentStatus");
        var status = statusRaw switch
        {
            "Invoiced" => "invoiced",
            "Canceled" or "Cancelled" => "cancelled",
            "Delivered" or "OpenOrder" => "confirmed",
            _ => "draft",
        };
        var title = Str(row, "OrderingCustomerAccountNumber", "InvoiceCustomerAccountNumber", "CustomersOrderingCustomerName");
        var total = Dec(row, "TotalAmount", "OrderTotalAmount", "InvoiceAmount", "SalesOrderBalance");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (naturalSoNo.Length > 0 && !string.Equals(naturalSoNo, soNo, StringComparison.Ordinal))
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_sales_orders` SET `so_no` = ? WHERE `so_no` = ?"), ct, soNo, naturalSoNo).ConfigureAwait(false);
        }
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_sales_orders` (`so_no`, `title`, `total_amount`, `status`, `admin_id`, `time_created`, `time_updated`) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `title` = VALUES(`title`), `total_amount` = VALUES(`total_amount`), `status` = VALUES(`status`), `time_updated` = VALUES(`time_updated`)",
            ct, Clip(soNo, 32), Clip(title, 255), total, status, adminId, now, now).ConfigureAwait(false);
        return true;
    }

    /// <summary>PurchaseOrderHeadersV2 → epc_erp_purchase_orders (upsert by po_no).</summary>
    private static async Task<bool> ApplyPurchaseOrderAsync(DbConnection connection, DbTransaction tx, JsonElement row, long adminId, CancellationToken ct)
    {
        var naturalPoNo = Str(row, "PurchaseOrderNumber", "PurchaseOrderId", "OrderNumber");
        var poNo = ScopedKey(row, naturalPoNo, 32);
        if (poNo.Length == 0 || poNo[^1] == '|')
        {
            return false;
        }

        var statusRaw = Str(row, "PurchaseOrderStatus", "DocumentStatus");
        var status = statusRaw switch
        {
            "Received" => "received",
            "Canceled" or "Cancelled" => "cancelled",
            "Approved" or "Confirmed" => "approved",
            "ReceivedAndInvoiced" => "received",
            _ => "draft",
        };
        var title = Str(row, "VendorAccountNumber", "OrderVendorAccountNumber", "VendorName");
        var total = Dec(row, "TotalAmount", "OrderTotalAmount", "InvoiceAmount");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (naturalPoNo.Length > 0 && !string.Equals(naturalPoNo, poNo, StringComparison.Ordinal))
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_purchase_orders` SET `po_no` = ? WHERE `po_no` = ?"), ct, poNo, naturalPoNo).ConfigureAwait(false);
        }
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_purchase_orders` (`po_no`, `title`, `total_amount`, `status`, `admin_id`, `time_created`, `time_updated`) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `title` = VALUES(`title`), `total_amount` = VALUES(`total_amount`), `status` = VALUES(`status`), `time_updated` = VALUES(`time_updated`)",
            ct, Clip(poNo, 32), Clip(title, 255), total, status, adminId, now, now).ConfigureAwait(false);
        return true;
    }

    /// <summary>MainAccounts → epc_erp_coa_accounts (upsert by code).</summary>
    private static async Task<bool> ApplyMainAccountAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var code = Str(row, "MainAccountId", "MainAccountRecId", "AccountCode", "AccountNum");
        var name = Str(row, "Name", "AccountName", "Description");
        if (code.Length == 0)
        {
            return false;
        }
        if (name.Length == 0)
        {
            name = code;
        }

        var typeRaw = Str(row, "MainAccountType", "Type");
        var (accountType, normalSide) = typeRaw switch
        {
            "Asset" => ("asset", "debit"),
            "Liability" => ("liability", "credit"),
            "Equity" => ("equity", "credit"),
            "Revenue" => ("revenue", "credit"),
            _ => ("expense", "debit"),
        };
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_coa_accounts` (`code`, `name`, `account_type`, `normal_side`, `active`, `time_created`) " +
            "VALUES (?, ?, ?, ?, 1, ?) ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `account_type` = VALUES(`account_type`), `normal_side` = VALUES(`normal_side`)",
            ct, Clip(code, 16), Clip(name, 255), accountType, normalSide, now).ConfigureAwait(false);
        return true;
    }

    /// <summary>SalesOrderLinesV2 → epc_erp_sales_order_lines (parent via company-scoped so_no).</summary>
    private static async Task<bool> ApplySalesOrderLineAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var soNo = ScopedKey(row, Str(row, "SalesOrderNumber", "SalesOrderId"), 32);
        if (soNo.Length == 0 || soNo[^1] == '|')
        {
            return false;
        }
        var soId = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_sales_orders` WHERE `so_no` = ? LIMIT 1"), ct, soNo).ConfigureAwait(false);
        if (soId <= 0)
        {
            return false; // header not applied yet — re-run after headers land
        }

        var itemCode = ScopedKey(row, Str(row, "ItemNumber", "ItemId"), 64);
        var description = Str(row, "LineDescription", "ProductName", "ASAP_ProductName", "Description");
        var qty = Dec(row, "OrderedSalesQuantity", "SalesQuantity", "Quantity");
        var unitPrice = Dec(row, "SalesPrice", "UnitPrice");
        var lineAmount = Dec(row, "LineAmount", "LineAmountTax", "NetAmount");
        var lineNo = (int)Dec(row, "LineNumber", "LineNum");
        if (lineNo <= 0) lineNo = 1;
        if (lineAmount == 0m) lineAmount = qty * unitPrice;

        var existing = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_sales_order_lines` WHERE `sales_order_id` = ? AND `line_no` = ? AND `item_code` = ? LIMIT 1"),
            ct, soId, lineNo, Clip(itemCode, 32)).ConfigureAwait(false);
        if (existing > 0)
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_sales_order_lines` SET `description` = ?, `qty` = ?, `unit_price_ex_vat` = ?, `line_ex_vat` = ? WHERE `id` = ?"),
                ct, Clip(description, 255), qty, unitPrice, lineAmount, existing).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("INSERT INTO `epc_erp_sales_order_lines` (`sales_order_id`, `line_no`, `item_code`, `description`, `qty`, `unit_price_ex_vat`, `line_ex_vat`) VALUES (?, ?, ?, ?, ?, ?, ?)"),
                ct, soId, lineNo, Clip(itemCode, 32), Clip(description, 255), qty, unitPrice, lineAmount).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>PurchaseOrderLinesV2 → epc_erp_po_lines (parent via company-scoped po_no).</summary>
    private static async Task<bool> ApplyPurchaseOrderLineAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var poNo = ScopedKey(row, Str(row, "PurchaseOrderNumber", "PurchaseOrderId"), 32);
        if (poNo.Length == 0 || poNo[^1] == '|')
        {
            return false;
        }
        var poId = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_purchase_orders` WHERE `po_no` = ? LIMIT 1"), ct, poNo).ConfigureAwait(false);
        if (poId <= 0)
        {
            return false;
        }

        var itemCode = ScopedKey(row, Str(row, "ItemNumber", "ItemId"), 64);
        var description = Str(row, "LineDescription", "ProductName", "ASAP_ProductName", "Description");
        var qty = Dec(row, "OrderedPurchaseQuantity", "PurchaseQuantity", "Quantity");
        var unitCost = Dec(row, "PurchasePrice", "UnitPrice", "CostPrice");
        var lineAmount = Dec(row, "LineAmount", "NetAmount");
        var qtyReceived = Dec(row, "ReceivedQuantity", "ReceivedPurchaseQuantity");
        var lineNo = (int)Dec(row, "LineNumber", "LineNum");
        if (lineNo <= 0) lineNo = 1;
        if (lineAmount == 0m) lineAmount = qty * unitCost;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var existing = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_po_lines` WHERE `po_id` = ? AND `line_no` = ? AND `item_code` = ? LIMIT 1"),
            ct, poId, lineNo, Clip(itemCode, 32)).ConfigureAwait(false);
        if (existing > 0)
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_po_lines` SET `description` = ?, `qty` = ?, `unit_cost_ex_vat` = ?, `line_ex_vat` = ?, `qty_received` = ?, `time_updated` = ? WHERE `id` = ?"),
                ct, Clip(description, 255), qty, unitCost, lineAmount, qtyReceived, now, existing).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("INSERT INTO `epc_erp_po_lines` (`po_id`, `line_no`, `item_code`, `description`, `qty`, `unit_cost_ex_vat`, `line_ex_vat`, `qty_received`, `time_updated`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                ct, poId, lineNo, Clip(itemCode, 32), Clip(description, 255), qty, unitCost, lineAmount, qtyReceived, now).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>SalesInvoiceHeadersV2 → epc_erp_sales_invoices (upsert by company-scoped invoice_no).</summary>
    private static async Task<bool> ApplySalesInvoiceAsync(DbConnection connection, DbTransaction tx, JsonElement row, long adminId, CancellationToken ct)
    {
        var naturalNo = Str(row, "InvoiceNumber", "InvoiceId", "SalesInvoiceNumber");
        var invoiceNo = ScopedKey(row, naturalNo, 40);
        if (invoiceNo.Length == 0 || invoiceNo[^1] == '|')
        {
            return false;
        }

        var customerAccount = Str(row, "CustomerAccount", "InvoiceCustomerAccountNumber", "OrderingCustomerAccountNumber");
        var customerName = Str(row, "InvoiceCustomerName", "CustomerName", "OrderingCustomerName");
        var company = Str(row, "dataAreaId", "CompanyId");
        var total = Dec(row, "TotalInvoiceAmount", "InvoiceAmount", "TotalAmount");
        var tax = Dec(row, "SalesTaxAmount", "TaxAmount", "TotalTaxAmount");
        var currency = Str(row, "CurrencyCode", "Currency");
        var invoiceDate = DateOf(row, "InvoiceDate", "DocumentDate", "PostingDate");
        var dueDate = DateOf(row, "DueDate");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (naturalNo.Length > 0 && !string.Equals(naturalNo, invoiceNo, StringComparison.Ordinal))
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_sales_invoices` SET `invoice_no` = ? WHERE `invoice_no` = ?"), ct, invoiceNo, naturalNo).ConfigureAwait(false);
        }
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_sales_invoices` (`invoice_no`, `company`, `customer_account`, `customer_name`, `invoice_date`, `due_date`, `status`, `currency`, `total_amount`, `tax_amount`, `admin_id`, `time_created`, `time_updated`) " +
            "VALUES (?, ?, ?, ?, ?, ?, 'posted', ?, ?, ?, ?, ?, ?) " +
            "ON DUPLICATE KEY UPDATE `customer_account` = VALUES(`customer_account`), `customer_name` = VALUES(`customer_name`), `invoice_date` = VALUES(`invoice_date`), `due_date` = VALUES(`due_date`), `total_amount` = VALUES(`total_amount`), `tax_amount` = VALUES(`tax_amount`), `time_updated` = VALUES(`time_updated`)",
            ct, invoiceNo, Clip(company, 16), Clip(customerAccount, 40), Clip(customerName, 190), invoiceDate, dueDate,
            Clip(currency.Length > 0 ? currency : "AED", 8), total, tax, adminId, now, now).ConfigureAwait(false);
        return true;
    }

    /// <summary>SalesInvoiceLinesV2 → epc_erp_sales_invoice_lines (parent via company-scoped invoice_no).</summary>
    private static async Task<bool> ApplySalesInvoiceLineAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var invoiceNo = ScopedKey(row, Str(row, "InvoiceNumber", "InvoiceId", "SalesInvoiceNumber"), 40);
        if (invoiceNo.Length == 0 || invoiceNo[^1] == '|')
        {
            return false;
        }
        var invId = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_sales_invoices` WHERE `invoice_no` = ? LIMIT 1"), ct, invoiceNo).ConfigureAwait(false);
        if (invId <= 0)
        {
            return false;
        }

        var itemCode = ScopedKey(row, Str(row, "ItemNumber", "ItemId"), 64);
        var description = Str(row, "LineDescription", "ProductName", "ASAP_ProductName", "Description");
        var qty = Dec(row, "InvoiceQuantity", "Quantity", "InvoicedQuantity");
        var unitPrice = Dec(row, "SalesPrice", "UnitPrice", "InvoicePrice");
        var lineAmount = Dec(row, "LineAmount", "NetAmount", "LineAmountTax");
        var lineNo = (int)Dec(row, "LineNumber", "LineNum");
        if (lineNo <= 0) lineNo = 1;
        if (lineAmount == 0m) lineAmount = qty * unitPrice;

        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_sales_invoice_lines` (`invoice_id`, `line_no`, `item_code`, `description`, `qty`, `unit_price`, `line_amount`) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `description` = VALUES(`description`), `qty` = VALUES(`qty`), `unit_price` = VALUES(`unit_price`), `line_amount` = VALUES(`line_amount`)",
            ct, invId, lineNo, Clip(itemCode, 64), Clip(description, 255), qty, unitPrice, lineAmount).ConfigureAwait(false);
        return true;
    }

    /// <summary>VendorInvoiceHeaders → epc_erp_purchases (upsert by company-scoped invoice_number).</summary>
    private static async Task<bool> ApplyPurchaseInvoiceAsync(DbConnection connection, DbTransaction tx, JsonElement row, long adminId, CancellationToken ct)
    {
        var naturalNo = Str(row, "InvoiceNumber", "InvoiceId", "VendorInvoiceNumber", "Number");
        var invoiceNo = ScopedKey(row, naturalNo, 64);
        if (invoiceNo.Length == 0 || invoiceNo[^1] == '|')
        {
            return false;
        }

        var vendorAccount = Str(row, "VendorAccount", "VendorAccountNumber", "InvoiceVendorAccountNumber");
        var total = Dec(row, "InvoiceAmount", "TotalAmount", "TotalInvoiceAmount");
        var tax = Dec(row, "SalesTaxAmount", "TaxAmount");
        var purchaseDate = DateOf(row, "InvoiceDate", "DocumentDate", "PostingDate");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var existing = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_purchases` WHERE `invoice_number` = ? LIMIT 1"), ct, invoiceNo).ConfigureAwait(false);
        if (existing > 0)
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_purchases` SET `amount_ex_vat` = ?, `vat_amount` = ?, `total_amount` = ?, `purchase_date` = ? WHERE `id` = ?"),
                ct, total - tax, tax, total, purchaseDate, existing).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("INSERT INTO `epc_erp_purchases` (`invoice_number`, `purchase_date`, `amount_ex_vat`, `vat_amount`, `total_amount`, `status`, `note`, `admin_id`, `time_created`) VALUES (?, ?, ?, ?, ?, 'confirmed', ?, ?, ?)"),
                ct, invoiceNo, purchaseDate, total - tax, tax, total, Clip("D365 vendor " + vendorAccount, 500), adminId, now).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>VendorInvoiceLines → epc_erp_purchase_invoice_lines (parent via invoice_number).</summary>
    private static async Task<bool> ApplyPurchaseInvoiceLineAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var invoiceNo = ScopedKey(row, Str(row, "InvoiceNumber", "InvoiceId", "VendorInvoiceNumber"), 64);
        if (invoiceNo.Length == 0 || invoiceNo[^1] == '|')
        {
            return false;
        }
        var purchaseId = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_purchases` WHERE `invoice_number` = ? LIMIT 1"), ct, invoiceNo).ConfigureAwait(false);
        if (purchaseId <= 0)
        {
            return false;
        }

        var itemCode = ScopedKey(row, Str(row, "ItemNumber", "ItemId"), 64);
        var description = Str(row, "LineDescription", "ProductName", "ASAP_ProductName", "Description");
        var qty = Dec(row, "InvoiceQuantity", "Quantity", "InvoicedQuantity");
        var unitCost = Dec(row, "PurchasePrice", "UnitPrice", "CostPrice", "NetUnitPrice");
        var lineAmount = Dec(row, "LineAmount", "NetAmount");
        var lineNo = (int)Dec(row, "LineNumber", "LineNum");
        if (lineNo <= 0) lineNo = 1;
        if (lineAmount == 0m) lineAmount = qty * unitCost;

        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_purchase_invoice_lines` (`purchase_id`, `line_no`, `item_code`, `description`, `qty`, `unit_cost`, `line_amount`) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?) ON DUPLICATE KEY UPDATE `description` = VALUES(`description`), `qty` = VALUES(`qty`), `unit_cost` = VALUES(`unit_cost`), `line_amount` = VALUES(`line_amount`)",
            ct, purchaseId, lineNo, Clip(itemCode, 64), Clip(description, 255), qty, unitCost, lineAmount).ConfigureAwait(false);
        return true;
    }

    /// <summary>LedgerJournalHeaders → epc_erp_gl_journals (upsert by company-scoped journal_no).</summary>
    private static async Task<bool> ApplyJournalHeaderAsync(DbConnection connection, DbTransaction tx, JsonElement row, long adminId, CancellationToken ct)
    {
        var naturalNo = Str(row, "JournalNumber", "JournalBatchNumber", "JournalId");
        var journalNo = ScopedKey(row, naturalNo, 32);
        if (journalNo.Length == 0 || journalNo[^1] == '|')
        {
            return false;
        }

        var journalType = Str(row, "JournalType", "JournalName", "LedgerJournalType");
        var sourceType = journalType.Contains("Payment", StringComparison.OrdinalIgnoreCase) ? "payment"
            : journalType.Contains("InvoiceRegister", StringComparison.OrdinalIgnoreCase) ? "purchase"
            : "manual";
        var reference = Str(row, "Description", "JournalName", "Name");
        var journalDate = DateOf(row, "Date", "JournalDate", "PostingDate");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var companyId = await ResolveCompanyIdAsync(connection, tx, row, ct).ConfigureAwait(false);

        if (naturalNo.Length > 0 && !string.Equals(naturalNo, journalNo, StringComparison.Ordinal))
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_gl_journals` SET `journal_no` = ? WHERE `journal_no` = ?"), ct, journalNo, naturalNo).ConfigureAwait(false);
        }
        var hasCompany = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_journals", "company_id", ct, tx).ConfigureAwait(false);
        if (hasCompany)
        {
            await ErpDb.ExecuteAsync(connection, tx,
                "INSERT INTO `epc_erp_gl_journals` (`journal_no`, `journal_date`, `reference`, `source_type`, `source_id`, `company_id`, `admin_id`, `time_created`) " +
                "VALUES (?, ?, ?, ?, 0, ?, ?, ?) ON DUPLICATE KEY UPDATE `journal_date` = VALUES(`journal_date`), `reference` = VALUES(`reference`), `source_type` = VALUES(`source_type`), `company_id` = VALUES(`company_id`)",
                ct, journalNo, journalDate > 0 ? journalDate : now, Clip(reference, 128), sourceType, companyId, adminId, now).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(connection, tx,
                "INSERT INTO `epc_erp_gl_journals` (`journal_no`, `journal_date`, `reference`, `source_type`, `source_id`, `admin_id`, `time_created`) " +
                "VALUES (?, ?, ?, ?, 0, ?, ?) ON DUPLICATE KEY UPDATE `journal_date` = VALUES(`journal_date`), `reference` = VALUES(`reference`), `source_type` = VALUES(`source_type`)",
                ct, journalNo, journalDate > 0 ? journalDate : now, Clip(reference, 128), sourceType, adminId, now).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>LegalEntities → epc_erp_pm_legal_entities (code = dataAreaId).</summary>
    private static async Task<bool> ApplyLegalEntityAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var code = Str(row, "Id", "dataAreaId", "LegalEntityId", "CompanyId");
        if (code.Length == 0)
        {
            return false;
        }
        var name = Str(row, "Name", "LegalEntityName", "Description");
        if (name.Length == 0) name = code;
        var country = Str(row, "CountryRegionId", "Country", "CountryCode");
        var currency = Str(row, "Currency", "CurrencyCode", "AccountingCurrency");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var existing = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code` = ? LIMIT 1"), ct, Clip(code, 40)).ConfigureAwait(false);
        if (existing > 0)
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_pm_legal_entities` SET `name` = ?, `country_code` = ?, `currency_code` = ?, `time_updated` = ? WHERE `id` = ?"),
                ct, Clip(name, 190), Clip(country, 4), Clip(currency, 8), now, existing).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(connection, tx,
                "INSERT INTO `epc_erp_pm_legal_entities` (`code`, `name`, `country_code`, `currency_code`, `active`, `time_created`, `time_updated`) " +
                "VALUES (?, ?, ?, ?, 1, ?, ?)",
                ct, Clip(code, 40), Clip(name, 190), Clip(country, 4), Clip(currency, 8), now, now).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>
    /// dataAreaId → epc_erp_pm_legal_entities.id; creates the row when the company hasn't been
    /// pulled via LegalEntities yet so imported journals always carry a legal entity.
    /// </summary>
    private static async Task<long> ResolveCompanyIdAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var code = Str(row, "dataAreaId", "CompanyId");
        if (code.Length == 0)
        {
            return 0L;
        }
        try
        {
            var existing = await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code` = ? LIMIT 1"), ct, Clip(code, 40)).ConfigureAwait(false);
            if (existing > 0)
            {
                return existing;
            }
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(connection, tx,
                "INSERT INTO `epc_erp_pm_legal_entities` (`code`, `name`, `active`, `time_created`, `time_updated`) VALUES (?, ?, 1, ?, ?)",
                ct, Clip(code, 40), Clip(code, 190), now, now).ConfigureAwait(false);
            return await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code` = ? LIMIT 1"), ct, Clip(code, 40)).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0L; // table not provisioned yet
        }
    }

    /// <summary>
    /// Parses the F&O dimension string (e.g. "501100-BU01-CC01") into structured line columns.
    /// Each non-account segment is looked up in BOTH master tables — a code can be a business unit
    /// in D365 and a legal entity here, or vice versa, so whichever table contains it decides the
    /// mapping. Unmatched segments are kept as dimension values, never force-created as the wrong
    /// kind. Returns (legalEntityId, businessUnitId, dimTypeCode, dimValueCode).
    /// </summary>
    private static async Task<(long EntityId, long BuId, long CostCentreId, string TypeCode, string ValueCode)> ResolveDimensionsAsync(
        DbConnection connection, DbTransaction tx, string accountDimensionString, string defaultDimensionString, JsonElement row, CancellationToken ct, string accountSegment = "")
    {
        var entityId = 0L;
        var buId = 0L;
        var costCentreId = 0L;
        var typeCode = "";
        var valueCode = "";
        // Account-bearing strings (LedgerDimension/AccountDisplayValue) lead with the main account
        // — skip segment 0. Default-dimension strings hold ONLY dimension values, keep them all.
        var segments = new List<string>();
        var accSegs = accountDimensionString.Split('-', '|').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (accSegs.Count > 0)
        {
            accSegs.RemoveAt(0); // leading segment is the company or the account — never a dimension
            // Company-prefixed strings still carry the main account inside the list: drop the
            // resolved account segment too so it is not stored as a fake dimension value.
            if (accountSegment.Length > 0)
            {
                // Only the account position is dropped — a later segment may be a genuine
                // dimension value that happens to share the account's code.
                var accountPos = accSegs.FindIndex(s => s.Equals(accountSegment, StringComparison.OrdinalIgnoreCase));
                if (accountPos >= 0)
                {
                    accSegs.RemoveAt(accountPos);
                }
            }
            segments.AddRange(accSegs);
        }
        segments.AddRange(defaultDimensionString.Split('-', '|').Select(s => s.Trim()).Where(s => s.Length > 0));
        // Explicit per-dimension payload fields supplement the string, never replace it.
        foreach (var f in new[] { Str(row, "LegalEntity", "Company", "CompanyCode"), Str(row, "BusinessUnit"), Str(row, "CostCenter", "CostCentre"), Str(row, "Department") })
        {
            if (f.Length > 0 && !segments.Contains(f, StringComparer.OrdinalIgnoreCase))
            {
                segments.Add(f);
            }
        }
        var explicitCostCentre = Str(row, "CostCenter", "CostCentre");
        var explicitDepartment = Str(row, "Department");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seg in segments)
        {
            if (!seen.Add(seg))
            {
                continue; // same code in both strings — a repeat is not a new dimension
            }
            // Classify by what EXISTS in our masters — a D365 code can be a BU here and a legal
            // entity there (or vice versa); whichever table contains it wins.
            if (buId == 0)
            {
                try
                {
                    var found = await ErpDb.LongAsync(connection, tx,
                        ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_business_units` WHERE `code` = ? LIMIT 1"), ct, Clip(seg, 40)).ConfigureAwait(false);
                    if (found > 0) { buId = found; continue; }
                }
                catch (DbException) { }
            }
            if (entityId == 0)
            {
                try
                {
                    var found = await ErpDb.LongAsync(connection, tx,
                        ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code` = ? LIMIT 1"), ct, Clip(seg, 40)).ConfigureAwait(false);
                    if (found > 0) { entityId = found; continue; }
                }
                catch (DbException) { }
            }
            // Dimension value master: reuse an existing value (any type); new codes go under
            // 'other' — nothing is dropped and ids never alias master ids.
            try
            {
                var preferred = seg == explicitCostCentre ? "cost_centre" : seg == explicitDepartment ? "department" : null;
                var (_, valType) = await EnsureDimValueAsync(connection, tx, seg, preferred, ct).ConfigureAwait(false);
                if (valType.Equals("cost_centre", StringComparison.OrdinalIgnoreCase) && costCentreId == 0)
                {
                    costCentreId = await ResolveCostCentreIdAsync(connection, tx, seg, ct).ConfigureAwait(false);
                    continue;
                }
                if (valueCode.Length == 0)
                {
                    typeCode = valType;
                    valueCode = Clip(seg, 64);
                }
            }
            catch (DbException)
            {
                if (valueCode.Length == 0)
                {
                    typeCode = "other";
                    valueCode = Clip(seg, 64);
                }
            }
        }
        return (entityId, buId, costCentreId, typeCode, valueCode);
    }

    /// <summary>Cost-centre code → epc_erp_pm_cost_centres.id; provisions the master row when missing.</summary>
    private static async Task<long> ResolveCostCentreIdAsync(DbConnection connection, DbTransaction tx, string code, CancellationToken ct)
    {
        try
        {
            var existing = await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_cost_centres` WHERE `code` = ? LIMIT 1"), ct, Clip(code, 40)).ConfigureAwait(false);
            if (existing > 0)
            {
                return existing;
            }
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(connection, tx,
                "INSERT INTO `epc_erp_pm_cost_centres` (`code`, `name`, `active`, `time_created`, `time_updated`) VALUES (?, ?, 1, ?, ?)",
                ct, Clip(code, 40), Clip(code, 190), now, now).ConfigureAwait(false);
            return await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_cost_centres` WHERE `code` = ? LIMIT 1"), ct, Clip(code, 40)).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0L;
        }
    }

    /// <summary>
    /// Finds (or creates under `preferredType`/`other`) a dimension value in epc_erp_gl_dim_values;
    /// returns (value id, its dimension type code). Values keep their existing type on re-sync.
    /// </summary>
    private static async Task<(long Id, string TypeCode)> EnsureDimValueAsync(
        DbConnection connection, DbTransaction tx, string valueCode, string? preferredType, CancellationToken ct)
    {
        var existing = 0L;
        var existingType = "";
        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                "SELECT v.`id`, t.`code` FROM `epc_erp_gl_dim_values` v " +
                "JOIN `epc_erp_gl_dim_types` t ON t.`id` = v.`type_id` WHERE v.`value_code` = @v LIMIT 1";
            var p = cmd.CreateParameter(); p.ParameterName = "@v"; p.Value = Clip(valueCode, 64); cmd.Parameters.Add(p);
            await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                existing = reader.GetInt64(0);
                existingType = reader.GetString(1);
            }
        }
        if (existing > 0)
        {
            return (existing, existingType);
        }
        var typeCode = preferredType ?? "other";
        var typeId = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_gl_dim_types` WHERE `code` = ? LIMIT 1"), ct, typeCode).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (typeId <= 0)
        {
            await ErpDb.ExecuteAsync(connection, tx,
                "INSERT INTO `epc_erp_gl_dim_types` (`code`, `name`, `is_system`, `active`, `time_created`) VALUES (?, ?, 0, 1, ?)",
                ct, typeCode, typeCode == "other" ? "Other dimensions" : typeCode, now).ConfigureAwait(false);
            typeId = await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_gl_dim_types` WHERE `code` = ? LIMIT 1"), ct, typeCode).ConfigureAwait(false);
        }
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT IGNORE INTO `epc_erp_gl_dim_values` (`type_id`, `value_code`, `value_name`, `active`, `time_created`) VALUES (?, ?, ?, 1, ?)",
            ct, typeId, Clip(valueCode, 64), Clip(valueCode, 255), now).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_gl_dim_values` WHERE `type_id` = ? AND `value_code` = ? LIMIT 1"), ct, typeId, Clip(valueCode, 64)).ConfigureAwait(false);
        return (id, typeCode);
    }

    /// <summary>LedgerJournalLines → epc_erp_gl_lines (parent via company-scoped journal_no, coa via main account).</summary>
    private static async Task<bool> ApplyJournalLineAsync(DbConnection connection, DbTransaction tx, JsonElement row, string extKey, string entity, Dictionary<long, HashSet<string>> appliedRefsByJournal, CancellationToken ct)
    {
        var journalNo = ScopedKey(row, Str(row, "JournalNumber", "JournalBatchNumber", "JournalId"), 32);
        var accountCode = Str(row, "MainAccount", "MainAccountId", "AccountDisplayValue", "LedgerDimension");
        if (journalNo.Length == 0 || journalNo[^1] == '|' || accountCode.Length == 0)
        {
            return false;
        }

        var journalId = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_gl_journals` WHERE `journal_no` = ? LIMIT 1"), ct, journalNo).ConfigureAwait(false);
        if (journalId <= 0)
        {
            return false;
        }
        // The D365 line number is the stable identity of this line — amount, note, voucher and
        // dimension edits all keep it, so matching on it updates in place (no duplicates, no collapse).
        var lineRef = Str(row, "LineNumber", "JournalLineNumber", "VoucherLineNumber", "LineRef", "RecId");
        if (lineRef.Length == 0) lineRef = "k:" + extKey; // no line id in payload — fall back to the staged key
        lineRef = Clip(lineRef, 64);
        if (!appliedRefsByJournal.TryGetValue(journalId, out var refs))
        {
            refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            appliedRefsByJournal[journalId] = refs;
        }
        refs.Add(lineRef);

        // Resolve the COA row: a LedgerDimension display string is either
        // company--account--dims (first segment is a legal-entity code — account at index 1)
        // or account--dims (account at index 0). Pick by whether segment 0 is a known legal
        // entity, so a dimension that happens to share a COA code can never steal the amount.
        var segments = accountCode.Split('-', '|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var accountIndex = 0;
        if (segments.Length > 1)
        {
            var entityHit = await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code` = ? LIMIT 1"), ct, Clip(segments[0], 40)).ConfigureAwait(false);
            if (entityHit > 0)
            {
                accountIndex = 1;
            }
        }
        var ordered = new[] { segments[accountIndex] }
            .Concat(segments.Where((s, i) => i != accountIndex)).ToArray();
        var coaId = 0L;
        var accountSegment = "";
        foreach (var seg in ordered)
        {
            coaId = await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1"), ct, Clip(seg, 16)).ConfigureAwait(false);
            if (coaId > 0) { accountSegment = seg; break; }
        }
        if (coaId <= 0)
        {
            return false; // account not synced yet — pull MainAccounts first
        }

        var debit = Dec(row, "DebitAmount", "AccountingCurrencyAmountDebit", "AmountCurDebit", "Debit");
        var credit = Dec(row, "CreditAmount", "AccountingCurrencyAmountCredit", "AmountCurCredit", "Credit");
        var note = Str(row, "Description", "TransactionText", "Text", "VoucherNumber");
        var voucher = Str(row, "VoucherNumber", "Voucher");
        if (note.Length == 0) note = voucher;

        var dimensionString = Str(row, "LedgerDimension", "AccountDisplayValue", "DimensionDisplayValue", "DefaultDimensionDisplayValue");
        var accountDimString = Str(row, "LedgerDimension", "AccountDisplayValue");
        var defaultDimString = Str(row, "DimensionDisplayValue", "DefaultDimensionDisplayValue");
        var (dimEntityId, dimBuId, dimCostCentreId, dimTypeCode, dimValueCode) = await ResolveDimensionsAsync(connection, tx, accountDimString, defaultDimString, row, ct, accountSegment).ConfigureAwait(false);
        var legalEntityId = dimEntityId > 0 ? dimEntityId : await ResolveCompanyIdAsync(connection, tx, row, ct).ConfigureAwait(false);
        var hasDimString = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "dimension_string", ct, tx).ConfigureAwait(false);
        var hasVoucher = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "voucher", ct, tx).ConfigureAwait(false);
        var hasLegalEntity = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "legal_entity_id", ct, tx).ConfigureAwait(false);
        var hasBu = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "business_unit_id", ct, tx).ConfigureAwait(false);
        var hasDimType = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "dimension_type_code", ct, tx).ConfigureAwait(false);
        var hasDimValue = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "dimension_value_code", ct, tx).ConfigureAwait(false);
        var hasCostCentre = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "cost_centre_id", ct, tx).ConfigureAwait(false);

        var existing = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_gl_lines` WHERE `journal_id` = ? AND `line_ref` = ? AND `ext_entity` = ? LIMIT 1"),
            ct, journalId, lineRef, Clip(entity, 64)).ConfigureAwait(false);
        if (existing <= 0)
        {
            // Legacy rows applied before line_ref existed: claim by the old match and stamp the ref.
            existing = await ErpDb.LongAsync(connection, tx,
                ErpDb.Positional(hasVoucher && voucher.Length > 0
                    ? "SELECT `id` FROM `epc_erp_gl_lines` WHERE `journal_id` = ? AND `coa_id` = ? AND IFNULL(`line_note`,'') = ? AND `voucher` = ? AND `line_ref` = '' LIMIT 1"
                    : "SELECT `id` FROM `epc_erp_gl_lines` WHERE `journal_id` = ? AND `coa_id` = ? AND IFNULL(`line_note`,'') = ? AND `line_ref` = '' LIMIT 1"),
                ct, hasVoucher && voucher.Length > 0
                    ? new object?[] { journalId, coaId, Clip(note, 255), Clip(voucher, 255) }
                    : new object?[] { journalId, coaId, Clip(note, 255) }).ConfigureAwait(false);
        }
        if (existing <= 0)
        {
            var cols = new List<string> { "`journal_id`", "`coa_id`", "`debit`", "`credit`", "`line_note`", "`ext_key`", "`line_ref`", "`ext_entity`" };
            var marks = new List<string> { "?", "?", "?", "?", "?", "?", "?", "?" };
            var vals = new List<object?> { journalId, coaId, debit, credit, Clip(note, 255), Clip(extKey, 255), lineRef, Clip(entity, 64) };
            if (hasDimString) { cols.Add("`dimension_string`"); marks.Add("?"); vals.Add(Clip(dimensionString, 255)); }
            if (hasVoucher) { cols.Add("`voucher`"); marks.Add("?"); vals.Add(Clip(voucher, 255)); }
            if (hasLegalEntity) { cols.Add("`legal_entity_id`"); marks.Add("?"); vals.Add(legalEntityId); }
            if (hasBu) { cols.Add("`business_unit_id`"); marks.Add("?"); vals.Add(dimBuId); }
            if (hasDimType) { cols.Add("`dimension_type_code`"); marks.Add("?"); vals.Add(Clip(dimTypeCode, 32)); }
            if (hasDimValue) { cols.Add("`dimension_value_code`"); marks.Add("?"); vals.Add(Clip(dimValueCode, 64)); }
            if (hasCostCentre) { cols.Add("`cost_centre_id`"); marks.Add("?"); vals.Add(dimCostCentreId); }
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("INSERT INTO `epc_erp_gl_lines` (" + string.Join(", ", cols) + ") VALUES (" + string.Join(", ", marks) + ")"),
                ct, vals.ToArray()).ConfigureAwait(false);
        }
        else
        {
            // coa_id is always updated: a changed MainAccount resolves a new account and the
            // stable line_ref match must not leave the amount on the old one.
            var sets = new List<string> { "`coa_id` = ?", "`debit` = ?", "`credit` = ?", "`line_note` = ?", "`ext_key` = ?", "`ext_entity` = ?" };
            var vals = new List<object?> { coaId, debit, credit, Clip(note, 255), Clip(extKey, 255), Clip(entity, 64) };
            if (hasDimString && dimensionString.Length > 0) { sets.Add("`dimension_string` = ?"); vals.Add(Clip(dimensionString, 255)); }
            if (hasVoucher && voucher.Length > 0) { sets.Add("`voucher` = ?"); vals.Add(Clip(voucher, 255)); }
            if (hasLegalEntity) { sets.Add("`legal_entity_id` = ?"); vals.Add(legalEntityId); }
            if (hasBu) { sets.Add("`business_unit_id` = ?"); vals.Add(dimBuId); }
            if (hasDimType) { sets.Add("`dimension_type_code` = ?"); vals.Add(Clip(dimTypeCode, 32)); }
            if (hasDimValue) { sets.Add("`dimension_value_code` = ?"); vals.Add(Clip(dimValueCode, 64)); }
            if (hasCostCentre) { sets.Add("`cost_centre_id` = ?"); vals.Add(dimCostCentreId); }
            vals.Add(existing);
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_gl_lines` SET " + string.Join(", ", sets) + " WHERE `id` = ?"),
                ct, vals.ToArray()).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>WarehousesOnHandV2 → epc_erp_inv_onhand (qty positions per item × site × warehouse).</summary>
    private static async Task<bool> ApplyOnHandAsync(DbConnection connection, DbTransaction tx, JsonElement row, CancellationToken ct)
    {
        var itemNo = Str(row, "ItemNumber", "ItemId");
        var warehouse = Str(row, "InventoryWarehouseId", "WarehouseId", "Warehouse");
        if (itemNo.Length == 0 || warehouse.Length == 0)
        {
            return false;
        }
        var dataArea = Str(row, "dataAreaId", "CompanyId");
        var site = Str(row, "InventorySiteId", "SiteId", "Site");
        var name = Str(row, "ProductName", "ItemName", "Name");
        var onhand = Dec(row, "OnHandQuantity", "AvailPhysical");
        var avail = Dec(row, "AvailableOnHandQuantity", "TotalAvailableQuantity", "AvailablePhysical");
        var reserved = Dec(row, "ReservedOnHandQuantity", "ReservPhysical");
        var ordered = Dec(row, "OrderedQuantity", "OnOrderQuantity");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_inv_onhand` (`data_area`, `item_no`, `product_name`, `site_id`, `warehouse_id`, `onhand_qty`, `avail_qty`, `reserved_qty`, `ordered_qty`, `synced_at`) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?) " +
            "ON DUPLICATE KEY UPDATE `product_name` = VALUES(`product_name`), `onhand_qty` = VALUES(`onhand_qty`), `avail_qty` = VALUES(`avail_qty`), `reserved_qty` = VALUES(`reserved_qty`), `ordered_qty` = VALUES(`ordered_qty`), `synced_at` = VALUES(`synced_at`)",
            ct,
            Clip(dataArea, 16), Clip(itemNo, 64), Clip(name, 190), Clip(site, 32), Clip(warehouse, 48),
            onhand, avail, reserved, ordered, now).ConfigureAwait(false);
        return true;
    }

    /// <summary>GeneralJournalEntryBiEntities → epc_erp_gl_journals (posted voucher headers; journal_no = gj|SourceKey).</summary>
    private static async Task<bool> ApplyGlEntryHeaderAsync(DbConnection connection, DbTransaction tx, JsonElement row, long adminId, CancellationToken ct)
    {
        var sourceKey = Str(row, "SourceKey", "GeneralJournalEntry", "RecId", "TransferId");
        if (sourceKey.Length == 0)
        {
            return false;
        }
        var journalNo = Clip("gj|" + sourceKey, 32);
        var category = Str(row, "JournalCategory", "JournalType", "JournalName");
        var sourceType = category.Contains("Purch", StringComparison.OrdinalIgnoreCase) ? "purchase"
            : category.Contains("Sales", StringComparison.OrdinalIgnoreCase) ? "sales"
            : category.Contains("Paym", StringComparison.OrdinalIgnoreCase) || category.Contains("Payment", StringComparison.OrdinalIgnoreCase) ? "payment"
            : category.Contains("Bank", StringComparison.OrdinalIgnoreCase) || category.Contains("Cash", StringComparison.OrdinalIgnoreCase) ? "cash"
            : "manual";
        var reference = Str(row, "SubledgerVoucher", "Voucher", "DocumentNumber", "JournalNumber");
        var journalDate = DateOf(row, "AccountingDate", "DocumentDate", "CreatedOn");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var companyId = await ResolveCompanyIdAsync(connection, tx, row, ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_gl_journals` (`journal_no`, `journal_date`, `reference`, `source_type`, `source_id`, `company_id`, `admin_id`, `time_created`) " +
            "VALUES (?, ?, ?, ?, 0, ?, ?, ?) ON DUPLICATE KEY UPDATE `journal_date` = VALUES(`journal_date`), `reference` = VALUES(`reference`), `source_type` = VALUES(`source_type`), `company_id` = VALUES(`company_id`)",
            ct, journalNo, journalDate > 0 ? journalDate : now, Clip(reference, 128), sourceType, companyId, adminId, now).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// GeneralJournalAccountEntryBiEntities → epc_erp_gl_lines: the posted ledger entries — every
    /// row carries LedgerDimensionValuesJson ({MAINACCOUNT, LEGALENTITY, BUSINESSUNIT, COSTCENTER, …})
    /// so the main account and each financial dimension land on the real master columns.
    /// </summary>
    private static async Task<bool> ApplyGlAccountEntryAsync(DbConnection connection, DbTransaction tx, JsonElement row, string extKey, string entity, CancellationToken ct)
    {
        var entryFk = Str(row, "GeneralJournalEntry");
        var journalNo = entryFk.Length > 0 ? Clip("gj|" + entryFk, 32) : ScopedKey(row, Str(row, "JournalNumber", "JournalBatchNumber"), 32);
        if (journalNo.Length == 0)
        {
            return false;
        }
        long journalId = 0;
        var voucher = "";
        await using (var jcmd = connection.CreateCommand())
        {
            jcmd.Transaction = tx;
            jcmd.CommandText = "SELECT `id`, IFNULL(`reference`,'') FROM `epc_erp_gl_journals` WHERE `journal_no` = @j LIMIT 1";
            var p = jcmd.CreateParameter(); p.ParameterName = "@j"; p.Value = journalNo; jcmd.Parameters.Add(p);
            await using var r = await jcmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                journalId = r.GetInt64(0);
                voucher = r.GetString(1);
            }
        }
        if (journalId <= 0)
        {
            return false; // header not applied yet — pull/apply GeneralJournalEntryBiEntities first
        }

        var entityId = 0L;
        var buId = 0L;
        var costCentreId = 0L;
        var dimTypeCode = "";
        var dimValueCode = "";
        var mainAccount = "";
        var dimsJson = Str(row, "LedgerDimensionValuesJson");
        if (dimsJson.Length > 0)
        {
            try
            {
                using var dims = JsonDocument.Parse(dimsJson);
                var dimObjs = dims.RootElement.ValueKind == JsonValueKind.Array
                    ? dims.RootElement.EnumerateArray().ToList()
                    : new List<JsonElement> { dims.RootElement };
                foreach (var obj in dimObjs)
                {
                    if (obj.ValueKind != JsonValueKind.Object) { continue; }
                    foreach (var prop in obj.EnumerateObject())
                    {
                        var val = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() ?? "" : prop.Value.ToString();
                        if (val.Length == 0) { continue; }
                        switch (prop.Name.ToUpperInvariant())
                        {
                            case "MAINACCOUNT":
                                mainAccount = val;
                                break;
                            case "LEGALENTITY":
                                entityId = entityId > 0 ? entityId : await ErpDb.LongAsync(connection, tx,
                                    ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code` = ? LIMIT 1"), ct, Clip(val, 40)).ConfigureAwait(false);
                                break;
                            case "BUSINESSUNIT":
                                buId = buId > 0 ? buId : await ErpDb.LongAsync(connection, tx,
                                    ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_business_units` WHERE `code` = ? LIMIT 1"), ct, Clip(val, 40)).ConfigureAwait(false);
                                break;
                            case "COSTCENTER":
                            case "COSTCENTRE":
                                costCentreId = costCentreId > 0 ? costCentreId : await ResolveCostCentreIdAsync(connection, tx, val, ct).ConfigureAwait(false);
                                break;
                            default:
                                // Any other D365 dimension (WORKER, DEPARTMENT, projects, …): provision a
                                // value under a type named after the dimension so nothing is dropped.
                                var (_, valType) = await EnsureDimValueAsync(connection, tx, val, prop.Name.ToLowerInvariant(), ct).ConfigureAwait(false);
                                if (dimValueCode.Length == 0)
                                {
                                    dimTypeCode = valType;
                                    dimValueCode = Clip(val, 64);
                                }
                                break;
                        }
                    }
                }
            }
            catch (JsonException) { /* malformed dims JSON — LedgerAccount fallback below still carries the string */ }
        }
        var ledgerAccount = Str(row, "LedgerAccount", "AccountDisplayValue", "LedgerDimension");
        if (mainAccount.Length == 0 && ledgerAccount.Length > 0)
        {
            mainAccount = ledgerAccount.Split('-', '|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        }
        if (mainAccount.Length == 0)
        {
            return false;
        }
        var coaId = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1"), ct, Clip(mainAccount, 16)).ConfigureAwait(false);
        if (coaId <= 0)
        {
            return false; // account not synced yet — pull MainAccounts first
        }
        if (entityId == 0)
        {
            entityId = await ResolveCompanyIdAsync(connection, tx, row, ct).ConfigureAwait(false);
        }

        var amount = Dec(row, "AccountingCurrencyAmount");
        var isCredit = Str(row, "IsCredit").Equals("Yes", StringComparison.OrdinalIgnoreCase);
        var debit = isCredit ? 0m : amount;
        var credit = isCredit ? amount : 0m;
        var note = Str(row, "Text", "PaymentReference", "Description");
        if (note.Length == 0) { note = voucher; }
        var lineRef = Clip(Str(row, "SourceKey", "RecId", "LedgerDimension"), 64);
        if (lineRef.Length == 0) { lineRef = "k:" + Clip(extKey, 60); }

        var hasDimString = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "dimension_string", ct, tx).ConfigureAwait(false);
        var hasVoucher = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "voucher", ct, tx).ConfigureAwait(false);
        var hasLegalEntity = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "legal_entity_id", ct, tx).ConfigureAwait(false);
        var hasBu = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "business_unit_id", ct, tx).ConfigureAwait(false);
        var hasDimType = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "dimension_type_code", ct, tx).ConfigureAwait(false);
        var hasDimValue = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "dimension_value_code", ct, tx).ConfigureAwait(false);
        var hasCostCentre = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "cost_centre_id", ct, tx).ConfigureAwait(false);
        var hasCurrency = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "currency_code", ct, tx).ConfigureAwait(false);
        var hasAmtCur = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "amount_currency", ct, tx).ConfigureAwait(false);
        var currency = Str(row, "TransactionCurrencyCode");
        var amountCur = Dec(row, "TransactionCurrencyAmount");

        var existing = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_gl_lines` WHERE `journal_id` = ? AND `line_ref` = ? AND `ext_entity` = ? LIMIT 1"),
            ct, journalId, lineRef, Clip(entity, 64)).ConfigureAwait(false);
        if (existing <= 0)
        {
            var cols = new List<string> { "`journal_id`", "`coa_id`", "`debit`", "`credit`", "`line_note`", "`ext_key`", "`line_ref`", "`ext_entity`" };
            var marks = new List<string> { "?", "?", "?", "?", "?", "?", "?", "?" };
            var vals = new List<object?> { journalId, coaId, debit, credit, Clip(note, 255), Clip(extKey, 255), lineRef, Clip(entity, 64) };
            if (hasDimString) { cols.Add("`dimension_string`"); marks.Add("?"); vals.Add(Clip(ledgerAccount, 255)); }
            if (hasVoucher) { cols.Add("`voucher`"); marks.Add("?"); vals.Add(Clip(voucher, 255)); }
            if (hasLegalEntity) { cols.Add("`legal_entity_id`"); marks.Add("?"); vals.Add(entityId); }
            if (hasBu) { cols.Add("`business_unit_id`"); marks.Add("?"); vals.Add(buId); }
            if (hasDimType) { cols.Add("`dimension_type_code`"); marks.Add("?"); vals.Add(Clip(dimTypeCode, 32)); }
            if (hasDimValue) { cols.Add("`dimension_value_code`"); marks.Add("?"); vals.Add(Clip(dimValueCode, 64)); }
            if (hasCostCentre) { cols.Add("`cost_centre_id`"); marks.Add("?"); vals.Add(costCentreId); }
            if (hasCurrency) { cols.Add("`currency_code`"); marks.Add("?"); vals.Add(Clip(currency, 255)); }
            if (hasAmtCur) { cols.Add("`amount_currency`"); marks.Add("?"); vals.Add(amountCur); }
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("INSERT INTO `epc_erp_gl_lines` (" + string.Join(", ", cols) + ") VALUES (" + string.Join(", ", marks) + ")"),
                ct, vals.ToArray()).ConfigureAwait(false);
        }
        else
        {
            var sets = new List<string> { "`coa_id` = ?", "`debit` = ?", "`credit` = ?", "`line_note` = ?", "`ext_key` = ?", "`ext_entity` = ?" };
            var vals = new List<object?> { coaId, debit, credit, Clip(note, 255), Clip(extKey, 255), Clip(entity, 64) };
            if (hasDimString && ledgerAccount.Length > 0) { sets.Add("`dimension_string` = ?"); vals.Add(Clip(ledgerAccount, 255)); }
            if (hasVoucher && voucher.Length > 0) { sets.Add("`voucher` = ?"); vals.Add(Clip(voucher, 255)); }
            if (hasLegalEntity) { sets.Add("`legal_entity_id` = ?"); vals.Add(entityId); }
            if (hasBu) { sets.Add("`business_unit_id` = ?"); vals.Add(buId); }
            if (hasDimType) { sets.Add("`dimension_type_code` = ?"); vals.Add(Clip(dimTypeCode, 32)); }
            if (hasDimValue) { sets.Add("`dimension_value_code` = ?"); vals.Add(Clip(dimValueCode, 64)); }
            if (hasCostCentre) { sets.Add("`cost_centre_id` = ?"); vals.Add(costCentreId); }
            if (hasCurrency) { sets.Add("`currency_code` = ?"); vals.Add(Clip(currency, 255)); }
            if (hasAmtCur) { sets.Add("`amount_currency` = ?"); vals.Add(amountCur); }
            vals.Add(existing);
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_gl_lines` SET " + string.Join(", ", sets) + " WHERE `id` = ?"),
                ct, vals.ToArray()).ConfigureAwait(false);
        }
        return true;
    }

    /// <summary>Parses an F&O date value (ISO string) into unix seconds; 0 when absent.</summary>
    private static long DateOf(JsonElement row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                {
                    return dt.ToUnixTimeSeconds();
                }
            }
        }
        return 0L;
    }
}
