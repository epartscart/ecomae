<?php
/**
 * Surface doorway alias regression tests.
 *
 *   php tests/erp_advanced/run_surface_route_alias_tests.php
 */
declare(strict_types=1);

if (PHP_SAPI !== 'cli') {
	http_response_code(403);
	exit('CLI only');
}

define('_ASTEXE_', 1);
$_SERVER['DOCUMENT_ROOT'] = dirname(__DIR__, 2);
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_route_aliases.php';

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

echo "== Surface route aliases ==\n";

check('/CP canonicalizes to /cp', epc_portal_alias_canonical_surface_path('/CP') === '/cp');
check('/CP/ keeps suffix', epc_portal_alias_canonical_surface_path('/CP/shop/orders') === '/cp/shop/orders');
check('/ERP canonicalizes to /erp', epc_portal_alias_canonical_surface_path('/ERP') === '/erp');
check('/ERP/ keeps suffix', epc_portal_alias_canonical_surface_path('/ERP/guide') === '/erp/guide');
check('/BOS canonicalizes to /bos', epc_portal_alias_canonical_surface_path('/BOS') === '/bos');
check('/BOS/ keeps suffix', epc_portal_alias_canonical_surface_path('/BOS/') === '/bos/');
check('lowercase /cp has no redirect', epc_portal_alias_canonical_surface_path('/cp') === null);
check('lowercase /erp has no redirect', epc_portal_alias_canonical_surface_path('/erp/guide') === null);
check('lowercase /bos has no redirect', epc_portal_alias_canonical_surface_path('/bos/') === null);
check('unrelated frontend path ignored', epc_portal_alias_canonical_surface_path('/en/parts/BOSCH/123') === null);
check('path parser strips query string', epc_portal_alias_request_path('/ERP/guide?x=1') === '/ERP/guide');
check('BOS entry file exists for index handoff', is_file($_SERVER['DOCUMENT_ROOT'] . '/bos/index.php'));

$index = (string) file_get_contents($_SERVER['DOCUMENT_ROOT'] . '/index.php');
check('index loads alias router before API/storefront routing', strpos($index, 'epc_portal_route_aliases.php') !== false);
check('index can hand off /bos when nginx sends it to index', strpos($index, 'epc_portal_alias_try_bos_entry') !== false);

$htaccess = (string) file_get_contents($_SERVER['DOCUMENT_ROOT'] . '/.htaccess');
check('.htaccess has BOS rewrite', stripos($htaccess, 'RewriteRule ^bos') !== false);
check('.htaccess BOS rewrite is case-insensitive', stripos($htaccess, '[NC,L]') !== false && stripos($htaccess, '[NC,QSA,L]') !== false);

echo "\n----------------------------\n";
echo "Passed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
