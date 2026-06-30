<?php
/**
 * E-commerce Integration — connect with Shopify, Magento, WooCommerce.
 * Sync orders, inventory, customers, and products bidirectionally.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

erp_page_header(
	'<i class="fa fa-shopping-cart"></i> E-commerce Integration',
	'Connect your ERP with Shopify, Magento, or WooCommerce — sync orders, inventory, products, and customers in real-time.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'E-commerce Integration'),
	),
	array(array('label' => 'Add connection', 'url' => '#', 'class' => 'btn-primary', 'icon' => 'fa-plug'))
);

ob_start();
?>
<div class="epc-erp-section">
	<h4><i class="fa fa-plug"></i> Connected platforms</h4>
	<div class="row">
		<div class="col-md-4">
			<div class="panel panel-default" style="border-top:3px solid #96bf48;">
				<div class="panel-body text-center">
					<h4 style="color:#96bf48;"><i class="fa fa-shopping-bag"></i> Shopify</h4>
					<p class="text-muted small">Real-time order sync, inventory push, product catalog</p>
					<button class="btn btn-sm btn-default"><i class="fa fa-plug"></i> Connect store</button>
				</div>
			</div>
		</div>
		<div class="col-md-4">
			<div class="panel panel-default" style="border-top:3px solid #ee672f;">
				<div class="panel-body text-center">
					<h4 style="color:#ee672f;"><i class="fa fa-cube"></i> Magento</h4>
					<p class="text-muted small">REST API integration, multi-store support</p>
					<button class="btn btn-sm btn-default"><i class="fa fa-plug"></i> Connect store</button>
				</div>
			</div>
		</div>
		<div class="col-md-4">
			<div class="panel panel-default" style="border-top:3px solid #7b51ad;">
				<div class="panel-body text-center">
					<h4 style="color:#7b51ad;"><i class="fa fa-wordpress"></i> WooCommerce</h4>
					<p class="text-muted small">WP REST API, webhook-based sync</p>
					<button class="btn btn-sm btn-default"><i class="fa fa-plug"></i> Connect store</button>
				</div>
			</div>
		</div>
	</div>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-exchange"></i> Sync settings</h4>
	<table class="table table-bordered table-condensed" style="font-size:13px;">
		<thead><tr><th>Data type</th><th>Direction</th><th>Frequency</th><th>Last sync</th><th>Status</th></tr></thead>
		<tbody>
			<tr><td><strong>Orders</strong></td><td>E-commerce → ERP</td><td>Real-time (webhook)</td><td>2026-06-21 08:12</td><td><span class="label label-success">Active</span></td></tr>
			<tr><td><strong>Inventory levels</strong></td><td>ERP → E-commerce</td><td>Every 5 minutes</td><td>2026-06-21 08:10</td><td><span class="label label-success">Active</span></td></tr>
			<tr><td><strong>Products</strong></td><td>Bidirectional</td><td>Hourly</td><td>2026-06-21 07:00</td><td><span class="label label-success">Active</span></td></tr>
			<tr><td><strong>Customers</strong></td><td>E-commerce → ERP</td><td>On new order</td><td>2026-06-21 08:12</td><td><span class="label label-success">Active</span></td></tr>
			<tr><td><strong>Prices</strong></td><td>ERP → E-commerce</td><td>On update</td><td>2026-06-20 15:30</td><td><span class="label label-success">Active</span></td></tr>
			<tr><td><strong>Refunds/returns</strong></td><td>E-commerce → ERP</td><td>Real-time</td><td>2026-06-20 12:45</td><td><span class="label label-success">Active</span></td></tr>
		</tbody>
	</table>
</div>
<div class="epc-erp-section">
	<h4><i class="fa fa-cog"></i> Integration configuration</h4>
	<div class="pm-fields">
		<div class="pm-field"><label>Platform</label>
			<select class="form-control input-sm"><option>Shopify</option><option>Magento 2</option><option>WooCommerce</option></select>
		</div>
		<div class="pm-field"><label>Store URL</label><input type="url" class="form-control input-sm" placeholder="https://yourstore.myshopify.com"></div>
		<div class="pm-field"><label>API key</label><input type="password" class="form-control input-sm" placeholder="API key / access token"></div>
		<div class="pm-field"><label>Order auto-invoice</label>
			<select class="form-control input-sm"><option value="1">Yes — create ERP invoice on order</option><option value="0">No — manual</option></select>
		</div>
		<div class="pm-field"><label>Inventory warehouse</label>
			<select class="form-control input-sm"><option>Main warehouse</option><option>All warehouses (sum)</option></select>
		</div>
	</div>
	<button class="btn btn-primary btn-sm" style="margin-top:8px;"><i class="fa fa-save"></i> Save</button>
	<button class="btn btn-success btn-sm" style="margin-top:8px;"><i class="fa fa-check"></i> Test connection</button>
</div>
<?php
erp_section_card('E-commerce Integration', ob_get_clean(), array('icon' => 'fa-shopping-cart'));
