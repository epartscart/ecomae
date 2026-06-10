<?php
/**
 * Smoke test: price list editor PHP renders.
 * GET: token=epartscart-deploy-2026, key=tech_key
 */
header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
if (($_GET['token'] ?? '') !== $deployToken) {
    http_response_code(403);
    exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
    http_response_code(403);
    exit(json_encode(array('status' => false, 'message' => 'Invalid key')));
}

$backend = $DP_Config->backend_dir;
$pricesPhp = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/prices_edit/prices.php';
$helpersPhp = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/prices_edit/epc_prices_edit_helpers.php';

$result = array(
    'status' => true,
    'php_version' => PHP_VERSION,
    'prices_php_exists' => is_file($pricesPhp),
    'helpers_exists' => is_file($helpersPhp),
    'include_ok' => false,
    'html_length' => 0,
    'error' => '',
);

if (!is_file($pricesPhp)) {
    $result['status'] = false;
    $result['error'] = 'prices.php missing';
    exit(json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
}

define('_ASTEXE_', 1);
$GLOBALS['DP_Config'] = $DP_Config;
try {
    $db_link = new PDO(
        'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
        $DP_Config->user,
        $DP_Config->password,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
    );
    $db_link->query('SET NAMES utf8;');
} catch (Exception $e) {
    $result['status'] = false;
    $result['error'] = 'DB: ' . $e->getMessage();
    exit(json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
}

ob_start();
try {
    include $pricesPhp;
    $html = ob_get_clean();
    $result['include_ok'] = true;
    $result['html_length'] = strlen($html);
    $result['has_fuzzy_search'] = (strpos($html, 'Fuzzy search') !== false);
} catch (Throwable $e) {
    ob_end_clean();
    $result['status'] = false;
    $result['error'] = $e->getMessage();
} catch (Exception $e) {
    ob_end_clean();
    $result['status'] = false;
    $result['error'] = $e->getMessage();
}

echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
