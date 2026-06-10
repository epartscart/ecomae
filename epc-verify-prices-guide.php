<?php
/**
 * Diagnose shop/prices/guide CMS registration and PHP file on disk.
 * GET: token=epartscart-deploy-2026, key=tech_key
 */
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
if (($_GET['token'] ?? '') !== $deployToken) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? '') !== $DP_Config->tech_key) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Invalid tech_key']));
}

$db = new PDO(
    'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
    $DP_Config->user,
    $DP_Config->password,
    [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
);
$db->query('SET NAMES utf8');

$guideUrl = 'shop/prices/guide';
$phpRel = '/' . $DP_Config->backend_dir . '/content/shop/prices_upload/prices_guide_page.php';
$phpAbs = $_SERVER['DOCUMENT_ROOT'] . $phpRel;

$row = $db->prepare('SELECT * FROM `content` WHERE `url` = ? AND `is_frontend` = 0');
$row->execute([$guideUrl]);
$guide = $row->fetch(PDO::FETCH_ASSOC);

$access = [];
if ($guide) {
    $aq = $db->prepare('SELECT `group_id` FROM `content_access` WHERE `content_id` = ?');
    $aq->execute([(int)$guide['id']]);
    while ($a = $aq->fetch(PDO::FETCH_ASSOC)) {
        $access[] = (int)$a['group_id'];
    }
}

$parent = $db->prepare('SELECT `id`, `url`, `modules_array` FROM `content` WHERE `url` = ? AND `is_frontend` = 0');
$parent->execute(['shop/prices']);
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
$parentAccess = [];
if ($parentRow) {
    $pa = $db->prepare('SELECT `group_id` FROM `content_access` WHERE `content_id` = ?');
    $pa->execute([(int)$parentRow['id']]);
    while ($a = $pa->fetch(PDO::FETCH_ASSOC)) {
        $parentAccess[] = (int)$a['group_id'];
    }
}

echo json_encode([
    'status' => true,
    'backend_dir' => $DP_Config->backend_dir,
    'php_abs' => $phpAbs,
    'php_exists' => is_file($phpAbs),
    'guide_content_row' => $guide ?: null,
    'guide_access_groups' => $access,
    'parent_shop_prices' => $parentRow,
    'parent_access_groups' => $parentAccess,
    'route_lookup_ok' => $guide && (int)($guide['published_flag'] ?? 0) === 1,
], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
