<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
$cookie = '';
epc_clp_web_login('admin', (string) ($_GET['clp_pass'] ?? ''), $cookie);
$html = epc_clp_web_request(epc_clp_panel_url() . '/site/www.ecomae.com/vhost', array(), $cookie);
if (preg_match('/<div id="editor">([\s\S]*?)<\/div>\s*<textarea/s', $html, $em)) {
	echo html_entity_decode($em[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
}
