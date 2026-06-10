<?php
/**
 * Read-only JSON health for all EPC hosts (Super CP / monitoring).
 * https://www.ecomae.com/epc-platform-health-check.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
set_time_limit(90);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$platformSite = 'www.ecomae.com';
$platformConf = '/etc/nginx/sites-enabled/' . epc_clp_nginx_platform_config_basename($platformSite);
$docroot = '/home/ecomae/htdocs/www.ecomae.com';

$areas = array(
	array('area' => 'Owner', 'host' => 'www.ecomae.com', 'paths' => array('/', '/cp/')),
	array('area' => 'Super CP', 'host' => 'www.ecomae.com', 'paths' => array('/cp/control/portal/epc_platform_failover_guide')),
	array('area' => 'CP panel', 'host' => 'cp.ecomae.com', 'paths' => array('/cp/')),
	array('area' => 'Splash', 'host' => 'www.ecomae.com', 'paths' => array('/epc-platform-splash.html?epc_splash_preview=1')),
);

$tenants = array(
	'epartscart' => 'www.epartscart.com',
	'taxofinca' => 'www.taxofinca.com',
	'electronicae' => 'www.electronicae.com',
	'stylenlook' => 'www.stylenlook.com',
	'thejewellerytrend' => 'www.thejewellerytrend.com',
);
foreach ($tenants as $slug => $host) {
	$areas[] = array('area' => ucfirst($slug), 'host' => $host, 'paths' => array('/', '/en/', '/cp/'));
}

function epc_hc_probe(string $url, string $hostHeader = '', int $timeout = 15): array
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => $timeout, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$t0 = microtime(true);
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $t0) * 1000);
	$code = 0;
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	$ok = $code >= 200 && $code < 400;
	if ($code === 302 && stripos($url, 'epc_platform_failover_guide') !== false) {
		$ok = true;
	}
	if ($code === 0) {
		$ok = false;
	}
	return array('http' => $code ?: null, 'ms' => $ms, 'ok' => $ok, 'hint' => $code === 0 ? 'timeout' : '');
}

function epc_hc_public_url(string $host, string $path): string
{
	if (strpos($path, '?') !== false) {
		list($p, $q) = explode('?', $path, 2);
		return 'https://' . $host . $p . '?' . $q;
	}
	return 'https://' . $host . $path;
}

$checks = array();
$fail = 0;
foreach ($areas as $row) {
	$host = $row['host'];
	foreach ($row['paths'] as $path) {
		$url = epc_hc_public_url($host, $path);
		$origin = epc_hc_probe('http://127.0.0.1' . (strpos($path, '?') !== false ? strstr($path, '?', true) : $path), $host);
		$pub = epc_hc_probe($url);
		if (!$pub['ok']) {
			$fail++;
		}
		$checks[] = array(
			'area' => $row['area'],
			'url' => $url,
			'http' => $pub['http'],
			'ok' => $pub['ok'],
			'origin_http' => $origin['http'],
			'ms' => $pub['ms'],
		);
	}
}

$listen8080 = 0;
$snEcomae = 0;
$orphans = array();
$conflicts = '';
if (is_file($platformConf)) {
	$text = @file_get_contents($platformConf);
	if (is_string($text)) {
		$listen8080 = preg_match_all('/listen\s+8080\b/', $text);
		$snEcomae = preg_match_all('/server_name[^;]*www\.ecomae\.com/i', $text);
	}
}
foreach (epc_clp_nginx_find_configs_for_hosts(epc_clp_model_c_tenant_hostnames(), $platformSite) as $c) {
	$orphans[] = basename($c);
}
$nt = epc_clp_run_cmd('nginx -t 2>&1');
if (stripos((string) $nt['output'], 'conflict') !== false) {
	$conflicts = trim((string) $nt['output']);
}
$b8080 = epc_hc_probe('http://127.0.0.1:8080/', 'www.ecomae.com');

$nginxOk = $listen8080 >= 1 && $snEcomae >= 2 && $orphans === array() && $conflicts === '' && ($b8080['http'] ?? 0) !== 404;

echo json_encode(array(
	'time' => date('c'),
	'platform' => $platformSite,
	'overall_ok' => $fail === 0 && $nginxOk,
	'public_failures' => $fail,
	'nginx' => array(
		'platform_conf' => is_file($platformConf),
		'listen_8080_blocks' => $listen8080,
		'server_name_ecomae_lines' => $snEcomae,
		'backend_8080_http' => $b8080['http'],
		'orphan_configs' => $orphans,
		'nginx_conflicts' => $conflicts !== '' ? $conflicts : null,
		'nginx_ok' => $nginxOk,
	),
	'checks' => $checks,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
