<?php
/**
 * Register Portal CP group: Industry Settings + deploy panel.
 * Run: https://www.epartscart.com/epc-portal-cp-setup.php?token=epartscart-deploy-2026
 */
header('Content-Type: text/plain; charset=utf-8');
error_reporting(E_ALL);
ini_set('display_errors', '1');
require_once __DIR__ . '/epc_deploy_auth.php';
if (($_GET['token'] ?? '') !== epc_deploy_token()) {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
try {
	$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
	$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);
} catch (Exception $e) {
	exit('DB connect failed: ' . $e->getMessage() . "\n");
}

epc_portal_db_ensure($pdo);

function epc_ps_lang($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_ps_lang($pdo, 'epc_cp_group_portal', 'Portal', 'Портал');
epc_ps_lang($pdo, 'epc_portal_industry_settings', 'Industry settings', 'Настройки отрасли');
epc_ps_lang($pdo, 'epc_portal_hub', 'Portal', 'Портал');

$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
$settingsUrl = '/<backend>/control/portal/industry_settings';
$settingsItem = epc_cp_mm_ensure_item($pdo, $portalGroup, 'epc_portal_industry_settings', $settingsUrl, 1, '#0d9488', 'fas fa-sliders-h', 1);

$controlParent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$controlParent->execute(array('control/config'));
$controlRow = $controlParent->fetch(PDO::FETCH_ASSOC);
if (!$controlRow) {
	$controlParent->execute(array('control'));
	$controlRow = $controlParent->fetch(PDO::FETCH_ASSOC);
}
if (!$controlRow) {
	exit("Parent control not found\n");
}
$controlParentId = (int) $controlRow['id'];
$portalLevel = (int) $controlRow['level'] + 1;
$now = time();

$portalHubUrl = 'control/portal';
$hubExisting = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$hubExisting->execute(array($portalHubUrl));
$portalHubId = (int) $hubExisting->fetchColumn();
if ($portalHubId > 0) {
	$pdo->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content_type` = \'text\', `content` = \'\', `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
	)->execute(array('Portal', 'epc_portal_hub', $controlParentId, $portalLevel, 'portal', $portalHubId));
} else {
	$pdo->prepare(
		'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'text\', \'\', ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 2)'
	)->execute(array(
		$portalHubUrl, $portalLevel, 'portal', 'epc_portal_hub', $controlParentId,
		'Super CP portal guides and industry settings',
		'Portal', $now, $now,
	));
	$portalHubId = (int) $pdo->lastInsertId();
}

$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ref->execute(array('control/config_edit'));
$refId = (int) $ref->fetchColumn();
$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
$root = (int) $pdo->query('SELECT `id` FROM `groups` WHERE `for_backend` = 1 LIMIT 1')->fetchColumn();
$allGroups = array($root > 0 ? $root : 1);
$collect = function ($pid) use ($pdo, &$collect, &$allGroups) {
	$ch = $pdo->prepare('SELECT `id` FROM `groups` WHERE `parent` = ?');
	$ch->execute(array($pid));
	while ($r = $ch->fetch(PDO::FETCH_ASSOC)) {
		$allGroups[] = (int) $r['id'];
		$collect((int) $r['id']);
	}
};
if ($root > 0) {
	$collect($root);
}
if ($refId > 0 && $portalHubId > 0) {
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($portalHubId));
	$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
	$groups->execute(array($refId));
	while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
		try {
			$ins->execute(array($portalHubId, (int) $g['group_id']));
		} catch (Exception $e) {
		}
	}
	foreach (array_unique($allGroups) as $gid) {
		try {
			$ins->execute(array($portalHubId, (int) $gid));
		} catch (Exception $e) {
		}
	}
}

$controlUrl = 'control';
$controlExisting = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$controlExisting->execute(array($controlUrl));
$controlNodeId = (int) $controlExisting->fetchColumn();
if ($controlNodeId <= 0) {
	$pdo->prepare(
		'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, 1, ?, ?, 0, ?, 0, \'text\', \'\', ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 1)'
	)->execute(array(
		$controlUrl, 'control', '743', 'Control panel breadcrumb anchor',
		'Control panel', $now, $now,
	));
	$controlNodeId = (int) $pdo->lastInsertId();
	if ($root > 0) {
		foreach (array_unique($allGroups) as $gid) {
			try {
				$ins->execute(array($controlNodeId, (int) $gid));
			} catch (Exception $e) {
			}
		}
	}
}

$parentUrl = 'control/portal/industry_settings';
$parentId = $portalHubId;
$level = $portalLevel + 1;
$phpPath = '/<backend_dir>/content/control/portal/industry_settings.php';

$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$existing->execute(array($parentUrl));
$contentId = (int) $existing->fetchColumn();

if ($contentId > 0) {
	$pdo->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ? WHERE `id` = ?'
	)->execute(array($phpPath, 'Industry settings', 'epc_portal_industry_settings', $parentId, $level, $contentId));
} else {
	$pdo->prepare(
		'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 3)'
	)->execute(array(
		$parentUrl, $level, 'industry_settings', 'epc_portal_industry_settings', $parentId,
		'Multi-industry portal settings and one-click deploy',
		$phpPath, 'Industry settings', $now, $now,
	));
	$contentId = (int) $pdo->lastInsertId();
}

$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ref->execute(array('control/config_edit'));
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

// Reparent portal guide pages under control/portal (fixes breadcrumb "404 Not found").
$reparent = $pdo->prepare(
	'UPDATE `content` SET `parent` = ?, `level` = ?
	 WHERE `url` LIKE ? AND `is_frontend` = 0 AND `url` != ?'
);
$reparent->execute(array($portalHubId, $portalLevel + 1, 'control/portal/%', $portalHubUrl));

$host = epc_portal_host();
if ($host === '') {
	$host = 'www.epartscart.com';
}
$defaults = epc_portal_default_site_settings($host);
epc_portal_save_site_settings($pdo, $defaults);

echo "Portal hub content id: {$portalHubId}\n";
echo "Portal CP group id: {$portalGroup}\n";
echo "Industry settings item id: {$settingsItem}\n";
echo "Content id: {$contentId}\n";
echo "URL: /cp/control/portal/industry_settings\n";
echo "Done.\n";
