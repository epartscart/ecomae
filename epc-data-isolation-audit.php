<?php
/**
 * Production audit — shared ERP tenant isolation and optional user lookup.
 * https://www.ecomae.com/epc-data-isolation-audit.php?token=...&user_id=19
 * https://www.ecomae.com/epc-data-isolation-audit.php?token=...&audit_all=1
 */
declare(strict_types=1);
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
	'mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8',
	$platUser,
	$platPass,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

$auditAll = !empty($_GET['audit_all']);
$userId = isset($_GET['user_id']) ? (int) $_GET['user_id'] : 0;

echo "=== Shared ERP tenant registry ===\n";
$st = $platformPdo->query(
	'SELECT site_key, db_name, db_user, trade_name, erp_only_shared, status
	 FROM epc_portal_tenants
	 WHERE erp_only_shared = 1
	 ORDER BY site_key'
);
$tenants = $st ? $st->fetchAll(PDO::FETCH_ASSOC) : array();
foreach ($tenants as $row) {
	$bad = in_array((string) $row['db_name'], array('docpart', 'ecomae', ''), true);
	echo ($bad ? 'FAIL ' : 'OK   ') . json_encode($row, JSON_UNESCAPED_UNICODE) . "\n";
	if (!$auditAll && (string) $row['site_key'] !== 'asap') {
		continue;
	}
	$db = (string) $row['db_name'];
	$u = (string) $row['db_user'];
	$pwSt = $platformPdo->prepare('SELECT db_password FROM epc_portal_tenants WHERE site_key = ? LIMIT 1');
	$pwSt->execute(array($row['site_key']));
	$pw = (string) $pwSt->fetchColumn();
	if ($db === '' || $u === '' || $pw === '') {
		echo "  skip stats — incomplete credentials\n";
		continue;
	}
	try {
		$tp = new PDO('mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8', $u, $pw);
		$stats = array();
		foreach (array('shop_orders', 'epc_crm_opportunities', 'users') as $tbl) {
			try {
				$stats[$tbl] = (int) $tp->query('SELECT COUNT(*) FROM `' . $tbl . '`')->fetchColumn();
			} catch (Exception $e) {
				$stats[$tbl] = -1;
			}
		}
		echo '  stats ' . json_encode($stats) . "\n";
		if ($userId > 0) {
			$ust = $tp->prepare('SELECT user_id, email, phone FROM users WHERE user_id = ? LIMIT 1');
			$ust->execute(array($userId));
			$urow = $ust->fetch(PDO::FETCH_ASSOC);
			echo '  user#' . $userId . ' in ' . $db . ': ' . json_encode($urow ?: 'NOT_FOUND') . "\n";
		}
	} catch (Exception $e) {
		echo '  DB error: ' . $e->getMessage() . "\n";
	}
}

if ($userId > 0) {
	echo "\n=== User #{$userId} in docpart (must NOT be ASAP ERP session source) ===\n";
	try {
		require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
		$doc = epc_portal_resolve_tenant_db_credentials();
		$dp = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $doc['db'] . ';charset=utf8',
			$doc['user'],
			$doc['password']
		);
		$ust = $dp->prepare('SELECT user_id, email FROM users WHERE user_id = ? LIMIT 1');
		$ust->execute(array($userId));
		echo json_encode($ust->fetch(PDO::FETCH_ASSOC) ?: 'NOT_FOUND') . "\n";
	} catch (Exception $e) {
		echo $e->getMessage() . "\n";
	}
	echo "\n=== User #{$userId} in platform {$platDb} ===\n";
	try {
		$ust = $platformPdo->prepare('SELECT user_id, email FROM users WHERE user_id = ? LIMIT 1');
		$ust->execute(array($userId));
		echo json_encode($ust->fetch(PDO::FETCH_ASSOC) ?: 'NOT_FOUND') . "\n";
	} catch (Exception $e) {
		echo $e->getMessage() . "\n";
	}
}

echo "\nDone.\n";
