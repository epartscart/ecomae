<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$newPass = trim((string) ($_GET['db_password'] ?? '2674f7feac3e3ac95ba8a965'));
if ($clpPass === '') {
	exit("clp_pass required\n");
}
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
$databases = epc_clp_web_request($panel . '/site/www.ecomae.com/databases', array(), $cookie);
if (stripos($databases, 'database/user/edit/ecomae') !== false
	&& preg_match('#/site/www\.ecomae\.com/database/user/delete/ecomae\?token=([^"\']+)#', $databases, $dm)) {
	$deleteUrl = $panel . '/site/www.ecomae.com/database/user/delete/ecomae?token=' . urlencode($dm[1]);
	$delResp = epc_clp_web_request($deleteUrl, array('method' => 'POST', 'body' => ''), $cookie);
	echo 'delete len=' . strlen($delResp) . "\n";
} else {
	echo "user already absent\n";
}

$newForm = epc_clp_web_request($panel . '/site/www.ecomae.com/database/user/new', array(), $cookie);
if (!preg_match('/name="site_database_user\[_token\]" value="([^"]+)"/', $newForm, $tm)) {
	echo substr($newForm, 0, 800) . "\n";
	exit("new user token missing\n");
}
$dbId = '5';
if (preg_match('/name="site_database_user\[database\]"[^>]*>.*?value="(\d+)"[^>]*>ecomae/', $newForm, $dbm)) {
	$dbId = $dbm[1];
}
$data = array(
	'site_database_user' => array(
		'userName' => 'ecomae',
		'password' => $newPass,
		'database' => $dbId,
		'permissions' => 'rw',
		'_token' => $tm[1],
		'submit' => '',
	),
);
$createResp = epc_clp_web_request($panel . '/site/www.ecomae.com/database/user/new', array(
	'method' => 'POST',
	'body' => http_build_query($data),
), $cookie);
echo 'create len=' . strlen($createResp) . ' err=' . (stripos($createResp, 'Error Occurred') !== false ? 'yes' : 'no') . "\n";

$cfgWww = "<?php\n\$epc_config_local = array(\n\t'password' => " . var_export($newPass, true) . ",\n\t'db' => 'ecomae',\n\t'user' => 'ecomae',\n\t'domain_path' => 'https://www.ecomae.com/',\n\t'from_name' => 'ecomae',\n\t'from_email' => 'hello@ecomae.com',\n);\n";
$cfgCp = str_replace('www.ecomae.com', 'cp.ecomae.com', $cfgWww);
file_put_contents('/home/ecomae/htdocs/www.ecomae.com/config.local.php', $cfgWww);
file_put_contents('/home/ecomaecp/htdocs/cp.ecomae.com/config.local.php', $cfgCp);

try {
	$pdo = new PDO('mysql:host=127.0.0.1;dbname=ecomae;charset=utf8', 'ecomae', $newPass);
	echo 'pdo ok tables=' . $pdo->query('SHOW TABLES')->rowCount() . "\n";
} catch (Exception $e) {
	echo 'pdo fail ' . $e->getMessage() . "\n";
}
