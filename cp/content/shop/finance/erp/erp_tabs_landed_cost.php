<?php
/**
 * Landed Cost Module — distribute additional expenses (freight, duty, insurance)
 * over the cost of purchased products by value, weight, or quantity.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-ship"></i> Landed Cost',
	'Distribute import/freight/duty expenses over product cost — by value, weight, or quantity. Ensures accurate inventory valuation.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Landed Cost'),
	),
	array(array('label' => 'New allocation', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plus'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-calculator"></i> Cost allocation</h4>
	<p class="text-muted">Allocate additional costs (freight, customs duty, insurance, handling) to purchase items. Distributed by value proportion, weight, or quantity.</p>
	<div class="pm-fields">
		<div class="pm-field"><label>Purchase order / GRN</label><input type="text" class="form-control input-sm" placeholder="PO-2026-0045 or GRN-2026-0032"></div>
		<div class="pm-field"><label>Allocation method</label>
			<select class="form-control input-sm" id="lc_method">
				<option value="value">By value (proportional to item cost)</option>
				<option value="weight">By weight</option>
				<option value="qty">By quantity</option>
				<option value="equal">Equal split</option>
			</select>
		</div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-plus-circle"></i> Additional costs</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="lc_costs">
		<thead><tr><th>Expense type</th><th>Vendor / Reference</th><th>Amount</th><th>Currency</th><th></th></tr></thead>
		<tbody></tbody>
	</table>
	<button class="btn btn-default btn-sm" id="lc_add_cost"><i class="fa fa-plus"></i> Add expense line</button>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-table"></i> Allocation preview</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="lc_preview">
		<thead><tr><th>Item</th><th>Qty</th><th>Unit cost</th><th>Line total</th><th>Weight</th><th>Freight alloc.</th><th>Duty alloc.</th><th>Insurance</th><th>New unit cost</th><th>Variance</th></tr></thead>
		<tbody></tbody>
		<tfoot><tr style="font-weight:bold;background:#f8fafc;"><td colspan="4">TOTALS</td><td></td><td>2,800</td><td>4,200</td><td>600</td><td></td><td></td></tr></tfoot>
	</table>
	<button class="btn btn-primary btn-sm" style="margin-top:8px;"><i class="fa fa-check"></i> Apply landed cost</button>
	<button class="btn btn-default btn-sm" style="margin-top:8px;"><i class="fa fa-print"></i> Print allocation report</button>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-history"></i> Allocation history</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;">
		<thead><tr><th>Date</th><th>PO / GRN</th><th>Items</th><th>Original cost</th><th>Added costs</th><th>New cost</th><th>Method</th><th></th></tr></thead>
		<tbody>
			<tr><td>2026-06-18</td><td>PO-2026-0042</td><td>12 items</td><td>85,000 AED</td><td>7,600 AED</td><td>92,600 AED</td><td>By value</td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
			<tr><td>2026-06-10</td><td>PO-2026-0038</td><td>8 items</td><td>52,000 AED</td><td>4,200 AED</td><td>56,200 AED</td><td>By weight</td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
			<tr><td>2026-06-02</td><td>PO-2026-0035</td><td>25 items</td><td>180,000 AED</td><td>12,400 AED</td><td>192,400 AED</td><td>By value</td><td><a class="btn btn-xs btn-default"><i class="fa fa-eye"></i></a></td></tr>
		</tbody>
	</table>
</div>
<script>
(function(){
	var costs=[
		{type:'Sea freight',vendor:'Maersk Line',amt:'2,800',cur:'AED'},
		{type:'Customs duty (5%)',vendor:'Dubai Customs',amt:'4,200',cur:'AED'},
		{type:'Insurance',vendor:'Orient Insurance',amt:'600',cur:'AED'},
	];
	var tb=document.querySelector('#lc_costs tbody');
	costs.forEach(function(c){
		tb.innerHTML+='<tr><td>'+c.type+'</td><td>'+c.vendor+'</td><td>'+c.amt+' '+c.cur+'</td><td>'+c.cur+'</td><td><a class="btn btn-xs btn-danger"><i class="fa fa-trash"></i></a></td></tr>';
	});
	var items=[
		{item:'22K Gold Chain 20"',qty:5,unit:'8,900',line:'44,500',wt:'122.5g',freight:'1,400',duty:'2,100',ins:'300',newCost:'9,660',var:'+760'},
		{item:'18K Diamond Ring',qty:3,unit:'8,500',line:'25,500',wt:'12.6g',freight:'800',duty:'1,200',ins:'170',newCost:'9,223',var:'+723'},
		{item:'22K Gold Bangles',qty:4,unit:'3,750',line:'15,000',wt:'42.0g',freight:'600',duty:'900',ins:'130',newCost:'4,158',var:'+408'},
	];
	var tb2=document.querySelector('#lc_preview tbody');
	items.forEach(function(i){
		tb2.innerHTML+='<tr><td>'+i.item+'</td><td>'+i.qty+'</td><td>'+i.unit+'</td><td>'+i.line+'</td><td>'+i.wt+'</td><td>'+i.freight+'</td><td>'+i.duty+'</td><td>'+i.ins+'</td><td><strong>'+i.newCost+'</strong></td><td class="text-warning">'+i.var+'</td></tr>';
	});
})();
</script>
<?php
erp_section_card('Landed Cost', ob_get_clean(), array('icon' => 'fa-ship'));
