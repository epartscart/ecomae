<?php
/**
 * Smoke test: can the price upload guide PHP render? (deploy diagnostics)
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
$guidePhp = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/prices_upload/prices_guide_page.php';
$guidePhpAlt = $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/prices_upload/prices_upload_guide.php';

$result = array(
    'status' => true,
    'php_version' => PHP_VERSION,
    'guide_page_exists' => is_file($guidePhp),
    'guide_body_exists' => is_file($guidePhpAlt),
    'include_ok' => false,
    'html_length' => 0,
    'error' => '',
);

if (!is_file($guidePhpAlt)) {
    $result['status'] = false;
    $result['error'] = 'prices_upload_guide.php missing';
    exit(json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE));
}

define('_ASTEXE_', 1);
if (!isset($GLOBALS['DP_Config'])) {
    $GLOBALS['DP_Config'] = $DP_Config;
}
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
    require $guidePhpAlt;
    $html = ob_get_clean();
    $result['include_ok'] = true;
    $result['html_length'] = strlen($html);
    $result['has_panel'] = (strpos($html, 'Price upload') !== false);
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
