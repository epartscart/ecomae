<?php
/** Recreate CloudPanel MySQL user for ASAP tenant DB. */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? $_POST['clp_pass'] ?? ''));
$dbName = 'asap';
$dbUser = 'asap';
$dbPass = trim((string) ($_GET['db_password'] ?? '8b5738124ec4feacAx!'));
if ($clpPass === '') {
	exit("clp_pass required\n");
}
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
$list = epc_clp_web_request($panel . '/site/www.ecomae.com/databases', array(), $cookie);
if (preg_match('#/site/www\.ecomae\.com/database/user/delete/asap\?token=([^"\']+)#', $list, $dm)) {
	$deleteUrl = $panel . '/site/www.ecomae.com/database/user/delete/asap?token=' . urlencode($dm[1]);
	epc_clp_web_request($deleteUrl, array('method' => 'POST', 'body' => ''), $cookie);
	echo "deleted existing asap user\n";
}
$newForm = epc_clp_web_request($panel . '/site/www.ecomae.com/database/user/new', array(), $cookie);
if (!preg_match('/name="site_database_user\[_token\]" value="([^"]+)"/', $newForm, $tm)) {
	exit("new user token missing\n");
}
$dbId = '';
if (preg_match('/value="(\d+)"[^>]*>\s*asap\s*</i', $newForm, $m)) {
	$dbId = $m[1];
}
if ($dbId === '' && preg_match('/value="(\d+)"[^>]*>\s*' . preg_quote($dbName, '/') . '\s*</i', $list, $m2)) {
	$dbId = $m2[1];
}
echo "dbId={$dbId}\n";
$data = array(
	'site_database_user' => array(
		'userName' => $dbUser,
		'password' => $dbPass,
		'database' => $dbId !== '' ? $dbId : $dbName,
		'permissions' => 'rw',
		'_token' => $tm[1],
		'submit' => '',
	),
);
epc_clp_web_request($panel . '/site/www.ecomae.com/database/user/new', array(
	'method' => 'POST',
	'body' => http_build_query($data),
), $cookie);
echo "user recreate POST sent password={$dbPass}\n";
try {
	$pdo = new PDO('mysql:host=127.0.0.1;dbname=' . $dbName . ';charset=utf8', $dbUser, $dbPass);
	echo 'pdo ok tables=' . $pdo->query('SHOW TABLES')->rowCount() . "\n";
	$cfgFile = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
	$epc_config_local = null;
	if (is_file($cfgFile)) {
		include $cfgFile;
		$platDb = trim((string) ($epc_config_local['db'] ?? 'ecomae'));
		$platUser = trim((string) ($epc_config_local['user'] ?? 'ecomae'));
		$platPass = trim((string) ($epc_config_local['password'] ?? ''));
		if ($platPass !== '') {
			require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
			require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
			$platformPdo = new PDO(
				'mysql:host=127.0.0.1;dbname=' . $platDb . ';charset=utf8',
				$platUser,
				$platPass,
				array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
			);
			epc_portal_db_ensure($platformPdo);
			$save = epc_portal_save_tenant($platformPdo, array(
				'site_key' => 'asap',
				'hostname' => 'www.ecomae.com',
				'industry_code' => 'erp_standalone',
				'status' => 'live',
				'trade_name' => 'ASAP',
				'hub_name' => 'ASAP',
				'from_email' => 'admin@asap-ae.com',
				'db_name' => $dbName,
				'db_user' => $dbUser,
				'db_password' => $dbPass,
				'hosted_on' => 'platform',
				'erp_only_shared' => 1,
				'notes' => 'ASAP CLP user recreate — registry password sync',
			));
			echo 'registry sync: ' . ($save['ok'] ? 'OK' : 'FAIL') . "\n";
		}
	}
} catch (Exception $e) {
	echo 'pdo fail: ' . $e->getMessage() . "\n";
}
