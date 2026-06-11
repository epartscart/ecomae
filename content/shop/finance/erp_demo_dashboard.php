<?php
/**
 * Frontend ERP live-demo dashboard (no login).
 * URL: /erp-demo  (registered via epc-erp-demo-frontend-setup.php)
 *
 * Renders an animated, Blue & White ERP dashboard from the multi-industry
 * sample data (epc_erp_demo.php). Pure read-only demo numbers — never touches a
 * live tenant ledger — so prospects can preview the ERP straight from the
 * marketing page with the shared demo credential, frontend + ERP only (no CP).
 */
defined('_ASTEXE_') or die('No access');

$doc = $_SERVER['DOCUMENT_ROOT'];
require_once $doc . '/content/shop/finance/epc_erp_demo.php';
@require_once $doc . '/content/shop/finance/epc_erp_theme.php';
@require_once $doc . '/content/shop/finance/epc_demo_portal.php';

$langPrefix = function_exists('epc_demo_lang_prefix') ? epc_demo_lang_prefix() : '/en';

$industries = function_exists('epc_demo_industries') ? epc_demo_industries() : array();
$codes = array();
foreach ($industries as $ind) {
    $codes[$ind['code']] = $ind;
}
$industry = isset($_GET['industry']) ? preg_replace('/[^a-z]/', '', strtolower((string) $_GET['industry'])) : '';
if ($industry === '' || !isset($codes[$industry])) {
    $industry = !empty($industries) ? $industries[0]['code'] : 'jewellery';
}

$k = function_exists('epc_demo_kpis') ? epc_demo_kpis($industry) : array();
$ccy = isset($k['currency']) ? (string) $k['currency'] : 'AED';
$esc = static function ($s) {
    return htmlspecialchars((string) $s, ENT_QUOTES, 'UTF-8');
};
$money = static function ($v) use ($ccy) {
    return htmlspecialchars($ccy . ' ' . number_format((float) $v, 0), ENT_QUOTES, 'UTF-8');
};
$num = static function ($v) {
    return (float) $v;
};
$docChain = isset($k['doc_chain']) && is_array($k['doc_chain']) ? $k['doc_chain'] : array();
$themeTag = function_exists('epc_theme_style_tag_for_surface') ? epc_theme_style_tag_for_surface('erp') : '';
?>
<?php echo $themeTag; ?>
<style>
.epcdemo-wrap{--c:var(--erp-card,#fff);--b:var(--erp-card-brd,#d6e2f5);--t:var(--erp-text,#0d1b2a);--m:var(--erp-muted,#5b6b85);--a:var(--erp-accent,#1a56db);--a2:var(--erp-accent-2,#2b8fff);--up:var(--erp-up,#0f9d6b);--dn:var(--erp-down,#e23b54);background:var(--erp-bg-0,#f3f7fd);color:var(--t);border-radius:14px;padding:22px;margin:0 0 28px;font-family:inherit}
.epcdemo-bar{display:flex;flex-wrap:wrap;align-items:center;gap:12px;justify-content:space-between;margin-bottom:18px}
.epcdemo-title{font-size:22px;font-weight:800;margin:0;color:var(--t)}
.epcdemo-sub{color:var(--m);font-size:13px;margin-top:2px}
.epcdemo-cred{background:var(--c);border:1px solid var(--b);border-radius:10px;padding:10px 14px;font-size:13px;color:var(--t)}
.epcdemo-cred b{color:var(--a)}
.epcdemo-picker{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:18px}
.epcdemo-chip{display:inline-block;padding:8px 14px;border-radius:999px;border:1px solid var(--b);background:var(--c);color:var(--t);text-decoration:none;font-size:13px;font-weight:600;transition:.15s}
.epcdemo-chip:hover{border-color:var(--a);color:var(--a)}
.epcdemo-chip.on{background:var(--a);border-color:var(--a);color:#fff}
.epcdemo-kpis{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:14px;margin-bottom:18px}
.epcdemo-kpi{background:var(--c);border:1px solid var(--b);border-radius:12px;padding:16px 18px;box-shadow:0 1px 3px rgba(13,27,42,.05)}
.epcdemo-kpi .lab{color:var(--m);font-size:12px;text-transform:uppercase;letter-spacing:.04em;font-weight:700}
.epcdemo-kpi .val{font-size:26px;font-weight:800;margin-top:6px;color:var(--t)}
.epcdemo-kpi .delta{font-size:12px;margin-top:4px;color:var(--m)}
.epcdemo-kpi .delta.up{color:var(--up)}
.epcdemo-grid2{display:grid;grid-template-columns:1fr 1fr;gap:14px;margin-bottom:18px}
@media(max-width:760px){.epcdemo-grid2{grid-template-columns:1fr}}
.epcdemo-panel{background:var(--c);border:1px solid var(--b);border-radius:12px;padding:16px 18px}
.epcdemo-panel h4{margin:0 0 12px;font-size:14px;font-weight:800;color:var(--t)}
.epcdemo-chain{display:flex;flex-wrap:wrap;gap:8px;align-items:center}
.epcdemo-step{background:var(--erp-bg-2,#e9f1fc);border:1px solid var(--b);border-radius:8px;padding:7px 11px;font-size:12px;color:var(--t);font-weight:600}
.epcdemo-arrow{color:var(--a);font-weight:800}
.epcdemo-cta{display:flex;flex-wrap:wrap;gap:10px;margin-top:6px}
.epcdemo-btn{display:inline-block;padding:11px 18px;border-radius:10px;font-weight:700;text-decoration:none;font-size:14px}
.epcdemo-btn.p{background:var(--a);color:#fff}
.epcdemo-btn.s{background:var(--c);color:var(--a);border:1px solid var(--a)}
</style>
<div class="epcdemo-wrap">
  <div class="epcdemo-bar">
    <div>
      <h2 class="epcdemo-title"><?php echo $esc($k['company'] ?? 'Live ERP demo'); ?></h2>
      <div class="epcdemo-sub">Live ERP demo &middot; <?php echo $esc($codes[$industry]['name'] ?? ucfirst($industry)); ?> &middot; sample data, no signup</div>
    </div>
    <div class="epcdemo-cred">Shared demo login: <b>demo@ecomae.com</b> / <b>demo1234</b> &middot; valid 3 days</div>
  </div>

  <div class="epcdemo-picker">
    <?php foreach ($industries as $ind) {
        $on = $ind['code'] === $industry ? ' on' : '';
        echo '<a class="epcdemo-chip' . $on . '" href="' . $esc($langPrefix) . '/erp-demo?demo=1&industry=' . $esc(rawurlencode($ind['code'])) . '">' . $esc($ind['name']) . '</a>';
    } ?>
  </div>

  <div class="epcdemo-kpis">
    <div class="epcdemo-kpi"><div class="lab">Revenue</div><div class="val epcdemo-count" data-to="<?php echo $num($k['revenue'] ?? 0); ?>" data-prefix="<?php echo $esc($ccy . ' '); ?>"><?php echo $money($k['revenue'] ?? 0); ?></div><div class="delta up">Gross margin <?php echo $esc($k['gross_margin_pct'] ?? 0); ?>%</div></div>
    <div class="epcdemo-kpi"><div class="lab">Gross margin</div><div class="val epcdemo-count" data-to="<?php echo $num($k['gross_margin'] ?? 0); ?>" data-prefix="<?php echo $esc($ccy . ' '); ?>"><?php echo $money($k['gross_margin'] ?? 0); ?></div><div class="delta">COGS <?php echo $money($k['cogs'] ?? 0); ?></div></div>
    <div class="epcdemo-kpi"><div class="lab">AR outstanding</div><div class="val epcdemo-count" data-to="<?php echo $num($k['ar_outstanding'] ?? 0); ?>" data-prefix="<?php echo $esc($ccy . ' '); ?>"><?php echo $money($k['ar_outstanding'] ?? 0); ?></div><div class="delta"><?php echo (int) ($k['unpaid_orders'] ?? 0); ?> unpaid invoices</div></div>
    <div class="epcdemo-kpi"><div class="lab">Stock value</div><div class="val epcdemo-count" data-to="<?php echo $num($k['stock_value'] ?? 0); ?>" data-prefix="<?php echo $esc($ccy . ' '); ?>"><?php echo $money($k['stock_value'] ?? 0); ?></div><div class="delta"><?php echo (int) ($k['products'] ?? 0); ?> products</div></div>
    <div class="epcdemo-kpi"><div class="lab">Orders</div><div class="val epcdemo-count" data-to="<?php echo (int) ($k['orders'] ?? 0); ?>"><?php echo (int) ($k['orders'] ?? 0); ?></div><div class="delta up"><?php echo (int) ($k['paid_orders'] ?? 0); ?> paid</div></div>
    <div class="epcdemo-kpi"><div class="lab">Customers</div><div class="val epcdemo-count" data-to="<?php echo (int) ($k['customers'] ?? 0); ?>"><?php echo (int) ($k['customers'] ?? 0); ?></div><div class="delta">active accounts</div></div>
  </div>

  <div class="epcdemo-grid2">
    <div class="epcdemo-panel"><h4>Revenue &middot; COGS &middot; margin</h4><canvas id="epcDemoBar" height="170"></canvas></div>
    <div class="epcdemo-panel"><h4>Paid vs outstanding orders</h4><canvas id="epcDemoPie" height="170"></canvas></div>
  </div>

  <?php if (!empty($docChain)) { ?>
  <div class="epcdemo-panel" style="margin-bottom:18px"><h4>Document workflow (<?php echo $esc($codes[$industry]['name'] ?? $industry); ?>)</h4>
    <div class="epcdemo-chain">
      <?php $last = count($docChain) - 1; foreach ($docChain as $i => $step) {
          echo '<span class="epcdemo-step">' . $esc($step) . '</span>';
          if ($i < $last) { echo '<span class="epcdemo-arrow">&rarr;</span>'; }
      } ?>
    </div>
  </div>
  <?php } ?>

  <div class="epcdemo-cta">
    <a class="epcdemo-btn p" href="/?demo=1">View storefront demo</a>
    <a class="epcdemo-btn s" href="/erp">Open full ERP (sign in)</a>
    <a class="epcdemo-btn s" href="/platform/demo">Get your own 3-day demo</a>
  </div>
</div>
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
<script>
(function(){
  var css = getComputedStyle(document.documentElement);
  var accent = (css.getPropertyValue('--erp-accent')||'#1a56db').trim();
  var accent2 = (css.getPropertyValue('--erp-accent-2')||'#2b8fff').trim();
  var up = (css.getPropertyValue('--erp-up')||'#0f9d6b').trim();
  var dn = (css.getPropertyValue('--erp-down')||'#e23b54').trim();
  function countUp(el){
    var to = parseFloat(el.getAttribute('data-to')||'0'); if(isNaN(to)) return;
    var prefix = el.getAttribute('data-prefix')||''; var start=null, dur=900;
    function fmt(n){ return prefix + Math.round(n).toLocaleString(); }
    function step(ts){ if(!start) start=ts; var p=Math.min((ts-start)/dur,1); el.textContent=fmt(to*(0.15+0.85*p)); if(p<1) requestAnimationFrame(step); else el.textContent=fmt(to); }
    requestAnimationFrame(step);
  }
  document.querySelectorAll('.epcdemo-count').forEach(countUp);
  function draw(){
    if(typeof Chart==='undefined') return;
    var b=document.getElementById('epcDemoBar');
    if(b) new Chart(b,{type:'bar',data:{labels:['Revenue','COGS','Margin'],datasets:[{data:[<?php echo $num($k['revenue'] ?? 0); ?>,<?php echo $num($k['cogs'] ?? 0); ?>,<?php echo $num($k['gross_margin'] ?? 0); ?>],backgroundColor:[accent,accent2,up],borderRadius:6}]},options:{plugins:{legend:{display:false}},scales:{y:{beginAtZero:true}}}});
    var p=document.getElementById('epcDemoPie');
    if(p) new Chart(p,{type:'doughnut',data:{labels:['Paid','Outstanding'],datasets:[{data:[<?php echo (int) ($k['paid_orders'] ?? 0); ?>,<?php echo (int) ($k['unpaid_orders'] ?? 0); ?>],backgroundColor:[up,dn]}]},options:{plugins:{legend:{position:'bottom'}}}});
  }
  if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',draw);}else{draw();}
})();
</script>
