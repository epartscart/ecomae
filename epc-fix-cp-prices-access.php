<?php
/**
 * Fix CP access for shop/prices/prices_edit and shop/prices/guide:
 * sync content_access from working CP routes; ensure admin users (e.g. yawer) have backend groups.
 * GET: token=epartscart-deploy-2026, key=tech_key
 * Optional: email=admin@example.com, apply=1
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
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

$routes = array('shop/prices', 'shop/prices/prices_edit', 'shop/prices/guide');
$contentIds = array();
foreach ($routes as $url) {
    $st = $db->prepare('SELECT `id`, `url`, `published_flag`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
    $st->execute(array($url));
    $row = $st->fetch(PDO::FETCH_ASSOC);
    if ($row) {
        $contentIds[$url] = (int)$row['id'];
    }
}

$refUrls = array('shop/prices', 'shop/orders/orders', 'shop');
$refGroups = array();
foreach ($refUrls as $url) {
    $st = $db->prepare(
        'SELECT DISTINCT ca.`group_id` FROM `content_access` ca
         INNER JOIN `content` c ON c.`id` = ca.`content_id`
         WHERE c.`url` = ? AND c.`is_frontend` = 0'
    );
    $st->execute(array($url));
    while ($g = $st->fetchColumn()) {
        $refGroups[(int)$g] = true;
    }
}
if (!$refGroups) {
    $refGroups[1] = true;
    $refGroups[3] = true;
}
$backendGroupsForAccess = epc_backend_group_ids($db);
foreach ($backendGroupsForAccess as $gid) {
    $refGroups[(int)$gid] = true;
}
$refGroupList = array_keys($refGroups);

$apply = !empty($_GET['apply']);
$accessChanges = array();
if ($apply) {
    foreach (array('shop/prices/prices_edit', 'shop/prices/guide') as $url) {
        if (empty($contentIds[$url])) {
            continue;
        }
        $cid = $contentIds[$url];
        $db->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($cid));
        $ins = $db->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
        foreach ($refGroupList as $gid) {
            $ins->execute(array($cid, (int)$gid));
        }
        $accessChanges[$url] = $refGroupList;
    }
    if (!empty($contentIds['shop/prices'])) {
        $cid = $contentIds['shop/prices'];
        $db->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($cid));
        $insParent = $db->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
        foreach ($refGroupList as $gid) {
            $insParent->execute(array($cid, (int)$gid));
        }
    }
}

function epc_backend_group_ids(PDO $db)
{
    $ids = array();
    $st = $db->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
    $root = $st->fetch(PDO::FETCH_ASSOC);
    if (!$root) {
        return array(1);
    }
    $collect = function ($parentId) use ($db, &$collect, &$ids) {
        $ids[(int)$parentId] = true;
        $ch = $db->prepare('SELECT `id`, `count` FROM `groups` WHERE `parent` = ?');
        $ch->execute(array($parentId));
        while ($row = $ch->fetch(PDO::FETCH_ASSOC)) {
            $ids[(int)$row['id']] = true;
            if ((int)$row['count'] > 0) {
                $collect((int)$row['id']);
            }
        }
    };
    $collect((int)$root['id']);
    return array_keys($ids);
}

$userFixes = array();
$backendGroups = $backendGroupsForAccess;
$emailFilter = trim((string)($_GET['email'] ?? ''));
$userSql = 'SELECT `user_id`, `email`, `phone` FROM `users` WHERE `unlocked` = 1';
$userArgs = array();
if ($emailFilter !== '') {
    $userSql .= ' AND (`email` = ? OR `email` LIKE ?)';
    $userArgs[] = $emailFilter;
    $userArgs[] = '%' . $emailFilter . '%';
} else {
    $userSql .= ' AND (`email` LIKE ? OR `email` LIKE ? OR `phone` LIKE ?)';
    $userArgs = array('%yawer%', '%Yawer%', '%yawer%');
}
$userSql .= ' LIMIT 20';
$uq = $db->prepare($userSql);
$uq->execute($userArgs);
$users = $uq->fetchAll(PDO::FETCH_ASSOC);

foreach ($users as $u) {
    $uid = (int)$u['user_id'];
    $gq = $db->prepare('SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?');
    $gq->execute(array($uid));
    $have = array();
    while ($g = $gq->fetchColumn()) {
        $have[(int)$g] = true;
    }
    $added = array();
    if ($apply) {
        $ins = $db->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)');
        foreach ($refGroupList as $gid) {
            if (empty($have[$gid])) {
                $ins->execute(array($uid, (int)$gid));
                $added[] = (int)$gid;
            }
        }
        foreach ($backendGroups as $gid) {
            if (empty($have[$gid]) && !in_array($gid, $added, true)) {
                $ins->execute(array($uid, (int)$gid));
                $added[] = (int)$gid;
            }
        }
    }
    $userFixes[] = array(
        'user_id' => $uid,
        'email' => $u['email'],
        'groups_before' => array_keys($have),
        'groups_added' => $added,
    );
}

$accessAfter = array();
foreach ($routes as $url) {
    if (empty($contentIds[$url])) {
        $accessAfter[$url] = null;
        continue;
    }
    $aq = $db->prepare('SELECT `group_id` FROM `content_access` WHERE `content_id` = ?');
    $aq->execute(array($contentIds[$url]));
    $accessAfter[$url] = $aq->fetchAll(PDO::FETCH_COLUMN);
}

echo json_encode(array(
    'status' => true,
    'apply' => $apply,
    'content_ids' => $contentIds,
    'reference_groups' => $refGroupList,
    'access_after' => $accessAfter,
    'users' => $userFixes,
    'hint' => 'Re-run with apply=1 to write DB. Then log in to CP and open /cp/shop/prices/prices_edit and /cp/shop/prices/guide.',
), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
