<?php
/**
 * Auto Price Engine — standalone operator guide route.
 * /cp/control/portal/epc_auto_price_guide?site_key=electronicae
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_engine.php';

$isSuperCp = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
if (!$isSuperCp) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	if (!DP_User::isAdmin()) {
		global $DP_Config;
		echo '<div class="alert alert-warning">Please <a href="/' . epc_ape_h((string) $DP_Config->backend_dir) . '/">log in to CP</a>.</div>';
		return;
	}
} else {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_super_cp_platform.php';
	if (!epc_scp_guard_super_admin()) {
		return;
	}
}

global $db_link, $DP_Config;
$platformPdo = ($db_link instanceof PDO) ? $db_link : null;
if (!$platformPdo instanceof PDO) {
	echo '<div class="alert alert-danger">Database unavailable.</div>';
	return;
}
epc_ape_ensure_schema($platformPdo);

$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
if ($siteKey === '') {
	if ($isSuperCp) {
		$siteKey = 'electronicae';
	} else {
		$host = function_exists('epc_portal_host') ? strtolower(epc_portal_host()) : '';
		if (strpos($host, 'electronicae') !== false) {
			$siteKey = 'electronicae';
		} elseif (strpos($host, 'epartscart') !== false) {
			$siteKey = 'epartscart';
		} else {
			$siteKey = 'platform';
		}
	}
}

$pdo = $platformPdo;
if ($isSuperCp && $siteKey !== '' && $siteKey !== 'platform') {
	$tenantPdo = epc_ape_tenant_pdo($platformPdo, $siteKey);
	if ($tenantPdo instanceof PDO) {
		epc_ape_ensure_schema($tenantPdo);
		$pdo = $tenantPdo;
	}
}

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_assets.php';
epc_cp_register_page_assets(array('/content/general_pages/epc_auto_price_engine_css.php?v=' . rawurlencode(epc_cp_page_asset_version())));

$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
$pageBase = '/' . $backend . '/control/portal/epc_auto_price_engine';
$guideBase = '/' . $backend . '/control/portal/epc_auto_price_guide';

epc_cp_page_frame_open(array(
	'class' => 'epc-ape-panel',
	'hero' => array(
		'badge' => 'Auto Price Engine',
		'title' => 'Operator guide',
		'sub' => 'Universal Auto Price AI workflow — discover, compare, import, and keep prices fresh for every tenant.',
		'actions' => array(
			array('label' => 'Open engine', 'url' => $pageBase . '?site_key=' . urlencode($siteKey), 'icon' => 'fa-line-chart'),
			array('label' => 'Compare matrix', 'url' => $pageBase . '?site_key=' . urlencode($siteKey) . '&tab=compare', 'icon' => 'fa-table', 'primary' => true),
		),
	),
));

$guidePanelPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/control/portal/epc_auto_price_guide_panel.php';
if (is_file($guidePanelPath)) {
	require $guidePanelPath;
} else {
	echo '<div class="alert alert-danger">Guide panel file missing. Deploy <code>cp/content/control/portal/epc_auto_price_guide_panel.php</code>.</div>';
}

epc_cp_page_frame_close();
