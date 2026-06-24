<?php
/**
 * Weekly commerce isolation audit — designed for cron execution.
 *
 * Cron setup (run every Sunday at 3 AM server time):
 *   0 3 * * 0 cd /home/ecomae/htdocs/www.ecomae.com && php epc-weekly-isolation-audit.php >> /var/log/epc-isolation-audit.log 2>&1
 *
 * Can also be triggered via HTTP:
 *   curl -sk "https://www.ecomae.com/epc-weekly-isolation-audit.php?token=..."
 *
 * Results are saved to epc_ci_audit_runs table for BOS trend tracking.
 */
declare(strict_types=1);
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }

// CLI mode: no token needed. HTTP mode: require deploy token.
if (PHP_SAPI !== 'cli') {
	require_once __DIR__ . '/epc_deploy_auth.php';
	epc_deploy_require_token();
}

header('Content-Type: text/plain; charset=utf-8');

echo "=== ecomae Weekly Isolation Audit ===\n";
echo "Started: " . date('Y-m-d H:i:s') . "\n\n";

$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
if (PHP_SAPI === 'cli' && !is_file($cfgFile)) {
	$cfgFile = __DIR__ . '/config.local.php';
}
$epc_config_local = null;
if (!is_file($cfgFile)) {
	echo "ERROR: Missing config.local.php\n";
	exit(1);
}
include $cfgFile;
$platDb   = (string) ($epc_config_local['db'] ?? 'ecomae');
$platUser = (string) ($epc_config_local['user'] ?? 'ecomae');
$platPass = (string) ($epc_config_local['password'] ?? '');

try {
	$platformPdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8mb4',
		$platUser, $platPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	echo "ERROR: Platform DB connection failed: " . $e->getMessage() . "\n";
	exit(1);
}

$commercePdo = $platformPdo;
try {
	$cDb = (string) ($epc_config_local['commerce_db'] ?? 'docpart');
	$commercePdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $cDb . ';charset=utf8mb4',
		$platUser, $platPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 10)
	);
} catch (Exception $e) {
	echo "WARNING: Commerce DB connection failed, using platform PDO: " . $e->getMessage() . "\n";
}

require_once __DIR__ . '/content/general_pages/epc_commerce_isolation.php';

$results = epc_ci_run_full_audit($platformPdo, $commercePdo);

// Display results
echo "Overall: " . $results['overall'] . "\n";
echo "Summary: " . $results['summary']['passed'] . " passed, "
	. $results['summary']['failed'] . " failed, "
	. $results['summary']['warnings'] . " warnings\n\n";

foreach ($results['checks'] as $key => $check) {
	$icon = $check['status'] === 'PASS' ? '[OK]  ' : ($check['status'] === 'WARN' ? '[WARN]' : '[FAIL]');
	echo $icon . ' ' . $check['name'] . "\n";
	if (isset($check['message'])) {
		echo "       " . $check['message'] . "\n";
	}
	if (isset($check['error'])) {
		echo "       Error: " . $check['error'] . "\n";
	}
	if (!empty($check['details'])) {
		foreach ($check['details'] as $detail) {
			if (is_array($detail) && isset($detail['ok']) && !$detail['ok']) {
				echo "       FAIL: " . ($detail['site_key'] ?? '') . ' — ' . ($detail['error'] ?? 'unknown') . "\n";
			}
		}
	}
	echo "\n";
}

// Alert on failures
if ($results['overall'] === 'FAIL') {
	echo "*** ALERT: Isolation audit FAILED — immediate attention required ***\n";
	echo "Check the BOS Isolation Audit panel for details: https://www.ecomae.com/bos/?m=isolation_audit\n";
	// Future: send email/webhook notification here
}

echo "Completed: " . date('Y-m-d H:i:s') . "\n";
echo "Done.\n";

exit($results['overall'] === 'FAIL' ? 1 : 0);
