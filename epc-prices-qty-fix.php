<?php
/**
 * Fix CP price module QTY display — ensure records_count column exists and is
 * backfilled from shop_docpart_prices_data (index-only GROUP BY), plus report
 * storefront disable flags so "data not showing" causes are visible.
 *
 * Dry-run:
 *   /epc-prices-qty-fix.php?token=epartscart-deploy-2026&key=TECH_KEY
 * Apply (create column if missing + backfill counts):
 *   ...&apply=1
 * Re-enable all storefront-disabled lists/storages:
 *   ...&apply=1&enable_storefront=1
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
set_time_limit(90);

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

if ((string) ($_GET['key'] ?? '') !== (string) $cfg->tech_key) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

$apply = !empty($_GET['apply']);
$enableStorefront = !empty($_GET['enable_storefront']);

$report = array(
	'ok' => true,
	'hostname' => $_SERVER['HTTP_HOST'],
	'db' => $cfg->db,
	'apply' => $apply,
	'column' => array('records_count_exists' => false, 'created' => false),
	'counts' => array(),
	'lists' => array(),
	'storefront_disabled' => array('price_lists' => array(), 'storages' => array()),
	'changes' => array(),
);

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8;connect_timeout=5',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 10)
	);
} catch (Throwable $e) {
	exit(json_encode(array('ok' => false, 'error' => 'DB: ' . $e->getMessage()), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

// 1) records_count column
$hasCol = false;
try {
	$pdo->query('SELECT `records_count` FROM `shop_docpart_prices` LIMIT 1');
	$hasCol = true;
} catch (Throwable $e) {
	$hasCol = false;
}
$report['column']['records_count_exists'] = $hasCol;

if (!$hasCol && $apply) {
	try {
		$pdo->exec('ALTER TABLE `shop_docpart_prices` ADD COLUMN `records_count` INT NOT NULL DEFAULT 0');
		$hasCol = true;
		$report['column']['created'] = true;
		$report['changes'][] = 'ALTER shop_docpart_prices ADD records_count';
	} catch (Throwable $e) {
		$report['column']['create_error'] = $e->getMessage();
	}
}

// 2) live counts per list (index-only GROUP BY on x_price_id / price_id)
$live = array();
$t0 = microtime(true);
try {
	$q = $pdo->query('SELECT `price_id`, COUNT(*) AS `c` FROM `shop_docpart_prices_data` GROUP BY `price_id`');
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		$live[(int) $row['price_id']] = (int) $row['c'];
	}
	$report['counts']['group_by_ms'] = (int) round((microtime(true) - $t0) * 1000);
	$report['counts']['lists_with_rows'] = count($live);
	$report['counts']['total_rows'] = array_sum($live);
} catch (Throwable $e) {
	$report['counts']['error'] = $e->getMessage();
}

// 3) current lists + stored counts + backfill
try {
	$sel = $hasCol
		? 'SELECT `id`, `name`, `records_count`, IFNULL(`storefront_temp_disabled`,0) AS `sf_disabled` FROM `shop_docpart_prices` ORDER BY `id`'
		: 'SELECT `id`, `name`, 0 AS `records_count`, IFNULL(`storefront_temp_disabled`,0) AS `sf_disabled` FROM `shop_docpart_prices` ORDER BY `id`';
	$rows = array();
	try {
		$rows = $pdo->query($sel)->fetchAll(PDO::FETCH_ASSOC);
	} catch (Throwable $e) {
		// storefront_temp_disabled may be missing too
		$sel2 = $hasCol
			? 'SELECT `id`, `name`, `records_count`, 0 AS `sf_disabled` FROM `shop_docpart_prices` ORDER BY `id`'
			: 'SELECT `id`, `name`, 0 AS `records_count`, 0 AS `sf_disabled` FROM `shop_docpart_prices` ORDER BY `id`';
		$rows = $pdo->query($sel2)->fetchAll(PDO::FETCH_ASSOC);
	}

	$upd = null;
	if ($hasCol && $apply) {
		$upd = $pdo->prepare('UPDATE `shop_docpart_prices` SET `records_count` = ? WHERE `id` = ?');
	}
	foreach ($rows as $row) {
		$pid = (int) $row['id'];
		$liveCnt = isset($live[$pid]) ? (int) $live[$pid] : 0;
		$stored = (int) $row['records_count'];
		$entry = array(
			'id' => $pid,
			'name' => (string) $row['name'],
			'stored_qty' => $stored,
			'live_qty' => $liveCnt,
			'storefront_disabled' => (int) $row['sf_disabled'] === 1,
		);
		if ($upd && $stored !== $liveCnt) {
			$upd->execute(array($liveCnt, $pid));
			$entry['updated'] = true;
			$report['changes'][] = 'records_count: list#' . $pid . ' ' . $stored . ' → ' . $liveCnt;
		}
		if ((int) $row['sf_disabled'] === 1) {
			$report['storefront_disabled']['price_lists'][] = array('id' => $pid, 'name' => (string) $row['name']);
		}
		$report['lists'][] = $entry;
	}
} catch (Throwable $e) {
	$report['lists_error'] = $e->getMessage();
	$report['ok'] = false;
}

// 4) storefront-disabled storages (why "data not show for warehouse")
try {
	$q = $pdo->query(
		'SELECT `id`, `name`, `short_name` FROM `shop_storages` WHERE IFNULL(`storefront_temp_disabled`,0) = 1'
	);
	while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
		$report['storefront_disabled']['storages'][] = array(
			'id' => (int) $row['id'],
			'name' => (string) $row['name'],
			'short_name' => (string) $row['short_name'],
		);
	}
} catch (Throwable $e) {
	// column may not exist — nothing disabled then
}

if ($apply && $enableStorefront) {
	try {
		$n1 = $pdo->exec('UPDATE `shop_docpart_prices` SET `storefront_temp_disabled` = 0 WHERE IFNULL(`storefront_temp_disabled`,0) = 1');
		$n2 = $pdo->exec('UPDATE `shop_storages` SET `storefront_temp_disabled` = 0 WHERE IFNULL(`storefront_temp_disabled`,0) = 1');
		$report['changes'][] = 'storefront re-enabled: price_lists=' . (int) $n1 . ' storages=' . (int) $n2;
	} catch (Throwable $e) {
		$report['enable_storefront_error'] = $e->getMessage();
	}
}

$report['hint'] = $apply
	? 'Applied. Hard-refresh /cp/shop/prices — QTY column should show live counts.'
	: 'Dry run. Add &apply=1 to create/backfill records_count. Add &enable_storefront=1 to re-enable disabled lists/storages.';

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
