<?php
/**
 * Barcode Purchase & Margin — in jewellery, barcode on product IS the purchase record.
 * All margin settings, cost breakdown, and salesman invoice details set at barcode level.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-barcode"></i> Barcode / Purchase Margin',
	'In jewellery, the barcode IS the purchase — all cost, margin, making charges, and salesman info configured at barcode level.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Barcode Purchase'),
	),
	array(array('label' => 'New barcode entry', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-barcode"></i> Barcode = purchase record</h4>
	<p class="text-muted">Each barcode contains full purchase information: supplier, cost, weight, karat, making charges, stone details. Margin is set here for salesman invoicing.</p>
	<table class="table table-bordered table-condensed" style="font-size:12px;" id="bp_table">
		<thead>
			<tr>
				<th>Barcode</th><th>Description</th><th>Supplier</th><th>Karat</th><th>Gross wt</th><th>Net wt</th>
				<th>Gold rate</th><th>Gold value</th><th>Making</th><th>Stone cost</th><th>Total cost</th>
				<th>Margin %</th><th>Sell price</th><th>Status</th>
			</tr>
		</thead>
		<tbody></tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-calculator"></i> Margin settings (salesman invoice)</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Default margin %</label><input type="number" class="form-control input-sm" value="15" step="0.5"></div>
		<div class="pm-field"><label>Making charge margin %</label><input type="number" class="form-control input-sm" value="20" step="0.5"></div>
		<div class="pm-field"><label>Stone markup %</label><input type="number" class="form-control input-sm" value="25" step="0.5"></div>
		<div class="pm-field"><label>Min. margin threshold</label><input type="number" class="form-control input-sm" value="8" step="0.5"></div>
		<div class="pm-field"><label>Discount authority</label>
			<select class="form-control input-sm"><option>Manager only (below min. margin)</option><option>Any salesman</option><option>No discounts allowed</option></select>
		</div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-search"></i> Barcode lookup (full info window)</h4>
	<div class="row">
		<div class="col-md-6">
			<div class="input-group">
				<input type="text" class="form-control input-sm" placeholder="Scan or enter barcode..." id="bp_lookup">
				<span class="input-group-btn"><button class="btn btn-primary btn-sm"><i class="fa fa-search"></i></button></span>
			</div>
		</div>
	</div>
	<div id="bp_detail" style="margin-top:12px;display:none;">
		<div class="panel panel-info">
			<div class="panel-heading"><strong><i class="fa fa-barcode"></i> GR-202606-0001 — Full information</strong></div>
			<div class="panel-body" style="font-size:13px;">
				<div class="row">
					<div class="col-md-6">
						<table class="table table-condensed">
							<tr><td><strong>Description</strong></td><td>22K Gold Ring — Flower Pattern</td></tr>
							<tr><td><strong>Supplier</strong></td><td>Al Romaizan Gold</td></tr>
							<tr><td><strong>Purchase date</strong></td><td>2026-06-15</td></tr>
							<tr><td><strong>PO reference</strong></td><td>PO-2026-0045</td></tr>
							<tr><td><strong>Karat</strong></td><td>22K (91.67%)</td></tr>
							<tr><td><strong>Gross weight</strong></td><td>8.45g</td></tr>
							<tr><td><strong>Net weight (gold)</strong></td><td>7.80g</td></tr>
							<tr><td><strong>Stone weight</strong></td><td>0.65g</td></tr>
						</table>
					</div>
					<div class="col-md-6">
						<table class="table table-condensed">
							<tr><td><strong>Gold rate (purchase)</strong></td><td>292.00 AED/g</td></tr>
							<tr><td><strong>Gold value</strong></td><td>2,277.60 AED</td></tr>
							<tr><td><strong>Making charge</strong></td><td>380.00 AED</td></tr>
							<tr><td><strong>Stone cost</strong></td><td>192.40 AED</td></tr>
							<tr><td><strong>Total cost</strong></td><td><strong>2,850.00 AED</strong></td></tr>
							<tr><td><strong>Margin (15%)</strong></td><td>427.50 AED</td></tr>
							<tr><td><strong>Selling price</strong></td><td><strong style="color:#16a34a;">3,277.50 AED</strong></td></tr>
							<tr><td><strong>Status</strong></td><td><span class="label label-success">In stock — Showroom A</span></td></tr>
						</table>
					</div>
				</div>
			</div>
		</div>
	</div>
</div>
<script>
(function(){
	var items=[
		{bc:'GR-202606-0001',desc:'22K Gold Ring — Flower',sup:'Al Romaizan',karat:'22K',gross:'8.45g',net:'7.80g',rate:'292.00',goldVal:'2,277.60',making:'380.00',stone:'192.40',total:'2,850.00',margin:'15',sell:'3,277.50',status:'In stock'},
		{bc:'DC-202606-0012',desc:'18K Diamond Pendant',sup:'Kalyan',karat:'18K',gross:'4.20g',net:'3.80g',rate:'220.00',goldVal:'836.00',making:'450.00',stone:'7,214.00',total:'8,500.00',margin:'20',sell:'10,200.00',status:'In stock'},
		{bc:'GC-202606-0034',desc:'22K Gold Chain 20"',sup:'Malabar',karat:'22K',gross:'24.50g',net:'24.50g',rate:'292.00',goldVal:'7,154.00',making:'1,746.00',stone:'—',total:'8,900.00',margin:'12',sell:'9,968.00',status:'In stock'},
		{bc:'BN-202606-0008',desc:'22K Bangle Set Bridal',sup:'Joyalukkas',karat:'22K',gross:'65.00g',net:'64.20g',rate:'292.00',goldVal:'18,746.40',making:'4,200.00',stone:'453.60',total:'23,400.00',margin:'18',sell:'27,612.00',status:'Reserved'},
	];
	var tb=document.querySelector('#bp_table tbody');
	items.forEach(function(i){
		var cls=i.status==='In stock'?'success':(i.status==='Reserved'?'warning':'default');
		tb.innerHTML+='<tr><td><code>'+i.bc+'</code></td><td>'+i.desc+'</td><td>'+i.sup+'</td><td>'+i.karat+'</td><td>'+i.gross+'</td><td>'+i.net+'</td><td>'+i.rate+'</td><td>'+i.goldVal+'</td><td>'+i.making+'</td><td>'+i.stone+'</td><td><strong>'+i.total+'</strong></td><td>'+i.margin+'%</td><td><strong style="color:#16a34a;">'+i.sell+'</strong></td><td><span class="label label-'+cls+'">'+i.status+'</span></td></tr>';
	});
	document.getElementById('bp_lookup').addEventListener('keyup',function(e){
		if(e.key==='Enter') document.getElementById('bp_detail').style.display='block';
	});
})();
</script>
<?php
erp_section_card('Barcode Purchase & Margin', ob_get_clean(), array('icon' => 'fa-barcode'));
