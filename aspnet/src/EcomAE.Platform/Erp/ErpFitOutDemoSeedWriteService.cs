using System.Data.Common;
using System.Globalization;
using MySqlConnector;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Seeds and clears tagged demo data for the fit-out workspace
/// (/erp/fitout/workspace). Every row carries a DEMO tag — a `DEMO |` prefix on
/// the display name or a `DEMO-` prefix on codes and references — so
/// <see cref="ClearDemoAsync"/> removes only demo rows and never touches users,
/// manually created masters, or live data. Seeding is idempotent: it clears any
/// previous demo rows first. Transactional rows are written through the
/// existing fit-out write services; reference masters (customers, suppliers,
/// items, warehouse) are inserted directly with the same DEMO tag.
/// </summary>
public interface IErpFitOutDemoSeedWriteService
{
    Task<string> SeedDemoAsync(CancellationToken cancellationToken = default);
    Task<string> ClearDemoAsync(CancellationToken cancellationToken = default);
}

public sealed class ErpFitOutDemoSeedWriteService : IErpFitOutDemoSeedWriteService
{
    public const string NamePrefix = "DEMO | ";
    public const string RefPrefix = "DEMO-";

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpPrjSaveWriteService _projects;
    private readonly IErpFitOutCostCodeWriteService _costCodes;
    private readonly IErpFitOutEstimateWriteService _estimates;
    private readonly IErpFitOutQuotationWriteService _quotations;
    private readonly IErpFitOutContractTermsWriteService _contracts;
    private readonly IErpFitOutProcurementLinkWriteService _procurement;
    private readonly IErpFitOutThreeWayMatchWriteService _matches;
    private readonly IErpFitOutMaterialMovementWriteService _materials;
    private readonly IErpFitOutDeliveryRecordWriteService _records;
    private readonly IErpPrjaBudgetSaveWriteService _budgets;
    private readonly IErpPrjaTxnAddWriteService _txns;
    private readonly IErpPrjaRecognitionWriteService _recognition;
    private readonly IErpCostCenterWriteService _costCenters;
    private readonly IErpFitOutGlPostWriteService _glPost;

    public ErpFitOutDemoSeedWriteService(
        IErpWriteConnectionFactory connections,
        IErpPrjSaveWriteService projects,
        IErpFitOutCostCodeWriteService costCodes,
        IErpFitOutEstimateWriteService estimates,
        IErpFitOutQuotationWriteService quotations,
        IErpFitOutContractTermsWriteService contracts,
        IErpFitOutProcurementLinkWriteService procurement,
        IErpFitOutThreeWayMatchWriteService matches,
        IErpFitOutMaterialMovementWriteService materials,
        IErpFitOutDeliveryRecordWriteService records,
        IErpPrjaBudgetSaveWriteService budgets,
        IErpPrjaTxnAddWriteService txns,
        IErpPrjaRecognitionWriteService recognition,
        IErpCostCenterWriteService costCenters,
        IErpFitOutGlPostWriteService glPost)
    {
        _connections = connections;
        _projects = projects;
        _costCodes = costCodes;
        _estimates = estimates;
        _quotations = quotations;
        _contracts = contracts;
        _procurement = procurement;
        _matches = matches;
        _materials = materials;
        _records = records;
        _budgets = budgets;
        _txns = txns;
        _recognition = recognition;
        _costCenters = costCenters;
        _glPost = glPost;
    }

    public async Task<string> SeedDemoAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — demo seed skipped.";
        }

        await ClearDemoAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureProjectAccountingAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureCostCentersAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureProjectsTableAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureRegistryAsync(connection, cancellationToken).ConfigureAwait(false);

        var marks = new DemoMarks();
        var customers = await SeedCustomersAsync(connection, marks, cancellationToken).ConfigureAwait(false);
        var suppliers = await SeedSuppliersAsync(connection, marks, cancellationToken).ConfigureAwait(false);
        await SeedWarehouseAsync(connection, marks, cancellationToken).ConfigureAwait(false);
        var items = await SeedItemsAsync(connection, marks, cancellationToken).ConfigureAwait(false);
        var projects = await SeedProjectsAsync(customers, marks, cancellationToken).ConfigureAwait(false);
        var costCodes = await SeedCostCodesAsync(projects, marks, cancellationToken).ConfigureAwait(false);
        var estimates = await SeedEstimatesAsync(projects, marks, cancellationToken).ConfigureAwait(false);
        var contracts = await SeedContractsAsync(projects, marks, cancellationToken).ConfigureAwait(false);
        var budgets = await SeedBudgetsAsync(projects, marks, cancellationToken).ConfigureAwait(false);
        var procurement = await SeedProcurementAsync(projects, marks, cancellationToken).ConfigureAwait(false);
        var movements = await SeedMaterialMovementsAsync(projects, marks, cancellationToken).ConfigureAwait(false);
        var site = await SeedSiteRecordsAsync(projects, suppliers, marks, cancellationToken).ConfigureAwait(false);
        var claims = await SeedClaimsAndRecognitionAsync(projects, marks, cancellationToken).ConfigureAwait(false);
        var allocations = await SeedAllocationsAsync(projects, marks, connection, cancellationToken).ConfigureAwait(false);
        await ApproveDemoClaimsAsync(connection, cancellationToken).ConfigureAwait(false);
        await PersistMarksAsync(connection, marks, cancellationToken).ConfigureAwait(false);

        // Post the demo rows to the GL scoped to each seeded project — never 0/all-projects,
        // which would also sweep up every unposted live transaction.
        var glNote = string.Empty;
        foreach (var project in projects)
        {
            glNote = await _glPost.PostAsync(project.Id, null, cancellationToken).ConfigureAwait(false);
        }

        return "Demo data loaded: " + projects.Count + " projects, " + customers + " customers, "
               + suppliers.Count + " suppliers, " + items + " items, " + costCodes + " cost codes, "
               + estimates + " estimate/BOQ/quotation rows, " + contracts + " contract terms, "
               + budgets + " budgets, " + procurement + " procurement links/matches, "
               + movements + " material movements, " + site + " site records, "
               + claims + " claims/recognition postings, " + allocations
               + " allocation rows. Total contract value "
               + projects.Sum(p => p.ContractValue).ToString("N0", CultureInfo.InvariantCulture)
               + " across the demo projects. " + glNote;
    }

    /// <summary>
    /// Table recording the exact row ids this seeder created. ClearDemoAsync
    /// deletes only registered rows (plus children of registered parents), so a
    /// live row that merely contains "DEMO" in a name or reference is never
    /// removed. The registry persists across app restarts; pressing "Remove
    /// sample data" at any time — including twice — is safe.
    /// </summary>
    private const string RegistryTable = "ecomae_fitout_demo_registry";

    /// <summary>Table -> row ids written by the current seed run.</summary>
    private sealed class DemoMarks
    {
        private readonly Dictionary<string, List<long>> _ids = new(StringComparer.Ordinal);

        public void Add(string table, long id)
        {
            if (id <= 0)
            {
                return;
            }

            if (!_ids.TryGetValue(table, out var list))
            {
                _ids[table] = list = [];
            }

            list.Add(id);
        }

        public IReadOnlyDictionary<string, List<long>> All => _ids;
    }

    private static async Task EnsureRegistryAsync(DbConnection connection, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `" + RegistryTable + "` (" +
            "`id` bigint NOT NULL AUTO_INCREMENT," +
            "`table_name` varchar(64) NOT NULL," +
            "`row_id` bigint NOT NULL," +
            "PRIMARY KEY (`id`), KEY `x_table` (`table_name`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8",
            ct).ConfigureAwait(false);
    }

    private static async Task PersistMarksAsync(DbConnection connection, DemoMarks marks, CancellationToken ct)
    {
        foreach (var (table, ids) in marks.All)
        {
            foreach (var id in ids)
            {
                try
                {
                    await ErpDb.ExecuteAsync(connection, null,
                        ErpDb.Positional("INSERT INTO `" + RegistryTable + "` (`table_name`,`row_id`) VALUES (?,?)"),
                        ct, table, id).ConfigureAwait(false);
                }
                catch (DbException)
                {
                }
            }
        }
    }

    public async Task<string> ClearDemoAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — demo clear skipped.";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureRegistryAsync(connection, cancellationToken).ConfigureAwait(false);
        var removed = 0;

        // Demo GL journals first, matched to the registered demo rows they were
        // posted for — live journals can never match a registry row.
        foreach (var sql in DemoJournalDeletes())
        {
            try
            {
                removed += await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
            }
            catch (MySqlException)
            {
            }
            catch (DbException)
            {
            }
        }

        // Child rows with no tag of their own are demo data only through a
        // registered parent: approval audit rows follow their delivery record,
        // POC recognition rows follow their project.
        var linked = new[]
        {
            "DELETE a FROM `ecomae_fitout_approval_audit` a INNER JOIN `" + RegistryTable + "` r"
                + " ON r.`table_name`='ecomae_fitout_delivery_records' AND r.`row_id`=a.`delivery_record_id`",
            "DELETE r2 FROM `epc_prja_recognition` r2 INNER JOIN `" + RegistryTable + "` r"
                + " ON r.`table_name`='epc_prj_projects' AND r.`row_id`=r2.`project_id`",
            // contract_terms is keyed by contract_id (the seeded project id).
            "DELETE t FROM `ecomae_fitout_contract_terms` t INNER JOIN `" + RegistryTable + "` r"
                + " ON r.`table_name`='ecomae_fitout_contract_terms' AND r.`row_id`=t.`contract_id`",
        };
        foreach (var sql in linked)
        {
            try
            {
                removed += await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
            }
            catch (MySqlException)
            {
            }
            catch (DbException)
            {
            }
        }

        foreach (var table in DemoTables())
        {
            try
            {
                removed += await ErpDb.ExecuteAsync(
                    connection, null,
                    "DELETE t FROM `" + table + "` t INNER JOIN `" + RegistryTable + "` r"
                        + " ON r.`table_name`='" + table + "' AND r.`row_id`=t.`id`",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (MySqlException)
            {
            }
            catch (DbException)
            {
            }
        }

        // Demo cost centres are deleted last, after the demo allocation rows,
        // and only when no remaining allocation still references them.
        try
        {
            removed += await ErpDb.ExecuteAsync(
                connection, null,
                "DELETE c FROM `epc_cc_centers` c INNER JOIN `" + RegistryTable + "` r"
                    + " ON r.`table_name`='epc_cc_centers' AND r.`row_id`=c.`id`"
                    + " WHERE NOT EXISTS (SELECT 1 FROM `epc_cc_allocations` a WHERE a.`cost_center_id` = c.`id`)",
                cancellationToken).ConfigureAwait(false);
        }
        catch (MySqlException)
        {
        }
        catch (DbException)
        {
        }

        try
        {
            await ErpDb.ExecuteAsync(connection, null,
                "DELETE FROM `" + RegistryTable + "`", cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        return "Removed " + removed + " demo row(s). Live rows and users were not touched.";
    }

    // Journals the month-end GL bridge posted for demo rows. Each DELETE joins the
    // journal's source back to the demo registry; live journals can never match.
    private static IEnumerable<string> DemoJournalDeletes()
    {
        // Claim journals on registered progress-claim records.
        yield return "DELETE l FROM `epc_erp_gl_lines` l INNER JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id`"
                     + " INNER JOIN `" + RegistryTable + "` r ON r.`table_name`='ecomae_fitout_delivery_records' AND r.`row_id`=j.`source_id`"
                     + " WHERE j.`source_type`='adjustment' AND j.`reference` LIKE 'FO-%-IPC-%'";
        yield return "DELETE j FROM `epc_erp_gl_journals` j"
                     + " INNER JOIN `" + RegistryTable + "` r ON r.`table_name`='ecomae_fitout_delivery_records' AND r.`row_id`=j.`source_id`"
                     + " WHERE j.`source_type`='adjustment' AND j.`reference` LIKE 'FO-%-IPC-%'";
        // PRJA journals on registered transactions.
        yield return "DELETE l FROM `epc_erp_gl_lines` l INNER JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id`"
                     + " INNER JOIN `" + RegistryTable + "` r ON r.`table_name`='epc_prja_txn' AND r.`row_id`=j.`source_id`"
                     + " WHERE j.`source_type`='adjustment' AND j.`reference` LIKE 'FO-%-TXN-%'";
        yield return "DELETE j FROM `epc_erp_gl_journals` j"
                     + " INNER JOIN `" + RegistryTable + "` r ON r.`table_name`='epc_prja_txn' AND r.`row_id`=j.`source_id`"
                     + " WHERE j.`source_type`='adjustment' AND j.`reference` LIKE 'FO-%-TXN-%'";
    }

    // The delivery-record approval whitelist excludes client_progress_claim and
    // subcontract_certification, so DecideApprovalAsync can never flip them.
    // Certify the demo rows directly, scoped strictly to DEMO references.
    private static async Task ApproveDemoClaimsAsync(DbConnection connection, CancellationToken ct)
    {
        try
        {
            await ErpDb.ExecuteAsync(connection, null,
                "UPDATE `ecomae_fitout_delivery_records` SET `status`='approved' " +
                "WHERE `reference` LIKE 'DEMO-%' AND `record_type` IN ('client_progress_claim','subcontract_certification')",
                ct).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    /// <summary>
    /// Tables that can hold seeded demo rows (registry-keyed on `id`).
    /// Account tables are deliberately absent — demo clear never touches them.
    /// </summary>
    private static IEnumerable<string> DemoTables()
    {
        yield return "ecomae_fitout_boq_lines";
        yield return "ecomae_fitout_estimates";
        yield return "ecomae_fitout_quotations";
        yield return "ecomae_fitout_cost_codes";
        yield return "ecomae_fitout_material_movements";
        yield return "ecomae_fitout_procurement_links";
        yield return "ecomae_fitout_three_way_matches";
        yield return "ecomae_fitout_delivery_records";
        yield return "epc_prja_budget";
        yield return "epc_prja_txn";
        yield return "epc_prja_recognition";
        yield return "epc_cc_allocations";
        yield return "epc_erp_suppliers";
        yield return "epc_erp_inv_items";
        yield return "epc_erp_inv_warehouses";
        yield return "epc_warehouses";
        yield return "epc_erp_customers";
        yield return "epc_prj_projects";
    }

    private static async Task EnsureProjectsTableAsync(DbConnection connection, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_prj_projects` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`code` varchar(40) NOT NULL DEFAULT ''," +
            "`name` varchar(160) NOT NULL DEFAULT ''," +
            "`customer_id` int(11) NOT NULL DEFAULT 0," +
            "`billing_type` varchar(12) NOT NULL DEFAULT 'tm'," +
            "`budget_cost` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`contract_value` decimal(18,2) NOT NULL DEFAULT 0.00," +
            "`status` varchar(12) NOT NULL DEFAULT 'open'," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`), KEY `x_code` (`code`)" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Projects'",
            ct).ConfigureAwait(false);
    }

    private static async Task<int> SeedCustomersAsync(DbConnection connection, DemoMarks marks, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_customers` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`name` varchar(255) NOT NULL DEFAULT ''," +
            "`email` varchar(255) NOT NULL DEFAULT ''," +
            "`created_at_utc` datetime NULL," +
            "PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            ct).ConfigureAwait(false);
        var count = 0;
        var i = 0;
        foreach (var name in new[] { "DEMO | Al Noor Holdings", "DEMO | Sunrise Properties" })
        {
            i++;
            try
            {
                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional("INSERT INTO `epc_erp_customers` (`name`,`email`,`created_at_utc`) VALUES (?,?,UTC_TIMESTAMP())"),
                    ct, name, "demo-customer-" + i + "@demo.example").ConfigureAwait(false);
                marks.Add("epc_erp_customers", await ErpDb.LastInsertIdAsync(connection, null, ct).ConfigureAwait(false));
                count++;
            }
            catch (DbException)
            {
            }
        }

        return count;
    }

    private async Task<List<long>> SeedSuppliersAsync(DbConnection connection, DemoMarks marks, CancellationToken ct)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_suppliers` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`name` varchar(255) NOT NULL DEFAULT ''," +
            "`contact_email` varchar(255) DEFAULT NULL," +
            "`contact_phone` varchar(64) DEFAULT NULL," +
            "`trn` varchar(64) DEFAULT NULL," +
            "`vendor_account` varchar(32) DEFAULT NULL," +
            "`active` tinyint(1) NOT NULL DEFAULT 1," +
            "`time_created` int(11) NOT NULL DEFAULT 0," +
            "PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            ct).ConfigureAwait(false);
        var ids = new List<long>();
        var names = new[] { "DEMO | Gulf Steel Trading", "DEMO | MEP Supplies Co", "DEMO | Finish Works LLC" };
        var i = 0;
        foreach (var name in names)
        {
            i++;
            try
            {
                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional("INSERT INTO `epc_erp_suppliers` (`vendor_account`,`name`,`contact_email`,`contact_phone`,`active`,`time_created`) VALUES (?,?,?,?,1,UNIX_TIMESTAMP())"),
                    ct, RefPrefix + "SUP-" + i, name, "demo-sup-" + i + "@demo.example",
                    "+9715000000" + i).ConfigureAwait(false);
                var id = await ErpDb.LastInsertIdAsync(connection, null, ct).ConfigureAwait(false);
                ids.Add(id);
                marks.Add("epc_erp_suppliers", id);
            }
            catch (DbException)
            {
            }
        }

        return ids;
    }

    private static async Task SeedWarehouseAsync(DbConnection connection, DemoMarks marks, CancellationToken ct)
    {
        foreach (var table in new[] { "epc_erp_inv_warehouses", "epc_warehouses" })
        {
            try
            {
                await ErpDb.TryExecuteAsync(connection,
                    "CREATE TABLE IF NOT EXISTS `" + table + "` (" +
                    "`id` int(11) NOT NULL AUTO_INCREMENT," +
                    "`code` varchar(64) NOT NULL DEFAULT ''," +
                    "`name` varchar(255) NOT NULL DEFAULT ''," +
                    "`created_at_utc` datetime NULL," +
                    "PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
                    ct).ConfigureAwait(false);
                // Provisioned schemas may not have created_at_utc: only write the
                // columns that actually exist so the demo warehouse always lands.
                var columns = new List<string> { "code", "name" };
                var values = new List<object?> { RefPrefix + "WH-01", "DEMO | Main Warehouse" };
                if (await HasColumnAsync(connection, table, "created_at_utc", ct).ConfigureAwait(false))
                {
                    columns.Add("created_at_utc");
                    values.Add(DateTime.UtcNow);
                }

                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `" + table + "` (`" + string.Join("`,`", columns) + "`) VALUES ("
                        + string.Join(",", columns.Select(_ => "?")) + ")"),
                    ct, values.ToArray()).ConfigureAwait(false);
                marks.Add(table, await ErpDb.LastInsertIdAsync(connection, null, ct).ConfigureAwait(false));
            }
            catch (DbException)
            {
            }
        }
    }

    private static async Task<bool> HasColumnAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken ct)
    {
        var value = await ErpDb.ScalarAsync(connection, null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=? AND COLUMN_NAME=?"),
            ct, table, column).ConfigureAwait(false);
        return value is not null && value is not DBNull
               && Convert.ToInt64(value, CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<int> SeedItemsAsync(DbConnection connection, DemoMarks marks, CancellationToken ct)
    {
        var items = new (string Code, string Name, decimal Cost)[]
        {
            ("DEMO-ITM-01", "DEMO | Cement 50kg", 18.50m),
            ("DEMO-ITM-02", "DEMO | Steel rebar 12mm", 32.00m),
            ("DEMO-ITM-03", "DEMO | Gypsum board 2400x1200", 14.75m),
            ("DEMO-ITM-04", "DEMO | Paint emulsion 20L", 95.00m),
            ("DEMO-ITM-05", "DEMO | Ceramic tile 60x60", 22.40m),
            ("DEMO-ITM-06", "DEMO | Copper cable 2.5mm", 4.90m),
            ("DEMO-ITM-07", "DEMO | PVC pipe 110mm", 12.30m),
            ("DEMO-ITM-08", "DEMO | Door leaf fire-rated", 480.00m),
            ("DEMO-ITM-09", "DEMO | Suspended ceiling tile", 9.80m),
            ("DEMO-ITM-10", "DEMO | LED panel 600x600", 65.00m),
        };
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_inv_items` (" +
            "`id` int(11) NOT NULL AUTO_INCREMENT," +
            "`code` varchar(64) NOT NULL DEFAULT ''," +
            "`name` varchar(255) NOT NULL DEFAULT ''," +
            "`unit_cost` decimal(14,4) NOT NULL DEFAULT 0," +
            "`warehouse_id` int(11) NOT NULL DEFAULT 0," +
            "`created_at_utc` datetime NULL," +
            "PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
            ct).ConfigureAwait(false);
        // Write only columns that exist — provisioned item tables differ (some
        // use `sku` instead of `code`, some require `item_type`/`unit`/`active`).
        var optional = new (string Column, Func<(string Code, string Name, decimal Cost), object?> Value)[]
        {
            ("unit_cost", i => i.Cost),
            ("standard_cost", i => i.Cost),
            ("purchase_price", i => i.Cost),
            ("warehouse_id", _ => 1L),
            ("item_type", _ => "stock"),
            ("unit", _ => "pcs"),
            ("active", _ => 1),
            ("time_created", _ => DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            ("created_at_utc", _ => DateTime.UtcNow),
        };
        // Tag column: `code` on our own schema, `sku` on provisioned schemas.
        var tagColumn = await HasColumnAsync(connection, "epc_erp_inv_items", "code", ct).ConfigureAwait(false)
            ? "code"
            : await HasColumnAsync(connection, "epc_erp_inv_items", "sku", ct).ConfigureAwait(false) ? "sku" : null;
        if (tagColumn is null)
        {
            return 0;
        }

        var columns = new List<string> { tagColumn, "name" };
        var extras = new List<Func<(string Code, string Name, decimal Cost), object?>>();
        foreach (var (column, value) in optional)
        {
            if (column == tagColumn || !await HasColumnAsync(connection, "epc_erp_inv_items", column, ct).ConfigureAwait(false))
            {
                continue;
            }

            columns.Add(column);
            extras.Add(value);
        }

        var count = 0;
        foreach (var item in items)
        {
            try
            {
                var values = new List<object?> { item.Code, item.Name };
                values.AddRange(extras.Select(v => v(item)));
                await ErpDb.ExecuteAsync(connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_inv_items` (`" + string.Join("`,`", columns) + "`) VALUES ("
                        + string.Join(",", columns.Select(_ => "?")) + ")"),
                    ct, values.ToArray()).ConfigureAwait(false);
                marks.Add("epc_erp_inv_items", await ErpDb.LastInsertIdAsync(connection, null, ct).ConfigureAwait(false));
                count++;
            }
            catch (DbException)
            {
            }
        }

        return count;
    }

    private async Task<List<ErpFitOutProject>> SeedProjectsAsync(int customerCount, DemoMarks marks, CancellationToken ct)
    {
        var spec = new (string Code, string Name, decimal Contract, decimal Budget)[]
        {
            ("DEMO-PRJ-01", "DEMO | Office fit-out — Bay Tower L12", 250000m, 190000m),
            ("DEMO-PRJ-02", "DEMO | Villa renovation — Palm 44", 180000m, 140000m),
            ("DEMO-PRJ-03", "DEMO | Retail unit — Marina Mall", 420000m, 330000m),
        };
        var list = new List<ErpFitOutProject>();
        var i = 0;
        foreach (var (code, name, contract, budget) in spec)
        {
            i++;
            var result = await _projects.SaveAsync(
                new ErpPrjSaveWriteRequest(
                    Code: code,
                    Name: name,
                    CustomerId: customerCount > 0 ? i % customerCount + 1 : 0,
                    BillingType: "tm",
                    BudgetCost: budget,
                    ContractValue: contract,
                    Status: "open"),
                ct).ConfigureAwait(false);
            if (result.Succeeded)
            {
                list.Add(new ErpFitOutProject(result.Id, code, name, contract, budget, "open"));
                marks.Add("epc_prj_projects", result.Id);
            }
        }

        return list;
    }

    private async Task<int> SeedCostCodesAsync(
        List<ErpFitOutProject> projects,
        DemoMarks marks,
        CancellationToken ct)
    {
        var codes = new (string Code, string Name, string CostType, bool Child)[]
        {
            ("DEMO-CC-100", "DEMO | Substructure", "subcontract", false),
            ("DEMO-CC-110", "DEMO | Earthworks", "material", true),
            ("DEMO-CC-120", "DEMO | Concrete works", "material", true),
            ("DEMO-CC-200", "DEMO | Superstructure", "subcontract", false),
            ("DEMO-CC-300", "DEMO | MEP", "material", false),
            ("DEMO-CC-400", "DEMO | Finishes", "labour", false),
            ("DEMO-CC-500", "DEMO | Preliminaries", "equipment", false),
        };
        var count = 0;
        foreach (var project in projects)
        {
            var parents = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var (code, name, costType, child) in codes)
            {
                var parentId = child ? parents.GetValueOrDefault("DEMO-CC-100") : 0;
                var saved = await _costCodes.SaveAsync(
                    new ErpFitOutCostCodeSaveRequest(
                        ProjectId: project.Id,
                        ParentId: parentId,
                        Code: code,
                        Name: name,
                        CostType: costType),
                    ct).ConfigureAwait(false);
                if (saved.Succeeded)
                {
                    count++;
                    marks.Add("ecomae_fitout_cost_codes", saved.Id);
                    if (!child && code == "DEMO-CC-100")
                    {
                        parents[code] = saved.Id;
                    }
                }
            }
        }

        return count;
    }

    private async Task<int> SeedEstimatesAsync(List<ErpFitOutProject> projects, DemoMarks marks, CancellationToken ct)
    {
        var count = 0;
        foreach (var project in projects)
        {
            var shortName = project.Name.Replace("DEMO | ", string.Empty, StringComparison.Ordinal);
            var estimate = await _estimates.SaveEstimateAsync(
                new ErpFitOutEstimateSaveRequest(
                    ProjectId: project.Id,
                    Code: RefPrefix + "EST-" + project.Id,
                    Title: NamePrefix + "Base estimate " + shortName,
                    Revision: 1,
                    MarkupPercent: 15m,
                    Status: "approved"),
                ct).ConfigureAwait(false);
            if (!estimate.Succeeded)
            {
                continue;
            }

            count++;
            marks.Add("ecomae_fitout_estimates", estimate.Id);
            var sort = 0;
            foreach (var (section, description, costType, qty, rate) in new (string, string, string, decimal, decimal)[]
                     {
                         ("DEMO | A — Substructure", "DEMO | Earthworks + concrete supply", "material", 40m, 1200m),
                         ("DEMO | B — Superstructure", "DEMO | Blockwork, joinery, glazing", "material", 60m, 1800m),
                         ("DEMO | C — MEP + finishes", "DEMO | MEP installation and finishes labour", "labour", 30m, 950m),
                     })
            {
                sort++;
                var line = await _estimates.SaveLineAsync(
                    new ErpFitOutBoqLineSaveRequest(
                        EstimateId: estimate.Id,
                        Section: section,
                        Description: description,
                        CostType: costType,
                        Quantity: qty,
                        Unit: "lot",
                        UnitRate: rate,
                        SortOrder: sort,
                        MaterialRate: rate * 0.55m,
                        LabourRate: rate * 0.20m,
                        EquipmentRate: rate * 0.05m,
                        SubcontractRate: costType == "labour" ? rate * 0.10m : 0m,
                        OverheadRate: rate * 0.05m,
                        WastagePercent: 5m,
                        TargetMarginPercent: 15m),
                    ct).ConfigureAwait(false);
                if (line.Succeeded)
                {
                    count++;
                    marks.Add("ecomae_fitout_boq_lines", line.Id);
                }
            }

            // One BOQ revision per project: a second revision row (same code) so
            // revision 1 stays comparable against the changed scope.
            var revised = await _estimates.SaveEstimateAsync(
                new ErpFitOutEstimateSaveRequest(
                    ProjectId: project.Id,
                    Code: RefPrefix + "EST-" + project.Id + "-R2",
                    Title: NamePrefix + "Rev 2 — client scope change (glazing upgrade) " + shortName,
                    Revision: 2,
                    MarkupPercent: 15m,
                    Status: "approved"),
                ct).ConfigureAwait(false);
            if (revised.Succeeded)
            {
                count++;
                marks.Add("ecomae_fitout_estimates", revised.Id);
            }

            var quotation = await _quotations.SaveAsync(
                new ErpFitOutQuotationSaveRequest(
                    EstimateId: estimate.Id,
                    Code: RefPrefix + "QT-" + project.Id,
                    Title: NamePrefix + "Quotation " + shortName,
                    Revision: 1,
                    ExpiresOn: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                    Status: "issued"),
                ct).ConfigureAwait(false);
            if (quotation.Succeeded)
            {
                count++;
                marks.Add("ecomae_fitout_quotations", quotation.Id);
            }
        }

        return count;
    }

    private async Task<int> SeedContractsAsync(List<ErpFitOutProject> projects, DemoMarks marks, CancellationToken ct)
    {
        var count = 0;
        foreach (var project in projects)
        {
            var result = await _contracts.SaveAsync(
                new ErpFitOutContractTermsSaveRequest(project.Id, 10m, 5m, 12),
                ct).ConfigureAwait(false);
            if (result.Succeeded)
            {
                count++;
                marks.Add("ecomae_fitout_contract_terms", result.Id);
            }
        }

        return count;
    }

    private async Task<int> SeedBudgetsAsync(List<ErpFitOutProject> projects, DemoMarks marks, CancellationToken ct)
    {
        var count = 0;
        foreach (var project in projects)
        {
            var half = project.BudgetCost / 2m;
            var a = await _budgets.SaveAsync(
                new ErpPrjaBudgetSaveWriteRequest(
                    ProjectId: project.Id,
                    Category: RefPrefix + "BDG-MAT",
                    CostBudget: half,
                    RevenueBudget: project.ContractValue / 2m),
                ct).ConfigureAwait(false);
            var b = await _budgets.SaveAsync(
                new ErpPrjaBudgetSaveWriteRequest(
                    ProjectId: project.Id,
                    Category: RefPrefix + "BDG-SUB",
                    CostBudget: half * 0.6m,
                    RevenueBudget: 0m),
                ct).ConfigureAwait(false);
            var c = await _budgets.SaveAsync(
                new ErpPrjaBudgetSaveWriteRequest(
                    ProjectId: project.Id,
                    Category: RefPrefix + "BDG-TOTAL",
                    CostBudget: project.BudgetCost,
                    RevenueBudget: project.ContractValue),
                ct).ConfigureAwait(false);
            count += (a.Succeeded ? 1 : 0) + (b.Succeeded ? 1 : 0) + (c.Succeeded ? 1 : 0);
            if (a.Succeeded)
            {
                marks.Add("epc_prja_budget", a.Id);
            }

            if (b.Succeeded)
            {
                marks.Add("epc_prja_budget", b.Id);
            }

            if (c.Succeeded)
            {
                marks.Add("epc_prja_budget", c.Id);
            }
        }

        return count;
    }

    private async Task<int> SeedProcurementAsync(List<ErpFitOutProject> projects, DemoMarks marks, CancellationToken ct)
    {
        var count = 0;
        var seq = 0;
        foreach (var project in projects)
        {
            seq++;
            var link = await _procurement.SaveAsync(
                new ErpFitOutProcurementLinkSaveRequest(
                    ProjectId: project.Id,
                    RequisitionId: seq * 100 + 10,
                    PurchaseOrderId: seq * 100 + 20,
                    GoodsReceiptId: seq * 100 + 30,
                    Description: "DEMO | PR→RFQ→PO→GRN→bill " + project.Code,
                    Quantity: 5m,
                    CommittedAmount: 260m * seq,
                    Status: "complete"),
                ct).ConfigureAwait(false);
            if (!link.Succeeded)
            {
                continue;
            }

            count++;
            marks.Add("ecomae_fitout_procurement_links", link.Id);
            var match = await _matches.SaveAsync(
                new ErpFitOutThreeWayMatchSaveRequest(
                    ProcurementLinkId: link.Id,
                    ProjectId: project.Id,
                    PurchaseOrderId: seq * 100 + 20,
                    GoodsReceiptId: seq * 100 + 30,
                    InvoiceReference: RefPrefix + "INV-" + seq,
                    OrderedAmount: 1300m,
                    ReceivedAmount: 1300m,
                    InvoicedAmount: 1300m,
                    TolerancePercent: 2m),
                ct).ConfigureAwait(false);
            if (match.Succeeded)
            {
                count++;
                marks.Add("ecomae_fitout_three_way_matches", match.Id);
            }
        }

        if (projects.Count > 0)
        {
            // One partial GRN whose invoice outruns the receipt: the 3-way match
            // fails the 2% tolerance.
            var project = projects[0];
            var link = await _procurement.SaveAsync(
                new ErpFitOutProcurementLinkSaveRequest(
                    ProjectId: project.Id,
                    RequisitionId: 901,
                    PurchaseOrderId: 902,
                    GoodsReceiptId: 903,
                    Description: "DEMO | Partial GRN — stage delivery",
                    Quantity: 10m,
                    CommittedAmount: 5000m,
                    Status: "partial"),
                ct).ConfigureAwait(false);
            if (link.Succeeded)
            {
                count++;
                marks.Add("ecomae_fitout_procurement_links", link.Id);
                var match = await _matches.SaveAsync(
                    new ErpFitOutThreeWayMatchSaveRequest(
                        ProcurementLinkId: link.Id,
                        ProjectId: project.Id,
                        PurchaseOrderId: 902,
                        GoodsReceiptId: 903,
                        InvoiceReference: RefPrefix + "INV-PART",
                        OrderedAmount: 5000m,
                        ReceivedAmount: 2300m,
                        InvoicedAmount: 5000m,
                        TolerancePercent: 2m),
                    ct).ConfigureAwait(false);
                if (match.Succeeded)
                {
                    count++;
                    marks.Add("ecomae_fitout_three_way_matches", match.Id);
                }
            }
        }

        return count;
    }

    private async Task<int> SeedMaterialMovementsAsync(
        List<ErpFitOutProject> projects,
        DemoMarks marks,
        CancellationToken ct)
    {
        if (projects.Count == 0)
        {
            return 0;
        }

        var count = 0;
        var first = projects[0];
        var second = projects.Count > 1 ? projects[1] : first;
        var moves = new (ErpFitOutProject P, string Item, string Desc, string Kind, decimal Qty, decimal Cost, string Ref)[]
        {
            (first, "DEMO-ITM-01", "DEMO | Cement 50kg", "issue", 120m, 18.50m, RefPrefix + "MI-001"),
            (first, "DEMO-ITM-03", "DEMO | Gypsum board 2400x1200", "issue", 200m, 14.75m, RefPrefix + "MI-002"),
            (second, "DEMO-ITM-05", "DEMO | Ceramic tile 60x60", "issue", 150m, 22.40m, RefPrefix + "MI-003"),
            (first, "DEMO-ITM-01", "DEMO | Cement 50kg", "return", 20m, 18.50m, RefPrefix + "MR-001"),
        };
        foreach (var (p, item, desc, kind, qty, cost, reference) in moves)
        {
            var result = await _materials.SaveAsync(
                new ErpFitOutMaterialMovementSaveRequest(
                    ProjectId: p.Id,
                    ItemCode: item,
                    Description: desc,
                    Quantity: qty,
                    UnitCost: cost,
                    Movement: kind,
                    Reference: reference),
                ct).ConfigureAwait(false);
            if (result.Succeeded)
            {
                count++;
                marks.Add("ecomae_fitout_material_movements", result.Id);
            }
        }

        return count;
    }

    private async Task<int> SeedSiteRecordsAsync(
        List<ErpFitOutProject> projects,
        List<long> suppliers,
        DemoMarks marks,
        CancellationToken ct)
    {
        var count = 0;
        var seq = 0;
        foreach (var project in projects)
        {
            seq++;
            var day = new DateOnly(2026, 9, 20 + seq);
            var entries = new (string Type, string Ref, string Title, string Desc, decimal Qty, decimal Amount)[]
            {
                ("site_daily_report", RefPrefix + "SDR-" + seq, "DEMO | Daily report", "DEMO | 48 workers on site, fine weather", 0m, 0m),
                ("timesheet", RefPrefix + "TS-" + seq, "DEMO | Timesheet", "DEMO | 320 labour hours booked", 320m, 0m),
                ("equipment_usage", RefPrefix + "EQ-" + seq, "DEMO | Equipment", "DEMO | Crane + scissor lift hours", 14m, 0m),
                ("variation", RefPrefix + "VAR-" + seq, "DEMO | Variation", "DEMO | Lobby feature wall added", 0m, 18500m),
                ("rfi", RefPrefix + "RFI-" + seq, "DEMO | RFI", "DEMO | MEP riser detail query", 0m, 0m),
                ("drawing_revision", RefPrefix + "DR-" + seq, "DEMO | Drawing rev C", "DEMO | IFC revision C issued", 0m, 0m),
                ("qa_inspection", RefPrefix + "QA-" + seq, "DEMO | QA-QC inspection", "DEMO | Blockwork inspection passed", 0m, 0m),
                ("snag", RefPrefix + "SNAG-" + seq, "DEMO | Snag", "DEMO | Door alignment G-04", 0m, 0m),
            };
            foreach (var (type, reference, title, desc, qty, amount) in entries)
            {
                var result = await _records.SaveAsync(
                    new ErpFitOutDeliveryRecordSaveRequest(
                        ProjectId: project.Id,
                        RecordType: type,
                        Reference: reference,
                        Title: title,
                        Description: desc,
                        Quantity: qty,
                        Amount: amount,
                        EventDate: day,
                        Status: type == "variation" ? "approved" : null),
                    ct).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    count++;
                    marks.Add("ecomae_fitout_delivery_records", result.Id);
                }
            }

            if (suppliers.Count > 0)
            {
                var rfq = await _records.SaveAsync(
                    new ErpFitOutDeliveryRecordSaveRequest(
                        ProjectId: project.Id,
                        SubcontractorId: suppliers[(seq - 1) % suppliers.Count],
                        RecordType: "supplier_rfq",
                        Reference: RefPrefix + "RFQ-" + seq,
                        Title: "DEMO | RFQ — steel + boards",
                        Description: "DEMO | request for quotation",
                        EventDate: day),
                    ct).ConfigureAwait(false);
                if (rfq.Succeeded)
                {
                    count++;
                    marks.Add("ecomae_fitout_delivery_records", rfq.Id);
                }
                // vendor_bill stays 'draft' (approval-controlled) — matches the
                // tolerance-fail three-way match.
                var bill = await _records.SaveAsync(
                    new ErpFitOutDeliveryRecordSaveRequest(
                        ProjectId: project.Id,
                        SubcontractorId: suppliers[(seq - 1) % suppliers.Count],
                        RecordType: "vendor_bill",
                        Reference: RefPrefix + "BILL-" + seq,
                        Title: "DEMO | Vendor bill — first delivery",
                        Description: "DEMO | supplier invoice received",
                        Amount: 2400m * seq,
                        EventDate: day,
                        Status: "draft"),
                    ct).ConfigureAwait(false);
                if (bill.Succeeded)
                {
                    count++;
                    marks.Add("ecomae_fitout_delivery_records", bill.Id);
                }
            }
        }

        if (projects.Count > 0)
        {
            // Subcontract order → measurement → certification chain on project 1.
            var p = projects[0];
            var order = await _records.SaveAsync(
                new ErpFitOutDeliveryRecordSaveRequest(
                    ProjectId: p.Id,
                    SubcontractorId: suppliers.Count > 0 ? suppliers[0] : 0,
                    RecordType: "subcontract_order",
                    Reference: RefPrefix + "SCO-1",
                    Title: "DEMO | Subcontract — MEP installation",
                    Description: "DEMO | MEP works package",
                    Amount: 60000m,
                    EventDate: new DateOnly(2026, 9, 5)),
                ct).ConfigureAwait(false);
            if (order.Succeeded)
            {
                count++;
                marks.Add("ecomae_fitout_delivery_records", order.Id);
                var measurement = await _records.SaveAsync(
                    new ErpFitOutDeliveryRecordSaveRequest(
                        ProjectId: p.Id,
                        ParentId: order.Id,
                        RecordType: "subcontract_measurement",
                        Reference: RefPrefix + "SCM-1",
                        Title: "DEMO | Subcontract measurement — September",
                        Description: "DEMO | measured works Sept",
                        Amount: 12000m,
                        CompletionPercent: 20m,
                        EventDate: new DateOnly(2026, 9, 28)),
                    ct).ConfigureAwait(false);
                if (measurement.Succeeded)
                {
                    count++;
                    marks.Add("ecomae_fitout_delivery_records", measurement.Id);
                }
                var certification = await _records.SaveAsync(
                    new ErpFitOutDeliveryRecordSaveRequest(
                        ProjectId: p.Id,
                        ParentId: order.Id,
                        RecordType: "subcontract_certification",
                        Reference: RefPrefix + "SCC-1",
                        Title: "DEMO | Subcontract certification — September",
                        Description: "DEMO | certified Sept works",
                        Amount: 12000m,
                        CompletionPercent: 20m,
                        EventDate: new DateOnly(2026, 9, 30)),
                    ct).ConfigureAwait(false);
                if (certification.Succeeded)
                {
                    count++;
                    marks.Add("ecomae_fitout_delivery_records", certification.Id);
                    await _records.DecideApprovalAsync(certification.Id, "approved", 0, ct).ConfigureAwait(false);
                }
            }
        }

        return count;
    }

    private async Task<int> SeedClaimsAndRecognitionAsync(
        List<ErpFitOutProject> projects,
        DemoMarks marks,
        CancellationToken ct)
    {
        var count = 0;
        var periods = new[] { new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 31) };
        var periodPct = new[] { 15m, 32m };
        for (var i = 0; i < periods.Length; i++)
        {
            var day = periods[i];
            var pct = periodPct[i];
            foreach (var project in projects)
            {
                var progress = await _records.SaveAsync(
                    new ErpFitOutDeliveryRecordSaveRequest(
                        ProjectId: project.Id,
                        RecordType: "weighted_progress",
                        Reference: RefPrefix + "WP-" + project.Id + "-" + day.Month,
                        Title: "DEMO | Weighted progress " + day.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                        Description: "DEMO | BOQ weighted % complete",
                        CompletionPercent: pct,
                        EventDate: day),
                    ct).ConfigureAwait(false);
                if (progress.Succeeded)
                {
                    count++;
                    marks.Add("ecomae_fitout_delivery_records", progress.Id);
                }

                var workDone = ErpFitOutCalc.Money(project.ContractValue * pct / 100m);
                var retention = ErpFitOutCalc.Money(workDone * 5m / 100m);
                var vat = ErpFitOutCalc.Money((workDone - retention) * 5m / 100m);
                var claimNet = ErpFitOutCalc.ProgressNet(workDone, retention, 0m, vat);
                var claim = await _records.SaveAsync(
                    new ErpFitOutDeliveryRecordSaveRequest(
                        ProjectId: project.Id,
                        RecordType: "client_progress_claim",
                        Reference: RefPrefix + "IPC-" + project.Id + "-" + day.Month,
                        Title: "DEMO | Progress claim " + day.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                        Description: "DEMO | work-done " + workDone.ToString("0.00", CultureInfo.InvariantCulture)
                                     + " − retention " + retention.ToString("0.00", CultureInfo.InvariantCulture)
                                     + " + VAT " + vat.ToString("0.00", CultureInfo.InvariantCulture),
                        Amount: claimNet,
                        CompletionPercent: pct,
                        EventDate: day),
                    ct).ConfigureAwait(false);
                if (claim.Succeeded)
                {
                    count++;
                    marks.Add("ecomae_fitout_delivery_records", claim.Id);
                    await _records.DecideApprovalAsync(claim.Id, "approved", 0, ct).ConfigureAwait(false);
                }
            }
        }

        // Month-end POC recognition + earned cost/revenue postings per project.
        var asOf = new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        foreach (var project in projects)
        {
            var run = await _recognition.RunAsync(
                new ErpPrjaRecognitionWriteRequest(
                    ProjectId: project.Id,
                    Method: "poc",
                    Fraction: 0.32m,
                    AsOf: asOf),
                ct).ConfigureAwait(false);
            if (run.Succeeded)
            {
                count++;
                marks.Add("epc_prja_recognition", run.Id);
            }
            var cost = await _txns.AddAsync(
                new ErpPrjaTxnAddWriteRequest(
                    ProjectId: project.Id,
                    TxnType: "cost",
                    Category: RefPrefix + "COST",
                    Description: "DEMO | Earned cost to date",
                    Amount: project.BudgetCost * 0.32m,
                    TxnDate: asOf),
                ct).ConfigureAwait(false);
            if (cost.Succeeded)
            {
                count++;
                marks.Add("epc_prja_txn", cost.Id);
            }
            var revenue = await _txns.AddAsync(
                new ErpPrjaTxnAddWriteRequest(
                    ProjectId: project.Id,
                    TxnType: "revenue",
                    Category: RefPrefix + "REV",
                    Description: "DEMO | Earned revenue to date",
                    Amount: project.ContractValue * 0.32m,
                    TxnDate: asOf),
                ct).ConfigureAwait(false);
            if (revenue.Succeeded)
            {
                count++;
                marks.Add("epc_prja_txn", revenue.Id);
            }
        }

        return count;
    }

    private async Task<int> SeedAllocationsAsync(
        List<ErpFitOutProject> projects,
        DemoMarks marks,
        DbConnection connection,
        CancellationToken ct)
    {
        if (projects.Count == 0)
        {
            return 0;
        }

        var weights = new Dictionary<long, decimal>();
        var weight = 0.5m;
        foreach (var project in projects)
        {
            var center = await _costCenters.SaveCenterAsync(
                0,
                RefPrefix + "CC-P" + project.Id,
                NamePrefix + "Cost centre " + project.Code,
                0,
                ct).ConfigureAwait(false);
            if (center.Succeeded)
            {
                weights[center.Id] = weight;
                marks.Add("epc_cc_centers", center.Id);
                weight = Math.Max(0.1m, weight - 0.15m);
            }
        }

        if (weights.Count == 0)
        {
            return 0;
        }

        var run = await _costCenters.PostAllocationAsync(
            RefPrefix + "SHARED-1", 15000m, weights, ct).ConfigureAwait(false);
        if (run.Succeeded)
        {
            // Allocation rows carry no id in the result — mark them by the run
            // label we just wrote.
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT `id` FROM `epc_cc_allocations` WHERE `run_label` = @label";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@label";
                parameter.Value = RefPrefix + "SHARED-1";
                command.Parameters.Add(parameter);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    marks.Add("epc_cc_allocations", reader.GetInt64(0));
                }
            }
            catch (DbException)
            {
            }

            return weights.Count;
        }

        return 0;
    }
}
