<?php
/**
 * EPC Auto Price Engine — schema, CP route, tenant presets.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_auto_price_engine.php';

function epc_ape_setup_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

/**
 * @return array{content_id:int,menu_item_id:int,seeded:array}
 */
function epc_auto_price_cp_install(PDO $pdo, string $backendDir = 'cp', bool $apply = true, bool $seed = false, string $siteKey = '', string $hostname = ''): array
{
	$backendDir = trim($backendDir, '/');
	if ($backendDir === '') {
		$backendDir = 'cp';
	}

	epc_ape_ensure_schema($pdo);

	$result = array('content_id' => 0, 'menu_item_id' => 0, 'seeded' => array());

	if ($seed && $siteKey !== '') {
		$result['seeded'] = epc_ape_seed_tenant_preset($pdo, $siteKey, $hostname);
	}

	if (!$apply) {
		return $result;
	}

	require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_cp_mainstream_menu.php';

	epc_ape_setup_lang($pdo, 'epc_portal_auto_price_engine', 'Auto Price AI', 'Auto Price AI');

	$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
	$itemUrl = '/<backend>/control/portal/epc_auto_price_engine?tab=discover';
	$result['menu_item_id'] = (int) epc_cp_mm_ensure_item($pdo, $portalGroup, 'epc_portal_auto_price_engine', $itemUrl, 12, '#0d9488', 'fa-magic', 1);

	$contentUrl = 'control/portal/epc_auto_price_engine';
	$phpPath = '/<backend_dir>/content/control/portal/epc_auto_price_engine.php';
	$now = time();

	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array('control/config'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		$parent->execute(array('control'));
		$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	}
	if (!$parentRow) {
		throw new RuntimeException('Parent content not found for Auto Price Engine CP route');
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($contentUrl));
	$contentId = (int) $existing->fetchColumn();

	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
		)->execute(array($phpPath, 'epc_portal_auto_price_engine', 'epc_portal_auto_price_engine', $parentId, $level, 'epc_auto_price_engine', $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 12)'
		)->execute(array(
			$contentUrl, $level, 'epc_auto_price_engine', 'epc_portal_auto_price_engine', $parentId,
			'Auto Price AI — Discover · Price · Import · Sell (universal multi-tenant)',
			$phpPath, 'epc_portal_auto_price_engine', $now, $now,
		));
		$contentId = (int) $pdo->lastInsertId();
	}
	$result['content_id'] = $contentId;

	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array('control/portal/epc_tax_toolkit_manage'));
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

	epc_ape_setup_lang($pdo, 'epc_portal_auto_price_guide', 'Auto Price guide', 'Гид авто-цен');
	$guideUrl = 'control/portal/epc_auto_price_guide';
	$guidePath = '/<backend_dir>/content/control/portal/epc_auto_price_guide.php';
	$guideExisting = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$guideExisting->execute(array($guideUrl));
	$guideContentId = (int) $guideExisting->fetchColumn();
	if ($guideContentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
		)->execute(array($guidePath, 'epc_portal_auto_price_guide', 'epc_portal_auto_price_guide', $parentId, $level, 'epc_auto_price_guide', $guideContentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 13)'
		)->execute(array(
			$guideUrl, $level, 'epc_auto_price_guide', 'epc_portal_auto_price_guide', $parentId,
			'Auto Price AI operator guide — universal tenant workflow',
			$guidePath, 'epc_portal_auto_price_guide', $now, $now,
		));
		$guideContentId = (int) $pdo->lastInsertId();
	}
	if ($refId > 0 && $guideContentId > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($guideContentId));
		$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$groups->execute(array($refId));
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			try {
				$ins->execute(array($guideContentId, (int) $g['group_id']));
			} catch (Exception $e) {
			}
		}
	}
	$result['guide_content_id'] = $guideContentId;

	$ajaxUrl = 'control/portal/ajax_auto_price';
	$ajaxPath = '/<backend_dir>/content/control/portal/ajax_auto_price.php';
	$ajaxExisting = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ajaxExisting->execute(array($ajaxUrl));
	$ajaxContentId = (int) $ajaxExisting->fetchColumn();
	if ($ajaxContentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
		)->execute(array($ajaxPath, 'epc_portal_ajax_auto_price', 'epc_portal_ajax_auto_price', $parentId, $level, 'ajax_auto_price', $ajaxContentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 14)'
		)->execute(array(
			$ajaxUrl, $level, 'ajax_auto_price', 'epc_portal_ajax_auto_price', $parentId,
			'Auto Price AI AJAX endpoint',
			$ajaxPath, 'epc_portal_ajax_auto_price', $now, $now,
		));
		$ajaxContentId = (int) $pdo->lastInsertId();
	}
	if ($refId > 0 && $ajaxContentId > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($ajaxContentId));
		$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$groups->execute(array($refId));
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			try {
				$ins->execute(array($ajaxContentId, (int) $g['group_id']));
			} catch (Exception $e) {
			}
		}
	}
	$result['ajax_content_id'] = $ajaxContentId;

	return $result;
}

function epc_auto_price_setup_connect(array $cred, DP_Config $cfg): ?PDO
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
