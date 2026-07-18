<?php
/**
 * Register Website Tracker CP pages + schema (Super CP + tenant CP).
 * Run: https://www.ecomae.com/epc-web-tracker-setup.php?token=epartscart-deploy-2026
 *      https://www.epartscart.com/epc-web-tracker-setup.php?token=epartscart-deploy-2026
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
require_once __DIR__ . '/content/general_pages/epc_web_tracker.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

$pdo = epc_portal_platform_pdo();
if (!$pdo instanceof PDO) {
	try {
		$pdo = new PDO(
			'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4',
			$cfg->user,
			$cfg->password,
			array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
		);
	} catch (Exception $e) {
		exit('DB connect failed: ' . $e->getMessage() . "\n");
	}
}

epc_portal_db_ensure($pdo);
epc_web_tracker_ensure_schema($pdo);
echo "schema: epc_web_tracker_sessions / pageviews / events OK\n";

function epc_wt_setup_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

/**
 * @return array{item_id:int,content_id:int,ok:bool}
 */
function epc_wt_register_page(
	PDO $pdo,
	int $groupId,
	int $parentId,
	int $level,
	int $refId,
	string $langKey,
	string $labelEn,
	string $labelRu,
	string $contentUrl,
	string $alias,
	string $menuUrl,
	string $phpPath,
	string $description,
	int $order,
	string $color,
	string $icon,
	int $showAnyway = 1
): array {
	epc_wt_setup_lang($pdo, $langKey, $labelEn, $labelRu);
	$itemId = epc_cp_mm_ensure_item($pdo, $groupId, $langKey, $menuUrl, $order, $color, $icon, $showAnyway);
	$now = time();

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($contentUrl));
	$contentId = (int) $existing->fetchColumn();

	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ? WHERE `id` = ?'
		)->execute(array($phpPath, $langKey, $langKey, $parentId, $level, $alias, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array(
			$contentUrl, $level, $alias, $langKey, $parentId,
			$description,
			$phpPath, $langKey, $now, $now, $order,
		));
		$contentId = (int) $pdo->lastInsertId();
	}

	if ($contentId > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		$groupIds = array();
		if ($refId > 0) {
			$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
			$groups->execute(array($refId));
			while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
				$groupIds[] = (int) $g['group_id'];
			}
		}
		if ($groupIds === array()) {
			foreach (array('shop/statistika', 'control/config', 'control', 'control/portal/epc_power_bi') as $refUrl) {
				$rg = $pdo->prepare(
					'SELECT DISTINCT ca.`group_id` FROM `content_access` ca
					 INNER JOIN `content` c ON c.`id` = ca.`content_id`
					 WHERE c.`url` = ? AND c.`is_frontend` = 0'
				);
				$rg->execute(array($refUrl));
				while ($g = $rg->fetch(PDO::FETCH_ASSOC)) {
					$groupIds[] = (int) $g['group_id'];
				}
				if ($groupIds !== array()) {
					break;
				}
			}
		}
		$groupIds = array_values(array_unique(array_filter($groupIds)));
		foreach ($groupIds as $gid) {
			try {
				$ins->execute(array($contentId, $gid));
			} catch (Exception $e) {
			}
		}
	}

	$verify = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 AND `published_flag` = 1 LIMIT 1');
	$verify->execute(array($contentUrl));
	$ok = (bool) $verify->fetchColumn();

	return array('item_id' => $itemId, 'content_id' => $contentId, 'ok' => $ok);
}

$portalGroup = epc_cp_mm_ensure_group($pdo, 'epc_cp_group_portal', 'Portal', 'Портал', 2);
$shopGroup = epc_cp_mm_find_shop_group($pdo);
$shopGroupId = (int) ($shopGroup['id'] ?? 6);

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

$shopParent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$shopParent->execute(array('shop/statistika'));
$shopParentRow = $shopParent->fetch(PDO::FETCH_ASSOC);
if (!$shopParentRow) {
	$shopParent->execute(array('shop'));
	$shopParentRow = $shopParent->fetch(PDO::FETCH_ASSOC);
}
$shopParentId = $shopParentRow ? (int) $shopParentRow['id'] : $parentId;
$shopLevel = $shopParentRow ? ((int) $shopParentRow['level'] + 1) : $level;

$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ref->execute(array('shop/statistika'));
$refId = (int) $ref->fetchColumn();
if ($refId <= 0) {
	$ref->execute(array('control/portal/epc_power_bi'));
	$refId = (int) $ref->fetchColumn();
}

$portal = epc_wt_register_page(
	$pdo,
	$portalGroup,
	$parentId,
	$level,
	$refId,
	'epc_portal_web_tracker',
	'Website tracker',
	'Трекер сайта',
	'control/portal/epc_web_tracker',
	'epc_web_tracker',
	'/<backend>/control/portal/epc_web_tracker',
	'/<backend_dir>/content/control/portal/epc_web_tracker.php',
	'Website traffic — pageviews, clicks, search, geo, session timelines',
	12,
	'#0ea5e9',
	'fa-line-chart',
	1
);

$shop = epc_wt_register_page(
	$pdo,
	$shopGroupId,
	$shopParentId,
	$shopLevel,
	$refId,
	'epc_shop_web_tracker',
	'Website traffic',
	'Трафик сайта',
	'shop/statistics/web_tracker',
	'web_tracker',
	'/<backend>/shop/statistics/web_tracker',
	'/<backend_dir>/content/shop/statistics/web_tracker.php',
	'Tenant website traffic tracker',
	35,
	'#0284c7',
	'fa-area-chart',
	1
);

echo 'db: ' . $cfg->db . "\n";
echo 'Portal menu id: ' . $portal['item_id'] . ' content id: ' . $portal['content_id'] . ' verify: ' . ($portal['ok'] ? 'ok' : 'MISSING') . "\n";
echo 'Shop menu id: ' . $shop['item_id'] . ' content id: ' . $shop['content_id'] . ' verify: ' . ($shop['ok'] ? 'ok' : 'MISSING') . "\n";
echo "CP Super/Portal: /cp/control/portal/epc_web_tracker\n";
echo "CP Tenant/Shop:  /cp/shop/statistics/web_tracker\n";
echo "Collect:         /epc-web-tracker-collect.php\n";
echo "Done.\n";
