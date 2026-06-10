<?php
/**
 * Step 2 — CloudPanel provision for ecomae.com (www + cp + database + SSL).
 * https://www.epartscart.com/epc-ecomae-provision.php?token=epartscart-deploy-2026
 * Optional: &site_user_password=...&db_password=...&clp_pass=...
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$siteUser = 'ecomae';
$sitePass = trim((string) ($_GET['site_user_password'] ?? getenv('ECOMAE_SITE_PASS') ?: ''));
$dbPass = trim((string) ($_GET['db_password'] ?? getenv('ECOMAE_DB_PASS') ?: ''));
$phpVer = trim((string) ($_GET['php'] ?? '8.3'));

if ($sitePass === '') {
	$sitePass = bin2hex(random_bytes(8));
}
if ($dbPass === '') {
	$dbPass = bin2hex(random_bytes(12));
}

$log = array();
$domains = array('www.ecomae.com', 'cp.ecomae.com');
$ok = true;

foreach ($domains as $domain) {
	if (epc_clp_site_exists($domain)) {
		$log[] = "Skip existing site: {$domain}";
		continue;
	}
	$r = epc_clp_provision_php_site(array(
		'domain' => $domain,
		'site_user' => $siteUser,
		'site_user_password' => $sitePass,
		'php_version' => $phpVer,
	));
	$log = array_merge($log, $r['log']);
	if (!$r['ok']) {
		$ok = false;
	}
}

$dbResult = epc_clp_provision_database(array(
	'domain' => 'www.ecomae.com',
	'database_name' => 'ecomae',
	'database_user' => 'ecomae',
	'database_password' => $dbPass,
));
$log = array_merge($log, $dbResult['log']);
if (!$dbResult['ok'] && epc_clp_available()) {
	$log[] = 'Note: DB may already exist — continuing';
}

$docroot = epc_clp_guess_docroot($siteUser, 'www.ecomae.com');

echo json_encode(array(
	'status' => $ok || !epc_clp_available(),
	'message' => epc_clp_available()
		? ($ok ? 'CloudPanel provision completed for ecomae.com' : 'CloudPanel CLI failed — create sites manually (root/sudo required for clpctl)')
		: 'clpctl not found — create sites manually in CloudPanel (see manual_steps)',
	'site_user' => $siteUser,
	'site_user_password' => $sitePass,
	'database' => 'ecomae',
	'database_user' => 'ecomae',
	'database_password' => $dbPass,
	'docroot_hint' => $docroot,
	'domains' => $domains,
	'log' => $log,
	'next' => 'Run epc-ecomae-deploy-portal.php with db_password=' . urlencode($dbPass),
	'manual_steps' => array(
		'CloudPanel → Sites → Create PHP Site: www.ecomae.com (user: ecomae, PHP 8.3, Generic template)',
		'CloudPanel → Sites → Create PHP Site: cp.ecomae.com (same user ecomae)',
		'CloudPanel → Databases → Add database "ecomae" on www.ecomae.com site',
		'Enable SSL (Let\'s Encrypt) for both domains',
		'Run: epc-ecomae-deploy-portal.php?token=...&db_password=YOUR_DB_PASSWORD',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
