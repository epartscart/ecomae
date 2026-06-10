<?php
/**
 * Register frontend ERP portal (shop/erp) + ERP team user group.
 * Run once: https://www.epartscart.com/epc-erp-frontend-setup.php?token=epartscart-deploy-2026
 * Optional: email=user@example.com — add user to ERP team group
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_schema.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_gl.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_menu_link.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_erp_fe_lang($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_erp_fe_lang($pdo, 'EPC_ERP_TEAM', 'ERP Finance team', 'ERP — команда финансов');
epc_erp_fe_lang($pdo, 'EPC_ERP_PORTAL', 'ERP Finance', 'ERP — финансы');
epc_erp_fe_lang($pdo, 'EPC_ERP_PORTAL_DESC', 'Sales, purchases, GL, P&L and balance sheet', 'Продажи, закупки, ГК, P&L и баланс');
epc_erp_fe_lang($pdo, 'EPC_ERP_GUIDE_FE', 'ERP guide', 'ERP — инструкция');
epc_erp_fe_lang($pdo, 'epc_menu_erp_login', 'ERP Login', 'Вход ERP');

epc_erp_gl_ensure_schema($pdo);

$groupKey = 'EPC_ERP_TEAM';
$groupId = (int)$pdo->query("SELECT `id` FROM `groups` WHERE `value` = " . $pdo->quote($groupKey) . " LIMIT 1")->fetchColumn();
if ($groupId <= 0) {
	$maxId = (int)$pdo->query('SELECT IFNULL(MAX(`id`), 0) FROM `groups`')->fetchColumn();
	$groupId = $maxId + 1;
	$pdo->prepare(
		'INSERT INTO `groups` (`id`, `value`, `count`, `level`, `parent`, `unblocked`, `for_guests`, `for_registrated`, `for_backend`, `for_percentage`, `description`, `order`)
		 VALUES (?, ?, 0, 2, 1, 1, 0, 0, 0, 0, ?, 95)'
	)->execute(array($groupId, $groupKey, $groupKey));
	$pdo->exec('UPDATE `groups` SET `count` = (SELECT COUNT(*) FROM (SELECT `id` FROM `groups` WHERE `parent` = 1) x) WHERE `id` = 1');
}

function epc_erp_fe_register_content(PDO $pdo, $parentId, $url, $alias, $valueKey, $phpPath, $titleKey, $order = 50)
{
	$now = time();
	$level = 2;
	if ($parentId > 0) {
		$pl = $pdo->prepare('SELECT `level` FROM `content` WHERE `id` = ? LIMIT 1');
		$pl->execute(array($parentId));
		$lv = (int)$pl->fetchColumn();
		if ($lv > 0) {
			$level = $lv + 1;
		}
	}
	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 1 LIMIT 1');
	$existing->execute(array($url));
	$contentId = (int)$existing->fetchColumn();
	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 1, `system_flag` = 0,
			 `content` = ?, `title_tag` = ?, `description_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `value` = ?, `time_edited` = ?, `robots_tag` = \'\'
			 WHERE `id` = ?'
		)->execute(array($phpPath, $titleKey, $titleKey, $parentId, $level, $alias, $valueKey, $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 1, \'php\', ?, ?, ?, \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array(
			$url, $level, $alias, $valueKey, $parentId, $titleKey, $phpPath, $titleKey, $titleKey, $now, $now, $order,
		));
		$contentId = (int)$pdo->lastInsertId();
	}
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	// Frontend ERP: do not set content_access here — guests must reach the login form.
	// Team membership is enforced inside erp_portal.php and ajax_erp.php.
	return $contentId;
}

$shopParent = (int)$pdo->query("SELECT `id` FROM `content` WHERE `url` = 'shop' AND `is_frontend` = 1 LIMIT 1")->fetchColumn();

$mainId = epc_erp_fe_register_content(
	$pdo,
	$shopParent,
	'shop/erp',
	'erp',
	'EPC_ERP_PORTAL',
	'/content/shop/finance/erp_portal.php',
	'EPC_ERP_PORTAL',
	88
);

$guideId = epc_erp_fe_register_content(
	$pdo,
	$mainId > 0 ? $mainId : $shopParent,
	'shop/erp/guide',
	'guide',
	'EPC_ERP_GUIDE_FE',
	'/content/shop/finance/erp_guide_portal.php',
	'EPC_ERP_GUIDE_FE',
	1
);

$userFixes = array();
$email = trim((string)($_GET['email'] ?? ''));
if ($email !== '') {
	$uq = $pdo->prepare('SELECT `user_id`, `email` FROM `users` WHERE `email` = ? OR `email` LIKE ? LIMIT 10');
	$uq->execute(array($email, '%' . $email . '%'));
	$ins = $pdo->prepare('INSERT IGNORE INTO `users_groups_bind` (`user_id`, `group_id`) VALUES (?, ?)');
	while ($u = $uq->fetch(PDO::FETCH_ASSOC)) {
		$ins->execute(array((int)$u['user_id'], $groupId));
		$userFixes[] = array(
			'user_id' => (int)$u['user_id'],
			'email' => $u['email'],
			'added' => $ins->rowCount() > 0,
		);
	}
}

$menuLink = epc_erp_menu_link_remove($pdo, $mainId);

$base = rtrim($cfg->domain_path, '/');
echo json_encode(array(
	'status' => true,
	'message' => 'Frontend ERP portal registered',
	'erp_group_id' => $groupId,
	'erp_group_key' => $groupKey,
	'content' => array(
		'main' => array('id' => $mainId, 'url' => 'shop/erp'),
		'guide' => array('id' => $guideId, 'url' => 'shop/erp/guide'),
	),
	'menu' => $menuLink,
	'users' => $userFixes,
	'urls' => array(
		'portal_en' => $base . '/en/shop/erp',
		'portal' => $base . '/shop/erp',
		'guide_en' => $base . '/en/shop/erp/guide',
		'cp_erp' => $base . '/' . $cfg->backend_dir . '/shop/finance/erp',
	),
	'hint' => 'Create a customer user per ERP staff member, then grant: epc-erp-frontend-setup.php?token=...&email=USER@domain.com',
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
