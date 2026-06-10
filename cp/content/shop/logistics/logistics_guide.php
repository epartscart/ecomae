<?php
/**
 * Logistics — step-by-step guide (delivery, carriers, normal orders).
 * URL: /cp/shop/logistics/guide
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/logistics/epc_logistics_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_site_context.php';
$epc_logistics_site_host = epc_site_host() !== '' ? epc_site_host() : parse_url(epc_site_domain(), PHP_URL_HOST);

if (!isset($db_link) || !($db_link instanceof PDO)) {
	try {
		$db_link = new PDO(
			'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
			$DP_Config->user,
			$DP_Config->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$db_link->query('SET NAMES utf8;');
	} catch (Exception $e) {
		echo '<div class="alert alert-danger">Database connection failed.</div>';
		return;
	}
}

extract(epc_logistics_configure_urls());
$snapshot = epc_logistics_guide_snapshot($db_link);
$dash = $snapshot['dashboard'];
$catalog = epc_channel_carriers_catalog();
$sampleShipments = isset($snapshot['shipments']) ? $snapshot['shipments'] : array();
$channelsGuideUrl = '/' . $DP_Config->backend_dir . '/shop/channels/guide';
?>

<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260525">
<style>
.epc-log-guide-intro { background: linear-gradient(135deg, #0f766e 0%, #0891b2 100%); color: #fff; border-radius: 8px; padding: 20px 22px; margin-bottom: 18px; }
.epc-log-guide-intro h3 { margin: 0 0 8px; color: #fff; }
.epc-log-guide-step { border-left: 4px solid #0f766e; padding: 12px 16px; margin: 14px 0; background: #f8fafc; border-radius: 0 6px 6px 0; }
.epc-log-guide-step h5 { margin: 0 0 8px; font-weight: 700; color: #0f172a; }
.epc-log-flow { font-size: 13px; line-height: 1.7; }
.epc-log-sample { background: #ecfdf5; border: 1px solid #6ee7b7; border-radius: 6px; padding: 10px 14px; margin: 10px 0; font-size: 13px; }
</style>

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			Logistics — step-by-step guide
			<span class="pull-right">
				<a class="btn btn-primary btn-xs" href="<?php echo epc_logistics_h($logisticsUrl); ?>"><i class="fa fa-th-large"></i> Logistics hub</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_logistics_h($carriersUrl); ?>"><i class="fa fa-truck"></i> Carriers</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_logistics_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
			</span>
		</div>
		<div class="panel-body">

			<div class="epc-log-guide-intro">
				<h3><i class="fa fa-book"></i> Delivery &amp; fulfilment for all orders</h3>
				<p style="margin:0;opacity:.92;">
					Logistics covers <strong>warehouses, stock, delivery methods</strong>, and
					<strong>international carriers</strong> (DHL, FedEx, Aramex, UPS) for
					<strong>normal storefront orders</strong> and imported marketplace orders alike.
				</p>
			</div>

			<div class="alert alert-warning">
				<strong>Channels are separate.</strong> Amazon/eBay listing sync and order import live under
				<a href="<?php echo epc_logistics_h($channelsGuideUrl); ?>">Channels guide</a> — not here.
			</div>

			<div class="alert alert-info">
				<strong>Menu:</strong> <em>Logistics</em> group in CP sidebar.
				Obtaining mode: <code>epc_carriers</code><?php if (!empty($snapshot['obtaining_mode_id'])): ?> (ID <?php echo (int)$snapshot['obtaining_mode_id']; ?>)<?php endif; ?>.
				Snapshot <?php echo epc_logistics_h($snapshot['generated_at']); ?>.
				<a href="<?php echo epc_logistics_h($demoJsonUrl); ?>" target="_blank">JSON report</a>.
			</div>

			<h4><i class="fa fa-bar-chart"></i> Live snapshot</h4>
			<table class="table table-striped table-bordered">
				<tbody>
					<tr><td>Carrier accounts</td><td><?php echo (int)$dash['carriers']; ?> (DHL, FedEx, Aramex, UPS)</td></tr>
					<tr><td>Shipments</td><td><?php echo (int)$dash['shipments_shipped']; ?> shipped / <?php echo (int)$dash['shipments']; ?> total</td></tr>
					<tr><td>Shop orders (all sources)</td><td><?php echo (int)$dash['shop_orders']; ?></td></tr>
				</tbody>
			</table>

			<?php if (!empty($sampleShipments)): ?>
			<div class="epc-log-sample">
				<strong>Sample shipment:</strong>
				<?php $sh = $sampleShipments[0]; ?>
				Order #<?php echo (int)$sh['order_id']; ?> —
				<?php echo epc_logistics_h(strtoupper($sh['carrier_code'])); ?>
				tracking <code><?php echo epc_logistics_h($sh['tracking_number']); ?></code>,
				<?php echo epc_logistics_money($sh['cost']); ?> <?php echo epc_logistics_h($sh['currency']); ?>
			</div>
			<?php endif; ?>

			<h4><i class="fa fa-sitemap"></i> End-to-end flows</h4>
			<div class="well well-sm epc-log-flow">
				<p><strong>A. Storefront customer order (normal checkout)</strong></p>
				<ol>
					<li>Customer adds parts to cart and checks out on <?php echo htmlspecialchars($epc_logistics_site_host ?: 'your storefront', ENT_QUOTES, 'UTF-8'); ?>.</li>
					<li>Selects delivery method — pickup, local courier, or <strong>DHL / FedEx / Aramex / UPS</strong>.</li>
					<li>For carriers: enters city, country, address, weight — sees demo or live rates.</li>
					<li>Order appears in CP <a href="<?php echo epc_logistics_h($ordersUrl); ?>">Orders</a> like any other sale.</li>
				</ol>
				<p><strong>B. Create shipping label (CP order card)</strong></p>
				<ol>
					<li>Open a paid order with <code>epc_carriers</code> delivery (or any order needing a label).</li>
					<li>In the obtaining-mode block: pick carrier, weight → <strong>Create demo label</strong>.</li>
					<li>Tracking and cost save to <code>epc_carrier_shipments</code>; listed on <a href="<?php echo epc_logistics_h($carriersUrl); ?>">Carriers hub</a>.</li>
				</ol>
				<p><strong>C. Warehouses &amp; stock</strong></p>
				<ol>
					<li><a href="<?php echo epc_logistics_h($logisticsUrl); ?>">Logistics hub</a> → warehouses, stock, pickup points.</li>
					<li>Allocate inventory before dispatch; ERP sees fulfilment when order completes.</li>
				</ol>
			</div>

			<h4><i class="fa fa-list-ol"></i> Step-by-step</h4>

			<div class="epc-log-guide-step">
				<h5>Step 1 — First-time setup</h5>
				<ol class="epc-log-flow" style="margin-bottom:0;">
					<li>Run: <code><?php echo epc_logistics_h($setupUrl); ?></code></li>
					<li>With sample: append <code>&amp;sample=1</code></li>
					<li>Registers logistics menu, carriers page, guide, and <code>epc_carriers</code> delivery method.</li>
				</ol>
			</div>

			<div class="epc-log-guide-step">
				<h5>Step 2 — Logistics hub &amp; warehouses</h5>
				<ol class="epc-log-flow" style="margin-bottom:0;">
					<li>Open <a href="<?php echo epc_logistics_h($logisticsUrl); ?>"><?php echo epc_logistics_h($logisticsUrl); ?></a></li>
					<li>Configure warehouses, pickup points, stock, and local delivery plugins (SDEK, DPD, etc.).</li>
				</ol>
			</div>

			<div class="epc-log-guide-step">
				<h5>Step 3 — Enable carrier delivery at checkout</h5>
				<ol class="epc-log-flow" style="margin-bottom:0;">
					<li>CP → <a href="<?php echo epc_logistics_h($obtainModesUrl); ?>">Delivery methods</a>.</li>
					<li>Ensure <strong>DHL / FedEx / Aramex / UPS</strong> (<code>epc_carriers</code>) is <strong>available</strong>.</li>
					<li>Parameters: <em>Demo mode</em>, <em>Origin city</em> (default Dubai).</li>
				</ol>
			</div>

			<div class="epc-log-guide-step">
				<h5>Step 4 — Carriers hub</h5>
				<ol class="epc-log-flow" style="margin-bottom:0;">
					<li>URL: <a href="<?php echo epc_logistics_h($carriersUrl); ?>"><?php echo epc_logistics_h($carriersUrl); ?></a></li>
					<li>Review carrier accounts, recent shipments, activity log.</li>
					<li><strong>Load sample shipment</strong> if tables are empty.</li>
				</ol>
			</div>

			<div class="epc-log-guide-step">
				<h5>Step 5 — Label from order card</h5>
				<ol class="epc-log-flow" style="margin-bottom:0;">
					<li><a href="<?php echo epc_logistics_h($ordersUrl); ?>">Orders</a> → open paid order.</li>
					<li>Carrier block → select DHL/FedEx/Aramex/UPS, weight (kg) → submit.</li>
					<li>Demo tracking format e.g. <code>DHL260518000018123</code>; live APIs when credentials configured.</li>
				</ol>
			</div>

			<div class="epc-log-guide-step">
				<h5>Step 6 — Carriers reference</h5>
				<table class="table table-condensed table-bordered epc-log-flow" style="margin-bottom:0;">
					<thead><tr><th>Code</th><th>Name</th><th>Demo services</th><th>Track URL</th></tr></thead>
					<tbody>
					<?php foreach ($catalog as $code => $c): ?>
						<tr>
							<td><code><?php echo epc_logistics_h($code); ?></code></td>
							<td><?php echo epc_logistics_h($c['name']); ?></td>
							<td><?php echo epc_logistics_h(implode(', ', array_values($c['services']))); ?></td>
							<td><small><?php echo epc_logistics_h($c['track_url']); ?></small></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			</div>

			<div class="epc-log-guide-step">
				<h5>Step 7 — Go live checklist</h5>
				<ol class="epc-log-flow" style="margin-bottom:0;">
					<li>DHL MyDHL, FedEx Ship, Aramex, UPS OAuth — store credentials in carrier accounts.</li>
					<li>Turn off <em>Demo mode</em> on obtaining mode when live rating/labels work.</li>
					<li>Test one storefront order + one label before production traffic.</li>
				</ol>
			</div>

			<h4><i class="fa fa-database"></i> Database tables</h4>
			<dl class="epc-log-flow">
				<dt><code>epc_carrier_accounts</code></dt><dd>DHL, FedEx, Aramex, UPS credentials and demo_mode.</dd>
				<dt><code>epc_carrier_shipments</code></dt><dd>Labels for any shop order: tracking, cost, status.</dd>
				<dt><code>shop_obtaining_modes</code></dt><dd>Delivery methods including <code>epc_carriers</code>.</dd>
				<dt><code>shop_orders</code></dt><dd>All customer orders — website, phone, or imported from channels.</dd>
			</dl>

			<h4><i class="fa fa-question-circle"></i> FAQ</h4>
			<dl class="epc-log-flow">
				<dt>Does logistics work for normal website orders?</dt>
				<dd>Yes. Any checkout order can use carrier delivery or local methods configured under Logistics.</dd>
				<dt>Where are Amazon/eBay orders?</dt>
				<dd>Import via <a href="<?php echo epc_logistics_h($channelsGuideUrl); ?>">Channels</a>; after import they are regular shop orders fulfilled here.</dd>
				<dt>Where do checkout rates come from?</dt>
				<dd>Demo: <code>epc_channel_demo_rate()</code> formula. Live: carrier APIs when configured.</dd>
			</dl>

			<p class="text-muted m-t-md">
				Guide: <a href="<?php echo epc_logistics_h($guideUrl); ?>"><?php echo epc_logistics_h($guideUrl); ?></a>
				· Carriers: <a href="<?php echo epc_logistics_h($carriersUrl); ?>"><?php echo epc_logistics_h($carriersUrl); ?></a>
			</p>
		</div>
	</div>
</div>
