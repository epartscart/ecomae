<?php
/**
 * Remove Emex / WeTransfer price lists from the epartscart tenant DB.
 *
 * Dry-run:
 *   https://www.epartscart.com/epc-remove-emex-price.php?token=epartscart-deploy-2026
 * Apply:
 *   https://www.epartscart.com/epc-remove-emex-price.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';

$hostname = strtolower(trim((string) ($_SERVER['HTTP_HOST'] ?? 'www.epartscart.com')));
if ($hostname !== '' && strpos($hostname, 'www.') !== 0 && strpos($hostname, '.') !== false) {
	$hostname = 'www.' . preg_replace('/^www\./', '', $hostname);
}
$_SERVER['HTTP_HOST'] = $hostname !== '' ? $hostname : 'www.epartscart.com';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$apply = !empty($_GET['apply']);
$report = array(
	'ok' => true,
	'hostname' => $_SERVER['HTTP_HOST'],
	'db' => $cfg->db,
	'apply' => $apply,
	'matched' => array(),
	'deleted' => array(),
	'message' => '',
);

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	exit(json_encode(array('ok' => false, 'error' => 'DB: ' . $e->getMessage()), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

$q = $pdo->query(
	"SELECT `id`, `name`, `records_count`, `last_updated`
	 FROM `shop_docpart_prices`
	 WHERE `name` LIKE '%Emex%' OR `name` LIKE '%WeTransfer%' OR `name` LIKE '%emex%'
	 ORDER BY `id`"
);
$rows = $q ? $q->fetchAll(PDO::FETCH_ASSOC) : array();
foreach ($rows as $row) {
	$report['matched'][] = array(
		'id' => (int) $row['id'],
		'name' => (string) $row['name'],
		'records_count' => (int) ($row['records_count'] ?? 0),
		'last_updated' => (int) ($row['last_updated'] ?? 0),
	);
}

if (count($report['matched']) === 0) {
	$report['message'] = 'No Emex / WeTransfer price lists found.';
	exit(json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

if (!$apply) {
	$report['message'] = 'Dry-run only. Re-run with &apply=1 to delete matched price lists and their rows.';
	exit(json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

$ids = array_map(static function ($r) {
	return (int) $r['id'];
}, $report['matched']);
$placeholders = implode(',', array_fill(0, count($ids), '?'));

try {
	$pdo->beginTransaction();

	$delData = $pdo->prepare("DELETE FROM `shop_docpart_prices_data` WHERE `price_id` IN ($placeholders)");
	$delData->execute($ids);
	$dataDeleted = $delData->rowCount();

	try {
		$delCron = $pdo->prepare("DELETE FROM `shop_docpart_pyprices_crontab_prices` WHERE `price_id` IN ($placeholders)");
		$delCron->execute($ids);
	} catch (Throwable $e) {
		// optional table
	}

	try {
		$delHist = $pdo->prepare("DELETE FROM `epc_price_upload_history` WHERE `price_id` IN ($placeholders)");
		$delHist->execute($ids);
	} catch (Throwable $e) {
		// optional table
	}

	// Unlink warehouse connections pointing at these price lists
	try {
		$st = $pdo->query("SELECT `id`, `connection_options` FROM `shop_storages` WHERE `interface_type` = 2");
		while ($st && ($s = $st->fetch(PDO::FETCH_ASSOC))) {
			$opts = json_decode((string) $s['connection_options'], true);
			if (!is_array($opts) || empty($opts['price_id'])) {
				continue;
			}
			if (!in_array((int) $opts['price_id'], $ids, true)) {
				continue;
			}
			unset($opts['price_id']);
			$pdo->prepare('UPDATE `shop_storages` SET `connection_options` = ? WHERE `id` = ? LIMIT 1;')
				->execute(array(json_encode($opts, JSON_UNESCAPED_UNICODE), (int) $s['id']));
		}
	} catch (Throwable $e) {
		// non-fatal
	}

	$delPrices = $pdo->prepare("DELETE FROM `shop_docpart_prices` WHERE `id` IN ($placeholders)");
	$delPrices->execute($ids);

	$pdo->commit();
	$report['deleted'] = array(
		'price_ids' => $ids,
		'price_rows' => $delPrices->rowCount(),
		'data_rows' => $dataDeleted,
	);
	$report['message'] = 'Emex / WeTransfer price lists removed.';
} catch (Throwable $e) {
	if ($pdo->inTransaction()) {
		$pdo->rollBack();
	}
	$report['ok'] = false;
	$report['message'] = $e->getMessage();
}

exit(json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
