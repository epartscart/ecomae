<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$pass = trim((string) ($_GET['clp_pass'] ?? ''));
$host = trim((string) ($_GET['host'] ?? 'www.epartscart.com'));
$cookie = '';
epc_clp_web_login('admin', $pass, $cookie);
$vf = epc_clp_vhost_fetch($cookie, 'www.ecomae.com');
$v = $vf['vhost'];
if ($v === '') {
	exit("no vhost\n");
}
if (preg_match('/# EPC_TENANT_DIRECT_START[\s\S]*?server_name[^;]*' . preg_quote($host, '/') . '[\s\S]*?# EPC_TENANT_DIRECT_END/', $v, $m)) {
	echo $m[0] . "\n";
} else {
	echo "no EPC_TENANT_DIRECT block for {$host}\n";
	if (preg_match('/server_name[^;]*' . preg_quote($host, '/') . '[\s\S]{0,800}/', $v, $m2)) {
		echo $m2[0] . "\n";
	}
}
