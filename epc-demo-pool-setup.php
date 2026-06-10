<?php
/**
 * Pre-create demo DB slots via CloudPanel web UI (run once with clp_pass).
 * https://www.ecomae.com/epc-demo-pool-setup.php?token=...&clp_pass=...&apply=1&slots=30
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

$apply = !empty($_GET['apply']);
$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: ''));
$slots = max(1, min(30, (int) ($_GET['slots'] ?? 30)));

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

echo "=== Demo DB pool setup ({$slots} slots) ===\n";
if ($clpPass === '') {
	exit("clp_pass required\n");
}
if (!$apply) {
	echo "Dry run — pass apply=1\n";
	exit;
}

$cookie = '';
$login = epc_clp_web_login('admin', $clpPass, $cookie);
if (empty($login['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login OK\n";

$created = 0;
for ($i = 1; $i <= $slots; $i++) {
	$dbName = sprintf('demo_slot_%02d', $i);
	$dbUser = $dbName;
	$dbPass = bin2hex(random_bytes(8)) . 'A!';
	if (epc_portal_demo_db_exists($dbName, $dbUser, $dbPass)) {
		echo "skip {$dbName} — exists\n";
		$created++;
		continue;
	}
	$webDb = epc_clp_web_add_database($cookie, 'www.ecomae.com', $dbName, $dbUser, $dbPass);
	echo $dbName . ': ' . implode(' | ', $webDb['log'] ?? array()) . "\n";
	sleep(3);
	if (epc_portal_demo_db_exists($dbName, $dbUser, $dbPass)) {
		$created++;
		echo "  -> verified\n";
	}
}
echo "\nVerified slots: {$created}/{$slots}\nDone.\n";
