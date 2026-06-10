<?php
/**
 * Remove stale ASAP tenant user copies from ecomae platform DB (pre-isolation clones).
 * Run once on server after CP URL separation deploy.
 *
 * https://www.ecomae.com/epc-purge-stale-asap-platform-users.php?token=epartscart-deploy-2026&apply=1
 */
header('Content-Type: text/plain; charset=utf-8');

$token = isset($_GET['token']) ? (string) $_GET['token'] : '';
if ($token !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once $_SERVER['DOCUMENT_ROOT'] . '/config.php';
$cfg = new DP_Config();

$apply = !empty($_GET['apply']) && (string) $_GET['apply'] === '1';
$staleEmails = array('asap_admin@asap-ae.com', 'asap_demo@asap-ae.com');

echo "=== Purge stale ASAP users from platform DB ({$cfg->db}) ===\n";
echo 'apply=' . ($apply ? 'yes' : 'dry-run') . "\n\n";

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Exception $e) {
	exit('DB connect failed: ' . $e->getMessage() . "\n");
}

foreach ($staleEmails as $email) {
	$st = $pdo->prepare('SELECT `user_id`, `email` FROM `users` WHERE `email` = ? LIMIT 5');
	$st->execute(array(htmlentities($email)));
	$rows = $st->fetchAll(PDO::FETCH_ASSOC);
	if ($rows === array()) {
		echo "OK — not in platform DB: {$email}\n";
		continue;
	}
	foreach ($rows as $row) {
		$uid = (int) $row['user_id'];
		echo "FOUND platform copy: user_id={$uid} email={$email}\n";
		if ($apply) {
			$pdo->prepare('DELETE FROM `users_groups_bind` WHERE `user_id` = ?')->execute(array($uid));
			$pdo->prepare('DELETE FROM `sessions` WHERE `user_id` = ?')->execute(array($uid));
			$pdo->prepare('DELETE FROM `users_options` WHERE `session_id` IN (SELECT `id` FROM `sessions` WHERE `user_id` = ?)')->execute(array($uid));
			$pdo->prepare('DELETE FROM `users` WHERE `user_id` = ?')->execute(array($uid));
			echo "  DELETED user_id={$uid}\n";
		}
	}
}

echo "\nDone. ASAP staff must use /cp/client-erp/asap/ only.\n";
