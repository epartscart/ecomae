<?php
defined('_ASTEXE_') or die('No access');
/**
 * Executive dashboard — cross-module KPI cockpit.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_exec_dashboard.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';

erp_page_header(
	'<i class="fa fa-dashboard"></i> Executive dashboard',
	'Cross-module executive cockpit — headline KPIs, revenue trend, working capital, top suppliers and planning alerts.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Executive dashboard'),
	)
);

$periodFrom = isset($date_from) && $date_from > 0 ? (int) $date_from : strtotime(date('Y-m-01 00:00:00'));
$periodTo = isset($date_to) && $date_to > 0 ? (int) $date_to : time();

$csrfLocal = isset($csrf) ? $csrf : '';
$kpis = epc_bos_intel_kpis($db_link, $periodFrom, $periodTo);
$trend = epc_exec_trend($db_link, 6);
$topSup = epc_exec_top_suppliers($db_link, 5);
$alerts = epc_exec_planning_alerts($db_link);

$healthColor = static function (string $h): string {
	$map = array('good' => '#0a7d33', 'warn' => '#c98a00', 'bad' => '#c0392b', 'info' => '#2bb3c0');
	return $map[$h] ?? '#666';
};

$maxRev = 0.0;
foreach ($trend as $t) {
	$maxRev = max($maxRev, (float) $t['revenue'], (float) $t['profit']);
}
if ($maxRev <= 0) {
	$maxRev = 1.0;
}
?>
<style>
.epc-exec-kpis{display:flex;flex-wrap:wrap;gap:12px;margin-bottom:18px;}
.epc-exec-kpi{flex:1 1 18%;min-width:170px;background:#fff;border:1px solid #e3e8ee;border-left-width:4px;border-radius:6px;padding:12px 14px;}
.epc-exec-kpi .lbl{font-size:11px;text-transform:uppercase;letter-spacing:.04em;color:#8a97a8;}
.epc-exec-kpi .val{font-size:22px;font-weight:700;margin-top:3px;}
.epc-exec-kpi .hint{font-size:11px;color:#9aa6b5;margin-top:2px;}
.epc-exec-bars{display:flex;align-items:flex-end;gap:14px;height:180px;padding:10px 6px;border:1px solid #eef2f6;border-radius:6px;background:#fafbfc;}
.epc-exec-bars .col{flex:1;text-align:center;}
.epc-exec-bars .pair{display:flex;align-items:flex-end;justify-content:center;gap:3px;height:140px;}
.epc-exec-bars .b{width:14px;border-radius:2px 2px 0 0;}
.epc-exec-bars .cap{font-size:11px;color:#8a97a8;margin-top:5px;}
</style>

<div id="epc_erp_msg" class="alert" style="display:none;"></div>
<div style="margin-bottom:14px;">
	<button type="button" id="epc_exec_seed" class="btn btn-sm btn-primary"><i class="fa fa-database"></i> Generate sample sales</button>
	<button type="button" id="epc_exec_clear" class="btn btn-sm btn-default"><i class="fa fa-eraser"></i> Clear sample sales</button>
	<span class="text-muted" style="margin-left:8px;font-size:12px;">Seeds 6 months of completed orders (tagged, re-runnable) so revenue KPIs and the trend populate.</span>
</div>

<div class="epc-exec-kpis">
	<?php foreach ($kpis as $k):
		$col = $healthColor((string) $k['health']); ?>
		<div class="epc-exec-kpi" style="border-left-color:<?php echo $col; ?>;">
			<div class="lbl"><?php echo epc_erp_h($k['label']); ?></div>
			<div class="val" style="color:<?php echo $col; ?>;"><?php echo epc_erp_h(epc_bos_intel_format((float) $k['value'], (string) $k['format'])); ?></div>
			<div class="hint"><?php echo epc_erp_h($k['hint']); ?></div>
		</div>
	<?php endforeach; ?>
</div>

<div class="row">
	<div class="col-md-7">
		<h5><i class="fa fa-line-chart"></i> Revenue &amp; profit — last 6 months</h5>
		<div class="epc-exec-bars">
			<?php foreach ($trend as $t):
				$rh = (int) round(140 * ((float) $t['revenue']) / $maxRev);
				$ph = (int) round(140 * max(0, (float) $t['profit']) / $maxRev); ?>
				<div class="col">
					<div class="pair">
						<div class="b" style="height:<?php echo $rh; ?>px;background:#2bb3c0;" title="Revenue <?php echo epc_erp_h(number_format((float) $t['revenue'], 0)); ?>"></div>
						<div class="b" style="height:<?php echo $ph; ?>px;background:#0a7d33;" title="Profit <?php echo epc_erp_h(number_format((float) $t['profit'], 0)); ?>"></div>
					</div>
					<div class="cap"><?php echo epc_erp_h($t['label']); ?></div>
				</div>
			<?php endforeach; ?>
		</div>
		<p class="text-muted" style="margin-top:6px;"><span style="color:#2bb3c0;">&#9632;</span> Revenue &nbsp; <span style="color:#0a7d33;">&#9632;</span> Profit (ex-VAT)</p>
	</div>
	<div class="col-md-5">
		<h5><i class="fa fa-exclamation-triangle"></i> Planning alerts</h5>
		<table class="table table-condensed table-bordered">
			<tbody>
				<tr class="danger"><th>Stock-out / critical</th><td class="text-right"><?php echo (int) $alerts['danger']; ?></td></tr>
				<tr class="warning"><th>Below safety stock</th><td class="text-right"><?php echo (int) $alerts['warning']; ?></td></tr>
				<tr class="info"><th>Excess stock</th><td class="text-right"><?php echo (int) $alerts['info']; ?></td></tr>
				<tr><th>Dead stock</th><td class="text-right"><?php echo (int) $alerts['default']; ?></td></tr>
				<tr><th>Total exceptions</th><td class="text-right"><strong><?php echo (int) $alerts['total']; ?></strong></td></tr>
			</tbody>
		</table>
		<p><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'order_planning', $date_from_str, $date_to_str) . (strpos(epc_erp_tab_url($erpUrl, 'order_planning', $date_from_str, $date_to_str), '?') === false ? '?' : '&') . 'opl_view=exceptions'); ?>">View exceptions &raquo;</a></p>
	</div>
</div>

<div class="row" style="margin-top:14px;">
	<div class="col-md-7">
		<h5><i class="fa fa-truck"></i> Top suppliers by spend</h5>
		<?php if (empty($topSup)): ?>
			<p class="text-muted">No supplier spend recorded.</p>
		<?php else: ?>
		<table class="table table-condensed table-bordered table-hover">
			<thead><tr><th>Supplier</th><th class="text-center">Rating</th><th class="text-right">Spend</th><th class="text-right">POs</th><th class="text-right">Score</th></tr></thead>
			<tbody>
			<?php foreach ($topSup as $s): ?>
				<tr>
					<td><?php echo epc_erp_h($s['name']); ?></td>
					<td class="text-center"><?php echo epc_erp_h($s['rating']); ?></td>
					<td class="text-right"><?php echo epc_erp_money($s['spend']); ?></td>
					<td class="text-right"><?php echo (int) $s['po_count']; ?></td>
					<td class="text-right"><?php echo epc_erp_money($s['score']); ?></td>
				</tr>
			<?php endforeach; ?>
			</tbody>
		</table>
		<?php endif; ?>
	</div>
	<div class="col-md-5">
		<h5><i class="fa fa-link"></i> Quick links</h5>
		<div class="list-group">
			<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'ai_advisor', $date_from_str, $date_to_str)); ?>"><i class="fa fa-magic"></i> AI advisor &amp; forecasts</a>
			<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'pl', $date_from_str, $date_to_str)); ?>"><i class="fa fa-bar-chart"></i> Profit &amp; loss</a>
			<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'order_planning', $date_from_str, $date_to_str)); ?>"><i class="fa fa-cubes"></i> Order planning</a>
			<a class="list-group-item" href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'supplier_portal', $date_from_str, $date_to_str)); ?>"><i class="fa fa-handshake-o"></i> Supplier portal</a>
		</div>
	</div>
</div>
<?php
$endpoint = isset($GLOBALS['erpAjaxEndpoint']) ? $GLOBALS['erpAjaxEndpoint'] : ('/' . (isset($GLOBALS['DP_Config']->backend_dir) ? $GLOBALS['DP_Config']->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php');
$endpointJson = json_encode($endpoint);
$csrfJson = json_encode($csrfLocal);
echo <<<HTML
<script>
(function(){
	var url = {$endpointJson};
	var csrf = {$csrfJson};
	function post(action){ var fd=new FormData(); fd.append('action',action); fd.append('csrf_guard_key',csrf); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
	function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 900); }
	var sd=document.getElementById('epc_exec_seed'); if(sd) sd.addEventListener('click', function(){ if(!confirm('Generate 6 months of sample completed sales orders? (re-runnable; tagged demo)')) return; sd.disabled=true; sd.innerHTML='Generating…'; post('demo_seed_sales').then(msg).catch(function(){ sd.disabled=false; sd.textContent='Generate sample sales'; }); });
	var cl=document.getElementById('epc_exec_clear'); if(cl) cl.addEventListener('click', function(){ if(!confirm('Clear all sample sales orders?')) return; post('demo_clear_sales').then(msg); });
})();
</script>
HTML;
