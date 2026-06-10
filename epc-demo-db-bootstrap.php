<?php
/**
 * One-time: grant ecomae MySQL user CREATE privilege for demo DB provisioning.
 * https://www.ecomae.com/epc-demo-db-bootstrap.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$apply = !empty($_GET['apply']);
$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: ''));

define('_ASTEXE_', 1);
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

echo "=== Demo DB bootstrap ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";

$cfgFile = __DIR__ . '/config.local.php';
$dbUser = 'ecomae';
if (is_file($cfgFile)) {
	$epc_config_local = null;
	include $cfgFile;
	if (!empty($epc_config_local['user'])) {
		$dbUser = (string) $epc_config_local['user'];
	}
}
echo "platform_mysql_user={$dbUser}\n";

$sql = "GRANT CREATE, DROP ON *.* TO '{$dbUser}'@'localhost';"
	. " GRANT CREATE, DROP ON *.* TO '{$dbUser}'@'%';"
	. " FLUSH PRIVILEGES;";

$attempts = array(
	'mysql -e ' . escapeshellarg($sql),
	'sudo -n mysql -e ' . escapeshellarg($sql),
);

if ($clpPass !== '') {
	$cookie = '';
	$login = epc_clp_web_login('admin', $clpPass, $cookie);
	echo 'clp_web_login=' . (!empty($login['ok']) ? 'ok' : 'fail') . "\n";
}

foreach ($attempts as $cmd) {
	$out = epc_clp_run_cmd($cmd);
	echo $cmd . "\n" . $out['output'] . "\n";
	if ($out['code'] === 0) {
		echo "Grant applied via shell\n";
		exit("Done.\n");
	}
}

if (!$apply) {
	echo "\nDry run — re-run with apply=1&clp_pass=...\n";
	exit;
}

// Verify CREATE works
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
$testDb = 'demo_bootstrap_probe_' . date('Ymd');
$testUser = $testDb;
$testPass = bin2hex(random_bytes(8)) . 'A!';
$prov = epc_portal_demo_provision_database($testDb, $testUser, $testPass);
echo "probe provision ok=" . (!empty($prov['ok']) ? 'yes' : 'no') . "\n";
foreach ($prov['log'] ?? array() as $line) {
	echo $line . "\n";
}
if (!empty($prov['ok'])) {
	require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
	$pdo = epc_portal_platform_pdo();
	if ($pdo instanceof PDO) {
		epc_portal_demo_force_delete($pdo, $testDb);
	}
	echo "Probe DB cleaned up\n";
}
echo "Done.\n";
