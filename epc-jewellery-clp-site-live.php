<?php
/**
 * Dedicated CLP site for jewellery (shared docroot + platform origin cert).
 * Use when Model C block is in CLP vhost DB but not on nginx disk.
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(90);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$apply = !empty($_GET['apply']);
$www = 'www.thejewellerytrend.com';
$bare = 'thejewellerytrend.com';
$platformSite = 'www.ecomae.com';
$platformDocroot = '/home/ecomae/htdocs/www.ecomae.com';
$platformCert = 'www.ecomae.com';
$siteUser = 'thejewellerytrend';
$sitePass = trim((string) ($_GET['site_user_password'] ?? 'EpcJewellery2026!'));
$ip = '31.97.216.247';

function epc_jcsl_sni(string $host, string $ip): string
{
	$r = epc_clp_run_cmd('echo | openssl s_client -connect ' . escapeshellarg($ip . ':443')
		. ' -servername ' . escapeshellarg($host)
		. ' 2>/dev/null | openssl x509 -noout -subject 2>/dev/null');
	$o = trim((string) ($r['output'] ?? ''));
	return $o !== '' ? $o : '(none)';
}

echo "=== jewellery CLP dedicated site ===\napply=" . ($apply ? 'yes' : 'no') . "\n";
echo "BEFORE SNI: " . epc_jcsl_sni($www, $ip) . "\n\n";

if (!$apply || $clpPass === '') {
	echo "Need apply=1&clp_pass=\n";
	exit;
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}

$modelCTenants = array(
	array('key' => 'epartscart', 'hosts' => array('www.epartscart.com', 'epartscart.com')),
	array('key' => 'taxofinca', 'hosts' => array('www.taxofinca.com', 'taxofinca.com')),
	array('key' => 'electronicae', 'hosts' => array('www.electronicae.com', 'electronicae.com')),
	array('key' => 'stylenlook', 'hosts' => array('www.stylenlook.com', 'stylenlook.com')),
);
$vh = epc_clp_vhost_configure_model_c_tenants($cookie, $platformSite, $modelCTenants);
echo "Model C (without jewellery): " . implode(' | ', array_slice($vh['log'], 0, 4)) . "\n";

if (!epc_clp_web_site_listed(epc_clp_web_request(epc_clp_panel_url() . '/', array(), $cookie), $www)) {
	$prov = epc_clp_provision_php_site(array(
		'domain' => $www,
		'site_user' => $siteUser,
		'site_user_password' => $sitePass,
		'php_version' => '8.3',
	));
	foreach (array_slice($prov['log'], 0, 6) as $line) {
		echo 'provision: ' . $line . "\n";
	}
	if (empty($prov['ok'])) {
		$create = epc_clp_web_create_php_site($cookie, array(
			'domain' => $www,
			'site_user' => $siteUser,
			'site_user_password' => $sitePass,
			'php_version' => '8.3',
		));
		echo 'web create: ' . implode(' | ', array_slice($create['log'], 0, 4)) . "\n";
	}
}
$repoint = epc_clp_web_set_site_docroot($cookie, $www, $platformDocroot);
echo 'docroot: ' . implode(' | ', array_slice($repoint['log'], 0, 3)) . "\n";

$vf = epc_clp_vhost_fetch($cookie, $www);
if ($vf['vhost'] === '' || $vf['token'] === '') {
	exit("Could not fetch vhost for {$www}\n");
}
$patch = epc_clp_vhost_patch_server_ssl_for_hosts($vf['vhost'], array($www, $bare), $platformCert, true);
$vhost = epc_clp_vhost_patch_tenant_direct_root($patch['vhost'], $platformDocroot);
$removed = 0;
$vhost = epc_clp_vhost_strip_ssl_reject_for_hosts($vhost, array($www, $bare), $removed);
if (!epc_clp_vhost_save($cookie, $www, $vhost, $vf['token'])) {
	exit("vhost save failed\n");
}
echo 'ssl patch: ' . implode('; ', $patch['log']) . "\n";
echo "ssl_reject removed={$removed}\n";

echo "\nAFTER SNI: " . epc_jcsl_sni($www, $ip) . "\n";
$code = epc_clp_run_cmd("curl -sI --max-time 8 -H 'Host: {$www}' https://127.0.0.1/en/ -k 2>&1 | head -1")['output'];
echo "origin https /en/: " . trim((string) $code) . "\n";
echo "Done.\n";
