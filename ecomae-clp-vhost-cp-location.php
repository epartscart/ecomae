<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$platformSite = 'www.ecomae.com';
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
$vhHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($platformSite) . '/vhost', array(), $cookie);
if (!preg_match('/name="token" value="([^"]+)"/', $vhHtml, $vt)) {
	exit("vhost token missing\n");
}
if (!preg_match('/<div id="editor">([\s\S]*?)<\/div>\s*<textarea/s', $vhHtml, $em)) {
	exit("vhost editor missing\n");
}
$vhost = html_entity_decode($em[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
$cpBlock = <<<'NGINX'

  location = /cp {
    return 301 /cp/;
  }

  location /cp/ {
    try_files $uri $uri/ /cp/index.php?$args;
  }

NGINX;
if (stripos($vhost, 'location /cp/') !== false) {
	echo "cp location already present\n";
	exit;
}
$vhost = preg_replace('/(\s+try_files \$uri \$uri\/ \/index\.php\?\$args;)/', $cpBlock . '$1', $vhost, 1, $count);
if ($count < 1) {
	exit("try_files anchor missing\n");
}
epc_clp_web_request($panel . '/site/' . rawurlencode($platformSite) . '/vhost', array(
	'method' => 'POST',
	'body' => http_build_query(array(
		'vhost-update' => '1',
		'vhost-template' => $vhost,
		'token' => $vt[1],
	)),
), $cookie);
echo "vhost cp location added\n";
