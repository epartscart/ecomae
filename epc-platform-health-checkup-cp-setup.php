<?php
/**
 * Register Super CP → Portal → Platform health checkup.
 * Run: https://www.ecomae.com/epc-platform-health-checkup-cp-setup.php?token=epartscart-deploy-2026
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

function epc_phcs_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_phcs_lang($pdo, 'epc_portal_health_checkup', 'Platform health checkup', 'Проверка платформы');
$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
$itemUrl = '/<backend>/control/portal/epc_platform_health_checkup';
$itemId = epc_cp_mm_ensure_item($pdo, $portalGroup, 'epc_portal_health_checkup', $itemUrl, 6, '#0d9488', 'fas fa-heartbeat', 1);

$contentUrl = 'control/portal/epc_platform_health_checkup';
$phpPath = '/<backend_dir>/content/control/portal/epc_platform_health_checkup.php';
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
	)->execute(array($phpPath, 'epc_portal_health_checkup', 'epc_portal_health_checkup', $parentId, $level, 'epc_platform_health_checkup', $contentId));
} else {
	$pdo->prepare(
		'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 6)'
	)->execute(array(
		$contentUrl, $level, 'epc_platform_health_checkup', 'epc_portal_health_checkup', $parentId,
		'Platform health checkup — tenant URLs, SSL, nginx, backups, indexing',
		$phpPath, 'epc_portal_health_checkup', $now, $now,
	));
	$contentId = (int) $pdo->lastInsertId();
}

$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ref->execute(array('control/portal/epc_platform_failover_guide'));
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
echo "Super CP URL: /cp/control/portal/epc_platform_health_checkup\n";
echo "API: /epc-platform-health-checkup-api.php\n";
echo "Done.\n";
