<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$domain = trim((string) ($_GET['site'] ?? 'cp.ecomae.com'));
$targetRoot = trim((string) ($_GET['root'] ?? ''));
if ($clpPass === '') {
	exit("clp_pass required\n");
}
if ($targetRoot === '') {
	$targetRoot = $domain === 'cp.ecomae.com'
		? '/home/ecomae/htdocs/cp.ecomae.com'
		: '/home/ecomae/htdocs/www.ecomae.com';
}

$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
$settingsHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/settings', array(), $cookie);
if (!preg_match('/name="site_domain_settings\[_token\]" value="([^"]+)"/', $settingsHtml, $tm)) {
	exit("domain settings token missing\n");
}
$data = array(
	'site_domain_settings' => array(
		'rootDirectory' => $targetRoot,
		'_token' => $tm[1],
		'submit' => '',
	),
);
$resp = epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/settings', array(
	'method' => 'POST',
	'body' => http_build_query($data),
), $cookie);
echo "domain settings POST len=" . strlen($resp) . " err=" . (stripos($resp, 'Error Occurred') !== false ? 'yes' : 'no') . "\n";

$vhHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/vhost', array(), $cookie);
if (preg_match('/name="token" value="([^"]+)"/', $vhHtml, $vt)) {
	$vhost = '';
	if (preg_match('/id="vhost-template"[^>]*>([\s\S]*?)<\/textarea>/', $vhHtml, $vm)) {
		$vhost = html_entity_decode($vm[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
	} elseif (preg_match('/ace_editor[^>]*data-value="([^"]+)"/', $vhHtml, $am)) {
		$vhost = html_entity_decode($am[1], ENT_QUOTES | ENT_HTML5, 'UTF-8');
	}
	if ($vhost !== '') {
		$vhost = preg_replace('/root\s+[^;]+;/', 'root ' . $targetRoot . ';', $vhost);
		epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/vhost', array(
			'method' => 'POST',
			'body' => http_build_query(array(
				'vhost-update' => '1',
				'vhost-template' => $vhost,
				'token' => $vt[1],
			)),
		), $cookie);
		echo "vhost root updated to {$targetRoot}\n";
	}
}

$perm = epc_clp_run('system:permissions:reset --directories=755 --files=644 --path=' . escapeshellarg($targetRoot));
echo "permissions: " . substr($perm['output'], 0, 200) . "\n";
