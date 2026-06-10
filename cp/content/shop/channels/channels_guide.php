<?php
/**
 * Channels — step-by-step guide (Amazon, eBay marketplaces only).
 * URL: /cp/shop/channels/guide
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/channels/epc_channel_helpers.php';

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

extract(epc_channel_configure_urls());
$snapshot = epc_channel_guide_snapshot($db_link);
$dash = $snapshot['dashboard'];
$sampleOrders = isset($snapshot['marketplace_orders']) ? $snapshot['marketplace_orders'] : array();
$logisticsGuideUrl = $logisticsGuideUrl ?? ('/' . $DP_Config->backend_dir . '/shop/logistics/guide');
?>

<link rel="stylesheet" href="/content/shop/finance/epc_erp_ui.css?v=20260525">
<style>
.epc-ch-guide-intro { background: linear-gradient(135deg, #0f172a 0%, #1e3a8a 100%); color: #fff; border-radius: 8px; padding: 20px 22px; margin-bottom: 18px; }
.epc-ch-guide-intro h3 { margin: 0 0 8px; color: #fff; }
.epc-ch-guide-step { border-left: 4px solid #2563eb; padding: 12px 16px; margin: 14px 0; background: #f8fafc; border-radius: 0 6px 6px 0; }
.epc-ch-guide-step h5 { margin: 0 0 8px; font-weight: 700; color: #0f172a; }
.epc-ch-flow { font-size: 13px; line-height: 1.7; }
.epc-ch-sample { background: #fffbeb; border: 1px solid #fcd34d; border-radius: 6px; padding: 10px 14px; margin: 10px 0; font-size: 13px; }
</style>

<div class="col-lg-12 epc-erp-shell">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			Channels — step-by-step guide
			<span class="pull-right">
				<a class="btn btn-primary btn-xs" href="<?php echo epc_channel_h($channelsUrl); ?>"><i class="fa fa-plug"></i> Channels hub</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_channel_h($logisticsGuideUrl); ?>"><i class="fa fa-truck"></i> Logistics guide</a>
				<a class="btn btn-default btn-xs" href="<?php echo epc_channel_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
			</span>
		</div>
		<div class="panel-body">

			<div class="epc-ch-guide-intro">
				<h3><i class="fa fa-book"></i> Amazon &amp; eBay marketplace integration</h3>
				<p style="margin:0;opacity:.92;">
					SKU mapping, inventory sync, and order import from external marketplaces.
					Hub: <a href="<?php echo epc_channel_h($channelsUrl); ?>" style="color:#bfdbfe;"><?php echo epc_channel_h($channelsUrl); ?></a>
				</p>
			</div>

			<div class="alert alert-warning">
				<strong>Logistics is separate.</strong> Delivery methods, carriers (DHL/FedEx), warehouses, and fulfilment for
				<strong>all customer orders</strong> are documented in the
				<a href="<?php echo epc_channel_h($logisticsGuideUrl); ?>">Logistics guide</a>.
			</div>

			<div class="alert alert-info">
				<strong>Menu:</strong> <em>Channels</em> group in CP sidebar.
				Snapshot <?php echo epc_channel_h($snapshot['generated_at']); ?>.
				<a href="<?php echo epc_channel_h($demoJsonUrl); ?>" target="_blank">JSON report</a>.
			</div>

			<h4><i class="fa fa-bar-chart"></i> Live snapshot</h4>
			<table class="table table-striped table-bordered">
				<tbody>
					<tr><td>Marketplace orders</td><td><strong><?php echo (int)$dash['marketplace_orders']; ?></strong> (<?php echo (int)$dash['marketplace_pending']; ?> awaiting ship/import)</td></tr>
					<tr><td>SKU mappings (active)</td><td><?php echo (int)$dash['sku_mapped']; ?></td></tr>
					<tr><td>Marketplace channels</td><td><?php echo count($snapshot['channels']); ?> (Amazon, eBay)</td></tr>
				</tbody>
			</table>

			<?php if (!empty($sampleOrders)): ?>
			<div class="epc-ch-sample">
				<strong>Sample marketplace orders:</strong>
				<ul style="margin:6px 0 0;">
				<?php foreach (array_slice($sampleOrders, 0, 3) as $o): ?>
					<li>
						<?php echo epc_channel_h($o['channel_code']); ?> —
						<code><?php echo epc_channel_h($o['external_order_id']); ?></code>,
						<?php echo epc_channel_h($o['customer_name']); ?>,
						<?php echo epc_channel_money($o['total_amount']); ?> <?php echo epc_channel_h($o['currency']); ?>
					</li>
				<?php endforeach; ?>
				</ul>
			</div>
			<?php endif; ?>

			<h4><i class="fa fa-sitemap"></i> Marketplace flow</h4>
			<div class="well well-sm epc-ch-flow">
				<ol>
					<li>Map shop SKUs (brand + article) to marketplace SKUs/ASINs.</li>
					<li>Demo sync pushes stock &amp; price to Amazon/eBay (live: SP-API / Sell API).</li>
					<li>Marketplace orders appear in the hub; <strong>Demo import</strong> links to shop orders.</li>
					<li>After import, fulfil via standard CP Orders + <a href="<?php echo epc_channel_h($logisticsGuideUrl); ?>">Logistics</a>.</li>
				</ol>
			</div>

			<h4><i class="fa fa-list-ol"></i> Step-by-step</h4>

			<div class="epc-ch-guide-step">
				<h5>Step 1 — Setup</h5>
				<ol class="epc-ch-flow" style="margin-bottom:0;">
					<li>Run: <code><?php echo epc_channel_h($setupUrl); ?></code> (add <code>&amp;sample=1</code> for demo data)</li>
					<li>Log in to CP → <a href="<?php echo epc_channel_h($channelsUrl); ?>">Channels hub</a></li>
				</ol>
			</div>

			<div class="epc-ch-guide-step">
				<h5>Step 2 — SKU mapping</h5>
				<ol class="epc-ch-flow" style="margin-bottom:0;">
					<li>Link manufacturer + article to external SKU / ASIN per channel.</li>
					<li>Sync pushes <code>stock_qty</code> and <code>price</code> when live credentials are set.</li>
				</ol>
			</div>

			<div class="epc-ch-guide-step">
				<h5>Step 3 — Inventory sync</h5>
				<ol class="epc-ch-flow" style="margin-bottom:0;">
					<li><strong>Demo sync Amazon stock</strong> / <strong>Demo sync eBay stock</strong> on the hub.</li>
					<li>Live: SP-API (Amazon) and Sell API (eBay) with OAuth credentials.</li>
				</ol>
			</div>

			<div class="epc-ch-guide-step">
				<h5>Step 4 — Import orders</h5>
				<ol class="epc-ch-flow" style="margin-bottom:0;">
					<li>Pending orders in hub → <strong>Demo import</strong>.</li>
					<li>Process in <a href="<?php echo epc_channel_h($ordersUrl); ?>">Orders</a>; ship via Logistics.</li>
				</ol>
			</div>

			<div class="epc-ch-guide-step">
				<h5>Step 5 — Go live</h5>
				<ol class="epc-ch-flow" style="margin-bottom:0;">
					<li>Amazon SP-API + eBay OAuth; disable demo_mode on channels.</li>
					<li>Test one SKU sync and one order import on staging.</li>
				</ol>
			</div>

			<h4><i class="fa fa-database"></i> Database tables</h4>
			<dl class="epc-ch-flow">
				<dt><code>epc_marketplace_channels</code></dt><dd>Amazon/eBay config.</dd>
				<dt><code>epc_marketplace_sku_map</code></dt><dd>Catalog ↔ marketplace SKU mapping.</dd>
				<dt><code>epc_marketplace_orders</code></dt><dd>External orders pending import.</dd>
				<dt><code>epc_channel_sync_log</code></dt><dd>Sync and import audit trail.</dd>
			</dl>

			<p class="text-muted m-t-md">
				Guide: <a href="<?php echo epc_channel_h($guideUrl); ?>"><?php echo epc_channel_h($guideUrl); ?></a>
				· Logistics: <a href="<?php echo epc_channel_h($logisticsGuideUrl); ?>"><?php echo epc_channel_h($logisticsGuideUrl); ?></a>
			</p>
		</div>
	</div>
</div>
