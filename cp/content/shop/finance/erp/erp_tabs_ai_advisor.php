<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_bos_ai.php';

$aiRevenue = epc_bos_ai_revenue_forecast($db_link, 6, 3);
$aiCash = epc_bos_ai_cashflow_forecast($db_link, 3);
$aiInv = epc_bos_ai_inventory_predictions($db_link);
$aiRecs = epc_bos_ai_recommendations($db_link, $date_from, $date_to);
$aiLlm = epc_bos_ai_llm_available();

erp_page_header(
	'<i class="fa fa-magic"></i> Intelligent BOS — AI advisor',
	'Forecasting, predictive inventory, cash-flow prediction and automated decision support computed live from your ERP data.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'AI advisor'),
	)
);

/* ---- Natural-language assistant ---- */
ob_start();
?>
<p class="text-muted" style="font-size:12px;margin-bottom:8px;">
	Ask in plain language — e.g. <em>"what should I do?"</em>, <em>"forecast cash flow"</em>,
	<em>"forecast revenue"</em>, <em>"what to reorder?"</em>, <em>"how much do customers owe me?"</em>.
	<?php if (!$aiLlm): ?><span class="label label-default" title="Set OPENAI_API_KEY to enable free-form chat">rules engine</span><?php else: ?><span class="label label-success">LLM enabled</span><?php endif; ?>
</p>
<form id="epc_ai_form" class="form-inline" style="margin-bottom:8px;">
	<input type="hidden" id="epc_ai_csrf" value="<?php echo epc_erp_h($csrf); ?>">
	<input type="text" id="epc_ai_q" class="form-control" placeholder="Ask the BOS…" style="min-width:380px;" autocomplete="off">
	<button type="submit" class="btn btn-primary"><i class="fa fa-paper-plane"></i> Ask</button>
</form>
<div id="epc_ai_answer" class="well well-sm" style="display:none;margin-top:6px;"></div>
<?php
erp_section_card('Ask the BOS', ob_get_clean(), array('icon' => 'fa-comments'));

/* ---- Recommendations / decision support ---- */
ob_start();
echo '<table class="table table-condensed"><tbody>';
foreach ($aiRecs as $r) {
	$cls = array('high' => 'danger', 'medium' => 'warning', 'low' => 'success');
	echo '<tr><td style="width:90px;"><span class="label label-' . ($cls[$r['severity']] ?? 'default') . '">' . epc_erp_h(strtoupper($r['severity'])) . '</span></td>'
		. '<td><strong>' . epc_erp_h($r['title']) . '</strong><br><span class="text-muted">' . epc_erp_h($r['action']) . '</span></td></tr>';
}
echo '</tbody></table>';
erp_section_card('Recommended actions', ob_get_clean(), array('icon' => 'fa-lightbulb-o'));

/* ---- Revenue forecast ---- */
ob_start();
echo '<p class="text-muted" style="font-size:12px;">Linear-trend + mean blend over the last ' . count($aiRevenue['series']) . ' months. Confidence: <strong>' . epc_erp_h($aiRevenue['confidence']) . '</strong>; trend ' . epc_erp_money($aiRevenue['trend_per_month']) . '/month.</p>';
echo '<table class="table table-condensed table-bordered" style="font-size:12px;"><thead><tr><th>Month</th><th class="text-right">Revenue</th><th>Type</th></tr></thead><tbody>';
foreach ($aiRevenue['series'] as $s) {
	echo '<tr><td>' . epc_erp_h($s['label']) . '</td><td class="text-right">' . epc_erp_money($s['revenue']) . '</td><td><span class="text-muted">actual</span></td></tr>';
}
foreach ($aiRevenue['forecast'] as $f) {
	echo '<tr style="background:#f6fbff;"><td>' . epc_erp_h($f['label']) . '</td><td class="text-right"><strong>' . epc_erp_money($f['value']) . '</strong></td><td><span class="label label-info">forecast</span></td></tr>';
}
echo '</tbody></table>';
erp_section_card('Revenue forecast (next 3 months)', ob_get_clean(), array('icon' => 'fa-line-chart'));

/* ---- Cash-flow forecast ---- */
ob_start();
echo '<p class="text-muted" style="font-size:12px;">Opening cash ' . epc_erp_money($aiCash['opening_cash']) . '; avg operating cash ' . epc_erp_money($aiCash['avg_operating_cash']) . '/mo; AR ' . epc_erp_money($aiCash['ar']) . ', AP ' . epc_erp_money($aiCash['ap']) . '.</p>';
if (!empty($aiCash['liquidity_alert'])) {
	echo '<div class="alert alert-danger" style="padding:8px;"><i class="fa fa-exclamation-triangle"></i> Projected cash dips below zero (min ' . epc_erp_money($aiCash['min_projected_cash']) . '). Act on collections.</div>';
}
echo '<table class="table table-condensed table-bordered" style="font-size:12px;"><thead><tr><th>Month</th><th class="text-right">Expected in</th><th class="text-right">Expected out</th><th class="text-right">Net</th><th class="text-right">Projected cash</th></tr></thead><tbody>';
foreach ($aiCash['points'] as $p) {
	echo '<tr><td>' . epc_erp_h($p['label']) . '</td><td class="text-right">' . epc_erp_money($p['expected_collections']) . '</td><td class="text-right">' . epc_erp_money($p['expected_payments']) . '</td><td class="text-right">' . epc_erp_money($p['net']) . '</td><td class="text-right"><strong style="color:' . ($p['projected_cash'] < 0 ? '#c00' : 'green') . ';">' . epc_erp_money($p['projected_cash']) . '</strong></td></tr>';
}
echo '</tbody></table>';
erp_section_card('Cash-flow forecast (next 3 months)', ob_get_clean(), array('icon' => 'fa-tint'));

/* ---- Predictive inventory ---- */
ob_start();
if (!$aiInv) {
	erp_empty_state('No outbound demand recorded yet — predictive reorder needs sales/issue movements.', 'fa-cubes');
} else {
	echo '<table class="table table-condensed table-bordered table-striped" style="font-size:12px;"><thead><tr><th>SKU</th><th>Item</th><th class="text-right">On hand</th><th class="text-right">Daily use</th><th class="text-right">Days cover</th><th class="text-right">Reorder qty</th><th class="text-right">Est. value</th><th>Status</th></tr></thead><tbody>';
	foreach (array_slice($aiInv, 0, 50) as $i) {
		$cls = array('critical' => 'danger', 'reorder' => 'warning', 'ok' => 'success');
		echo '<tr><td><code>' . epc_erp_h($i['sku']) . '</code></td><td>' . epc_erp_h($i['name']) . '</td>'
			. '<td class="text-right">' . epc_erp_h(number_format($i['on_hand'], 3)) . '</td>'
			. '<td class="text-right">' . epc_erp_h(number_format($i['daily_use'], 3)) . '</td>'
			. '<td class="text-right">' . epc_erp_h($i['days_cover']) . '</td>'
			. '<td class="text-right"><strong>' . epc_erp_h(number_format($i['recommend_qty'], 2)) . ' ' . epc_erp_h($i['unit']) . '</strong></td>'
			. '<td class="text-right">' . epc_erp_money($i['reorder_value']) . '</td>'
			. '<td><span class="label label-' . ($cls[$i['status']] ?? 'default') . '">' . epc_erp_h($i['status']) . '</span></td></tr>';
	}
	echo '</tbody></table>';
}
erp_section_card('Predictive inventory (reorder advisor)', ob_get_clean(), array('icon' => 'fa-cubes'));
?>
<script>
(function(){
	var form = document.getElementById('epc_ai_form');
	if (!form) return;
	form.addEventListener('submit', function(e){
		e.preventDefault();
		var q = (document.getElementById('epc_ai_q')||{}).value||'';
		var out = document.getElementById('epc_ai_answer');
		out.style.display = 'block';
		out.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Thinking…';
		var fd = new FormData();
		fd.append('action', 'ai_query');
		fd.append('q', q);
		fd.append('date_from', <?php echo json_encode($date_from_str); ?>);
		fd.append('date_to', <?php echo json_encode($date_to_str); ?>);
		fd.append('csrf_guard_key', (document.getElementById('epc_ai_csrf')||{}).value||'');
		fetch(<?php echo json_encode($erpAjaxEndpoint); ?>, { method:'POST', body:fd, credentials:'same-origin' })
			.then(function(r){ return r.json(); })
			.then(function(j){
				out.innerHTML = '<i class="fa fa-magic"></i> ' + (j.answer ? j.answer.replace(/</g,'&lt;') : (j.message||'No answer'));
			})
			.catch(function(){ out.innerHTML = '<span class="text-danger">Request failed.</span>'; });
	});
})();
</script>
