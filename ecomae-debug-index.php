<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
$_SERVER['HTTP_HOST'] = 'www.ecomae.com';
$_SERVER['SERVER_NAME'] = 'www.ecomae.com';
$_SERVER['REQUEST_URI'] = '/';
$_SERVER['REQUEST_METHOD'] = 'GET';
$_SERVER['HTTPS'] = 'on';
$_SERVER['DOCUMENT_ROOT'] = __DIR__;
chdir(__DIR__);
require __DIR__ . '/index.php';
