<?php
/**
 * epartscart-only origin SSL (direct DNS): dedicated CLP site + Model C ssl_certificate paths.
 * https://www.ecomae.com/epc-epartscart-ssl-fix.php?token=...&clp_pass=...&apply=1
 *
 * If LE still fails from PHP, run as root on VPS (clp user):
 *   su -s /bin/bash -c "/usr/bin/clpctl lets-encrypt:install:certificate --domainName=www.epartscart.com --subjectAlternativeName=epartscart.com" clp
 *   systemctl reload nginx
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(300);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: ''));
$apply = !empty($_GET['apply']);
$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';
$wwwHost = 'www.epartscart.com';
$bareHost = 'epartscart.com';
$tenantHosts = array($wwwHost, $bareHost);
$platformIp = '31.97.216.247';
$siteUser = 'epartscart';
$siteUserPass = trim((string) ($_GET['site_user_password'] ?? getenv('EPC_SITE_USER_PASSWORD') ?: 'EpcEpartscart2026!'));

function epc_epsl_origin_cert(string $sniHost, string $ip): string
{
	$cmd = 'echo | openssl s_client -connect ' . escapeshellarg($ip . ':443')
		. ' -servername ' . escapeshellarg($sniHost)
		. ' 2>/dev/null | openssl x509 -noout -subject -ext subjectAltName 2>/dev/null';
	$r = epc_clp_run_cmd($cmd);
	return trim((string) ($r['output'] ?? '')) !== '' ? trim((string) $r['output']) : '(none)';
}

function epc_epsl_probe(string $url): string
{
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 25, 'ignore_errors' => true),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$body = @file_get_contents($url, false, $ctx);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	$hint = is_string($body) && stripos($body, 'No DB connect') !== false ? ' [no-db]' : '';
	return "HTTP {$code}{$hint}";
}

echo "=== epartscart origin SSL fix (epartscart only) ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

echo "=== orphan nginx configs for epartscart ===\n";
foreach (epc_clp_nginx_find_configs_for_hosts($tenantHosts, $platformSite) as $conf) {
	echo '  ' . $conf . "\n";
}

echo "\n=== BEFORE (origin SNI @ {$platformIp}) ===\n";
echo epc_epsl_origin_cert($wwwHost, $platformIp) . "\n\n";

if (!$apply) {
	echo "Dry run. Add apply=1&clp_pass=...\n";
	exit;
}
if ($clpPass === '') {
	exit("apply=1 requires clp_pass=\n");
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
echo "CloudPanel login: OK\n\n";

echo "=== Dedicated CLP site (LE cert holder) ===\n";
$dash = epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie);
if (!epc_clp_web_site_listed($dash, $wwwHost)) {
	$create = epc_clp_web_create_php_site($cookie, array(
		'domain' => $wwwHost,
		'site_user' => $siteUser,
		'site_user_password' => $siteUserPass,
		'php_version' => '8.3',
	));
	foreach ($create['log'] as $line) {
		echo '  ' . $line . "\n";
	}
}
$repoint = epc_clp_web_set_site_docroot($cookie, $wwwHost, $platformDocroot);
echo '  docroot: ' . implode(' | ', array_slice($repoint['log'], 0, 3)) . "\n";
$sslSite = epc_clp_web_install_ssl($cookie, $wwwHost, array($bareHost));
echo '  LE web: ' . implode(' | ', array_slice($sslSite['log'], 0, 4)) . "\n";
$paths = epc_clp_ssl_certificate_paths($wwwHost);
echo '  cert: ' . ($paths !== null ? $paths['crt'] : 'MISSING (needs root clpctl — see script header)') . "\n\n";

echo "=== Model C vhost ssl paths (epartscart server blocks only) ===\n";
$sslPatch = epc_clp_vhost_install_per_tenant_ssl($cookie, $platformSite, $platformDocroot, array(
	array('www' => $wwwHost, 'bare' => $bareHost),
));
foreach ($sslPatch['log'] as $line) {
	echo '  ' . $line . "\n";
}

echo "\n=== Quarantine duplicate epartscart nginx files ===\n";
$quarantine = epc_clp_nginx_quarantine_orphan_configs($tenantHosts, $platformSite);
foreach ($quarantine['log'] as $line) {
	echo '  ' . $line . "\n";
}

echo "\n=== nginx reload ===\n";
$reload = epc_clp_nginx_reload_with_pass($clpPass);
foreach ($reload['log'] as $line) {
	echo $line . "\n";
}
if (!$reload['ok']) {
	foreach (epc_clp_nginx_reload()['log'] as $line) {
		echo 'fallback: ' . $line . "\n";
	}
}

echo "\n=== AFTER (origin SNI @ {$platformIp}) ===\n";
echo epc_epsl_origin_cert($wwwHost, $platformIp) . "\n";
echo "\n=== HTTP ===\n";
echo '  /: ' . epc_epsl_probe('https://' . $wwwHost . '/') . "\n";
echo '  /cp/: ' . epc_epsl_probe('https://' . $wwwHost . '/cp/') . "\n";
echo '  taxofinca control: ' . epc_epsl_probe('https://www.taxofinca.com/') . "\n";
echo "\nDone.\n";
