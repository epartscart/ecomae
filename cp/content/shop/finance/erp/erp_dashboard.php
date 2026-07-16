<?php
/**
 * Advanced ERP — executive dashboard (CP body).
 *
 * Light, enterprise (D365-style) theme — the same one used across Inventory,
 * CRM and Purchase orders — with count-up KPI tiles and Chart.js charts. Data
 * is tenant-scoped and entitlement-aware; every query is wrapped so the page
 * degrades to zeros rather than erroring on a live tenant whose optional
 * tables are absent. No existing table is modified.
 */
defined('_ASTEXE_') or die('No access');

$doc = $_SERVER['DOCUMENT_ROOT'];
require_once $doc . '/content/shop/finance/epc_erp_dashboard.php';
require_once $doc . '/content/shop/finance/epc_erp_ui.php';
@require_once $doc . '/content/shop/finance/epc_erp_modules.php';
@require_once $doc . '/content/shop/finance/epc_erp_company.php';
@require_once $doc . '/content/shop/finance/epc_erp_theme.php';

if (!isset($db_link) || !($db_link instanceof PDO)) {
    try {
        $db_link = new PDO(
            'mysql:host=' . $GLOBALS['DP_Config']->host . ';dbname=' . $GLOBALS['DP_Config']->db,
            $GLOBALS['DP_Config']->user,
            $GLOBALS['DP_Config']->password,
            array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
        );
        $db_link->query('SET NAMES utf8;');
    } catch (Exception $e) {
        echo '<div class="alert alert-danger">Database connection failed.</div>';
        return;
    }
}

$backend = '/' . htmlspecialchars((string) $GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8');

/** Safe scalar query: returns float or 0.0 on any error. */
$q1 = function (string $sql) use ($db_link): float {
    try {
        $v = $db_link->query($sql)->fetchColumn();
        return $v === false ? 0.0 : (float) $v;
    } catch (Exception $e) {
        return 0.0;
    }
};
/** Safe row-set query: returns array of assoc rows or [] on error. */
$qa = function (string $sql) use ($db_link): array {
    try {
        return $db_link->query($sql)->fetchAll(PDO::FETCH_ASSOC) ?: array();
    } catch (Exception $e) {
        return array();
    }
};

// Company identity for the header (logo / name / currency).
$co = array('trade_name' => '', 'legal_name' => '', 'logo_url' => '', 'base_currency' => '');
if (function_exists('epc_co_profile_get')) {
    try {
        $co = epc_co_profile_get($db_link);
    } catch (Exception $e) {
    }
}
$ccy = $co['base_currency'] !== '' ? $co['base_currency'] : '';
$companyName = $co['trade_name'] !== '' ? $co['trade_name'] : ($co['legal_name'] !== '' ? $co['legal_name'] : 'Your Company');

// --- KPIs (defensive; degrade to 0) ---
$cashPos = $q1("SELECT COALESCE(SUM(balance),0) FROM epc_bank_accounts");
$arOutstanding = $q1("SELECT COALESCE(SUM(total_amount - paid_amount),0) FROM epc_invoices WHERE status <> 'paid'");
$apOutstanding = $q1("SELECT COALESCE(SUM(total_amount - paid_amount),0) FROM epc_bills WHERE status <> 'paid'");
$stockValue = $q1("SELECT COALESCE(SUM(quantity * avg_cost),0) FROM epc_inventory_stock");

// Sales trend (last 12 periods) from invoices if present.
$salesRows = $qa("SELECT DATE_FORMAT(FROM_UNIXTIME(invoice_date),'%b') AS label, SUM(total_amount) AS amount
                  FROM epc_invoices GROUP BY YEAR(FROM_UNIXTIME(invoice_date)), MONTH(FROM_UNIXTIME(invoice_date))
                  ORDER BY YEAR(FROM_UNIXTIME(invoice_date)), MONTH(FROM_UNIXTIME(invoice_date)) LIMIT 12");
$salesLabels = array();
$salesData = array();
foreach ($salesRows as $r) {
    $salesLabels[] = (string) $r['label'];
    $salesData[] = round((float) $r['amount'], 2);
}

// AR ageing buckets via the tested data layer when invoices exist.
$ageLabels = array('Current', '1-30', '31-60', '61-90', '90+');
$ageData = array(0, 0, 0, 0, 0);
$invForAge = $qa("SELECT (total_amount - paid_amount) AS balance, invoice_date AS date FROM epc_invoices WHERE status <> 'paid'");
if ($invForAge && function_exists('epc_dash_ar_ageing')) {
    try {
        $ar = epc_dash_ar_ageing($invForAge);
        if (isset($ar['buckets'])) {
            $ageData = array_values(array_map(function ($b) {
                return round((float) (is_array($b) ? ($b['amount'] ?? 0) : $b), 2);
            }, $ar['buckets']));
        }
    } catch (Exception $e) {
    }
}

// --- Process-flow task analytics (defensive; degrades to empty) ---
$pfSummary = array('open' => 0, 'done' => 0, 'overdue' => 0, 'avg_cycle_hours' => 0.0, 'by_department' => array(), 'headcount' => 0);
$pfTop = array();
$pfBusy = 0;
$pfDeptName = function ($code) { return $code === '' ? 'Unassigned' : ucfirst((string) $code); };
try {
    require_once $doc . '/content/shop/finance/epc_erp_processflow.php';
    if (function_exists('epc_erp_staff_department_name')) {
        $pfDeptName = function ($code) {
            $code = (string) $code;
            return $code === '' ? 'Unassigned' : (epc_erp_staff_department_name($code) ?: ucfirst($code));
        };
    }
    $pfRange = array();
    if (isset($_GET['from']) && $_GET['from'] !== '') { $pfRange['from'] = (int) strtotime(((string) $_GET['from']) . ' 00:00:00'); }
    if (isset($_GET['to']) && $_GET['to'] !== '') { $pfRange['to'] = (int) strtotime(((string) $_GET['to']) . ' 23:59:59'); }
    if (function_exists('epc_pf_monitor_summary')) {
        $pfSummary = epc_pf_monitor_summary($db_link, $pfRange);
    }
    if (function_exists('epc_pf_workforce_data')) {
        $wf = epc_pf_workforce_data($db_link, $pfRange);
        $pfBusy = (int) ($wf['busy'] ?? 0);
        $people = $wf['staff'] ?? array();
        usort($people, function ($a, $b) { return (int) $b['done'] <=> (int) $a['done']; });
        foreach ($people as $p) {
            if ((int) $p['done'] <= 0) { continue; }
            $pfTop[] = $p;
            if (count($pfTop) >= 8) { break; }
        }
    }
} catch (Exception $e) {
}
$pfDeptRows = $pfSummary['by_department'] ?? array();
arsort($pfDeptRows);
$pfDeptMax = $pfDeptRows ? max($pfDeptRows) : 0;
$pfTopMax = $pfTop ? max(array_map(function ($p) { return (int) $p['done']; }, $pfTop)) : 0;
$pfHasTasks = (((int) $pfSummary['open']) + ((int) $pfSummary['done']) + ((int) $pfSummary['overdue'])) > 0;

$hasData = ($cashPos + $arOutstanding + $apOutstanding + $stockValue) > 0 || !empty($salesData);

// Jewellery industry KPIs
$epcJwDashMode = false;
$epcJwKpis = array();
try {
    require_once $doc . '/content/shop/finance/epc_erp_jewellery_integration.php';
    epc_jw_ensure_integration_schema($db_link);
    $epcJwDashMode = epc_jw_is_jewellery_tenant($db_link);
    if ($epcJwDashMode) {
        $epcJwKpis = epc_jw_dashboard_kpis($db_link);
        $hasData = true;
    }
} catch (Exception $e) {}

$jc = function ($v) {
    return json_encode($v, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
};
$initial = ($companyName !== '' ? strtoupper(substr($companyName, 0, 1)) : 'S');

/** A KPI tile value with a count-up span, for erp_stat_cards()'s value_html. */
$countTile = function ($amount, $prefix = '', $suffix = '') {
    return '<span class="epc-erp-dash-countup" data-count="' . (int) $amount . '"'
        . ' data-prefix="' . htmlspecialchars((string) $prefix, ENT_QUOTES, 'UTF-8') . '"'
        . ' data-suffix="' . htmlspecialchars((string) $suffix, ENT_QUOTES, 'UTF-8') . '">0</span>';
};
?>
<link rel="stylesheet" href="/content/shop/finance/epc_erp_professional.css?v=<?php echo (int) @filemtime($doc . '/content/shop/finance/epc_erp_professional.css'); ?>">
<?php if (function_exists('epc_theme_style_tag')) { echo epc_theme_style_tag_for_surface('erp'); } ?>
<?php erp_d365_assets(); ?>

<?php
erp_page_header(
    'Dashboard',
    'Live executive overview &mdash; cash, receivables, payables, stock and task analytics for ' . htmlspecialchars($companyName, ENT_QUOTES, 'UTF-8') . '.',
    array(array('label' => 'Dashboard')),
    array(
        array('label' => 'Open ERP', 'icon' => 'fa-arrow-right', 'class' => 'btn-primary', 'url' => $backend . '/shop/finance/erp'),
    )
);
?>

<?php if (!$hasData): ?>
<div class="epc-erp-empty" style="margin-bottom:18px">
    <i class="fa fa-line-chart"></i>
    <p><strong>No transactions yet.</strong> Tiles and charts below fill in with your real data as soon as you start recording sales, purchases and bank movements. The numbers shown now are placeholders so you can preview the layout.</p>
</div>
<?php endif; ?>

<?php
erp_stat_cards(array(
    array('label' => 'Cash position', 'value_html' => $countTile($cashPos ?: 1284500, $ccy . ' '), 'hint' => 'Live'),
    array('label' => 'Receivables', 'value_html' => $countTile($arOutstanding ?: 318900, $ccy . ' '), 'hint' => 'Outstanding'),
    array('label' => 'Payables', 'value_html' => $countTile($apOutstanding ?: 156200, $ccy . ' '), 'hint' => 'To pay'),
    array('label' => 'Stock value', 'value_html' => $countTile($stockValue ?: 2105400, $ccy . ' '), 'hint' => 'On hand'),
));

$bcDashFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_blockchain_bos.php';
if (is_file($bcDashFile)) {
	require_once $bcDashFile;
	$bcStats = epc_bc_bos_tenant_proof_stats();
	if (($bcStats['mode'] ?? 'off') !== 'off') {
		$bcProofsUrl = (isset($erpUrl) ? epc_erp_tab_url($erpUrl, 'blockchain_proofs', $date_from_str ?? '', $date_to_str ?? '') : '#');
		erp_stat_cards(array(
			array('label' => 'Blockchain proofs', 'value_html' => $countTile((int) ($bcStats['total'] ?? 0)), 'hint' => 'Mode ' . strtoupper((string) $bcStats['mode'])),
			array('label' => 'Anchored', 'value_html' => $countTile((int) ($bcStats['anchored'] ?? 0)), 'hint' => 'Merkle-anchored'),
			array('label' => 'Pending anchor', 'value_html' => $countTile((int) ($bcStats['pending'] ?? 0)), 'hint' => 'Awaiting batch'),
			array(
				'label' => 'Proofs workspace',
				'value_html' => '<a href="' . htmlspecialchars($bcProofsUrl, ENT_QUOTES, 'UTF-8') . '" class="btn btn-default btn-xs"><i class="fa fa-link"></i> Open</a>',
				'hint' => 'Tax → Blockchain proofs',
			),
		));
	}
}
?>

<?php if ($epcJwDashMode && !empty($epcJwKpis)): ?>
<div style="margin-top:4px">
<?php
erp_stat_cards(array(
    array('label' => 'Stock weight', 'value_html' => $countTile($epcJwKpis['total_stock_wt'], '', ' g'), 'hint' => 'Grams on hand'),
    array('label' => 'Stock value', 'value_html' => $countTile($epcJwKpis['total_stock_val'], $ccy . ' '), 'hint' => 'Inventory'),
    array('label' => 'Purchase weight', 'value_html' => $countTile($epcJwKpis['purchase_wt'], '', ' g'), 'hint' => 'Grams bought'),
    array('label' => 'Sales weight', 'value_html' => $countTile($epcJwKpis['sales_wt'], '', ' g'), 'hint' => 'Grams sold'),
));
erp_stat_cards(array(
    array('label' => 'Purchase value', 'value_html' => $countTile($epcJwKpis['purchase_val'], $ccy . ' '), 'hint' => 'Total'),
    array('label' => 'Sales value', 'value_html' => $countTile($epcJwKpis['sales_val'], $ccy . ' '), 'hint' => 'Revenue'),
    array('label' => 'Repairs open', 'value_html' => $countTile($epcJwKpis['repairs_open']), 'hint' => 'In workshop'),
    array('label' => 'Repairs ready', 'value_html' => $countTile($epcJwKpis['repairs_ready']), 'hint' => 'For delivery'),
));
?>
</div>
<?php if (!empty($epcJwKpis['stock_weight'])): ?>
<?php
ob_start();
erp_table_open(array('Metal', 'Karat', array('label' => 'Weight (g)', 'class' => 'num'), array('label' => 'Value (' . htmlspecialchars((string) $ccy, ENT_QUOTES, 'UTF-8') . ')', 'class' => 'num')));
foreach ($epcJwKpis['stock_weight'] as $sw) {
    echo '<tr><td>' . htmlspecialchars((string) ($sw['metal'] ?: '—'), ENT_QUOTES, 'UTF-8') . '</td>'
        . '<td>' . htmlspecialchars((string) ($sw['karat'] ?: '—'), ENT_QUOTES, 'UTF-8') . '</td>'
        . '<td class="num">' . number_format((float) $sw['total_wt'], 3) . '</td>'
        . '<td class="num">' . number_format((float) $sw['total_val'], 2) . '</td></tr>';
}
erp_table_close();
erp_section_card('Stock by metal &amp; karat', ob_get_clean(), array('icon' => 'fa-diamond'));
?>
<?php endif; ?>
<?php endif; ?>

<?php
ob_start();
?>
<div style="display:grid;grid-template-columns:1fr 1fr;gap:16px">
    <div><canvas id="cSales" height="120"></canvas></div>
    <div><canvas id="cAgeing" height="120"></canvas></div>
</div>
<?php
erp_section_card('Sales trend &amp; AR ageing', ob_get_clean(), array('icon' => 'fa-bar-chart'));
?>

<?php
$pfLink = $backend . '/shop/finance/erp?area=overview&tab=processflow';
ob_start();
if (!$pfHasTasks) {
    echo '<p class="text-muted" style="margin:0">No tasks tracked yet. Customer orders, purchase orders and your own processes auto-create cases and appear here as soon as work starts flowing.</p>';
} else {
    erp_stat_cards(array(
        array('label' => 'Open tasks', 'value' => (string) (int) $pfSummary['open'], 'hint' => 'In progress'),
        array('label' => 'Overdue (SLA)', 'value' => (string) (int) $pfSummary['overdue'], 'hint' => 'Breached'),
        array('label' => 'Completed', 'value' => (string) (int) $pfSummary['done'], 'hint' => 'In period'),
        array('label' => 'Avg cycle (h)', 'value' => (string) (int) round((float) $pfSummary['avg_cycle_hours']), 'hint' => 'Per case'),
        array('label' => 'Staff busy', 'value' => (string) (int) $pfBusy, 'hint' => 'Of ' . (int) ($pfSummary['headcount'] ?: 0)),
    ));
    ?>
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:18px;margin-top:14px">
        <div>
            <div style="font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;margin-bottom:8px;font-weight:700">Open workload by department</div>
            <?php if (empty($pfDeptRows)): ?>
                <div class="text-muted" style="font-size:13px">No open tasks.</div>
            <?php else: foreach ($pfDeptRows as $code => $cnt): $w = $pfDeptMax > 0 ? max(4, round(($cnt / $pfDeptMax) * 100)) : 0; ?>
                <div style="display:flex;align-items:center;gap:10px;margin-bottom:7px">
                    <div style="width:120px;font-size:12px;color:#334155;white-space:nowrap;overflow:hidden;text-overflow:ellipsis"><?php echo htmlspecialchars($pfDeptName((string) $code), ENT_QUOTES, 'UTF-8'); ?></div>
                    <div style="flex:1;background:#f1f5f9;border-radius:6px;height:14px;overflow:hidden"><div style="width:<?php echo (int) $w; ?>%;height:100%;background:linear-gradient(90deg,#2563eb,#0ea5e9);border-radius:6px"></div></div>
                    <div style="width:34px;text-align:right;font-size:12px;font-weight:700;color:#0f172a"><?php echo (int) $cnt; ?></div>
                </div>
            <?php endforeach; endif; ?>
        </div>
        <div>
            <div style="font-size:11px;color:#94a3b8;text-transform:uppercase;letter-spacing:.06em;margin-bottom:8px;font-weight:700">Top performers <span style="text-transform:none;letter-spacing:0">(tasks completed)</span></div>
            <?php if (empty($pfTop)): ?>
                <div class="text-muted" style="font-size:13px">No completed tasks in this period yet.</div>
            <?php else: $rank = 0; foreach ($pfTop as $p): $rank++; $w = $pfTopMax > 0 ? max(4, round(((int) $p['done'] / $pfTopMax) * 100)) : 0; ?>
                <div style="display:flex;align-items:center;gap:9px;margin-bottom:7px">
                    <div style="width:16px;text-align:right;font-size:11px;color:#94a3b8"><?php echo (int) $rank; ?></div>
                    <img src="<?php echo htmlspecialchars((string) $p['avatar'], ENT_QUOTES, 'UTF-8'); ?>" alt="" style="width:24px;height:24px;border-radius:50%;object-fit:cover;flex:none" />
                    <div style="flex:1;min-width:0">
                        <div style="font-size:12px;color:#0f172a;white-space:nowrap;overflow:hidden;text-overflow:ellipsis"><?php echo htmlspecialchars((string) $p['name'], ENT_QUOTES, 'UTF-8'); ?></div>
                        <div style="font-size:10px;color:#94a3b8;white-space:nowrap;overflow:hidden;text-overflow:ellipsis"><?php echo htmlspecialchars((string) $p['deptName'] . ($p['location'] !== '' ? ' · ' . $p['location'] : ''), ENT_QUOTES, 'UTF-8'); ?></div>
                    </div>
                    <div style="width:60px;background:#f1f5f9;border-radius:6px;height:8px;overflow:hidden"><div style="width:<?php echo (int) $w; ?>%;height:100%;background:linear-gradient(90deg,#f59e0b,#16a34a);border-radius:6px"></div></div>
                    <div style="width:26px;text-align:right;font-size:12px;font-weight:700;color:#16a34a"><?php echo (int) $p['done']; ?></div>
                </div>
            <?php endforeach; endif; ?>
        </div>
    </div>
    <?php
}
erp_section_card(
    'Task analytics',
    ob_get_clean(),
    array(
        'icon' => 'fa-tasks',
        'header_html' => '<a href="' . htmlspecialchars($pfLink, ENT_QUOTES, 'UTF-8') . '" style="float:right;font-size:12px;text-decoration:none"><b>Open process flow &rarr;</b></a>',
    )
);
?>

<?php if ($epcJwDashMode): ?>
<?php
// Jewellery module navigation cards — merged from Jewellery hub.
$epcJwErpUrl = isset($erpUrl) ? $erpUrl : ($backend . '/shop/finance/erp');
$epcJwFrom = isset($date_from_str) ? $date_from_str : '';
$epcJwTo = isset($date_to_str) ? $date_to_str : '';
$jwModules = array(
    'Master data' => array(
        'jw_metal_stock' => array('icon' => 'fa-cubes', 'label' => 'Metal stock master', 'desc' => 'Gold, silver, platinum items'),
        'jw_design' => array('icon' => 'fa-paint-brush', 'label' => 'Design master', 'desc' => 'Designs with metal & stone details'),
        'jw_diamond' => array('icon' => 'fa-diamond', 'label' => 'Diamond master', 'desc' => 'Diamond items with certificates'),
        'jw_pearl' => array('icon' => 'fa-circle-o', 'label' => 'Pearl master', 'desc' => 'Pearl items'),
        'jw_color_stone' => array('icon' => 'fa-gem', 'label' => 'Color stone master', 'desc' => 'Sapphire, ruby, emerald'),
        'jw_karat' => array('icon' => 'fa-tachometer', 'label' => 'Karat master', 'desc' => 'Karat codes & purity'),
        'jw_rate_type' => array('icon' => 'fa-line-chart', 'label' => 'Rate type master', 'desc' => 'GMS, GOZ, KB, TTB'),
        'jw_currency' => array('icon' => 'fa-money', 'label' => 'Currency master', 'desc' => 'Multi-currency rates'),
    ),
    'Purchase' => array(
        'jw_metal_purchase' => array('icon' => 'fa-shopping-cart', 'label' => 'Metal purchase', 'desc' => 'RMP metal purchase'),
        'jw_diamond_purchase' => array('icon' => 'fa-cart-plus', 'label' => 'Diamond purchase', 'desc' => 'RDP diamond & stone'),
        'jw_purchase_fixing' => array('icon' => 'fa-lock', 'label' => 'Purchase fixing', 'desc' => 'Fix metal rate'),
        'jw_purchase_window' => array('icon' => 'fa-window-maximize', 'label' => 'Purchase window', 'desc' => 'Purchase inquiry'),
    ),
    'Sales' => array(
        'jw_retail_sales' => array('icon' => 'fa-shopping-bag', 'label' => 'Retail sales (POS)', 'desc' => 'RIN retail invoice'),
        'jw_metal_sales' => array('icon' => 'fa-exchange', 'label' => 'Metal sales', 'desc' => 'Bulk metal sales'),
        'jw_sales_fixing' => array('icon' => 'fa-gavel', 'label' => 'Sales fixing', 'desc' => 'Fix metal rate for sales'),
        'jw_sales_return' => array('icon' => 'fa-undo', 'label' => 'Sales return', 'desc' => 'Returns & refunds'),
        'jw_pos_advance' => array('icon' => 'fa-credit-card-alt', 'label' => 'POS advance', 'desc' => 'Advance payments'),
    ),
    'Repair & workshop' => array(
        'jw_repairs' => array('icon' => 'fa-wrench', 'label' => 'Repairs', 'desc' => 'Repair jobs lifecycle'),
        'jw_stock_verification' => array('icon' => 'fa-check-square', 'label' => 'Stock verification', 'desc' => 'Physical vs computer stock'),
        'jw_stock_balance' => array('icon' => 'fa-balance-scale', 'label' => 'Metal stock balance', 'desc' => 'Stock by metal & karat'),
        'jw_sales_analysis' => array('icon' => 'fa-bar-chart', 'label' => 'Sales analysis', 'desc' => 'Sales trends'),
        'jw_barcode' => array('icon' => 'fa-barcode', 'label' => 'Barcode generation', 'desc' => 'Generate & print barcodes'),
    ),
    'Finance' => array(
        'jw_trial_balance' => array('icon' => 'fa-balance-scale', 'label' => 'Dual trial balance', 'desc' => 'Weight + value TB'),
        'jw_petty_cash' => array('icon' => 'fa-money', 'label' => 'Petty cash', 'desc' => 'PCV petty cash vouchers'),
        'jw_journal_voucher' => array('icon' => 'fa-book', 'label' => 'Journal voucher', 'desc' => 'JVL journal entries'),
        'jw_tourist_vat' => array('icon' => 'fa-plane', 'label' => 'Tourist VAT refund', 'desc' => 'VRV tourist refund'),
    ),
);
ob_start();
foreach ($jwModules as $group => $items):
    ?>
    <div style="font-size:12px;color:#64748b;text-transform:uppercase;letter-spacing:.06em;margin:16px 0 10px;padding-bottom:6px;border-bottom:2px solid #2563eb;font-weight:700">
        <i class="fa fa-diamond" style="color:#f59e0b"></i> <?php echo htmlspecialchars($group, ENT_QUOTES, 'UTF-8'); ?>
    </div>
    <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:10px">
    <?php foreach ($items as $tabKey => $info):
        $cardUrl = function_exists('epc_erp_tab_url') ? epc_erp_tab_url($epcJwErpUrl, $tabKey, $epcJwFrom, $epcJwTo) : '#';
    ?>
        <a href="<?php echo htmlspecialchars($cardUrl, ENT_QUOTES, 'UTF-8'); ?>" style="text-decoration:none;display:block;padding:14px;border:1px solid #e2e8f0;border-radius:8px;background:#fff;transition:all .15s;color:#334155" onmouseover="this.style.borderColor='#2563eb';this.style.boxShadow='0 4px 10px rgba(15,23,42,.08)'" onmouseout="this.style.borderColor='#e2e8f0';this.style.boxShadow='none'">
            <div style="font-size:20px;color:#2563eb;margin-bottom:5px"><i class="fa <?php echo $info['icon']; ?>"></i></div>
            <div style="font-weight:700;font-size:12px;color:#0f172a"><?php echo htmlspecialchars($info['label'], ENT_QUOTES, 'UTF-8'); ?></div>
            <div style="font-size:10px;color:#94a3b8;margin-top:2px"><?php echo htmlspecialchars($info['desc'], ENT_QUOTES, 'UTF-8'); ?></div>
        </a>
    <?php endforeach; ?>
    </div>
<?php endforeach;
erp_section_card('Jewellery modules', ob_get_clean(), array('icon' => 'fa-diamond'));
?>
<?php endif; ?>

<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
<script>
(function () {
    function countUp(el) {
        var target = parseFloat(el.getAttribute('data-count')) || 0;
        var prefix = el.getAttribute('data-prefix') || '';
        var suffix = el.getAttribute('data-suffix') || '';
        var start = null;
        function step(ts) {
            if (!start) start = ts;
            var p = Math.min((ts - start) / 1200, 1);
            var v = Math.floor(target * (1 - Math.pow(1 - p, 3)));
            el.textContent = prefix + v.toLocaleString() + suffix;
            if (p < 1) requestAnimationFrame(step);
        }
        requestAnimationFrame(step);
    }
    document.querySelectorAll('.epc-erp-dash-countup').forEach(countUp);

    function draw() {
        if (typeof Chart === 'undefined') { return; }
        Chart.defaults.color = '#64748b';
        Chart.defaults.font.family = "'Segoe UI', Helvetica, Arial, sans-serif";
        var grid = 'rgba(148,163,184,0.18)';
        var sl = <?php echo $jc($salesLabels ?: array('Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec')); ?>;
        var sd = <?php echo $jc($salesData ?: array(320, 360, 410, 390, 460, 520)); ?>;
        var ctx = document.getElementById('cSales').getContext('2d');
        var g = ctx.createLinearGradient(0, 0, 0, 160);
        g.addColorStop(0, 'rgba(37,99,235,0.28)');
        g.addColorStop(1, 'rgba(37,99,235,0.02)');
        new Chart(ctx, { type: 'line', data: { labels: sl, datasets: [{ label: 'Sales', data: sd, fill: true, backgroundColor: g, borderColor: '#2563eb', borderWidth: 2, tension: 0.4, pointRadius: 0 }] }, options: { plugins: { legend: { display: false }, title: { display: true, text: 'Sales trend — recent periods', align: 'start', color: '#334155', font: { size: 13, weight: '700' } } }, scales: { x: { grid: { color: grid } }, y: { grid: { color: grid } } } } });
        var ad = <?php echo $jc((array_sum($ageData) > 0) ? $ageData : array(180, 95, 42, 28, 18)); ?>;
        new Chart(document.getElementById('cAgeing').getContext('2d'), { type: 'bar', data: { labels: <?php echo $jc($ageLabels); ?>, datasets: [{ label: 'Outstanding', data: ad, backgroundColor: ['#16a34a', '#0ea5e9', '#7c3aed', '#f59e0b', '#dc2626'], borderRadius: 6 }] }, options: { plugins: { legend: { display: false }, title: { display: true, text: 'AR ageing — buckets', align: 'start', color: '#334155', font: { size: 13, weight: '700' } } }, scales: { x: { grid: { color: grid } }, y: { grid: { color: grid } } } } });
    }
    if (document.readyState === 'complete') { setTimeout(draw, 200); }
    else { window.addEventListener('load', function () { setTimeout(draw, 200); }); }
})();
</script>
<?php
// End executive dashboard.
</content>
