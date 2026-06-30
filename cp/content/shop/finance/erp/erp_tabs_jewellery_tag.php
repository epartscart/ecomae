<?php
/**
 * Jewellery TAG System — unique tag per item generated at barcode level.
 * Tags used for invoicing, transaction recording, tracking lifecycle.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-tag"></i> Jewellery TAG System',
	'Unique TAG per jewellery item — generated at barcode level. Used for invoicing, purchase tracking, sales, and audit trail.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Jewellery TAG'),
	),
	array(
		array('label' => 'Generate tags', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-barcode'),
		array('label' => 'Print tags', 'url' => '#', 'class' => 'btn-default', 'icon' => 'fa-print'),
	)
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-barcode"></i> TAG overview</h4>
	<div class="row">
		<div class="col-md-3"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;color:#b8860b;">2,458</h3><small class="text-muted">Total active tags</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;color:#16a34a;">1,890</h3><small class="text-muted">In stock</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;color:#2563eb;">456</h3><small class="text-muted">Sold (30 days)</small></div></div></div>
		<div class="col-md-3"><div class="panel panel-default"><div class="panel-body text-center"><h3 style="margin:0;color:#dc2626;">112</h3><small class="text-muted">Untagged items</small></div></div></div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-list"></i> TAG register</h4>
	<p class="text-muted">Each tag is unique, generated during purchase/barcode entry. TAG tracks the item's full lifecycle: purchase → stock → display → sale.</p>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="jt_table">
		<thead><tr><th>TAG #</th><th>Item description</th><th>Category</th><th>Karat</th><th>Gross wt</th><th>Net wt</th><th>Stone wt</th><th>Cost</th><th>Location</th><th>Status</th></tr></thead>
		<tbody></tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-plus-circle"></i> Generate new tags</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Purchase reference</label><input type="text" class="form-control input-sm" placeholder="PO-2026-0045"></div>
		<div class="pm-field"><label>Category</label>
			<select class="form-control input-sm"><option>Gold Ring</option><option>Gold Chain</option><option>Gold Bangle</option><option>Diamond Ring</option><option>Diamond Necklace</option><option>Silver</option><option>Platinum</option></select>
		</div>
		<div class="pm-field"><label>Quantity</label><input type="number" class="form-control input-sm" value="1"></div>
		<div class="pm-field"><label>TAG prefix</label><input type="text" class="form-control input-sm" value="GR" placeholder="e.g. GR, DC, BN"></div>
		<div class="pm-field"><label>&nbsp;</label><button class="btn btn-primary btn-sm"><i class="fa fa-barcode"></i> Generate</button></div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> TAG settings</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>TAG format</label>
			<select class="form-control input-sm"><option>PREFIX-YYYYMM-NNNN (e.g. GR-202606-0001)</option><option>PREFIX-NNNNN (sequential)</option><option>Custom pattern</option></select>
		</div>
		<div class="pm-field"><label>Auto-generate on purchase</label>
			<select class="form-control input-sm"><option value="1">Yes — tag at GRN level</option><option value="0">No — manual generation</option></select>
		</div>
		<div class="pm-field"><label>Barcode type</label>
			<select class="form-control input-sm"><option>Code 128</option><option>QR Code</option><option>EAN-13</option></select>
		</div>
		<div class="pm-field"><label>Print label size</label>
			<select class="form-control input-sm"><option>Small (25x15mm) — jewellery sticker</option><option>Medium (50x25mm)</option><option>Large (70x35mm)</option></select>
		</div>
	</div>
</div>
<script>
(function(){
	var tags=[
		{tag:'GR-202606-0001',desc:'22K Gold Ring — Flower Pattern',cat:'Gold Ring',karat:'22K',gross:'8.45g',net:'7.80g',stone:'0.65g',cost:'2,850 AED',loc:'Showroom A',status:'In stock'},
		{tag:'DC-202606-0012',desc:'18K Diamond Pendant — Solitaire 0.5ct',cat:'Diamond',karat:'18K',gross:'4.20g',net:'3.80g',stone:'0.40g (0.5ct)',cost:'8,500 AED',loc:'Vault',status:'In stock'},
		{tag:'GC-202606-0034',desc:'22K Gold Chain — Rope 20inch',cat:'Gold Chain',karat:'22K',gross:'24.50g',net:'24.50g',stone:'—',cost:'8,900 AED',loc:'Showroom B',status:'In stock'},
		{tag:'BN-202606-0008',desc:'22K Gold Bangle Set — Bridal',cat:'Gold Bangle',karat:'22K',gross:'65.00g',net:'64.20g',stone:'0.80g',cost:'23,400 AED',loc:'Bridal section',status:'Reserved'},
		{tag:'GR-202605-0089',desc:'18K Rose Gold Ring — Heart',cat:'Gold Ring',karat:'18K',gross:'5.10g',net:'4.80g',stone:'0.30g',cost:'1,950 AED',loc:'—',status:'Sold'},
	];
	var tb=document.querySelector('#jt_table tbody');
	tags.forEach(function(t){
		var cls={};cls['In stock']='success';cls['Reserved']='warning';cls['Sold']='default';cls['Exhibition']='info';
		var c=cls[t.status]||'default';
		tb.innerHTML+='<tr><td><code>'+t.tag+'</code></td><td>'+t.desc+'</td><td>'+t.cat+'</td><td><span class="label label-warning">'+t.karat+'</span></td><td>'+t.gross+'</td><td>'+t.net+'</td><td>'+t.stone+'</td><td>'+t.cost+'</td><td>'+t.loc+'</td><td><span class="label label-'+c+'">'+t.status+'</span></td></tr>';
	});
})();
</script>
<?php
erp_section_card('Jewellery TAG System', ob_get_clean(), array('icon' => 'fa-tag'));
