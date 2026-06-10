<?php
/**
 * Verify ERP tenant DB isolation — KPI sources must not cross ecomae/docpart/asapc boundaries.
 * GET: token=epartscart-deploy-2026
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$out = array('status' => true, 'checks' => array(), 'rules' => array(
	'client_erp' => 'URL /cp/client-erp/{site_key}/ binds registry tenant DB only',
	'platform_erp' => 'URL /cp/platform-erp/ binds ecomae only',
	'tenant_cp' => 'Client hostname /cp/shop/finance/erp binds docpart (or tenant registry) only',
	'guard' => 'epc_erp_assert_tenant_db_context() blocks docpart/ecomae on shared ERP tenants',
));

require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_shared_erp.php';
require_once __DIR__ . '/content/general_pages/epc_client_erp_router.php';
require_once __DIR__ . '/content/general_pages/epc_platform_erp_router.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_helpers.php';

$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
$epc_config_local = null;
if (is_file($cfgFile)) {
	include $cfgFile;
}
$platDb = (string) ($epc_config_local['db'] ?? 'ecomae');
$platUser = (string) ($epc_config_local['user'] ?? 'ecomae');
$platPass = (string) ($epc_config_local['password'] ?? '');

function epc_isolation_probe_pdo(string $db, string $user, string $pass): ?PDO
{
	if ($db === '' || $user === '' || $pass === '') {
		return null;
	}
	try {
		return new PDO(
			'mysql:host=127.0.0.1;dbname=' . $db . ';charset=utf8',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		return null;
	}
}

function epc_isolation_kpi_snapshot(?PDO $pdo): ?array
{
	if (!$pdo instanceof PDO) {
		return null;
	}
	try {
		require_once __DIR__ . '/content/shop/finance/epc_erp_schema.php';
		epc_erp_ensure_schema($pdo);
		$dash = epc_erp_dashboard($pdo, strtotime(date('Y-m-01')), time());
		return array(
			'db' => (string) $pdo->query('SELECT DATABASE()')->fetchColumn(),
			'payable_balance' => round((float) ($dash['payable_balance'] ?? 0), 2),
			'cash_bank_total' => round((float) ($dash['cash_bank_total'] ?? 0), 2),
			'profit_ex_vat' => round((float) ($dash['profit_ex_vat'] ?? 0), 2),
		);
	} catch (Throwable $e) {
		return array('error' => $e->getMessage());
	}
}

$platformPdo = epc_isolation_probe_pdo($platDb, $platUser, $platPass);
$out['checks']['platform_pdo'] = $platformPdo instanceof PDO;

$asapRow = epc_portal_shared_erp_load_by_site_key('asapcustom', $platformPdo);
$out['checks']['asapcustom_registry'] = $asapRow ? array(
	'site_key' => $asapRow['site_key'],
	'db_name' => $asapRow['db_name'],
) : null;

$asapPdo = ($asapRow && is_array($asapRow)) ? epc_portal_shared_erp_tenant_pdo($asapRow) : null;
$docpartPass = '';
$tenantHostDbFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($tenantHostDbFile)) {
	include $tenantHostDbFile;
	if (!empty($epc_tenant_host_db['www.epartscart.com']['password'])) {
		$docpartPass = (string) $epc_tenant_host_db['www.epartscart.com']['password'];
	}
}
$docpartPdo = epc_isolation_probe_pdo('docpart', 'docpart', $docpartPass);

$kpis = array(
	'ecomae' => epc_isolation_kpi_snapshot($platformPdo),
	'asapc' => epc_isolation_kpi_snapshot($asapPdo),
	'docpart' => epc_isolation_kpi_snapshot($docpartPdo),
);
$out['checks']['kpi_by_db'] = $kpis;

// Simulate client-erp request binding (no session required for DB name).
unset($GLOBALS['epc_client_erp_context'], $GLOBALS['epc_client_erp_site_key'], $GLOBALS['epc_client_erp_tenant_row']);
$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['REQUEST_URI'] = '/cp/client-erp/asapcustom/shop/finance/erp?epc_erp_shell=1';
if (function_exists('epc_client_erp_bootstrap')) {
	epc_client_erp_bootstrap();
}
$simCfg = new DP_Config();
epc_portal_apply_config($simCfg);
$out['checks']['client_erp_simulated'] = array(
	'context_active' => function_exists('epc_client_erp_is_active') && epc_client_erp_is_active(),
	'config_db' => (string) ($simCfg->db ?? ''),
	'expected_db' => $asapRow ? (string) ($asapRow['db_name'] ?? '') : '',
	'match' => $asapRow && strcasecmp((string) ($simCfg->db ?? ''), (string) ($asapRow['db_name'] ?? '')) === 0,
);
if (!$out['checks']['client_erp_simulated']['match']) {
	$out['status'] = false;
}

// Platform ERP simulated binding.
unset($GLOBALS['epc_client_erp_context'], $GLOBALS['epc_platform_erp_context']);
$_SERVER['REQUEST_URI'] = '/cp/platform-erp/shop/finance/erp?epc_erp_shell=1';
if (function_exists('epc_platform_erp_bootstrap')) {
	epc_platform_erp_bootstrap();
}
$platCfg = new DP_Config();
epc_portal_apply_config($platCfg);
if (function_exists('epc_platform_erp_apply_config')) {
	epc_platform_erp_apply_config($platCfg);
}
$out['checks']['platform_erp_simulated'] = array(
	'config_db' => (string) ($platCfg->db ?? ''),
	'expected_db' => $platDb,
	'match' => strcasecmp((string) ($platCfg->db ?? ''), $platDb) === 0,
);
if (!$out['checks']['platform_erp_simulated']['match']) {
	$out['status'] = false;
}

// Cross-leak heuristic: asapcustom must not match ecomae/docpart KPI totals when DBs differ.
if ($asapPdo instanceof PDO && is_array($kpis['asapc']) && is_array($kpis['ecomae']) && is_array($kpis['docpart'])) {
	$asapCash = (float) ($kpis['asapc']['cash_bank_total'] ?? 0);
	$ecomCash = (float) ($kpis['ecomae']['cash_bank_total'] ?? 0);
	$docCash = (float) ($kpis['docpart']['cash_bank_total'] ?? 0);
	$out['checks']['asapcustom_distinct_from_platform'] = array(
		'asapc_cash' => $asapCash,
		'ecomae_cash' => $ecomCash,
		'docpart_cash' => $docCash,
		'asap_equals_ecomae' => abs($asapCash - $ecomCash) < 0.01,
		'asap_equals_docpart' => abs($asapCash - $docCash) < 0.01,
	);
	if ($out['checks']['asapcustom_distinct_from_platform']['asap_equals_ecomae']
		&& abs($ecomCash) > 0.01) {
		$out['status'] = false;
		$out['checks']['leak_warning'] = 'asapc cash_bank_total matches ecomae — possible cross-tenant leak';
	}
}

echo json_encode($out, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
