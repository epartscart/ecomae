<?php
/**
 * epartscart click→result ~1s pack (ops).
 *
 * Dry-run:
 *   /epc-epartscart-1s-speed.php?token=epartscart-deploy-2026&key=TECH_KEY
 * Apply (kill long queries + one article_search chunk + cache warm):
 *   ...&apply=1
 * Optional: &max_chunks=3&chunk_size=15000
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
set_time_limit(120);

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/shop/docpart/docpart_article_match.php';

$hostname = 'www.epartscart.com';
$_SERVER['HTTP_HOST'] = $hostname;
$cfg = new DP_Config();
epc_portal_apply_config($cfg);

if ((string) ($_GET['key'] ?? '') !== (string) $cfg->tech_key) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

$apply = !empty($_GET['apply']);
$maxChunks = max(1, min(10, (int) ($_GET['max_chunks'] ?? 2)));
$chunkSize = max(1000, min(25000, (int) ($_GET['chunk_size'] ?? 15000)));
$minKill = max(10, (int) ($_GET['min_time'] ?? 25));

$report = array(
	'ok' => true,
	'host' => $hostname,
	'db' => $cfg->db,
	'apply' => $apply,
	'load' => null,
	'target' => 'click_to_result_~1s',
	'processlist_long' => array(),
	'killed' => array(),
	'article_search' => array(),
	'timings' => array(),
	'probes' => array(),
	'hints' => array(),
);

if (function_exists('sys_getloadavg')) {
	$load = sys_getloadavg();
	if (is_array($load)) {
		$report['load'] = array('1m' => $load[0], '5m' => $load[1], '15m' => $load[2]);
		if ($load[0] > 8) {
			$report['hints'][] = 'Load is high — restart PHP-FPM in CloudPanel, then re-run with apply=1.';
		}
	}
}

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8;connect_timeout=5',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 8)
	);
} catch (Throwable $e) {
	exit(json_encode(array('ok' => false, 'error' => 'DB: ' . $e->getMessage()), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

try {
	$rows = $pdo->query('SHOW FULL PROCESSLIST')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as $row) {
		$time = (int) ($row['Time'] ?? 0);
		$info = (string) ($row['Info'] ?? '');
		$cmd = (string) ($row['Command'] ?? '');
		if ($time < $minKill && strtolower($cmd) !== 'query') {
			continue;
		}
		if ($time < 5) {
			continue;
		}
		$report['processlist_long'][] = array(
			'id' => (int) ($row['Id'] ?? 0),
			'user' => (string) ($row['User'] ?? ''),
			'command' => $cmd,
			'time' => $time,
			'state' => (string) ($row['State'] ?? ''),
			'info' => substr($info, 0, 200),
		);
		if ($apply && $time >= $minKill) {
			$id = (int) ($row['Id'] ?? 0);
			$user = (string) ($row['User'] ?? '');
			if ($id > 0 && !in_array($user, array('system user', 'event_scheduler'), true)) {
				$needle = stripos($info, 'shop_docpart') !== false
					|| stripos($info, 'ALTER') !== false
					|| stripos($info, 'information_schema') !== false
					|| strtolower($cmd) === 'sleep'
					|| $time >= 60;
				if ($needle) {
					try {
						$pdo->exec('KILL ' . $id);
						$report['killed'][] = $id;
					} catch (Throwable $e) {
						// ignore
					}
				}
			}
		}
	}
} catch (Throwable $e) {
	$report['processlist_error'] = $e->getMessage();
}

$t0 = microtime(true);
try {
	$empty = (int) $pdo->query(
		"SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `article_search` = '' OR `article_search` IS NULL"
	)->fetchColumn();
	$report['article_search']['empty_rows'] = $empty;
	$report['article_search']['count_ms'] = (int) round((microtime(true) - $t0) * 1000);
} catch (Throwable $e) {
	$report['article_search']['error'] = $e->getMessage();
}

if ($apply) {
	$total = 0;
	for ($i = 0; $i < $maxChunks; $i++) {
		$n = docpart_price_data_backfill_article_search($pdo, 0, $chunkSize);
		$total += $n;
		if ($n <= 0) {
			break;
		}
	}
	$report['article_search']['backfilled'] = $total;
	$report['article_search']['max_chunks'] = $maxChunks;
	$report['article_search']['chunk_size'] = $chunkSize;
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
		? 'SELECT p.`id`, p.`name`, COALESCE(p.`records_count`,0) AS c FROM `shop_docpart_prices` p ORDER BY p.`id`'
		: 'SELECT p.`id`, p.`name`, 0 AS c FROM `shop_docpart_prices` p ORDER BY p.`id`';
	$rows = $pdo->query($sql)->fetchAll(PDO::FETCH_ASSOC);
	$report['timings']['cp_prices_list_ms'] = (int) round((microtime(true) - $t1) * 1000);
	$report['timings']['cp_prices_rows'] = count($rows);
} catch (Throwable $e) {
	$report['timings']['cp_prices_list_error'] = $e->getMessage();
}

// Lightweight HTTP probes (origin via public URL).
$probeUrls = array(
	'storefront_en' => 'https://www.epartscart.com/en/',
	'cp_login' => 'https://www.epartscart.com/cp/',
	'laximo_api' => 'https://www.epartscart.com/api/laximo_proxy.php?action=sync_status',
);
foreach ($probeUrls as $label => $url) {
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_CONNECTTIMEOUT => 3,
		CURLOPT_TIMEOUT => 12,
		CURLOPT_SSL_VERIFYHOST => 0,
		CURLOPT_SSL_VERIFYPEER => 0,
		CURLOPT_USERAGENT => 'EPC-1s-speed/1.0',
	));
	$t = microtime(true);
	$body = curl_exec($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$ttfb = (float) curl_getinfo($ch, CURLINFO_STARTTRANSFER_TIME);
	curl_close($ch);
	$report['probes'][$label] = array(
		'http' => $code,
		'ttfb_ms' => (int) round($ttfb * 1000),
		'total_ms' => (int) round((microtime(true) - $t) * 1000),
		'bytes' => is_string($body) ? strlen($body) : 0,
		'ok_1s' => $ttfb > 0 && $ttfb <= 1.0 && $code >= 200 && $code < 400,
	);
}

if ($apply && function_exists('opcache_reset')) {
	@opcache_reset();
	$report['opcache_reset'] = true;
}

$report['hint'] = $apply
	? 'Applied kill/backfill. Restart PHP-FPM if load still high, then hard-refresh CP + search.'
	: 'Dry run. Pass apply=1 to kill long queries and backfill article_search chunks.';

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
