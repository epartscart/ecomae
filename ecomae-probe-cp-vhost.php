<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
$dst = '/home/ecomae/htdocs/cp.ecomae.com';
echo 'cp_index=' . (is_file($dst . '/cp/index.php') ? 'yes' : 'no') . ' bytes=' . (is_file($dst . '/cp/index.php') ? filesize($dst . '/cp/index.php') : 0) . "\n";
echo 'cp_tenant_stub=' . (is_file($dst . '/cp/content/shop/tenant_hub/tenant_hub_main_page.php') ? 'yes' : 'no') . "\n";
$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($clpPass === '') {
	exit("clp_pass required for vhost dump\n");
}
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
$cookie = '';
epc_clp_web_login('admin', $clpPass, $cookie);
$html = epc_clp_web_request(epc_clp_panel_url() . '/site/' . rawurlencode('cp.ecomae.com') . '/vhost', array(), $cookie);
if (preg_match('/<textarea[^>]*name="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/', $html, $m)) {
	$vh = html_entity_decode($m[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
} elseif (preg_match('/<div id="editor">([\s\S]*?)<\/div>\s*<textarea/s', $html, $m2)) {
	$vh = html_entity_decode($m2[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
} else {
	exit("no vhost\n");
}
foreach (preg_split('/\n/', $vh) as $line) {
	if (preg_match('/^\s*(server_name|root|listen|location|try_files)/', $line)) {
		echo trim($line) . "\n";
	}
}
