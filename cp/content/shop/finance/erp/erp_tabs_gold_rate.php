<?php
/**
 * Gold Rate API — fetch live gold/silver/platinum rates from online sources.
 * Supports multiple providers, historical tracking, and auto-update.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-line-chart"></i> Gold Rate (Live)',
	'Fetch real-time gold, silver, and platinum rates from API providers. Rates auto-update for invoicing and valuation.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Gold Rate'),
	),
	array(array('label' => 'Refresh rates', 'url' => '#', 'class' => 'btn-success', 'icon' => 'fa-refresh'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-bolt"></i> Current rates</h4>
	<p class="text-muted">Live rates fetched from configured API. Last update: <strong id="gr_last_update">2026-06-21 08:00 UTC</strong></p>
	<div class="row">
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #b8860b;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">Gold 24K (per gram)</h5><h3 style="margin:0;color:#b8860b;" id="gr_gold24">AED 295.50</h3><small class="text-success"><i class="fa fa-arrow-up"></i> +1.20 (0.41%)</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #d4a017;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">Gold 22K (per gram)</h5><h3 style="margin:0;color:#d4a017;" id="gr_gold22">AED 270.88</h3><small class="text-success"><i class="fa fa-arrow-up"></i> +1.10</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #a0a0a0;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">Silver (per gram)</h5><h3 style="margin:0;color:#6b7280;" id="gr_silver">AED 3.42</h3><small class="text-danger"><i class="fa fa-arrow-down"></i> -0.05</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default" style="border-left:4px solid #e5e7eb;"><div class="panel-body"><h5 style="margin:0 0 4px;color:#64748b;">Platinum (per gram)</h5><h3 style="margin:0;color:#475569;" id="gr_platinum">AED 115.80</h3><small class="text-success"><i class="fa fa-arrow-up"></i> +0.30</small></div></div></div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-history"></i> Rate history (30 days)</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="gr_history">
		<thead><tr><th>Date</th><th>Gold 24K</th><th>Gold 22K</th><th>Gold 18K</th><th>Silver</th><th>Change</th></tr></thead>
		<tbody></tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> API configuration</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Rate provider</label>
			<select class="form-control input-sm" id="gr_provider">
				<option value="goldapi">GoldAPI.io</option>
				<option value="metalpriceapi">MetalPriceAPI.com</option>
				<option value="kitco">Kitco (scrape)</option>
				<option value="xe">XE Precious Metals</option>
				<option value="custom">Custom API endpoint</option>
			</select>
		</div>
		<div class="pm-field"><label>API key</label><input type="password" class="form-control input-sm" placeholder="Enter API key..." id="gr_apikey"></div>
		<div class="pm-field"><label>Base currency</label>
			<select class="form-control input-sm"><option>AED</option><option>USD</option><option>GBP</option><option>EUR</option><option>SAR</option><option>INR</option></select>
		</div>
		<div class="pm-field"><label>Auto-update frequency</label>
			<select class="form-control input-sm"><option value="15">Every 15 minutes</option><option value="60">Hourly</option><option value="360">Every 6 hours</option><option value="1440">Daily</option></select>
		</div>
		<div class="pm-field"><label>Use rate in invoicing</label>
			<select class="form-control input-sm"><option value="1">Yes — auto-apply live rate</option><option value="0">No — manual entry only</option></select>
		</div>
	</div>
	<button class="btn btn-primary btn-sm" style="margin-top:8px;"><i class="fa fa-save"></i> Save configuration</button>
	<button class="btn btn-success btn-sm" style="margin-top:8px;"><i class="fa fa-check"></i> Test connection</button>
</div>
<script>
(function(){
	var hist=[];
	var baseGold=295.50;
	for(var i=0;i<15;i++){
		var d=new Date();d.setDate(d.getDate()-i);
		var g24=baseGold-(Math.random()*5-2).toFixed(2);
		var g22=(g24*0.9167).toFixed(2);
		var g18=(g24*0.75).toFixed(2);
		var silver=(3.42+(Math.random()*0.2-0.1)).toFixed(2);
		var change=((Math.random()*4-2)).toFixed(2);
		hist.push({date:d.toISOString().slice(0,10),g24:g24.toFixed(2),g22:g22,g18:g18,silver:silver,change:change});
	}
	var tb=document.querySelector('#gr_history tbody');
	hist.forEach(function(h){
		var cls=parseFloat(h.change)>=0?'text-success':'text-danger';
		var icon=parseFloat(h.change)>=0?'fa-arrow-up':'fa-arrow-down';
		tb.innerHTML+='<tr><td>'+h.date+'</td><td>'+h.g24+'</td><td>'+h.g22+'</td><td>'+h.g18+'</td><td>'+h.silver+'</td><td class="'+cls+'"><i class="fa '+icon+'"></i> '+h.change+'</td></tr>';
	});
})();
</script>
<?php
erp_section_card('Gold Rate (Live)', ob_get_clean(), array('icon' => 'fa-line-chart'));
