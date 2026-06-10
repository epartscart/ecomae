<?php
/**
 * Fix ERP CP access: all backend admin groups + optional user group bind.
 * GET: token=epartscart-deploy-2026
 * Optional: apply=1 (default), email=user@example.com
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

function epc_erp_backend_group_ids(PDO $db)
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

$apply = !isset($_GET['apply']) || $_GET['apply'] !== '0';
$groups = epc_erp_backend_group_ids($pdo);
$urls = array('shop/finance/erp', 'shop/finance/erp/guide');
$fixed = array();

if ($apply) {
	foreach ($urls as $url) {
		$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
		$st->execute(array($url));
		$cid = (int)$st->fetchColumn();
		if ($cid <= 0) {
			continue;
		}
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($cid));
		$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		$added = array();
		foreach ($groups as $gid) {
			$ins->execute(array($cid, (int)$gid));
			$added[] = (int)$gid;
		}
		$fixed[$url] = array('content_id' => $cid, 'groups' => $added);
	}
	$pdo->exec("UPDATE `control_items` SET `show_anyway` = 1 WHERE `url` LIKE '%/shop/finance/erp%'");
}

$userFixes = array();
$email = trim((string)($_GET['email'] ?? ''));
if ($apply && $email !== '') {
	$uq = $pdo->prepare('SELECT `user_id`, `email` FROM `users` WHERE `email` = ? OR `email` LIKE ? LIMIT 5');
	$uq->execute(array($email, '%' . $email . '%'));
	while ($u = $uq->fetch(PDO::FETCH_ASSOC)) {
		$uid = (int)$u['user_id'];
		$ins = $pdo->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)');
		$added = array();
		foreach ($groups as $gid) {
			$ins->execute(array($uid, (int)$gid));
			if ($ins->rowCount() > 0) {
				$added[] = (int)$gid;
			}
		}
		$userFixes[] = array('user_id' => $uid, 'email' => $u['email'], 'groups_added' => $added);
	}
}

echo json_encode(array(
	'status' => true,
	'message' => 'ERP access synced to all backend CP groups',
	'backend_groups' => $groups,
	'fixed' => $fixed,
	'users' => $userFixes,
	'cp_login' => 'https://www.epartscart.com/' . $cfg->backend_dir . '/',
	'cp_erp' => 'https://www.epartscart.com/' . $cfg->backend_dir . '/shop/finance/erp',
	'hint' => 'You must log in to CP first. Direct URL without session shows the login page (not an error).',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
