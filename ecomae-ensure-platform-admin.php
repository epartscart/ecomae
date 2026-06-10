<?php
/**
 * Ensure ecomae platform DB has CP auth plugin + operator admin (Super CP login).
 * https://www.ecomae.com/ecomae-ensure-platform-admin.php?token=epartscart-deploy-2026&apply=1
 */

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$apply = !empty($_GET['apply']);
$email = trim((string) ($_GET['email'] ?? 'taxofin2025@gmail.com'));
$password = (string) ($_GET['password'] ?? '12345678');

require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
epc_portal_apply_config($cfg);

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	exit(json_encode(array('status' => false, 'message' => 'DB connect failed: ' . $e->getMessage())));
}

function ecomae_backend_group_ids(PDO $db): array
{
	$ids = array();
	$st = $db->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = $st->fetch(PDO::FETCH_ASSOC);
	if (!$root) {
		return array(1);
	}
	$collect = function (int $parentId) use ($db, &$collect, &$ids): void {
		$ids[$parentId] = true;
		$ch = $db->prepare('SELECT `id`, `count` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($parentId));
		while ($row = $ch->fetch(PDO::FETCH_ASSOC)) {
			if ((int) $row['count'] > 0) {
				$collect((int) $row['id']);
			} else {
				$ids[(int) $row['id']] = true;
			}
		}
	};
	$collect((int) $root['id']);
	return array_keys($ids);
}

$result = array(
	'status' => true,
	'db' => $cfg->db,
	'host' => epc_portal_host(),
	'apply' => $apply,
	'email' => $email,
);

$authPlugin = $pdo->query("SELECT `id`, `activated`, `is_frontend` FROM `plugins` WHERE `name` LIKE '%authentication%' OR `dir` LIKE '%authentication%' LIMIT 5")->fetchAll(PDO::FETCH_ASSOC);
$result['auth_plugins'] = $authPlugin;
if ($apply) {
	$pdo->exec("UPDATE `plugins` SET `activated` = 1 WHERE (`name` LIKE '%authentication%' OR `dir` LIKE '%authentication%') AND `is_frontend` = 0");
}

$groups = ecomae_backend_group_ids($pdo);
$result['backend_groups'] = $groups;

$st = $pdo->prepare('SELECT `user_id`, `email`, `unlocked`, `email_confirmed` FROM `users` WHERE `email` = ? LIMIT 1');
$st->execute(array($email));
$user = $st->fetch(PDO::FETCH_ASSOC);
$passwordHash = md5($password . $cfg->secret_succession);

if (!$user) {
	$result['user'] = 'missing';
	if ($apply) {
		$pdo->prepare(
			'INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_created`, `admin_created`)
			 VALUES (?, 1, ?, 1, 1, ?, 1)'
		)->execute(array($email, $passwordHash, time()));
		$userId = (int) $pdo->lastInsertId();
		$pdo->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)')->execute(array($userId, 'name', 'Platform operator'));
		$result['user_created'] = $userId;
	} else {
		$result['hint'] = 'Pass apply=1 to create operator user';
	}
} else {
	$userId = (int) $user['user_id'];
	$result['user'] = array(
		'user_id' => $userId,
		'unlocked' => (int) $user['unlocked'],
		'email_confirmed' => (int) $user['email_confirmed'],
	);
	if ($apply) {
		$pdo->prepare('UPDATE `users` SET `password` = ?, `unlocked` = 1, `email_confirmed` = 1 WHERE `user_id` = ?')
			->execute(array($passwordHash, $userId));
		$result['password_reset'] = true;
	}
}

if ($apply && !empty($userId)) {
	$ins = $pdo->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)');
	$added = array();
	foreach ($groups as $gid) {
		$ins->execute(array($userId, (int) $gid));
		if ($ins->rowCount() > 0) {
			$added[] = (int) $gid;
		}
	}
	$result['groups_bound'] = $added;
}

$result['super_cp_login'] = array(
	'url' => 'https://www.ecomae.com/cp/',
	'email' => $email,
	'note' => 'Client CP users are created per tenant on the client domain — not this platform account.',
);

echo json_encode($result, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
