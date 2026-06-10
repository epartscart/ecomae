<?php
/**
 * Auto Price AI Discover tab — runtime config (loaded from CP footer).
 */
define('_ASTEXE_', 1);
header('Content-Type: application/javascript; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');

require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
if (!DP_User::isAdmin()) {
	http_response_code(403);
	echo 'window.EPC_APAI_DISCOVER={};';
	exit;
}

$backend = trim((string) $DP_Config->backend_dir, '/');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_engine.php';
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? ''))));
$tab = (string) ($_GET['tab'] ?? 'discover');
$tabAliases = array('my_imports' => 'imports', 'discovery' => 'discover');
if (isset($tabAliases[$tab])) {
	$tab = $tabAliases[$tab];
}

$ajaxUrl = function_exists('epc_apai_ajax_url') ? epc_apai_ajax_url($backend) : ('/' . $backend . '/control/portal/ajax_auto_price');
echo 'window.EPC_APAI_DISCOVER = ' . json_encode(array(
	'ajaxUrl' => $ajaxUrl,
	'siteKey' => $siteKey,
	'tab' => $tab,
	'active' => ($tab === 'discover'),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';

echo 'window.EPC_APAI_SOURCES = ' . json_encode(array(
	'ajaxUrl' => $ajaxUrl,
	'siteKey' => $siteKey,
	'tab' => $tab,
	'active' => (in_array($tab, array('uae_sources', 'disc_sources', 'sources', 'market_sources'), true)),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';

echo 'window.EPC_APAI_PRODUCT_LINES = ' . json_encode(array(
	'siteKey' => $siteKey,
	'tab' => $tab,
	'active' => ($tab === 'product_lines'),
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';

$importsFilter = (string) ($_GET['imports_filter'] ?? 'new');
if (!in_array($importsFilter, array('new', 'price_changes', 'duplicates'), true)) {
	$importsFilter = 'new';
}
echo 'window.EPC_APAI_IMPORTS = ' . json_encode(array(
	'ajaxUrl' => $ajaxUrl,
	'siteKey' => $siteKey,
	'tab' => $tab,
	'filter' => $importsFilter,
	'active' => ($tab === 'imports'),
	'pageBase' => '/' . $backend . '/control/portal/epc_auto_price_engine',
), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . ';';
