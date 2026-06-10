<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';

$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
$dbName = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($_GET['db'] ?? 'demotest01')));
$dbUser = $dbName;
$dbPass = bin2hex(random_bytes(8)) . 'A!';

$cookie = '';
if ($clpPass === '' || empty(epc_clp_web_login('admin', $clpPass, $cookie)['ok'])) {
	exit("login fail\n");
}
$panel = epc_clp_panel_url();
$html = epc_clp_web_request($panel . '/site/www.ecomae.com/database/new', array(), $cookie);
if (!preg_match('/name="([^"]*\[_token\])" value="([^"]+)"/', $html, $m)) {
	exit("no token\n");
}
$body = http_build_query(array(
	'site_database' => array(
		'name' => $dbName,
		'userName' => $dbUser,
		'userPassword' => $dbPass,
		'submit' => 'Create',
		'_token' => $m[2],
	),
));
$resp = epc_clp_web_request($panel . '/site/www.ecomae.com/database/new', array('method' => 'POST', 'body' => $body), $cookie);
echo "db={$dbName} pass={$dbPass}\n";
echo "resp len=" . strlen($resp) . "\n";
if (stripos($resp, 'error') !== false || stripos($resp, 'alert-danger') !== false) {
	echo "possible error in response\n";
}
echo substr($resp, 0, 1500) . "\n---\n";
sleep(3);
echo "exists=" . (epc_portal_demo_db_exists($dbName, $dbUser, $dbPass) ? 'yes' : 'no') . "\n";

$list = epc_clp_web_request($panel . '/site/www.ecomae.com/databases', array(), $cookie);
if (preg_match_all('#database/delete/([a-z0-9_]+)#', $list, $dm)) {
	echo "databases: " . implode(', ', array_unique($dm[1])) . "\n";
}
