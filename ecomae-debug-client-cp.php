<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');

$_SERVER['HTTP_HOST'] = 'www.epartscart.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['HTTPS'] = 'on';
$_SERVER['REQUEST_URI'] = '/cp/control/portal/industry_settings';
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
chdir(__DIR__);

try {
	require __DIR__ . '/cp/index.php';
	echo "\n--- CP boot OK ---\n";
} catch (Throwable $e) {
	echo 'BOOT ERROR: ' . $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine() . "\n";
}
