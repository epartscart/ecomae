<?php
/**
 * Multi-Level Inventory Report — by category, sub-category, SKU, location.
 * Drill from top-level category down to individual item.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-cubes"></i> Inventory Report',
	'Multi-level inventory report — drill down from category → sub-category → SKU. Filter by location, value, age.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Inventory Report'),
	),
	array(
		array('label' => 'Export Excel', 'url' => '#', 'class' => 'btn-success', 'icon' => 'fa-file-excel-o'),
		array('label' => 'Export PDF', 'url' => '#', 'class' => 'btn-default', 'icon' => 'fa-file-pdf-o'),
	)
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-filter"></i> Report filters</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Level</label>
			<select class="form-control input-sm" id="inv_level">
				<option value="cat">Category summary</option>
				<option value="subcat">Sub-category detail</option>
				<option value="sku">SKU level (individual items)</option>
			</select>
		</div>
		<div class="pm-field"><label>Category</label>
			<select class="form-control input-sm"><option value="">All categories</option><option>Gold</option><option>Diamond</option><option>Silver</option><option>Platinum</option><option>Watches</option></select>
		</div>
		<div class="pm-field"><label>Location</label>
			<select class="form-control input-sm"><option value="">All locations</option><option>Main warehouse</option><option>Showroom</option><option>Vault</option><option>Exhibition</option></select>
		</div>
		<div class="pm-field"><label>Valuation method</label>
			<select class="form-control input-sm"><option>WAC (Weighted Average Cost)</option><option>FIFO</option><option>Latest cost</option><option>Retail price</option></select>
		</div>
		<div class="pm-field"><label>&nbsp;</label>
			<button class="btn btn-primary btn-sm"><i class="fa fa-search"></i> Generate report</button>
		</div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-bar-chart"></i> Category summary</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="inv_cat_table">
		<thead><tr><th>Category</th><th>Sub-categories</th><th>SKUs</th><th>Quantity</th><th>Cost value</th><th>Retail value</th><th>% of total</th><th></th></tr></thead>
		<tbody></tbody>
		<tfoot><tr style="font-weight:bold;background:#f8fafc;"><td>TOTAL</td><td>18</td><td>2,458</td><td>4,892</td><td>8,450,000 AED</td><td>12,600,000 AED</td><td>100%</td><td></td></tr></tfoot>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-sitemap"></i> Sub-category detail (click category above to drill)</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;" id="inv_subcat_table">
		<thead><tr><th>Sub-category</th><th>SKUs</th><th>Quantity</th><th>Cost value</th><th>Avg. days in stock</th><th>Reorder level</th><th></th></tr></thead>
		<tbody>
			<tr class="text-muted"><td colspan="7" class="text-center">Click a category above to see sub-categories</td></tr>
		</tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-archive"></i> Aging analysis</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;">
		<thead><tr><th>Age bracket</th><th>Items</th><th>Value</th><th>% of stock</th></tr></thead>
		<tbody>
			<tr><td>0-30 days</td><td>1,245</td><td>3,200,000 AED</td><td>38%</td></tr>
			<tr><td>31-60 days</td><td>856</td><td>2,100,000 AED</td><td>25%</td></tr>
			<tr><td>61-90 days</td><td>423</td><td>1,500,000 AED</td><td>18%</td></tr>
			<tr><td class="text-danger">90+ days (slow-moving)</td><td class="text-danger">368</td><td class="text-danger">1,650,000 AED</td><td class="text-danger">19%</td></tr>
		</tbody>
	</table>
</div>
<script>
(function(){
	var cats=[
		{cat:'Gold Jewellery',subcats:5,skus:890,qty:1420,cost:'3,200,000 AED',retail:'4,800,000 AED',pct:'38%'},
		{cat:'Diamond Jewellery',subcats:4,skus:456,qty:780,cost:'2,800,000 AED',retail:'4,200,000 AED',pct:'33%'},
		{cat:'Silver',subcats:3,skus:620,qty:1500,cost:'850,000 AED',retail:'1,200,000 AED',pct:'10%'},
		{cat:'Watches',subcats:3,skus:245,qty:380,cost:'1,200,000 AED',retail:'1,800,000 AED',pct:'14%'},
		{cat:'Platinum',subcats:2,skus:147,qty:212,cost:'400,000 AED',retail:'600,000 AED',pct:'5%'},
	];
	var tb=document.querySelector('#inv_cat_table tbody');
	cats.forEach(function(c){
		tb.innerHTML+='<tr><td><strong>'+c.cat+'</strong></td><td>'+c.subcats+'</td><td>'+c.skus+'</td><td>'+c.qty+'</td><td>'+c.cost+'</td><td>'+c.retail+'</td><td>'+c.pct+'</td><td><a class="btn btn-xs btn-primary"><i class="fa fa-arrow-right"></i> Drill</a></td></tr>';
	});
})();
</script>
<?php
erp_section_card('Inventory Report', ob_get_clean(), array('icon' => 'fa-cubes'));
