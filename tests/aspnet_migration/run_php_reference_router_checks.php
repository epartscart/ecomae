<?php
/**
 * Static checks for PHP reference router (protocol: aspnet-primary-php-reference).
 */
declare(strict_types=1);

$root = dirname(__DIR__, 2);
$fail = 0;
$pass = 0;

function check(string $label, bool $ok): void
{
	global $fail, $pass;
	if ($ok) {
		echo "PASS  $label\n";
		$pass++;
	} else {
		echo "FAIL  $label\n";
		$fail++;
	}
}

$router = $root . '/content/general_pages/epc_php_reference_router.php';
$index = file_get_contents($root . '/index.php') ?: '';
$routerSrc = file_get_contents($router) ?: '';

check('router file exists', is_file($router));
check('index.php loads router', str_contains($index, 'epc_php_reference_router.php'));
check('index.php exits on handled reference', str_contains($index, 'epc_php_reference_try_route()'));
check('router never Location to /cp product', !preg_match('/header\\s*\\(\\s*[\'"]Location:\\s*\\/cp/i', $routerSrc));
check('router boots cp/index.php in-process', str_contains($routerSrc, "/cp/index.php"));
check('router boots erp portal in-process', str_contains($routerSrc, 'epc_erp_portal_try_route'));
check('router denies tenant BOS', str_contains($routerSrc, 'Super-CP only'));
check('sets X-EcomAE-Php-Reference', str_contains($routerSrc, 'X-EcomAE-Php-Reference'));

$tenantNgx = file_get_contents($root . '/deploy/aspnet/nginx-classic-entry-tenant-aspnet-primary-shadow-example.conf') ?: '';
$wwwNgx = file_get_contents($root . '/deploy/aspnet/nginx-classic-entry-aspnet-primary-shadow-example.conf') ?: '';
check('tenant nginx rewrites php-reference/cp to boot.php', str_contains($tenantNgx, 'rewrite ^ /epc_php_reference_boot.php?epc_php_reference=cp&$args last'));
check('www nginx rewrites php-reference/cp to boot.php', str_contains($wwwNgx, 'rewrite ^ /epc_php_reference_boot.php?epc_php_reference=cp&$args last'));
check('www nginx does not steal compare via index.php', !str_contains($wwwNgx, 'rewrite ^ /index.php?epc_php_reference=cp last'));
check('tenant nginx has deep CP compare', str_contains($tenantNgx, 'location ^~ /php-reference/CP'));
check('tenant nginx has deep ERP compare', str_contains($tenantNgx, 'location ^~ /php-reference/ERP'));
check('router applies deep compare URI', str_contains($routerSrc, 'epc_php_reference_apply_deep_uri'));
check('boot file exists', is_file($root . '/epc_php_reference_boot.php'));
check('tenant nginx 404s php-reference/bos', str_contains($tenantNgx, 'location = /php-reference/bos') && str_contains($tenantNgx, 'return 404'));

$hybrid = file_get_contents($root . '/aspnet/src/EcomAE.Platform/Components/Shared/PhpHybridWorkspaceFrame.razor') ?: '';
check('hybrid iframe requires php-reference', str_contains($hybrid, '/php-reference/'));
check('hybrid rewrites via PhpReferenceOnlyHref', str_contains($hybrid, 'PhpReferenceOnlyHref'));

$compare = file_get_contents($root . '/aspnet/src/EcomAE.Platform/Components/Pages/MigrationCompareConsole.razor') ?: '';
check('compare board opens tenant classic CP', str_contains($compare, '/php-reference/cp'));
check('compare board opens tenant classic ERP', str_contains($compare, '/php-reference/erp'));
check('compare board does not send PHP twin to product /CP/', !str_contains($compare, '@tenantPhp/CP/') && !str_contains($compare, '@wwwPhp/CP/'));
check('compare board does not send PHP twin to product /ERP/', !str_contains($compare, '@tenantPhp/ERP/') && !str_contains($compare, '@wwwPhp/ERP/'));

echo "\nPassed: $pass  Failed: $fail\n";
exit($fail > 0 ? 1 : 0);
