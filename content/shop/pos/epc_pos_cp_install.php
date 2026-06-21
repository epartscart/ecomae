<?php
/**
 * POS — CP content routes, menu, schema (per MySQL database).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_pos_helpers.php';

function epc_pos_cp_lang(PDO $pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

function epc_pos_cp_register_content(PDO $pdo, $parentUrl, $url, $alias, $valueKey, $phpPath, $title, $order = 88)
{
	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array($parentUrl));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		throw new Exception('Parent not found: ' . $parentUrl);
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;
	$now = time();
	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($url));
	$contentId = (int) $existing->fetchColumn();
	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `value` = ?, `time_edited` = ? WHERE `id` = ?'
		)->execute(array($phpPath, $title, $parentId, $level, $alias, $valueKey, $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array($url, $level, $alias, $valueKey, $parentId, $title, $phpPath, $title, $now, $now, $order));
		$contentId = (int) $pdo->lastInsertId();
	}
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$st = $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1');
	$root = (int) $st->fetchColumn();
	$groups = array($root > 0 ? $root : 1);
	$collect = function ($pid) use ($pdo, &$collect, &$groups) {
		$ch = $pdo->prepare('SELECT `id` FROM `groups` WHERE `parent` = ?');
		$ch->execute(array($pid));
		while ($r = $ch->fetch(PDO::FETCH_ASSOC)) {
			$groups[] = (int) $r['id'];
			$collect((int) $r['id']);
		}
	};
	if ($root > 0) {
		$collect($root);
	}
	$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	foreach (array_unique($groups) as $gid) {
		$ins->execute(array($contentId, (int) $gid));
	}
	return $contentId;
}

/**
 * @return array{hub_content_id:int,content_id:int,menu:array,walkin_user_id:int}
 */
function epc_pos_cp_install(PDO $pdo, $backendDir = 'cp')
{
	$mainstream = $_SERVER['DOCUMENT_ROOT'] . '/epc_cp_mainstream_menu.php';
	if (is_file($mainstream)) {
		require_once $mainstream;
	}

	epc_pos_cp_lang($pdo, 'epc_pos_terminal_cp', 'POS Terminal', 'Касса POS');
	epc_pos_cp_lang($pdo, 'epc_cp_group_pos', 'Point of Sale', 'Касса');
	epc_pos_cp_lang($pdo, 'epc_portal_pos_manage', 'POS overview', 'Обзор POS');

	epc_pos_ensure_schema($pdo);
	$walkinId = epc_pos_ensure_walkin_user($pdo);

	$hubId = epc_pos_cp_register_content(
		$pdo,
		'shop',
		'shop/pos',
		'pos_folder',
		'epc_cp_group_pos',
		'/<backend_dir>/content/shop/pos/epc_pos_hub_page.php',
		'Point of Sale',
		86
	);

	$contentId = epc_pos_cp_register_content(
		$pdo,
		'shop/pos',
		'shop/pos/terminal',
		'pos_terminal',
		'epc_pos_terminal_cp',
		'/<backend_dir>/content/shop/pos/epc_pos_terminal_page.php',
		'POS Terminal',
		87
	);

	$menu = function_exists('epc_cp_pos_menu_apply') ? epc_cp_pos_menu_apply($pdo) : array();
	if (function_exists('epc_cp_portal_menu_apply')) {
		$portalMenu = epc_cp_portal_menu_apply($pdo);
		$menu['portal'] = $portalMenu;
	}

	$superContentId = 0;
	if (function_exists('epc_portal_is_super_cp_host') || true) {
		try {
			$superContentId = epc_pos_cp_register_super_route($pdo, $backendDir);
		} catch (Exception $e) {
		}
	}

	return array(
		'hub_content_id' => $hubId,
		'content_id' => $contentId,
		'super_content_id' => $superContentId,
		'menu' => $menu,
		'walkin_user_id' => $walkinId,
	);
}

function epc_pos_cp_register_super_route(PDO $pdo, $backendDir = 'cp')
{
	$backendDir = trim((string) $backendDir, '/');
	$contentUrl = 'control/portal/epc_pos_tenant_manage';
	$phpPath = '/<backend_dir>/content/control/portal/epc_pos_tenant_manage.php';
	$now = time();

	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array('control/config'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		$parent->execute(array('control'));
		$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	}
	if (!$parentRow) {
		return 0;
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($contentUrl));
	$contentId = (int) $existing->fetchColumn();

	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
		)->execute(array($phpPath, 'epc_portal_pos_manage', 'epc_portal_pos_manage', $parentId, $level, 'epc_pos_manage', $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 12)'
		)->execute(array(
			$contentUrl, $level, 'epc_pos_manage', 'epc_portal_pos_manage', $parentId,
			'Super CP — POS overview and tenant enablement',
			$phpPath, 'epc_portal_pos_manage', $now, $now,
		));
		$contentId = (int) $pdo->lastInsertId();
	}

	if (function_exists('epc_cp_mm_ensure_group') && function_exists('epc_cp_mm_ensure_item')) {
		epc_pos_cp_lang($pdo, 'epc_portal_pos_manage', 'POS overview', 'Обзор POS');
		$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
		epc_cp_mm_ensure_item($pdo, $portalGroup, 'epc_portal_pos_manage', '/<backend>/control/portal/epc_pos_tenant_manage', 12, '#2563eb', 'fa-cash-register', 1);
	}

	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array('control/portal/epc_tenant_control_center'));
	$refId = (int) $ref->fetchColumn();
	if ($refId > 0 && $contentId > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
		$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$groups->execute(array($refId));
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			try {
				$ins->execute(array($contentId, (int) $g['group_id']));
			} catch (Exception $e) {
			}
		}
	}

	return $contentId;
}

function epc_pos_setup_connect(array $cred, DP_Config $cfg): ?PDO
{
	$db = trim((string) ($cred['db'] ?? ''));
	if ($db === '') {
		return null;
	}
	$user = trim((string) ($cred['user'] ?? ''));
	if ($user === '') {
		$user = (string) $cfg->user;
	}
	$pass = (string) ($cred['pass'] ?? '');
	if ($pass === '') {
		$pass = (string) $cfg->password;
	}
	try {
		return new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $db . ';charset=utf8',
			$user,
			$pass,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Throwable $e) {
		return null;
	}
}
