<?php
/**
 * CP — Website traffic tracker (all tenants on Super CP; own site on tenant CP).
 * Canonical menu route: /cp/shop/statistics/web_tracker
 * Legacy portal URL redirects there (one menu item only).
 * CSS/JS load via epc_cp_page_assets (inline script inside .row does not run in CP shell).
 */
$qs = isset($_SERVER['QUERY_STRING']) && $_SERVER['QUERY_STRING'] !== ''
	? ('?' . $_SERVER['QUERY_STRING'])
	: '';
$reqUri = (string) ($_SERVER['REQUEST_URI'] ?? '');
if (strpos($reqUri, '/control/portal/epc_web_tracker') !== false) {
	$backend = 'cp';
	if (isset($GLOBALS['DP_Config']->backend_dir) && (string) $GLOBALS['DP_Config']->backend_dir !== '') {
		$backend = trim((string) $GLOBALS['DP_Config']->backend_dir, '/');
	}
	header('Location: /' . $backend . '/shop/statistics/web_tracker' . $qs, true, 302);
	exit;
}
if (!defined('_ASTEXE_')) {
	header('Location: /cp/shop/statistics/web_tracker' . $qs, true, 302);
	exit;
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_web_tracker.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

$isSuper = function_exists('epc_portal_is_platform_operator') && epc_portal_is_platform_operator();
if (!$isSuper && function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host()) {
	$isSuper = true;
}
$backend = htmlspecialchars((string) ($GLOBALS['DP_Config']->backend_dir ?? 'cp'), ENT_QUOTES, 'UTF-8');
$ver = '20260719wt3';

if (function_exists('epc_cp_register_page_assets')) {
	epc_cp_register_page_assets(
		array('/content/general_pages/epc_web_tracker_cp_css.php?v=' . rawurlencode($ver)),
		array(
			'/' . trim($backend, '/') . '/content/control/portal/epc_web_tracker_config.php?v=' . rawurlencode($ver),
			'/' . trim($backend, '/') . '/content/control/portal/epc_web_tracker_cp.js?v=' . rawurlencode($ver),
		)
	);
}

$pdo = function_exists('epc_portal_platform_pdo') ? epc_portal_platform_pdo() : null;
if (!$pdo instanceof PDO && isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) {
	$pdo = $GLOBALS['db_link'];
}
if ($pdo instanceof PDO) {
	try {
		epc_web_tracker_ensure_schema($pdo);
	} catch (Throwable $e) {
		$pdo = (isset($GLOBALS['db_link']) && $GLOBALS['db_link'] instanceof PDO) ? $GLOBALS['db_link'] : null;
		if ($pdo instanceof PDO) {
			try {
				epc_web_tracker_ensure_schema($pdo);
			} catch (Throwable $e2) {
				$pdo = null;
			}
		}
	}
}

$tenants = ($pdo instanceof PDO && function_exists('epc_portal_list_tenants'))
	? epc_portal_list_tenants($pdo)
	: array();

$ownKey = epc_web_tracker_resolve_site_key();
$siteKey = preg_replace('/[^a-z0-9_\-]/', '', strtolower((string) ($_GET['site_key'] ?? '')));
if (!$isSuper) {
	$siteKey = $ownKey;
} elseif ($siteKey === '') {
	$siteKey = '_all';
}

$from = date('Y-m-d', time() - 7 * 86400);
$to = date('Y-m-d');
if (!empty($_GET['from'])) {
	$from = preg_replace('/[^0-9\-]/', '', (string) $_GET['from']);
}
if (!empty($_GET['to'])) {
	$to = preg_replace('/[^0-9\-]/', '', (string) $_GET['to']);
}

epc_cp_page_frame_open(array(
	'class' => 'epc-web-tracker',
	'hero' => array(
		'badge' => $isSuper ? 'Super CP · all tenants' : 'Tenant traffic',
		'title' => 'Website tracker',
		'sub' => 'Pageviews, clicks, search, geography, devices, and full session timelines for guests and registered users.',
	),
));
?>
<div class="epc-wt">
	<div class="wt-filters">
		<?php if ($isSuper) { ?>
		<div>
			<label>Site / tenant</label>
			<select id="wt_site" class="form-control">
				<option value="_all"<?php echo $siteKey === '_all' ? ' selected' : ''; ?>>All sites (Super)</option>
				<option value="ecomae"<?php echo $siteKey === 'ecomae' ? ' selected' : ''; ?>>ecomae (marketing)</option>
				<option value="epartscart"<?php echo $siteKey === 'epartscart' ? ' selected' : ''; ?>>epartscart</option>
				<?php foreach ($tenants as $t) {
					$sk = (string) ($t['site_key'] ?? '');
					if ($sk === '' || $sk === 'ecomae' || $sk === 'epartscart') continue;
					$lab = $sk . ' — ' . (string) ($t['hostname'] ?? '');
					echo '<option value="' . epc_web_tracker_h($sk) . '"' . ($siteKey === $sk ? ' selected' : '') . '>'
						. epc_web_tracker_h($lab) . '</option>';
				} ?>
			</select>
		</div>
		<?php } else { ?>
		<input type="hidden" id="wt_site" value="<?php echo epc_web_tracker_h($siteKey); ?>" />
		<div>
			<label>Site</label>
			<div class="form-control" style="background:#f8fafc;"><?php echo epc_web_tracker_h($siteKey); ?></div>
		</div>
		<?php } ?>
		<div>
			<label>From</label>
			<input type="date" id="wt_from" class="form-control" value="<?php echo epc_web_tracker_h($from); ?>" />
		</div>
		<div>
			<label>To</label>
			<input type="date" id="wt_to" class="form-control" value="<?php echo epc_web_tracker_h($to); ?>" />
		</div>
		<div>
			<label>Device</label>
			<select id="wt_device" class="form-control">
				<option value="">All devices</option>
				<option value="desktop">Desktop</option>
				<option value="mobile">Mobile</option>
				<option value="tablet">Tablet</option>
			</select>
		</div>
		<div>
			<label>Country</label>
			<select id="wt_country" class="form-control">
				<option value="">All countries</option>
			</select>
		</div>
		<div>
			<label>IP address</label>
			<input type="text" id="wt_ip" class="form-control" placeholder="e.g. 86.96." autocomplete="off" />
		</div>
		<div>
			<label>User ID</label>
			<input type="text" id="wt_user_id" class="form-control" placeholder="e.g. 5" inputmode="numeric" autocomplete="off" />
		</div>
		<div>
			<label>Who</label>
			<select id="wt_user_type" class="form-control">
				<option value="">All visitors</option>
				<option value="guest">Guests only</option>
				<option value="registered">Registered only</option>
			</select>
		</div>
		<div>
			<label>Browser</label>
			<select id="wt_browser" class="form-control">
				<option value="">All browsers</option>
			</select>
		</div>
		<div>
			<label>Page path contains</label>
			<input type="text" id="wt_path" class="form-control" placeholder="/en/parts/" autocomplete="off" />
		</div>
		<div>
			<button type="button" class="btn btn-primary" id="wt_reload"><i class="fa fa-refresh"></i> Apply filters</button>
		</div>
		<div>
			<button type="button" class="btn btn-default" id="wt_clear_filters" title="Clear device / country / IP / user filters">Clear filters</button>
		</div>
		<div>
			<button type="button" class="btn btn-default" id="wt_csv" title="Download full report (CSV) for the selected filters"><i class="fa fa-download"></i> Download full report (CSV)</button>
		</div>
		<div class="wt-muted" style="align-self:center;">
			Beacon: <code>/epc-web-tracker-collect.php</code> · storefront + ecomae marketing
		</div>
	</div>

	<div class="wt-status alert alert-info" id="wt_status">Loading traffic…</div>
	<div class="wt-kpis" id="wt_kpis"></div>

	<div class="wt-grid">
		<div class="wt-panel">
			<h4>Traffic by day</h4>
			<div class="wt-body" id="wt_daily"></div>
		</div>
		<div class="wt-panel">
			<h4><?php echo $isSuper ? 'By tenant / hostname' : 'Devices & browsers'; ?></h4>
			<div class="wt-body" id="wt_side_a"></div>
		</div>
	</div>

	<div class="wt-grid">
		<div class="wt-panel">
			<h4>Top pages (experience)</h4>
			<div class="wt-body" id="wt_pages"></div>
		</div>
		<div class="wt-panel">
			<h4>Geography</h4>
			<div class="wt-body" id="wt_geo"></div>
		</div>
	</div>

	<div class="wt-grid">
		<div class="wt-panel">
			<h4>Search terms</h4>
			<div class="wt-body" id="wt_search"></div>
		</div>
		<div class="wt-panel">
			<h4>Top clicks</h4>
			<div class="wt-body" id="wt_clicks"></div>
		</div>
	</div>

	<div class="wt-grid">
		<div class="wt-panel">
			<h4>Referrers &amp; UTM</h4>
			<div class="wt-body" id="wt_refs"></div>
		</div>
		<div class="wt-panel">
			<h4><?php echo $isSuper ? 'Devices & browsers' : 'Recent note'; ?></h4>
			<div class="wt-body" id="wt_side_b"></div>
		</div>
	</div>

	<div class="wt-panel" style="margin-bottom:20px;">
		<h4>Recent sessions — IP shown · click page links to open that page · click a row for full timeline</h4>
		<div class="wt-body" id="wt_sessions" style="max-height:520px;"></div>
	</div>
</div>

<div class="modal fade" id="wt_session_modal" tabindex="-1" role="dialog">
	<div class="modal-dialog modal-lg" role="document">
		<div class="modal-content">
			<div class="modal-header">
				<button type="button" class="close" data-dismiss="modal"><span>&times;</span></button>
				<h4 class="modal-title">Session timeline</h4>
			</div>
			<div class="modal-body" id="wt_session_body">Loading…</div>
		</div>
	</div>
</div>
<?php epc_cp_page_frame_close(); ?>
