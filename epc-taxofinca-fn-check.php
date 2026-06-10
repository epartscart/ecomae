<?php
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
echo function_exists('epc_clp_vhost_configure_tenant_direct_php') ? "configure_fn=yes\n" : "configure_fn=no\n";
$pass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($pass === '') {
	exit;
}
$cookie = '';
epc_clp_web_login('admin', $pass, $cookie);
$r = epc_clp_vhost_configure_tenant_direct_php($cookie, 'www.epartscart.com', array('www.taxofinca.com', 'taxofinca.com'), 'www.epartscart.com');
echo implode("\n", $r['log']) . "\nok=" . ($r['ok'] ? '1' : '0') . "\n";
