<?php
/**
 * Jewellery Sample Data Seeder — Suntech ef-window style.
 * Populates comprehensive test data across all existing ERP modules for a jewellery tenant.
 */
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_jewellery_integration.php';
include __DIR__ . '/erp_entry_form_css.php';
epc_jw_ensure_integration_schema($db_link);

$csrfLocal = isset($csrf) ? $csrf : '';
$erpAjaxEndpoint = isset($erpAjaxUrl) ? $erpAjaxUrl : '';

erp_page_header(
	'<i class="fa fa-database"></i> Jewellery sample data',
	'Seed comprehensive test data across Inventory, Purchase, Sales, Repairs, and GL for the jewellery industry.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'System administration'),
		array('label' => 'Sample data'),
	)
);
?>
<div class="ef-window">
	<div class="ef-title"><i class="fa fa-database"></i> Jewellery Sample Data Seeder</div>
	<div class="ef-toolbar">
		<button class="btn btn-default btn-xs" onclick="window.location.reload()"><i class="fa fa-refresh"></i> Refresh</button>
	</div>
	<div class="ef-body">
		<div class="ef-section">
			<span class="ef-section-title"><i class="fa fa-diamond"></i> Data to be seeded</span>
			<table class="ef-grid">
				<thead><tr>
					<th>Module</th><th>Records</th><th>Description</th>
				</tr></thead>
				<tbody>
					<tr><td><strong>Warehouses</strong></td><td>3</td><td>Main Showroom, Vault, Workshop</td></tr>
					<tr><td><strong>Inventory Items</strong></td><td>15</td><td>Gold rings, necklaces, bangles (22K/18K/24K), silver, diamonds, pearls, platinum</td></tr>
					<tr><td><strong>Suppliers</strong></td><td>5</td><td>Dubai Gold Souk, Rajesh Gems, Antwerp Diamond Exchange, PAMP SA, Mikimoto</td></tr>
					<tr><td><strong>Customers</strong></td><td>5</td><td>With UAE phone numbers and emails</td></tr>
					<tr><td><strong>Purchase Orders</strong></td><td>5</td><td>With jewellery weight + rate details (22K, 24K, diamonds, pearls)</td></tr>
					<tr><td><strong>Sales Orders</strong></td><td>5</td><td>With weight sold + making charges + stone values</td></tr>
					<tr><td><strong>Repair Jobs</strong></td><td>3</td><td>Ring resize, chain repair, stone setting</td></tr>
					<tr><td><strong>GL Entries</strong></td><td>7</td><td>Opening balances for dual trial balance (weight + value)</td></tr>
				</tbody>
				<tfoot>
					<tr><td colspan="2"><strong>Total</strong></td><td>48 records across 8 modules</td></tr>
				</tfoot>
			</table>
		</div>

		<div class="ef-section" style="margin-top:10px">
			<span class="ef-section-title"><i class="fa fa-info-circle"></i> Notes</span>
			<div style="padding:6px 0;font-size:12px;color:#4a6a7a">
				Existing data is preserved — only new records are added (INSERT IGNORE). This also sets the industry profile to <strong>jewellery</strong> so dashboard KPIs and conditional fields activate across all ERP modules.
			</div>
		</div>

		<div class="ef-actions">
			<form id="jw_seed_form" style="display:inline">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<button type="submit" class="btn btn-warning btn-sm"><i class="fa fa-magic"></i> Seed sample data now</button>
			</form>
		</div>
		<div id="jw_seed_result" style="display:none;margin-top:10px;"></div>
	</div>
	<div class="ef-status">
		<span>Mode:=SETUP</span>
		<span>Industry: Jewellery</span>
	</div>
</div>
<script>
document.getElementById('jw_seed_form').addEventListener('submit', function(e){
	e.preventDefault();
	var btn = this.querySelector('button');
	btn.disabled = true;
	btn.innerHTML = '<i class="fa fa-spinner fa-spin"></i> Seeding...';
	var fd = new FormData(this);
	fd.append('action', 'jw_seed_sample_data');
	fetch(<?php echo json_encode($erpAjaxEndpoint); ?>, {method:'POST', body:fd, credentials:'same-origin'})
		.then(function(r){return r.json()})
		.then(function(j){
			var out = document.getElementById('jw_seed_result');
			out.style.display = 'block';
			btn.disabled = false;
			btn.innerHTML = '<i class="fa fa-magic"></i> Seed sample data now';
			if(j.status){
				var d = j.seeded || {};
				var errHtml = '';
				if (d.errors && d.errors.length > 0) {
					errHtml = '<br><div style="margin-top:6px;color:#c62828;font-size:11px"><strong>Errors:</strong><ul style="margin:2px 0 0 16px">';
					d.errors.forEach(function(e){ errHtml += '<li>' + e + '</li>'; });
					errHtml += '</ul></div>';
				}
				out.innerHTML = '<div style="background:#e8f5e9;border:1px solid #a5d6a7;padding:10px 14px;border-radius:3px;font-size:12px">'
					+ '<strong><i class="fa fa-check-circle" style="color:#2e7d32"></i> Sample data seeded successfully!</strong><br>'
					+ 'Warehouses: <strong>' + (d.warehouses||0) + '</strong> | '
					+ 'Items: <strong>' + (d.items||0) + '</strong> | '
					+ 'Suppliers: <strong>' + (d.suppliers||0) + '</strong> | '
					+ 'Customers: <strong>' + (d.customers||0) + '</strong> | '
					+ 'Purchases: <strong>' + (d.purchases||0) + '</strong> | '
					+ 'Sales: <strong>' + (d.sales||0) + '</strong> | '
					+ 'Repairs: <strong>' + (d.repairs||0) + '</strong> | '
					+ 'GL: <strong>' + (d.gl_entries||0) + '</strong>'
					+ errHtml
					+ '<br><a href="<?php echo epc_erp_h(epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)); ?>" style="color:#1565c0;font-weight:600">Go to Dashboard &rarr;</a>'
					+ '</div>';
			} else {
				out.innerHTML = '<div style="background:#ffebee;border:1px solid #ef9a9a;padding:10px 14px;border-radius:3px;font-size:12px;color:#c62828">'
					+ '<strong><i class="fa fa-exclamation-circle"></i> Error:</strong> ' + (j.message||'Failed to seed data')
					+ '</div>';
			}
		});
});
</script>
