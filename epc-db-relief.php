<?php
/**
 * DB / PHP-FPM relief for epartscart CP 524s.
 *
 * Dry-run (processlist + load + listing timing):
 *   https://www.epartscart.com/epc-db-relief.php?token=epartscart-deploy-2026&key=TECH_KEY
 *
 * Kill long queries (ALTER / metadata lock / Sleep / prices_data):
 *   ...&apply=1&min_time=30
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
set_time_limit(60);

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
$minTime = max(5, (int) ($_GET['min_time'] ?? 30));

$report = array(
	'ok' => true,
	'hostname' => $_SERVER['HTTP_HOST'],
	'db' => $cfg->db,
	'apply' => $apply,
	'min_time' => $minTime,
	'load' => null,
	'processlist' => array(),
	'killed' => array(),
	'timings' => array(),
	'hints' => array(),
);

if (function_exists('sys_getloadavg')) {
	$load = sys_getloadavg();
	if (is_array($load)) {
		$report['load'] = array('1m' => $load[0], '5m' => $load[1], '15m' => $load[2]);
		if ($load[0] > 8) {
			$report['hints'][] = 'Host load is high — restart PHP-FPM from CloudPanel if CP pages still 524.';
		}
	}
}

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8;connect_timeout=5',
		$cfg->user,
		$cfg->password,
		array(
			PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
			PDO::ATTR_TIMEOUT => 8,
		)
	);
} catch (Throwable $e) {
	exit(json_encode(array('ok' => false, 'error' => 'DB: ' . $e->getMessage()), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

try {
	$rows = $pdo->query('SHOW FULL PROCESSLIST')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as $row) {
		$info = (string) ($row['Info'] ?? $row['info'] ?? '');
		$cmd = (string) ($row['Command'] ?? $row['command'] ?? '');
		$time = (int) ($row['Time'] ?? $row['time'] ?? 0);
		$user = (string) ($row['User'] ?? $row['user'] ?? '');
		$report['processlist'][] = array(
			'id' => (int) ($row['Id'] ?? $row['ID'] ?? 0),
			'user' => $user,
			'db' => (string) ($row['db'] ?? ''),
			'command' => $cmd,
			'time' => $time,
			'state' => (string) ($row['State'] ?? $row['state'] ?? ''),
			'info' => substr($info, 0, 240),
		);
	}
} catch (Throwable $e) {
	$report['processlist_error'] = $e->getMessage();
}

$killPatterns = array(
	'ALTER TABLE',
	'shop_docpart_prices_data',
	'shop_docpart_prices',
	'information_schema',
	'metadata lock',
	'Waiting for table',
);

if ($apply) {
	foreach ($report['processlist'] as $row) {
		$id = (int) $row['id'];
		$time = (int) $row['time'];
		$cmd = strtolower((string) $row['command']);
		$info = (string) $row['info'];
		$user = (string) $row['user'];
		if ($id <= 0 || $time < $minTime) {
			continue;
		}
		if (in_array($user, array('system user', 'event_scheduler'), true)) {
			continue;
		}
		$shouldKill = ($cmd === 'sleep');
		foreach ($killPatterns as $pat) {
			if ($info !== '' && stripos($info, $pat) !== false) {
				$shouldKill = true;
				break;
			}
		}
		if ($cmd === 'query' && $time >= max($minTime, 60)) {
			$shouldKill = true;
		}
		if (!$shouldKill) {
			continue;
		}
		try {
			$pdo->exec('KILL ' . $id);
			$report['killed'][] = array('id' => $id, 'time' => $time, 'command' => $cmd, 'info' => substr($info, 0, 120));
		} catch (Throwable $e) {
			$report['killed'][] = array('id' => $id, 'error' => $e->getMessage());
		}
	}
}

// Listing path timings (must stay under ~1s).
$t0 = microtime(true);
try {
	$cnt = (int) $pdo->query('SELECT COUNT(*) FROM `shop_docpart_prices`')->fetchColumn();
	$report['timings']['count_prices_ms'] = (int) round((microtime(true) - $t0) * 1000);
	$report['timings']['prices_count'] = $cnt;
} catch (Throwable $e) {
	$report['timings']['count_prices_error'] = $e->getMessage();
}

$t1 = microtime(true);
try {
	$hasCol = false;
	try {
		$pdo->query('SELECT `records_count` FROM `shop_docpart_prices` LIMIT 1');
		$hasCol = true;
	} catch (Throwable $e) {
		$hasCol = false;
	}
	$sql = $hasCol
		? 'SELECT p.`id`, p.`name`, COALESCE(p.`records_count`, 0) AS `records_count` FROM `shop_docpart_prices` p ORDER BY p.`id`'
		: 'SELECT p.`id`, p.`name`, 0 AS `records_count` FROM `shop_docpart_prices` p ORDER BY p.`id`';
	$st = $pdo->query($sql);
	$rows = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
	$report['timings']['list_prices_ms'] = (int) round((microtime(true) - $t1) * 1000);
	$report['timings']['list_rows'] = count($rows);
	$report['timings']['has_records_count'] = $hasCol;
} catch (Throwable $e) {
	$report['timings']['list_prices_error'] = $e->getMessage();
}

$report['hint'] = $apply
	? 'Killed matching long queries. Hard-refresh CP. If still 524, restart PHP-FPM in CloudPanel.'
	: 'Dry run. Pass apply=1&min_time=30 to KILL long Sleep/ALTER/prices queries.';

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
