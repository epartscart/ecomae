<?php
/**
 * CloudPanel check + ecomae provisioning (runs ON VPS via localhost :8443 + sudo clpctl).
 * https://www.epartscart.com/epc-ecomae-clp-arrange.php?token=...&clp_pass=...&deploy=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');
set_time_limit(0);

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpUser = trim((string) ($_GET['clp_user'] ?? getenv('CLP_USER') ?: 'admin'));
$clpPass = trim((string) ($_GET['clp_pass'] ?? getenv('CLP_PASS') ?: getenv('EPC_CLP_PASS') ?: ''));
$sitePass = trim((string) ($_GET['site_user_password'] ?? ''));
$dbPass = trim((string) ($_GET['db_password'] ?? ''));
$doDeploy = !empty($_GET['deploy']);
$debug = !empty($_GET['debug']);

if ($sitePass === '') {
	$sitePass = bin2hex(random_bytes(8));
}
if ($dbPass === '') {
	$dbPass = bin2hex(random_bytes(12));
}

$result = array(
	'panel_external' => 'https://31.97.216.247:8443/login (often blocked externally — use SSH tunnel or server localhost)',
	'panel_local' => epc_clp_panel_url() . '/login',
	'clp_user' => $clpUser,
);

if ($clpPass === '') {
	$result['status'] = false;
	$result['message'] = 'Missing clp_pass — set CLP_PASS on server or pass clp_pass= in URL';
	echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
	exit;
}

$cookie = '';
$loginResult = epc_clp_web_login($clpUser, $clpPass, $cookie, $debug);
$webLogin = !empty($loginResult['ok']);
$result['web_login'] = $webLogin ? 'OK' : 'FAILED';
$result['web_login_detail'] = $loginResult['detail'];
$result['web_sites'] = $webLogin ? epc_clp_web_sites($cookie) : array();

$result['clp'] = epc_clp_diagnostics();

$siteUser = 'ecomae';
$domains = array('www.ecomae.com', 'cp.ecomae.com');
$provisionLog = array();
$ok = true;

if ($webLogin) {
	foreach ($domains as $domain) {
		$r = epc_clp_web_create_php_site($cookie, array(
			'domain' => $domain,
			'site_user' => $siteUser,
			'site_user_password' => $sitePass,
			'php_version' => '8.3',
		));
		$provisionLog[$domain] = array_merge(array('via' => 'web_ui'), $r['log']);
		if ($r['ok']) {
			$ssl = epc_clp_web_install_ssl($cookie, $domain);
			$provisionLog[$domain . '_ssl'] = $ssl['log'];
		}
		if (!$r['ok'] && !is_dir(epc_clp_guess_docroot($siteUser, $domain))) {
			$ok = false;
		}
	}
	$dbWeb = epc_clp_web_add_database($cookie, 'www.ecomae.com', 'ecomae', 'ecomae', $dbPass);
	$provisionLog['database'] = array_merge(array('via' => 'web_ui'), $dbWeb['log']);
} else {
	foreach ($domains as $domain) {
		$r = epc_clp_provision_php_site(array(
			'domain' => $domain,
			'site_user' => $siteUser,
			'site_user_password' => $sitePass,
			'php_version' => '8.3',
		));
		$provisionLog[$domain] = $r['log'];
		if (!$r['ok'] && !epc_clp_site_exists($domain)) {
			$ok = false;
		}
	}
	$dbResult = epc_clp_provision_database(array(
		'domain' => 'www.ecomae.com',
		'database_name' => 'ecomae',
		'database_user' => 'ecomae',
		'database_password' => $dbPass,
	));
	$provisionLog['database'] = $dbResult['log'];
}

$docroots = array();
foreach ($domains as $d) {
	$docroots[$d] = epc_clp_guess_docroot($siteUser, $d);
	$docroots[$d . '_exists'] = is_dir($docroots[$d]);
}
if ($webLogin) {
	$result['web_sites'] = epc_clp_web_sites($cookie);
}

$result['provision'] = array(
	'ok' => $ok || $docroots['www.ecomae.com_exists'] || $docroots['cp.ecomae.com_exists'],
	'log' => $provisionLog,
	'site_user' => $siteUser,
	'site_user_password' => $sitePass,
	'database' => 'ecomae',
	'database_user' => 'ecomae',
	'database_password' => $dbPass,
	'docroots' => $docroots,
);

if ($doDeploy && ($result['provision']['ok'] || $docroots['www.ecomae.com_exists'])) {
	$token = epc_deploy_token();
	$deployUrl = 'https://www.epartscart.com/epc-ecomae-deploy-portal.php?token='
		. urlencode($token) . '&db_password=' . urlencode($dbPass);
	$deployResp = @file_get_contents($deployUrl, false, stream_context_create(array(
		'http' => array('timeout' => 300),
		'ssl' => array('verify_peer' => false, 'verify_peer_name' => false),
	)));
	$result['deploy'] = array(
		'triggered' => true,
		'output' => substr((string) $deployResp, 0, 3000),
	);
}

$result['status'] = $webLogin || $result['provision']['ok'];
$result['message'] = $webLogin
	? 'CloudPanel login OK — provision ' . ($result['provision']['ok'] ? 'completed or sites exist' : 'needs manual CloudPanel UI')
	: (isset($loginResult['detail']['after_snippet']) && stripos($loginResult['detail']['after_snippet'], 'Invalid credentials') !== false
		? 'CloudPanel login failed: Invalid credentials — update CLP_PASS or reset admin password via root SSH (clpctl user:reset:password admin NEWPASS)'
		: 'CloudPanel web login failed — check clp_pass or use SSH tunnel');

if (!$webLogin && !$result['provision']['ok']) {
	$result['manual_steps'] = array(
		'Port 8443 is NOT open publicly — use SSH tunnel: ssh -L 8443:127.0.0.1:8443 root@31.97.216.247 then open https://127.0.0.1:8443/login',
		'Or SSH as root and run: bash scripts/ecomae-cloudpanel-root.sh (creates sites + DB + SSL)',
		'Reset CloudPanel admin password (root): clpctl user:reset:password admin YOUR_NEW_PASSWORD',
		'After sites exist, run: epc-ecomae-deploy-portal.php?token=...&db_password=...',
	);
}

echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
