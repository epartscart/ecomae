<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$domain = 'cp.ecomae.com';
$targetRoot = '/home/ecomae/htdocs/cp.ecomae.com';
if ($clpPass === '') {
	exit("clp_pass required\n");
}
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
$settingsHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/settings', array(), $cookie);
echo "settings len=" . strlen($settingsHtml) . "\n";
if (preg_match_all('/<input[^>]+>/i', $settingsHtml, $inputs)) {
	foreach ($inputs[0] as $inp) {
		if (stripos($inp, 'token') !== false || stripos($inp, 'root') !== false || stripos($inp, 'siteUser') !== false || stripos($inp, 'php') !== false) {
			echo $inp . "\n";
		}
	}
}
if (preg_match_all('/<select[^>]*name="([^"]+)"[^>]*>([\s\S]*?)<\/select>/i', $settingsHtml, $selects, PREG_SET_ORDER)) {
	foreach ($selects as $sel) {
		if (stripos($sel[1], 'root') !== false || stripos($sel[1], 'user') !== false || stripos($sel[1], 'php') !== false) {
			echo "SELECT {$sel[1]} " . substr(strip_tags($sel[2]), 0, 120) . "\n";
		}
	}
}

$vhHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/vhost', array(), $cookie);
if (preg_match('/<div id="editor">([\s\S]*?)<\/div>\s*<textarea/s', $vhHtml, $em)) {
	$vh = html_entity_decode($em[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
	echo "\nvhost editor snippet:\n" . substr($vh, 0, 800) . "\n";
}

if (!empty($_GET['apply'])) {
	if (!preg_match('/name="site_settings\[_token\]" value="([^"]+)"/', $settingsHtml, $tm)) {
		exit("settings token missing\n");
	}
	$data = array(
		'site_settings' => array(
			'rootDirectory' => $targetRoot,
			'_token' => $tm[1],
			'submit' => '',
		),
	);
	$resp = epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/settings', array(
		'method' => 'POST',
		'body' => http_build_query($data),
	), $cookie);
	echo "settings POST len=" . strlen($resp) . " err=" . (stripos($resp, 'Error Occurred') !== false ? 'yes' : 'no') . "\n";

	if (preg_match('/name="token" value="([^"]+)"/', $vhHtml, $vt) && preg_match('/id="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/', $vhHtml, $vm2)) {
		$vhost = html_entity_decode($vm2[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
		$vhost = preg_replace('/root\s+[^;]+;/', 'root ' . $targetRoot . ';', $vhost);
		epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/vhost', array(
			'method' => 'POST',
			'body' => http_build_query(array(
				'vhost-update' => '1',
				'vhost-template' => $vhost,
				'token' => $vt[1],
			)),
		), $cookie);
		echo "vhost POST sent root={$targetRoot}\n";
	}
}
