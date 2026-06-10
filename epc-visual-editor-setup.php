<?php
/**
 * ECOM Visual Page Editor — schema, content route, Operator menu.
 * Run: https://www.ecomae.com/epc-visual-editor-setup.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$apply = !empty($_GET['apply']);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_visual_page_editor.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	exit("Platform DB connect failed\n");
}

echo "=== ECOM Visual Page Editor Setup ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

epc_vpe_ensure_schema($pdo);
echo "Schema: epc_page_builder_layouts OK\n";
echo "Schema: epc_platform_info_blocks (existing) OK\n\n";

function epc_vpe_setup_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

function epc_vpe_register_content(PDO $pdo, string $contentUrl, string $phpPath, string $titleTag, string $alias, string $description, int $order): int
{
	$now = time();
	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array('control/config'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		$parent->execute(array('control'));
		$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	}
	if (!$parentRow) {
		throw new RuntimeException('Parent content not found');
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($contentUrl));
	$contentId = (int) $existing->fetchColumn();

	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ?, `description` = ? WHERE `id` = ?'
		)->execute(array($phpPath, $titleTag, $titleTag, $parentId, $level, $alias, $description, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array(
			$contentUrl, $level, $alias, $titleTag, $parentId, $description,
			$phpPath, $titleTag, $now, $now, $order,
		));
		$contentId = (int) $pdo->lastInsertId();
	}

	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array('control/portal/epc_super_cp_info_blocks'));
	$refId = (int) $ref->fetchColumn();
	if ($refId <= 0) {
		$ref->execute(array('control/portal/industry_settings'));
		$refId = (int) $ref->fetchColumn();
	}
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

if ($apply) {
	epc_vpe_setup_lang($pdo, 'epc_visual_page_editor', 'Visual page editor', 'Визуальный редактор страниц');

	$operatorGroup = epc_cp_mm_group_id($pdo, 'epc_cp_group_operator');
	if ($operatorGroup <= 0) {
		epc_cp_super_cp_operator_menu_apply($pdo);
		$operatorGroup = epc_cp_mm_group_id($pdo, 'epc_cp_group_operator');
	}
	$itemId = epc_cp_mm_ensure_item(
		$pdo,
		$operatorGroup,
		'epc_visual_page_editor',
		'/<backend>/control/portal/epc_visual_page_editor',
		6,
		'#7c3aed',
		'fas fa-magic',
		1
	);
	echo "Operator menu item id: {$itemId}\n";

	$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
	$portalItem = epc_cp_mm_ensure_item(
		$pdo,
		$portalGroup,
		'epc_visual_page_editor',
		'/<backend>/control/portal/epc_visual_page_editor',
		12,
		'#7c3aed',
		'fas fa-magic',
		1
	);
	echo "Portal menu item id: {$portalItem}\n";

	$contentId = epc_vpe_register_content(
		$pdo,
		'control/portal/epc_visual_page_editor',
		'/<backend_dir>/content/control/portal/epc_visual_page_editor.php',
		'epc_visual_page_editor',
		'epc_visual_page_editor',
		'ECOM Visual Page Editor — block layout, brand colours, live preview',
		12
	);
	echo "Content route id: {$contentId}\n";

	$disk = array(
		'helpers' => is_file(__DIR__ . '/content/general_pages/epc_visual_page_editor.php'),
		'cp_module' => is_file(__DIR__ . '/cp/content/control/portal/epc_visual_page_editor.php'),
		'ajax' => is_file(__DIR__ . '/cp/content/control/portal/ajax_visual_page_editor.php'),
		'js' => is_file(__DIR__ . '/cp/content/control/portal/epc_visual_page_editor.js'),
		'css_cp' => is_file(__DIR__ . '/cp/content/control/portal/epc_visual_page_editor.css'),
		'render' => is_file(__DIR__ . '/content/general_pages/epc_page_builder_render.php'),
		'render_css' => is_file(__DIR__ . '/content/general_pages/epc_page_builder_render.css'),
	);
	echo "\nFiles on disk:\n";
	foreach ($disk as $k => $ok) {
		echo "  {$k}: " . ($ok ? 'yes' : 'NO') . "\n";
	}

	echo "\nVerify URLs (expect 200/302 when logged in as admin):\n";
	echo "  https://www.ecomae.com/cp/control/portal/epc_visual_page_editor\n";
	echo "  https://www.ecomae.com/cp/content/control/portal/ajax_visual_page_editor.php?action=load_layout&site_key=platform\n";
}

echo "\nDone.\n";
