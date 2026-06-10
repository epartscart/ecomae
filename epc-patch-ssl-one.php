<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$pass = trim((string) ($_GET['clp_pass'] ?? ''));
$www = trim((string) ($_GET['www'] ?? 'www.epartscart.com'));
$cookie = '';
epc_clp_web_login('admin', $pass, $cookie);
$vf = epc_clp_vhost_fetch($cookie, 'www.ecomae.com');
$patch = epc_clp_vhost_patch_server_ssl_for_hosts($vf['vhost'], array($www, preg_replace('/^www\./', '', $www)), $www, true);
echo 'patched=' . $patch['patched'] . "\n";
foreach ($patch['log'] as $l) {
	echo $l . "\n";
}
if ($patch['patched'] > 0) {
	epc_clp_vhost_save($cookie, 'www.ecomae.com', $patch['vhost'], $vf['token']);
	echo "saved\n";
}
if (preg_match('/server_name\s+' . preg_quote($www, '/') . '[^;]*;[\s\S]{0,400}/', $patch['vhost'], $m)) {
	echo "\nSNIPPET:\n" . $m[0] . "\n";
}
