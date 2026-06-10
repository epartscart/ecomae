<?php
/**
 * Super CP enhancement setup — auth settings content + menu registration.
 * https://www.ecomae.com/epc-super-cp-enhance-setup.php?token=epartscart-deploy-2026&apply=1
 */
declare(strict_types=1);

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
header('Content-Type: text/plain; charset=utf-8');

$apply = !empty($_GET['apply']);

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	exit("Platform DB connect failed\n");
}

echo "=== ECOM AE Super CP Enhance Setup ===\n";
echo 'apply=' . ($apply ? 'yes' : 'no') . "\n\n";

$results = array();

if ($apply) {
	// Modern auth settings — menu + content record
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array('epc_cp_auth_settings', 'Modern CP auth'));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array('epc_cp_auth_settings', 'en', 'Modern auth'));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array('epc_cp_auth_settings', 'ru', 'Современный вход CP'));

	$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
	$itemUrl = '/<backend>/control/portal/epc_cp_auth_settings';
	$itemId = epc_cp_mm_ensure_item($pdo, $portalGroup, 'epc_cp_auth_settings', $itemUrl, 7, '#0d9488', 'fas fa-sign-in-alt', 1);
	$results['auth_menu_item_id'] = $itemId;

	$contentUrl = 'control/portal/epc_cp_auth_settings';
	$phpPath = '/<backend_dir>/content/control/portal/epc_cp_auth_settings.php';
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
		)->execute(array($phpPath, 'epc_cp_auth_settings', 'epc_cp_auth_settings', $parentId, $level, 'epc_cp_auth_settings', $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 7)'
		)->execute(array(
			$contentUrl, $level, 'epc_cp_auth_settings', 'epc_cp_auth_settings', $parentId,
			'Modern CP authentication — Google OAuth, email OTP toggles, SMTP tools',
			$phpPath, 'epc_cp_auth_settings', $now, $now,
		));
		$contentId = (int) $pdo->lastInsertId();
	}
	$results['auth_content_id'] = $contentId;

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

	// Ensure portal governance + tenant control content exist in menu
	epc_cp_super_platform_menu_apply($pdo);
	epc_cp_customer_mgmt_menu_apply($pdo);
	epc_cp_document_control_menu_apply($pdo);

	$authPhp = __DIR__ . '/cp/content/control/portal/epc_cp_auth_settings.php';
	$dashPhp = __DIR__ . '/cp/content/control/epc_super_cp_dashboard.php';
	$results['auth_php_on_disk'] = is_file($authPhp) ? 'yes' : 'no';
	$results['dashboard_php_on_disk'] = is_file($dashPhp) ? 'yes' : 'no';

	echo "Auth menu item: {$itemId}\n";
	echo "Auth content id: {$contentId}\n";
	echo "Auth PHP on disk: " . $results['auth_php_on_disk'] . "\n";
	echo "Dashboard PHP on disk: " . $results['dashboard_php_on_disk'] . "\n";
	echo "\nVerify:\n";
	echo "  https://www.ecomae.com/cp/\n";
	echo "  https://www.ecomae.com/cp/control/portal/epc_cp_auth_settings\n";
}

echo "\nDone.\n";
