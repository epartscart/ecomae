<?php
/**
 * Register CP URL shop/prices/prices_edit in Docpart `content` (one-time deploy helper).
 * POST: token=epartscart-deploy-2026, key=tech_key
 */
header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
if (($_POST['token'] ?? $_GET['token'] ?? '') !== $deployToken) {
    http_response_code(403);
    exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_POST['key'] ?? $_GET['key'] ?? '') !== $DP_Config->tech_key) {
    http_response_code(403);
    exit(json_encode(array('status' => false, 'message' => 'Invalid key')));
}

try {
    $db = new PDO(
        'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
        $DP_Config->user,
        $DP_Config->password,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
    );
} catch (PDOException $e) {
    exit(json_encode(array('status' => false, 'message' => 'DB connect failed')));
}
$db->query('SET NAMES utf8');

$parentUrl = 'shop/prices';
$editUrl = 'shop/prices/prices_edit';
$phpPath = '/<backend_dir>/content/shop/prices_edit/prices.php';
$now = time();

$existing = $db->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$existing->execute(array($editUrl));
$existingId = (int)$existing->fetchColumn();

$parent = $db->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$parent->execute(array($parentUrl));
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
    exit(json_encode(array('status' => false, 'message' => 'Parent content shop/prices not found')));
}
$parentId = (int)$parentRow['id'];
$level = (int)$parentRow['level'] + 1;

if ($existingId > 0) {
    $db->prepare(
        'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0,
         `system_flag` = 0, `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = \'prices_edit\', `url` = ?
         WHERE `id` = ?'
    )->execute(array(
        $phpPath,
        'Edit price list records — eParts Cart',
        $parentId,
        $level,
        $editUrl,
        $existingId
    ));
} else {
    $ins = $db->prepare(
        'INSERT INTO `content`
        (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
         `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
         `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
         VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 80)'
    );
    $ins->execute(array(
        $editUrl,
        $level,
        'prices_edit',
        'Edit price list records',
        $parentId,
        'Search and edit rows in shop_docpart_prices_data',
        $phpPath,
        'Edit price list records — eParts Cart',
        $now,
        $now
    ));
    $existingId = (int)$db->lastInsertId();
}

if ($existingId > 0 && $parentId > 0) {
    $db->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($existingId));
    $groups = $db->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
    $groups->execute(array($parentId));
    $insAcc = $db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
    while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
        try {
            $insAcc->execute(array($existingId, (int)$g['group_id']));
        } catch (Exception $e) {
        }
    }
    if ((int)$db->query('SELECT COUNT(*) FROM `content_access` WHERE `content_id` = ' . (int)$existingId)->fetchColumn() === 0) {
        $db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, 1)')->execute(array($existingId));
    }
}

echo json_encode(array(
    'status' => $existingId > 0,
    'message' => $existingId > 0 ? 'Price edit page registered' : 'Insert failed',
    'content_id' => $existingId,
    'cp_url' => '/' . $DP_Config->backend_dir . '/' . $editUrl,
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
