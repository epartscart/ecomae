<?php
/**
 * Fast LE activate + Model C ssl patch only (no long LE create waits).
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(90);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$apply = !empty($_GET['apply']);
$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';
$ip = '31.97.216.247';

$tenants = array(
	array('www' => 'www.epartscart.com', 'bare' => 'epartscart.com'),
	array('www' => 'www.thejewellerytrend.com', 'bare' => 'thejewellerytrend.com'),
);

function epc_sat_sni(string $host, string $ip): string
{
	$r = epc_clp_run_cmd('echo | openssl s_client -connect ' . escapeshellarg($ip . ':443')
		. ' -servername ' . escapeshellarg($host)
		. ' 2>/dev/null | openssl x509 -noout -subject 2>/dev/null');
	$o = trim((string) ($r['output'] ?? ''));
	return $o !== '' ? $o : '(none)';
}

echo "=== ssl activate fast apply=" . ($apply ? 'yes' : 'no') . " ===\n";
foreach ($tenants as $t) {
	echo "BEFORE {$t['www']}: " . epc_sat_sni($t['www'], $ip) . "\n";
}

if (!$apply || $clpPass === '') {
	echo "\nDry run. apply=1&clp_pass=...\n";
	exit;
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}

$assume = array();
foreach ($tenants as $t) {
	echo "\n=== {$t['www']} ===\n";
	$act = epc_clp_web_activate_certificates($cookie, $t['www']);
	foreach ($act['log'] as $line) {
		echo $line . "\n";
	}
	if (!empty($act['ok'])) {
		$assume[$t['www']] = true;
	}
	if (epc_clp_ssl_certificate_paths($t['www']) === null && empty($assume[$t['www']])) {
		$le = epc_clp_web_install_ssl($cookie, $t['www'], array($t['bare']));
		foreach (array_slice($le['log'], 0, 6) as $line) {
			echo 'LE: ' . $line . "\n";
		}
		if (!empty($le['ok'])) {
			$assume[$t['www']] = true;
		}
		$act2 = epc_clp_web_activate_certificates($cookie, $t['www']);
		if (!empty($act2['ok'])) {
			$assume[$t['www']] = true;
		}
	}
	echo 'assume_paths=' . (!empty($assume[$t['www']]) ? 'yes' : 'no') . "\n";
}

$vf = epc_clp_vhost_fetch($cookie, $platformSite);
if ($vf['vhost'] === '' || $vf['token'] === '') {
	exit("\nvhost fetch failed\n");
}
$vhost = $vf['vhost'];
$patched = 0;
foreach ($tenants as $t) {
	$hosts = array($t['www'], $t['bare']);
	$useAssume = !empty($assume[$t['www']]);
	$patch = epc_clp_vhost_patch_server_ssl_for_hosts($vhost, $hosts, $t['www'], $useAssume);
	$vhost = $patch['vhost'];
	$patched += (int) $patch['patched'];
	foreach ($patch['log'] as $line) {
		echo 'patch: ' . $line . "\n";
	}
}
$vhost = epc_clp_vhost_patch_tenant_direct_root($vhost, $platformDocroot);
epc_clp_vhost_save($cookie, $platformSite, $vhost, $vf['token']);
echo "vhost saved patches={$patched}\n";

echo "\n=== AFTER ===\n";
foreach ($tenants as $t) {
	echo "{$t['www']}: " . epc_sat_sni($t['www'], $ip) . "\n";
}
echo "Done.\n";
