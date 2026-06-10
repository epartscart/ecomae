<?php
/**
 * Platform health checkup — aggregated JSON for Super CP dashboard.
 * GET https://www.ecomae.com/epc-platform-health-checkup-api.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
set_time_limit(180);

define('_ASTEXE_', 1);

try {

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';

function epc_hcu_probe(string $url, string $hostHeader = '', int $timeout = 18): array
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\nAccept-Encoding: gzip\r\n") : "Accept-Encoding: gzip\r\n";
	$ctx = stream_context_create(array(
		'http' => array('timeout' => $timeout, 'ignore_errors' => true, 'header' => $headers),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	));
	$t0 = microtime(true);
	$body = @file_get_contents($url, false, $ctx);
	$ms = (int) round((microtime(true) - $t0) * 1000);
	$code = 0;
	$sslNote = '';
	if (isset($http_response_header) && is_array($http_response_header)) {
		foreach ($http_response_header as $h) {
			if (preg_match('/^\s*HTTP\/\S+\s+(\d{3})/', $h, $m)) {
				$code = (int) $m[1];
			}
		}
	}
	if ($code === 525) {
		$sslNote = 'Cloudflare SSL handshake failure — check origin cert or CF SSL mode (Full strict)';
	}
	return array(
		'http' => $code ?: null,
		'ms' => $ms,
		'ok' => $code >= 200 && $code < 400,
		'bytes' => is_string($body) ? strlen($body) : 0,
		'ssl_note' => $sslNote,
	);
}

function epc_hcu_ssl_check(string $host): array
{
	$ctx = stream_context_create(array(
		'ssl' => array(
			'capture_peer_cert' => true,
			'verify_peer' => false,
			'verify_peer_name' => false,
		),
	));
	$errno = 0;
	$errstr = '';
	$client = @stream_socket_client(
		'ssl://' . $host . ':443',
		$errno,
		$errstr,
		12,
		STREAM_CLIENT_CONNECT,
		$ctx
	);
	if (!$client) {
		return array('ok' => false, 'error' => $errstr !== '' ? $errstr : 'connect failed', 'expires' => null);
	}
	$params = stream_context_get_params($client);
	fclose($client);
	$cert = $params['options']['ssl']['peer_certificate'] ?? null;
	if (!$cert) {
		return array('ok' => false, 'error' => 'no peer certificate', 'expires' => null);
	}
	$parsed = openssl_x509_parse($cert);
	$expires = isset($parsed['validTo_time_t']) ? date('c', (int) $parsed['validTo_time_t']) : null;
	$days = isset($parsed['validTo_time_t']) ? (int) floor(((int) $parsed['validTo_time_t'] - time()) / 86400) : null;
	return array(
		'ok' => $days === null || $days > 7,
		'expires' => $expires,
		'days_left' => $days,
		'subject' => $parsed['subject']['CN'] ?? '',
	);
}

$platformSite = 'www.ecomae.com';
$platformConf = '/etc/nginx/sites-enabled/' . epc_clp_nginx_platform_config_basename($platformSite);

$tenantRows = array(
	array('slug' => 'ecomae', 'label' => 'Owner', 'host' => 'www.ecomae.com', 'db' => 'ecomae'),
	array('slug' => 'epartscart', 'label' => 'eParts Cart', 'host' => 'www.epartscart.com', 'db' => 'docpart'),
	array('slug' => 'taxofinca', 'label' => 'Taxofinca', 'host' => 'www.taxofinca.com', 'db' => 'docpart'),
	array('slug' => 'electronicae', 'label' => 'Electronicae', 'host' => 'www.electronicae.com', 'db' => 'docpart'),
	array('slug' => 'stylenlook', 'label' => 'Stylenlook', 'host' => 'www.stylenlook.com', 'db' => 'docpart'),
	array('slug' => 'thejewellerytrend', 'label' => 'Jewellery Trend', 'host' => 'www.thejewellerytrend.com', 'db' => 'docpart'),
	array('slug' => 'cp_super', 'label' => 'Super CP host', 'host' => 'cp.ecomae.com', 'db' => 'ecomae'),
);

$urlChecks = array();
foreach ($tenantRows as $row) {
	$host = $row['host'];
	foreach (array('/en/', '/cp/') as $path) {
		if ($host === 'www.ecomae.com' && $path === '/en/') {
			$path = '/';
		}
		if ($host === 'cp.ecomae.com' && $path === '/en/') {
			continue;
		}
		$pub = 'https://' . $host . $path;
		$originPath = strpos($path, '?') !== false ? strstr($path, '?', true) : $path;
		$origin = epc_hcu_probe('http://127.0.0.1' . $originPath, $host);
		$public = epc_hcu_probe($pub);
		$urlChecks[] = array(
			'slug' => $row['slug'],
			'label' => $row['label'],
			'url' => $pub,
			'path' => $path,
			'public_http' => $public['http'],
			'public_ms' => $public['ms'],
			'public_ok' => $public['ok'],
			'origin_http' => $origin['http'],
			'origin_ms' => $origin['ms'],
			'ssl_note' => $public['ssl_note'],
		);
	}
}

$sslChecks = array();
foreach ($tenantRows as $row) {
	$sslChecks[] = array_merge(
		array('host' => $row['host'], 'slug' => $row['slug']),
		epc_hcu_ssl_check($row['host'])
	);
}

$erpIsolation = array();
$hostDbMap = array();
$overrideFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($overrideFile)) {
	$epc_tenant_host_db = null;
	include $overrideFile;
	if (is_array($epc_tenant_host_db)) {
		foreach ($epc_tenant_host_db as $h => $cred) {
			if (is_array($cred) && !empty($cred['db'])) {
				$hostDbMap[strtolower((string) $h)] = strtolower((string) $cred['db']);
			}
		}
	}
}
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
$pdoPlatform = null;
try {
	$pdoPlatform = epc_portal_platform_pdo();
} catch (Throwable $e) {
	$pdoPlatform = null;
}
if ($pdoPlatform instanceof PDO) {
	epc_portal_db_ensure($pdoPlatform);
	$st = $pdoPlatform->query(
		'SELECT `site_key`, `hostname`, `db_name`, `status`, `erp_only_shared` FROM `epc_portal_tenants` ORDER BY `site_key`'
	);
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$host = strtolower((string) ($r['hostname'] ?? ''));
		$db = strtolower((string) ($r['db_name'] ?? ''));
		$erpOnly = !empty($r['erp_only_shared']);
		$runtime = $hostDbMap[$host] ?? '';
		// Model C: shared docroot config may show platform DB (ecomae) while tenant registry binds docpart/asap.
		$runtimeLabel = $runtime !== '' ? $runtime : ($db === 'docpart' ? 'docpart (tenant registry)' : '(registry only)');
		$isolationOk = $db !== '' && (
			($erpOnly && $db === 'asap')
			|| (!$erpOnly && $db === 'docpart')
			|| ($db === 'ecomae' && $host === 'www.ecomae.com')
		);
		$erpIsolation[] = array(
			'site_key' => $r['site_key'],
			'hostname' => $r['hostname'],
			'registry_db' => $db,
			'runtime_db' => $runtimeLabel,
			'status' => $r['status'],
			'ok' => $isolationOk,
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
if (stripos((string) ($nt['output'] ?? ''), 'conflict') !== false) {
	$conflicts = trim((string) $nt['output']);
}
$b8080 = epc_hcu_probe('http://127.0.0.1:8080/', 'www.ecomae.com');

$backupFreshness = array('latest' => null, 'age_hours' => null, 'ok' => false);
$backupDir = '/home/ecomae/backups';
if (is_dir($backupDir)) {
	$latest = 0;
	$latestName = '';
	foreach ((array) @scandir($backupDir) as $name) {
		if ($name === '.' || $name === '..') {
			continue;
		}
		$path = $backupDir . '/' . $name;
		if (!is_file($path) || !preg_match('/\.(tar\.gz|zip)$/i', $name)) {
			continue;
		}
		$m = (int) @filemtime($path);
		if ($m > $latest) {
			$latest = $m;
			$latestName = $name;
		}
	}
	if ($latest > 0) {
		$ageH = round((time() - $latest) / 3600, 1);
		$backupFreshness = array(
			'latest' => $latestName,
			'latest_at' => date('c', $latest),
			'age_hours' => $ageH,
			'ok' => $ageH <= 168,
		);
	}
}

$indexing = array();
$indexHosts = array('www.ecomae.com', 'www.epartscart.com', 'www.taxofinca.com', 'www.electronicae.com', 'www.stylenlook.com', 'www.thejewellerytrend.com');
foreach ($indexHosts as $ih) {
	$robots = epc_hcu_probe('https://' . $ih . '/robots.txt');
	$sitemap = epc_hcu_probe('https://' . $ih . '/sitemap-index.php');
	if ($ih === 'www.ecomae.com') {
		$sitemap = epc_hcu_probe('https://' . $ih . '/epc-ecomae-sitemap.xml');
	}
	$indexing[] = array(
		'host' => $ih,
		'robots_http' => $robots['http'],
		'sitemap_http' => $sitemap['http'],
		'sitemap_ok' => ($sitemap['http'] ?? 0) === 200,
		'note' => 'Submit sitemap in Google Search Console — no site: results yet (May 2026 audit)',
	);
}

$opcache = array('enabled' => function_exists('opcache_get_status'), 'status' => null);
if (function_exists('opcache_get_status')) {
	$st = @opcache_get_status(false);
	if (is_array($st)) {
		$opcache['status'] = array(
			'cached_scripts' => $st['opcache_statistics']['num_cached_scripts'] ?? null,
			'hit_rate' => isset($st['opcache_statistics']['opcache_hit_rate'])
				? round((float) $st['opcache_statistics']['opcache_hit_rate'], 2)
				: null,
		);
	}
}

$load = null;
if (function_exists('sys_getloadavg')) {
	$l = sys_getloadavg();
	if (is_array($l)) {
		$load = array('1m' => $l[0], '5m' => $l[1], '15m' => $l[2]);
	}
}

$disk = array();
if (function_exists('disk_free_space')) {
	$free = @disk_free_space('/');
	$total = @disk_total_space('/');
	if ($free !== false && $total !== false && $total > 0) {
		$disk = array(
			'free_gb' => round($free / 1073741824, 1),
			'total_gb' => round($total / 1073741824, 1),
			'used_pct' => round(100 - ($free / $total * 100), 1),
		);
	}
}

$publicFails = 0;
foreach ($urlChecks as $c) {
	if (!$c['public_ok']) {
		$publicFails++;
	}
}

echo json_encode(array(
	'time' => date('c'),
	'overall_ok' => $publicFails === 0 && $conflicts === '' && $orphans === array() && ($b8080['http'] ?? 0) !== 404,
	'public_failures' => $publicFails,
	'cloudflare_ssl_note' => 'Use Full (strict) when origin has valid cert; cp.ecomae.com 525 = dedicated vhost cert or CF mode mismatch',
	'url_checks' => $urlChecks,
	'ssl_checks' => $sslChecks,
	'erp_isolation' => $erpIsolation,
	'nginx' => array(
		'platform_conf' => $platformConf,
		'listen_8080_blocks' => $listen8080,
		'server_name_ecomae_lines' => $snEcomae,
		'backend_8080_http' => $b8080['http'],
		'orphan_configs' => $orphans,
		'nginx_conflicts' => $conflicts !== '' ? $conflicts : null,
		'nginx_ok' => $listen8080 >= 1 && $snEcomae >= 2 && $orphans === array() && $conflicts === '',
		'operator_reminder' => 'After nginx edits in CloudPanel: Save vhost + Test + Reload nginx',
	),
	'backup' => $backupFreshness,
	'indexing' => $indexing,
	'opcache' => $opcache,
	'load' => $load,
	'disk' => $disk,
), JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);

} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array(
		'ok' => false,
		'error' => $e->getMessage(),
		'file' => basename($e->getFile()),
		'line' => $e->getLine(),
	), JSON_UNESCAPED_SLASHES | JSON_PRETTY_PRINT);
}
