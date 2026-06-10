<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
set_time_limit(0);
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$domain = 'cp.ecomae.com';
$siteUser = 'ecomae';
$sitePass = trim((string) ($_GET['site_user_password'] ?? '574948b90c302530'));
if ($clpPass === '') {
	exit("clp_pass required\n");
}
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}

$del = epc_clp_web_delete_site($cookie, $domain);
echo 'delete cp site: ' . implode(' ', $del['log']) . "\n";
sleep(2);

$create = epc_clp_web_create_php_site($cookie, array(
	'domain' => $domain,
	'site_user' => $siteUser,
	'site_user_password' => $sitePass,
	'php_version' => '8.3',
));
echo 'create: ' . implode(' | ', $create['log']) . "\n";

$panel = epc_clp_panel_url();
$settingsHtml = epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/settings', array(), $cookie);
if (preg_match('/name="site_domain_settings\[_token\]" value="([^"]+)"/', $settingsHtml, $tm)) {
	$root = '/home/ecomae/htdocs/cp.ecomae.com';
	epc_clp_web_request($panel . '/site/' . rawurlencode($domain) . '/settings', array(
		'method' => 'POST',
		'body' => http_build_query(array(
			'site_domain_settings' => array(
				'rootDirectory' => $root,
				'_token' => $tm[1],
				'submit' => '',
			),
		)),
	), $cookie);
	echo "root set {$root}\n";
}

$ssl = epc_clp_web_install_ssl($cookie, $domain);
echo 'ssl: ' . implode(' | ', array_slice($ssl['log'], 0, 2)) . "\n";

$perm = epc_clp_run('system:permissions:reset --directories=755 --files=644 --path=/home/ecomae/htdocs/cp.ecomae.com');
echo 'perm: ' . substr($perm['output'], 0, 120) . "\n";
