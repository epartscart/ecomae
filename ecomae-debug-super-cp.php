<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
header('Content-Type: text/plain; charset=utf-8');
$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['HTTPS'] = 'on';
$_SERVER['REQUEST_URI'] = '/cp/shop/tenant_hub/tenant_hub?tab=onboard';
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
chdir(__DIR__);
try {
	require __DIR__ . '/cp/index.php';
	echo "\nOK\n";
} catch (Throwable $e) {
	echo 'ERR: ' . $e->getMessage() . ' @ ' . $e->getFile() . ':' . $e->getLine() . "\n";
}
