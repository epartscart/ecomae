<?php
/**
 * Auto Price AI — ajax_auto_price.php bootstrap probe (deploy token).
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$steps = array();
$fail = function (string $step, Throwable $e) use (&$steps): void {
	$steps[] = array('step' => $step, 'ok' => false, 'error' => $e->getMessage(), 'file' => $e->getFile(), 'line' => $e->getLine());
	echo json_encode(array('ok' => false, 'steps' => $steps), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
};

try {
	define('_ASTEXE_', 1);
	$steps[] = array('step' => 'define', 'ok' => true);
	require_once __DIR__ . '/config.php';
	$steps[] = array('step' => 'config.php', 'ok' => true);
	require_once __DIR__ . '/content/users/dp_user.php';
	$steps[] = array('step' => 'dp_user.php', 'ok' => true);
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_engine.php';
	$steps[] = array('step' => 'epc_auto_price_engine.php', 'ok' => true);
	require_once __DIR__ . '/content/shop/price_engine/epc_discovery_adapters.php';
	$steps[] = array('step' => 'epc_discovery_adapters.php', 'ok' => true);
	require_once __DIR__ . '/content/shop/price_engine/epc_apai_country_sources.php';
	$steps[] = array('step' => 'epc_apai_country_sources.php', 'ok' => true);
	require_once __DIR__ . '/content/shop/price_engine/epc_industry_taxonomy.php';
	$steps[] = array('step' => 'epc_industry_taxonomy.php', 'ok' => true);
	require_once __DIR__ . '/content/shop/price_engine/epc_auto_price_categories.php';
	$steps[] = array('step' => 'epc_auto_price_categories.php', 'ok' => true);

	global $DP_Config, $db_link;
	$DP_Config = new DP_Config();
	$GLOBALS['DP_Config'] = $DP_Config;
	$db_link = new PDO(
		'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db . ';charset=utf8',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
	$GLOBALS['db_link'] = $db_link;
	$steps[] = array('step' => 'pdo', 'ok' => true, 'db' => $db_link->query('SELECT DATABASE()')->fetchColumn());

	$isAdmin = DP_User::isAdmin();
	$steps[] = array('step' => 'DP_User::isAdmin', 'ok' => true, 'is_admin' => (bool) $isAdmin);

	$platformPdo = $db_link;

	epc_ape_ensure_schema($platformPdo);
	$steps[] = array('step' => 'epc_ape_ensure_schema', 'ok' => true);

	$siteKey = 'electronicae';
	$suggested = $platformPdo->prepare(
		'SELECT `id` FROM `epc_product_discovery_queue` WHERE `site_key` = ? AND `status` = \'suggested\' ORDER BY `id` DESC LIMIT 2'
	);
	$suggested->execute(array($siteKey));
	$queueIds = array_map('intval', $suggested->fetchAll(PDO::FETCH_COLUMN) ?: array());
	if (count($queueIds) < 2) {
		epc_disc_run_for_taxonomy($platformPdo, $siteKey, 'cell-phones', '');
		$suggested->execute(array($siteKey));
		$queueIds = array_map('intval', $suggested->fetchAll(PDO::FETCH_COLUMN) ?: array());
	}
	$overrides = array();
	foreach ($queueIds as $qid) {
		$overrides[$qid] = array('category_mode' => 'auto', 'category_id' => 0);
	}
	$res = epc_disc_bulk_approve($platformPdo, $siteKey, $queueIds, array('category_overrides' => $overrides));
	$steps[] = array('step' => 'epc_disc_bulk_approve', 'ok' => !empty($res['ok']), 'result' => $res);

	echo json_encode(array('ok' => true, 'steps' => $steps), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
} catch (Throwable $e) {
	$fail('uncaught', $e);
}
