<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$site = trim((string) ($_GET['site'] ?? 'cp.ecomae.com'));
$cookie = '';
epc_clp_web_login('admin', $clpPass, $cookie);
$html = epc_clp_web_request(epc_clp_panel_url() . '/site/' . rawurlencode($site) . '/vhost', array(), $cookie);
$vhost = '';
if (preg_match('/<textarea[^>]*name="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/', $html, $m)) {
	$vhost = html_entity_decode($m[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
} elseif (preg_match('/<div id="editor">([\s\S]*?)<\/div>\s*<textarea/s', $html, $m2)) {
	$vhost = html_entity_decode($m2[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
}
if ($vhost !== '') {
	echo $vhost;
	exit;
}
echo substr($html, 0, 12000);
