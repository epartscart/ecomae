<?php
/**
 * Try CloudPanel login usernames + optional sudo clpctl (token required).
 * DELETE or restrict after use — accepts clp_pass in URL.
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(120);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$pass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($pass === '') {
	exit(json_encode(array('error' => 'clp_pass required')));
}

$users = array('admin');
$login = array();
	foreach ($users as $u) {
	$cookie = '';
	$r = epc_clp_web_login($u, $pass, $cookie, true);
	$sites = !empty($r['ok']) ? epc_clp_web_sites($cookie) : array();
	$login[$u] = array(
		'ok' => !empty($r['ok']),
		'detail' => $r['detail'],
		'sites_count' => count($sites),
		'sites_sample' => array_slice($sites, 0, 12),
	);
}

$sudo = array();
$escaped = str_replace("'", "'\\''", $pass);
foreach (array('site:list', 'app:list') as $sub) {
	$cmd = "echo '" . $escaped . "' | sudo -S /usr/bin/clpctl {$sub} 2>&1";
	$out = array();
	$code = 1;
	exec($cmd, $out, $code);
	$sudo[$sub] = array('code' => $code, 'output' => substr(implode("\n", $out), 0, 800));
}

echo json_encode(array('login' => $login, 'sudo_clpctl' => $sudo), JSON_PRETTY_PRINT);
