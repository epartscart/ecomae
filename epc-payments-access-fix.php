<?php
/**
 * Fix Payment gateways CP access for all backend admin groups.
 * GET: token=epartscart-deploy-2026
 */
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

function epc_pay_backend_group_ids(PDO $db)
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

function epc_pay_ref_groups(PDO $pdo, array $backendGroups)
{
	$refGroups = array();
	foreach (array('shop/orders/orders', 'shop/finance/account_operations', 'shop/finance/erp', 'shop/channels/channels', 'shop') as $refUrl) {
		$ref = $pdo->prepare(
			'SELECT DISTINCT ca.`group_id` FROM `content_access` ca
			 INNER JOIN `content` c ON c.`id` = ca.`content_id`
			 WHERE c.`url` = ? AND c.`is_frontend` = 0'
		);
		$ref->execute(array($refUrl));
		while ($gid = $ref->fetchColumn()) {
			$refGroups[(int)$gid] = true;
		}
	}
	foreach ($backendGroups as $gid) {
		$refGroups[(int)$gid] = true;
	}
	return array_keys($refGroups);
}

function epc_pay_apply_access(PDO $pdo, $contentId, array $groupIds)
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

$backendGroups = epc_pay_backend_group_ids($pdo);
$groupIds = epc_pay_ref_groups($pdo, $backendGroups);

$urls = array('shop/payments/payments', 'shop/payments/payments/guide', 'shop/payments/guide');
$fixed = array();

foreach ($urls as $url) {
	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array($url));
	$cid = (int)$st->fetchColumn();
	if ($cid <= 0) {
		continue;
	}
	$fixed[$url] = array(
		'content_id' => $cid,
		'groups' => epc_pay_apply_access($pdo, $cid, $groupIds),
	);
}

$pdo->exec("UPDATE `control_items` SET `show_anyway` = 1 WHERE `url` LIKE '%/shop/payments/%'");

$be = $cfg->backend_dir;
$base = rtrim($cfg->domain_path, '/');

echo json_encode(array(
	'status' => true,
	'message' => 'Payment gateways CP access synced',
	'backend_groups' => $backendGroups,
	'fixed' => $fixed,
	'cp_login' => $base . '/' . $be . '/',
	'cp_payments' => $base . '/' . $be . '/shop/payments/payments',
	'hint' => 'Log in to CP first — unauthenticated visits show the login form.',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
