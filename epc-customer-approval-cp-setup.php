<?php
/**
 * Register CP page: Users → Customer trade approvals
 * https://www.epartscart.com/epc-customer-approval-cp-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

$pdo->prepare("INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES ('epc_customer_approvals_cp', 'Customer trade approvals', NULL, 0, 1, 1)")->execute();
$pdo->prepare("INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES ('epc_customer_approvals_cp', 'en', 'Customer approvals') ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)")->execute();
$pdo->prepare("INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES ('epc_customer_approvals_cp', 'ru', 'Одобрение клиентов') ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)")->execute();

$parent = $pdo->prepare("SELECT `id`, `level` FROM `content` WHERE `url` = 'users' AND `is_frontend` = 0 LIMIT 1");
$parent->execute();
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
	exit("Parent content users not found\n");
}
$parentId = (int)$parentRow['id'];
$level = (int)$parentRow['level'] + 1;
$url = 'users/customer_approvals';
$phpPath = '/<backend_dir>/content/users/epc_customer_approvals.php';
$now = time();

$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$existing->execute(array($url));
$contentId = (int)$existing->fetchColumn();

if ($contentId > 0) {
	$pdo->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `value` = ? WHERE `id` = ?'
	)->execute(array($phpPath, 'Customer trade approvals', $parentId, $level, 'epc_customer_approvals_cp', $contentId));
} else {
	$pdo->prepare(
		'INSERT INTO `content`
		(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 50)'
	)->execute(array(
		$url, $level, 'customer_approvals', 'epc_customer_approvals_cp', $parentId,
		'Approve retail/wholesale registrations and assign dealing currency',
		$phpPath, 'Customer trade approvals', $now, $now,
	));
	$contentId = (int)$pdo->lastInsertId();
}

$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ref->execute(array('users/usermanager'));
$refId = (int)$ref->fetchColumn();
if ($refId > 0 && $contentId > 0) {
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
	$groups->execute(array($refId));
	$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
		try {
			$ins->execute(array($contentId, (int)$g['group_id']));
		} catch (Exception $e) {
		}
	}
}

$usersGroup = 2;
$umRef = $pdo->prepare("SELECT `items_group`, `order` FROM `control_items` WHERE `url` LIKE ? LIMIT 1");
$umRef->execute(array('%/users/usermanager%'));
$umRow = $umRef->fetch(PDO::FETCH_ASSOC);
$menuOrder = 45;
if ($umRow) {
	$usersGroup = (int)$umRow['items_group'];
	if ($usersGroup <= 0) {
		$usersGroup = 2;
	}
	$menuOrder = (int)$umRow['order'] + 1;
}

$menuUrl = '/<backend>/users/customer_approvals';
$menuCheck = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `url` = ? LIMIT 1');
$menuCheck->execute(array($menuUrl));
$menuId = (int)$menuCheck->fetchColumn();
if ($menuId <= 0) {
	$pdo->prepare(
		'INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, 0)'
	)->execute(array($usersGroup, 'epc_customer_approvals_cp', $menuUrl, '', $menuOrder, '#16a085', 'fas fa-user-check', ''));
	echo "control_items menu entry created\n";
} else {
	$pdo->prepare('UPDATE `control_items` SET `items_group` = ?, `caption` = ?, `order` = ?, `background_color` = ?, `fontawesome_class` = ? WHERE `id` = ?')->execute(array($usersGroup, 'epc_customer_approvals_cp', $menuOrder, '#16a085', 'fas fa-user-check', $menuId));
	echo "control_items menu entry updated (id {$menuId})\n";
}

echo "content_id: {$contentId}\n";
echo "CP URL: /cp/users/customer_approvals\n";
echo "Done.\n";
