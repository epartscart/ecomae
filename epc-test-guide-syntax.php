<?php
/**
 * Probe PHP version and guide file presence (deploy only).
 */
header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
$token = isset($_GET['token']) ? $_GET['token'] : '';
if ($token !== $deployToken) {
    http_response_code(403);
    exit('{"status":false,"message":"Forbidden"}');
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
$key = isset($_GET['key']) ? $_GET['key'] : '';
if ($key !== $DP_Config->tech_key) {
    http_response_code(403);
    exit('{"status":false,"message":"Invalid key"}');
}

$guidePhp = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/prices_upload/prices_upload_guide.php';
$diagPhp = $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_price_upload_guide_data.php';
$pagePhp = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/prices_upload/prices_guide_page.php';

$diagOk = false;
$diagErr = '';
if (is_file($diagPhp)) {
    try {
        require_once $diagPhp;
        $diagOk = function_exists('epc_guide_snapshot');
    } catch (Exception $e) {
        $diagErr = $e->getMessage();
    }
}

echo json_encode(array(
    'status' => true,
    'php_version' => PHP_VERSION,
    'files' => array(
        'guide' => is_file($guidePhp),
        'diagnostics' => is_file($diagPhp),
        'guide_page' => is_file($pagePhp),
    ),
    'diagnostics_loaded' => $diagOk,
    'diagnostics_error' => $diagErr,
), JSON_PRETTY_PRINT);
