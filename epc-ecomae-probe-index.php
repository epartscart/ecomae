<?php
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
error_reporting(E_ALL);
ini_set('display_errors', '1');

define('_ASTEXE_', 1);
$isFrontMode = 1;

ob_start();
try {
	require __DIR__ . '/index.php';
	$out = ob_get_clean();
	echo "index_ok len=" . strlen($out) . "\n";
	echo substr($out, 0, 800) . "\n";
} catch (Throwable $e) {
	ob_end_clean();
	echo "exception: " . $e->getMessage() . "\n";
}
