<?php
/**
 * Dedupe nginx tenant conflicts: quarantine orphan site configs + strip Certbot blocks from Model C vhost.
 * https://www.ecomae.com/epc-nginx-dedupe-tenants.php?token=...&clp_pass=...&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(180);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: ''));
$apply = !empty($_GET['apply']);
$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';

$tenants = array(
	'thejewellerytrend' => array('www' => 'www.thejewellerytrend.com', 'bare' => 'thejewellerytrend.com'),
	'epartscart' => array('www' => 'www.epartscart.com', 'bare' => 'epartscart.com'),
	'taxofinca' => array('www' => 'www.taxofinca.com', 'bare' => 'taxofinca.com'),
	'electronicae' => array('www' => 'www.electronicae.com', 'bare' => 'electronicae.com'),
	'stylenlook' => array('www' => 'www.stylenlook.com', 'bare' => 'stylenlook.com'),
);

function epc_ndt_probe(string $url, string $hostHeader = ''): string
{
	$headers = $hostHeader !== '' ? ("Host: {$hostHeader}\r\n") : '';
	$ctx = stream_context_create(array(
		'http' => array('timeout' => 20, 'ignore_errors' => true, 'header' => $headers),
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
	$hint = (is_string($body) && stripos($body, 'No DB connect') !== false) ? ' [no-db]' : '';
	return $code > 0 ? "HTTP {$code}{$hint}" : 'TIMEOUT';
}

echo "=== EPC nginx tenant dedupe ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n";
echo 'platform_site=' . $platformSite . "\n\n";

$allAliases = array();
$tenantGroups = array();
foreach ($tenants as $key => $t) {
	$tenantGroups[] = array('key' => $key, 'hosts' => array($t['www'], $t['bare']));
	$allAliases[] = $t['www'];
	$allAliases[] = $t['bare'];
}
$allAliases = array_values(array_unique($allAliases));

echo "=== orphan configs (would quarantine) ===\n";
foreach (epc_clp_nginx_find_configs_for_hosts($allAliases, $platformSite) as $conf) {
	echo '  ' . $conf . "\n";
}

echo "\n=== duplicate ecomae enabled configs (would disable extras, keep www.ecomae.com.conf) ===\n";
$enabledDir = '/etc/nginx/sites-enabled';
$canonical = '/etc/nginx/sites-enabled/' . epc_clp_nginx_platform_config_basename($platformSite);
if (is_dir($enabledDir)) {
	foreach (glob($enabledDir . '/*') ?: array() as $conf) {
		if (!is_file($conf)) {
			continue;
		}
		$text = @file_get_contents($conf);
		if ($text !== false && preg_match('/server_name[^;]*\b(?:www\.)?ecomae\.com\b/i', $text)) {
			$tag = (realpath($conf) === realpath($canonical) || basename($conf) === epc_clp_nginx_platform_config_basename($platformSite))
				? 'KEEP' : 'DISABLE';
			echo "  [{$tag}] {$conf}\n";
		}
	}
}

if (!$apply) {
	echo "\nDry run. Add apply=1&clp_pass=... to quarantine orphans, strip Certbot blocks, reload nginx.\n";
	echo "Or run as root: bash /root/NGINX-DEDUPE-KODEE.sh\n";
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

echo "=== Quarantine orphan per-tenant nginx files ===\n";
$quarantine = epc_clp_nginx_quarantine_orphan_configs($allAliases, $platformSite);
foreach ($quarantine['log'] as $line) {
	echo '  ' . $line . "\n";
}

echo "\n=== Strip Certbot/orphan tenant blocks from platform vhost ===\n";
$vf = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf['vhost'] === '' || $vf['token'] === '') {
	exit("Could not read platform vhost\n");
}
$strip = epc_clp_vhost_strip_orphan_tenant_server_blocks($vf['vhost'], $allAliases);
foreach ($strip['log'] as $line) {
	echo '  ' . $line . "\n";
}
echo '  removed=' . $strip['removed'] . "\n";

$vhost = $strip['vhost'];
if ($strip['removed'] > 0) {
	if (!epc_clp_vhost_save($cookie, $platformSite, $vhost, $vf['token'])) {
		exit("vhost save after strip failed\n");
	}
	echo "Saved stripped vhost\n";
	$vf['vhost'] = $vhost;
}

$hasMarkers = strpos($vf['vhost'], '# EPC_TENANT_DIRECT_START') !== false;
if (!$hasMarkers) {
	echo "\n=== Model C tenant blocks missing — inserting ===\n";
	$vh = epc_clp_vhost_configure_model_c_tenants($cookie, $platformSite, $tenantGroups);
	foreach ($vh['log'] as $line) {
		echo '  ' . $line . "\n";
	}
	$vf = epc_clp_vhost_fetch($cookie, $platformSite);
}

if ($vf['vhost'] !== '' && $vf['token'] !== '') {
	$patched = epc_clp_vhost_patch_tenant_direct_root($vf['vhost'], $platformDocroot);
	if ($patched !== $vf['vhost']) {
		epc_clp_vhost_save($cookie, $platformSite, $patched, $vf['token']);
		echo "Patched tenant direct root → {$platformDocroot}\n";
		$vf['vhost'] = $patched;
	}
}

echo "\n=== nginx -t (before reload) ===\n";
$nt = epc_clp_run_cmd('nginx -t 2>&1');
echo substr(trim((string) $nt['output']), 0, 800) . "\n";

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

$vfAudit = epc_clp_vhost_fetch($cookie, $platformSite);
echo "\n=== server_name audit ===\n";
foreach (epc_clp_vhost_audit_server_names($vfAudit['vhost']) as $snLine) {
	echo '  ' . $snLine . "\n";
}

echo "\n=== Probes ===\n";
echo 'platform www.ecomae.com /: ' . epc_ndt_probe('http://127.0.0.1/', 'www.ecomae.com') . "\n";
foreach (array('thejewellerytrend', 'epartscart') as $key) {
	$t = $tenants[$key];
	echo "[{$key}]\n";
	echo '  origin /: ' . epc_ndt_probe('http://127.0.0.1/', $t['www']) . "\n";
	echo '  public /: ' . epc_ndt_probe('https://' . $t['www'] . '/') . "\n";
}
echo "\nDedupe complete.\n";
