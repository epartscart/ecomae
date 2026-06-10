<?php
/**
 * Register CP ERP module + guide + database schema.
 *
 * Super CP first (platform operator):
 *   1) Deploy PHP via platform-fix (ecomae + client docroots get files).
 *   2) Run on www.ecomae.com to register menus/modules in platform DB.
 *   3) Per tenant: enabled_packs in site_settings + portal deploy (or shared docroot alias).
 *
 * Run: https://www.ecomae.com/epc-erp-cp-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_schema.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_gl.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_erp_lang($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_erp_lang($pdo, 'epc_erp_cp', 'ERP Finance', 'ERP — финансы');
epc_erp_lang($pdo, 'epc_erp_guide_cp', 'ERP guide', 'ERP — инструкция');

epc_erp_gl_ensure_schema($pdo);

function epc_erp_register_content(PDO $pdo, $parentUrl, $url, $alias, $valueKey, $phpPath, $title, $order = 90)
{
	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array($parentUrl));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		throw new Exception('Parent not found: ' . $parentUrl);
	}
	$parentId = (int)$parentRow['id'];
	$level = (int)$parentRow['level'] + 1;
	$now = time();

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($url));
	$contentId = (int)$existing->fetchColumn();

	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0, `system_flag` = 0,
			 `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `url` = ?, `value` = ?, `time_edited` = ?
			 WHERE `id` = ?'
		)->execute(array($phpPath, $title, $parentId, $level, $alias, $url, $valueKey, $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array(
			$url, $level, $alias, $valueKey, $parentId, $title, $phpPath, $title, $now, $now, $order,
		));
		$contentId = (int)$pdo->lastInsertId();
	}

	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$refIds = array();
	foreach (array('shop/orders/orders', 'shop/prices', 'shop/finance/account_operations', 'shop') as $refUrl) {
		$ref->execute(array($refUrl));
		$rid = (int)$ref->fetchColumn();
		if ($rid > 0) {
			$refIds[] = $rid;
		}
	}
	if ($contentId > 0 && !empty($refIds)) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
		$placeholders = implode(',', array_fill(0, count($refIds), '?'));
		$groups = $pdo->prepare(
			'SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` IN (' . $placeholders . ')'
		);
		$groups->execute($refIds);
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			try {
				$ins->execute(array($contentId, (int)$g['group_id']));
			} catch (Exception $e) {
			}
		}
		if ((int)$pdo->query('SELECT COUNT(*) FROM `content_access` WHERE `content_id` = ' . (int)$contentId)->fetchColumn() === 0) {
			$pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, 1)')->execute(array($contentId));
		}
	}
	return $contentId;
}

$mainContentId = epc_erp_register_content(
	$pdo,
	'shop',
	'shop/finance/erp',
	'erp',
	'epc_erp_cp',
	'/<backend_dir>/content/shop/finance/erp/erp_main_page.php',
	'ERP Finance — eParts Cart',
	88
);

$guideContentId = epc_erp_register_content(
	$pdo,
	'shop/finance/erp',
	'shop/finance/erp/guide',
	'guide',
	'epc_erp_guide_cp',
	'/<backend_dir>/content/shop/finance/erp/erp_guide_page.php',
	'ERP guide — eParts Cart',
	1
);

$menu = epc_cp_mainstream_menu_apply($pdo);
$controlId = (int)$menu['items']['erp_hub'];

echo "ERP module registered.\n";
echo "content_id (main): {$mainContentId}\n";
echo "content_id (guide): {$guideContentId}\n";
echo "control_items_id: {$controlId}\n";
echo "CP menu: ERP Finance group {$menu['erp_group']}\n";
echo "URL: /" . $cfg->backend_dir . "/shop/finance/erp\n";
echo "Guide: /" . $cfg->backend_dir . "/shop/finance/erp/guide\n";
echo "Schema: epc_erp_* + COA/GL tables ready.\n";
