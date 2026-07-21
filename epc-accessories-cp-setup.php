<?php
/**
 * Register CP page: Accessories Marketplace (list / add / edit listings).
 *
 * Run:
 *   https://www.epartscart.com/epc-accessories-cp-setup.php?token=...
 *
 * Opens:
 *   https://www.epartscart.com/cp/shop/accessories
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/docpart/epc_accessories_db.php';

$cfg = new DP_Config();
$epcTenantHostDbFile = __DIR__ . '/config.tenant-host-db.php';
if (is_file($epcTenantHostDbFile)) {
	$epc_tenant_host_db = null;
	require $epcTenantHostDbFile;
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? 'www.epartscart.com'));
	if (strpos($host, ':') !== false) {
		$host = explode(':', $host, 2)[0];
	}
	if (isset($epc_tenant_host_db) && is_array($epc_tenant_host_db) && isset($epc_tenant_host_db[$host])) {
		foreach (array('db', 'user', 'password') as $epcTk) {
			if (!empty($epc_tenant_host_db[$host][$epcTk]) && property_exists($cfg, $epcTk)) {
				$cfg->$epcTk = $epc_tenant_host_db[$host][$epcTk];
			}
		}
	}
}

$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_acc_cp_lang(PDO $pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$ins = $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)');
	$ins->execute(array($key, 'en', $en));
	$ins->execute(array($key, 'ru', $ru));
}

epc_acc_cp_lang($pdo, 'epc_accessories_marketplace_cp', 'Accessories', 'Аксессуары');

epc_acc_ensure_schema($pdo);
$migrate = epc_acc_migrate_uae_locale($pdo);
echo 'uae_migrate=' . json_encode($migrate) . "\n";
$seedStats = epc_acc_seed_categories_from_json($pdo, false);

$mmHelpers = __DIR__ . '/epc_cp_mainstream_menu.php';
if (is_file($mmHelpers)) {
	require_once $mmHelpers;
}

$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$parent->execute(array('shop'));
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
	exit("Parent content shop not found\n");
}
$parentId = (int) $parentRow['id'];
$level = (int) $parentRow['level'] + 1;

$url = 'shop/accessories';
$phpPath = '/<backend_dir>/content/shop/accessories/accessories_listings.php';
$now = time();

$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$existing->execute(array($url));
$contentId = (int) $existing->fetchColumn();

if ($contentId > 0) {
	$pdo->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0, `system_flag` = 0,
		 `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = \'accessories_marketplace\', `url` = ?, `value` = ?, `description` = ?, `time_edited` = ?
		 WHERE `id` = ?'
	)->execute(array(
		$phpPath,
		'Accessories Marketplace — ePartsCart',
		$parentId,
		$level,
		$url,
		'epc_accessories_marketplace_cp',
		'Manage accessories & spare parts listings by PakWheels-style category',
		$now,
		$contentId,
	));
} else {
	$pdo->prepare(
		'INSERT INTO `content`
		(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 88)'
	)->execute(array(
		$url,
		$level,
		'accessories_marketplace',
		'epc_accessories_marketplace_cp',
		$parentId,
		'Manage accessories & spare parts listings by PakWheels-style category',
		$phpPath,
		'Accessories Marketplace — ePartsCart',
		$now,
		$now,
	));
	$contentId = (int) $pdo->lastInsertId();
}

// ACL: copy from shop/prices or shop/crosses
$refUrls = array('shop/prices', 'shop/crosses', 'shop/orders/orders');
$refId = 0;
foreach ($refUrls as $refUrl) {
	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array($refUrl));
	$refId = (int) $ref->fetchColumn();
	if ($refId > 0) {
		break;
	}
}
$aclGroups = 0;
if ($refId > 0 && $contentId > 0) {
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
	$groups->execute(array($refId));
	$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
		try {
			$ins->execute(array($contentId, (int) $g['group_id']));
			$aclGroups++;
		} catch (Exception $e) {
		}
	}
}

$itemsGroup = 6;
$menuOrder = 16;
if (function_exists('epc_cp_mm_find_shop_group')) {
	$shopGroup = epc_cp_mm_find_shop_group($pdo);
	$itemsGroup = (int) ($shopGroup['id'] ?? 6);
}
$pricesRef = $pdo->prepare('SELECT `items_group`, `order` FROM `control_items` WHERE `url` LIKE ? LIMIT 1');
$pricesRef->execute(array('%/shop/prices'));
$pricesRow = $pricesRef->fetch(PDO::FETCH_ASSOC);
if ($pricesRow) {
	if ((int) $pricesRow['items_group'] > 0) {
		$itemsGroup = (int) $pricesRow['items_group'];
	}
	$menuOrder = (int) $pricesRow['order'] + 3;
} else {
	$crossRef = $pdo->prepare('SELECT `items_group`, `order` FROM `control_items` WHERE `url` LIKE ? LIMIT 1');
	$crossRef->execute(array('%/shop/crosses'));
	$crossRow = $crossRef->fetch(PDO::FETCH_ASSOC);
	if ($crossRow) {
		if ((int) $crossRow['items_group'] > 0) {
			$itemsGroup = (int) $crossRow['items_group'];
		}
		$menuOrder = (int) $crossRow['order'] + 2;
	}
}

$controlUrl = '/<backend>/shop/accessories';
if (function_exists('epc_cp_mm_ensure_item')) {
	$controlId = epc_cp_mm_ensure_item(
		$pdo,
		$itemsGroup,
		'epc_accessories_marketplace_cp',
		$controlUrl,
		$menuOrder,
		'#dc2626',
		'fas fa-shopping-bag',
		1
	);
} else {
	$existingControl = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `url` = ? OR `url` LIKE ? LIMIT 1');
	$existingControl->execute(array($controlUrl, '%/shop/accessories'));
	$controlId = (int) $existingControl->fetchColumn();
	if ($controlId > 0) {
		$pdo->prepare(
			'UPDATE `control_items` SET `items_group` = ?, `caption` = ?, `url` = ?, `order` = ?, `background_color` = ?, `fontawesome_class` = ?, `show_anyway` = 1 WHERE `id` = ?'
		)->execute(array($itemsGroup, 'epc_accessories_marketplace_cp', $controlUrl, $menuOrder, '#dc2626', 'fas fa-shopping-bag', $controlId));
	} else {
		$pdo->prepare(
			'INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`)
			 VALUES (?, ?, ?, ?, ?, ?, ?, ?, 1)'
		)->execute(array($itemsGroup, 'epc_accessories_marketplace_cp', $controlUrl, '', $menuOrder, '#dc2626', 'fas fa-shopping-bag', ''));
		$controlId = (int) $pdo->lastInsertId();
	}
}

// Bust left-menu cache so Accessories appears immediately.
$perf = __DIR__ . '/content/general_pages/epc_perf_cache.php';
if (is_file($perf)) {
	require_once $perf;
	if (function_exists('epc_perf_cache_bust_prefix')) {
		epc_perf_cache_bust_prefix('epc_cp_menu_rows');
	}
}

$listingCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_acc_listings`')->fetchColumn();
$catCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_acc_categories`')->fetchColumn();

echo "OK accessories CP registered\n";
echo "content_id={$contentId}\n";
echo "control_items_id={$controlId}\n";
echo "acl_groups={$aclGroups}\n";
echo "menu_group={$itemsGroup} order={$menuOrder}\n";
echo "categories parents={$seedStats['parents']} children={$seedStats['children']} total_rows={$catCount}\n";
echo "listings={$listingCount}\n";
echo "URL: /" . $cfg->backend_dir . "/{$url}\n";
echo "Open: https://www.epartscart.com/" . $cfg->backend_dir . "/{$url}\n";
