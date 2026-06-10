<?php
/**
 * Register Custom & Shipping ERP tab, guide page, DB schema, CP menu.
 * Run: https://www.ecomae.com/epc-custom-shipping-setup.php?token=epartscart-deploy-2026
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
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/shop/finance/epc_custom_shipping.php';
require_once __DIR__ . '/content/shop/finance/epc_custom_declaration_pdf_import.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_cs_setup_lang($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_cs_setup_lang($pdo, 'epc_custom_shipping_cp', 'Custom & Shipping', 'Таможня и доставка');
epc_cs_setup_lang($pdo, 'epc_custom_shipping_guide_cp', 'Custom & Shipping guide', 'Гид: таможня и доставка');
epc_cs_setup_lang($pdo, 'epc_portal_custom_shipping_guide', 'Custom & Shipping guide', 'Гид: таможня и доставка');

epc_cs_ensure_schema($pdo);
epc_cs_ensure_box_schema($pdo);

function epc_cs_register_content(PDO $pdo, $parentUrl, $url, $alias, $valueKey, $phpPath, $title, $order = 90)
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

$guideContentId = epc_cs_register_content(
	$pdo,
	'shop/finance/erp',
	'shop/finance/erp/custom-shipping-guide',
	'custom-shipping-guide',
	'epc_custom_shipping_guide_cp',
	'/<backend_dir>/content/shop/finance/erp/custom_shipping/custom_shipping_guide_page.php',
	'Custom & Shipping guide',
	15
);

$menu = epc_cp_mainstream_menu_apply($pdo);

$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
$portalItemId = epc_cp_mm_ensure_item(
	$pdo,
	$portalGroup,
	'epc_portal_custom_shipping_guide',
	'/<backend>/control/portal/epc_custom_shipping_guide',
	12,
	'#0f766e',
	'fas fa-ship',
	1
);

$portalContentUrl = 'control/portal/epc_custom_shipping_guide';
$portalPhpPath = '/<backend_dir>/content/control/portal/epc_custom_shipping_guide.php';
$now = time();
$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$parent->execute(array('control/config'));
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
	$parent->execute(array('control'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
}
$portalContentId = 0;
if ($parentRow) {
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;
	$existingPortal = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existingPortal->execute(array($portalContentUrl));
	$portalContentId = (int) $existingPortal->fetchColumn();
	if ($portalContentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
		)->execute(array($portalPhpPath, 'epc_portal_custom_shipping_guide', 'epc_portal_custom_shipping_guide', $parentId, $level, 'epc_custom_shipping_guide', $portalContentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 12)'
		)->execute(array(
			$portalContentUrl, $level, 'epc_custom_shipping_guide', 'epc_portal_custom_shipping_guide', $parentId,
			'Custom & Shipping ERP module deploy and operator workflow',
			$portalPhpPath, 'epc_portal_custom_shipping_guide', $now, $now,
		));
		$portalContentId = (int) $pdo->lastInsertId();
	}
	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array('control/portal/industry_settings'));
	$refId = (int) $ref->fetchColumn();
	if ($refId > 0 && $portalContentId > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($portalContentId));
		$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$groups->execute(array($refId));
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			try {
				$ins->execute(array($portalContentId, (int) $g['group_id']));
			} catch (Exception $e) {
			}
		}
	}
}

$base = rtrim($cfg->domain_path, '/');
$shell = '?area=custom_shipping&tab=custom_shipping&epc_erp_shell=1';

echo json_encode(array(
	'status' => true,
	'message' => 'Custom & Shipping module registered (Phase 1 + line items + Phase 2 reports)',
	'line_items_table' => 'epc_custom_shipping_declaration_items',
	'guide_content_id' => $guideContentId,
	'declaration_types' => array_sum(array_map('count', epc_cs_declaration_types_registry())),
	'declaration_box_fields' => epc_cs_declaration_box_count(),
	'pdf_import' => true,
	'database' => $cfg->db,
	'menu' => array(
		'erp_group' => $menu['erp_group'] ?? null,
		'custom_shipping_item' => $menu['items']['custom_shipping'] ?? null,
		'portal_menu_item_id' => $portalItemId ?? null,
	),
	'portal' => array(
		'content_id' => $portalContentId ?? 0,
		'super_cp_url' => $base . '/' . $cfg->backend_dir . '/control/portal/epc_custom_shipping_guide',
	),
	'urls' => array(
		'cp_module' => $base . '/' . $cfg->backend_dir . '/shop/finance/erp' . $shell,
		'cp_guide' => $base . '/' . $cfg->backend_dir . '/shop/finance/erp/custom-shipping-guide' . '?epc_erp_shell=1',
	),
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
