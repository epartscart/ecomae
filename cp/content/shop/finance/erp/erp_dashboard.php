<?php
/**
 * Advanced ERP — animated executive dashboard (CP body).
 *
 * Crypto/fintech-style dark theme: count-up KPI tiles + animated Chart.js
 * charts. Data is tenant-scoped and entitlement-aware; every query is wrapped
 * so the page degrades to zeros rather than erroring on a live tenant whose
 * optional tables are absent. No existing table is modified.
 */
defined('_ASTEXE_') or die('No access');

$doc = $_SERVER['DOCUMENT_ROOT'];
require_once $doc . '/content/shop/finance/epc_erp_dashboard.php';
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
$themeBase = $backend . '/content/shop/finance/erp/theme';

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
?>
<link rel="stylesheet" href="<?php echo $themeBase; ?>/erp_theme.css" />
<?php if (function_exists('epc_theme_style_tag')) { echo epc_theme_style_tag_for_surface('erp'); } ?>
<div class="erp-theme" style="background:transparent;min-height:auto">
<div class="erp-orb a" style="position:absolute"></div>
<div class="erp-dash" style="padding-top:6px">
    <div class="erp-topbar">
        <div class="erp-brand">
            <div class="mark"><?php
            if (!empty($co['logo_url'])) {
                echo '<img src="' . htmlspecialchars((string) $co['logo_url'], ENT_QUOTES, 'UTF-8') . '" alt="" style="width:100%;height:100%;border-radius:12px;object-fit:cover">';
            } else {
                echo htmlspecialchars($initial, ENT_QUOTES, 'UTF-8');
            }
?></div>
            <div class="name">Spare247 ERP<small><?php echo htmlspecialchars($companyName, ENT_QUOTES, 'UTF-8'); ?></small></div>
        </div>
        <div>
            <?php if ($ccy !== ''): ?><span class="erp-chip">Currency: <b><?php echo htmlspecialchars($ccy, ENT_QUOTES, 'UTF-8'); ?></b></span><?php endif; ?>
            <span class="erp-chip">Live dashboard</span>
            <a class="erp-chip" style="text-decoration:none" href="<?php echo $backend; ?>/shop/finance/erp"><b>Open ERP &rarr;</b></a>
        </div>
    </div>

    <?php if (!$hasData): ?>
    <div class="erp-panel" style="margin-bottom:16px">
        <h3>Welcome to your animated dashboard <span>&mdash; no transactions yet</span></h3>
        <div style="color:var(--erp-muted);font-size:13px;line-height:1.7">
            Tiles and charts below animate with your real data as soon as you start recording sales, purchases and bank movements. The numbers shown now are placeholders so you can preview the layout.
        </div>
    </div>
    <?php endif; ?>

    <div class="erp-grid">
        <div class="erp-kpi" style="animation-delay:.05s"><div class="lbl">Cash Position</div><div class="val" data-count="<?php echo (int) ($cashPos ?: 1284500); ?>" data-prefix="<?php echo htmlspecialchars($ccy . ' ', ENT_QUOTES, 'UTF-8'); ?>">0</div><div class="delta up">live</div></div>
        <div class="erp-kpi" style="animation-delay:.12s"><div class="lbl">Receivables</div><div class="val" data-count="<?php echo (int) ($arOutstanding ?: 318900); ?>" data-prefix="<?php echo htmlspecialchars($ccy . ' ', ENT_QUOTES, 'UTF-8'); ?>">0</div><div class="delta down">outstanding</div></div>
        <div class="erp-kpi" style="animation-delay:.19s"><div class="lbl">Payables</div><div class="val" data-count="<?php echo (int) ($apOutstanding ?: 156200); ?>" data-prefix="<?php echo htmlspecialchars($ccy . ' ', ENT_QUOTES, 'UTF-8'); ?>">0</div><div class="delta up">to pay</div></div>
        <div class="erp-kpi" style="animation-delay:.26s"><div class="lbl">Stock Value</div><div class="val" data-count="<?php echo (int) ($stockValue ?: 2105400); ?>" data-prefix="<?php echo htmlspecialchars($ccy . ' ', ENT_QUOTES, 'UTF-8'); ?>">0</div><div class="delta up">on hand</div></div>
    </div>

    <?php if ($epcJwDashMode && !empty($epcJwKpis)): ?>
    <div class="erp-grid" style="margin-top:8px">
        <div class="erp-kpi" style="animation-delay:.33s;border-left:3px solid #f59e0b"><div class="lbl"><i class="fa fa-diamond"></i> Stock Weight</div><div class="val" data-count="<?php echo (int)$epcJwKpis['total_stock_wt']; ?>" data-suffix=" g">0</div><div class="delta up">grams on hand</div></div>
        <div class="erp-kpi" style="animation-delay:.38s;border-left:3px solid #f59e0b"><div class="lbl"><i class="fa fa-diamond"></i> Stock Value</div><div class="val" data-count="<?php echo (int)$epcJwKpis['total_stock_val']; ?>" data-prefix="<?php echo htmlspecialchars($ccy . ' ', ENT_QUOTES, 'UTF-8'); ?>">0</div><div class="delta up">inventory</div></div>
        <div class="erp-kpi" style="animation-delay:.43s;border-left:3px solid #16e0a3"><div class="lbl"><i class="fa fa-diamond"></i> Purchase Wt</div><div class="val" data-count="<?php echo (int)$epcJwKpis['purchase_wt']; ?>" data-suffix=" g">0</div><div class="delta up">grams bought</div></div>
        <div class="erp-kpi" style="animation-delay:.48s;border-left:3px solid #2bb8ff"><div class="lbl"><i class="fa fa-diamond"></i> Sales Wt</div><div class="val" data-count="<?php echo (int)$epcJwKpis['sales_wt']; ?>" data-suffix=" g">0</div><div class="delta up">grams sold</div></div>
    </div>
    <div class="erp-grid" style="margin-top:4px">
        <div class="erp-kpi" style="animation-delay:.53s;border-left:3px solid #9b6bff"><div class="lbl"><i class="fa fa-diamond"></i> Purchase Value</div><div class="val" data-count="<?php echo (int)$epcJwKpis['purchase_val']; ?>" data-prefix="<?php echo htmlspecialchars($ccy . ' ', ENT_QUOTES, 'UTF-8'); ?>">0</div><div class="delta up">total</div></div>
        <div class="erp-kpi" style="animation-delay:.58s;border-left:3px solid #16e0a3"><div class="lbl"><i class="fa fa-diamond"></i> Sales Value</div><div class="val" data-count="<?php echo (int)$epcJwKpis['sales_val']; ?>" data-prefix="<?php echo htmlspecialchars($ccy . ' ', ENT_QUOTES, 'UTF-8'); ?>">0</div><div class="delta up">revenue</div></div>
        <div class="erp-kpi" style="animation-delay:.63s;border-left:3px solid #ffb020"><div class="lbl"><i class="fa fa-wrench"></i> Repairs Open</div><div class="val" data-count="<?php echo $epcJwKpis['repairs_open']; ?>">0</div><div class="delta up">in workshop</div></div>
        <div class="erp-kpi" style="animation-delay:.68s;border-left:3px solid #16e0a3"><div class="lbl"><i class="fa fa-check-circle"></i> Repairs Ready</div><div class="val" data-count="<?php echo $epcJwKpis['repairs_ready']; ?>">0</div><div class="delta up">for delivery</div></div>
    </div>
    <?php if (!empty($epcJwKpis['stock_weight'])): ?>
    <div class="erp-panel" style="margin-top:12px;animation-delay:.72s">
        <h3><i class="fa fa-diamond"></i> Stock by Metal &amp; Karat <span>&mdash; weight + value</span></h3>
        <table class="table table-bordered table-condensed" style="color:#cdd9ee;font-size:12px;margin:0">
            <thead><tr style="background:rgba(120,160,220,0.06)"><th>Metal</th><th>Karat</th><th class="text-right">Weight (g)</th><th class="text-right">Value (<?php echo htmlspecialchars($ccy, ENT_QUOTES, 'UTF-8'); ?>)</th></tr></thead>
            <tbody>
            <?php foreach ($epcJwKpis['stock_weight'] as $sw): ?>
            <tr><td><?php echo htmlspecialchars($sw['metal'] ?: '—', ENT_QUOTES, 'UTF-8'); ?></td>
                <td><?php echo htmlspecialchars($sw['karat'] ?: '—', ENT_QUOTES, 'UTF-8'); ?></td>
                <td class="text-right"><?php echo number_format((float)$sw['total_wt'], 3); ?></td>
                <td class="text-right"><?php echo number_format((float)$sw['total_val'], 2); ?></td></tr>
            <?php endforeach; ?>
            </tbody>
        </table>
    </div>
    <?php endif; ?>
    <?php endif; ?>

    <div class="erp-panels">
        <div class="erp-panel" style="animation-delay:.2s"><h3>Sales Trend <span>&mdash; recent periods</span></h3><canvas id="cSales" height="120"></canvas></div>
        <div class="erp-panel" style="animation-delay:.3s"><h3>AR Ageing <span>&mdash; buckets</span></h3><canvas id="cAgeing" height="120"></canvas></div>
    </div>

    <?php $pfLink = $backend . '/shop/finance/erp?area=overview&tab=processflow'; ?>
    <div class="erp-panel" style="margin-top:16px;animation-delay:.36s">
        <h3>Task analytics <span>&mdash; process flow across every department</span>
            <a class="erp-chip" style="float:right;text-decoration:none;font-size:11px" href="<?php echo htmlspecialchars($pfLink, ENT_QUOTES, 'UTF-8'); ?>"><b>Open process flow &rarr;</b></a>
        </h3>
        <?php if (!$pfHasTasks): ?>
        <div style="color:var(--erp-muted);font-size:13px;line-height:1.7">
            No tasks tracked yet. Customer orders, purchase orders and your own processes auto-create cases and appear here as soon as work starts flowing.
        </div>
        <?php else: ?>
        <div class="erp-grid" style="margin:6px 0 14px">
            <div class="erp-kpi" style="animation-delay:.05s"><div class="lbl">Open tasks</div><div class="val" data-count="<?php echo (int) $pfSummary['open']; ?>">0</div><div class="delta up">in progress</div></div>
            <div class="erp-kpi" style="animation-delay:.1s"><div class="lbl">Overdue (SLA)</div><div class="val" data-count="<?php echo (int) $pfSummary['overdue']; ?>">0</div><div class="delta <?php echo ((int) $pfSummary['overdue'] > 0) ? 'down' : 'up'; ?>">breached</div></div>
            <div class="erp-kpi" style="animation-delay:.15s"><div class="lbl">Completed</div><div class="val" data-count="<?php echo (int) $pfSummary['done']; ?>">0</div><div class="delta up">in period</div></div>
            <div class="erp-kpi" style="animation-delay:.2s"><div class="lbl">Avg cycle (h)</div><div class="val" data-count="<?php echo (int) round((float) $pfSummary['avg_cycle_hours']); ?>">0</div><div class="delta up">per case</div></div>
            <div class="erp-kpi" style="animation-delay:.25s"><div class="lbl">Staff busy</div><div class="val" data-count="<?php echo (int) $pfBusy; ?>">0</div><div class="delta up">of <?php echo (int) ($pfSummary['headcount'] ?: 0); ?></div></div>
        </div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:18px">
            <div>
                <div style="font-size:12px;color:var(--erp-muted);text-transform:uppercase;letter-spacing:.06em;margin-bottom:8px">Open workload by department</div>
                <?php if (empty($pfDeptRows)): ?>
                    <div style="color:var(--erp-muted);font-size:13px">No open tasks.</div>
                <?php else: foreach ($pfDeptRows as $code => $cnt): $w = $pfDeptMax > 0 ? max(4, round(($cnt / $pfDeptMax) * 100)) : 0; ?>
                    <div style="display:flex;align-items:center;gap:10px;margin-bottom:7px">
                        <div style="width:120px;font-size:12px;color:#cdd9ee;white-space:nowrap;overflow:hidden;text-overflow:ellipsis"><?php echo htmlspecialchars($pfDeptName((string) $code), ENT_QUOTES, 'UTF-8'); ?></div>
                        <div style="flex:1;background:rgba(120,160,220,0.08);border-radius:6px;height:14px;overflow:hidden"><div style="width:<?php echo (int) $w; ?>%;height:100%;background:linear-gradient(90deg,#3b82f6,#16e0a3);border-radius:6px"></div></div>
                        <div style="width:34px;text-align:right;font-size:12px;font-weight:700;color:#eaf1ff"><?php echo (int) $cnt; ?></div>
                    </div>
                <?php endforeach; endif; ?>
            </div>
            <div>
                <div style="font-size:12px;color:var(--erp-muted);text-transform:uppercase;letter-spacing:.06em;margin-bottom:8px">Top performers <span style="text-transform:none;letter-spacing:0">(tasks completed)</span></div>
                <?php if (empty($pfTop)): ?>
                    <div style="color:var(--erp-muted);font-size:13px">No completed tasks in this period yet.</div>
                <?php else: $rank = 0; foreach ($pfTop as $p): $rank++; $w = $pfTopMax > 0 ? max(4, round(((int) $p['done'] / $pfTopMax) * 100)) : 0; ?>
                    <div style="display:flex;align-items:center;gap:9px;margin-bottom:7px">
                        <div style="width:16px;text-align:right;font-size:11px;color:var(--erp-muted)"><?php echo (int) $rank; ?></div>
                        <img src="<?php echo htmlspecialchars((string) $p['avatar'], ENT_QUOTES, 'UTF-8'); ?>" alt="" style="width:24px;height:24px;border-radius:50%;object-fit:cover;flex:none" />
                        <div style="flex:1;min-width:0">
                            <div style="font-size:12px;color:#eaf1ff;white-space:nowrap;overflow:hidden;text-overflow:ellipsis"><?php echo htmlspecialchars((string) $p['name'], ENT_QUOTES, 'UTF-8'); ?></div>
                            <div style="font-size:10px;color:var(--erp-muted);white-space:nowrap;overflow:hidden;text-overflow:ellipsis"><?php echo htmlspecialchars((string) $p['deptName'] . ($p['location'] !== '' ? ' · ' . $p['location'] : ''), ENT_QUOTES, 'UTF-8'); ?></div>
                        </div>
                        <div style="width:60px;background:rgba(120,160,220,0.08);border-radius:6px;height:8px;overflow:hidden"><div style="width:<?php echo (int) $w; ?>%;height:100%;background:linear-gradient(90deg,#f59e0b,#16e0a3);border-radius:6px"></div></div>
                        <div style="width:26px;text-align:right;font-size:12px;font-weight:700;color:#16e0a3"><?php echo (int) $p['done']; ?></div>
                    </div>
                <?php endforeach; endif; ?>
            </div>
        </div>
        <?php endif; ?>
    </div>

    <?php if ($epcJwDashMode): ?>
    <?php
    // Jewellery module navigation cards — merged from Jewellery hub
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
    ?>
    <div style="margin-top:16px">
    <?php foreach ($jwModules as $group => $items): ?>
        <div style="font-size:13px;color:var(--erp-muted,#8aa0c4);text-transform:uppercase;letter-spacing:.06em;margin:18px 0 10px;padding-bottom:6px;border-bottom:2px solid #3b82f6;">
            <i class="fa fa-diamond" style="color:#f59e0b"></i> <?php echo htmlspecialchars($group, ENT_QUOTES, 'UTF-8'); ?>
        </div>
        <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:10px">
        <?php foreach ($items as $tabKey => $info):
            $cardUrl = function_exists('epc_erp_tab_url') ? epc_erp_tab_url($epcJwErpUrl, $tabKey, $epcJwFrom, $epcJwTo) : '#';
        ?>
            <a href="<?php echo htmlspecialchars($cardUrl, ENT_QUOTES, 'UTF-8'); ?>" style="text-decoration:none;display:block;padding:14px;border:1px solid rgba(120,160,220,0.15);border-radius:8px;background:rgba(120,160,220,0.04);transition:all .2s;color:#cdd9ee" onmouseover="this.style.background='rgba(120,160,220,0.1)';this.style.borderColor='#3b82f6'" onmouseout="this.style.background='rgba(120,160,220,0.04)';this.style.borderColor='rgba(120,160,220,0.15)'">
                <div style="font-size:20px;color:#3b82f6;margin-bottom:5px"><i class="fa <?php echo $info['icon']; ?>"></i></div>
                <div style="font-weight:600;font-size:12px;color:#eaf1ff"><?php echo htmlspecialchars($info['label'], ENT_QUOTES, 'UTF-8'); ?></div>
                <div style="font-size:10px;color:var(--erp-muted,#8aa0c4);margin-top:2px"><?php echo htmlspecialchars($info['desc'], ENT_QUOTES, 'UTF-8'); ?></div>
            </a>
        <?php endforeach; ?>
        </div>
    <?php endforeach; ?>
    </div>
    <?php endif; ?>

</div>
</div>

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
            var p = Math.min((ts - start) / 1400, 1);
            var v = Math.floor(target * (1 - Math.pow(1 - p, 3)));
            el.textContent = prefix + v.toLocaleString() + suffix;
            if (p < 1) requestAnimationFrame(step);
        }
        requestAnimationFrame(step);
    }
    document.querySelectorAll('.erp-kpi .val').forEach(countUp);

    function draw() {
        if (typeof Chart === 'undefined') { return; }
        Chart.defaults.color = '#8aa0c4';
        var grid = 'rgba(120,160,220,0.08)';
        var sl = <?php echo $jc($salesLabels ?: array('Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec')); ?>;
        var sd = <?php echo $jc($salesData ?: array(320, 360, 410, 390, 460, 520)); ?>;
        var ctx = document.getElementById('cSales').getContext('2d');
        var g = ctx.createLinearGradient(0, 0, 0, 160);
        g.addColorStop(0, 'rgba(22,224,163,0.45)');
        g.addColorStop(1, 'rgba(22,224,163,0.02)');
        new Chart(ctx, { type: 'line', data: { labels: sl, datasets: [{ data: sd, fill: true, backgroundColor: g, borderColor: '#16e0a3', borderWidth: 2, tension: 0.4, pointRadius: 0 }] }, options: { plugins: { legend: { display: false } }, scales: { x: { grid: { color: grid } }, y: { grid: { color: grid } } } } });
        var ad = <?php echo $jc((array_sum($ageData) > 0) ? $ageData : array(180, 95, 42, 28, 18)); ?>;
        new Chart(document.getElementById('cAgeing').getContext('2d'), { type: 'bar', data: { labels: <?php echo $jc($ageLabels); ?>, datasets: [{ data: ad, backgroundColor: ['#16e0a3', '#2bb8ff', '#9b6bff', '#ffb020', '#ff5d73'], borderRadius: 6 }] }, options: { plugins: { legend: { display: false } }, scales: { x: { grid: { color: grid } }, y: { grid: { color: grid } } } } });
    }
    if (document.readyState === 'complete') { setTimeout(draw, 200); }
    else { window.addEventListener('load', function () { setTimeout(draw, 200); }); }
})();
</script>
<?php
// End animated dashboard.
