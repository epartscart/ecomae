<?php
/**
 * Cron / manual: refresh shop_currencies rates from live FX vs main currency.
 *
 * Modes:
 *   (default / schedule=1) — run only when nightly auto-update is due
 *   force=1                — apply immediately (ignore schedule window)
 *   dry=1                  — preview only, no DB writes
 *
 * Examples:
 *   # Safe for every-minute hosting cron (once per night when due):
 *   curl -sk "https://www.epartscart.com/epc-currency-live-rates-cron.php?token=epartscart-deploy-2026"
 *   # Dedicated nightly crontab (also works with schedule window):
 *   0 2 * * * curl -sk "https://www.epartscart.com/epc-currency-live-rates-cron.php?token=epartscart-deploy-2026"
 *   # Force now:
 *   curl -sk "https://www.epartscart.com/epc-currency-live-rates-cron.php?token=...&force=1"
 *   # Dry-run:
 *   curl -sk "https://www.epartscart.com/epc-currency-live-rates-cron.php?token=...&dry=1"
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');

$token = (string) ($_GET['token'] ?? $_POST['token'] ?? '');
$expected = getenv('EPC_DEPLOY_TOKEN') ?: 'epartscart-deploy-2026';
if ($token === '' || !hash_equals($expected, $token)) {
	http_response_code(403);
	exit("Forbidden\n");
}

$docRoot = rtrim((string) ($_SERVER['DOCUMENT_ROOT'] ?? dirname(__FILE__)), '/\\');
require_once $docRoot . '/config.php';
$DP_Config = new DP_Config();
$GLOBALS['DP_Config'] = $DP_Config;

$dbHost = trim((string) $DP_Config->host);
if ($dbHost === '' || strtolower($dbHost) === 'localhost') {
	$dbHost = '127.0.0.1';
}

try {
	$db = new PDO(
		'mysql:host=' . $dbHost . ';dbname=' . $DP_Config->db . ';charset=utf8mb4',
		$DP_Config->user,
		$DP_Config->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	http_response_code(503);
	exit("DB error\n");
}

require_once $docRoot . '/content/shop/finance/epc_currency_live_rates.php';

$dry = !empty($_GET['dry']) || !empty($_POST['dry']);
if ($dry) {
	$preview = epc_currency_live_preview($db, $DP_Config);
	if (!$preview['ok']) {
		exit("FAIL preview: " . $preview['error'] . "\n");
	}
	$sched = epc_currency_live_schedule_get($db);
	echo "OK dry-run base=" . $preview['base_alpha'] . " provider=" . $preview['provider'] . " as_of=" . $preview['as_of'] . "\n";
	echo "schedule enabled=" . $sched['enabled'] . " tz=" . $sched['timezone'] . " hour=" . $sched['hour']
		. " due=" . ($sched['due'] ? '1' : '0') . " local_now=" . $sched['local_now'] . "\n";
	foreach ($preview['rows'] as $row) {
		$live = $row['has_live'] ? (string) $row['live_rate'] : 'n/a';
		echo $row['iso_name'] . "\tcurrent=" . $row['current_rate'] . "\tlive=" . $live
			. "\tdiff%=" . ($row['diff_pct'] === null ? 'n/a' : $row['diff_pct'])
			. ($row['is_main'] ? "\tMAIN" : '') . "\n";
	}
	exit;
}

$force = !empty($_GET['force']) || !empty($_POST['force']);
// Backward-compat: apply=1 also forces an immediate apply.
if (!$force && (!empty($_GET['apply']) || !empty($_POST['apply']))) {
	$force = true;
}

$tick = epc_currency_live_schedule_tick($db, $DP_Config, $force);
$sched = $tick['schedule'] ?? epc_currency_live_schedule_get($db);

if (!empty($tick['skipped'])) {
	echo "OK skipped reason=" . ($tick['reason'] ?? 'n/a')
		. " enabled=" . (int) ($sched['enabled'] ?? 0)
		. " tz=" . ($sched['timezone'] ?? '')
		. " hour=" . (int) ($sched['hour'] ?? 0)
		. " local_now=" . ($sched['local_now'] ?? '')
		. " next=" . ($sched['next_window'] ?? '')
		. "\n";
	exit;
}

if (empty($tick['ok'])) {
	exit("FAIL apply: " . ($tick['error'] ?? 'unknown') . "\n");
}

echo "OK ran reason=" . ($tick['reason'] ?? '')
	. " updated=" . (int) ($tick['updated'] ?? 0)
	. " provider=" . ($tick['provider'] ?? '')
	. " as_of=" . ($tick['as_of'] ?? '')
	. "\n";
