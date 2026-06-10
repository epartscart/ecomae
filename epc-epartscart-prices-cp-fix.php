<?php
/**
 * eParts Cart — enable auto-parts price lists in CP (packs, routes, menu, access).
 * Run on tenant host: https://www.epartscart.com/epc-epartscart-prices-cp-fix.php?token=epartscart-deploy-2026&key=TECH_KEY
 * Apply: add &apply=1
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Forbidden')));
}

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
require_once __DIR__ . '/content/general_pages/epc_portal_db.php';

$hostname = strtolower(trim((string)($_SERVER['HTTP_HOST'] ?? 'www.epartscart.com')));
if ($hostname === '') {
	$hostname = 'www.epartscart.com';
}
$bare = preg_replace('/^www\./', '', $hostname);
if (strpos($hostname, 'www.') !== 0 && strpos($hostname, '.') !== false) {
	$hostname = 'www.' . $bare;
}
$_SERVER['HTTP_HOST'] = $hostname;

$cfg = new DP_Config();
epc_portal_apply_config($cfg);

if ((string)($_GET['key'] ?? '') !== $cfg->tech_key) {
	http_response_code(403);
	exit(json_encode(array('ok' => false, 'error' => 'Invalid key')));
}

$apply = !empty($_GET['apply']);
$report = array(
	'ok' => true,
	'hostname' => $hostname,
	'db' => $cfg->db,
	'apply' => $apply,
	'changes' => array(),
	'checks' => array(),
);

try {
	$pdo = new PDO(
		'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
		$cfg->user,
		$cfg->password,
		array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
	);
} catch (Throwable $e) {
	exit(json_encode(array('ok' => false, 'error' => 'DB: ' . $e->getMessage()), JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT));
}

epc_portal_db_ensure($pdo);

$requiredPacks = array('core', 'commerce', 'auto_parts', 'logistics', 'erp', 'professional', 'marketing');
$settings = epc_portal_load_site_settings($pdo);
$packsBefore = isset($settings['enabled_packs']) ? $settings['enabled_packs'] : array();
$accessBefore = isset($settings['access_mode']) ? $settings['access_mode'] : '';

$report['checks']['packs_before'] = $packsBefore;
$report['checks']['access_mode_before'] = $accessBefore;
$report['checks']['resolved_access_before'] = epc_portal_resolve_access_mode($settings);
$report['checks']['prices_visible_before'] = epc_portal_cp_item_visible('/cp/shop/prices');

if ($apply) {
	$mergedPacks = array_values(array_unique(array_merge($packsBefore, $requiredPacks)));
	$cpMenu = isset($settings['cp_menu']) && is_array($settings['cp_menu']) ? $settings['cp_menu'] : epc_portal_cp_menu_defaults();
	$save = array(
		'host' => $hostname,
		'industry_code' => 'auto_parts',
		'access_mode' => 'full',
		'enabled_packs' => $mergedPacks,
		'system_name' => $settings['system_name'] ?? 'eParts Cart',
		'hub_name' => $settings['hub_name'] ?? 'Electronic World Group',
		'tagline' => $settings['tagline'] ?? 'Auto parts & commerce',
		'domain_path' => $settings['domain_path'] ?? ('https://' . $hostname . '/'),
		'contact' => $settings['contact'] ?? array(),
		'theme_template' => $settings['theme_template'] ?? 'classic',
		'theme' => $settings['theme'] ?? array(),
		'cp_menu' => $cpMenu,
		'erp_modules' => $settings['erp_modules'] ?? array(),
	);
	epc_portal_save_site_settings($pdo, $save);
	$report['changes'][] = 'site_settings: industry=auto_parts access_mode=full packs=' . implode(',', $mergedPacks);
}

$settingsAfter = epc_portal_load_site_settings($pdo);
$report['checks']['packs_after'] = $settingsAfter['enabled_packs'] ?? array();
$report['checks']['access_mode_after'] = $settingsAfter['access_mode'] ?? '';
$report['checks']['resolved_access_after'] = epc_portal_resolve_access_mode($settingsAfter);
$report['checks']['prices_visible_after'] = epc_portal_cp_item_visible('/cp/shop/prices');

if (!$report['checks']['prices_visible_after']) {
	$report['ok'] = false;
}

function epc_epc_prices_sync_access(PDO $pdo, int $contentId, array $refUrls): int
{
	if ($contentId <= 0) {
		return 0;
	}
	$refIds = array();
	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	foreach ($refUrls as $url) {
		$ref->execute(array($url));
		$rid = (int)$ref->fetchColumn();
		if ($rid > 0) {
			$refIds[] = $rid;
		}
	}
	$groups = array();
	if (!empty($refIds)) {
		$ph = implode(',', array_fill(0, count($refIds), '?'));
		$gq = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` IN (' . $ph . ')');
		$gq->execute($refIds);
		while ($g = $gq->fetchColumn()) {
			$groups[(int)$g] = true;
		}
	}
	if (!$groups) {
		$groups[1] = true;
		$groups[3] = true;
	}
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$ins = $pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	$n = 0;
	foreach (array_keys($groups) as $gid) {
		$ins->execute(array($contentId, (int)$gid));
		$n++;
	}
	return $n;
}

function epc_epc_prices_ensure_content(PDO $pdo, string $parentUrl, string $url, string $alias, string $phpPath, string $title, int $order = 79): int
{
	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array($parentUrl));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		return 0;
	}
	$parentId = (int)$parentRow['id'];
	$level = (int)$parentRow['level'] + 1;
	$now = time();

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($url));
	$contentId = (int)$existing->fetchColumn();

	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0, `system_flag` = 0,
			 `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = ?, `url` = ?, `time_edited` = ?
			 WHERE `id` = ?'
		)->execute(array($phpPath, $title, $parentId, $level, $alias, $url, $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, ?)'
		)->execute(array(
			$url, $level, $alias, $title, $parentId, $title, $phpPath, $title, $now, $now, $order,
		));
		$contentId = (int)$pdo->lastInsertId();
	}

	if ($contentId > 0) {
		epc_epc_prices_sync_access($pdo, $contentId, array('shop/prices', 'shop/orders/orders', 'shop'));
	}
	return $contentId;
}

$routes = array();
$contentChecks = array(
	'shop/prices' => '/<backend_dir>/content/shop/prices_upload/prices_manager.php',
	'shop/prices/guide' => '/<backend_dir>/content/shop/prices_upload/prices_guide_page.php',
	'shop/prices/prices_edit' => '/<backend_dir>/content/shop/prices_edit/prices.php',
);

foreach ($contentChecks as $url => $phpPath) {
	$st = $pdo->prepare('SELECT `id`, `url`, `published_flag`, `content`, `content_type` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array($url));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$routes[$url] = $row ?: null;
}

$parentMissing = empty($routes['shop/prices']);
if ($parentMissing && $apply) {
	$shopParent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$shopParent->execute(array('shop'));
	$shopRow = $shopParent->fetch(PDO::FETCH_ASSOC);
	if ($shopRow) {
		$now = time();
		$pdo->prepare(
			'INSERT INTO `content`
			(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 1, 1, 1, ?, ?, 79)'
		)->execute(array(
			'shop/prices', (int)$shopRow['level'] + 1, 'prices', 'Price lists', (int)$shopRow['id'],
			'Supplier price lists', $contentChecks['shop/prices'], 'Price lists — eParts Cart', $now, $now,
		));
		$cid = (int)$pdo->lastInsertId();
		epc_epc_prices_sync_access($pdo, $cid, array('shop/orders/orders', 'shop'));
		$report['changes'][] = 'content: created shop/prices id=' . $cid;
	}
}

if ($apply) {
	$mainId = epc_epc_prices_ensure_content(
		$pdo,
		'shop',
		'shop/prices',
		'prices',
		$contentChecks['shop/prices'],
		'Price lists — eParts Cart',
		79
	);
	if ($mainId > 0) {
		$report['changes'][] = 'content: shop/prices id=' . $mainId;
	}
	$guideId = epc_epc_prices_ensure_content(
		$pdo,
		'shop/prices',
		'shop/prices/guide',
		'guide',
		$contentChecks['shop/prices/guide'],
		'Price upload guide — eParts Cart',
		79
	);
	if ($guideId > 0) {
		$report['changes'][] = 'content: shop/prices/guide id=' . $guideId;
	}
	$editId = epc_epc_prices_ensure_content(
		$pdo,
		'shop/prices',
		'shop/prices/prices_edit',
		'prices_edit',
		$contentChecks['shop/prices/prices_edit'],
		'Edit price list records — eParts Cart',
		96
	);
	if ($editId > 0) {
		$report['changes'][] = 'content: shop/prices/prices_edit id=' . $editId;
	}
}

foreach ($contentChecks as $url => $phpPath) {
	$st = $pdo->prepare('SELECT `id`, `published_flag`, `content` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$st->execute(array($url));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	$ok = $row && (int)$row['published_flag'] === 1 && strpos((string)$row['content'], 'prices') !== false;
	$report['checks']['route_' . str_replace('/', '_', $url)] = array(
		'ok' => $ok,
		'id' => $row ? (int)$row['id'] : 0,
		'content' => $row ? (string)$row['content'] : null,
	);
	if (!$ok) {
		$report['ok'] = false;
	}
}

$docRoot = $_SERVER['DOCUMENT_ROOT'];
$files = array(
	'prices_manager' => $docRoot . '/cp/content/shop/prices_upload/prices_manager.php',
	'prices_guide' => $docRoot . '/cp/content/shop/prices_upload/prices_guide_page.php',
	'prices_edit' => $docRoot . '/cp/content/shop/prices_edit/prices.php',
	'access_control' => $docRoot . '/cp/plugins/access_control/control.php',
);
foreach ($files as $key => $path) {
	$report['checks']['file_' . $key] = is_file($path);
	if (!is_file($path)) {
		$report['ok'] = false;
	}
}

$menuUrl = '/<backend>/shop/prices';
$menu = $pdo->prepare('SELECT `id`, `items_group`, `url`, `order` FROM `control_items` WHERE `url` = ? OR `url` LIKE ? LIMIT 1');
$menu->execute(array($menuUrl, '%/shop/prices'));
$menuRow = $menu->fetch(PDO::FETCH_ASSOC);
$report['checks']['menu_item'] = $menuRow ?: null;

if ($apply && !$menuRow) {
	$shopGroup = $pdo->query("SELECT `id` FROM `control_groups` WHERE `caption` = '744' LIMIT 1")->fetch(PDO::FETCH_ASSOC);
	if (!$shopGroup) {
		$shopGroup = $pdo->query(
			'SELECT g.`id` FROM `control_groups` g
			 INNER JOIN `control_items` i ON i.`items_group` = g.`id`
			 WHERE i.`url` LIKE \'%<backend>/shop/%\'
			 GROUP BY g.`id` ORDER BY COUNT(*) DESC LIMIT 1'
		)->fetch(PDO::FETCH_ASSOC);
	}
	if ($shopGroup) {
		$gid = (int)$shopGroup['id'];
		$pdo->prepare(
			'INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`)
			 VALUES (?, \'771\', ?, \'\', 13, \'#26ad5f\', \'fas fa-file-excel\', \'\', 0)'
		)->execute(array($gid, $menuUrl));
		$report['changes'][] = 'menu: created control_items shop/prices in group ' . $gid;
		$menuRow = array('id' => (int)$pdo->lastInsertId(), 'items_group' => $gid, 'url' => $menuUrl);
		$report['checks']['menu_item'] = $menuRow;
	}
}

$acPath = $files['access_control'];
$acSnippet = is_file($acPath) ? file_get_contents($acPath) : '';
$report['checks']['access_control_empty_allow'] = strpos($acSnippet, 'count($allowed_groups) === 0') !== false;

$report['cp_url'] = 'https://' . $hostname . '/' . $cfg->backend_dir . '/shop/prices';
$report['hint'] = $apply
	? 'Applied. Log in to CP and open /cp/shop/prices for the price list UI.'
	: 'Dry run — pass apply=1 to write DB.';

echo json_encode($report, JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);
