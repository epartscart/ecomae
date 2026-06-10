<?php
/**
 * Verify Auto Price AI imports tab functions (deploy token).
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

define('_ASTEXE_', 1);

try {
	require_once __DIR__ . '/config.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
	require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';

	$cfg = new DP_Config();
	$dbHost = trim((string) $cfg->host);
	if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
		$dbHost = '127.0.0.1';
	}
	$pdo = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $cfg->db . ';charset=utf8',
		(string) $cfg->user,
		(string) $cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);

	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? 'epartscart'))));
	if ($siteKey === '') {
		$siteKey = 'epartscart';
	}

	epc_ape_ensure_schema($pdo);

	$checks = array(
		'epc_disc_queue_list_for_imports' => function_exists('epc_disc_queue_list_for_imports'),
		'epc_disc_queue_dismiss_duplicates' => function_exists('epc_disc_queue_dismiss_duplicates'),
		'epc_disc_imports_counts' => function_exists('epc_disc_imports_counts'),
	);

	$counts = epc_disc_imports_counts($pdo, $siteKey);
	$newList = epc_disc_queue_list_for_imports($pdo, $siteKey, array('filter' => 'new', 'limit' => 5));
	$priceList = epc_disc_queue_list_for_imports($pdo, $siteKey, array('filter' => 'price_changes', 'limit' => 5));
	$dupList = epc_disc_queue_list_for_imports($pdo, $siteKey, array('filter' => 'duplicates', 'limit' => 5));

	$enginePath = __DIR__ . '/cp/content/control/portal/epc_auto_price_engine.php';
	$engineSrc = is_file($enginePath) ? file_get_contents($enginePath) : '';
	$jsPath = __DIR__ . '/cp/content/control/portal/epc_auto_price_imports.js';
	$ajaxPath = __DIR__ . '/cp/content/control/portal/ajax_auto_price.php';
	$ajaxSrc = is_file($ajaxPath) ? file_get_contents($ajaxPath) : '';

	echo json_encode(array(
		'ok' => !in_array(false, $checks, true),
		'site_key' => $siteKey,
		'checks' => $checks,
		'counts' => $counts,
		'lists' => array(
			'new' => count((array) ($newList['items'] ?? array())),
			'price_changes' => count((array) ($priceList['items'] ?? array())),
			'duplicate_groups' => count((array) ($dupList['groups'] ?? array())),
		),
		'ui' => array(
			'engine_has_subtabs' => strpos($engineSrc, 'epc-imports-subtabs') !== false,
			'engine_has_my_imports_alias' => strpos($engineSrc, "'my_imports' => 'imports'") !== false,
			'imports_js_bytes' => is_file($jsPath) ? filesize($jsPath) : 0,
			'ajax_has_list_my_imports' => strpos($ajaxSrc, 'list_my_imports') !== false,
			'ajax_has_dismiss_duplicate' => strpos($ajaxSrc, 'dismiss_duplicate') !== false,
		),
		'tab_urls' => array(
			'imports' => 'https://www.epartscart.com/cp/control/portal/epc_auto_price_engine?site_key=epartscart&tab=imports',
			'my_imports' => 'https://www.epartscart.com/cp/control/portal/epc_auto_price_engine?site_key=epartscart&tab=my_imports',
		),
	), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => $e->getMessage()));
}
