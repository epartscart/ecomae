<?php
/**
 * CP — Worldwide carriers & shipments (logistics hub).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/logistics/epc_logistics_helpers.php';

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
	require __DIR__ . '/ajax_logistics.php';
	exit;
}

$epcLcSeedNote = '';
try {
	// Keep account rows in sync with the worldwide catalog (idempotent).
	epc_logistics_seed_defaults($db_link);
} catch (Throwable $e) {
	$epcLcSeedNote = 'Partner sync skipped: ' . $e->getMessage();
}

try {
	$dash = epc_logistics_dashboard($db_link);
	$carriers = epc_channel_list_carriers($db_link);
	$shipments = $db_link->query(
		'SELECT s.* FROM `epc_carrier_shipments` s ORDER BY s.`id` DESC LIMIT 30'
	)->fetchAll(PDO::FETCH_ASSOC);
	$logs = $db_link->query(
		"SELECT * FROM `epc_channel_sync_log` WHERE `kind` IN ('shipment','seed','carrier') OR `channel_code` = 'logistics' ORDER BY `id` DESC LIMIT 12"
	)->fetchAll(PDO::FETCH_ASSOC);
} catch (Throwable $e) {
	echo '<div class="alert alert-danger">Logistics data unavailable: ' . htmlspecialchars($e->getMessage(), ENT_QUOTES, 'UTF-8') . '</div>';
	return;
}

$catalog = epc_channel_carriers_catalog();
$byCode = array();
foreach ($carriers as $ca) {
	$byCode[(string)$ca['code']] = $ca;
}

extract(epc_logistics_configure_urls());
$csrf = isset($user_session['csrf_guard_key']) ? (string)$user_session['csrf_guard_key'] : '';

$regions = array();
foreach ($catalog as $meta) {
	$r = isset($meta['region']) ? (string)$meta['region'] : 'Global';
	$regions[$r] = isset($regions[$r]) ? $regions[$r] + 1 : 1;
}

$activeCount = 0;
foreach ($byCode as $ca) {
	if ((int)$ca['active'] === 1) {
		$activeCount++;
	}
}

// Avoid nested BOS shell conflicts — render a self-contained col frame.
$epcLcCssVer = @filemtime($_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_logistics_carriers.css') ?: time();
echo '<link rel="stylesheet" href="/content/general_pages/epc_logistics_carriers.css?v=' . rawurlencode((string)$epcLcCssVer) . '">';
echo '<div class="col-lg-12 epc-lc-hub">';
?>
<div class="epc-lc">
	<?php if ($epcLcSeedNote !== '') { ?>
		<div class="alert alert-warning"><?php echo epc_logistics_h($epcLcSeedNote); ?></div>
	<?php } ?>
	<header class="epc-lc-brandbar">
		<div class="epc-lc-brandbar__mark" aria-hidden="true"><i class="fa fa-truck"></i></div>
		<div>
			<div class="epc-lc-brandbar__name">Carriers &amp; shipments</div>
			<div class="epc-lc-brandbar__sub">Worldwide logistics partners for checkout rates, demo labels, and order fulfilment from Dubai / UAE.</div>
		</div>
		<div class="epc-lc-brandbar__actions">
			<a class="epc-lc-chip-link" href="<?php echo epc_logistics_h($guideUrl); ?>"><i class="fa fa-book"></i> Guide</a>
			<a class="epc-lc-chip-link" href="<?php echo epc_logistics_h($logisticsUrl); ?>"><i class="fa fa-th-large"></i> Hub</a>
			<a class="epc-lc-chip-link" href="<?php echo epc_logistics_h($obtainModesUrl); ?>"><i class="fa fa-list"></i> Delivery methods</a>
			<a class="epc-lc-chip-link" href="<?php echo epc_logistics_h($ordersUrl); ?>"><i class="fa fa-shopping-cart"></i> Orders</a>
		</div>
	</header>

	<div class="epc-lc-kpi" role="group" aria-label="Logistics metrics">
		<div class="epc-lc-kpi__item">
			<div class="epc-lc-kpi__icon"><i class="fa fa-globe"></i></div>
			<div><div class="epc-lc-kpi__val"><?php echo (int)$dash['catalog_count']; ?></div><div class="epc-lc-kpi__label">Partners</div></div>
		</div>
		<div class="epc-lc-kpi__item">
			<div class="epc-lc-kpi__icon epc-lc-kpi__icon--sky"><i class="fa fa-check-circle"></i></div>
			<div><div class="epc-lc-kpi__val"><?php echo (int)$activeCount; ?></div><div class="epc-lc-kpi__label">Active</div></div>
		</div>
		<div class="epc-lc-kpi__item">
			<div class="epc-lc-kpi__icon epc-lc-kpi__icon--sand"><i class="fa fa-map"></i></div>
			<div><div class="epc-lc-kpi__val"><?php echo (int)$dash['regions']; ?></div><div class="epc-lc-kpi__label">Regions</div></div>
		</div>
		<div class="epc-lc-kpi__item">
			<div class="epc-lc-kpi__icon"><i class="fa fa-barcode"></i></div>
			<div><div class="epc-lc-kpi__val"><?php echo (int)$dash['shipments_shipped']; ?>/<?php echo (int)$dash['shipments']; ?></div><div class="epc-lc-kpi__label">Shipments</div></div>
		</div>
		<div class="epc-lc-kpi__item">
			<div class="epc-lc-kpi__icon epc-lc-kpi__icon--slate"><i class="fa fa-shopping-cart"></i></div>
			<div><div class="epc-lc-kpi__val"><?php echo (int)$dash['shop_orders']; ?></div><div class="epc-lc-kpi__label">Shop orders</div></div>
		</div>
	</div>

	<div class="epc-lc-toolbar">
		<button type="button" class="btn btn-success btn-sm" id="epc_lc_seed_carriers"><i class="fa fa-refresh"></i> Sync worldwide partners</button>
		<button type="button" class="btn btn-default btn-sm" id="epc_lc_seed_sample"><i class="fa fa-database"></i> Load sample shipment</button>
		<a class="btn btn-default btn-sm" href="<?php echo epc_logistics_h($demoJsonUrl); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> JSON report</a>
		<span class="text-muted" style="font-size:12px;font-weight:600;">Demo rates &amp; labels until live API keys are connected</span>
	</div>
	<div id="epc_lc_msg" class="alert epc-lc-msg" style="display:none;"></div>

	<div class="epc-lc-filters" role="toolbar" aria-label="Filter by region">
		<button type="button" class="epc-lc-filter is-active" data-region="all">All regions (<?php echo count($catalog); ?>)</button>
		<?php foreach ($regions as $regionName => $regionCount): ?>
			<button type="button" class="epc-lc-filter" data-region="<?php echo epc_logistics_h($regionName); ?>"><?php echo epc_logistics_h($regionName); ?> (<?php echo (int)$regionCount; ?>)</button>
		<?php endforeach; ?>
	</div>

	<div class="epc-lc-partners" id="epc_lc_partners">
		<?php foreach ($catalog as $code => $meta):
			$acc = isset($byCode[$code]) ? $byCode[$code] : null;
			$active = $acc ? ((int)$acc['active'] === 1) : false;
			$demo = $acc ? ((int)$acc['demo_mode'] === 1) : true;
			$accent = isset($meta['accent']) ? (string)$meta['accent'] : '#0f766e';
			$icon = isset($meta['icon']) ? (string)$meta['icon'] : 'fa-truck';
			$region = isset($meta['region']) ? (string)$meta['region'] : 'Global';
			$services = isset($meta['services']) && is_array($meta['services']) ? array_values($meta['services']) : array();
			$trackTpl = isset($meta['track_url']) ? (string)$meta['track_url'] : '';
			$trackHome = $trackTpl !== '' ? preg_replace('/%s.*/', '', $trackTpl) : '';
			if ($trackHome === '' && $trackTpl !== '') {
				$trackHome = $trackTpl;
			}
			?>
			<article class="epc-lc-partner<?php echo $active ? '' : ' is-off'; ?>" data-region="<?php echo epc_logistics_h($region); ?>" data-code="<?php echo epc_logistics_h($code); ?>">
				<div class="epc-lc-partner__stripe" style="background:<?php echo epc_logistics_h($accent); ?>"></div>
				<div class="epc-lc-partner__body">
					<div class="epc-lc-partner__top">
						<div class="epc-lc-partner__mark" style="background:<?php echo epc_logistics_h($accent); ?>"><i class="fa <?php echo epc_logistics_h($icon); ?>"></i></div>
						<div>
							<h4 class="epc-lc-partner__name"><?php echo epc_logistics_h($meta['name'] ?? strtoupper($code)); ?></h4>
							<div class="epc-lc-partner__code"><code><?php echo epc_logistics_h($code); ?></code></div>
						</div>
					</div>
					<p class="epc-lc-partner__blurb"><?php echo epc_logistics_h($meta['blurb'] ?? ''); ?></p>
					<div class="epc-lc-partner__meta">
						<span class="epc-lc-badge"><?php echo epc_logistics_h($region); ?></span>
						<?php if ($active): ?>
							<span class="epc-lc-badge epc-lc-badge--ok"><i class="fa fa-check"></i> Active</span>
						<?php else: ?>
							<span class="epc-lc-badge epc-lc-badge--off">Off</span>
						<?php endif; ?>
						<span class="epc-lc-badge<?php echo $demo ? ' epc-lc-badge--warn' : ' epc-lc-badge--ok'; ?>"><?php echo $demo ? 'Demo rates' : 'Live API'; ?></span>
					</div>
					<div class="epc-lc-partner__services"><?php echo epc_logistics_h(implode(' · ', $services)); ?></div>
					<div class="epc-lc-partner__foot">
						<button type="button" class="btn btn-xs btn-default" data-toggle-carrier="<?php echo epc_logistics_h($code); ?>">
							<i class="fa <?php echo $active ? 'fa-toggle-on' : 'fa-toggle-off'; ?>"></i> <?php echo $active ? 'Disable' : 'Enable'; ?>
						</button>
						<?php if ($trackHome !== ''): ?>
							<a class="btn btn-xs btn-default" href="<?php echo epc_logistics_h($trackHome); ?>" target="_blank" rel="noopener"><i class="fa fa-external-link"></i> Tracking</a>
						<?php endif; ?>
					</div>
				</div>
			</article>
		<?php endforeach; ?>
	</div>

	<section class="epc-lc-section">
		<div class="epc-lc-section__head">
			<h3><i class="fa fa-barcode"></i> Recent shipments</h3>
			<span>Storefront orders &amp; CP order cards</span>
		</div>
		<div class="epc-lc-section__body">
			<?php if (empty($shipments)): ?>
				<div class="epc-lc-empty">No shipments yet — create from an order card or load sample data.</div>
			<?php else: ?>
				<table class="epc-lc-table">
					<thead><tr><th>Order</th><th>Carrier</th><th>Tracking</th><th>Cost</th><th>Status</th></tr></thead>
					<tbody>
					<?php foreach ($shipments as $sh):
						$st = strtolower((string)($sh['status'] ?? ''));
						$stClass = 'epc-lc-status';
						if (in_array($st, array('shipped', 'delivered'), true)) {
							$stClass .= ' epc-lc-status--shipped';
						} elseif ($st !== '') {
							$stClass .= ' epc-lc-status--pending';
						}
						$cname = isset($catalog[$sh['carrier_code']]['name']) ? $catalog[$sh['carrier_code']]['name'] : strtoupper((string)$sh['carrier_code']);
						?>
						<tr>
							<td><a href="<?php echo epc_logistics_h($ordersUrl); ?>?order_id=<?php echo (int)$sh['order_id']; ?>">#<?php echo (int)$sh['order_id']; ?></a></td>
							<td><strong><?php echo epc_logistics_h($cname); ?></strong> <code><?php echo epc_logistics_h($sh['carrier_code']); ?></code></td>
							<td><?php if (!empty($sh['label_url'])): ?><a href="<?php echo epc_logistics_h($sh['label_url']); ?>" target="_blank" rel="noopener"><?php echo epc_logistics_h($sh['tracking_number']); ?></a><?php else: ?><?php echo epc_logistics_h($sh['tracking_number']); ?><?php endif; ?></td>
							<td><?php echo epc_logistics_money($sh['cost']); ?> <?php echo epc_logistics_h($sh['currency']); ?></td>
							<td><span class="<?php echo $stClass; ?>"><?php echo epc_logistics_h($sh['status']); ?></span></td>
						</tr>
					<?php endforeach; ?>
					</tbody>
				</table>
			<?php endif; ?>
		</div>
	</section>

	<section class="epc-lc-section">
		<div class="epc-lc-section__head">
			<h3><i class="fa fa-history"></i> Activity log</h3>
			<span>Seeds, labels, enable/disable</span>
		</div>
		<div class="epc-lc-section__body">
			<?php if (empty($logs)): ?>
				<div class="epc-lc-empty">No log entries yet</div>
			<?php else: ?>
				<ul class="epc-lc-log">
					<?php foreach ($logs as $lg): ?>
						<li>
							<time><?php echo epc_logistics_h(date('Y-m-d H:i', (int)$lg['time_created'])); ?></time>
							<span><?php echo epc_logistics_h($lg['message']); ?><?php if (!empty($lg['channel_code'])): ?> <code><?php echo epc_logistics_h($lg['channel_code']); ?></code><?php endif; ?></span>
						</li>
					<?php endforeach; ?>
				</ul>
			<?php endif; ?>
		</div>
	</section>
</div>
<script>
window.EPC_LC = <?php
	$epcLcBackend = isset($GLOBALS['DP_Config']->backend_dir) ? trim((string)$GLOBALS['DP_Config']->backend_dir, '/') : 'cp';
	echo json_encode(array(
		// Prefer CMS page POST (admin session already established in CP shell).
		'url' => $carriersUrl,
		'ajaxUrl' => '/' . $epcLcBackend . '/content/shop/logistics/ajax_logistics.php',
		'csrf' => $csrf,
	), JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
?>;
</script>
<script src="/<?php echo htmlspecialchars($epcLcBackend, ENT_QUOTES, 'UTF-8'); ?>/content/shop/logistics/logistics_carriers.js?v=<?php echo (int)(@filemtime(__DIR__ . '/logistics_carriers.js') ?: time()); ?>"></script>
<script>if (typeof window.epcLcBind === 'function') { window.epcLcBind(); }</script>
</div>
<?php
?>
