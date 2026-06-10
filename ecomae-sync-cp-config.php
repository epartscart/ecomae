<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$src = '/home/ecomae/htdocs/www.ecomae.com/config.local.php';
$targets = array(
	'/home/ecomae/htdocs/cp.ecomae.com/config.local.php',
);
$text = (string) file_get_contents($src);
$cpCfg = str_replace('www.ecomae.com', 'cp.ecomae.com', $text);
foreach ($targets as $path) {
	$n = file_put_contents($path, $cpCfg);
	echo "wrote {$path} bytes={$n}\n";
	require $path;
	$pass = isset($epc_config_local['password']) ? (string) $epc_config_local['password'] : '';
	try {
		$pdo = new PDO('mysql:host=127.0.0.1;dbname=ecomae;charset=utf8', 'ecomae', $pass);
		echo "verify ok tables=" . $pdo->query('SHOW TABLES')->rowCount() . "\n";
	} catch (Exception $e) {
		echo "verify fail " . $e->getMessage() . "\n";
	}
	unset($epc_config_local);
}
