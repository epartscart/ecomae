<?php
/**
 * eParts Cart — ensure fast warehouse article index + short_name labels.
 *
 * https://www.epartscart.com/epc-epartscart-warehouse-search-fast.php?token=epartscart-deploy-2026&key=TECH_KEY&apply=1
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
set_time_limit(300);

if (($_GET['token'] ?? $_POST['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/docpart/docpart_article_match.php';

$hostname = strtolower(trim((string) ($_SERVER['HTTP_HOST'] ?? 'www.epartscart.com')));
$_SERVER['HTTP_HOST'] = (strpos($hostname, 'www.') === 0) ? $hostname : ('www.' . preg_replace('/^www\./', '', $hostname));

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

if ((string) ($_GET['key'] ?? $_POST['key'] ?? '') !== $cfg->tech_key) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

$apply = !empty($_GET['apply']) || !empty($_POST['apply']);
$report = array(
	'ok' => true,
	'host' => $_SERVER['HTTP_HOST'],
	'db' => $cfg->db,
	'apply' => $apply,
	'article_search_ready' => false,
	'backfilled_rows' => 0,
	'short_name_filled' => 0,
	'notes' => array(),
);

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	exit(json_encode(array('ok' => false, 'error' => 'DB: ' . $e->getMessage()), JSON_UNESCAPED_UNICODE));
}

$report['article_search_ready'] = docpart_price_data_ensure_article_search_column($pdo, true);
if (!$report['article_search_ready']) {
	$report['ok'] = false;
	$report['notes'][] = 'Could not create article_search column';
	echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
	exit;
}

$maxChunks = max(1, min(40, (int) ($_GET['max_chunks'] ?? $_POST['max_chunks'] ?? 40)));
$chunkSize = max(1000, min(50000, (int) ($_GET['chunk_size'] ?? $_POST['chunk_size'] ?? 50000)));

if ($apply) {
	// Backfill in chunks until empty or cap (tunable for live tenants under load).
	$total = 0;
	for ($i = 0; $i < $maxChunks; $i++) {
		$n = docpart_price_data_backfill_article_search($pdo, 0, $chunkSize);
		$total += $n;
		if ($n <= 0) {
			break;
		}
	}
	$report['backfilled_rows'] = $total;
	$report['max_chunks'] = $maxChunks;
	$report['chunk_size'] = $chunkSize;

	// Fill empty short_name from name so storefront warehouse chips are never blank
	$st = $pdo->exec(
		"UPDATE `shop_storages`
		 SET `short_name` = `name`
		 WHERE (TRIM(IFNULL(`short_name`, '')) = '') AND TRIM(IFNULL(`name`, '')) <> ''"
	);
	$report['short_name_filled'] = (int) $st;
	$report['notes'][] = 'article_search backfilled + empty short_name copied from name';
} else {
	$empty = (int) $pdo->query(
		"SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `article_search` = '' OR `article_search` IS NULL"
	)->fetchColumn();
	$blankShort = (int) $pdo->query(
		"SELECT COUNT(*) FROM `shop_storages` WHERE TRIM(IFNULL(`short_name`, '')) = '' AND TRIM(IFNULL(`name`, '')) <> ''"
	)->fetchColumn();
	$report['notes'][] = "Dry-run: empty article_search rows=$empty; blank short_name warehouses=$blankShort. Add &apply=1 to fix.";
	$report['empty_article_search'] = $empty;
	$report['blank_short_name'] = $blankShort;
}

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
