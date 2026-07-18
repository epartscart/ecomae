<?php
/**
 * Public beacon ingest for website tracker (storefront + marketing).
 * POST JSON body or text/plain sendBeacon payload.
 */
declare(strict_types=1);

define('EPC_WEB_TRACKER_STANDALONE', 1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
header('X-Content-Type-Options: nosniff');

// Same-origin preferred; allow simple POST from tenant hosts on this stack.
if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
	header('Access-Control-Allow-Methods: POST, OPTIONS');
	header('Access-Control-Allow-Headers: Content-Type');
	http_response_code(204);
	exit;
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
	http_response_code(405);
	echo json_encode(array('ok' => false, 'error' => 'POST required'));
	exit;
}

$raw = file_get_contents('php://input');
if ($raw === false || $raw === '') {
	$raw = (string) ($_POST['payload'] ?? '');
}
$payload = json_decode($raw, true);
if (!is_array($payload)) {
	http_response_code(400);
	echo json_encode(array('ok' => false, 'error' => 'bad_json'));
	exit;
}

// Soft rate limit by IP (file-based, best-effort).
$ip = $_SERVER['REMOTE_ADDR'] ?? '0';
$rlDir = sys_get_temp_dir() . '/epc_wt_rl';
if (!is_dir($rlDir)) {
	@mkdir($rlDir, 0700, true);
}
$rlFile = $rlDir . '/' . hash('sha256', (string) $ip) . '.cnt';
$now = time();
$bucket = array('t' => $now, 'n' => 0);
if (is_file($rlFile)) {
	$prev = json_decode((string) @file_get_contents($rlFile), true);
	if (is_array($prev) && (int) ($prev['t'] ?? 0) >= ($now - 60)) {
		$bucket = array('t' => (int) $prev['t'], 'n' => (int) ($prev['n'] ?? 0));
	}
}
$bucket['n']++;
@file_put_contents($rlFile, json_encode($bucket), LOCK_EX);
if ($bucket['n'] > 120) {
	http_response_code(429);
	echo json_encode(array('ok' => false, 'error' => 'rate_limited'));
	exit;
}

require_once __DIR__ . '/content/general_pages/epc_web_tracker.php';

$pdo = null;
try {
	if (is_file(__DIR__ . '/content/general_pages/epc_portal.php')) {
		require_once __DIR__ . '/content/general_pages/epc_portal.php';
	}
	if (function_exists('epc_portal_platform_pdo')) {
		$pdo = epc_portal_platform_pdo();
	}
	if (!$pdo instanceof PDO) {
		require_once __DIR__ . '/config.php';
		$cfg = new DP_Config();
		if (is_file(__DIR__ . '/config.local.php')) {
			$epc_config_local = null;
			require __DIR__ . '/config.local.php';
			if (isset($epc_config_local) && is_array($epc_config_local)) {
				foreach ($epc_config_local as $k => $v) {
					if (property_exists($cfg, $k)) {
						$cfg->$k = $v;
					}
				}
			}
		}
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	}
} catch (Throwable $e) {
	http_response_code(503);
	echo json_encode(array('ok' => false, 'error' => 'db'));
	exit;
}

try {
	$result = epc_web_tracker_ingest($pdo, $payload);
	echo json_encode($result);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array('ok' => false, 'error' => 'ingest_failed'));
}
