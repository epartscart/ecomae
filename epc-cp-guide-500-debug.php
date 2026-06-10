<?php
/**
 * Reproduce CP guide route with admin cookies; capture PHP errors.
 */
header('Content-Type: text/plain; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
    exit('Forbidden');
}
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
    exit('Invalid key');
}

$db = new PDO(
    'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
    $DP_Config->user,
    $DP_Config->password,
    array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$st = $db->prepare('SELECT `session`, `user_id`, `2fa_session` FROM `sessions` WHERE `user_id` = 5 AND `type` = 1 ORDER BY `last_activiti_time` DESC LIMIT 1');
$st->execute();
$row = $st->fetch(PDO::FETCH_ASSOC);
if (!$row) {
    exit("No session\n");
}

$_COOKIE['admin_session'] = $row['session'];
$_COOKIE['admin_u_id'] = (string)$row['user_id'];
if (!empty($row['2fa_session'])) {
    $_COOKIE['2fa'] = $row['2fa_session'];
}

$route = isset($_GET['route']) ? (string)$_GET['route'] : 'shop/prices/guide';
$_SERVER['REQUEST_URI'] = '/' . $DP_Config->backend_dir . '/' . $route;
$_GET = array();

register_shutdown_function(function () {
    $e = error_get_last();
    if ($e && in_array($e['type'], array(E_ERROR, E_PARSE, E_CORE_ERROR, E_COMPILE_ERROR), true)) {
        echo "\nSHUTDOWN FATAL: {$e['message']} in {$e['file']}:{$e['line']}\n";
    }
});

chdir($_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir);

// Probe: which CMS row + file would load for this route?
$dbProbe = new PDO(
    'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
    $DP_Config->user,
    $DP_Config->password
);
$stProbe = $dbProbe->prepare('SELECT `id`, `url`, `content` FROM `content` WHERE `url` = ? AND `published_flag` = 1 AND `is_frontend` = 0');
$stProbe->execute(array($route));
$probeRow = $stProbe->fetch(PDO::FETCH_ASSOC);
echo "probe_route={$route} content_id=" . ($probeRow['id'] ?? '') . " path=" . ($probeRow['content'] ?? '') . "\n";

ob_start();
try {
    include 'index.php';
} catch (Throwable $t) {
    ob_end_clean();
    exit("Throwable: " . $t->getMessage() . "\n" . $t->getTraceAsString());
}
$html = ob_get_clean();
echo 'bytes=' . strlen($html) . "\n";
echo 'http_response_code=' . http_response_code() . "\n";
if (strlen($html) < 500) {
    echo $html;
} else {
    echo substr(strip_tags($html), 0, 500) . "\n...\n";
    echo (stripos($html, 'Price upload') !== false ? "HAS guide content\n" : "NO guide marker\n");
    echo (stripos($html, 'splash') !== false ? "HAS splash\n" : "");
}
