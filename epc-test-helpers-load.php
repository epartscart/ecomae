<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
try {
	require_once __DIR__ . '/content/general_pages/epc_cloudpanel_helpers.php';
	echo "helpers load OK\n";
	echo 'functions: ' . (function_exists('epc_clp_nginx_reload_with_pass') ? 'reload_with_pass yes' : 'reload_with_pass no') . "\n";
} catch (Throwable $e) {
	echo 'ERROR: ' . $e->getMessage() . "\n" . $e->getFile() . ':' . $e->getLine() . "\n";
}
