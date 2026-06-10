<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$newPass = trim((string) ($_GET['db_password'] ?? 'EcomaeDb2026xK9mQ2'));
if ($clpPass === '') {
	exit("clp_pass required\n");
}
$cookie = '';
if (empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("CloudPanel login failed\n");
}
$panel = epc_clp_panel_url();
$editPath = '/site/www.ecomae.com/database/user/edit/ecomae';
$html = epc_clp_web_request($panel . $editPath, array(), $cookie);
if (!preg_match('/name="(site_database_user_edit\[_token\])" value="([^"]+)"/', $html, $m)) {
	echo substr($html, 0, 2000) . "\n";
	exit("token missing\n");
}
$token = $m[2];
echo "token ok\n";
$data = array(
	'site_database_user_edit' => array(
		'password' => $newPass,
		'_token' => $token,
		'submit' => '',
	),
);
$resp = epc_clp_web_request($panel . $editPath, array(
	'method' => 'POST',
	'body' => http_build_query($data),
), $cookie);
echo 'post len=' . strlen($resp) . ' err=' . (stripos($resp, 'Error Occurred') !== false ? 'yes' : 'no') . "\n";
if (strlen($resp) < 3000) {
	echo substr(strip_tags($resp), 0, 1500) . "\n";
}

$cfgWww = "<?php\n\$epc_config_local = array(\n\t'password' => " . var_export($newPass, true) . ",\n\t'db' => 'ecomae',\n\t'user' => 'ecomae',\n\t'domain_path' => 'https://www.ecomae.com/',\n\t'from_name' => 'ecomae',\n\t'from_email' => 'hello@ecomae.com',\n);\n";
$cfgCp = str_replace('www.ecomae.com', 'cp.ecomae.com', $cfgWww);
foreach (array(
	'/home/ecomae/htdocs/www.ecomae.com/config.local.php',
	'/home/ecomaecp/htdocs/cp.ecomae.com/config.local.php',
) as $path) {
	file_put_contents($path, strpos($path, 'cp.ecomae') !== false ? $cfgCp : $cfgWww);
	echo "wrote {$path}\n";
}

try {
	$pdo = new PDO('mysql:host=127.0.0.1;dbname=ecomae;charset=utf8', 'ecomae', $newPass);
	echo "pdo ok tables=" . $pdo->query('SHOW TABLES')->rowCount() . "\n";
} catch (Exception $e) {
	echo "pdo fail " . $e->getMessage() . "\n";
}
