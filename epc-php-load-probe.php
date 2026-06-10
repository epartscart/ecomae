<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
error_reporting(E_ALL);
ini_set('display_errors', '1');
$_SERVER['REQUEST_URI'] = '/en/';
$_GET = array();
$_SERVER['QUERY_STRING'] = '';
require __DIR__ . '/index.php';
