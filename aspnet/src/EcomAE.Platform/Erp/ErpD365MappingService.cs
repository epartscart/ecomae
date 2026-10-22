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
        };

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

        var staged = new List<(string Key, string Payload)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT ext_key, payload FROM epc_d365_sync_objects WHERE entity = @entity ORDER BY id";
            var entityParam = command.CreateParameter();
            entityParam.ParameterName = "@entity";
            entityParam.Value = entity;
            command.Parameters.Add(entityParam);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                staged.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        if (staged.Count == 0)
        {
            return new ErpD365ApplyResult(false, "no_staged", $"No staged rows for {entity} — pull the entity first.", 0, 0);
        }

        var mapped = 0;
        var skipped = 0;
        var appliedJournalLineRefs = new Dictionary<long, HashSet<string>>();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        foreach (var (extKey, payload) in staged)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(payload);
            }
            catch (JsonException)
            {
                skipped++;
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
                    "MainAccounts" => await ApplyMainAccountAsync(connection, transaction, doc.RootElement, cancellationToken).ConfigureAwait(false),
                    _ => false,
                };
                if (applied)
                {
                    await StoreFieldsAsync(connection, transaction, entity, extKey, doc.RootElement, cancellationToken).ConfigureAwait(false);
                    mapped++;
                }
                else
                {
                    skipped++;
                }
            }
        }
        // Journal-line reconcile: a dimension or voucher edit changes the staged key, so a stale
        // staged row could leave a dead GL line. Read this feed's keyed refs on each touched journal
        // and delete the ones this run did not apply (stale), plus any unclaimed legacy rows
        // (line_ref='' rows can only exist from earlier imports on a D365 journal). Scoped by
        // ext_entity so a different feed's lines on the same journal are never erased.
        foreach (var (journalId, refs) in appliedJournalLineRefs)
        {
            var stale = new List<string>();
            var otherFeedKeyed = false;
            await using (var readCmd = connection.CreateCommand())
            {
                readCmd.Transaction = transaction;
                readCmd.CommandText =
                    "SELECT `line_ref`, `ext_entity` FROM `epc_erp_gl_lines` " +
                    "WHERE `journal_id` = @a AND `line_ref` <> ''";
                var p1 = readCmd.CreateParameter(); p1.ParameterName = "@a"; p1.Value = journalId; readCmd.Parameters.Add(p1);
                await using var reader = await readCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var existing = reader.GetString(0);
                    var feed = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                    if (feed.Equals(entity, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!refs.Contains(existing))
                        {
                            stale.Add(existing);
                        }
                    }
                    else
                    {
                        otherFeedKeyed = true;
                    }
                }
            }
            foreach (var chunk in stale.Chunk(400))
            {
                var marks = string.Join(",", chunk.Select(_ => "?"));
                var args = new List<object?> { journalId, entity };
                args.AddRange(chunk.Select(k => (object?)Clip(k, 64)));
                await ErpDb.ExecuteAsync(connection, transaction,
                    ErpDb.Positional($"DELETE FROM `epc_erp_gl_lines` WHERE `journal_id` = ? AND `ext_entity` = ? AND `line_ref` IN ({marks})"),
                    cancellationToken, args.ToArray()).ConfigureAwait(false);
            }
            // Blank-ref leftovers belong to SOME feed but can't be attributed — only clear them
            // when this journal is used by a single feed, so a shared journal doesn't lose the
            // other feed's unclaimed rows.
            if (!otherFeedKeyed)
            {
                await ErpDb.ExecuteAsync(connection, transaction,
                    ErpDb.Positional("DELETE FROM `epc_erp_gl_lines` WHERE `journal_id` = ? AND `ext_entity` = '' AND `line_ref` = ''"),
                    cancellationToken, journalId).ConfigureAwait(false);
            }
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        var label = MappedEntities[entity];
        return new ErpD365ApplyResult(true, "ok", $"Applied {mapped} row(s) into {label}.", mapped, skipped);
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

        // D365 side: staged posted entries (GeneralLedgerBalanceCDREntities) aggregated per account+dimension.
        var theirs = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var snapshotPeriods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        decimal theirDr = 0m, theirCr = 0m;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT payload FROM epc_d365_sync_objects WHERE entity = 'GeneralLedgerBalanceCDREntities'";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                JsonDocument doc;
                try { doc = JsonDocument.Parse(reader.GetString(0)); }
                catch (JsonException) { continue; }
                using (doc)
                {
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
                        if (!snapshotPeriods.TryGetValue(key, out var existingPeriod) || string.CompareOrdinal(period, existingPeriod) >= 0)
                        {
                            snapshotPeriods[key] = period;
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

    /// <summary>Stores every F&O payload field for a staged row in epc_d365_object_fields (one row per field) so the module screens can display the complete field set, not just the mapped columns.</summary>
    private static async Task StoreFieldsAsync(DbConnection connection, DbTransaction tx, string entity, string extKey, JsonElement row, CancellationToken ct)
    {
        await ErpDb.ExecuteAsync(connection, tx,
            ErpDb.Positional("DELETE FROM `epc_d365_object_fields` WHERE `entity` = ? AND `ext_key` = ?"),
            ct, entity, extKey).ConfigureAwait(false);
        foreach (var property in row.EnumerateObject())
        {
            var text = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.GetRawText();
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("INSERT INTO `epc_d365_object_fields` (`entity`, `ext_key`, `field_name`, `field_value`) VALUES (?, ?, ?, ?)"),
                ct, entity, extKey, Clip(property.Name, 191), Clip(text, 60000)).ConfigureAwait(false);
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

        var hasEmail = await ErpDb.ColumnKnownAsync(connection, "epc_erp_contacts", "email", ct).ConfigureAwait(false);
        var hasPhone = await ErpDb.ColumnKnownAsync(connection, "epc_erp_contacts", "phone", ct).ConfigureAwait(false);
        var hasCompany = await ErpDb.ColumnKnownAsync(connection, "epc_erp_contacts", "company", ct).ConfigureAwait(false);
        var hasTrn = await ErpDb.ColumnKnownAsync(connection, "epc_erp_contacts", "trn", ct).ConfigureAwait(false);
        var hasAccountCode = await ErpDb.ColumnKnownAsync(connection, "epc_erp_contacts", "account_code", ct).ConfigureAwait(false);

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
        var hasVendorAccount = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "vendor_account", ct).ConfigureAwait(false);
        var hasCurrency = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "currency_code", ct).ConfigureAwait(false);
        var hasTrn = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "trn", ct).ConfigureAwait(false);
        var hasPhone = await ErpDb.ColumnKnownAsync(connection, "epc_erp_suppliers", "contact_phone", ct).ConfigureAwait(false);
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

        if (naturalNo.Length > 0 && !string.Equals(naturalNo, journalNo, StringComparison.Ordinal))
        {
            await ErpDb.ExecuteAsync(connection, tx,
                ErpDb.Positional("UPDATE `epc_erp_gl_journals` SET `journal_no` = ? WHERE `journal_no` = ?"), ct, journalNo, naturalNo).ConfigureAwait(false);
        }
        await ErpDb.ExecuteAsync(connection, tx,
            "INSERT INTO `epc_erp_gl_journals` (`journal_no`, `journal_date`, `reference`, `source_type`, `source_id`, `admin_id`, `time_created`) " +
            "VALUES (?, ?, ?, ?, 0, ?, ?) ON DUPLICATE KEY UPDATE `journal_date` = VALUES(`journal_date`), `reference` = VALUES(`reference`), `source_type` = VALUES(`source_type`)",
            ct, journalNo, journalDate > 0 ? journalDate : now, Clip(reference, 128), sourceType, adminId, now).ConfigureAwait(false);
        return true;
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

        // Resolve the COA row: take the main-account segment when the field carries a dimension string.
        var code = accountCode.Split('-', '|')[0].Trim();
        var coaId = await ErpDb.LongAsync(connection, tx,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1"), ct, Clip(code, 16)).ConfigureAwait(false);
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
        var hasDimString = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "dimension_string", ct).ConfigureAwait(false);
        var hasVoucher = await ErpDb.ColumnKnownAsync(connection, "epc_erp_gl_lines", "voucher", ct).ConfigureAwait(false);

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
