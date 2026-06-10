<?php
/**
 * Diagnose shop/prices/prices_edit CMS registration and PHP files on disk.
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

$db = new PDO(
    'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
    $DP_Config->user,
    $DP_Config->password,
    array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);
$db->query('SET NAMES utf8');

$editUrl = 'shop/prices/prices_edit';
$phpRel = '/' . $DP_Config->backend_dir . '/content/shop/prices_edit/prices.php';
$phpAbs = $_SERVER['DOCUMENT_ROOT'] . $phpRel;
$helpersAbs = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/prices_edit/epc_prices_edit_helpers.php';

$row = $db->prepare('SELECT * FROM `content` WHERE `url` = ? AND `is_frontend` = 0');
$row->execute(array($editUrl));
$edit = $row->fetch(PDO::FETCH_ASSOC);

$access = array();
if ($edit) {
    $aq = $db->prepare('SELECT `group_id` FROM `content_access` WHERE `content_id` = ?');
    $aq->execute(array((int)$edit['id']));
    while ($a = $aq->fetch(PDO::FETCH_ASSOC)) {
        $access[] = (int)$a['group_id'];
    }
}

if (!empty($_GET['repair']) && $edit) {
    $db->prepare(
        'UPDATE `content` SET `system_flag` = 0, `content_type` = \'php\', `published_flag` = 1,
         `content` = ?, `title_tag` = ? WHERE `id` = ?'
    )->execute(array(
        '/' . $DP_Config->backend_dir . '/content/shop/prices_edit/prices.php',
        'Edit price list records — eParts Cart',
        (int)$edit['id']
    ));
    $row2 = $db->prepare('SELECT * FROM `content` WHERE `id` = ?');
    $row2->execute(array((int)$edit['id']));
    $edit = $row2->fetch(PDO::FETCH_ASSOC);
}

echo json_encode(array(
    'status' => true,
    'backend_dir' => $DP_Config->backend_dir,
    'php_abs' => $phpAbs,
    'php_exists' => is_file($phpAbs),
    'helpers_exists' => is_file($helpersAbs),
    'content_row' => $edit ?: null,
    'access_groups' => $access,
    'route_lookup_ok' => $edit && (int)$edit['published_flag'] === 1,
    'content_path_uses_backend_placeholder' => $edit && strpos((string)$edit['content'], '<backend_dir>') !== false,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
