<?php
/**
 * Ensure Super CP operator login.
 * https://www.ecomae.com/ecomae-setup-super-admin.php?token=...&apply=1&email=...&password=...
 */
error_reporting(0);
ini_set('display_errors', '0');
header('Content-Type: application/json; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token(false, 'secret');

$apply = !empty($_GET['apply']);
$email = trim(isset($_GET['email']) ? (string) $_GET['email'] : '');
$password = isset($_GET['password']) ? (string) $_GET['password'] : '';
if ($email === '' || $password === '') {
	http_response_code(400);
	exit(json_encode(array('status' => false, 'message' => 'email and password required')));
}
require_once __DIR__ . '/config.php';
$cfg = new DP_Config();
require_once __DIR__ . '/content/general_pages/epc_portal.php';
if (function_exists('epc_portal_apply_config')) {
	epc_portal_apply_config($cfg);
}

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

$st = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
$root = $st->fetch(PDO::FETCH_ASSOC);
$groups = $root ? array((int) $root['id']) : array(1);

$result = array('status' => true, 'db' => $cfg->db, 'apply' => $apply, 'email' => $email, 'backend_groups' => $groups);

$st = $pdo->prepare('SELECT `user_id`, `email` FROM `users` WHERE `email` = ? LIMIT 1');
$st->execute(array($email));
$user = $st->fetch(PDO::FETCH_ASSOC);
$passwordHash = md5($password . $cfg->secret_succession);
$userId = 0;

if (!$user) {
	if ($apply) {
		$pdo->prepare(
			'INSERT INTO `users` (`email`, `email_confirmed`, `password`, `unlocked`, `reg_variant`, `time_created`, `admin_created`) VALUES (?, 1, ?, 1, 1, ?, 1)'
		)->execute(array($email, $passwordHash, time()));
		$userId = (int) $pdo->lastInsertId();
		$pdo->prepare('INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES (?, ?, ?)')->execute(array($userId, 'name', 'Platform operator'));
		$result['user_created'] = $userId;
	} else {
		$result['user'] = 'missing';
	}
} else {
	$userId = (int) $user['user_id'];
	$result['user_id'] = $userId;
	if ($apply) {
		$pdo->prepare('UPDATE `users` SET `password` = ?, `unlocked` = 1, `email_confirmed` = 1 WHERE `user_id` = ?')->execute(array($passwordHash, $userId));
		$result['password_reset'] = true;
	}
}

if ($apply && $userId > 0) {
	$ins = $pdo->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)');
	foreach ($groups as $gid) {
		$ins->execute(array($userId, (int) $gid));
	}
	$result['groups_bound'] = $groups;
}

$result['login'] = array('url' => 'https://www.ecomae.com/cp/', 'email' => $email);
echo json_encode($result, JSON_PRETTY_PRINT);
