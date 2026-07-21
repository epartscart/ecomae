<?php
/**
 * Isolate a live CLIENT tenant off shared Model C `docpart` onto a dedicated MySQL.
 *
 * Critical privacy fix: taxofinca (and other non–eParts tenants) were binding the
 * epartscart `docpart` DB, so spare-parts orders + bank rows appeared in their CP/ERP.
 *
 * Flow:
 *  1) Create dedicated DB named after site_key (CloudPanel)
 *  2) Copy SCHEMA (+ optional seed) from docpart
 *  3) DELETE transactional commerce/finance rows (orders, bank, ledgers, …)
 *  4) Update registry: dedicated_db=1, scale_policy=dedicated_mysql, hosted_on=client
 *
 * Dry run:
 *   curl -sk "https://www.ecomae.com/epc-client-tenant-db-isolate.php?token=...&site_key=taxofinca"
 *
 * Apply:
 *   curl -sk "https://www.ecomae.com/epc-client-tenant-db-isolate.php?token=...&site_key=taxofinca&apply=1"
 *
 * All non–eParts client tenants:
 *   curl -sk "https://www.ecomae.com/epc-client-tenant-db-isolate.php?token=...&all_clients=1&apply=1"
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(900);

require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant_control.php';
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$apply = !empty($_GET['apply']) || !empty($_POST['apply']);
$allClients = !empty($_GET['all_clients']) || !empty($_POST['all_clients']);
$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim((string) ($_GET['site_key'] ?? $_POST['site_key'] ?? ''))));
$clpPass = trim((string) ($_GET['clp_pass'] ?? $_POST['clp_pass'] ?? ''));
$dbPassOverride = trim((string) ($_GET['db_password'] ?? $_POST['db_password'] ?? ''));

echo "=== Client tenant DB isolation (off shared docpart) ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . ' all_clients=' . ($allClients ? 'yes' : 'no') . "\n\n";

function epc_cti_platform_pdo(): PDO
{
	$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
	$epc_config_local = null;
	include $cfgFile;
	return new PDO(
		'mysql:host=127.0.0.1;dbname=' . ($epc_config_local['db'] ?? 'ecomae') . ';charset=utf8mb4',
		(string) ($epc_config_local['user'] ?? 'ecomae'),
		(string) ($epc_config_local['password'] ?? ''),
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

function epc_cti_connects(string $db, string $user, string $pass): bool
{
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8mb4',
			$user,
			$pass,
			array(PDO::ATTR_TIMEOUT => 5, PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		$pdo->query('SELECT 1');
		return true;
	} catch (Throwable $e) {
		return false;
	}
}

function epc_cti_table_count(string $db, string $user, string $pass): int
{
	try {
		$pdo = new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8mb4',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
		return (int) $pdo->query('SHOW TABLES')->rowCount();
	} catch (Throwable $e) {
		return -1;
	}
}

function epc_cti_pdo(string $db, string $user, string $pass): ?PDO
{
	try {
		return new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8mb4',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		return null;
	}
}

/** @return list<string> */
function epc_cti_transactional_tables(): array
{
	return array(
		'shop_orders',
		'shop_orders_items',
		'shop_orders_statuses',
		'shop_orders_messages',
		'shop_orders_payments',
		'shop_carts',
		'shop_carts_items',
		'epc_erp_cash_bank_accounts',
		'epc_erp_cash_bank_entries',
		'epc_erp_bank_accounts',
		'epc_erp_bank_transactions',
		'epc_erp_bank_statements',
		'epc_erp_invoices',
		'epc_erp_invoice_lines',
		'epc_erp_purchase_orders',
		'epc_erp_purchase_order_lines',
		'epc_erp_journal_entries',
		'epc_erp_journal_lines',
		'epc_erp_supplier_invoices',
		'epc_erp_customer_ledger',
		'epc_erp_supplier_ledger',
		'epc_erp_vat_returns',
		'epc_crm_leads',
		'epc_crm_opportunities',
		'epc_crm_activities',
		'shop_docpart_prices_data',
		'shop_docpart_prices',
		'shop_storages_data',
	);
}

function epc_cti_clear_transactional(PDO $pdo): array
{
	$cleared = array();
	$pdo->exec('SET FOREIGN_KEY_CHECKS=0');
	foreach (epc_cti_transactional_tables() as $tbl) {
		$safe = str_replace('`', '', $tbl);
		try {
			$n = $pdo->exec('DELETE FROM `' . $safe . '`');
			$cleared[$safe] = is_int($n) ? $n : 0;
		} catch (Throwable $e) {
			// table may not exist on this schema version
		}
	}
	$pdo->exec('SET FOREIGN_KEY_CHECKS=1');
	return $cleared;
}

function epc_cti_count_orders(PDO $pdo): int
{
	try {
		return (int) $pdo->query('SELECT COUNT(*) FROM `shop_orders`')->fetchColumn();
	} catch (Throwable $e) {
		return -1;
	}
}

function epc_cti_count_bank(PDO $pdo): int
{
	foreach (array('epc_erp_cash_bank_accounts', 'epc_erp_bank_accounts') as $tbl) {
		try {
			return (int) $pdo->query('SELECT COUNT(*) FROM `' . $tbl . '`')->fetchColumn();
		} catch (Throwable $e) {
		}
	}
	return -1;
}

function epc_cti_schema_migrate(string $srcDb, string $destDb, string $destUser, string $destPass): array
{
	$log = array();
	// Prefer schema-only dump so we never import live epartscart order/bank rows.
	$cmd = 'mysqldump --single-transaction --no-data --routines --triggers '
		. escapeshellarg($srcDb)
		. ' 2>/dev/null | mysql -u ' . escapeshellarg($destUser)
		. ' -p' . escapeshellarg($destPass) . ' ' . escapeshellarg($destDb) . ' 2>&1';
	$out = epc_clp_run_cmd($cmd);
	$log[] = 'schema_dump_exit=' . (int) ($out['code'] ?? -1);
	$log[] = trim((string) ($out['output'] ?? ''));

	$tables = epc_cti_table_count($destDb, $destUser, $destPass);
	if ($tables < 10) {
		$dumpGz = '/tmp/epc-cti-full-' . preg_replace('/[^a-z0-9_]/', '', $srcDb) . '-' . time() . '.sql.gz';
		@unlink($dumpGz);
		$exp = epc_clp_run('db:export --databaseName=' . escapeshellarg($srcDb) . ' --file=' . escapeshellarg($dumpGz));
		$log[] = 'fallback_export=' . trim((string) ($exp['output'] ?? ''));
		if (is_file($dumpGz)) {
			$imp = epc_clp_run('db:import --databaseName=' . escapeshellarg($destDb) . ' --file=' . escapeshellarg($dumpGz));
			$log[] = 'fallback_import=' . trim((string) ($imp['output'] ?? ''));
			@unlink($dumpGz);
			$pdo = epc_cti_pdo($destDb, $destUser, $destPass);
			if ($pdo instanceof PDO) {
				$cleared = epc_cti_clear_transactional($pdo);
				$log[] = 'fallback_cleared_tables=' . count($cleared);
			}
		}
	}
	$tables = epc_cti_table_count($destDb, $destUser, $destPass);
	return array('ok' => $tables >= 10, 'tables' => $tables, 'log' => $log);
}

/**
 * @return array{ok:bool,message:string,db?:string,orders?:int,bank?:int}
 */
function epc_cti_isolate_one(PDO $platformPdo, string $siteKey, bool $apply, string $clpPass, string $dbPassOverride): array
{
	$row = epc_portal_tenant_control_get_row($platformPdo, $siteKey);
	if ($row === null) {
		return array('ok' => false, 'message' => 'tenant_not_in_registry');
	}
	$hostname = strtolower(trim((string) ($row['hostname'] ?? '')));
	$industry = (string) ($row['industry_code'] ?? '');
	$currentDb = (string) ($row['db_name'] ?? '');
	$currentUser = (string) ($row['db_user'] ?? '');
	$currentPass = (string) ($row['db_password'] ?? '');

	if ($siteKey === 'epartscart' || (function_exists('epc_portal_is_epartscart_hostname') && epc_portal_is_epartscart_hostname($hostname))) {
		return array('ok' => true, 'message' => 'skip_epartscart_keeps_docpart', 'db' => 'docpart');
	}
	if (!empty($row['erp_only_shared']) || (string) ($row['hosted_on'] ?? '') === 'platform') {
		return array('ok' => true, 'message' => 'skip_erp_only_use_dedicated_db_script', 'db' => $currentDb);
	}

	$desiredDb = preg_replace('/[^a-z0-9_]/', '', $siteKey);
	$desiredUser = $desiredDb;
	$desiredPass = $dbPassOverride !== '' ? $dbPassOverride : (
		($currentDb === $desiredDb && $currentPass !== '') ? $currentPass : epc_portal_tenant_control_generate_password()
	);

	$already = ($currentDb === $desiredDb)
		&& !empty($row['dedicated_db'])
		&& epc_cti_connects($desiredDb, $desiredUser !== '' ? $currentUser : $desiredUser, $currentPass !== '' ? $currentPass : $desiredPass)
		&& epc_cti_table_count($currentDb, $currentUser, $currentPass) > 10;

	if ($already) {
		$pdo = epc_cti_pdo($currentDb, $currentUser, $currentPass);
		$orders = $pdo ? epc_cti_count_orders($pdo) : -1;
		$bank = $pdo ? epc_cti_count_bank($pdo) : -1;
		return array(
			'ok' => true,
			'message' => 'already_dedicated',
			'db' => $currentDb,
			'orders' => $orders,
			'bank' => $bank,
		);
	}

	$msg = "current={$currentDb} desired={$desiredDb} industry={$industry} host={$hostname}";
	if (!$apply) {
		return array('ok' => true, 'message' => 'dry_run ' . $msg, 'db' => $currentDb);
	}

	if ($clpPass === '') {
		$clpPass = epc_portal_demo_clp_password();
	}
	if ($clpPass === '') {
		return array('ok' => false, 'message' => 'clp_pass_required ' . $msg);
	}

	$cookie = '';
	$login = epc_clp_web_login('admin', $clpPass, $cookie, true);
	if (empty($login['ok'])) {
		return array('ok' => false, 'message' => 'clp_login_failed ' . $msg);
	}

	if (!epc_cti_connects($desiredDb, $desiredUser, $desiredPass)) {
		if ($currentPass !== '' && epc_cti_connects($desiredDb, $desiredUser, $currentPass)) {
			$desiredPass = $currentPass;
		} else {
			$created = epc_clp_web_add_database($cookie, 'www.ecomae.com', $desiredDb, $desiredUser, $desiredPass);
			$ok = false;
			for ($i = 0; $i < 15; $i++) {
				if ($i > 0) {
					sleep(2);
				}
				if (epc_cti_connects($desiredDb, $desiredUser, $desiredPass)) {
					$ok = true;
					break;
				}
			}
			if (!$ok) {
				return array(
					'ok' => false,
					'message' => 'db_create_failed ' . $msg . ' log=' . json_encode($created['log'] ?? array()),
				);
			}
		}
	}

	$destTables = epc_cti_table_count($desiredDb, $desiredUser, $desiredPass);
	if ($destTables < 10) {
		$srcDb = ($currentDb !== '' && $currentDb !== $desiredDb) ? $currentDb : 'docpart';
		$mig = epc_cti_schema_migrate($srcDb, $desiredDb, $desiredUser, $desiredPass);
		if (empty($mig['ok'])) {
			return array('ok' => false, 'message' => 'schema_migrate_failed ' . json_encode($mig['log'] ?? array()));
		}
		$destTables = (int) ($mig['tables'] ?? 0);
	}

	$pdo = epc_cti_pdo($desiredDb, $desiredUser, $desiredPass);
	if (!$pdo instanceof PDO) {
		return array('ok' => false, 'message' => 'dest_connect_failed');
	}
	$cleared = epc_cti_clear_transactional($pdo);
	$orders = epc_cti_count_orders($pdo);
	$bank = epc_cti_count_bank($pdo);

	$intro = array();
	if (!empty($row['intro_json'])) {
		$decoded = json_decode((string) $row['intro_json'], true);
		if (is_array($decoded)) {
			$intro = $decoded;
		}
	}
	$intro['dedicated_db_isolated_at'] = date('c');
	$intro['previous_db_name'] = $currentDb;
	$intro['cleared_transactional'] = $cleared;

	$save = epc_portal_save_tenant($platformPdo, array(
		'site_key' => $siteKey,
		'hostname' => $hostname !== '' ? $hostname : ((string) (epc_portal_tenant_templates()[$siteKey]['hostname'] ?? '')),
		'industry_code' => $industry !== '' ? $industry : 'general',
		'status' => (string) ($row['status'] ?? 'live'),
		'trade_name' => (string) ($row['trade_name'] ?? $siteKey),
		'hub_name' => (string) ($row['hub_name'] ?? ''),
		'from_email' => (string) ($row['from_email'] ?? ''),
		'db_name' => $desiredDb,
		'db_user' => $desiredUser,
		'db_password' => $desiredPass,
		'hosted_on' => 'client',
		'erp_only_shared' => 0,
		'dedicated_db' => 1,
		'scale_policy' => 'dedicated_mysql',
		'notes' => trim((string) ($row['notes'] ?? '') . ' | client-isolate ' . date('Y-m-d H:i') . " ({$currentDb}→{$desiredDb})"),
		'intro_json' => json_encode($intro, JSON_UNESCAPED_UNICODE),
	));

	if (empty($save['ok'])) {
		return array('ok' => false, 'message' => 'registry_save_failed ' . ($save['message'] ?? ''), 'db' => $desiredDb);
	}

	return array(
		'ok' => true,
		'message' => 'isolated tables=' . $destTables . ' cleared=' . count($cleared),
		'db' => $desiredDb,
		'orders' => $orders,
		'bank' => $bank,
	);
}

try {
	$platformPdo = epc_cti_platform_pdo();
	epc_portal_db_ensure($platformPdo);
} catch (Throwable $e) {
	exit('platform_pdo_fail: ' . $e->getMessage() . "\n");
}

$keys = array();
if ($allClients) {
	foreach (epc_portal_list_tenants($platformPdo) as $row) {
		$key = (string) ($row['site_key'] ?? '');
		if ($key === '' || $key === 'epartscart') {
			continue;
		}
		if (!empty($row['erp_only_shared']) || (string) ($row['hosted_on'] ?? '') === 'platform') {
			continue;
		}
		$host = (string) ($row['hostname'] ?? '');
		if ($host === '' || (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname($host))) {
			continue;
		}
		$keys[] = $key;
	}
	$keys = array_values(array_unique($keys));
} elseif ($siteKey !== '') {
	$keys[] = $siteKey;
} else {
	exit("site_key=... or all_clients=1 required\n");
}

if ($clpPass === '') {
	$clpPass = epc_portal_demo_clp_password();
}
echo 'clp_pass=' . ($clpPass !== '' ? 'provided/saved len=' . strlen($clpPass) : 'MISSING') . "\n";
echo 'targets=' . implode(',', $keys) . "\n\n";

$fail = 0;
foreach ($keys as $key) {
	echo "--- {$key} ---\n";
	$result = epc_cti_isolate_one($platformPdo, $key, $apply, $clpPass, $dbPassOverride);
	echo '  ok=' . (!empty($result['ok']) ? 'yes' : 'NO')
		. ' db=' . ($result['db'] ?? '-')
		. ' orders=' . (isset($result['orders']) ? (string) $result['orders'] : '-')
		. ' bank=' . (isset($result['bank']) ? (string) $result['bank'] : '-')
		. "\n";
	echo '  ' . ($result['message'] ?? '') . "\n";
	if (empty($result['ok'])) {
		$fail++;
	}
}

echo "\nVerify: curl -sk \"https://www.ecomae.com/epc-tenant-db-connect-fix.php?token=...\"\n";
echo "Audit:  curl -sk \"https://www.ecomae.com/epc-commerce-isolation-audit.php?token=...&format=json\"\n";
echo "Done.\n";
exit($fail > 0 ? 1 : 0);
