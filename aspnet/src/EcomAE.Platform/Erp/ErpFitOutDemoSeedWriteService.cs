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

        var customers = await SeedCustomersAsync(connection, cancellationToken).ConfigureAwait(false);
        var suppliers = await SeedSuppliersAsync(connection, cancellationToken).ConfigureAwait(false);
        await SeedWarehouseAsync(connection, cancellationToken).ConfigureAwait(false);
        var items = await SeedItemsAsync(connection, cancellationToken).ConfigureAwait(false);
        var projects = await SeedProjectsAsync(customers, cancellationToken).ConfigureAwait(false);
        var costCodes = await SeedCostCodesAsync(projects, cancellationToken).ConfigureAwait(false);
        var estimates = await SeedEstimatesAsync(projects, cancellationToken).ConfigureAwait(false);
        var contracts = await SeedContractsAsync(projects, cancellationToken).ConfigureAwait(false);
        var budgets = await SeedBudgetsAsync(projects, cancellationToken).ConfigureAwait(false);
        var procurement = await SeedProcurementAsync(projects, cancellationToken).ConfigureAwait(false);
        var movements = await SeedMaterialMovementsAsync(projects, cancellationToken).ConfigureAwait(false);
        var site = await SeedSiteRecordsAsync(projects, suppliers, cancellationToken).ConfigureAwait(false);
        var claims = await SeedClaimsAndRecognitionAsync(projects, cancellationToken).ConfigureAwait(false);
        var allocations = await SeedAllocationsAsync(projects, cancellationToken).ConfigureAwait(false);
        await ApproveDemoClaimsAsync(connection, cancellationToken).ConfigureAwait(false);

        var glNote = await _glPost.PostAsync(0, null, cancellationToken).ConfigureAwait(false);

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

    public async Task<string> ClearDemoAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured — demo clear skipped.";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var removed = 0;

        // Demo GL journals first, while the demo rows they point at still carry
        // their DEMO tags — otherwise the journal's source row can no longer be matched.
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

        // Child rows that carry no DEMO text tag are still demo data when their parent
        // project/record is DEMO-tagged: contract terms, POC recognition runs and the
        // approval audit trail. Delete them through the demo-project linkage only —
        // live parents can never match.
        foreach (var sql in DemoLinkedDeletes())
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

        foreach (var (table, columns) in DemoTables())
        {
            // Both tag forms are required — a bare 'DEMO%' would also match live
            // rows such as "Demolition works".
            var predicate = string.Join(
                " OR ",
                columns.Select(col => "`" + col + "` LIKE 'DEMO |%' OR `" + col + "` LIKE 'DEMO-%'"));
            try
            {
                removed += await ErpDb.ExecuteAsync(
                    connection, null,
                    "DELETE FROM `" + table + "` WHERE " + predicate,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (MySqlException)
            {
            }
            catch (DbException)
            {
            }
        }

        // Demo cost centres are deleted last, after the demo allocation rows, and
        // only when no remaining (non-demo) allocation still references them.
        try
        {
            removed += await ErpDb.ExecuteAsync(
                connection, null,
                "DELETE c FROM `epc_cc_centers` c " +
                "WHERE (c.`code` LIKE 'DEMO-%' OR c.`name` LIKE 'DEMO |%') " +
                "AND NOT EXISTS (SELECT 1 FROM `epc_cc_allocations` a WHERE a.`cost_center_id` = c.`id`)",
                cancellationToken).ConfigureAwait(false);
        }
        catch (MySqlException)
        {
        }
        catch (DbException)
        {
        }

        return "Removed " + removed + " demo row(s) tagged DEMO. Live rows and users were not touched.";
    }

    // Journals the month-end GL bridge posted for demo rows. Each DELETE joins the
    // journal's source back to its demo-tagged row; live journals can never match.
    private static IEnumerable<string> DemoJournalDeletes()
    {
        // Claim journals on DEMO-referenced progress claims.
        yield return "DELETE l FROM `epc_erp_gl_lines` l INNER JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id`"
                     + " WHERE j.`source_type`='fitout_claim' AND j.`source_id` IN"
                     + " (SELECT `id` FROM `ecomae_fitout_delivery_records` WHERE `reference` LIKE 'DEMO-%')";
        yield return "DELETE j FROM `epc_erp_gl_journals` j"
                     + " WHERE j.`source_type`='fitout_claim' AND j.`source_id` IN"
                     + " (SELECT `id` FROM `ecomae_fitout_delivery_records` WHERE `reference` LIKE 'DEMO-%')";
        // PRJA journals on DEMO-tagged transactions.
        yield return "DELETE l FROM `epc_erp_gl_lines` l INNER JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id`"
                     + " WHERE j.`source_type` IN ('fitout_prja_cost','fitout_prja_rev','fitout_prja_bill') AND j.`source_id` IN"
                     + " (SELECT `id` FROM `epc_prja_txn` WHERE `category` LIKE 'DEMO-%' OR `description` LIKE 'DEMO |%')";
        yield return "DELETE j FROM `epc_erp_gl_journals` j"
                     + " WHERE j.`source_type` IN ('fitout_prja_cost','fitout_prja_rev','fitout_prja_bill') AND j.`source_id` IN"
                     + " (SELECT `id` FROM `epc_prja_txn` WHERE `category` LIKE 'DEMO-%' OR `description` LIKE 'DEMO |%')";
        // Advance journals on contract terms whose project is DEMO-coded.
        yield return "DELETE l FROM `epc_erp_gl_lines` l INNER JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id`"
                     + " WHERE j.`source_type`='fitout_advance' AND j.`source_id` IN"
                     + " (SELECT `id` FROM `epc_prj_projects` WHERE `code` LIKE 'DEMO-%' OR `name` LIKE 'DEMO |%')";
        yield return "DELETE j FROM `epc_erp_gl_journals` j"
                     + " WHERE j.`source_type`='fitout_advance' AND j.`source_id` IN"
                     + " (SELECT `id` FROM `epc_prj_projects` WHERE `code` LIKE 'DEMO-%' OR `name` LIKE 'DEMO |%')";
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
    /// Demo rows are found by the DEMO tag only: `DEMO-` on code/reference/category
    /// columns or `DEMO |` on name/title/description columns. Account tables are
    /// deliberately absent — demo clear never touches them.
    /// </summary>
    private static IEnumerable<(string Table, string[] Columns)> DemoTables()
    {
        yield return ("ecomae_fitout_boq_lines", ["section", "description"]);
        yield return ("ecomae_fitout_estimates", ["code", "title"]);
        yield return ("ecomae_fitout_quotations", ["code", "title"]);
        yield return ("ecomae_fitout_cost_codes", ["code", "name"]);
        yield return ("ecomae_fitout_material_movements", ["item_code", "reference", "description"]);
        yield return ("ecomae_fitout_procurement_links", ["description"]);
        yield return ("ecomae_fitout_three_way_matches", ["invoice_reference"]);
        yield return ("ecomae_fitout_delivery_records", ["reference", "title", "description"]);
        yield return ("epc_prja_budget", ["category"]);
        yield return ("epc_prja_txn", ["category", "description"]);
        yield return ("epc_prja_recognition", ["method"]);
        yield return ("epc_cc_allocations", ["run_label"]);
        yield return ("epc_erp_suppliers", ["name", "vendor_account"]);
        yield return ("epc_erp_inv_items", ["code", "name"]);
        yield return ("epc_erp_inv_warehouses", ["code", "name"]);
        yield return ("epc_warehouses", ["code", "name"]);
        yield return ("epc_erp_customers", ["name"]);
        yield return ("epc_prj_projects", ["code", "name"]);
    }

    private static IEnumerable<string> DemoLinkedDeletes()
    {
        const string demoProjects = "SELECT `id` FROM `epc_prj_projects` WHERE `code` LIKE 'DEMO-%' OR `name` LIKE 'DEMO |%'";
        const string demoRecords = "SELECT `id` FROM `ecomae_fitout_delivery_records` WHERE `reference` LIKE 'DEMO-%' OR `title` LIKE 'DEMO |%' OR `description` LIKE 'DEMO |%'";
        yield return "DELETE FROM `ecomae_fitout_contract_terms` WHERE `contract_id` IN (" + demoProjects + ")";
        yield return "DELETE FROM `epc_prja_recognition` WHERE `method` LIKE 'DEMO-%' OR `project_id` IN (" + demoProjects + ")";
        yield return "DELETE FROM `ecomae_fitout_approval_audit` WHERE `delivery_record_id` IN (" + demoRecords + ")";
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

    private static async Task<int> SeedCustomersAsync(DbConnection connection, CancellationToken ct)
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
                count++;
            }
            catch (DbException)
            {
            }
        }

        return count;
    }

    private async Task<List<long>> SeedSuppliersAsync(DbConnection connection, CancellationToken ct)
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
                ids.Add(await ErpDb.LastInsertIdAsync(connection, null, ct).ConfigureAwait(false));
            }
            catch (DbException)
            {
            }
        }

        return ids;
    }

    private static async Task SeedWarehouseAsync(DbConnection connection, CancellationToken ct)
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

    private static async Task<int> SeedItemsAsync(DbConnection connection, CancellationToken ct)
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
        // Write only columns that exist — provisioned item tables differ.
        var optional = new (string Column, Func<(string Code, string Name, decimal Cost), object?> Value)[]
        {
            ("unit_cost", i => i.Cost),
            ("warehouse_id", _ => 1L),
            ("created_at_utc", _ => DateTime.UtcNow),
        };
        var columns = new List<string> { "code", "name" };
        var extras = new List<Func<(string Code, string Name, decimal Cost), object?>>();
        foreach (var (column, value) in optional)
        {
            if (await HasColumnAsync(connection, "epc_erp_inv_items", column, ct).ConfigureAwait(false))
            {
                columns.Add(column);
                extras.Add(value);
            }
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
                count++;
            }
            catch (DbException)
            {
            }
        }

        return count;
    }

    private async Task<List<ErpFitOutProject>> SeedProjectsAsync(int customerCount, CancellationToken ct)
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
            }
        }

        return list;
    }

    private async Task<int> SeedCostCodesAsync(
        List<ErpFitOutProject> projects,
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
                    if (!child && code == "DEMO-CC-100")
                    {
                        parents[code] = saved.Id;
                    }
                }
            }
        }

        return count;
    }

    private async Task<int> SeedEstimatesAsync(List<ErpFitOutProject> projects, CancellationToken ct)
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
                count += line.Succeeded ? 1 : 0;
            }

            // One BOQ revision per project: a second revision row (same code) so
            // revision 1 stays comparable against the changed scope.
            var revised = await _estimates.SaveEstimateAsync(
                new ErpFitOutEstimateSaveRequest(
                    ProjectId: project.Id,
                    Code: RefPrefix + "EST-" + project.Id,
                    Title: NamePrefix + "Rev 2 — client scope change (glazing upgrade) " + shortName,
                    Revision: 2,
                    MarkupPercent: 15m,
                    Status: "approved"),
                ct).ConfigureAwait(false);
            count += revised.Succeeded ? 1 : 0;

            var quotation = await _quotations.SaveAsync(
                new ErpFitOutQuotationSaveRequest(
                    EstimateId: estimate.Id,
                    Code: RefPrefix + "QT-" + project.Id,
                    Title: NamePrefix + "Quotation " + shortName,
                    Revision: 1,
                    ExpiresOn: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                    Status: "issued"),
                ct).ConfigureAwait(false);
            count += quotation.Succeeded ? 1 : 0;
        }

        return count;
    }

    private async Task<int> SeedContractsAsync(List<ErpFitOutProject> projects, CancellationToken ct)
    {
        var count = 0;
        foreach (var project in projects)
        {
            var result = await _contracts.SaveAsync(
                new ErpFitOutContractTermsSaveRequest(project.Id, 10m, 5m, 12),
                ct).ConfigureAwait(false);
            count += result.Succeeded ? 1 : 0;
        }

        return count;
    }

    private async Task<int> SeedBudgetsAsync(List<ErpFitOutProject> projects, CancellationToken ct)
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
        }

        return count;
    }

    private async Task<int> SeedProcurementAsync(List<ErpFitOutProject> projects, CancellationToken ct)
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
            count += match.Succeeded ? 1 : 0;
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
                count += match.Succeeded ? 1 : 0;
            }
        }

        return count;
    }

    private async Task<int> SeedMaterialMovementsAsync(
        List<ErpFitOutProject> projects,
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
            count += result.Succeeded ? 1 : 0;
        }

        return count;
    }

    private async Task<int> SeedSiteRecordsAsync(
        List<ErpFitOutProject> projects,
        List<long> suppliers,
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
                count += result.Succeeded ? 1 : 0;
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
                count += rfq.Succeeded ? 1 : 0;
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
                count += bill.Succeeded ? 1 : 0;
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
                count += measurement.Succeeded ? 1 : 0;
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
                    await _records.DecideApprovalAsync(certification.Id, "approved", 0, ct).ConfigureAwait(false);
                }
            }
        }

        return count;
    }

    private async Task<int> SeedClaimsAndRecognitionAsync(
        List<ErpFitOutProject> projects,
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
                count += progress.Succeeded ? 1 : 0;

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
            count += run.Succeeded ? 1 : 0;
            var cost = await _txns.AddAsync(
                new ErpPrjaTxnAddWriteRequest(
                    ProjectId: project.Id,
                    TxnType: "cost",
                    Category: RefPrefix + "COST",
                    Description: "DEMO | Earned cost to date",
                    Amount: project.BudgetCost * 0.32m,
                    TxnDate: asOf),
                ct).ConfigureAwait(false);
            count += cost.Succeeded ? 1 : 0;
            var revenue = await _txns.AddAsync(
                new ErpPrjaTxnAddWriteRequest(
                    ProjectId: project.Id,
                    TxnType: "revenue",
                    Category: RefPrefix + "REV",
                    Description: "DEMO | Earned revenue to date",
                    Amount: project.ContractValue * 0.32m,
                    TxnDate: asOf),
                ct).ConfigureAwait(false);
            count += revenue.Succeeded ? 1 : 0;
        }

        return count;
    }

    private async Task<int> SeedAllocationsAsync(
        List<ErpFitOutProject> projects,
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
                weight = Math.Max(0.1m, weight - 0.15m);
            }
        }

        if (weights.Count == 0)
        {
            return 0;
        }

        var run = await _costCenters.PostAllocationAsync(
            RefPrefix + "SHARED-1", 15000m, weights, ct).ConfigureAwait(false);
        return run.Succeeded ? weights.Count : 0;
    }
}
