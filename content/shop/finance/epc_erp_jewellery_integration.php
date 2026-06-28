<?php
/**
 * Jewellery Industry Integration — adds jewellery-specific fields to existing
 * ERP modules when the tenant's industry = 'jewellery'.
 *
 * Architecture: ONE ERP, industry configuration determines field visibility.
 * Like Oracle Process Manufacturing dual-UOM — every transaction tracks BOTH
 * weight (grams/carats) AND value (AED). Reports show both dimensions.
 *
 * This file extends:
 *   - Inventory items:  karat, purity, metal_type, gross_wt, net_wt, stone details
 *   - Purchase orders:  metal_weight_gm, rate_per_gram, making_charge
 *   - Sales orders:     weight_sold_gm, rate_per_gram, making_charge, stone_value
 *   - Invoices:         weight lines alongside value lines
 *   - Service mgmt:     repair receipt, job card, delivery tracking
 *   - GL:               dual trial balance (weight + value)
 */
declare(strict_types=1);
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_helpers.php';
require_once __DIR__ . '/epc_erp_inventory.php';

/* ─── Detect jewellery industry ─── */

function epc_jw_is_jewellery_tenant(PDO $db): bool
{
    static $result = null;
    if ($result !== null) return $result;
    try {
        require_once __DIR__ . '/epc_erp_advanced.php';
        $key = epc_erp_adv_get_setting($db, 'erp_industry_profile', '');
        if ($key === 'jewellery') { $result = true; return true; }

        // Also check company industry pack (e.g. 'jewellery_diamond')
        if (function_exists('epc_erp_company_industry_pack')) {
            require_once __DIR__ . '/epc_erp_company_context.php';
            $companyId = function_exists('epc_erp_active_company_id')
                ? epc_erp_active_company_id($db) : 0;
            $pack = epc_erp_company_industry_pack($db, $companyId);
            if ($pack !== '' && strpos($pack, 'jewellery') === 0) {
                $result = true; return true;
            }
        }
        $result = false;
    } catch (Throwable $e) {
        $result = false;
    }
    return $result;
}

/* ─── Schema extensions for jewellery on existing tables ─── */

function epc_jw_ensure_integration_schema(PDO $db): void
{
    static $done = false;
    if ($done) return;
    $done = true;

    if (!function_exists('epc_erp_schema_add_column_if_missing')) {
        require_once __DIR__ . '/epc_erp_schema.php';
    }

    // Inventory items — jewellery-specific columns
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_metal_type', "varchar(16) NOT NULL DEFAULT ''");
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_karat', "varchar(10) NOT NULL DEFAULT ''");
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_purity', 'decimal(7,6) NOT NULL DEFAULT 0.000000');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_gross_wt', 'decimal(12,3) NOT NULL DEFAULT 0.000');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_net_wt', 'decimal(12,3) NOT NULL DEFAULT 0.000');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_stone_wt', 'decimal(12,3) NOT NULL DEFAULT 0.000');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_stone_type', "varchar(60) NOT NULL DEFAULT ''");
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_stone_pcs', 'int(11) NOT NULL DEFAULT 0');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_hallmark', "varchar(40) NOT NULL DEFAULT ''");
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_design_no', "varchar(30) NOT NULL DEFAULT ''");
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_making_charge_per_gm', 'decimal(12,2) NOT NULL DEFAULT 0.00');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_items', 'jw_division', "char(2) NOT NULL DEFAULT ''");

    // Purchase orders — jewellery weight & rate columns
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchase_orders', 'jw_metal_weight_gm', 'decimal(12,3) NOT NULL DEFAULT 0.000');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchase_orders', 'jw_rate_per_gram', 'decimal(12,2) NOT NULL DEFAULT 0.00');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchase_orders', 'jw_metal_value', 'decimal(14,2) NOT NULL DEFAULT 0.00');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchase_orders', 'jw_making_charge', 'decimal(14,2) NOT NULL DEFAULT 0.00');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchase_orders', 'jw_stone_value', 'decimal(14,2) NOT NULL DEFAULT 0.00');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchase_orders', 'jw_karat', "varchar(10) NOT NULL DEFAULT ''");
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_purchase_orders', 'jw_purity', 'decimal(7,6) NOT NULL DEFAULT 0.000000');

    // Inventory movements — weight tracking
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_movements', 'jw_weight_gm', 'decimal(12,3) NOT NULL DEFAULT 0.000');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_movements', 'jw_rate_per_gram', 'decimal(12,2) NOT NULL DEFAULT 0.00');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_movements', 'jw_karat', "varchar(10) NOT NULL DEFAULT ''");

    // Inventory stock — weight on hand
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_stock', 'jw_weight_on_hand', 'decimal(12,3) NOT NULL DEFAULT 0.000');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_inv_stock', 'jw_karat', "varchar(10) NOT NULL DEFAULT ''");

    // Sales order lines — add jewellery weight & rate columns to the existing voucher lines table.
    // The epc_erp_sales_order_lines table is created by epc_erp_vouchers_ensure_schema();
    // here we only add the jewellery-specific columns.
    require_once __DIR__ . '/epc_erp_vouchers.php';
    epc_erp_vouchers_ensure_schema($db);
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_sales_order_lines', 'jw_weight_gm', 'decimal(12,3) NOT NULL DEFAULT 0.000');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_sales_order_lines', 'jw_rate_per_gram', 'decimal(12,2) NOT NULL DEFAULT 0.00');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_sales_order_lines', 'jw_metal_value', 'decimal(14,2) NOT NULL DEFAULT 0.00');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_sales_order_lines', 'jw_making_charge', 'decimal(14,2) NOT NULL DEFAULT 0.00');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_sales_order_lines', 'jw_stone_value', 'decimal(14,2) NOT NULL DEFAULT 0.00');
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_sales_order_lines', 'jw_karat', "varchar(10) NOT NULL DEFAULT ''");
    epc_erp_schema_add_column_if_missing($db, 'epc_erp_sales_order_lines', 'jw_discount', 'decimal(14,2) NOT NULL DEFAULT 0.00');

    // Repair / service management for jewellery
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_jw_repairs` (
        `id` int(11) NOT NULL AUTO_INCREMENT,
        `repair_no` varchar(32) NOT NULL,
        `customer_id` int(11) NOT NULL DEFAULT 0,
        `customer_name` varchar(255) NOT NULL DEFAULT '',
        `customer_phone` varchar(30) NOT NULL DEFAULT '',
        `item_description` varchar(255) NOT NULL DEFAULT '',
        `metal_type` varchar(16) NOT NULL DEFAULT '',
        `karat` varchar(10) NOT NULL DEFAULT '',
        `gross_wt_in` decimal(12,3) NOT NULL DEFAULT 0.000,
        `net_wt_in` decimal(12,3) NOT NULL DEFAULT 0.000,
        `stone_details` text,
        `repair_type` varchar(60) NOT NULL DEFAULT '',
        `estimated_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
        `actual_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
        `status` enum('received','in_progress','ready','delivered','invoiced') NOT NULL DEFAULT 'received',
        `received_date` int(11) NOT NULL DEFAULT 0,
        `promised_date` int(11) NOT NULL DEFAULT 0,
        `delivered_date` int(11) NOT NULL DEFAULT 0,
        `gross_wt_out` decimal(12,3) NOT NULL DEFAULT 0.000,
        `workshop_notes` text,
        `invoice_id` int(11) NOT NULL DEFAULT 0,
        `admin_id` int(11) NOT NULL DEFAULT 0,
        `time_created` int(11) NOT NULL DEFAULT 0,
        `time_updated` int(11) NOT NULL DEFAULT 0,
        PRIMARY KEY (`id`),
        UNIQUE KEY `x_repair_no` (`repair_no`),
        KEY `x_status` (`status`),
        KEY `x_customer` (`customer_id`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8");

    // Jewellery GL weight ledger — tracks weight movements for dual trial balance
    $db->exec("CREATE TABLE IF NOT EXISTS `epc_erp_jw_weight_ledger` (
        `id` int(11) NOT NULL AUTO_INCREMENT,
        `account_code` varchar(20) NOT NULL,
        `account_name` varchar(120) NOT NULL DEFAULT '',
        `transaction_date` int(11) NOT NULL DEFAULT 0,
        `source_type` enum('purchase','sale','adjustment','transfer','repair','opening') NOT NULL DEFAULT 'purchase',
        `source_id` int(11) NOT NULL DEFAULT 0,
        `reference` varchar(64) NOT NULL DEFAULT '',
        `metal_type` varchar(16) NOT NULL DEFAULT '',
        `karat` varchar(10) NOT NULL DEFAULT '',
        `weight_in` decimal(12,3) NOT NULL DEFAULT 0.000,
        `weight_out` decimal(12,3) NOT NULL DEFAULT 0.000,
        `value_debit` decimal(14,2) NOT NULL DEFAULT 0.00,
        `value_credit` decimal(14,2) NOT NULL DEFAULT 0.00,
        `narration` varchar(255) NOT NULL DEFAULT '',
        `admin_id` int(11) NOT NULL DEFAULT 0,
        `time_created` int(11) NOT NULL DEFAULT 0,
        PRIMARY KEY (`id`),
        KEY `x_account` (`account_code`),
        KEY `x_date` (`transaction_date`),
        KEY `x_source` (`source_type`, `source_id`),
        KEY `x_metal` (`metal_type`, `karat`)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8");
}

/* ─── Jewellery fields HTML fragment for Inventory item form ─── */

function epc_jw_inventory_item_fields_html(PDO $db): string
{
    if (!epc_jw_is_jewellery_tenant($db)) return '';

    // Fetch karat list from jewellery master
    $karats = array();
    try {
        $companyId = function_exists('epc_erp_active_company_id')
            ? epc_erp_active_company_id($db) : 0;
        $st = $db->prepare('SELECT karat_code, std_purity FROM epc_jewel_karat_master WHERE company_id = ? ORDER BY std_purity DESC');
        $st->execute(array($companyId));
        $karats = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
    } catch (Throwable $e) {}

    $h = function($v) { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };

    $html = '<div class="form-group" style="background:#fef9e7;padding:10px;border:1px solid #f0e68c;border-radius:4px;margin:12px 0;">';
    $html .= '<label class="col-sm-12" style="color:#b8860b;font-weight:bold;margin-bottom:8px;"><i class="fa fa-diamond"></i> Jewellery attributes</label>';

    // Metal type
    $html .= '<div class="form-group"><label class="col-sm-3">Metal type</label><div class="col-sm-9">';
    $html .= '<select name="jw_metal_type" class="form-control input-sm">';
    $html .= '<option value="">— select —</option>';
    foreach (array('Gold', 'Silver', 'Platinum', 'White Gold', 'Rose Gold', 'Diamond', 'Pearl') as $m) {
        $html .= '<option value="' . $h($m) . '">' . $h($m) . '</option>';
    }
    $html .= '</select></div></div>';

    // Karat
    $html .= '<div class="form-group"><label class="col-sm-3">Karat</label><div class="col-sm-9">';
    $html .= '<select name="jw_karat" class="form-control input-sm" id="jw_karat_select">';
    $html .= '<option value="">— select —</option>';
    foreach ($karats as $k) {
        $html .= '<option value="' . $h($k['karat_code']) . '" data-purity="' . $h($k['std_purity']) . '">' . $h($k['karat_code']) . '</option>';
    }
    $html .= '</select></div></div>';

    // Purity (auto-fills from karat)
    $html .= '<div class="form-group"><label class="col-sm-3">Purity</label><div class="col-sm-9">';
    $html .= '<input type="number" step="0.000001" name="jw_purity" id="jw_purity_input" class="form-control input-sm" placeholder="e.g. 0.916700">';
    $html .= '</div></div>';

    // Division
    $html .= '<div class="form-group"><label class="col-sm-3">Division</label><div class="col-sm-9">';
    $html .= '<select name="jw_division" class="form-control input-sm">';
    foreach (array('G' => 'Gold', 'S' => 'Silver', 'T' => 'Platinum', 'D' => 'Diamond', 'P' => 'Pearl') as $code => $label) {
        $html .= '<option value="' . $h($code) . '">' . $h($code . ' — ' . $label) . '</option>';
    }
    $html .= '</select></div></div>';

    // Weights
    $html .= '<div class="form-group"><label class="col-sm-3">Gross / Net / Stone weight (g)</label>';
    $html .= '<div class="col-sm-9" style="display:flex;gap:6px;">';
    $html .= '<input type="number" step="0.001" name="jw_gross_wt" class="form-control input-sm" placeholder="Gross wt">';
    $html .= '<input type="number" step="0.001" name="jw_net_wt" class="form-control input-sm" placeholder="Net wt">';
    $html .= '<input type="number" step="0.001" name="jw_stone_wt" class="form-control input-sm" placeholder="Stone wt">';
    $html .= '</div></div>';

    // Stone details
    $html .= '<div class="form-group"><label class="col-sm-3">Stone type / pcs</label>';
    $html .= '<div class="col-sm-9" style="display:flex;gap:6px;">';
    $html .= '<input type="text" name="jw_stone_type" class="form-control input-sm" placeholder="e.g. Diamond, Ruby" style="flex:2">';
    $html .= '<input type="number" name="jw_stone_pcs" class="form-control input-sm" placeholder="Pcs" style="flex:1">';
    $html .= '</div></div>';

    // Design & hallmark
    $html .= '<div class="form-group"><label class="col-sm-3">Design no. / Hallmark</label>';
    $html .= '<div class="col-sm-9" style="display:flex;gap:6px;">';
    $html .= '<input type="text" name="jw_design_no" class="form-control input-sm" placeholder="Design no.">';
    $html .= '<input type="text" name="jw_hallmark" class="form-control input-sm" placeholder="Hallmark / cert no.">';
    $html .= '</div></div>';

    // Making charge per gram
    $html .= '<div class="form-group"><label class="col-sm-3">Making charge / gram</label><div class="col-sm-9">';
    $html .= '<input type="number" step="0.01" name="jw_making_charge_per_gm" class="form-control input-sm" placeholder="AED per gram">';
    $html .= '</div></div>';

    $html .= '</div>';

    // JS: auto-fill purity from karat selection
    $html .= '<script>document.getElementById("jw_karat_select")&&document.getElementById("jw_karat_select").addEventListener("change",function(){';
    $html .= 'var o=this.options[this.selectedIndex];var p=o.getAttribute("data-purity");';
    $html .= 'if(p&&document.getElementById("jw_purity_input"))document.getElementById("jw_purity_input").value=p;';
    $html .= '});</script>';

    return $html;
}

/* ─── Jewellery columns for Inventory stock table ─── */

function epc_jw_inventory_stock_columns_header(PDO $db): string
{
    if (!epc_jw_is_jewellery_tenant($db)) return '';
    return '<th>Metal</th><th>Karat</th><th class="num">Weight (g)</th>';
}

function epc_jw_inventory_stock_columns_row(PDO $db, array $row): string
{
    if (!epc_jw_is_jewellery_tenant($db)) return '';
    $h = function($v) { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };
    return '<td>' . $h($row['jw_metal_type'] ?? '') . '</td>'
         . '<td>' . $h($row['jw_karat'] ?? '') . '</td>'
         . '<td class="num">' . $h(number_format((float)($row['jw_weight_on_hand'] ?? 0), 3)) . '</td>';
}

/* ─── Jewellery fields for Purchase Order form ─── */

function epc_jw_purchase_order_fields_html(PDO $db): string
{
    if (!epc_jw_is_jewellery_tenant($db)) return '';
    $h = function($v) { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };

    $html = '<div style="background:#e8f4f8;padding:10px;border:1px solid #b3d7e8;border-radius:4px;margin:12px 0;">';
    $html .= '<label style="color:#2c5282;font-weight:bold;margin-bottom:8px;display:block;"><i class="fa fa-diamond"></i> Jewellery purchase details (weight + value)</label>';

    $html .= '<div class="form-inline" style="margin-bottom:6px;">';
    $html .= '<label>Karat</label> <input type="text" name="jw_karat" class="form-control input-sm" placeholder="e.g. 22K" style="width:80px;"> ';
    $html .= '<label>Purity</label> <input type="number" step="0.000001" name="jw_purity" class="form-control input-sm" placeholder="0.916700" style="width:120px;"> ';
    $html .= '<label>Metal wt (g)</label> <input type="number" step="0.001" name="jw_metal_weight_gm" class="form-control input-sm" placeholder="grams" style="width:110px;"> ';
    $html .= '<label>Rate/g</label> <input type="number" step="0.01" name="jw_rate_per_gram" class="form-control input-sm" placeholder="AED/g" style="width:110px;"> ';
    $html .= '</div>';

    $html .= '<div class="form-inline">';
    $html .= '<label>Metal value</label> <input type="number" step="0.01" name="jw_metal_value" class="form-control input-sm" placeholder="auto" style="width:120px;" readonly> ';
    $html .= '<label>Making</label> <input type="number" step="0.01" name="jw_making_charge" class="form-control input-sm" placeholder="AED" style="width:110px;"> ';
    $html .= '<label>Stone value</label> <input type="number" step="0.01" name="jw_stone_value" class="form-control input-sm" placeholder="AED" style="width:110px;"> ';
    $html .= '</div>';
    $html .= '<script>document.querySelectorAll("[name=jw_metal_weight_gm],[name=jw_rate_per_gram]").forEach(function(el){';
    $html .= 'el.addEventListener("input",function(){var w=parseFloat(document.querySelector("[name=jw_metal_weight_gm]").value)||0;';
    $html .= 'var r=parseFloat(document.querySelector("[name=jw_rate_per_gram]").value)||0;';
    $html .= 'var v=document.querySelector("[name=jw_metal_value]");if(v)v.value=(w*r).toFixed(2);});});</script>';
    $html .= '</div>';
    return $html;
}

/* ─── Jewellery columns for PO list table ─── */

function epc_jw_po_columns_header(PDO $db): string
{
    if (!epc_jw_is_jewellery_tenant($db)) return '';
    return '<th>Karat</th><th class="num">Weight (g)</th><th class="num">Rate/g</th>';
}

function epc_jw_po_columns_row(PDO $db, array $row): string
{
    if (!epc_jw_is_jewellery_tenant($db)) return '';
    $h = function($v) { return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8'); };
    return '<td>' . $h($row['jw_karat'] ?? '') . '</td>'
         . '<td class="num">' . $h(number_format((float)($row['jw_metal_weight_gm'] ?? 0), 3)) . '</td>'
         . '<td class="num">' . $h(number_format((float)($row['jw_rate_per_gram'] ?? 0), 2)) . '</td>';
}

/* ─── Sales order jewellery fields ─── */

function epc_jw_sales_order_line_fields_html(PDO $db): string
{
    if (!epc_jw_is_jewellery_tenant($db)) return '';

    $html = '<div style="background:#f0f8e8;padding:10px;border:1px solid #b8d8a0;border-radius:4px;margin:8px 0;">';
    $html .= '<label style="color:#2d6a1e;font-weight:bold;display:block;margin-bottom:6px;"><i class="fa fa-diamond"></i> Jewellery sale details (weight + value)</label>';
    $html .= '<div class="form-inline" style="margin-bottom:6px;">';
    $html .= '<label>Karat</label> <input type="text" name="jw_line_karat" class="form-control input-sm" style="width:80px;"> ';
    $html .= '<label>Weight (g)</label> <input type="number" step="0.001" name="jw_line_weight" class="form-control input-sm jw-weight" style="width:110px;"> ';
    $html .= '<label>Rate/g</label> <input type="number" step="0.01" name="jw_line_rate" class="form-control input-sm jw-rate" style="width:110px;"> ';
    $html .= '<label>Metal val</label> <input type="number" step="0.01" name="jw_line_metal_value" class="form-control input-sm" style="width:120px;" readonly> ';
    $html .= '</div>';
    $html .= '<div class="form-inline">';
    $html .= '<label>Making</label> <input type="number" step="0.01" name="jw_line_making" class="form-control input-sm" style="width:110px;"> ';
    $html .= '<label>Stone val</label> <input type="number" step="0.01" name="jw_line_stone" class="form-control input-sm" style="width:110px;"> ';
    $html .= '<label>Discount</label> <input type="number" step="0.01" name="jw_line_discount" class="form-control input-sm" style="width:110px;"> ';
    $html .= '</div>';
    $html .= '<script>document.querySelectorAll(".jw-weight,.jw-rate").forEach(function(el){';
    $html .= 'el.addEventListener("input",function(){var w=parseFloat(document.querySelector(".jw-weight").value)||0;';
    $html .= 'var r=parseFloat(document.querySelector(".jw-rate").value)||0;';
    $html .= 'var v=document.querySelector("[name=jw_line_metal_value]");if(v)v.value=(w*r).toFixed(2);});});</script>';
    $html .= '</div>';
    return $html;
}

/* ─── Dual Trial Balance (Weight + Value) ─── */

function epc_jw_weight_trial_balance(PDO $db, int $dateFrom = 0, int $dateTo = 0): array
{
    epc_jw_ensure_integration_schema($db);
    $where = '';
    $params = array();
    if ($dateFrom > 0) { $where .= ' AND transaction_date >= ?'; $params[] = $dateFrom; }
    if ($dateTo > 0)   { $where .= ' AND transaction_date <= ?'; $params[] = $dateTo; }

    $sql = "SELECT account_code, account_name, metal_type, karat,
                   SUM(weight_in) AS total_weight_in,
                   SUM(weight_out) AS total_weight_out,
                   (SUM(weight_in) - SUM(weight_out)) AS weight_balance,
                   SUM(value_debit) AS total_debit,
                   SUM(value_credit) AS total_credit,
                   (SUM(value_debit) - SUM(value_credit)) AS value_balance
            FROM epc_erp_jw_weight_ledger
            WHERE 1=1 {$where}
            GROUP BY account_code, metal_type, karat
            ORDER BY account_code, metal_type, karat";

    $st = $db->prepare($sql);
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_jw_value_trial_balance(PDO $db, int $dateFrom = 0, int $dateTo = 0): array
{
    epc_jw_ensure_integration_schema($db);
    $where = '';
    $params = array();
    if ($dateFrom > 0) { $where .= ' AND transaction_date >= ?'; $params[] = $dateFrom; }
    if ($dateTo > 0)   { $where .= ' AND transaction_date <= ?'; $params[] = $dateTo; }

    $sql = "SELECT account_code, account_name,
                   SUM(value_debit) AS total_debit,
                   SUM(value_credit) AS total_credit,
                   (SUM(value_debit) - SUM(value_credit)) AS balance
            FROM epc_erp_jw_weight_ledger
            WHERE 1=1 {$where}
            GROUP BY account_code
            ORDER BY account_code";

    $st = $db->prepare($sql);
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/* ─── Post weight ledger entry (called from purchase, sale, repair) ─── */

function epc_jw_post_weight_ledger(PDO $db, array $entry): bool
{
    epc_jw_ensure_integration_schema($db);
    $st = $db->prepare(
        'INSERT INTO epc_erp_jw_weight_ledger
         (account_code, account_name, transaction_date, source_type, source_id,
          reference, metal_type, karat, weight_in, weight_out,
          value_debit, value_credit, narration, admin_id, time_created)
         VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
    );
    return $st->execute(array(
        (string)($entry['account_code'] ?? ''),
        (string)($entry['account_name'] ?? ''),
        (int)($entry['transaction_date'] ?? time()),
        (string)($entry['source_type'] ?? 'purchase'),
        (int)($entry['source_id'] ?? 0),
        (string)($entry['reference'] ?? ''),
        (string)($entry['metal_type'] ?? ''),
        (string)($entry['karat'] ?? ''),
        (float)($entry['weight_in'] ?? 0),
        (float)($entry['weight_out'] ?? 0),
        (float)($entry['value_debit'] ?? 0),
        (float)($entry['value_credit'] ?? 0),
        (string)($entry['narration'] ?? ''),
        (int)($entry['admin_id'] ?? 0),
        time(),
    ));
}

/* ─── Repair management functions ─── */

function epc_jw_repair_list(PDO $db, string $status = '', int $limit = 100): array
{
    epc_jw_ensure_integration_schema($db);
    $where = '';
    $params = array();
    if ($status !== '') {
        $where = ' WHERE status = ?';
        $params[] = $status;
    }
    $st = $db->prepare("SELECT * FROM epc_erp_jw_repairs{$where} ORDER BY time_created DESC LIMIT " . (int)$limit);
    $st->execute($params);
    return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_jw_repair_save(PDO $db, array $data): int
{
    epc_jw_ensure_integration_schema($db);
    $repairNo = !empty($data['repair_no']) ? (string)$data['repair_no']
        : 'RPR-' . strtoupper(substr(md5((string)time()), 0, 6));

    $st = $db->prepare(
        'INSERT INTO epc_erp_jw_repairs
         (repair_no, customer_id, customer_name, customer_phone, item_description,
          metal_type, karat, gross_wt_in, net_wt_in, stone_details,
          repair_type, estimated_cost, status, received_date, promised_date, admin_id, time_created)
         VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
    );
    $st->execute(array(
        $repairNo,
        (int)($data['customer_id'] ?? 0),
        (string)($data['customer_name'] ?? ''),
        (string)($data['customer_phone'] ?? ''),
        (string)($data['item_description'] ?? ''),
        (string)($data['metal_type'] ?? ''),
        (string)($data['karat'] ?? ''),
        (float)($data['gross_wt_in'] ?? 0),
        (float)($data['net_wt_in'] ?? 0),
        (string)($data['stone_details'] ?? ''),
        (string)($data['repair_type'] ?? ''),
        (float)($data['estimated_cost'] ?? 0),
        'received',
        (int)($data['received_date'] ?? time()),
        (int)($data['promised_date'] ?? 0),
        (int)($data['admin_id'] ?? 0),
        time(),
    ));
    return (int)$db->lastInsertId();
}

/* ─── Dashboard KPIs for jewellery ─── */

function epc_jw_dashboard_kpis(PDO $db): array
{
    epc_jw_ensure_integration_schema($db);
    $q1 = function(string $sql) use ($db): float {
        try { $v = $db->query($sql)->fetchColumn(); return $v === false ? 0.0 : (float)$v; }
        catch (Throwable $e) { return 0.0; }
    };

    // Stock weight by metal type
    $stockWeight = array();
    try {
        $rows = $db->query(
            "SELECT i.jw_metal_type AS metal, i.jw_karat AS karat,
                    SUM(s.jw_weight_on_hand) AS total_wt,
                    SUM(s.qty_on_hand * s.avg_unit_cost) AS total_val
             FROM epc_erp_inv_stock s
             JOIN epc_erp_inv_items i ON i.id = s.item_id
             WHERE i.jw_metal_type <> ''
             GROUP BY i.jw_metal_type, i.jw_karat
             ORDER BY total_val DESC"
        )->fetchAll(PDO::FETCH_ASSOC) ?: array();
        $stockWeight = $rows;
    } catch (Throwable $e) {}

    // Total stock weight
    $totalStockWt = $q1("SELECT COALESCE(SUM(jw_weight_on_hand),0) FROM epc_erp_inv_stock WHERE jw_weight_on_hand > 0");
    $totalStockVal = $q1("SELECT COALESCE(SUM(qty_on_hand * avg_unit_cost),0) FROM epc_erp_inv_stock");

    // Purchase weight (period)
    $purchaseWt = $q1("SELECT COALESCE(SUM(jw_metal_weight_gm),0) FROM epc_erp_purchase_orders WHERE jw_metal_weight_gm > 0");
    $purchaseVal = $q1("SELECT COALESCE(SUM(total_amount),0) FROM epc_erp_purchase_orders");

    // Sales weight (period)
    $salesWt = $q1("SELECT COALESCE(SUM(jw_weight_gm),0) FROM epc_erp_sales_order_lines WHERE jw_weight_gm > 0");
    $salesVal = $q1("SELECT COALESCE(SUM(line_total),0) FROM epc_erp_sales_order_lines");

    // Repairs
    $repairsOpen = (int)$q1("SELECT COUNT(*) FROM epc_erp_jw_repairs WHERE status IN ('received','in_progress')");
    $repairsReady = (int)$q1("SELECT COUNT(*) FROM epc_erp_jw_repairs WHERE status = 'ready'");

    return array(
        'stock_weight' => $stockWeight,
        'total_stock_wt' => $totalStockWt,
        'total_stock_val' => $totalStockVal,
        'purchase_wt' => $purchaseWt,
        'purchase_val' => $purchaseVal,
        'sales_wt' => $salesWt,
        'sales_val' => $salesVal,
        'repairs_open' => $repairsOpen,
        'repairs_ready' => $repairsReady,
    );
}

/* ─── Comprehensive sample data seeder ─── */

function epc_jw_seed_sample_data(PDO $db, int $adminId = 0): array
{
    epc_jw_ensure_integration_schema($db);
    epc_erp_inventory_ensure_schema($db);
    require_once __DIR__ . '/epc_erp_vouchers.php';
    epc_erp_vouchers_ensure_schema($db);

    // Set industry profile to 'jewellery' so dashboard KPIs and conditional fields activate
    require_once __DIR__ . '/epc_erp_advanced.php';
    epc_erp_adv_set_setting($db, 'erp_industry_profile', 'jewellery');

    $seeded = array('warehouses' => 0, 'items' => 0, 'suppliers' => 0, 'customers' => 0,
                    'purchases' => 0, 'sales' => 0, 'repairs' => 0, 'gl_entries' => 0,
                    'errors' => array());

    // 1. Warehouses
    $warehouses = array(
        array('WH-SHOWROOM', 'Main Showroom'),
        array('WH-VAULT', 'Vault / Safe Storage'),
        array('WH-WORKSHOP', 'Workshop / Repair'),
    );
    foreach ($warehouses as $wh) {
        try {
            $db->prepare('INSERT IGNORE INTO epc_erp_inv_warehouses (code, name, time_created) VALUES (?,?,?)')
               ->execute(array($wh[0], $wh[1], time()));
            $seeded['warehouses']++;
        } catch (Throwable $e) {
            $seeded['errors'][] = 'warehouse: ' . $e->getMessage();
        }
    }

    // Get warehouse IDs
    $whShowroom = (int)$db->query("SELECT id FROM epc_erp_inv_warehouses WHERE code='WH-SHOWROOM' LIMIT 1")->fetchColumn() ?: 1;

    // 2. Inventory items — jewellery
    $items = array(
        array('GR-22K-001', '22K Gold Ring — Classic Band',       'Gold', '22K', 0.916700, 8.500, 7.200, 0, '',       0, 'G', 12.00),
        array('GN-22K-001', '22K Gold Necklace — Rope Chain',     'Gold', '22K', 0.916700, 45.000, 44.500, 0, '',     0, 'G', 10.00),
        array('GB-22K-001', '22K Gold Bangle — Floral',           'Gold', '22K', 0.916700, 32.000, 31.200, 0, '',     0, 'G', 15.00),
        array('GE-22K-001', '22K Gold Earrings — Jhumka',         'Gold', '22K', 0.916700, 12.500, 11.800, 0, '',     0, 'G', 14.00),
        array('GR-18K-001', '18K Gold Ring — Diamond Solitaire',  'Gold', '18K', 0.750000, 5.200, 3.800, 1.200, 'Diamond', 1, 'G', 25.00),
        array('GN-18K-001', '18K Gold Pendant — Heart',           'Gold', '18K', 0.750000, 6.800, 5.500, 0.400, 'Ruby',    1, 'G', 20.00),
        array('GP-24K-001', '24K Gold Bar — 10g PAMP',            'Gold', '24K', 0.999900, 10.000, 10.000, 0, '',     0, 'G', 0.00),
        array('GP-24K-002', '24K Gold Coin — 5g',                 'Gold', '24K', 0.999900, 5.000, 5.000, 0, '',      0, 'G', 0.00),
        array('SR-925-001', 'Sterling Silver Ring — Gemstone',     'Silver', '925', 0.925000, 6.000, 5.200, 0.500, 'Topaz', 1, 'S', 3.00),
        array('SN-925-001', 'Sterling Silver Chain — Box Link',    'Silver', '925', 0.925000, 22.000, 22.000, 0, '',  0, 'S', 2.00),
        array('DN-001', 'Loose Diamond — 1.02ct Round Brilliant',  'Diamond', '', 0, 0.204, 0.204, 0, '',             0, 'D', 0.00),
        array('DN-002', 'Loose Diamond — 0.50ct Princess Cut',     'Diamond', '', 0, 0.100, 0.100, 0, '',             0, 'D', 0.00),
        array('PN-001', 'South Sea Pearl Strand — 18"',            'Pearl', '', 0, 85.000, 85.000, 0, '',             0, 'P', 0.00),
        array('GW-22K-001', '22K Gold Watch — Men\'s Dress',       'Gold', '22K', 0.916700, 65.000, 28.000, 0, '',    0, 'G', 18.00),
        array('PT-950-001', 'Platinum Band — Comfort Fit',         'Platinum', '950', 0.950000, 10.500, 10.200, 0, '', 0, 'T', 30.00),
    );
    $goldRate = 235.00;
    foreach ($items as $it) {
        list($sku, $name, $metal, $karat, $purity, $grossWt, $netWt, $stoneWt, $stoneTyp, $stonePcs, $div, $making) = $it;
        $metalValue = $netWt * $goldRate * $purity;
        if ($metal === 'Silver') $metalValue = $netWt * 3.50;
        if ($metal === 'Diamond') $metalValue = $netWt * 5 * 45000;
        if ($metal === 'Pearl') $metalValue = $grossWt * 150;
        if ($metal === 'Platinum') $metalValue = $netWt * 120 * $purity;
        $makingTotal = $grossWt * $making;
        $totalCost = $metalValue + $makingTotal;
        $salesPrice = $totalCost * 1.15;
        $unit = $metal === 'Diamond' ? 'carat' : 'gram';

        try {
            $db->prepare(
                'INSERT IGNORE INTO epc_erp_inv_items
                 (sku, name, item_type, unit, active, time_created,
                  jw_metal_type, jw_karat, jw_purity, jw_gross_wt, jw_net_wt,
                  jw_stone_wt, jw_stone_type, jw_stone_pcs, jw_division,
                  jw_making_charge_per_gm, standard_cost, sales_price, purchase_price)
                 VALUES (?,?,?,?,1,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
            )->execute(array(
                $sku, $name, 'serialized', $unit, time(),
                $metal, $karat, $purity, $grossWt, $netWt,
                $stoneWt, $stoneTyp, $stonePcs, $div,
                $making, round($totalCost, 2), round($salesPrice, 2), round($totalCost, 2),
            ));
            $seeded['items']++;
            $itemId = (int)$db->lastInsertId();
            if ($itemId > 0) {
                $db->prepare(
                    'INSERT IGNORE INTO epc_erp_inv_stock
                     (warehouse_id, item_id, qty_on_hand, avg_unit_cost, jw_weight_on_hand, jw_karat, time_updated)
                     VALUES (?,?,?,?,?,?,?)'
                )->execute(array($whShowroom, $itemId, 1, round($totalCost, 4), $grossWt, $karat, time()));
            }
        } catch (Throwable $e) {
            $seeded['errors'][] = 'item ' . $sku . ': ' . $e->getMessage();
        }
    }

    // 3. Suppliers (using contacts table)
    $suppliers = array(
        array('Dubai Gold Souk Trading LLC', 'supplier', '+971-4-226-1234', 'goldsouk@example.com', 'AE'),
        array('Rajesh Gems & Jewellery Pvt Ltd', 'supplier', '+91-22-2389-5678', 'rajesh@example.com', 'IN'),
        array('Antwerp Diamond Exchange BVBA', 'supplier', '+32-3-233-9012', 'antwerp@example.com', 'BE'),
        array('PAMP SA — Swiss Bullion', 'supplier', '+41-91-695-3456', 'pamp@example.com', 'CH'),
        array('Mikimoto Pearl Co. Ltd', 'supplier', '+81-3-3535-7890', 'mikimoto@example.com', 'JP'),
    );
    foreach ($suppliers as $s) {
        try {
            $db->prepare(
                'INSERT IGNORE INTO epc_erp_contacts
                 (name, contact_type, phone, email, country, notes, time_created)
                 VALUES (?,?,?,?,?,?,?)'
            )->execute(array($s[0], $s[1], $s[2], $s[3], $s[4], 'Sample jewellery supplier', time()));
            $seeded['suppliers']++;
        } catch (Throwable $e) {
            $seeded['errors'][] = 'supplier ' . $s[0] . ': ' . $e->getMessage();
        }
    }

    // 4. Customers (using ERP contacts table)
    $customers = array(
        array('Mrs. Fatima Al Maktoum', 'customer', '+971-50-123-4567', 'fatima@example.com', 'AE'),
        array('Mr. Rajiv Sharma', 'customer', '+971-55-234-5678', 'rajiv@example.com', 'IN'),
        array('Ms. Sarah Chen', 'customer', '+971-52-345-6789', 'sarah@example.com', 'CN'),
        array('Mr. James Wilson', 'customer', '+971-56-456-7890', 'james@example.com', 'GB'),
        array('Mrs. Aisha Bin Khalifa', 'customer', '+971-50-567-8901', 'aisha@example.com', 'AE'),
    );
    $customerContactIds = array();
    foreach ($customers as $ci => $c) {
        try {
            $db->prepare(
                'INSERT IGNORE INTO epc_erp_contacts
                 (name, contact_type, phone, email, country, notes, time_created)
                 VALUES (?,?,?,?,?,?,?)'
            )->execute(array($c[0], $c[1], $c[2], $c[3], $c[4], 'Sample jewellery customer', time()));
            $cid = (int)$db->lastInsertId();
            if ($cid === 0) {
                $chk = $db->prepare('SELECT id FROM epc_erp_contacts WHERE email = ? AND contact_type = ? LIMIT 1');
                $chk->execute(array($c[3], 'customer'));
                $cid = (int)$chk->fetchColumn();
            }
            $customerContactIds[$ci] = $cid;
            $seeded['customers']++;
        } catch (Throwable $e) {
            $seeded['errors'][] = 'customer ' . $c[0] . ': ' . $e->getMessage();
        }
    }

    // 5. Purchase orders with jewellery weight fields
    $poData = array(
        array('PO-JW-001', 'Dubai Gold Souk Trading LLC', '22K Gold — 500g Bar', 500.000, 215.47, '22K', 0.916700, 5500.00, 0),
        array('PO-JW-002', 'Rajesh Gems & Jewellery Pvt Ltd', 'Finished 22K Bangles — 10 pcs', 320.000, 215.47, '22K', 0.916700, 4800.00, 2500.00),
        array('PO-JW-003', 'Antwerp Diamond Exchange BVBA', 'Loose Diamonds — 5ct total', 1.000, 0, '', 0, 0, 225000.00),
        array('PO-JW-004', 'PAMP SA — Swiss Bullion', '24K Gold Bars — 100g', 100.000, 235.00, '24K', 0.999900, 0, 0),
        array('PO-JW-005', 'Mikimoto Pearl Co. Ltd', 'South Sea Pearls — 20 strands', 0, 0, '', 0, 0, 180000.00),
    );
    foreach ($poData as $po) {
        $metalValue = $po[3] * $po[4];
        $total = $metalValue + $po[7] + $po[8];
        try {
            $db->prepare(
                'INSERT IGNORE INTO epc_erp_purchase_orders
                 (po_no, title, amount_ex_vat, total_amount, status,
                  jw_metal_weight_gm, jw_rate_per_gram, jw_metal_value, jw_making_charge, jw_stone_value,
                  jw_karat, jw_purity, time_created, time_updated)
                 VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
            )->execute(array(
                $po[0], $po[2], round($total, 2), round($total * 1.05, 2), 'received',
                $po[3], $po[4], round($metalValue, 2), $po[7], $po[8],
                $po[5], $po[6], time() - rand(86400, 604800), time(),
            ));
            $seeded['purchases']++;
            $poId = (int)$db->lastInsertId();
            if ($po[3] > 0) {
                epc_jw_post_weight_ledger($db, array(
                    'account_code' => '1300', 'account_name' => 'Metal Inventory',
                    'source_type' => 'purchase', 'source_id' => $poId,
                    'reference' => $po[0], 'metal_type' => ($po[5] !== '' ? 'Gold' : ''),
                    'karat' => $po[5], 'weight_in' => $po[3], 'weight_out' => 0,
                    'value_debit' => round($total * 1.05, 2), 'value_credit' => 0,
                    'narration' => 'Purchase: ' . $po[2],
                ));
            }
        } catch (Throwable $e) {
            $seeded['errors'][] = 'PO ' . $po[0] . ': ' . $e->getMessage();
        }
    }

    // 6. Sales orders — create HEADER first, then line with correct FK
    $salesData = array(
        array('SO-JW-001', 0, '22K Gold Necklace — Rope Chain', 44.500, 235.00, '22K', 450.00, 0, 500.00),
        array('SO-JW-002', 1, '22K Gold Ring — Classic Band + Diamond', 7.200, 235.00, '22K', 96.00, 15000.00, 0),
        array('SO-JW-003', 2, '18K Gold Pendant — Heart with Ruby', 5.500, 200.00, '18K', 110.00, 8000.00, 0),
        array('SO-JW-004', 3, '24K Gold Bar — 10g PAMP', 10.000, 235.00, '24K', 0, 0, 0),
        array('SO-JW-005', 4, '22K Gold Bangles Set (4 pcs)', 128.000, 235.00, '22K', 1920.00, 0, 1000.00),
    );
    foreach ($salesData as $so) {
        $custIdx = (int)$so[1];
        $contactId = isset($customerContactIds[$custIdx]) ? $customerContactIds[$custIdx] : 0;
        $metalVal = $so[3] * $so[4];
        $lineTotal = $metalVal + $so[6] + $so[7] - $so[8];
        try {
            // Generate SO number using ERP voucher sequence
            $soNo = epc_erp_next_voucher_no($db, 'SO');

            // Create the sales order HEADER in epc_erp_sales_orders
            $now = time();
            $db->prepare(
                'INSERT INTO epc_erp_sales_orders
                 (so_no, contact_id, title, amount_ex_vat, vat_amount, total_amount,
                  status, admin_id, time_created, time_updated)
                 VALUES (?,?,?,?,?,?,?,?,?,?)'
            )->execute(array(
                $soNo, $contactId, $so[2],
                round($lineTotal, 2), round($lineTotal * 0.05, 2), round($lineTotal * 1.05, 2),
                'confirmed', $adminId, $now - rand(86400, 604800), $now,
            ));
            $soId = (int)$db->lastInsertId();

            // Create the sales order LINE linked to the header
            $db->prepare(
                'INSERT INTO epc_erp_sales_order_lines
                 (sales_order_id, line_no, description, qty, unit_price_ex_vat, line_ex_vat,
                  jw_weight_gm, jw_rate_per_gram, jw_metal_value, jw_making_charge,
                  jw_stone_value, jw_karat, jw_discount)
                 VALUES (?,1,?,1,?,?,?,?,?,?,?,?,?)'
            )->execute(array(
                $soId, $so[2], round($lineTotal, 4), round($lineTotal, 2),
                $so[3], $so[4], round($metalVal, 2), $so[6],
                $so[7], $so[5], $so[8],
            ));
            $seeded['sales']++;

            // Post to weight ledger (sale = weight out)
            if ($so[3] > 0) {
                epc_jw_post_weight_ledger($db, array(
                    'account_code' => '4100', 'account_name' => 'Sales Revenue',
                    'source_type' => 'sale', 'source_id' => $soId,
                    'reference' => $soNo, 'metal_type' => 'Gold', 'karat' => $so[5],
                    'weight_in' => 0, 'weight_out' => $so[3],
                    'value_debit' => 0, 'value_credit' => round($lineTotal, 2),
                    'narration' => 'Sale: ' . $so[2],
                ));
                epc_jw_post_weight_ledger($db, array(
                    'account_code' => '1300', 'account_name' => 'Metal Inventory',
                    'source_type' => 'sale', 'source_id' => $soId,
                    'reference' => $soNo, 'metal_type' => 'Gold', 'karat' => $so[5],
                    'weight_in' => 0, 'weight_out' => $so[3],
                    'value_debit' => 0, 'value_credit' => round($metalVal, 2),
                    'narration' => 'COGS: ' . $so[2],
                ));
            }
        } catch (Throwable $e) {
            $seeded['errors'][] = 'SO ' . $so[0] . ': ' . $e->getMessage();
        }
    }

    // 7. Repair jobs — use unique repair_no per record to avoid UNIQUE constraint collision
    $repairs = array(
        array('Mrs. Fatima Al Maktoum', '+971-50-123-4567', '22K Gold Ring — resize', 'Gold', '22K', 8.5, 7.2, 'Ring Resize', 150.00),
        array('Mr. Rajiv Sharma', '+971-55-234-5678', '18K Chain — broken clasp repair', 'Gold', '18K', 22.0, 21.5, 'Clasp Repair', 250.00),
        array('Ms. Sarah Chen', '+971-52-345-6789', 'Diamond re-setting on platinum ring', 'Platinum', '950', 10.5, 10.2, 'Stone Setting', 450.00),
    );
    foreach ($repairs as $ri => $rp) {
        try {
            $repairNo = 'RPR-' . strtoupper(substr(md5('seed_' . $ri . '_' . $rp[0]), 0, 6));
            epc_jw_repair_save($db, array(
                'repair_no' => $repairNo,
                'customer_name' => $rp[0], 'customer_phone' => $rp[1],
                'item_description' => $rp[2], 'metal_type' => $rp[3],
                'karat' => $rp[4], 'gross_wt_in' => $rp[5], 'net_wt_in' => $rp[6],
                'repair_type' => $rp[7], 'estimated_cost' => $rp[8],
                'received_date' => time() - ($ri + 1) * 86400,
                'promised_date' => time() + ($ri + 1) * 86400 * 2,
                'admin_id' => $adminId,
            ));
            $seeded['repairs']++;
        } catch (Throwable $e) {
            $seeded['errors'][] = 'repair #' . ($ri + 1) . ' (' . $rp[0] . '): ' . $e->getMessage();
        }
    }

    // 8. GL entries for dual trial balance
    $glEntries = array(
        array('1100', 'Cash & Bank', 'opening', 0, '', '', 0, 0, 500000.00, 0, 'Opening cash balance'),
        array('1300', 'Metal Inventory — Gold', 'opening', 0, 'Gold', '22K', 800.000, 0, 172376.00, 0, 'Opening gold stock 800g 22K'),
        array('1310', 'Metal Inventory — Silver', 'opening', 0, 'Silver', '925', 5000.000, 0, 17500.00, 0, 'Opening silver stock 5kg'),
        array('1320', 'Diamond Inventory', 'opening', 0, 'Diamond', '', 25.000, 0, 450000.00, 0, 'Opening diamond inventory 25ct'),
        array('1400', 'Stone Inventory', 'opening', 0, '', '', 0, 0, 85000.00, 0, 'Opening stone/pearl inventory'),
        array('2100', 'Accounts Payable', 'opening', 0, '', '', 0, 0, 0, 125000.00, 'Opening AP balance'),
        array('3100', 'Capital', 'opening', 0, '', '', 0, 0, 0, 1099876.00, 'Owner equity'),
    );
    foreach ($glEntries as $gl) {
        try {
            epc_jw_post_weight_ledger($db, array(
                'account_code' => $gl[0], 'account_name' => $gl[1],
                'source_type' => $gl[2], 'source_id' => $gl[3],
                'metal_type' => $gl[4], 'karat' => $gl[5],
                'weight_in' => $gl[6], 'weight_out' => $gl[7],
                'value_debit' => $gl[8], 'value_credit' => $gl[9],
                'narration' => $gl[10],
            ));
            $seeded['gl_entries']++;
        } catch (Throwable $e) {
            $seeded['errors'][] = 'GL ' . $gl[0] . ': ' . $e->getMessage();
        }
    }

    return $seeded;
}


