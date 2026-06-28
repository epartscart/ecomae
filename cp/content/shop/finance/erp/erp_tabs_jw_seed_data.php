<?php
/**
 * Jewellery Sample Data Seeder — populates comprehensive test data
 * across all existing ERP modules for a jewellery tenant.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';
epc_jw_ensure_integration_schema($db_link);

erp_page_header(
	'<i class="fa fa-database"></i> Jewellery sample data',
	'Seed comprehensive test data across Inventory, Purchase, Sales, Repairs, and GL for the jewellery industry.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Sample data'),
	)
);
erp_d365_assets();
?>

<div style="background:#fef9e7;padding:16px;border:1px solid #f0e68c;border-radius:6px;margin-bottom:16px;">
	<h4><i class="fa fa-diamond"></i> This will seed:</h4>
	<ul style="margin:8px 0;line-height:1.8;">
		<li><strong>3 warehouses</strong> — Main Showroom, Vault, Workshop</li>
		<li><strong>15 inventory items</strong> — Gold rings, necklaces, bangles (22K/18K/24K), silver, diamonds, pearls, platinum</li>
		<li><strong>5 suppliers</strong> — Dubai Gold Souk, Rajesh Gems, Antwerp Diamond Exchange, PAMP SA, Mikimoto</li>
		<li><strong>5 customers</strong> — with UAE phone numbers and emails</li>
		<li><strong>5 purchase orders</strong> — with jewellery weight + rate details (22K, 24K, diamonds, pearls)</li>
		<li><strong>5 sales orders</strong> — with weight sold + making charges + stone values</li>
		<li><strong>3 repair jobs</strong> — ring resize, chain repair, stone setting</li>
		<li><strong>7 GL entries</strong> — opening balances for dual trial balance (weight + value)</li>
	</ul>
	<p style="color:#b8860b;margin:0;"><i class="fa fa-info-circle"></i> Existing data is preserved — only new records are added (INSERT IGNORE).</p>
</div>

<form id="jw_seed_form" class="form-inline" style="margin-bottom:16px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<button type="submit" class="btn btn-warning btn-sm"><i class="fa fa-magic"></i> Seed sample data now</button>
</form>
<div id="jw_seed_result" style="display:none;"></div>

<script>
document.getElementById('jw_seed_form').addEventListener('submit', function(e){
	e.preventDefault();
	var fd = new FormData(this);
	fd.append('action', 'jw_seed_sample_data');
	fetch(<?php echo json_encode($erpAjaxEndpoint); ?>, {method:'POST', body:fd, credentials:'same-origin'})
		.then(function(r){return r.json()})
		.then(function(j){
			var out = document.getElementById('jw_seed_result');
			out.style.display = 'block';
			if(j.status){
				var d = j.seeded || {};
				out.innerHTML = '<div class="alert alert-success">'
					+ '<strong>Sample data seeded!</strong><br>'
					+ 'Warehouses: ' + (d.warehouses||0) + ' | '
					+ 'Items: ' + (d.items||0) + ' | '
					+ 'Suppliers: ' + (d.suppliers||0) + ' | '
					+ 'Customers: ' + (d.customers||0) + ' | '
					+ 'Purchases: ' + (d.purchases||0) + ' | '
					+ 'Sales: ' + (d.sales||0) + ' | '
					+ 'Repairs: ' + (d.repairs||0) + ' | '
					+ 'GL entries: ' + (d.gl_entries||0)
					+ '<br><a href="' + '<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)); ?>' + '">Go to Dashboard</a>'
					+ '</div>';
			} else {
				out.innerHTML = '<div class="alert alert-danger">' + (j.message||'Failed') + '</div>';
			}
		});
});
</script>
