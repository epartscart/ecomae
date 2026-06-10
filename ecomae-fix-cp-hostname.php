<?php
/**
 * Fix cp.ecomae.com hostname + tenant hub eval + re-run vhost unify.
 * https://www.ecomae.com/ecomae-fix-cp-hostname.php?token=...&clp_pass=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($clpPass === '') {
	exit("clp_pass required\n");
}

echo "=== Patch tenant hub stub (eval fix) ===\n";
$stubPath = __DIR__ . '/cp/content/shop/tenant_hub/tenant_hub_main_page.php';
if (is_file($stubPath)) {
	$stub = (string) file_get_contents($stubPath);
	if (substr(rtrim($stub), -2) !== '?>') {
		$stub = rtrim($stub) . "\n?>\n";
		file_put_contents($stubPath, $stub);
		echo "patched {$stubPath}\n";
	} else {
		echo "stub already closed\n";
	}
} else {
	echo "stub missing\n";
}

echo "\n=== cp/index.php root redirect for cp.ecomae.com ===\n";
$cpIndex = __DIR__ . '/cp/index.php';
if (is_file($cpIndex)) {
	$cp = (string) file_get_contents($cpIndex);
	$marker = 'epc_cp_hostname_root_redirect';
	if (strpos($cp, $marker) === false) {
		$insert = <<<'PHP'

// epc_cp_hostname_root_redirect — cp.ecomae.com bare /cp/ → tenant hub
if (function_exists('epc_portal_host') && epc_portal_host() === 'cp.ecomae.com'
	&& ($_SERVER['REQUEST_METHOD'] ?? '') === 'GET' && isset($_SERVER['REQUEST_URI'])) {
	$cpPath = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
	$cpBase = '/' . trim((string) $DP_Config->backend_dir, '/');
	if ($cpPath === $cpBase || $cpPath === $cpBase . '/') {
		header('Location: ' . $cpBase . '/shop/tenant_hub/tenant_hub?tab=onboard', true, 302);
		exit;
	}
}

PHP;
		$needle = "epc_portal_apply_config(\$DP_Config);\n\n";
		if (strpos($cp, $needle) !== false && strpos($cp, 'tenant_hub/tenant_hub?tab=onboard') === false) {
			$cp = str_replace($needle, $needle . $insert, $cp);
			file_put_contents($cpIndex, $cp);
			echo "patched cp/index.php\n";
		} else {
			echo "cp/index redirect already present or anchor missing\n";
		}
	}
}

echo "\n=== CloudPanel vhost unify ===\n";
$_GET['clp_pass'] = $clpPass;
ob_start();
require __DIR__ . '/ecomae-clp-unify-vhost.php';
echo ob_get_clean();

function ecp_fix_probe(string $url): string
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header[0]) && preg_match('/\s(\d{3})\s/', $http_response_header[0], $m)) {
		$code = (int) $m[1];
	}
	$flat = $body !== false ? substr(preg_replace('/\s+/', ' ', strip_tags((string) $body)), 0, 100) : '';
	return "HTTP {$code} — {$flat}";
}

echo "\n=== Probes ===\n";
echo "cp.ecomae.com/cp/ → " . ecp_fix_probe('https://cp.ecomae.com/cp/') . "\n";
echo "cp tenant hub → " . ecp_fix_probe('https://cp.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard') . "\n";
echo "www tenant hub → " . ecp_fix_probe('https://www.ecomae.com/cp/shop/tenant_hub/tenant_hub?tab=onboard') . "\n";
