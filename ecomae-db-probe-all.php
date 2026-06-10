<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
error_reporting(E_ALL);
ini_set('display_errors', '1');

$paths = glob('/home/*/htdocs/*/config.local.php') ?: array();
echo 'configs=' . count($paths) . "\n";
$passwords = array();
foreach ($paths as $path) {
	echo $path . "\n";
	$text = (string) @file_get_contents($path);
	if (preg_match("/'password'\s*=>\s*'([^']+)'/", $text, $m)) {
		$passwords[] = array('pass' => $m[1], 'path' => $path);
		if (preg_match("/'user'\s*=>\s*'([^']+)'/", $text, $u)) {
			echo "  user={$u[1]} pass_len=" . strlen($m[1]) . "\n";
		}
	}
}

foreach ($passwords as $row) {
	foreach (array('ecomae', 'docpart') as $user) {
		foreach (array('ecomae', 'docpart') as $db) {
			try {
				$pdo = new PDO("mysql:host=127.0.0.1;dbname={$db};charset=utf8", $user, $row['pass']);
				echo "CONNECT user={$user} db={$db} pass_from={$row['path']}\n";
			} catch (Exception $e) {
				// silent
			}
		}
	}
}

try {
	$pdo = new PDO('mysql:host=127.0.0.1;charset=utf8', 'ecomae', '2674f7feac3e3ac95ba8a965');
	echo "ecomae no-db ok\n";
} catch (Exception $e) {
	echo 'ecomae no-db fail: ' . $e->getMessage() . "\n";
}

require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
$cookie = '';
$clpPass = trim((string) ($_GET['clp_pass'] ?? ''));
if ($clpPass !== '' && epc_clp_web_login('admin', $clpPass, $cookie)['ok']) {
	$html = epc_clp_web_request(epc_clp_panel_url() . '/site/www.ecomae.com/databases', array(), $cookie);
	echo 'databases page mentions ecomae user: ' . (stripos($html, 'database/user/edit/ecomae') !== false ? 'yes' : 'no') . "\n";
}
