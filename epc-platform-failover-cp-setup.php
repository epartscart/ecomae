<?php
/**
 * Register Super CP → Portal → Failover & splash guide.
 * Run: https://www.ecomae.com/epc-platform-failover-cp-setup.php?token=epartscart-deploy-2026
 */
declare(strict_types=1);

header('Content-Type: text/plain; charset=utf-8');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		exit('DB connect failed: ' . $e->getMessage() . "\n");
	}
}

epc_portal_db_ensure($pdo);

function epc_ffs_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_ffs_lang($pdo, 'epc_portal_failover_guide', 'Failover & splash', 'Резерв и splash');
$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
$itemUrl = '/<backend>/control/portal/epc_platform_failover_guide';
$itemId = epc_cp_mm_ensure_item($pdo, $portalGroup, 'epc_portal_failover_guide', $itemUrl, 5, '#7c3aed', 'fas fa-shield-alt', 1);

$contentUrl = 'control/portal/epc_platform_failover_guide';
$phpPath = '/<backend_dir>/content/control/portal/epc_platform_failover_guide.php';
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
	)->execute(array($phpPath, 'epc_portal_failover_guide', 'epc_portal_failover_guide', $parentId, $level, 'epc_platform_failover_guide', $contentId));
} else {
	$pdo->prepare(
		'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 5)'
	)->execute(array(
		$contentUrl, $level, 'epc_platform_failover_guide', 'epc_portal_failover_guide', $parentId,
		'Cloud failover splash, status API, nginx error_page, jewellery demo',
		$phpPath, 'epc_portal_failover_guide', $now, $now,
	));
	$contentId = (int) $pdo->lastInsertId();
}

$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ref->execute(array('control/portal/industry_settings'));
$refId = (int) $ref->fetchColumn();
if ($refId <= 0) {
	$ref->execute(array('control/config_edit'));
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

$verify = $pdo->prepare('SELECT `id`, `published_flag` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 AND `published_flag` = 1 LIMIT 1');
$verify->execute(array($contentUrl));
$ok = $verify->fetch(PDO::FETCH_ASSOC);

echo 'db: ' . $cfg->db . "\n";
echo "Portal group id: {$portalGroup}\n";
echo "Menu item id: {$itemId}\n";
echo "Content id: {$contentId}\n";
echo 'Route verify: ' . ($ok ? 'ok id=' . $ok['id'] : 'MISSING') . "\n";
echo "Super CP URL: /cp/control/portal/epc_platform_failover_guide\n";
echo "Done.\n";
