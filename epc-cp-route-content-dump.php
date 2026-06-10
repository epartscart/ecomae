<?php
header('Content-Type: application/json; charset=utf-8');
if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
    exit('{}');
}
require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
    exit('{}');
}
$db = new PDO(
    'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
    $DP_Config->user,
    $DP_Config->password
);
$route = isset($_GET['route']) ? (string)$_GET['route'] : 'shop/prices/guide';
$all = $db->prepare('SELECT `id`, `url`, `content_type`, `content`, `published_flag` FROM `content` WHERE `url` LIKE ? AND `is_frontend` = 0');
$all->execute(array('%guide%'));
$rows = $all->fetchAll(PDO::FETCH_ASSOC);
$st = $db->prepare('SELECT * FROM `content` WHERE `url` = ? AND `published_flag` = 1 AND `is_frontend` = 0');
$st->execute(array($route));
$main = $st->fetch(PDO::FETCH_ASSOC);
$filePreview = null;
$fileMd5 = null;
if ($main && $main['content_type'] === 'php') {
    $phpPath = str_replace('<backend_dir>', $DP_Config->backend_dir, $_SERVER['DOCUMENT_ROOT'] . $main['content']);
    $filePreview = is_file($phpPath) ? substr(file_get_contents($phpPath), 0, 400) : 'MISSING';
    $fileMd5 = is_file($phpPath) ? md5_file($phpPath) : null;
}
echo json_encode(array(
    'route' => $route,
    'main_row' => $main,
    'guide_like_rows' => $rows,
    'file_preview' => $filePreview,
    'file_md5' => $fileMd5,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
