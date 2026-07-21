<?php
/**
 * Cron / manual: refresh shop_currencies rates from live FX vs main currency.
 *
 * Example:
 *   curl -sk "https://www.epartscart.com/epc-currency-live-rates-cron.php?token=epartscart-deploy-2026"
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
	echo "OK dry-run base=" . $preview['base_alpha'] . " provider=" . $preview['provider'] . " as_of=" . $preview['as_of'] . "\n";
	foreach ($preview['rows'] as $row) {
		$live = $row['has_live'] ? (string) $row['live_rate'] : 'n/a';
		echo $row['iso_name'] . "\tcurrent=" . $row['current_rate'] . "\tlive=" . $live
			. "\tdiff%=" . ($row['diff_pct'] === null ? 'n/a' : $row['diff_pct'])
			. ($row['is_main'] ? "\tMAIN" : '') . "\n";
	}
	exit;
}

$out = epc_currency_live_apply($db, $DP_Config, null);
if (!$out['ok']) {
	exit("FAIL apply: " . $out['error'] . "\n");
}
echo "OK updated=" . $out['updated'] . " skipped=" . $out['skipped']
	. " provider=" . $out['provider'] . " as_of=" . $out['as_of'] . "\n";
foreach ($out['rows'] as $row) {
	echo $row['iso_name'] . "\t" . ($row['applied_rate'] ?? $row['live_rate']) . "\n";
}
