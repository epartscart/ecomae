<?php
/**
 * Commerce Isolation Audit — comprehensive tenant data isolation check.
 *
 * Runs 5 checks across the shared docpart DB and tenant registry:
 *  1. ERP DB isolation (dedicated DBs for shared ERP tenants)
 *  2. Price ID ownership (no cross-tenant price_id sharing)
 *  3. Orphan price data (rows with no parent price list)
 *  4. Query scoping (static analysis of PHP files)
 *  5. Registry credentials (connection tests)
 *
 * Usage:
 *   curl -sk "https://www.ecomae.com/epc-commerce-isolation-audit.php?token=epartscart-deploy-2026"
 *   curl -sk "https://www.ecomae.com/epc-commerce-isolation-audit.php?token=epartscart-deploy-2026&format=json"
 *
 * @since PR #35 — P0 #1 commerce site_key isolation audit
 */
declare(strict_types=1);

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

$format = (string) ($_GET['format'] ?? 'text');
if ($format === 'json') {
	header('Content-Type: application/json; charset=utf-8');
} else {
	header('Content-Type: text/plain; charset=utf-8');
}

// Load platform DB config
$cfgFile = __DIR__ . '/config.local.php';
if (!is_file($cfgFile)) {
	$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
}
$epc_config_local = null;
if (!is_file($cfgFile)) {
	$msg = 'Missing config.local.php';
	echo $format === 'json' ? json_encode(array('error' => $msg)) : $msg;
	exit;
}
include $cfgFile;

$platDb = (string) ($epc_config_local['db'] ?? 'ecomae');
$platUser = (string) ($epc_config_local['user'] ?? 'ecomae');
$platPass = (string) ($epc_config_local['password'] ?? '');

try {
	$platformPdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8mb4',
		$platUser,
		$platPass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 10)
	);
} catch (Exception $e) {
	$msg = 'Platform DB connection failed: ' . $e->getMessage();
	echo $format === 'json' ? json_encode(array('error' => $msg)) : $msg;
	exit;
}

// Resolve commerce DB (docpart) credentials
$commerceDb = 'docpart';
$commerceUser = $platUser;
$commercePass = $platPass;

// Try to read docpart credentials from the DB config or config file
if (isset($epc_config_local['commerce_db'])) {
	$commerceDb = (string) $epc_config_local['commerce_db'];
}
if (isset($epc_config_local['commerce_user'])) {
	$commerceUser = (string) $epc_config_local['commerce_user'];
}
if (isset($epc_config_local['commerce_password'])) {
	$commercePass = (string) $epc_config_local['commerce_password'];
}

$commercePdo = null;
try {
	$commercePdo = new PDO(
		'mysql:host=127.0.0.1;dbname=' . $commerceDb . ';charset=utf8mb4',
		$commerceUser,
		$commercePass,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 10)
	);
} catch (Exception $e) {
	// Commerce DB might be the same as platform DB
	try {
		$commercePdo = new PDO(
			'mysql:host=127.0.0.1;dbname=docpart;charset=utf8mb4',
			$platUser,
			$platPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_TIMEOUT => 10)
		);
	} catch (Exception $e2) {
		// Fall back to platform DB if docpart doesn't exist as separate DB
		$commercePdo = $platformPdo;
	}
}

require_once __DIR__ . '/content/general_pages/epc_commerce_isolation.php';

$results = epc_ci_run_full_audit($platformPdo, $commercePdo);

if ($format === 'json') {
	echo json_encode($results, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

// Text output
echo "╔══════════════════════════════════════════════════════════════╗\n";
echo "║       COMMERCE ISOLATION AUDIT — " . $results['timestamp'] . "       ║\n";
echo "║       Overall: " . str_pad($results['overall'], 44) . "║\n";
echo "╚══════════════════════════════════════════════════════════════╝\n\n";

echo "Summary: " . $results['summary']['passed'] . " passed, "
	. $results['summary']['failed'] . " failed, "
	. $results['summary']['warnings'] . " warnings\n\n";

foreach ($results['checks'] as $key => $check) {
	$icon = $check['status'] === 'PASS' ? '[OK]' : ($check['status'] === 'WARN' ? '[!!]' : '[FAIL]');
	echo $icon . ' ' . $check['name'] . "\n";
	if (isset($check['message'])) {
		echo "    " . $check['message'] . "\n";
	}
	if (isset($check['error'])) {
		echo "    Error: " . $check['error'] . "\n";
	}
	if (isset($check['details']) && is_array($check['details'])) {
		foreach ($check['details'] as $d) {
			if (is_array($d)) {
				$ok = $d['ok'] ?? null;
				$prefix = $ok === true ? '  OK   ' : ($ok === false ? '  FAIL ' : '  ');
				echo $prefix . json_encode($d, JSON_UNESCAPED_UNICODE) . "\n";
			}
		}
	}
	if (isset($check['unscoped']) && is_array($check['unscoped'])) {
		foreach ($check['unscoped'] as $u) {
			$risk = $u['risk'] ?? 'unknown';
			echo "  [{$risk}] {$u['file']}" . ($u['admin'] ? ' (admin script)' : '') . "\n";
		}
	}
	echo "\n";
}

echo "Done.\n";
