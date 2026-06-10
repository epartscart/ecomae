<?php
/**
 * Build the same eval string as dp_core for a CP route and validate it compiles.
 */
header('Content-Type: application/json; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
    exit('{}');
}
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
    exit('{}');
}

$route = isset($_GET['route']) ? (string)$_GET['route'] : 'shop/prices/guide';
$db = new PDO(
    'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
    $DP_Config->user,
    $DP_Config->password
);
$st = $db->prepare('SELECT * FROM `content` WHERE `url` = ? AND `published_flag` = 1 AND `is_frontend` = 0');
$st->execute(array($route));
$row = $st->fetch(PDO::FETCH_ASSOC);
if (!$row) {
    exit(json_encode(array('status' => false, 'message' => 'No content row')));
}

$mainPhp = '';
if ($row['content_type'] === 'php') {
    $phpPath = str_replace('<backend_dir>', $DP_Config->backend_dir, $_SERVER['DOCUMENT_ROOT'] . $row['content']);
    $mainPhp = is_file($phpPath) ? file_get_contents($phpPath) : '';
}

$tplPath = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/templates/bootstrap_admin/desktop.php';
$tpl = is_file($tplPath) ? file_get_contents($tplPath) : '';
$merged = str_replace('<docpart type="main" name="main" />', $mainPhp, $tpl);
$merged = str_replace('<backend_dir>', $DP_Config->backend_dir, $merged);
$evalCode = ' ?>' . $merged . '<?php ';

$compileOk = true;
$compileErr = '';
try {
    token_get_all($evalCode, TOKEN_PARSE);
} catch (ParseError $e) {
    $compileOk = false;
    $compileErr = $e->getMessage();
}

// Try token_get_all doesn't throw on all versions - use eval in output buffer
if ($compileOk) {
    ob_start();
    if (!defined('_ASTEXE_')) {
        define('_ASTEXE_', 1);
    }
    $prev = set_error_handler(function ($errno, $errstr) {
        throw new Exception($errstr, $errno);
    });
    try {
        eval($evalCode);
    } catch (Throwable $e) {
        $compileOk = false;
        $compileErr = $e->getMessage();
    }
    restore_error_handler();
    ob_end_clean();
}

echo json_encode(array(
    'status' => true,
    'route' => $route,
    'content_id' => (int)$row['id'],
    'main_php_bytes' => strlen($mainPhp),
    'template_bytes' => strlen($tpl),
    'merged_bytes' => strlen($merged),
    'compile_ok' => $compileOk,
    'compile_error' => $compileErr,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
