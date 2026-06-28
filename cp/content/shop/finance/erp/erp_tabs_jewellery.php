<?php
/**
 * Jewellery ERP — main navigation hub.
 * Renders the Jewellery Business Management dashboard with links to all sub-modules.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_company_context.php';

epc_jewel_ensure_schema($db_link);
$companyId = function_exists('epc_erp_active_company_id') ? epc_erp_active_company_id($db_link) : 0;

erp_page_header(
    '<i class="fa fa-diamond"></i> Jewellery ERP',
    'Complete jewellery business management — master data, purchase, sales, repair, stock and analysis.',
    array(
        array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
        array('label' => 'Jewellery'),
    )
);

$stockBalance = epc_jewel_metal_stock_balance($db_link, $companyId);
$totalPcs = 0; $totalGms = 0; $totalVal = 0;
foreach ($stockBalance as $b) {
    $totalPcs += (int) $b['total_pcs'];
    $totalGms += (float) $b['total_gms'];
    $totalVal += (float) $b['total_value'];
}
$pendingRepairs = count(epc_jewel_repair_pending_jobs($db_link, $companyId));
$karats = epc_jewel_karat_list($db_link, $companyId);

erp_stat_cards(array(
    array('label' => 'Stock items (pcs)', 'value' => number_format($totalPcs)),
    array('label' => 'Stock weight (g)', 'value' => number_format($totalGms, 2)),
    array('label' => 'Stock value', 'value' => epc_erp_money($totalVal, 0)),
    array('label' => 'Pending repairs', 'value' => (string) $pendingRepairs),
    array('label' => 'Karat codes', 'value' => (string) count($karats)),
    array('label' => 'Divisions', 'value' => (string) count(epc_jewel_divisions())),
));

$tabBase = epc_erp_tab_url($erpUrl, 'jewellery', $date_from_str, $date_to_str);
$sep = strpos($tabBase, '?') === false ? '?' : '&';

$modules = array(
    'Master data' => array(
        'jw_metal_stock' => array('icon' => 'fa-cubes', 'label' => 'Metal stock master', 'desc' => 'Gold, silver, platinum items'),
        'jw_design' => array('icon' => 'fa-paint-brush', 'label' => 'Design master', 'desc' => 'Jewellery designs with metal & stone details'),
        'jw_diamond' => array('icon' => 'fa-diamond', 'label' => 'Diamond master', 'desc' => 'Diamond items with certificates'),
        'jw_pearl' => array('icon' => 'fa-circle-o', 'label' => 'Pearl master', 'desc' => 'Pearl items — natural & cultured'),
        'jw_color_stone' => array('icon' => 'fa-gem', 'label' => 'Color stone master', 'desc' => 'Sapphire, ruby, emerald etc.'),
        'jw_karat' => array('icon' => 'fa-tachometer', 'label' => 'Karat master', 'desc' => 'Karat codes & standard purity'),
        'jw_rate_type' => array('icon' => 'fa-line-chart', 'label' => 'Rate type master', 'desc' => 'GMS, GOZ, KB, TTB rate types'),
        'jw_currency' => array('icon' => 'fa-money', 'label' => 'Currency master', 'desc' => 'Multi-currency with conversion rates'),
    ),
    'Purchase' => array(
        'jw_metal_purchase' => array('icon' => 'fa-shopping-cart', 'label' => 'Metal purchase', 'desc' => 'RMP — metal purchase with fixed/floating'),
        'jw_diamond_purchase' => array('icon' => 'fa-cart-plus', 'label' => 'Diamond purchase', 'desc' => 'RDP — diamond & stone purchase'),
        'jw_purchase_fixing' => array('icon' => 'fa-lock', 'label' => 'Purchase fixing', 'desc' => 'Fix metal rate for purchase'),
        'jw_purchase_window' => array('icon' => 'fa-window-maximize', 'label' => 'Purchase window', 'desc' => 'Purchase inquiry window'),
    ),
    'Sales' => array(
        'jw_retail_sales' => array('icon' => 'fa-shopping-bag', 'label' => 'Retail sales (POS)', 'desc' => 'RIN — retail invoice with receipts'),
        'jw_metal_sales' => array('icon' => 'fa-exchange', 'label' => 'Metal sales', 'desc' => 'Bulk metal sales'),
        'jw_sales_fixing' => array('icon' => 'fa-gavel', 'label' => 'Sales fixing', 'desc' => 'Fix metal rate for sales'),
        'jw_sales_return' => array('icon' => 'fa-undo', 'label' => 'Sales return', 'desc' => 'Process returns & refunds'),
        'jw_pos_advance' => array('icon' => 'fa-credit-card-alt', 'label' => 'POS advance', 'desc' => 'Advance payments from customers'),
    ),
    'Repair & workshop' => array(
        'jw_repair_receipt' => array('icon' => 'fa-wrench', 'label' => 'Repair receipt', 'desc' => 'REP — receive items for repair'),
        'jw_repair_transfer' => array('icon' => 'fa-truck', 'label' => 'Transfer repair jobs', 'desc' => 'RET — send jobs to workshop'),
        'jw_workshop_receive' => array('icon' => 'fa-inbox', 'label' => 'Receive from workshop', 'desc' => 'RRC — receive repaired items'),
        'jw_repair_delivery' => array('icon' => 'fa-gift', 'label' => 'Customer delivery', 'desc' => 'RTD — deliver repaired items'),
        'jw_repair_sale' => array('icon' => 'fa-money', 'label' => 'Repair sale', 'desc' => 'Invoice repair charges'),
        'jw_repair_register' => array('icon' => 'fa-list-alt', 'label' => 'Repair register', 'desc' => 'Report of all repair jobs'),
        'jw_repair_search' => array('icon' => 'fa-search', 'label' => 'Repair item search', 'desc' => 'Search repair items by status'),
    ),
    'Stock & analysis' => array(
        'jw_stock_verification' => array('icon' => 'fa-check-square', 'label' => 'Stock verification', 'desc' => 'MSV — physical vs computer stock'),
        'jw_stock_balance' => array('icon' => 'fa-balance-scale', 'label' => 'Metal stock balance', 'desc' => 'Current stock by metal & karat'),
        'jw_sales_analysis' => array('icon' => 'fa-bar-chart', 'label' => 'Metal sales analysis', 'desc' => 'Sales trends by date, salesman, division'),
        'jw_barcode' => array('icon' => 'fa-barcode', 'label' => 'Barcode generation', 'desc' => 'Generate & print item barcodes'),
    ),
    'Finance' => array(
        'jw_petty_cash' => array('icon' => 'fa-money', 'label' => 'Petty cash', 'desc' => 'PCV — petty cash vouchers'),
        'jw_journal_voucher' => array('icon' => 'fa-book', 'label' => 'Journal voucher', 'desc' => 'JVL — journal entries'),
        'jw_tourist_vat' => array('icon' => 'fa-plane', 'label' => 'Tourist VAT refund', 'desc' => 'VRV — tourist refund verification'),
    ),
);
?>
<div class="row">
<?php foreach ($modules as $group => $items): ?>
    <div class="col-md-12">
        <h4 style="margin:18px 0 10px;color:#2c3e50;font-weight:600;border-bottom:2px solid #3498db;padding-bottom:6px;">
            <?php echo htmlspecialchars($group, ENT_QUOTES, 'UTF-8'); ?>
        </h4>
    </div>
    <?php foreach ($items as $tab => $info): ?>
    <div class="col-md-3 col-sm-4 col-xs-6" style="margin-bottom:12px;">
        <a href="<?php echo htmlspecialchars(epc_erp_tab_url($erpUrl, $tab, $date_from_str, $date_to_str), ENT_QUOTES, 'UTF-8'); ?>"
           style="text-decoration:none;display:block;padding:14px;border:1px solid #ddd;border-radius:6px;background:#fff;min-height:90px;transition:box-shadow .2s;"
           onmouseover="this.style.boxShadow='0 2px 8px rgba(0,0,0,.12)'" onmouseout="this.style.boxShadow='none'">
            <div style="font-size:22px;color:#3498db;margin-bottom:6px;"><i class="fa <?php echo $info['icon']; ?>"></i></div>
            <div style="font-weight:600;color:#2c3e50;font-size:13px;"><?php echo htmlspecialchars($info['label'], ENT_QUOTES, 'UTF-8'); ?></div>
            <div style="font-size:11px;color:#7f8c8d;margin-top:3px;"><?php echo htmlspecialchars($info['desc'], ENT_QUOTES, 'UTF-8'); ?></div>
        </a>
    </div>
    <?php endforeach; ?>
<?php endforeach; ?>
</div>
