<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($clpPass === '') {
	exit("clp_pass required\n");
}
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
foreach (array('www.ecomae.com', 'cp.ecomae.com', 'www.epartscart.com') as $site) {
	echo "=== {$site} ===\n";
	$settings = epc_clp_web_request($panel . '/site/' . rawurlencode($site) . '/settings', array(), $cookie);
	if (preg_match('/siteUserName[^>]*value="([^"]+)"/', $settings, $um)) {
		echo "site_user={$um[1]}\n";
	}
	if (preg_match('/rootDirectory[^>]*value="([^"]+)"/', $settings, $rd)) {
		echo "root_directory={$rd[1]}\n";
		$user = isset($um[1]) ? $um[1] : 'ecomae';
		if ($rd[1][0] !== '/') {
			echo "guessed_root=/home/{$user}/htdocs/{$rd[1]}\n";
		}
	}
	$html = epc_clp_web_request($panel . '/site/' . rawurlencode($site) . '/vhost', array(), $cookie);
	echo "vhost len=" . strlen($html) . "\n";
	if (preg_match('/<textarea[^>]*name="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/', $html, $vm)) {
		$vh = html_entity_decode($vm[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
		foreach (array('root', 'server_name', 'listen') as $key) {
			if (preg_match_all('/^\s*' . $key . '\s+([^;]+);/m', $vh, $mm)) {
				foreach ($mm[1] as $val) {
					echo "{$key}={$val}\n";
				}
			}
		}
	}
}
