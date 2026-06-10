<?php
/**
 * Register CP URL shop/prices/guide in Docpart `content` (one-time deploy helper).
 * POST: token=epartscart-deploy-2026, key=tech_key
 */
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

$deployToken = 'epartscart-deploy-2026';
if (($_POST['token'] ?? $_GET['token'] ?? '') !== $deployToken) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Forbidden']));
}

require_once __DIR__ . '/config.php';
$DP_Config = new DP_Config;
if ((string)($_POST['key'] ?? $_GET['key'] ?? '') !== $DP_Config->tech_key) {
    http_response_code(403);
    exit(json_encode(['status' => false, 'message' => 'Invalid tech_key']));
}

try {
    $db = new PDO(
        'mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db,
        $DP_Config->user,
        $DP_Config->password,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
} catch (PDOException $e) {
    exit(json_encode(['status' => false, 'message' => 'DB connect failed']));
}
$db->query('SET NAMES utf8');

$parentUrl = 'shop/prices';
$guideUrl = 'shop/prices/guide';
$phpPath = '/<backend_dir>/content/shop/prices_upload/prices_guide_page.php';
$now = time();

$existing = $db->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$existing->execute([$guideUrl]);
$existingId = (int)$existing->fetchColumn();

if ($existingId > 0) {
    $ps = $db->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
    $ps->execute([$parentUrl]);
    $parentIdFix = (int)$ps->fetchColumn();
    $db->prepare(
        'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0,
         `content` = ?, `title_tag` = ?, `parent` = ?, `level` = 3, `alias` = \'guide\', `url` = ?
         WHERE `id` = ?'
    )->execute(array($phpPath, 'Price upload guide — eParts Cart', $parentIdFix, $guideUrl, $existingId));
} else {
    $existingId = 0;
}

if ($existingId > 0) {
    // Sync access groups from shop/prices parent so all admins who see price lists see the guide.
    $ps2 = $db->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
    $ps2->execute([$parentUrl]);
    $parentIdFix = (int)$ps2->fetchColumn();
    if ($parentIdFix > 0) {
        $db->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute([$existingId]);
        $groups = $db->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
        $groups->execute([$parentIdFix]);
        $insAcc = $db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
        while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
            try {
                $insAcc->execute([$existingId, (int)$g['group_id']]);
            } catch (Throwable $e) {
            }
        }
        if ($db->query('SELECT COUNT(*) FROM `content_access` WHERE `content_id` = ' . (int)$existingId)->fetchColumn() == 0) {
            $db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, 1)')->execute([$existingId]);
        }
    }
    echo json_encode([
        'status' => true,
        'message' => 'Guide page repaired (content path + access)',
        'content_id' => $existingId,
        'cp_url' => '/' . $DP_Config->backend_dir . '/' . $guideUrl,
        'alt_url' => '/' . $DP_Config->backend_dir . '/shop/prices?view=guide',
        'php_path' => $phpPath,
    ], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
    exit;
}

$parent = $db->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$parent->execute([$parentUrl]);
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
    exit(json_encode(['status' => false, 'message' => 'Parent content shop/prices not found']));
}
$parentId = (int)$parentRow['id'];
$level = (int)$parentRow['level'] + 1;

$ins = $db->prepare(
    'INSERT INTO `content`
    (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
     `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
     `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
     VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 79)'
);
$ins->execute(array(
    $guideUrl,
    $level,
    'guide',
    'Price upload guide',
    $parentId,
    'All channels, e-mail rules per list, upload history',
    $phpPath,
    'Price upload guide — eParts Cart',
    $now,
    $now,
));

$contentId = (int)$db->lastInsertId();
if ($contentId > 0) {
    try {
        $db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, 1)')
            ->execute([$contentId]);
    } catch (Throwable $e) {
        // Access row may already exist for some installs.
    }
}

echo json_encode([
    'status' => $contentId > 0,
    'message' => $contentId > 0 ? 'Guide page registered' : 'Insert failed',
    'content_id' => $contentId,
    'cp_url' => '/' . $DP_Config->backend_dir . '/' . $guideUrl,
    'alt_url' => '/' . $DP_Config->backend_dir . '/shop/prices?view=guide',
], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
