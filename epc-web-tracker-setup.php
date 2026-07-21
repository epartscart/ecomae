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
require_once __DIR__ . '/content/general_pages/epc_perf_cache.php';
require_once __DIR__ . '/epc_cp_mainstream_menu.php';

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

function epc_wt_setup_pdo_from_cfg(DP_Config $cfg): PDO
{
	$host = trim((string) $cfg->host);
	if ($host === '' || strtolower($host) === 'localhost') {
		$host = '127.0.0.1';
	}
	return new PDO(
		'mysql:host=' . $host . ';dbname=' . $cfg->db . ';charset=utf8mb4',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
}

// CP routing uses the host-applied DB (docpart on epartscart, ecomae on Super CP).
$pdoCp = null;
try {
	$pdoCp = epc_wt_setup_pdo_from_cfg($cfg);
} catch (Exception $e) {
	exit('CP DB connect failed: ' . $e->getMessage() . "\n");
}

$pdoPlatform = epc_portal_platform_pdo();
if (!$pdoPlatform instanceof PDO) {
	$pdoPlatform = $pdoCp;
}

// Schema on both (collect → platform; tenant CP may read either).
foreach (array('platform' => $pdoPlatform, 'cp' => $pdoCp) as $label => $pdoOne) {
	try {
		epc_web_tracker_ensure_schema($pdoOne);
		echo "schema[{$label}]: OK db=" . ($label === 'cp' ? $cfg->db : '(platform)') . "\n";
	} catch (Exception $e) {
		echo "schema[{$label}]: FAIL " . $e->getMessage() . "\n";
	}
}

// Menu + content must live in the CP-routed DB.
$pdo = $pdoCp;
epc_portal_db_ensure($pdo);
echo "registering CP content into db: {$cfg->db}\n";

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

$shopGroup = epc_cp_mm_find_shop_group($pdo);
$shopGroupId = (int) ($shopGroup['id'] ?? 6);

$shopParent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$shopParent->execute(array('shop/statistika'));
$shopParentRow = $shopParent->fetch(PDO::FETCH_ASSOC);
if (!$shopParentRow) {
	$shopParent->execute(array('shop'));
	$shopParentRow = $shopParent->fetch(PDO::FETCH_ASSOC);
}
if (!$shopParentRow) {
	exit("Shop parent content not found\n");
}
$shopParentId = (int) $shopParentRow['id'];
$shopLevel = (int) $shopParentRow['level'] + 1;

$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ref->execute(array('shop/statistika'));
$refId = (int) $ref->fetchColumn();
if ($refId <= 0) {
	$ref->execute(array('shop/statistics/statistics'));
	$refId = (int) $ref->fetchColumn();
}

// One menu item only — Shop → Web tracker (canonical).
$shop = epc_wt_register_page(
	$pdo,
	$shopGroupId,
	$shopParentId,
	$shopLevel,
	$refId,
	'epc_shop_web_tracker',
	'Web tracker',
	'Трекер сайта',
	'shop/statistics/web_tracker',
	'web_tracker',
	'/<backend>/shop/statistics/web_tracker',
	'/<backend_dir>/content/shop/statistics/web_tracker.php',
	'Website traffic — pageviews, clicks, search, geo, session timelines',
	35,
	'#0284c7',
	'fa-line-chart',
	1
);

// Remove duplicate Control/Portal menu item (keep content URL as redirect-only).
$portalRemoved = 0;
$stPortalItems = $pdo->query(
	"SELECT `id`, `url` FROM `control_items`
	 WHERE `url` LIKE '%/control/portal/epc_web_tracker%'
	    OR `url` = 'control/portal/epc_web_tracker'"
);
if ($stPortalItems) {
	$delById = $pdo->prepare('DELETE FROM `control_items` WHERE `id` = ?');
	while ($row = $stPortalItems->fetch(PDO::FETCH_ASSOC)) {
		$delById->execute(array((int) $row['id']));
		$portalRemoved += $delById->rowCount();
		echo 'removed menu: ' . $row['url'] . "\n";
	}
}

$cacheBusted = 0;
if (function_exists('epc_cp_menu_cache_bust')) {
	$cacheBusted = epc_cp_menu_cache_bust($pdo);
}
if (function_exists('epc_perf_cache_bust_prefix')) {
	$cacheBusted += epc_perf_cache_bust_prefix('epc_cp_menu_rows');
}

echo 'db: ' . $cfg->db . "\n";
echo 'Shop menu id: ' . $shop['item_id'] . ' content id: ' . $shop['content_id'] . ' verify: ' . ($shop['ok'] ? 'ok' : 'MISSING') . "\n";
echo 'Removed portal menu rows: ' . $portalRemoved . "\n";
echo 'Menu cache busted: ' . $cacheBusted . "\n";
echo "Canonical CP menu: /cp/shop/statistics/web_tracker (Web tracker)\n";
echo "Legacy /cp/control/portal/epc_web_tracker redirects to canonical.\n";
echo "Collect:         /epc-web-tracker-collect.php\n";
echo "Done.\n";
