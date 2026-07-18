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
 *   https://www.epartscart.com/epc-accessories-setup.php?token=...&action=seed_demo&per_sub=1
 *   https://www.epartscart.com/epc-accessories-setup.php?token=...&action=form
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

$action = isset($_GET['action']) ? trim((string) ($_REQUEST['action'] ?? '')) : '';

// --- Simple HTML form to add listings one category at a time ---
if ($action === 'form') {
	header('Content-Type: text/html; charset=utf-8');
	epc_acc_ensure_schema($pdo);
	$tree = epc_acc_get_category_tree($pdo);
	$tax = epc_acc_load_taxonomy_json();
	$makes = isset($tax['makes']) ? $tax['makes'] : array();
	$cities = isset($tax['cities']) ? $tax['cities'] : array();
	$token = htmlspecialchars((string) ($_GET['token'] ?? ''), ENT_QUOTES, 'UTF-8');
	echo '<!doctype html><html><head><meta charset="utf-8"><title>Add Accessories Listing</title>';
	echo '<style>body{font:15px/1.4 system-ui,sans-serif;max-width:720px;margin:24px auto;padding:0 16px}label{display:block;margin:12px 0 4px;font-weight:700}input,select,textarea{width:100%;padding:8px;border:1px solid #cbd5e1;border-radius:8px}button{margin-top:16px;padding:10px 16px;border:0;border-radius:8px;background:#dc2626;color:#fff;font-weight:800;cursor:pointer}.row{display:grid;grid-template-columns:1fr 1fr;gap:12px}</style></head><body>';
	echo '<h1>Add accessories listing</h1><p>Put data into a PakWheels-style category one by one.</p>';
	echo '<form method="post" action="?token=' . $token . '&action=add_listing">';
	echo '<input type="hidden" name="token" value="' . $token . '" />';
	echo '<label>Category</label><select name="category" id="cat" required><option value="">Select…</option>';
	foreach ($tree as $p) {
		echo '<option value="' . htmlspecialchars($p['slug'], ENT_QUOTES, 'UTF-8') . '">' . htmlspecialchars($p['label'], ENT_QUOTES, 'UTF-8') . '</option>';
	}
	echo '</select><label>Sub category</label><select name="subcategory" id="sub"><option value="">Optional…</option></select>';
	echo '<label>Title</label><input name="title" required maxlength="255" />';
	echo '<label>Description</label><textarea name="description" rows="3"></textarea>';
	echo '<div class="row"><div><label>Make</label><select name="make"><option value="">—</option>';
	foreach ($makes as $m) {
		echo '<option>' . htmlspecialchars($m, ENT_QUOTES, 'UTF-8') . '</option>';
	}
	echo '</select></div><div><label>Model</label><input name="model" /></div></div>';
	echo '<div class="row"><div><label>Year</label><input name="year" placeholder="e.g. 2018" /></div><div><label>Condition</label><select name="condition"><option value="new">New</option><option value="used">Used</option></select></div></div>';
	echo '<div class="row"><div><label>City</label><select name="city"><option value="">—</option>';
	foreach ($cities as $c) {
		echo '<option>' . htmlspecialchars($c, ENT_QUOTES, 'UTF-8') . '</option>';
	}
	echo '</select></div><div><label>Featured</label><select name="featured"><option value="0">No</option><option value="1">Yes</option></select></div></div>';
	echo '<div class="row"><div><label>Price</label><input name="price" type="number" step="1" min="0" /></div><div><label>Compare price</label><input name="compare_price" type="number" step="1" min="0" /></div></div>';
	echo '<div class="row"><div><label>Currency</label><input name="currency" value="PKR" /></div><div><label>Photo count</label><input name="photo_count" type="number" min="1" value="1" /></div></div>';
	echo '<label>Image URL</label><input name="image_url" />';
	echo '<label>External / detail URL</label><input name="external_url" />';
	echo '<button type="submit">Publish listing</button></form>';
	$treeJson = json_encode($tree, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	echo '<script>var tree=' . $treeJson . ';var cat=document.getElementById("cat");var sub=document.getElementById("sub");function fill(){sub.innerHTML="<option value=\\"\\">Optional…</option>";var slug=cat.value;for(var i=0;i<tree.length;i++){if(tree[i].slug===slug){(tree[i].children||[]).forEach(function(c){var o=document.createElement("option");o.value=c.slug;o.textContent=c.label;sub.appendChild(o);});break;}}}cat.addEventListener("change",fill);</script>';
	echo '</body></html>';
	exit;
}

// --- Seed one demo listing per subcategory (fill categories) ---
if ($action === 'seed_demo') {
	epc_acc_ensure_schema($pdo);
	epc_acc_seed_categories_from_json($pdo, false);
	$perSub = max(1, min(3, (int) ($_GET['per_sub'] ?? 1)));
	$clear = isset($_GET['clear']) && $_GET['clear'] === '1';
	if ($clear) {
		$pdo->exec('DELETE FROM `epc_acc_listings`');
	}
	$tax = epc_acc_load_taxonomy_json();
	$makes = isset($tax['makes']) && is_array($tax['makes']) ? $tax['makes'] : array('Toyota', 'Honda', 'Suzuki');
	$cities = isset($tax['cities']) && is_array($tax['cities']) ? $tax['cities'] : array('Karachi', 'Lahore', 'Islamabad');
	$modelsByMake = array(
		'Toyota' => array('Corolla', 'Yaris', 'Fortuner', 'Hilux', 'Vitz'),
		'Honda' => array('Civic', 'City', 'BR-V', 'Vezel', 'N Wgn'),
		'Suzuki' => array('Alto', 'Cultus', 'Wagon R', 'Swift', 'Every'),
		'Daihatsu' => array('Mira', 'Cuore', 'Move', 'Hijet'),
		'Nissan' => array('Dayz', 'Sunny', 'Clipper', 'Note'),
		'Hyundai' => array('Tucson', 'Elantra', 'Santro', 'Sonata'),
		'KIA' => array('Sportage', 'Picanto', 'Stonic', 'Sorento'),
		'Mitsubishi' => array('Lancer', 'Pajero', 'Minicab'),
		'Changan' => array('Alsvin', 'Karvaan', 'Oshan X7'),
		'Mercedes Benz' => array('C Class', 'E Class', 'GLA'),
		'Haval' => array('H6', 'Jolion'),
		'Audi' => array('A3', 'A4', 'Q5'),
		'MG' => array('HS', 'ZS', '5'),
		'BMW' => array('3 Series', '5 Series', 'X1'),
		'Lexus' => array('RX', 'NX', 'ES'),
	);
	$years = array('2016', '2017', '2018', '2019', '2020', '2021', '2022', '2023', '2024');
	$tree = epc_acc_get_category_tree($pdo);
	$added = 0;
	$mi = 0;
	$ci = 0;
	$prices = array(799, 1199, 1499, 2499, 3999, 5499, 8999, 12999, 19999, 34999, 55999, 89999);
	foreach ($tree as $parent) {
		$children = !empty($parent['children']) ? $parent['children'] : array(array('id' => 0, 'slug' => '', 'label' => $parent['label']));
		foreach ($children as $child) {
			for ($n = 0; $n < $perSub; $n++) {
				$make = $makes[$mi % count($makes)];
				$city = $cities[$ci % count($cities)];
				$modelList = isset($modelsByMake[$make]) ? $modelsByMake[$make] : array('Universal');
				$model = $modelList[($added + $n) % count($modelList)];
				$year = $years[($added + $n) % count($years)];
				$mi++;
				$ci++;
				$price = $prices[($added + $n) % count($prices)];
				$compare = (($added + $n) % 3 === 0) ? (int) round($price * 1.18) : 0;
				$cond = (($added + $n) % 5 === 0) ? 'used' : 'new';
				$featured = (($added + $n) % 11 === 0) ? 1 : 0;
				$photos = 1 + (($added + $n) % 8);
				$subLabel = !empty($child['label']) ? $child['label'] : $parent['label'];
				// PakWheels-like ad title: "Dash Cover for Toyota Corolla - 2018 | Karachi"
				$title = $subLabel . ' for ' . $make . ' ' . $model . ' - ' . $year . ' | ' . $city;
				epc_acc_add_listing($pdo, array(
					'category_id' => (int) $parent['id'],
					'subcategory_id' => (int) ($child['id'] ?? 0),
					'title' => $title,
					'description' => $subLabel . ' for ' . $make . ' ' . $model . ' (' . $year . '). Listed under ' . $parent['label'] . '. Replace with real photos and seller details when ready.',
					'make' => $make,
					'model' => $model,
					'year' => $year,
					'city' => $city,
					'condition_type' => $cond,
					'price' => $price,
					'compare_price' => $compare,
					'currency' => 'PKR',
					'image_url' => '',
					'external_url' => '/en/accessories-spare-parts?category=' . rawurlencode($parent['slug']) . '&subcategory=' . rawurlencode((string) ($child['slug'] ?? '')),
					'photo_count' => $photos,
					'featured' => $featured,
					'stock_qty' => 1 + (($added + $n) % 12),
					'status' => 'published',
				));
				$added++;
			}
		}
	}
	$total = (int) $pdo->query('SELECT COUNT(*) FROM `epc_acc_listings`')->fetchColumn();
	echo "OK seed_demo added={$added} listings_total={$total} per_sub={$perSub}\n";
	exit;
}

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
		'year' => (string) ($_REQUEST['year'] ?? ''),
		'city' => (string) ($_REQUEST['city'] ?? ''),
		'condition_type' => (string) ($_REQUEST['condition'] ?? 'new'),
		'price' => (float) ($_REQUEST['price'] ?? 0),
		'compare_price' => (float) ($_REQUEST['compare_price'] ?? 0),
		'currency' => (string) ($_REQUEST['currency'] ?? 'PKR'),
		'image_url' => (string) ($_REQUEST['image_url'] ?? ''),
		'external_url' => (string) ($_REQUEST['external_url'] ?? ''),
		'photo_count' => (int) ($_REQUEST['photo_count'] ?? 1),
		'featured' => !empty($_REQUEST['featured']) ? 1 : 0,
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
