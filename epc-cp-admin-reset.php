<?php
/**
 * Reset / create CP backend admin on this tenant (Docpart users.email login).
 * https://www.epartscart.com/epc-cp-admin-reset.php?token=...&apply=1
 * Optional: &login=ecomae.admin&password=... (password only applied when apply=1)
 */
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: application/json; charset=utf-8');

$apply = !empty($_GET['apply']);
$login = trim((string) (isset($_GET['login']) ? $_GET['login'] : 'ecomae.admin'));
$password = (string) (isset($_GET['password']) ? $_GET['password'] : 'EpcCp2026!Admin');
$alsoResetLegacy = !isset($_GET['also_admin']) || $_GET['also_admin'] !== '0';

define('_ASTEXE_', 1);
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

function epc_cp_backend_group_ids($db)
{
	$ids = array();
	$st = $db->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = $st->fetch(PDO::FETCH_ASSOC);
	if (!$root) {
		return array(3);
	}
	$walk = function ($parentId) use ($db, &$walk, &$ids) {
		$ids[$parentId] = true;
		$ch = $db->prepare('SELECT `id`, `count` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($parentId));
		while ($row = $ch->fetch(PDO::FETCH_ASSOC)) {
			if ((int) $row['count'] > 0) {
				$walk((int) $row['id']);
			} else {
				$ids[(int) $row['id']] = true;
			}
		}
	};
	$walk((int) $root['id']);
	return array_keys($ids);
}

function epc_cp_bind_backend_groups($pdo, $userId, $groups)
{
	$ins = $pdo->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)');
	$added = array();
	foreach ($groups as $gid) {
		$ins->execute(array($userId, (int) $gid));
		if ($ins->rowCount() > 0) {
			$added[] = (int) $gid;
		}
	}
	return $added;
}

function epc_cp_upsert_admin($pdo, $cfg, $login, $password, $groups, $apply)
{
	$hash = md5($password . $cfg->secret_succession);
	$st = $pdo->prepare('SELECT `user_id`, `email`, `unlocked`, `email_confirmed` FROM `users` WHERE `email` = ? LIMIT 1');
	$st->execute(array($login));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$out = array('login' => $login);
	if (!$row) {
		$out['status'] = 'missing';
		if ($apply) {
			$pdo->prepare(
				'INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_registered`, `admin_created`)
				 VALUES (?, 1, ?, 1, 1, ?, 1)'
			)->execute(array($login, $hash, (string) time()));
			$userId = (int) $pdo->lastInsertId();
			@$pdo->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)')
				->execute(array($userId, 'name', 'CP operator'));
			$out['user_created'] = $userId;
			$out['groups_bound'] = epc_cp_bind_backend_groups($pdo, $userId, $groups);
		}
		return $out;
	}
	$userId = (int) $row['user_id'];
	$out['status'] = 'exists';
	$out['user_id'] = $userId;
	$out['unlocked'] = (int) $row['unlocked'];
	$out['email_confirmed'] = (int) $row['email_confirmed'];
	if ($apply) {
		$pdo->prepare('UPDATE `users` SET `password` = ?, `unlocked` = 1, `email_confirmed` = 1 WHERE `user_id` = ?')
			->execute(array($hash, $userId));
		$out['password_reset'] = true;
		$out['groups_bound'] = epc_cp_bind_backend_groups($pdo, $userId, $groups);
	}
	return $out;
}

$backendGroups = epc_cp_backend_group_ids($pdo);
$backendUsers = $pdo->query(
	'SELECT DISTINCT u.`user_id`, u.`email`, u.`unlocked`, u.`email_confirmed`
	 FROM `users` u
	 INNER JOIN `users_groups_bind` b ON b.`user_id` = u.`user_id`
	 INNER JOIN `groups` g ON g.`id` = b.`group_id` AND g.`for_backend` = 1
	 ORDER BY u.`user_id`'
)->fetchAll(PDO::FETCH_ASSOC);

$host = function_exists('epc_portal_host') ? epc_portal_host() : (isset($_SERVER['HTTP_HOST']) ? $_SERVER['HTTP_HOST'] : '');
$backendDir = trim((string) $cfg->backend_dir, '/');
if ($backendDir === '') {
	$backendDir = 'cp';
}

$result = array(
	'status' => true,
	'host' => $host,
	'db' => $cfg->db,
	'apply' => $apply,
	'backend_groups' => $backendGroups,
	'backend_users_before' => $backendUsers,
	'cp_login_url' => 'https://' . $host . '/' . $backendDir . '/',
	'login_field' => 'email (auth_contact when auth_contact_select=email)',
);

if ($apply) {
	$expectedDomain = 'https://' . $host . '/';
	try {
		$pdo->exec("UPDATE `plugins` SET `activated` = 1 WHERE (`dir` LIKE '%authentication%') AND `is_frontend` = 0");
		$result['auth_plugin_activated'] = true;
	} catch (Exception $e) {
		$result['auth_plugin_error'] = $e->getMessage();
	}
	try {
		$stDom = $pdo->prepare('UPDATE `epc_portal_site_settings` SET `domain_path` = ? WHERE `host` = ? OR `host` = ?');
		$stDom->execute(array($expectedDomain, $host, 'www.' . preg_replace('/^www\./', '', $host)));
		$result['domain_path_rows'] = $stDom->rowCount();
	} catch (Exception $e) {
		$result['domain_path_error'] = $e->getMessage();
	}
	try {
		$result['primary'] = epc_cp_upsert_admin($pdo, $cfg, $login, $password, $backendGroups, true);
	} catch (Exception $e) {
		$result['status'] = false;
		$result['primary_error'] = $e->getMessage();
	}
	if ($alsoResetLegacy && $login !== 'admin') {
		try {
			$result['legacy_admin'] = epc_cp_upsert_admin($pdo, $cfg, 'admin', $password, $backendGroups, true);
		} catch (Exception $e) {
			$result['legacy_admin_error'] = $e->getMessage();
		}
	}
	$result['credentials'] = array(
		'login_email' => $login,
		'password' => $password,
		'legacy_login_email' => $alsoResetLegacy ? 'admin' : null,
	);
	$result['note'] = 'Use E-mail on CP login form (not a separate username column).';
} else {
	$result['primary'] = epc_cp_upsert_admin($pdo, $cfg, $login, $password, $backendGroups, false);
	if ($alsoResetLegacy && $login !== 'admin') {
		$result['legacy_admin'] = epc_cp_upsert_admin($pdo, $cfg, 'admin', $password, $backendGroups, false);
	}
}

$jsonFlags = defined('JSON_PRETTY_PRINT') ? JSON_PRETTY_PRINT : 0;
if (defined('JSON_UNESCAPED_UNICODE')) {
	$jsonFlags |= JSON_UNESCAPED_UNICODE;
}
echo json_encode($result, $jsonFlags);
