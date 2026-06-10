<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

foreach (array('www.epartscart.com', 'www.ecomae.com') as $host) {
	$_SERVER['HTTP_HOST'] = $host;
	$_SERVER['SERVER_NAME'] = $host;
	$_SERVER['HTTPS'] = 'on';
	$_SERVER['DOCUMENT_ROOT'] = __DIR__;
	define('_ASTEXE_', 1);
	require_once __DIR__ . '/config.php';
	$DP_Config = new DP_Config();
	require_once __DIR__ . '/content/general_pages/epc_portal.php';
	epc_portal_apply_config($DP_Config);
	echo "=== {$host} ===\n";
	echo "domain_path={$DP_Config->domain_path}\n";
	echo "db={$DP_Config->db} user={$DP_Config->user}\n";
	echo "is_client=" . (epc_portal_is_client_hostname($host) ? 'yes' : 'no') . "\n";
	$site = epc_portal_site_profile();
	echo "site_industry=" . ($site['industry'] ?? '') . "\n";
	echo "site_db=" . ($site['db'] ?? '') . "\n";
	echo "\n";
}
