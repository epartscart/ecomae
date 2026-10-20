using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Generic core-entity DEMO sample data so every workspace area shows real
/// rows (warehouses, items, stock, movements, reservations, suppliers,
/// purchases, customers, sales orders, invoices, cash/bank, GL journal,
/// staff, payroll, BOM, work order, RMA, budgets, periods, audit log).
/// Rows are tagged "DEMO" in a natural marker column so the clear path can
/// remove exactly what was seeded without touching live data.
/// </summary>
public interface IErpCoreDemoSeedWriteService
{
    Task<string> SeedAsync(CancellationToken cancellationToken = default);

    Task<string> ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class ErpCoreDemoSeedWriteService : IErpCoreDemoSeedWriteService
{
    private const string Tag = "DEMO";

    private readonly IErpWriteConnectionFactory _connections;

    public ErpCoreDemoSeedWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<string> SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured.";
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpCoverageSweepSpecs.EnsureAllAsync(c, cancellationToken).ConfigureAwait(false);
        lock (Skipped) { Skipped.Clear(); }
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var p = ErpDb.Positional;

        // Warehouse + items + stock + movements + reservations.
        await Exec(c, "INSERT IGNORE INTO `epc_erp_inv_warehouses` (`code`,`name`) VALUES ('WH-DEMO','Demo Main Warehouse')");
        var whId = await ScalarSafe(c, "SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `code`='WH-DEMO' ORDER BY `id` LIMIT 1");
        for (var i = 1; i <= 8; i++)
        {
            var sku = $"DEMO-ITEM-{i:00}";
            await Exec(c, p("INSERT IGNORE INTO `epc_erp_inv_items` (`sku`,`name`,`item_type`,`unit`,`unit_cost`,`sales_price`,`reorder_level`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?,1,?)"),
                sku, $"Demo Item {i} (sample)", i % 2 == 0 ? "stocked" : "service", "ea", 12.5m * i, 25m * i, 5, now);
            var itemId = await ScalarSafe(c, p("SELECT `id` FROM `epc_erp_inv_items` WHERE `sku`=? LIMIT 1"), sku);
            if (whId > 0 && itemId > 0)
            {
                await Exec(c, p("INSERT INTO `epc_erp_inv_stock` (`warehouse_id`,`item_id`,`qty_on_hand`,`avg_unit_cost`,`batch_no`,`time_updated`) VALUES (?,?,?,?,?,?)"),
                    whId, itemId, 40 + 5 * i, 12.5m * i, $"DEMO-B{i}", now);
                await Exec(c, p("INSERT INTO `epc_erp_inv_movements` (`movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`reference`,`note`,`movement_date`,`active`) VALUES ('purchase_in',?,?,?,?,?,?,?,?,1)"),
                    whId, itemId, 40 + 5 * i, 12.5m * i, (40 + 5 * i) * 12.5m * i, "DEMO-RCPT", "Demo opening receipt", now - i * 86400);
                await Exec(c, p("INSERT INTO `epc_erp_inv_reservations` (`item_id`,`sku`,`warehouse_id`,`qty`,`backorder_qty`,`source_type`,`source_id`,`status`,`reserved_at`,`time_updated`) VALUES (?,?,?,?,?,'demo',0,'held',?,?)"),
                    itemId, sku, whId, 3, 0, now, now);
            }
        }

        // Suppliers + purchases + AP ledger.
        for (var i = 1; i <= 3; i++)
        {
            var code = $"DEMO-VND-{i}";
            await Exec(c, p("INSERT INTO `epc_erp_suppliers` (`name`,`supplier_code`,`vendor_group`,`country_code`,`payment_terms`,`active`,`time_created`) VALUES (?,?,?,?,?,1,?)"),
                $"Demo Supplier {i}", code, i == 1 ? "raw-materials" : "services", "AE", i == 1 ? "Net 30" : "Immediate", now);
            var supId = await ScalarSafe(c, p("SELECT `id` FROM `epc_erp_suppliers` WHERE `supplier_code`=? ORDER BY `id` DESC LIMIT 1"), code);
            if (supId > 0)
            {
                await Exec(c, p("INSERT INTO `epc_erp_purchases` (`supplier_id`,`invoice_number`,`purchase_date`,`amount_ex_vat`,`vat_amount`,`total_amount`,`status`,`note`,`active`,`time_created`) VALUES (?,?,?,?,?,?,?,?,1,?)"),
                    supId, $"DEMO-PI-{i:000}", now - i * 86400 * 9, 900m * i, 45m * i, 945m * i, "confirmed", "DEMO purchase invoice", now);
                await Exec(c, p("INSERT INTO `epc_erp_supplier_accounting` (`supplier_id`,`time`,`is_credit`,`amount`,`reference`,`note`,`active`,`entry_kind`) VALUES (?,?,1,?,?,?,1,'invoice')"),
                    supId, now - i * 86400 * 9, 945m * i, $"DEMO-PI-{i:000}", "Demo vendor invoice");
                await Exec(c, p("INSERT INTO `epc_erp_supplier_accounting` (`supplier_id`,`time`,`is_credit`,`amount`,`reference`,`note`,`active`,`entry_kind`) VALUES (?,?,0,?,?,?,1,'payment')"),
                    supId, now - i * 86400 * 4, 300m * i, $"DEMO-PAY-{i:000}", "Demo vendor payment");
            }
        }

        // Sales orders + einvoice documents (customers).
        for (var i = 1; i <= 4; i++)
        {
            var so = $"SO-DEMO-{i:000}";
            await Exec(c, p("INSERT INTO `epc_erp_sales_orders` (`so_no`,`title`,`amount_ex_vat`,`vat_amount`,`total_amount`,`status`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?)"),
                so, $"Demo sales order {i}", 1200m * i, 60m * i, 1260m * i, i <= 2 ? "confirmed" : "draft", "DEMO order", now - i * 86400 * 3, now);
            var buyerJson = $"{{\"name\":\"Demo Customer {i}\",\"trn\":\"1000000000000{i:D2}\",\"email\":\"demo{i}@example.test\"}}";
            await Exec(c, p("INSERT INTO `epc_einvoice_documents` (`invoice_number`,`doc_category`,`issue_date`,`payment_due_date`,`buyer_json`,`subtotal_ex_vat`,`total_vat`,`total_incl_vat`,`paid_amount`,`amount_due`,`status`,`admin_id`,`active`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,0,1,?,?)"),
                $"DEMO-INV-{i:000}", "tax_invoice", DateTime.UtcNow.AddDays(-6 * i).ToString("yyyy-MM-dd"), DateTime.UtcNow.AddDays(30 - 6 * i).ToString("yyyy-MM-dd"), buyerJson, 1200m * i, 60m * i, 1260m * i, i <= 2 ? 630m * i : 0m, i <= 2 ? 630m * i : 1260m * i, "validated", now - i * 86400 * 6, now);
            var docId = await ScalarSafe(c, p("SELECT `id` FROM `epc_einvoice_documents` WHERE `invoice_number`=? ORDER BY `id` DESC LIMIT 1"), $"DEMO-INV-{i:000}");
            if (docId > 0)
            {
                await Exec(c, p("INSERT INTO `epc_einvoice_lines` (`document_id`,`line_no`,`item_name`,`quantity`,`unit_price`,`line_net`,`tax_amount`,`gross_amount`) VALUES (?,?,?,?,?,?,?,?)"),
                    docId, 1, $"Demo line {i}", 4, 300m * i, 1200m * i, 60m * i, 1260m * i);
            }
        }

        // Cash/bank account + entries + statement lines.
        await Exec(c, p("INSERT INTO `epc_erp_cash_bank_accounts` (`name`,`account_type`,`bank_name`,`account_number`,`currency_code`,`opening_balance`,`active`,`time_created`) VALUES ('Demo Operating Account','bank','Demo Bank','IBAN-DEMO-01','AED',50000,1,?)"), now);
        var accId = await ScalarSafe(c, "SELECT `id` FROM `epc_erp_cash_bank_accounts` WHERE `account_number`='IBAN-DEMO-01' ORDER BY `id` DESC LIMIT 1");
        if (accId > 0)
        {
            for (var i = 1; i <= 5; i++)
            {
                await Exec(c, p("INSERT INTO `epc_erp_cash_bank_entries` (`account_id`,`time`,`entry_type`,`direction`,`amount`,`reference`,`note`,`admin_id`,`active`) VALUES (?,?,?,?,?,?,?,0,1)"),
                    accId, now - i * 86400 * 2, i % 2 == 0 ? "receipt" : "payment", i % 2 == 0 ? 1 : 0, 750m * i, $"DEMO-CB-{i:000}", "Demo cash/bank movement");
            }
        }

        // GL journal + lines against COA (first two accounts).
        var coa1 = await ScalarSafe(c, "SELECT `id` FROM `epc_erp_coa_accounts` ORDER BY `id` LIMIT 1");
        var coa2 = await ScalarSafe(c, "SELECT `id` FROM `epc_erp_coa_accounts` ORDER BY `id` DESC LIMIT 1");
        if (coa1 > 0 && coa2 > 0)
        {
            await Exec(c, p("INSERT INTO `epc_erp_gl_journals` (`journal_no`,`journal_date`,`reference`,`description`,`source_type`,`source_id`,`admin_id`,`active`,`time_created`) VALUES (?,?,?,?,?,?,0,1,?)"),
                "GJ-DEMO-001", DateTime.UtcNow.ToString("yyyy-MM-dd"), "DEMO-JNL", "Demo journal — sample posting", "adjustment", 0, now);
            var jnId = await ScalarSafe(c, "SELECT `id` FROM `epc_erp_gl_journals` WHERE `journal_no`='GJ-DEMO-001' ORDER BY `id` DESC LIMIT 1");
            if (jnId > 0)
            {
                await Exec(c, p("INSERT INTO `epc_erp_gl_lines` (`journal_id`,`coa_id`,`debit`,`credit`,`line_note`) VALUES (?,?,?,0,'Demo debit')"), jnId, coa1, 5000m);
                await Exec(c, p("INSERT INTO `epc_erp_gl_lines` (`journal_id`,`coa_id`,`debit`,`credit`,`line_note`) VALUES (?,?,0,?,'Demo credit')"), jnId, coa2, 5000m);
            }
        }

        // Staff + payroll run + lines.
        for (var i = 1; i <= 3; i++)
        {
            await Exec(c, p("INSERT INTO `epc_erp_staff_profiles` (`user_id`,`department_code`,`display_name`,`job_title`,`email`,`active`,`time_created`) VALUES (0,?,?,?,?,1,?)"),
                i == 1 ? "OPS" : "FIN", $"Demo Staff {i}", i == 1 ? "Warehouse Operative" : "Accountant", $"staff{i}@demo.test", now);
        }
        await Exec(c, p("INSERT INTO `epc_erp_payroll_runs` (`period_label`,`period_start`,`period_end`,`status`,`total_gross`,`total_deductions`,`total_net`,`note`,`created_by`,`time_created`) VALUES (?,?,?,?,?,?,?,?,'demo',?)"),
            "DEMO " + DateTime.UtcNow.ToString("yyyy-MM"), "2026-10-01", "2026-10-31", "draft", 21000m, 1200m, 19800m, "DEMO payroll", now);
        var runId = await ScalarSafe(c, "SELECT `id` FROM `epc_erp_payroll_runs` WHERE `note`='DEMO payroll' ORDER BY `id` DESC LIMIT 1");
        if (runId > 0)
        {
            await Exec(c, p("INSERT INTO `epc_erp_payroll_lines` (`run_id`,`staff_profile_id`,`department_code`,`display_name`,`job_title`,`basic_salary`,`allowances`,`deductions`,`net_pay`,`status`,`time_created`) VALUES (?,0,'OPS','Demo Staff 1','Warehouse Operative',6000,1000,400,6600,'draft',?)"), runId, now);
            await Exec(c, p("INSERT INTO `epc_erp_payroll_lines` (`run_id`,`staff_profile_id`,`department_code`,`display_name`,`job_title`,`basic_salary`,`allowances`,`deductions`,`net_pay`,`status`,`time_created`) VALUES (?,0,'FIN','Demo Staff 2','Accountant',9000,2000,800,10200,'draft',?)"), runId, now);
            await Exec(c, p("INSERT INTO `epc_erp_payroll_lines` (`run_id`,`staff_profile_id`,`department_code`,`display_name`,`job_title`,`basic_salary`,`allowances`,`deductions`,`net_pay`,`status`,`time_created`) VALUES (?,0,'FIN','Demo Staff 3','Controller',4000,800,0,4800,'draft',?)"), runId, now);
        }

        // BOM + work order.
        var prodItem = await ScalarSafe(c, "SELECT `id` FROM `epc_erp_inv_items` WHERE `sku`='DEMO-ITEM-01' LIMIT 1");
        if (prodItem > 0)
        {
            await Exec(c, p("INSERT INTO `epc_mfg_bom` (`product_item_id`,`name`,`output_qty`,`labour_cost`,`overhead_cost`,`active`,`time_created`) VALUES (?,'DEMO-BOM-01',1,250,120,1,?)"), prodItem, now);
            var bomId = await ScalarSafe(c, "SELECT `id` FROM `epc_mfg_bom` WHERE `name`='DEMO-BOM-01' ORDER BY `id` DESC LIMIT 1");
            if (bomId > 0)
            {
                await Exec(c, p("INSERT INTO `epc_mfg_work_orders` (`wo_no`,`bom_id`,`product_item_id`,`qty_planned`,`qty_produced`,`status`,`material_cost`,`labour_cost`,`overhead_cost`,`time_created`,`time_updated`) VALUES ('WO-DEMO-001',?,?,10,4,'in_progress',3000,250,120,?,?)"),
                    bomId, prodItem, now, now);
            }
        }

        // RMA + item.
        await Exec(c, p("INSERT INTO `epc_rma_requests` (`site_key`,`rma_number`,`customer_name`,`reason`,`description`,`status`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,NOW(),NOW())"),
            "", "DEMO-RMA-001", "Demo Customer 1", "damaged", "Demo return case", "pending");
        var rmaId = await ScalarSafe(c, "SELECT `id` FROM `epc_rma_requests` WHERE `rma_number`='DEMO-RMA-001' ORDER BY `id` DESC LIMIT 1");
        if (rmaId > 0)
        {
            await Exec(c, p("INSERT INTO `epc_rma_items` (`rma_id`,`product_sku`,`product_name`,`qty`,`unit_price`) VALUES (?,'DEMO-ITEM-01','Demo Item 1 (sample)',1,125)"), rmaId);
        }

        // Budget + project budget + period + audit trail.
        await Exec(c, p("INSERT INTO `epc_erp_pm_budgets` (`code`,`name`,`fiscal_year`,`is_master`,`note`,`active`,`time_created`,`time_updated`) VALUES ('DEMO-BUDGET','Demo FY Budget',2026,1,'DEMO budget',1,?,?)"), now, now);
        var budId = await ScalarSafe(c, "SELECT `id` FROM `epc_erp_pm_budgets` WHERE `code`='DEMO-BUDGET' ORDER BY `id` DESC LIMIT 1");
        if (budId > 0)
        {
            await Exec(c, p("INSERT INTO `epc_erp_pm_budget_lines` (`budget_id`,`account_code`,`account_name`,`month_no`,`amount`,`time_updated`) VALUES (?,?,?,?,?,?)"), budId, "4000", "Demo revenue", 10, 150000m, now);
            await Exec(c, p("INSERT INTO `epc_erp_pm_budget_lines` (`budget_id`,`account_code`,`account_name`,`month_no`,`amount`,`time_updated`) VALUES (?,?,?,?,?,?)"), budId, "6000", "Demo cost", 10, 90000m, now);
        }
        await Exec(c, p("INSERT INTO `epc_prja_budget` (`company_id`,`project_id`,`category`,`cost_budget`,`revenue_budget`,`time_created`) VALUES (0,900001,'DEMO-project',75000,120000,?)"), now);
        await Exec(c, "INSERT INTO `epc_erp_audit_log` (`time`,`admin_id`,`action`,`entity_type`,`entity_id`,`summary`,`detail_json`,`ip_address`,`user_agent`)"
            + $" VALUES ({now},0,'demo.seed','workspace',0,'DEMO sample data loaded','{{\"demo\":true}}','127.0.0.1','devin-demo-seed')");

        string skipped;
        lock (Skipped) { skipped = Skipped.Count == 0 ? "" : $" ({Skipped.Count} statements skipped: {string.Join(" | ", Skipped.Distinct().Take(3))})"; }
        return "DEMO sample data loaded — items, suppliers, invoices, orders, cash/bank, GL, payroll, production, RMA, budgets, projects." + skipped;
    }

    public async Task<string> ClearAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return "TenantRegistry DB is not configured.";
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpCoverageSweepSpecs.EnsureAllAsync(c, cancellationToken).ConfigureAwait(false);

        string[] deletes =
        {
            "DELETE s FROM `epc_erp_inv_stock` s INNER JOIN `epc_erp_inv_items` i ON i.`id`=s.`item_id` WHERE i.`sku` LIKE 'DEMO-ITEM-%'",
            "DELETE m FROM `epc_erp_inv_movements` m INNER JOIN `epc_erp_inv_items` i ON i.`id`=m.`item_id` WHERE i.`sku` LIKE 'DEMO-ITEM-%' OR m.`reference` LIKE 'DEMO-%'",
            "DELETE FROM `epc_erp_inv_reservations` WHERE `sku` LIKE 'DEMO-%' OR `source_type`='demo'",
            "DELETE FROM `epc_erp_inv_items` WHERE `sku` LIKE 'DEMO-ITEM-%'",
            "DELETE FROM `epc_erp_inv_warehouses` WHERE `code`='WH-DEMO'",
            "DELETE p FROM `epc_erp_purchases` p INNER JOIN `epc_erp_suppliers` s ON s.`id`=p.`supplier_id` WHERE s.`supplier_code` LIKE 'DEMO-VND-%' OR p.`invoice_number` LIKE 'DEMO-%'",
            "DELETE a FROM `epc_erp_supplier_accounting` a INNER JOIN `epc_erp_suppliers` s ON s.`id`=a.`supplier_id` WHERE s.`supplier_code` LIKE 'DEMO-VND-%' OR a.`reference` LIKE 'DEMO-%'",
            "DELETE FROM `epc_erp_suppliers` WHERE `supplier_code` LIKE 'DEMO-VND-%'",
            "DELETE FROM `epc_erp_sales_orders` WHERE `so_no` LIKE 'SO-DEMO-%' OR `notes` LIKE 'DEMO%'",
            "DELETE l FROM `epc_einvoice_lines` l INNER JOIN `epc_einvoice_documents` d ON d.`id`=l.`document_id` WHERE d.`invoice_number` LIKE 'DEMO-%'",
            "DELETE FROM `epc_einvoice_documents` WHERE `invoice_number` LIKE 'DEMO-%'",
            "DELETE e FROM `epc_erp_cash_bank_entries` e INNER JOIN `epc_erp_cash_bank_accounts` a ON a.`id`=e.`account_id` WHERE a.`account_number`='IBAN-DEMO-01' OR e.`reference` LIKE 'DEMO-%'",
            "DELETE FROM `epc_erp_cash_bank_accounts` WHERE `account_number`='IBAN-DEMO-01'",
            "DELETE l FROM `epc_erp_gl_lines` l INNER JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id` WHERE j.`journal_no`='GJ-DEMO-001'",
            "DELETE FROM `epc_erp_gl_journals` WHERE `journal_no`='GJ-DEMO-001'",
            "DELETE FROM `epc_erp_staff_profiles` WHERE `email` LIKE 'staff%@demo.test'",
            "DELETE FROM `epc_erp_payroll_lines` WHERE `run_id` IN (SELECT `id` FROM `epc_erp_payroll_runs` WHERE `note`='DEMO payroll')",
            "DELETE FROM `epc_erp_payroll_runs` WHERE `note`='DEMO payroll'",
            "DELETE w FROM `epc_mfg_work_orders` w WHERE w.`wo_no`='WO-DEMO-001'",
            "DELETE FROM `epc_mfg_bom` WHERE `name`='DEMO-BOM-01'",
            "DELETE i FROM `epc_rma_items` i INNER JOIN `epc_rma_requests` r ON r.`id`=i.`rma_id` WHERE r.`rma_number`='DEMO-RMA-001'",
            "DELETE FROM `epc_rma_requests` WHERE `rma_number`='DEMO-RMA-001'",
            "DELETE FROM `epc_erp_pm_budget_lines` WHERE `budget_id` IN (SELECT `id` FROM `epc_erp_pm_budgets` WHERE `code`='DEMO-BUDGET')",
            "DELETE FROM `epc_erp_pm_budgets` WHERE `code`='DEMO-BUDGET'",
            "DELETE FROM `epc_prja_budget` WHERE `category`='DEMO-project'",
            "DELETE FROM `epc_erp_audit_log` WHERE `action`='demo.seed'",
        };

        var removed = 0;
        foreach (var sql in deletes)
        {
            try
            {
                removed += await ErpDb.ExecuteAsync(c, null, sql, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // table absent on this tenant — nothing to clear
            }
        }

        return $"DEMO sample data removed ({removed} rows).";
    }

    private static async Task<long> Scalar(DbConnection c, string sql, params object[] args)
    {
        var value = await ErpDb.ScalarAsync(c, null, sql, CancellationToken.None, args.Cast<object?>().ToArray()).ConfigureAwait(false);
        return value is null ? 0L : Convert.ToInt64(value);
    }

    private static readonly List<string> Skipped = new();

    private static async Task Exec(DbConnection c, string sql, params object[] args)
    {
        try
        {
            await ErpDb.ExecuteAsync(c, null, sql, CancellationToken.None, args.Cast<object?>().ToArray()).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            lock (Skipped)
            {
                Skipped.Add(ex.Message);
            }
        }
    }

    private static async Task<long> ScalarSafe(DbConnection c, string sql, params object[] args)
    {
        try
        {
            return await ScalarSafe(c, sql, args).ConfigureAwait(false);
        }
        catch (DbException ex)
        {
            lock (Skipped)
            {
                Skipped.Add(ex.Message);
            }

            return 0;
        }
    }
}
