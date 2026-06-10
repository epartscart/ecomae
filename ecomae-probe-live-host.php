<?php
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');
echo 'HTTP_HOST=' . ($_SERVER['HTTP_HOST'] ?? '') . "\n";
echo 'index_bytes=' . (is_file(__DIR__ . '/index.php') ? filesize(__DIR__ . '/index.php') : 0) . "\n";
echo 'has_apply=' . (strpos((string) @file_get_contents(__DIR__ . '/index.php'), 'epc_portal_apply_config') !== false ? 'yes' : 'no') . "\n";
define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config();
echo 'config_domain_path=' . $DP_Config->domain_path . "\n";
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($DP_Config);
echo 'after_apply_domain_path=' . $DP_Config->domain_path . "\n";
echo 'after_apply_db=' . $DP_Config->db . "\n";
$dh = parse_url($DP_Config->domain_path, PHP_URL_HOST);
echo 'license_match=' . (($dh === ($_SERVER['HTTP_HOST'] ?? '')) ? 'yes' : "no ({$dh})") . "\n";
