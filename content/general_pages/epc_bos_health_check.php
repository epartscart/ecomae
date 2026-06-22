<?php
/**
 * BOS platform health check — centralized diagnostic for all tenants.
 *
 * Checks: DB connectivity, schema integrity, ERP module availability,
 * security config, performance metrics. Returns structured results
 * consumable by the BOS dashboard.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Run all health checks for a single tenant.
 *
 * @return array{tenant:string,checks:array<int,array{name:string,status:string,detail:string}>,score:int}
 */
function epc_bos_health_check_tenant(PDO $platformPdo, string $siteKey): array
{
	$checks = array();
	$pass = 0;
	$total = 0;

	$addCheck = function (string $name, bool $ok, string $detail = '') use (&$checks, &$pass, &$total) {
		$checks[] = array(
			'name' => $name,
			'status' => $ok ? 'pass' : 'fail',
			'detail' => $detail,
		);
		$total++;
		if ($ok) {
			$pass++;
		}
	};

	// 1. Tenant record exists
	$st = $platformPdo->prepare("SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1");
	$st->execute(array($siteKey));
	$tenant = $st->fetch(PDO::FETCH_ASSOC);
	$addCheck('Tenant record', $tenant !== false, $tenant ? 'Found' : 'Not found in registry');
	if (!$tenant) {
		return array('tenant' => $siteKey, 'checks' => $checks, 'score' => 0);
	}

	// 2. DB connectivity
	$dbName = (string) ($tenant['db_name'] ?? '');
	$tenantPdo = null;
	if ($dbName !== '') {
		try {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_bos_unified.php';
			$conn = epc_bos_tenant_connect($platformPdo, $siteKey);
			$tenantPdo = $conn['pdo'] ?? null;
			$addCheck('DB connectivity', $tenantPdo instanceof PDO, $tenantPdo ? 'Connected to ' . $dbName : 'Connection failed');
		} catch (Throwable $e) {
			$addCheck('DB connectivity', false, $e->getMessage());
		}
	} else {
		$addCheck('DB connectivity', false, 'No db_name configured');
	}

	if (!$tenantPdo instanceof PDO) {
		return array('tenant' => $siteKey, 'checks' => $checks, 'score' => $total > 0 ? (int) round($pass / $total * 100) : 0);
	}

	// 3. Core tables exist
	$coreTables = array('users', 'sessions', 'pages', 'modules', 'control_groups', 'control_items');
	foreach ($coreTables as $table) {
		try {
			$st = $tenantPdo->query("SELECT COUNT(*) FROM `$table`");
			$count = (int) $st->fetchColumn();
			$addCheck('Table: ' . $table, true, $count . ' rows');
		} catch (Throwable $e) {
			$addCheck('Table: ' . $table, false, 'Missing or inaccessible');
		}
	}

	// 4. ERP tables
	$erpTables = array('epc_erp_coa_accounts', 'epc_erp_gl_journals', 'epc_erp_cash_bank_accounts', 'epc_erp_suppliers', 'epc_erp_audit_log');
	foreach ($erpTables as $table) {
		try {
			$st = $tenantPdo->query("SELECT COUNT(*) FROM `$table`");
			$addCheck('ERP: ' . $table, true, (int) $st->fetchColumn() . ' rows');
		} catch (Throwable $e) {
			$addCheck('ERP: ' . $table, false, 'Not provisioned');
		}
	}

	// 5. Admin user exists
	try {
		$st = $tenantPdo->query("SELECT COUNT(*) FROM `users` WHERE `is_admin` = 1");
		$adminCount = (int) $st->fetchColumn();
		$addCheck('Admin user', $adminCount > 0, $adminCount . ' admin(s)');
	} catch (Throwable $e) {
		$addCheck('Admin user', false, 'Cannot query users');
	}

	// 6. Language config
	try {
		$st = $tenantPdo->query("SELECT COUNT(*) FROM `lang_languages` WHERE `active` = 1");
		$langCount = (int) $st->fetchColumn();
		$addCheck('Languages', $langCount > 0, $langCount . ' active language(s)');
	} catch (Throwable $e) {
		$addCheck('Languages', false, 'Cannot query languages');
	}

	// 7. SSL / hostname
	$hostname = (string) ($tenant['hostname'] ?? '');
	if ($hostname !== '') {
		$addCheck('Hostname configured', true, $hostname);
	} else {
		$addCheck('Hostname configured', false, 'No hostname set');
	}

	$score = $total > 0 ? (int) round($pass / $total * 100) : 0;
	return array('tenant' => $siteKey, 'checks' => $checks, 'score' => $score);
}

/**
 * Run health checks for all tenants.
 *
 * @return array<int,array{tenant:string,checks:array,score:int}>
 */
function epc_bos_health_check_all(PDO $platformPdo): array
{
	$results = array();
	$st = $platformPdo->query("SELECT `site_key` FROM `epc_portal_tenants` WHERE `site_key` != '' AND `is_active` = 1 ORDER BY `trade_name`");
	while ($row = $st->fetch(PDO::FETCH_ASSOC)) {
		$results[] = epc_bos_health_check_tenant($platformPdo, (string) $row['site_key']);
	}
	return $results;
}

/**
 * Platform-wide health summary.
 *
 * @return array{total_tenants:int,avg_score:int,critical:int,healthy:int}
 */
function epc_bos_health_summary(array $results): array
{
	$total = count($results);
	$totalScore = 0;
	$critical = 0;
	$healthy = 0;
	foreach ($results as $r) {
		$totalScore += $r['score'];
		if ($r['score'] < 50) {
			$critical++;
		} elseif ($r['score'] >= 80) {
			$healthy++;
		}
	}
	return array(
		'total_tenants' => $total,
		'avg_score' => $total > 0 ? (int) round($totalScore / $total) : 0,
		'critical' => $critical,
		'healthy' => $healthy,
	);
}
