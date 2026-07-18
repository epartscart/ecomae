<?php
/**
 * Repair tenant DB credentials — reset MySQL password and update registry.
 *
 * Usage:
 *   curl -sk "https://www.ecomae.com/epc-repair-tenant-credentials.php?token=...&site_key=spare247"
 *   curl -sk "https://www.ecomae.com/epc-repair-tenant-credentials.php?token=...&check_all=1"
 *   curl -sk "https://www.ecomae.com/epc-repair-tenant-credentials.php?token=...&site_key=spare247&fix=1"
 *
 * Fix strategies (in order):
 *   1. ALTER USER via platform MySQL (needs CREATE USER privilege)
 *   2. CloudPanel web UI create/reset (needs valid config.demo-clp.php)
 *   3. Claim a ready demo DB pool slot and re-point registry
 */
declare(strict_types=1);
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
$epc_config_local = null;
if (!is_file($cfgFile)) {
	exit("Missing config.local.php\n");
}
include $cfgFile;
$platDb = (string) ($epc_config_local['db'] ?? 'ecomae');
$platUser = (string) ($epc_config_local['user'] ?? 'ecomae');
$platPass = (string) ($epc_config_local['password'] ?? '');

$platformPdo = new PDO(
	'mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8mb4',
	$platUser,
	$platPass,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$siteKey = trim((string) ($_GET['site_key'] ?? ''));
$checkAll = !empty($_GET['check_all']);
$fix = !empty($_GET['fix']);
$allowPool = !isset($_GET['allow_pool']) || (string) $_GET['allow_pool'] !== '0';

if ($siteKey === '' && !$checkAll) {
	echo "Usage:\n";
	echo "  ?token=...&site_key=spare247        Check one tenant\n";
	echo "  ?token=...&check_all=1              Check all shared ERP tenants\n";
	echo "  ?token=...&site_key=spare247&fix=1  Reset / reprovision credentials\n";
	echo "  Optional: allow_pool=0 to skip demo pool claim fallback\n";
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

/**
 * @return array{ok:bool,message:string}
 */
function epc_repair_try_mysql_alter(string $platUser, string $platPass, string $user, string $newPass): array
{
	try {
		$rootPdo = new PDO(
			'mysql:host=127.0.0.1;charset=utf8mb4',
			$platUser,
			$platPass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$userEsc = str_replace('`', '', $user);
		$passEsc = str_replace("'", "''", $newPass);
		try {
			$rootPdo->exec("ALTER USER '{$userEsc}'@'localhost' IDENTIFIED BY '{$passEsc}'");
		} catch (Exception $e) {
			$rootPdo->exec("CREATE USER IF NOT EXISTS '{$userEsc}'@'localhost' IDENTIFIED BY '{$passEsc}'");
			$rootPdo->exec("ALTER USER '{$userEsc}'@'localhost' IDENTIFIED BY '{$passEsc}'");
		}
		$rootPdo->exec('FLUSH PRIVILEGES');
		return array('ok' => true, 'message' => 'ALTER USER OK');
	} catch (Exception $e) {
		return array('ok' => false, 'message' => $e->getMessage());
	}
}

/**
 * @return array{ok:bool,message:string,db_name?:string}
 */
function epc_repair_try_clp_web(string $dbName, string $dbUser, string $dbPass): array
{
	$clpPass = epc_portal_demo_clp_password();
	if ($clpPass === '') {
		return array('ok' => false, 'message' => 'CloudPanel password not configured');
	}
	$cookie = '';
	$login = epc_clp_web_login('admin', $clpPass, $cookie);
	if (empty($login['ok'])) {
		return array('ok' => false, 'message' => 'CloudPanel web login failed');
	}
	$web = epc_clp_web_add_database($cookie, 'www.ecomae.com', $dbName, $dbUser, $dbPass);
	if (!empty($web['ok']) || epc_portal_demo_db_exists($dbName, $dbUser, $dbPass)) {
		return array('ok' => true, 'message' => 'CloudPanel web DB OK', 'db_name' => $dbName);
	}
	$hint = implode('; ', array_slice((array) ($web['log'] ?? array()), 0, 4));
	return array('ok' => false, 'message' => 'CloudPanel web DB failed: ' . $hint);
}

foreach ($tenants as $row) {
	$sk = (string) $row['site_key'];
	$db = (string) $row['db_name'];
	$user = (string) $row['db_user'];
	$pass = (string) $row['db_password'];

	echo "--- {$sk} ({$row['trade_name']}) ---\n";
	echo "  DB: {$db}, User: {$user}\n";

	if ($db === '' || $user === '') {
		echo "  SKIP: missing db_name or db_user\n\n";
		continue;
	}

	$ok = false;
	try {
		$tp = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8mb4',
			$user,
			$pass,
			array(PDO::ATTR_TIMEOUT => 5, PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$tp->query('SELECT 1');
		$ok = true;
		echo "  Status: OK (credentials valid)\n";
	} catch (Exception $e) {
		echo '  Status: FAIL — ' . $e->getMessage() . "\n";
	}

	if ($ok) {
		echo "\n";
		continue;
	}

	if (!$fix) {
		echo "  To fix, add &fix=1 (tries ALTER USER → CloudPanel → demo pool claim).\n\n";
		continue;
	}

	$newPass = bin2hex(random_bytes(12));
	echo "  Fixing...\n";

	$mysqlFix = epc_repair_try_mysql_alter($platUser, $platPass, $user, $newPass);
	if (!empty($mysqlFix['ok'])) {
		echo '  MySQL password reset: ' . $mysqlFix['message'] . "\n";
		$platformPdo->prepare(
			'UPDATE `epc_portal_tenants` SET `db_password` = ? WHERE `site_key` = ?'
		)->execute(array($newPass, $sk));
		echo "  Registry updated: OK\n";
		try {
			$tp = new PDO(
				'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8mb4',
				$user,
				$newPass,
				array(PDO::ATTR_TIMEOUT => 5, PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			$tp->query('SELECT 1');
			echo "  Verification: OK\n\n";
		} catch (Exception $e) {
			echo '  Verification FAILED: ' . $e->getMessage() . "\n\n";
		}
		continue;
	}
	echo '  MySQL ALTER/CREATE failed: ' . $mysqlFix['message'] . "\n";

	$clpFix = epc_repair_try_clp_web($db, $user, $newPass);
	if (!empty($clpFix['ok'])) {
		echo '  CloudPanel: ' . $clpFix['message'] . "\n";
		$platformPdo->prepare(
			'UPDATE `epc_portal_tenants` SET `db_password` = ? WHERE `site_key` = ?'
		)->execute(array($newPass, $sk));
		echo "  Registry updated: OK\n";
		echo "  Verification: " . (epc_portal_demo_db_exists($db, $user, $newPass) ? 'OK' : 'FAIL') . "\n\n";
		continue;
	}
	echo '  CloudPanel failed: ' . $clpFix['message'] . "\n";

	if (!$allowPool) {
		echo "  Pool claim skipped (allow_pool=0). Manual CloudPanel DB create required.\n\n";
		continue;
	}

	$claimed = epc_portal_demo_pool_claim($platformPdo, $sk);
	if ($claimed === null) {
		echo "  Pool claim FAILED — no ready demo DB (run epc-demo-pool-seed.php).\n\n";
		continue;
	}
	$platformPdo->prepare(
		'UPDATE `epc_portal_tenants`
		 SET `db_name` = ?, `db_user` = ?, `db_password` = ?,
		     `notes` = CONCAT(IFNULL(`notes`, \'\'), ?), `updated_at` = ?
		 WHERE `site_key` = ?'
	)->execute(array(
		(string) $claimed['db_name'],
		(string) $claimed['db_user'],
		(string) $claimed['db_password'],
		' | credential-repair pool-claim ' . date('Y-m-d H:i') . ' (was ' . $db . ')',
		time(),
		$sk,
	));
	echo '  Pool claim: OK db=' . $claimed['db_name'] . ' user=' . $claimed['db_user']
		. ' pool_id=' . (int) ($claimed['pool_id'] ?? 0) . "\n";
	echo '  Registry re-pointed. Run epc-erp-tenant-provision.php?site_key=' . rawurlencode($sk)
		. "&apply=1 to clone ERP schema if tables are empty.\n\n";
}

echo "Done.\n";
