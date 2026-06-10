<?php
/**
 * Demo sandbox tables + Super CP menu registration.
 * https://www.ecomae.com/epc-demo-setup.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$apply = !empty($_GET['apply']);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_demo.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = epc_portal_demo_platform_pdo();
if (!$pdo instanceof PDO) {
	exit("Platform DB connect failed\n");
}

echo "=== EPC Demo Sandbox Setup ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

epc_portal_demo_ensure_schema($pdo);
echo "Tables: epc_portal_demo_requests + epc_portal_tenants demo columns OK\n";

function epc_demo_setup_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

if ($apply) {
	epc_demo_setup_lang($pdo, 'epc_portal_demo_tenants', 'Demo tenants', 'Демо-арендаторы');
	$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
	$itemUrl = '/<backend>/control/portal/epc_demo_tenants_manage';
	$itemId = epc_cp_mm_ensure_item($pdo, $portalGroup, 'epc_portal_demo_tenants', $itemUrl, 7, '#7c3aed', 'fas fa-flask', 1);

	$contentUrl = 'control/portal/epc_demo_tenants_manage';
	$phpPath = '/<backend_dir>/content/control/portal/epc_demo_tenants_manage.php';
	$now = time();

	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array('control/config'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		$parent->execute(array('control'));
		$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	}
	if (!$parentRow) {
		exit("Parent content not found\n");
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($contentUrl));
	$contentId = (int) $existing->fetchColumn();

	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
		)->execute(array($phpPath, 'epc_portal_demo_tenants', 'epc_portal_demo_tenants', $parentId, $level, 'epc_demo_tenants_manage', $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 7)'
		)->execute(array(
			$contentUrl, $level, 'epc_demo_tenants_manage', 'epc_portal_demo_tenants', $parentId,
			'Demo sandbox tenants — extend, convert, delete',
			$phpPath, 'epc_portal_demo_tenants', $now, $now,
		));
		$contentId = (int) $pdo->lastInsertId();
	}

	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array('control/portal/epc_platform_health_checkup'));
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

	echo "Menu item id: {$itemId}\n";
	echo "Content id: {$contentId}\n";
	echo "Super CP: /cp/control/portal/epc_demo_tenants_manage\n";

	if (!empty($_GET['clp_pass'])) {
		$clpPass = trim((string) $_GET['clp_pass']);
		$configPath = __DIR__ . '/config.demo-clp.php';
		$content = "<?php\n\$epc_demo_clp_pass = " . var_export($clpPass, true) . ";\n";
		if (file_put_contents($configPath, $content) !== false) {
			echo "Saved config.demo-clp.php for automated DB provisioning\n";
		}
	}
}

echo "\nEndpoints:\n";
echo "  POST /epc-demo-provision-api.php?token=...\n";
echo "  GET  /epc-demo-status.php?email=...&token=...\n";
echo "  GET  /epc-demo-expire-cron.php?token=...\n";
echo "  GET  /platform/demo (AI wizard)\n";
echo "\nActive demos: " . epc_portal_demo_count_active($pdo) . '/' . epc_portal_demo_max_active() . "\n";
echo "Done.\n";
