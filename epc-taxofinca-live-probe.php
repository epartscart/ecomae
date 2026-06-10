<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
echo '__DIR__=' . __DIR__ . "\n";
echo '__FILE__=' . __FILE__ . "\n";
echo 'HTTP_HOST=' . ($_SERVER['HTTP_HOST'] ?? '') . "\n";
echo 'SERVER_NAME=' . ($_SERVER['SERVER_NAME'] ?? '') . "\n";
$core = @file_get_contents(__DIR__ . '/core/dp_core.php');
echo 'dp_core_apply_before_license=' . (is_string($core) && strpos($core, 'epcDomainMatchesRequest') !== false ? 'yes' : 'no') . "\n";
echo 'index_has_apply=' . (is_string(@file_get_contents(__DIR__ . '/index.php')) && strpos((string) @file_get_contents(__DIR__ . '/index.php'), 'epc_portal_apply_config') !== false ? 'yes' : 'no') . "\n";
define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);
echo 'domain_path=' . $DP_Config->domain_path . "\n";
echo 'is_client=' . (epc_portal_is_client_hostname() ? 'yes' : 'no') . "\n";
