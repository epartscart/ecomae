<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$pass = trim((string) ($_GET['clp_pass'] ?? ''));
$site = trim((string) ($_GET['site'] ?? 'www.epartscart.com'));
if ($pass === '') {
	exit("clp_pass required\n");
}
$cookie = '';
if (empty(epc_clp_web_login('admin', $pass, $cookie)['ok'])) {
	exit("login failed\n");
}
$vf = epc_clp_vhost_fetch($cookie, $site);
echo "token=" . ($vf['token'] !== '' ? 'yes' : 'no') . " vhost_len=" . strlen($vf['vhost']) . "\n\n";
echo $vf['vhost'];
