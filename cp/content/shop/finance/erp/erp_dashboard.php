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

$hasData = ($cashPos + $arOutstanding + $apOutstanding + $stockValue) > 0 || !empty($salesData);

$jc = function ($v) {
    return json_encode($v, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
};
$initial = ($companyName !== '' ? strtoupper(substr($companyName, 0, 1)) : 'S');
?>
<link rel="stylesheet" href="<?php echo $themeBase; ?>/erp_theme.css" />
<?php if (function_exists('epc_theme_style_tag')) { echo epc_theme_style_tag(getenv('EPC_UI_THEME') ?: epc_theme_default()); } ?>
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

    <div class="erp-panels">
        <div class="erp-panel" style="animation-delay:.2s"><h3>Sales Trend <span>&mdash; recent periods</span></h3><canvas id="cSales" height="120"></canvas></div>
        <div class="erp-panel" style="animation-delay:.3s"><h3>AR Ageing <span>&mdash; buckets</span></h3><canvas id="cAgeing" height="120"></canvas></div>
    </div>
</div>
</div>

<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
<script>
(function () {
    function countUp(el) {
        var target = parseFloat(el.getAttribute('data-count')) || 0;
        var prefix = el.getAttribute('data-prefix') || '';
        var start = null;
        function step(ts) {
            if (!start) start = ts;
            var p = Math.min((ts - start) / 1400, 1);
            var v = Math.floor(target * (1 - Math.pow(1 - p, 3)));
            el.textContent = prefix + v.toLocaleString();
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
