<?php
/**
 * Super CP ABCP-inspired modules — schema, menu, content registration.
 * https://www.ecomae.com/epc-super-cp-abcp-setup.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$apply = !empty($_GET['apply']);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_super_cp_platform.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	exit("Platform DB connect failed\n");
}

echo "=== ECOM AE Super CP ABCP Modules Setup ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

epc_scp_platform_ensure_schema($pdo);
echo "Schema: epc_platform_price_configs, epc_platform_info_blocks, epc_platform_comm_settings, epc_platform_internal_tasks OK\n";

function epc_scp_abcp_setup_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

function epc_scp_abcp_register_content(PDO $pdo, string $contentUrl, string $phpPath, string $titleTag, string $alias, string $description, int $order): int
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
	$ref->execute(array('control/portal/epc_tenant_control_center'));
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

$pages = array(
	array(
		'url' => 'control/portal/epc_super_cp_operator_guide',
		'php' => '/<backend_dir>/content/control/portal/epc_super_cp_operator_guide.php',
		'title' => 'epc_super_cp_operator_guide',
		'alias' => 'epc_super_cp_operator_guide',
		'desc' => 'Super CP operator workspace guide',
		'order' => 7,
	),
	array(
		'url' => 'control/portal/epc_super_cp_customer_board',
		'php' => '/<backend_dir>/content/control/portal/epc_super_cp_customer_board.php',
		'title' => 'epc_super_cp_customer_board',
		'alias' => 'epc_super_cp_customer_board',
		'desc' => 'Super CP customer board — cross-tenant search and ERP links',
		'order' => 8,
	),
	array(
		'url' => 'control/portal/epc_super_cp_price_configs',
		'php' => '/<backend_dir>/content/control/portal/epc_super_cp_price_configs.php',
		'title' => 'epc_super_cp_price_configs',
		'alias' => 'epc_super_cp_price_configs',
		'desc' => 'Super CP price generation configs',
		'order' => 9,
	),
	array(
		'url' => 'control/portal/epc_super_cp_info_blocks',
		'php' => '/<backend_dir>/content/control/portal/epc_super_cp_info_blocks.php',
		'title' => 'epc_super_cp_info_blocks',
		'alias' => 'epc_super_cp_info_blocks',
		'desc' => 'Super CP CMS info blocks',
		'order' => 10,
	),
	array(
		'url' => 'control/portal/epc_super_cp_communication',
		'php' => '/<backend_dir>/content/control/portal/epc_super_cp_communication.php',
		'title' => 'epc_super_cp_communication',
		'alias' => 'epc_super_cp_communication',
		'desc' => 'Super CP communication setup and internal tasks',
		'order' => 11,
	),
);

if ($apply) {
	foreach ($pages as $p) {
		epc_scp_abcp_setup_lang($pdo, $p['title'], str_replace('epc_super_cp_', '', str_replace('_', ' ', $p['title'])), $p['desc']);
	}

	$menu = epc_cp_super_cp_operator_menu_apply($pdo);
	echo "Operator menu group: " . (int) $menu['operator_group'] . "\n";
	foreach (array('operator_guide', 'customer_board', 'price_configs', 'info_blocks', 'communication') as $k) {
		echo "  {$k} item: " . (int) ($menu[$k] ?? 0) . "\n";
	}

	foreach ($pages as $p) {
		$id = epc_scp_abcp_register_content($pdo, $p['url'], $p['php'], $p['title'], $p['alias'], $p['desc'], (int) $p['order']);
		echo "Content {$p['url']}: id={$id}\n";
	}

	$disk = array(
		'helpers' => is_file(__DIR__ . '/content/general_pages/epc_super_cp_platform.php'),
		'operator_guide' => is_file(__DIR__ . '/cp/content/control/portal/epc_super_cp_operator_guide.php'),
		'customer_board' => is_file(__DIR__ . '/cp/content/control/portal/epc_super_cp_customer_board.php'),
		'price_configs' => is_file(__DIR__ . '/cp/content/control/portal/epc_super_cp_price_configs.php'),
		'info_blocks' => is_file(__DIR__ . '/cp/content/control/portal/epc_super_cp_info_blocks.php'),
		'communication' => is_file(__DIR__ . '/cp/content/control/portal/epc_super_cp_communication.php'),
	);
	echo "\nFiles on disk:\n";
	foreach ($disk as $k => $ok) {
		echo "  {$k}: " . ($ok ? 'yes' : 'NO') . "\n";
	}

	echo "\nVerify URLs (expect 200 when logged in as Super CP admin):\n";
	echo "  https://www.ecomae.com/cp/control/portal/epc_super_cp_operator_guide\n";
	echo "  https://www.ecomae.com/cp/control/portal/epc_super_cp_customer_board\n";
	echo "  https://www.ecomae.com/cp/control/portal/epc_super_cp_price_configs\n";
	echo "  https://www.ecomae.com/cp/control/portal/epc_super_cp_info_blocks\n";
	echo "  https://www.ecomae.com/cp/control/portal/epc_super_cp_communication\n";
}

echo "\nDone.\n";
