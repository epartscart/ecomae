<?php
/**
 * Tourist Refund Scheme — generate invoices with barcode for tourist VAT refund claims.
 * Worldwide: UAE Planet Tax Free, EU Tax Free Shopping, UK VAT Retail Export Scheme.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-plane"></i> Tourist Refund',
	'Issue invoices with barcode for tourist VAT refund — works with Planet Tax Free (UAE), Global Blue (EU), and other schemes worldwide.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Tourist Refund'),
	),
	array(array('label' => 'New refund invoice', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-barcode"></i> Tourist refund invoices</h4>
	<p class="text-muted">When invoice category is set to "Tourist Refund", a barcode is auto-generated for the refund claim. The tourist presents this at departure.</p>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="tr_table">
		<thead><tr><th>Invoice #</th><th>Date</th><th>Customer</th><th>Passport</th><th>Amount</th><th>VAT refundable</th><th>Barcode</th><th>Status</th></tr></thead>
		<tbody></tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> Scheme configuration</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Refund scheme provider</label>
			<select class="form-control input-sm">
				<option>Planet Tax Free (UAE)</option>
				<option>Global Blue (EU)</option>
				<option>Premier Tax Free</option>
				<option>HMRC VAT Retail Export (UK)</option>
				<option>Custom / Manual</option>
			</select>
		</div>
		<div class="pm-field"><label>Minimum purchase for refund</label><input type="number" class="form-control input-sm" value="250"></div>
		<div class="pm-field"><label>VAT refund rate (%)</label><input type="number" class="form-control input-sm" value="5" step="0.1"></div>
		<div class="pm-field"><label>Barcode format</label>
			<select class="form-control input-sm"><option>Code 128</option><option>QR Code</option><option>EAN-13</option><option>PDF417</option></select>
		</div>
		<div class="pm-field"><label>Auto-print refund slip</label>
			<select class="form-control input-sm"><option value="1">Yes — print with invoice</option><option value="0">No — manual</option></select>
		</div>
		<div class="pm-field"><label>Passport required</label>
			<select class="form-control input-sm"><option value="1">Yes — mandatory</option><option value="0">No — optional</option></select>
		</div>
	</div>
</div>
<script>
(function(){
	var invoices=[
		{inv:'TRF-2026-0012',date:'2026-06-20',cust:'John Smith',passport:'GB****567',amt:'4,500 AED',vat:'225 AED',barcode:'TRF0012UAE2026',status:'Pending'},
		{inv:'TRF-2026-0011',date:'2026-06-19',cust:'Maria Garcia',passport:'ES****234',amt:'8,200 AED',vat:'410 AED',barcode:'TRF0011UAE2026',status:'Claimed'},
		{inv:'TRF-2026-0010',date:'2026-06-18',cust:'Yuki Tanaka',passport:'JP****891',amt:'12,000 AED',vat:'600 AED',barcode:'TRF0010UAE2026',status:'Refunded'},
		{inv:'TRF-2026-0009',date:'2026-06-17',cust:'Hans Mueller',passport:'DE****456',amt:'3,800 AED',vat:'190 AED',barcode:'TRF0009UAE2026',status:'Expired'},
	];
	var tb=document.querySelector('#tr_table tbody');
	invoices.forEach(function(r){
		var cls={Pending:'warning',Claimed:'info',Refunded:'success',Expired:'danger'}[r.status]||'default';
		tb.innerHTML+='<tr><td><code>'+r.inv+'</code></td><td>'+r.date+'</td><td>'+r.cust+'</td><td>'+r.passport+'</td><td>'+r.amt+'</td><td><strong>'+r.vat+'</strong></td><td><code>'+r.barcode+'</code> <i class="fa fa-barcode"></i></td><td><span class="label label-'+cls+'">'+r.status+'</span></td></tr>';
	});
})();
</script>
<?php
erp_section_card('Tourist Refund', ob_get_clean(), array('icon' => 'fa-plane'));
