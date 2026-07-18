<?php
/**
 * Accessories marketplace setup:
 * - Registers /accessories-spare-parts + /accessories pages and top menu
 * - Seeds PakWheels-crawled categories into epc_acc_categories
 * - Optional: add a listing into a category
 *
 * Run:
 *   https://www.epartscart.com/epc-accessories-setup.php?token=...
 *   https://www.epartscart.com/epc-accessories-setup.php?token=...&seed=1&reset=1
 *   https://www.epartscart.com/epc-accessories-setup.php?token=...&action=add_listing&category=car-care&subcategory=car-top-covers&title=...&price=1199&make=Toyota&city=Karachi&condition=new
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

function epc_acc_tr($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$ins = $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)');
	$ins->execute(array($key, 'en', $en));
	$ins->execute(array($key, 'ru', $ru));
}

function epc_acc_content($pdo, $url, $alias, $titleKey, $descKey, $path, $modules)
{
	$now = time();
	$stmt = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
	$stmt->execute(array($url));
	$id = $stmt->fetchColumn();
	if ($id) {
		$pdo->prepare('UPDATE `content` SET `alias`=?, `value`=?, `description`=?, `content_type`="php", `content`=?, `title_tag`=?, `description_tag`=?, `keywords_tag`=?, `modules_array`=?, `published_flag`=1, `time_edited`=? WHERE `id`=?')
			->execute(array($alias, $titleKey, $descKey, $path, $titleKey, $descKey, $descKey, $modules, $now, $id));
		return (int) $id;
	}
	$maxOrder = (int) $pdo->query('SELECT COALESCE(MAX(`order`), 0) FROM `content` WHERE `is_frontend` = 1')->fetchColumn();
	$pdo->prepare('INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`) VALUES (0, ?, 1, ?, ?, 0, ?, 1, "php", ?, ?, ?, ?, "0", 0, ?, "", "", 0, 1, 0, ?, ?, ?)')
		->execute(array($url, $alias, $titleKey, $descKey, $path, $titleKey, $descKey, $descKey, $modules, $now, $now, $maxOrder + 1));
	return (int) $pdo->lastInsertId();
}

function epc_acc_menu_item($captionKey, $id, $contentId = 0)
{
	return array(
		'value' => $captionKey,
		'class_li' => '', 'class_ul' => '', 'class_a' => '',
		'id_li' => '', 'id_ul' => '', 'id_a' => '',
		'a_innerhtml_mode' => 'auto',
		'a_innerhtml' => $captionKey,
		'link_mode' => $contentId ? 'content' : 'url',
		'content_id' => $contentId,
		'href' => '',
		'target' => '', 'onclick' => '',
		'img_src' => '',
		'$count' => 0,
		'$level' => 1,
		'$parent' => 0,
		'id' => $id,
	);
}

function epc_acc_find_cat_id(PDO $pdo, $slug, $parentId = 0)
{
	$stmt = $pdo->prepare('SELECT `id` FROM `epc_acc_categories` WHERE `slug` = ? AND `parent_id` = ? LIMIT 1');
	$stmt->execute(array($slug, (int) $parentId));
	return (int) $stmt->fetchColumn();
}

$action = isset($_GET['action']) ? trim((string) $_GET['action']) : '';

// --- Add listing one-by-one into a crawled category ---
if ($action === 'add_listing') {
	epc_acc_ensure_schema($pdo);
	epc_acc_seed_categories_from_json($pdo, false);
	$catSlug = trim((string) ($_REQUEST['category'] ?? ''));
	$subSlug = trim((string) ($_REQUEST['subcategory'] ?? ''));
	$title = trim((string) ($_REQUEST['title'] ?? ''));
	if ($catSlug === '' || $title === '') {
		exit("category and title required\n");
	}
	$catId = epc_acc_find_cat_id($pdo, $catSlug, 0);
	if ($catId < 1) {
		exit("unknown category slug: {$catSlug}\n");
	}
	$subId = 0;
	if ($subSlug !== '') {
		$subId = epc_acc_find_cat_id($pdo, $subSlug, $catId);
		if ($subId < 1) {
			// allow subcategory slug unique lookup
			$stmt = $pdo->prepare('SELECT `id` FROM `epc_acc_categories` WHERE `slug` = ? AND `parent_id` > 0 LIMIT 1');
			$stmt->execute(array($subSlug));
			$subId = (int) $stmt->fetchColumn();
		}
	}
	$id = epc_acc_add_listing($pdo, array(
		'category_id' => $catId,
		'subcategory_id' => $subId,
		'title' => $title,
		'description' => (string) ($_REQUEST['description'] ?? ''),
		'make' => (string) ($_REQUEST['make'] ?? ''),
		'model' => (string) ($_REQUEST['model'] ?? ''),
		'city' => (string) ($_REQUEST['city'] ?? ''),
		'condition_type' => (string) ($_REQUEST['condition'] ?? 'new'),
		'price' => (float) ($_REQUEST['price'] ?? 0),
		'currency' => (string) ($_REQUEST['currency'] ?? 'PKR'),
		'image_url' => (string) ($_REQUEST['image_url'] ?? ''),
		'external_url' => (string) ($_REQUEST['external_url'] ?? ''),
		'stock_qty' => (int) ($_REQUEST['stock_qty'] ?? 0),
		'status' => (string) ($_REQUEST['status'] ?? 'published'),
	));
	echo "OK listing_id={$id} category={$catSlug} subcategory={$subSlug}\n";
	exit;
}

if ($action === 'list_categories') {
	epc_acc_ensure_schema($pdo);
	$tree = epc_acc_get_category_tree($pdo);
	foreach ($tree as $p) {
		echo $p['slug'] . "\t" . $p['label'] . "\t" . count($p['children']) . " subs\n";
		foreach ($p['children'] as $c) {
			echo "  - " . $c['slug'] . "\t" . $c['label'] . "\n";
		}
	}
	exit;
}

epc_acc_tr($pdo, 'epc_accessories_title', 'Accessories & Spare Parts', 'Аксессуары и запчасти');
epc_acc_tr($pdo, 'epc_accessories_desc', 'Browse car accessories and spare parts by PakWheels-style categories, make, city and price.', 'Каталог автоаксессуаров и запчастей по категориям, марке, городу и цене.');
epc_acc_tr($pdo, 'epc_menu_accessories', 'Accessories', 'Аксессуары');

$modules = '[1,22,32,34]';
$path = '/content/general_pages/epc_epartscart_accessories.php';
$contentId = epc_acc_content($pdo, 'accessories-spare-parts', 'accessories_spare_parts', 'epc_accessories_title', 'epc_accessories_desc', $path, $modules);
$contentIdShort = epc_acc_content($pdo, 'accessories', 'accessories_hub', 'epc_accessories_title', 'epc_accessories_desc', $path, $modules);

$menuIds = array();
$stmt = $pdo->query('SELECT `id` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id`');
while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
	if ((int) $row['id'] === 15) {
		$menuIds[] = (int) $row['id'];
	}
}
if (!$menuIds) {
	$first = $pdo->query('SELECT `id` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id` LIMIT 1')->fetchColumn();
	if ($first) {
		$menuIds[] = (int) $first;
	}
}

$updated = array();
foreach ($menuIds as $menuId) {
	$stmt = $pdo->prepare('SELECT `structure` FROM `menu` WHERE `id` = ?');
	$stmt->execute(array($menuId));
	$structure = json_decode($stmt->fetchColumn(), true);
	if (!is_array($structure)) {
		$structure = array();
	}
	$has = false;
	foreach ($structure as $item) {
		if ((isset($item['content_id']) && ((int) $item['content_id'] === $contentId || (int) $item['content_id'] === $contentIdShort))
			|| (isset($item['a_innerhtml']) && $item['a_innerhtml'] === 'epc_menu_accessories')) {
			$has = true;
			break;
		}
	}
	if (!$has) {
		$item = epc_acc_menu_item('epc_menu_accessories', time() + 77, $contentId);
		if (count($structure) > 1) {
			array_splice($structure, 1, 0, array($item));
		} else {
			$structure[] = $item;
		}
		$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(
			json_encode($structure, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
			$menuId,
		));
		$updated[] = $menuId;
	}
}

$reset = isset($_GET['reset']) && $_GET['reset'] === '1';
$seed = !isset($_GET['seed']) || $_GET['seed'] !== '0';
$seedStats = array('parents' => 0, 'children' => 0);
if ($seed) {
	$seedStats = epc_acc_seed_categories_from_json($pdo, $reset);
}

$listingCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_acc_listings`')->fetchColumn();
$catCount = (int) $pdo->query('SELECT COUNT(*) FROM `epc_acc_categories`')->fetchColumn();

echo "OK accessories content_id={$contentId} short_id={$contentIdShort} menus=" . implode(',', $updated) . "\n";
echo "categories_seeded parents={$seedStats['parents']} children={$seedStats['children']} total_rows={$catCount}\n";
echo "listings_count={$listingCount} (add with action=add_listing)\n";
echo "URLs: /en/accessories-spare-parts and /en/accessories\n";
echo "Example add:\n";
echo "  ?token=...&action=add_listing&category=car-care&subcategory=car-top-covers&title=Dashboard+Cover&price=1199&make=Toyota&city=Karachi&condition=new\n";
exit;
