<?php
/**
 * Repair CP routes: shop/prices/guide and shop/prices/prices_edit (content path + access).
 * GET/POST: token=epartscart-deploy-2026, key=tech_key
 */
header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
if (($_GET['token'] ?? $_POST['token'] ?? '') !== $deployToken) {
    http_response_code(403);
    exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_GET['key'] ?? $_POST['key'] ?? '') !== $DP_Config->tech_key) {
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

$routes = array(
    'shop/prices/guide' => '/' . $DP_Config->backend_dir . '/content/shop/prices_upload/prices_guide_page.php',
    'shop/prices/prices_edit' => '/' . $DP_Config->backend_dir . '/content/shop/prices_edit/prices.php',
);

$parentId = 0;
$ps = $db->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ps->execute(array('shop/prices'));
$parentId = (int)$ps->fetchColumn();

$out = array();
foreach ($routes as $url => $phpPath) {
    $row = $db->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
    $row->execute(array($url));
    $id = (int)$row->fetchColumn();
    if ($id < 1) {
        $out[$url] = array('ok' => false, 'message' => 'content row missing — run register script');
        continue;
    }
    $db->prepare(
        'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0,
         `system_flag` = 0, `content` = ?, `parent` = ? WHERE `id` = ?'
    )->execute(array($phpPath, $parentId > 0 ? $parentId : null, $id));

    $db->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($id));
    $groupIds = array(1);
    if ($parentId > 0) {
        $gq = $db->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
        $gq->execute(array($parentId));
        while ($g = $gq->fetch(PDO::FETCH_ASSOC)) {
            $groupIds[] = (int)$g['group_id'];
        }
    }
    $groupIds = array_values(array_unique(array_filter($groupIds)));
    $ins = $db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
    foreach ($groupIds as $gid) {
        try {
            $ins->execute(array($id, $gid));
        } catch (Exception $e) {
        }
    }
    $out[$url] = array(
        'ok' => true,
        'content_id' => $id,
        'php_path' => $phpPath,
        'php_exists' => is_file($_SERVER['DOCUMENT_ROOT'] . $phpPath),
        'access_groups' => $groupIds,
    );
}

echo json_encode(array('status' => true, 'routes' => $out), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
