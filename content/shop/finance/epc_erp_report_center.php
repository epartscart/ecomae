<?php
/**
 * Report center — standard per-module inquiries and reports.
 *
 * Each D365-style module exposes an "Inquiries and reports" set. This registry
 * defines those reports as pure data sources (closures returning rows) so a
 * single report tab can render any module's reports as a filterable, exportable
 * table. Reports are scoped to the active company.
 *
 * Reports are keyed and tagged with an `area` matching the nav area key. To add
 * a report, append to epc_rc_registry() with a `run` closure(PDO $db, int
 * $companyId): array<int,array<string,mixed>>.
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_rc_safe_rows')) {
    /** Run a source closure, swallowing schema/availability errors into an empty set. */
    function epc_rc_safe_rows(callable $fn, PDO $db, int $companyId): array
    {
        try {
            $rows = $fn($db, $companyId);
            return is_array($rows) ? $rows : array();
        } catch (Throwable $e) {
            return array();
        }
    }
}

if (!function_exists('epc_rc_table_rows')) {
    /**
     * Safe generic table read: company-scoped if the table has a company_id,
     * else unscoped. Caps rows. Returns empty on any error / missing table.
     * @return array<int,array<string,mixed>>
     */
    function epc_rc_table_rows(PDO $db, string $table, int $companyId, string $orderBy = 'id', int $limit = 500): array
    {
        if (!preg_match('/^[A-Za-z0-9_]+$/', $table)) {
            return array();
        }
        $order = preg_match('/^[A-Za-z0-9_]+$/', $orderBy) ? $orderBy : 'id';
        $hasCompany = false;
        try {
            $c = $db->query("SHOW COLUMNS FROM `$table` LIKE 'company_id'");
            $hasCompany = $c && $c->fetch(PDO::FETCH_ASSOC);
        } catch (Throwable $e) {
            return array();
        }
        try {
            if ($hasCompany) {
                $st = $db->prepare("SELECT * FROM `$table` WHERE `company_id`=? ORDER BY `$order` DESC LIMIT $limit");
                $st->execute(array($companyId));
            } else {
                $st = $db->query("SELECT * FROM `$table` ORDER BY `$order` DESC LIMIT $limit");
            }
            return $st ? ($st->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
        } catch (Throwable $e) {
            return array();
        }
    }
}

if (!function_exists('epc_rc_registry')) {
    /** @return array<int,array<string,mixed>> */
    function epc_rc_registry(): array
    {
        $reports = array();

        $add = function (string $key, string $area, string $name, string $desc, callable $run) use (&$reports): void {
            $reports[] = array('key' => $key, 'area' => $area, 'name' => $name, 'desc' => $desc, 'run' => $run);
        };

        // Accounts payable
        $add('ap_vendor_list', 'ap', 'Vendor list', 'All suppliers / vendors on file.', function (PDO $db, int $co): array {
            return function_exists('epc_erp_list_suppliers') ? epc_erp_list_suppliers($db) : array();
        });
        $add('ap_withholding', 'ap', 'Withholding register', 'Withholding tax applied on vendor payments.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_withholding.php';
            return epc_wht_txns($db, $co);
        });

        // Accounts receivable
        $add('ar_customer_list', 'ar', 'Customer master', 'Customer master records with credit and terms.', function (PDO $db, int $co): array {
            try {
                $st = $db->query("SELECT `customer_id`,`customer_account`,`customer_name`,`customer_group`,`currency_code`,`credit_limit`,`terms_days`,`risk_band`,`on_hold` FROM `epc_credit_profiles` ORDER BY `customer_id` DESC");
                return $st ? ($st->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
            } catch (Throwable $e) {
                return array();
            }
        });

        // Cash and bank management
        $add('bank_accounts', 'banking', 'Bank accounts', 'Cash and bank accounts.', function (PDO $db, int $co): array {
            return function_exists('epc_erp_list_cash_accounts') ? epc_erp_list_cash_accounts($db) : array();
        });
        $add('bank_instruments_rep', 'banking', 'Bank instruments', 'Letters of credit, guarantees and SBLC with status.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cash_treasury.php';
            return epc_cft_instruments($db, $co);
        });
        $add('cash_forecasts_rep', 'banking', 'Cash flow forecasts', 'Cash flow forecast headers.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_cash_treasury.php';
            return epc_cft_forecasts($db, $co);
        });

        // Budgeting
        $add('budget_plans', 'budgeting', 'Budget plans', 'Budget plans by stage.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_budget_planning.php';
            return epc_bplan_list($db, $co);
        });

        // Human resources
        $add('hr_job_reqs', 'people', 'Job requisitions', 'Open and closed recruitment requisitions.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_talent.php';
            return epc_hrt_jobs($db, $co);
        });
        $add('hr_reviews', 'people', 'Performance reviews', 'Performance reviews and overall ratings.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_hr_talent.php';
            return epc_hrt_reviews($db, $co);
        });

        // Procurement and sourcing
        $add('proc_requisitions', 'purchasing', 'Purchase requisitions', 'Requisitions by status.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_procurement.php';
            return epc_proc_reqs($db, $co);
        });
        $add('proc_categories', 'purchasing', 'Procurement categories', 'Category hierarchy.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_procurement.php';
            return epc_proc_categories($db, $co);
        });

        // Tax
        $add('tax_withholding', 'tax', 'Withholding register', 'Withholding tax transactions.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_withholding.php';
            return epc_wht_txns($db, $co);
        });
        $add('tax_er_runs', 'tax', 'Electronic reporting runs', 'Generated electronic reporting runs.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_elec_reporting.php';
            return epc_er_runs($db, $co);
        });

        // General ledger
        $add('gl_trial_balance', 'finance', 'Trial balance', 'Account balances as of today.', function (PDO $db, int $co): array {
            if (!function_exists('epc_erp_gl_trial_balance')) {
                return array();
            }
            $tb = epc_erp_gl_trial_balance($db, date('Y-m-d'));
            return is_array($tb) ? $tb : array();
        });
        $add('gl_journals', 'finance', 'Journal register', 'General ledger journals.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_erp_gl_journals', $co);
        });

        // Procurement — purchase orders
        $add('proc_pos', 'purchasing', 'Purchase orders', 'Purchase orders register.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_erp_purchase_orders', $co);
        });

        // Sales and marketing
        $add('sales_orders', 'sales', 'Sales orders', 'Sales orders register.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_erp_sales_orders', $co);
        });
        $add('sales_leads', 'sales', 'Leads', 'Prospect / lead pipeline.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_crm_leads', $co);
        });
        $add('sales_quotes', 'sales', 'Sales quotations', 'Quotations / proposals.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_crm_quotes', $co);
        });

        // Inventory management
        $add('inv_items', 'inventory_mgmt', 'Item list', 'Released products / items.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_erp_inv_items', $co);
        });
        $add('inv_stock', 'inventory_mgmt', 'On-hand stock', 'Inventory on-hand by item.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_erp_inv_stock', $co);
        });

        // Product information management
        $add('pim_items', 'pim', 'Products', 'Product master records.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_erp_inv_items', $co);
        });

        // Fixed assets
        $add('fa_assets', 'fixed_assets', 'Fixed asset register', 'Fixed assets on file.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_erp_fa_assets', $co);
        });

        // Asset management
        $add('eam_assets', 'asset_mgmt', 'Asset register', 'Maintained / operational assets.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_eam_assets', $co);
        });

        // Project management and accounting
        $add('prj_projects', 'projects', 'Projects', 'Projects register.', function (PDO $db, int $co): array {
            $rows = epc_rc_table_rows($db, 'epc_crm_projects', $co);
            return !empty($rows) ? $rows : epc_rc_table_rows($db, 'epc_prj_projects', $co);
        });

        // Expense management
        $add('exp_reports', 'expense', 'Expense reports', 'Submitted expense reports.', function (PDO $db, int $co): array {
            $rows = epc_rc_table_rows($db, 'epc_erp_expense_reports', $co);
            return !empty($rows) ? $rows : epc_rc_table_rows($db, 'epc_hr_expenses', $co);
        });

        // Payroll
        $add('payroll_runs', 'payroll_area', 'Payroll runs', 'Payroll run history.', function (PDO $db, int $co): array {
            $rows = epc_rc_table_rows($db, 'epc_erp_payroll_runs', $co);
            return !empty($rows) ? $rows : epc_rc_table_rows($db, 'epc_hr_payroll_runs', $co);
        });

        // Cost accounting / cost management
        $add('cost_items', 'cost_acct', 'Cost items', 'Cost accounting items.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_costm_item', $co);
        });
        $add('cost_mgmt_items', 'cost_mgmt', 'Costed items', 'Inventory cost / valuation items.', function (PDO $db, int $co): array {
            return epc_rc_table_rows($db, 'epc_costm_item', $co);
        });

        // Credit and collections
        $add('credit_holds', 'credit_coll', 'Customers on credit hold', 'Customers currently on hold.', function (PDO $db, int $co): array {
            try {
                $st = $db->query("SELECT `customer_id`,`customer_name`,`credit_limit`,`terms_days`,`risk_band` FROM `epc_credit_profiles` WHERE `on_hold`=1 ORDER BY `customer_id` DESC");
                return $st ? ($st->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
            } catch (Throwable $e) {
                return array();
            }
        });

        // Working capital analysis
        $add('exec_working_capital', 'finance', 'Working capital analysis', 'AR, AP, inventory, cash position and current ratio.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_exec_dashboard.php';
            $wc = epc_exec_working_capital($db);
            return array(array(
                'Accounts Receivable' => number_format($wc['ar'], 2),
                'Accounts Payable' => number_format($wc['ap'], 2),
                'Inventory Value' => number_format($wc['inventory'], 2),
                'Cash & Bank' => number_format($wc['cash'], 2),
                'Net Working Capital' => number_format($wc['net_wc'], 2),
                'Current Ratio' => $wc['current_ratio'],
            ));
        });

        // AR aging
        $add('exec_ar_aging', 'ar', 'AR aging summary', 'Receivables aging: current, 30, 60, 90, 90+ days.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_exec_dashboard.php';
            $aging = epc_exec_ar_aging($db);
            return array(array(
                'Current (0-30 days)' => number_format($aging['current'], 2),
                '31-60 days' => number_format($aging['d30'], 2),
                '61-90 days' => number_format($aging['d60'], 2),
                '91-120 days' => number_format($aging['d90'], 2),
                'Over 120 days' => number_format($aging['over90'], 2),
            ));
        });

        // Cash flow forecast
        $add('exec_cash_forecast', 'banking', 'Cash flow forecast (3 months)', 'Projected cash inflows and outflows for the next 3 months.', function (PDO $db, int $co): array {
            require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_exec_dashboard.php';
            $forecast = epc_exec_cash_flow_forecast($db);
            $rows = array();
            foreach ($forecast as $m) {
                $rows[] = array(
                    'Month' => $m['month'],
                    'Expected Inflow' => number_format($m['inflow'], 2),
                    'Expected Outflow' => number_format($m['outflow'], 2),
                    'Net Cash Flow' => number_format($m['net'], 2),
                );
            }
            return $rows;
        });

        return $reports;
    }
}

if (!function_exists('epc_rc_reports_for')) {
    /** @return array<int,array<string,mixed>> reports registered for a module area */
    function epc_rc_reports_for(string $area): array
    {
        $out = array();
        foreach (epc_rc_registry() as $r) {
            if ($r['area'] === $area) {
                $out[] = $r;
            }
        }
        return $out;
    }
}

if (!function_exists('epc_rc_report_get')) {
    /** @return array<string,mixed>|null */
    function epc_rc_report_get(string $key): ?array
    {
        foreach (epc_rc_registry() as $r) {
            if ($r['key'] === $key) {
                return $r;
            }
        }
        return null;
    }
}

if (!function_exists('epc_rc_run')) {
    /**
     * Execute a report and return ordered columns + rows.
     * @return array{columns:array<int,string>,rows:array<int,array<string,mixed>>}
     */
    function epc_rc_run(PDO $db, string $key, int $companyId): array
    {
        $report = epc_rc_report_get($key);
        if (!$report) {
            throw new Exception('Report not found');
        }
        $rows = epc_rc_safe_rows($report['run'], $db, $companyId);
        $columns = array();
        if (!empty($rows)) {
            foreach (array_keys($rows[0]) as $c) {
                $columns[] = (string) $c;
            }
        }
        return array('columns' => $columns, 'rows' => $rows);
    }
}
