<?php
/**
 * CP — Worldwide marketplace channels hub.
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

if ($_SERVER['REQUEST_METHOD'] === 'POST' && !empty($_POST['action'])) {
	if (!headers_sent()) {
		header('Content-Type: application/json; charset=utf-8');
	}
	require __DIR__ . '/ajax_channels.php';
	die();
}

$epcChSeedNote = '';
try {
	epc_channel_seed_defaults($db_link);
} catch (Throwable $e) {
	$epcChSeedNote = 'Partner sync skipped: ' . $e->getMessage();
}

try {
	$dash = epc_channel_dashboard($db_link);
	$channels = epc_channel_list_marketplaces($db_link);
	$orders = $db_link->query(
		'SELECT mo.*, c.`code` AS channel_code, c.`name` AS channel_name
		FROM `epc_marketplace_orders` mo
		INNER JOIN `epc_marketplace_channels` c ON c.`id` = mo.`channel_id`
		ORDER BY mo.`id` DESC LIMIT 30'
	)->fetchAll(PDO::FETCH_ASSOC);
	$skus = $db_link->query(
		'SELECT m.*, c.`code` AS channel_code FROM `epc_marketplace_sku_map` m
		INNER JOIN `epc_marketplace_channels` c ON c.`id` = m.`channel_id`
		ORDER BY m.`id` DESC LIMIT 30'
	)->fetchAll(PDO::FETCH_ASSOC);
	$logs = $db_link->query(
		"SELECT * FROM `epc_channel_sync_log`
		 WHERE `kind` IN ('inventory_sync','order_import','seed','channel')
		    OR `channel_code` = 'system'
		 ORDER BY `id` DESC LIMIT 16"
	)->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	echo '<div class="alert alert-danger">Channels data unavailable: ' . htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8') . '</div>';
	return;
}

$catalog = epc_channel_marketplaces_catalog();
$byCode = array();
foreach ($channels as $ch) {
	$byCode[(string)$ch['code']] = $ch;
}

extract(epc_channel_configure_urls());
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';
$ajaxUrl = '/' . $DP_Config->backend_dir . '/content/shop/channels/ajax_channels.php';

$regions = array();
$families = array();
foreach ($catalog as $meta) {
	$r = isset($meta['region']) ? (string)$meta['region'] : 'Global';
	$f = isset($meta['family']) ? (string)$meta['family'] : 'Other';
	$regions[$r] = isset($regions[$r]) ? $regions[$r] + 1 : 1;
	$families[$f] = isset($families[$f]) ? $families[$f] + 1 : 1;
}

$activeCount = 0;
foreach ($byCode as $ch) {
	if ((int)$ch['active'] === 1) {
		$activeCount++;
	}
}

$epcChCssVer = @filemtime($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_channels_hub.css') ?: time();
echo '<link rel="stylesheet" href="/content/general_pages/epc_channels_hub.css?v=' . rawurlencode((string)$epcChCssVer) . '">';
echo '<div class="col-lg-12 epc-ch-hub">';
?>
<div class="epc-ch">
	<?php if ($epcChSeedNote !== '') { ?>
		<div class="alert alert-warning"><?php echo epc_channel_h($epcChSeedNote); ?></div>
	<?php } ?>

	<header class="epc-ch-brandbar">
		<div class="epc-ch-brandbar__mark" aria-hidden="true"><i class="fa fa-plug"></i></div>
		<div>
			<div class="epc-ch-brandbar__name">Channels</div>
			<div class="epc-ch-brandbar__sub">Plug-and-play marketplace partners — Amazon &amp; eBay worldwide, noon, Flipkart, Walmart, Mercado Libre, and more. Sync stock, map SKUs, import orders.</div>
		</div>
		<div class="epc-ch-brandbar__actions">
			<a class="epc-ch-chip-link" href="<?php echo epc_channel_h($guideUrl); ?>"><i class="fa fa-book"></i> Guide</a>
			<a class="epc-ch-chip-link" href="<?php echo epc_channel_h($logisticsGuideUrl); ?>"><i class="fa fa-truck"></i> Logistics</a>
			<a class="epc-ch-chip-link" href="<?php echo epc_channel_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
		</div>
	</header>

	<div class="epc-ch-kpi" role="group" aria-label="Channel metrics">
		<div class="epc-ch-kpi__item">
			<div class="epc-ch-kpi__icon"><i class="fa fa-globe"></i></div>
			<div><div class="epc-ch-kpi__val"><?php echo (int)$dash['catalog_count']; ?></div><div class="epc-ch-kpi__label">Partners</div></div>
		</div>
		<div class="epc-ch-kpi__item">
			<div class="epc-ch-kpi__icon epc-ch-kpi__icon--green"><i class="fa fa-check-circle"></i></div>
			<div><div class="epc-ch-kpi__val"><?php echo (int)$activeCount; ?></div><div class="epc-ch-kpi__label">Active</div></div>
		</div>
		<div class="epc-ch-kpi__item">
			<div class="epc-ch-kpi__icon epc-ch-kpi__icon--amber"><i class="fa fa-map"></i></div>
			<div><div class="epc-ch-kpi__val"><?php echo (int)$dash['regions']; ?></div><div class="epc-ch-kpi__label">Regions</div></div>
		</div>
		<div class="epc-ch-kpi__item">
			<div class="epc-ch-kpi__icon"><i class="fa fa-barcode"></i></div>
			<div><div class="epc-ch-kpi__val"><?php echo (int)$dash['sku_mapped']; ?></div><div class="epc-ch-kpi__label">SKU maps</div></div>
		</div>
		<div class="epc-ch-kpi__item">
			<div class="epc-ch-kpi__icon epc-ch-kpi__icon--slate"><i class="fa fa-inbox"></i></div>
			<div><div class="epc-ch-kpi__val"><?php echo (int)$dash['marketplace_orders']; ?></div><div class="epc-ch-kpi__label">MP orders</div></div>
		</div>
	</div>

	<div class="epc-ch-toolbar">
		<button type="button" class="btn btn-success btn-sm" id="epc_ch_seed_channels"><i class="fa fa-refresh"></i> Sync worldwide partners</button>
		<button type="button" class="btn btn-default btn-sm" id="epc_ch_seed_sample"><i class="fa fa-database"></i> Load sample data</button>
		<a class="btn btn-default btn-sm" href="<?php echo epc_channel_h($demoJsonUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> JSON report</a>
		<span class="text-muted" style="font-size:12px;font-weight:600;">Demo sync until live SP-API / Sell API / Partner keys are connected</span>
	</div>
	<div id="epc_ch_msg" class="alert epc-ch-msg" style="display:none;"></div>

	<div class="epc-ch-filters" role="toolbar" aria-label="Filter marketplaces">
		<button type="button" class="epc-ch-filter is-active" data-filter="all">All (<?php echo count($catalog); ?>)</button>
		<?php foreach ($families as $familyName => $familyCount): ?>
			<button type="button" class="epc-ch-filter" data-filter="<?php echo epc_channel_h($familyName); ?>"><?php echo epc_channel_h($familyName); ?> (<?php echo (int)$familyCount; ?>)</button>
		<?php endforeach; ?>
		<?php foreach ($regions as $regionName => $regionCount): ?>
			<button type="button" class="epc-ch-filter" data-filter="<?php echo epc_channel_h($regionName); ?>"><?php echo epc_channel_h($regionName); ?> (<?php echo (int)$regionCount; ?>)</button>
		<?php endforeach; ?>
	</div>

	<div class="epc-ch-partners" id="epc_ch_partners">
		<?php foreach ($catalog as $code => $meta):
			$acc = isset($byCode[$code]) ? $byCode[$code] : null;
			$active = $acc ? ((int)$acc['active'] === 1) : false;
			$demo = $acc ? ((int)$acc['demo_mode'] === 1) : true;
			$accent = isset($meta['accent']) ? (string)$meta['accent'] : '#2563eb';
			$icon = isset($meta['icon']) ? (string)$meta['icon'] : 'fa-plug';
			$region = isset($meta['region']) ? (string)$meta['region'] : 'Global';
			$family = isset($meta['family']) ? (string)$meta['family'] : 'Other';
			$api = isset($meta['api']) ? (string)$meta['api'] : '';
			$lastSync = ($acc && (int)$acc['last_sync_at'] > 0) ? date('Y-m-d H:i', (int)$acc['last_sync_at']) : '—';
			$markFg = (strtolower($accent) === '#feee00' || strtolower($accent) === '#fff159') ? '#111' : '#fff';
			?>
			<article class="epc-ch-partner<?php echo $active ? '' : ' is-off'; ?>"
				data-region="<?php echo epc_channel_h($region); ?>"
				data-family="<?php echo epc_channel_h($family); ?>"
				data-code="<?php echo epc_channel_h($code); ?>">
				<div class="epc-ch-partner__stripe" style="background:<?php echo epc_channel_h($accent); ?>"></div>
				<div class="epc-ch-partner__body">
					<div class="epc-ch-partner__top">
						<div class="epc-ch-partner__mark" style="background:<?php echo epc_channel_h($accent); ?>;color:<?php echo epc_channel_h($markFg); ?>">
							<i class="fa <?php echo epc_channel_h($icon); ?>"></i>
						</div>
						<div>
							<h4 class="epc-ch-partner__name"><?php echo epc_channel_h($meta['name'] ?? strtoupper($code)); ?></h4>
							<div class="epc-ch-partner__code"><code><?php echo epc_channel_h($code); ?></code></div>
						</div>
					</div>
					<p class="epc-ch-partner__blurb"><?php echo epc_channel_h($meta['blurb'] ?? ''); ?></p>
					<div class="epc-ch-partner__meta">
						<span class="epc-ch-badge"><?php echo epc_channel_h($family); ?></span>
						<span class="epc-ch-badge"><?php echo epc_channel_h($region); ?></span>
						<?php if ($active): ?>
							<span class="epc-ch-badge epc-ch-badge--ok"><i class="fa fa-check"></i> Active</span>
						<?php else: ?>
							<span class="epc-ch-badge epc-ch-badge--off">Off</span>
						<?php endif; ?>
						<span class="epc-ch-badge<?php echo $demo ? ' epc-ch-badge--warn' : ' epc-ch-badge--ok'; ?>"><?php echo $demo ? 'Demo' : 'Live'; ?></span>
					</div>
					<div class="epc-ch-partner__api"><?php echo epc_channel_h($api); ?> · last sync <?php echo epc_channel_h($lastSync); ?></div>
					<div class="epc-ch-partner__foot">
						<button type="button" class="btn btn-xs btn-default" data-toggle-channel="<?php echo epc_channel_h($code); ?>">
							<i class="fa <?php echo $active ? 'fa-toggle-on' : 'fa-toggle-off'; ?>"></i> <?php echo $active ? 'Disable' : 'Enable'; ?>
						</button>
						<?php if ($active): ?>
							<button type="button" class="btn btn-xs btn-primary" data-sync-channel="<?php echo epc_channel_h($code); ?>">
								<i class="fa fa-refresh"></i> Demo sync
							</button>
						<?php endif; ?>
					</div>
				</div>
			</article>
		<?php endforeach; ?>
	</div>

	<section class="epc-ch-section">
		<div class="epc-ch-section__head">
			<h3><i class="fa fa-link"></i> SKU map</h3>
			<span>Sample mappings across Amazon, eBay, noon</span>
		</div>
		<?php if (empty($skus)): ?>
			<div class="epc-ch-empty">No SKUs yet — click <strong>Load sample data</strong>.</div>
		<?php else: ?>
			<table class="epc-ch-table">
				<thead><tr><th>Channel</th><th>Brand</th><th>Article</th><th>External SKU</th><th>ASIN</th><th>Price</th><th>Stock</th></tr></thead>
				<tbody>
				<?php foreach ($skus as $s): ?>
					<tr>
						<td><code><?php echo epc_channel_h($s['channel_code']); ?></code></td>
						<td><?php echo epc_channel_h($s['manufacturer']); ?></td>
						<td><?php echo epc_channel_h($s['article']); ?></td>
						<td><?php echo epc_channel_h($s['external_sku']); ?></td>
						<td><?php echo epc_channel_h($s['external_asin'] ?: '—'); ?></td>
						<td><?php echo epc_channel_money($s['price']); ?></td>
						<td><?php echo (int)$s['stock_qty']; ?></td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		<?php endif; ?>
	</section>

	<section class="epc-ch-section">
		<div class="epc-ch-section__head">
			<h3><i class="fa fa-inbox"></i> Marketplace orders</h3>
			<span><?php echo (int)$dash['marketplace_pending']; ?> awaiting ship / import</span>
		</div>
		<?php if (empty($orders)): ?>
			<div class="epc-ch-empty">No marketplace orders — click <strong>Load sample data</strong>.</div>
		<?php else: ?>
			<table class="epc-ch-table">
				<thead><tr><th>Channel</th><th>External ID</th><th>Customer</th><th>Ship to</th><th>Total</th><th>Status</th><th></th></tr></thead>
				<tbody>
				<?php foreach ($orders as $o):
					$st = strtolower((string)($o['status'] ?? ''));
					?>
					<tr>
						<td><code><?php echo epc_channel_h($o['channel_code']); ?></code></td>
						<td><?php echo epc_channel_h($o['external_order_id']); ?></td>
						<td><?php echo epc_channel_h($o['customer_name']); ?></td>
						<td><?php echo epc_channel_h($o['ship_city'] . ', ' . $o['ship_country']); ?></td>
						<td><?php echo epc_channel_money($o['total_amount']); ?> <?php echo epc_channel_h($o['currency']); ?></td>
						<td><span class="epc-ch-status epc-ch-status--<?php echo epc_channel_h($st); ?>"><?php echo epc_channel_h($o['status']); ?></span></td>
						<td>
							<?php if ($st !== 'imported'): ?>
								<button type="button" class="btn btn-xs btn-default" data-import-order="<?php echo (int)$o['id']; ?>">Demo import</button>
							<?php else: ?>
								<span class="text-muted">Imported</span>
							<?php endif; ?>
						</td>
					</tr>
				<?php endforeach; ?>
				</tbody>
			</table>
		<?php endif; ?>
	</section>

	<section class="epc-ch-section">
		<div class="epc-ch-section__head">
			<h3><i class="fa fa-history"></i> Activity log</h3>
			<span>Seeds, toggles, inventory sync, order import</span>
		</div>
		<?php if (empty($logs)): ?>
			<div class="epc-ch-empty">No log entries yet.</div>
		<?php else: ?>
			<ul class="epc-ch-log">
				<?php foreach ($logs as $lg): ?>
					<li>
						<time><?php echo epc_channel_h(date('Y-m-d H:i', (int)$lg['time_created'])); ?></time>
						<span><?php echo epc_channel_h($lg['message']); ?></span>
					</li>
				<?php endforeach; ?>
			</ul>
		<?php endif; ?>
	</section>
</div>
</div>
<script>
window.EPC_CH = {
	ajaxUrl: <?php echo json_encode($ajaxUrl); ?>,
	url: <?php echo json_encode($channelsUrl); ?>,
	csrf: <?php echo json_encode($csrf); ?>
};
</script>
<?php
$epcChJsVer = @filemtime(__DIR__ . '/channels_hub.js') ?: time();
echo '<script src="/' . epc_channel_h($DP_Config->backend_dir) . '/content/shop/channels/channels_hub.js?v=' . rawurlencode((string)$epcChJsVer) . '"></script>';
?>
