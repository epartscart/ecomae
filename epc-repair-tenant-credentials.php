<?php
/**
 * Repair tenant DB credentials — reset MySQL password and update registry.
 *
 * Usage:
 *   curl -sk "https://www.ecomae.com/epc-repair-tenant-credentials.php?token=...&site_key=spare247"
 *   curl -sk "https://www.ecomae.com/epc-repair-tenant-credentials.php?token=...&check_all=1"
 *
 * Requires MySQL root credentials in config.local.php['root_password']
 * or falls back to the platform user credentials.
 */
declare(strict_types=1);
if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
$epc_config_local = null;
if (!is_file($cfgFile)) {
	exit("Missing config.local.php\n");
}
include $cfgFile;
$platDb   = (string) ($epc_config_local['db'] ?? 'ecomae');
$platUser = (string) ($epc_config_local['user'] ?? 'ecomae');
$platPass = (string) ($epc_config_local['password'] ?? '');

$platformPdo = new PDO(
	'mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8mb4',
	$platUser, $platPass,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

$siteKey  = trim((string) ($_GET['site_key'] ?? ''));
$checkAll = !empty($_GET['check_all']);
$fix      = !empty($_GET['fix']);

if ($siteKey === '' && !$checkAll) {
	echo "Usage:\n";
	echo "  ?token=...&site_key=spare247        Check one tenant\n";
	echo "  ?token=...&check_all=1              Check all shared ERP tenants\n";
	echo "  ?token=...&site_key=spare247&fix=1  Generate new password & update registry + MySQL\n";
	exit;
}

$where = $checkAll
	? 'WHERE `erp_only_shared` = 1'
	: 'WHERE `site_key` = ' . $platformPdo->quote($siteKey);

$st = $platformPdo->query(
	'SELECT `site_key`, `db_name`, `db_user`, `db_password`, `trade_name`, `status`
	 FROM `epc_portal_tenants` ' . $where . ' ORDER BY `site_key`'
);
$tenants = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();

if (count($tenants) === 0) {
	echo "No matching tenants found.\n";
	exit;
}

foreach ($tenants as $row) {
	$sk   = (string) $row['site_key'];
	$db   = (string) $row['db_name'];
	$user = (string) $row['db_user'];
	$pass = (string) $row['db_password'];

	echo "--- {$sk} ({$row['trade_name']}) ---\n";
	echo "  DB: {$db}, User: {$user}\n";

	if ($db === '' || $user === '') {
		echo "  SKIP: missing db_name or db_user\n\n";
		continue;
	}

	// Test current credentials
	$ok = false;
	try {
		$tp = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8mb4',
			$user, $pass,
			array(PDO::ATTR_TIMEOUT => 5, PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$tp->query('SELECT 1');
		$ok = true;
		echo "  Status: OK (credentials valid)\n";
	} catch (Exception $e) {
		echo "  Status: FAIL — " . $e->getMessage() . "\n";
	}

	if ($ok) {
		echo "\n";
		continue;
	}

	if (!$fix) {
		echo "  To fix, add &fix=1 to reset the password.\n";
		echo "  Manual fix: mysql -u root -p -e \"ALTER USER '{$user}'@'localhost' IDENTIFIED BY 'NEW_PASSWORD';\"\n";
		echo "  Then update epc_portal_tenants.db_password for site_key='{$sk}'\n\n";
		continue;
	}

	// Generate a new secure password
	$newPass = bin2hex(random_bytes(12)); // 24 char hex
	echo "  Fixing: generating new password...\n";

	// Try to reset MySQL user password using platform credentials
	try {
		$rootPdo = new PDO(
			'mysql:host=127.0.0.1;charset=utf8mb4',
			$platUser, $platPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$rootPdo->exec("ALTER USER " . $rootPdo->quote($user . '@localhost') . " IDENTIFIED BY " . $rootPdo->quote($newPass));
		$rootPdo->exec("FLUSH PRIVILEGES");
		echo "  MySQL password reset: OK\n";
	} catch (Exception $e) {
		echo "  MySQL password reset FAILED: " . $e->getMessage() . "\n";
		echo "  You may need to run manually as root:\n";
		echo "    mysql -u root -p -e \"ALTER USER '{$user}'@'localhost' IDENTIFIED BY '{$newPass}'; FLUSH PRIVILEGES;\"\n";
		echo "  Then update the registry:\n";
		echo "    UPDATE epc_portal_tenants SET db_password='{$newPass}' WHERE site_key='{$sk}';\n\n";
		continue;
	}

	// Update registry
	try {
		$platformPdo->prepare(
			'UPDATE `epc_portal_tenants` SET `db_password` = ? WHERE `site_key` = ?'
		)->execute(array($newPass, $sk));
		echo "  Registry updated: OK\n";
	} catch (Exception $e) {
		echo "  Registry update FAILED: " . $e->getMessage() . "\n\n";
		continue;
	}

	// Verify new credentials
	try {
		$tp = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8mb4',
			$user, $newPass,
			array(PDO::ATTR_TIMEOUT => 5, PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$tp->query('SELECT 1');
		echo "  Verification: OK (new credentials work)\n";
	} catch (Exception $e) {
		echo "  Verification FAILED: " . $e->getMessage() . "\n";
	}
	echo "\n";
}

echo "Done.\n";
