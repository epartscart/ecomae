<?php
/**
 * One-time: Product Family catalog page + top menu link.
 * Run: https://www.epartscart.com/epc-product-family-setup.php?token=...
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_pf_tr($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$ins = $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)');
	$ins->execute(array($key, 'en', $en));
	$ins->execute(array($key, 'ru', $ru));
}

function epc_pf_content($pdo, $url, $alias, $titleKey, $descKey, $path, $modules)
{
	$now = time();
	$stmt = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
	$stmt->execute(array($url));
	$id = $stmt->fetchColumn();
	if ($id) {
		$pdo->prepare('UPDATE `content` SET `alias`=?, `value`=?, `description`=?, `content_type`="php", `content`=?, `title_tag`=?, `description_tag`=?, `keywords_tag`=?, `modules_array`=?, `published_flag`=1, `time_edited`=? WHERE `id`=?')
			->execute(array($alias, $titleKey, $descKey, $path, $titleKey, $descKey, $descKey, $modules, $now, $id));
		return (int)$id;
	}
	$maxOrder = (int)$pdo->query('SELECT COALESCE(MAX(`order`), 0) FROM `content` WHERE `is_frontend` = 1')->fetchColumn();
	$pdo->prepare('INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`) VALUES (0, ?, 1, ?, ?, 0, ?, 1, "php", ?, ?, ?, ?, "0", 0, ?, "", "", 0, 1, 0, ?, ?, ?)')
		->execute(array($url, $alias, $titleKey, $descKey, $path, $titleKey, $descKey, $descKey, $modules, $now, $now, $maxOrder + 1));
	return (int)$pdo->lastInsertId();
}

function epc_pf_menu_item($captionKey, $href, $id, $contentId = 0, $level = 1)
{
	return array(
		'value' => $captionKey,
		'class_li' => '', 'class_ul' => '', 'class_a' => '',
		'id_li' => '', 'id_ul' => '', 'id_a' => '',
		'a_innerhtml_mode' => 'auto',
		'a_innerhtml' => $captionKey,
		'link_mode' => $contentId ? 'content' : 'url',
		'content_id' => $contentId,
		'href' => $contentId ? '' : $href,
		'target' => '', 'onclick' => '',
		'img_src' => '',
		'$count' => 0,
		'$level' => $level,
		'$parent' => $level > 1 ? 1 : 0,
		'id' => $id,
	);
}

function epc_pf_href_match($item, array $needles)
{
	$href = isset($item['href']) ? (string)$item['href'] : '';
	foreach ($needles as $n) {
		if ($n !== '' && strpos($href, $n) !== false) {
			return true;
		}
	}
	return false;
}

epc_pf_tr($pdo, 'epc_product_family_title', 'Product family', 'Семейство товаров');
epc_pf_tr($pdo, 'epc_product_family_desc', 'Browse parts by product family, brand and article.', 'Подбор запчастей по семейству, бренду и артикулу.');
epc_pf_tr($pdo, 'epc_menu_product_family', 'Product Family', 'Семейства товаров');

$modules = '[1,22,32,34]';
$contentId = epc_pf_content(
	$pdo,
	'product-family',
	'product_family_catalog',
	'epc_product_family_title',
	'epc_product_family_desc',
	'/content/product_family_catalog.php',
	$modules
);

$menuIds = array();
$stmt = $pdo->query('SELECT `id` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id`');
while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
	if ((int)$row['id'] === 15) {
		$menuIds[] = (int)$row['id'];
	}
}
if (!$menuIds) {
	$first = $pdo->query('SELECT `id` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id` LIMIT 1')->fetchColumn();
	if ($first) {
		$menuIds[] = (int)$first;
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
	$nextId = time() + 50;
	$catalogDropdownIndex = null;

	foreach ($structure as $index => $item) {
		if ((isset($item['content_id']) && (int)$item['content_id'] === $contentId)
			|| (isset($item['a_innerhtml']) && $item['a_innerhtml'] === 'epc_menu_product_family')
			|| epc_pf_href_match($item, array('product-family'))) {
			$has = true;
			$structure[$index] = epc_pf_menu_item('epc_menu_product_family', '/product-family', isset($item['id']) ? (int)$item['id'] : $nextId++, $contentId);
			break;
		}
		if (isset($item['data']) && is_array($item['data'])) {
			foreach ($item['data'] as $child) {
				if (epc_pf_href_match($child, array('vehicle-catalog', 'umapi_catalog', 'demand-intelligence'))) {
					$catalogDropdownIndex = $index;
					break 2;
				}
			}
		}
	}

	if (!$has && $catalogDropdownIndex !== null) {
		foreach ($structure[$catalogDropdownIndex]['data'] as $ci => $child) {
			if ((isset($child['content_id']) && (int)$child['content_id'] === $contentId)
				|| (isset($child['a_innerhtml']) && $child['a_innerhtml'] === 'epc_menu_product_family')
				|| epc_pf_href_match($child, array('product-family'))) {
				$has = true;
				$structure[$catalogDropdownIndex]['data'][$ci] = epc_pf_menu_item('epc_menu_product_family', '/product-family', isset($child['id']) ? (int)$child['id'] : $nextId++, $contentId, 2);
				break;
			}
		}
		if (!$has) {
			$structure[$catalogDropdownIndex]['data'][] = epc_pf_menu_item('epc_menu_product_family', '/product-family', $nextId++, $contentId, 2);
			if (isset($structure[$catalogDropdownIndex]['$count'])) {
				$structure[$catalogDropdownIndex]['$count'] = count($structure[$catalogDropdownIndex]['data']);
			}
			$has = true;
		}
	}

	if (!$has) {
		$newItem = epc_pf_menu_item('epc_menu_product_family', '/product-family', $nextId++, $contentId);
		$insertAt = null;
		foreach ($structure as $index => $item) {
			if (epc_pf_href_match($item, array('vehicle-catalog', 'epc_menu_vehicle_catalog'))) {
				$insertAt = $index + 1;
				break;
			}
		}
		if ($insertAt !== null) {
			array_splice($structure, $insertAt, 0, array($newItem));
		} else {
			$structure[] = $newItem;
		}
		$has = true;
	}

	// Ensure Product Family sits immediately after Vehicle parts catalog (top-level items only).
	$pfItem = null;
	$reordered = array();
	$pfPlaced = false;
	$vehicleCid = (int)$pdo->query("SELECT `id` FROM `content` WHERE `is_frontend`=1 AND `url`='vehicle-catalog' LIMIT 1")->fetchColumn();
	foreach ($structure as $item) {
		if (!is_array($item)) {
			continue;
		}
		$isPf = (isset($item['content_id']) && (int)$item['content_id'] === $contentId)
			|| epc_pf_href_match($item, array('product-family', 'epc_menu_product_family'));
		if ($isPf) {
			if ($pfItem === null) {
				$pfItem = $item;
			}
			continue;
		}
		$reordered[] = $item;
		if ($pfItem && !$pfPlaced && ((isset($item['content_id']) && (int)$item['content_id'] === $vehicleCid) || epc_pf_href_match($item, array('vehicle-catalog', 'epc_menu_vehicle_catalog')))) {
			$reordered[] = $pfItem;
			$pfPlaced = true;
		}
	}
	if ($pfItem && !$pfPlaced) {
		$tmp = array();
		$placed = false;
		foreach ($reordered as $item) {
			$tmp[] = $item;
			if (!$placed && ((isset($item['content_id']) && (int)$item['content_id'] === $vehicleCid) || epc_pf_href_match($item, array('vehicle-catalog', 'epc_menu_vehicle_catalog')))) {
				$tmp[] = $pfItem;
				$placed = true;
			}
		}
		$reordered = $placed ? $tmp : array_merge($reordered, array($pfItem));
	}
	$structure = $reordered;

	$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(json_encode($structure), $menuId));
	$updated[] = $menuId;
}

$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));

echo "Product Family content_id={$contentId}\n";
echo "URL: /product-family (/en/product-family)\n";
echo "File: /content/product_family_catalog.php\n";
echo "API: /content/shop/docpart/ajax_epc_product_family.php\n";
echo "Menu updated: " . implode(',', $updated) . "\n";
echo "OK\n";
