<?php
/**
 * Fix Logistics CP content access for all backend admin groups.
 * GET: token=epartscart-deploy-2026
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

function epc_log_backend_group_ids(PDO $db)
{
	$ids = array();
	$st = $db->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = $st->fetch(PDO::FETCH_ASSOC);
	if (!$root) {
		return array(1, 3);
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

function epc_log_apply_access(PDO $pdo, $contentId, array $groupIds)
{
	if ($contentId <= 0 || empty($groupIds)) {
		return array();
	}
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	$added = array();
	foreach ($groupIds as $gid) {
		$ins->execute(array($contentId, (int)$gid));
		$added[] = (int)$gid;
	}
	return $added;
}

$backendGroups = epc_log_backend_group_ids($pdo);
$groupIds = array();
foreach ($backendGroups as $gid) {
	$groupIds[(int)$gid] = true;
}
foreach (array('shop/logistics', 'shop/logistics/storages', 'shop/orders/orders') as $refUrl) {
	$ref = $pdo->prepare(
		'SELECT DISTINCT ca.`group_id` FROM `content_access` ca
		 INNER JOIN `content` c ON c.`id` = ca.`content_id`
		 WHERE c.`url` = ? AND c.`is_frontend` = 0'
	);
	$ref->execute(array($refUrl));
	while ($gid = $ref->fetchColumn()) {
		$groupIds[(int)$gid] = true;
	}
}
$groupIds = array_keys($groupIds);

$urls = array('shop/logistics', 'shop/logistics/carriers', 'shop/logistics/guide');
$fixed = array();
foreach ($urls as $url) {
	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array($url));
	$cid = (int)$st->fetchColumn();
	if ($cid > 0) {
		$fixed[$url] = array('content_id' => $cid, 'groups' => epc_log_apply_access($pdo, $cid, $groupIds));
	}
}

$menu = epc_cp_mainstream_menu_apply($pdo);
$itemIds = array();
foreach ($menu['items'] as $key => $id) {
	if (strpos($key, 'logistics_') === 0) {
		$itemIds[] = (int)$id;
	}
}
if (!empty($itemIds)) {
	$pdo->exec('UPDATE `control_items` SET `show_anyway` = 1 WHERE `id` IN (' . implode(',', $itemIds) . ')');
}

$base = rtrim($cfg->domain_path, '/');
echo json_encode(array(
	'status' => true,
	'message' => 'Logistics CP access synced',
	'fixed' => $fixed,
	'menu_show_anyway_ids' => $itemIds,
	'urls' => array(
		'carriers' => $base . '/' . $cfg->backend_dir . '/shop/logistics/carriers',
		'guide' => $base . '/' . $cfg->backend_dir . '/shop/logistics/guide',
		'hub' => $base . '/' . $cfg->backend_dir . '/shop/logistics',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
