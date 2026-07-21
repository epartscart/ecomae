<?php
/**
 * Client-tenant DB isolation — non–eParts hosts must never bind shared docpart.
 *
 *   php tests/erp_advanced/run_client_tenant_isolation_tests.php
 */

declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

$root = dirname(__DIR__, 2);
define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = $root;

require_once $root . '/content/general_pages/epc_portal.php';
require_once $root . '/content/general_pages/epc_portal_tenant.php';
require_once $root . '/content/general_pages/epc_commerce_isolation.php';

$pass = 0;
$fail = 0;
function check(string $label, bool $cond): void
{
	global $pass, $fail;
	if ($cond) {
		$pass++;
		echo "  PASS  $label\n";
	} else {
		$fail++;
		echo "  FAIL  $label\n";
	}
}

echo "== Shared-docpart allowlist ==\n";
check('helper exists', function_exists('epc_portal_client_may_share_docpart'));
check('epartscart may share', epc_portal_client_may_share_docpart('www.epartscart.com'));
check('epartscart bare may share', epc_portal_client_may_share_docpart('epartscart.com'));
check('taxofinca must NOT share', !epc_portal_client_may_share_docpart('www.taxofinca.com'));
check('electronicae must NOT share', !epc_portal_client_may_share_docpart('www.electronicae.com'));
check('stylenlook must NOT share', !epc_portal_client_may_share_docpart('www.stylenlook.com'));

echo "\n== Degraded shared-docpart containment ==\n";
class EpcIsoFakeConfig
{
	public $db = 'docpart';
	public $user = 'docpart';
	public $password = 'leaked';
	public $host = '127.0.0.1';
	public $epc_tenant_db_isolation_error = '';
}

$_SERVER['HTTP_HOST'] = 'www.electronicae.com';
unset($GLOBALS['epc_tenant_db_isolation_error'], $GLOBALS['epc_tenant_db_degraded_shared']);
$cfg = new EpcIsoFakeConfig();
epc_portal_resolve_tenant_db($cfg);
check('non-eparts on docpart keeps bind (site stays up)', (string) $cfg->db === 'docpart');
check('non-eparts on docpart marks degraded', !empty($GLOBALS['epc_tenant_db_degraded_shared']));
check('degraded helper true', function_exists('epc_portal_tenant_db_is_degraded_shared') && epc_portal_tenant_db_is_degraded_shared());

$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
unset($GLOBALS['epc_tenant_db_isolation_error'], $GLOBALS['epc_tenant_db_degraded_shared']);
$cfg2 = new EpcIsoFakeConfig();
epc_portal_resolve_tenant_db($cfg2);
check('epartscart is not degraded', empty($GLOBALS['epc_tenant_db_degraded_shared']));

require_once $root . '/content/general_pages/epc_tenant_data_guard.php';
$_SERVER['HTTP_HOST'] = 'www.stylenlook.com';
$GLOBALS['epc_tenant_db_degraded_shared'] = true;
$GLOBALS['DP_Config'] = (object) array('db' => 'docpart');
check('data guard active when degraded', epc_tenant_data_guard_active());
$GLOBALS['epc_tenant_db_degraded_shared'] = false;
$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
$GLOBALS['DP_Config'] = (object) array('db' => 'docpart');
check('data guard inactive for epartscart', !epc_tenant_data_guard_active());

echo "\n== Ops scripts no longer force clients onto docpart ==\n";
$modelc = (string) file_get_contents($root . '/epc-tenant-modelc-db.php');
check('modelc skips non-epartscart registry bind', strpos($modelc, 'must not share docpart') !== false);
check('modelc only saves epartscart', strpos($modelc, "siteKey !== 'epartscart'") !== false);

$restore = (string) file_get_contents($root . '/epc-tenant-registry-restore.php');
check('restore taxofinca db is taxofinca', strpos($restore, "'db_name' => 'taxofinca'") !== false);
check('restore does not set taxofinca db_name docpart', !preg_match("/'taxofinca'[\\s\\S]{0,400}'db_name'\\s*=>\\s*'docpart'/", $restore));

$connectFix = (string) file_get_contents($root . '/epc-tenant-db-connect-fix.php');
check('db-connect-fix skips non-epartscart rebind', strpos($connectFix, 'dedicated isolate required') !== false);
check('db-connect-fix probes via resolve_tenant_db', strpos($connectFix, 'epc_portal_resolve_tenant_db') !== false);

$isolate = (string) file_get_contents($root . '/epc-client-tenant-db-isolate.php');
check('isolate script present', is_file($root . '/epc-client-tenant-db-isolate.php'));
check('isolate clears shop_orders', strpos($isolate, "'shop_orders'") !== false);
check('isolate clears bank accounts', strpos($isolate, 'epc_erp_cash_bank_accounts') !== false || strpos($isolate, 'epc_erp_bank_accounts') !== false);
check('isolate sets dedicated_mysql', strpos($isolate, "'scale_policy' => 'dedicated_mysql'") !== false);
check('isolate keeps hosted_on client', strpos($isolate, "'hosted_on' => 'client'") !== false);

echo "\n== Commerce audit covers client docpart ==\n";
$ciSrc = (string) file_get_contents($root . '/content/general_pages/epc_commerce_isolation.php');
check('audit helper exists', function_exists('epc_ci_audit_client_docpart_isolation'));
check('full audit calls client_docpart_isolation', strpos($ciSrc, 'client_docpart_isolation') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
