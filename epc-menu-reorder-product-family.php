<?php
/**
 * Top menu: Product Family directly after Vehicle parts catalog.
 * Run: https://www.epartscart.com/epc-menu-reorder-product-family.php
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

function epc_menu_matches(array $item, array $needles): bool
{
	$href = isset($item['href']) ? (string)$item['href'] : '';
	$inner = isset($item['a_innerhtml']) ? (string)$item['a_innerhtml'] : '';
	foreach ($needles as $n) {
		if ($n !== '' && (strpos($href, $n) !== false || $inner === $n)) {
			return true;
		}
	}
	return false;
}

function epc_menu_content_id(PDO $pdo, string $url): int
{
	$stmt = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
	$stmt->execute(array($url));
	return (int)$stmt->fetchColumn();
}

$vehicleContentId = epc_menu_content_id($pdo, 'vehicle-catalog');
$productFamilyContentId = epc_menu_content_id($pdo, 'product-family');

function epc_menu_is_vehicle(array $item, int $vehicleContentId): bool
{
	return ($vehicleContentId > 0 && isset($item['content_id']) && (int)$item['content_id'] === $vehicleContentId)
		|| epc_menu_matches($item, array('/vehicle-catalog', 'epc_menu_vehicle_catalog', 'vehicle-catalog'));
}

function epc_menu_is_product_family(array $item, int $productFamilyContentId): bool
{
	return ($productFamilyContentId > 0 && isset($item['content_id']) && (int)$item['content_id'] === $productFamilyContentId)
		|| epc_menu_matches($item, array('/product-family', 'epc_menu_product_family', 'product-family'));
}

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

foreach ($menuIds as $menuId) {
	$stmt = $pdo->prepare('SELECT `structure` FROM `menu` WHERE `id` = ?');
	$stmt->execute(array($menuId));
	$structure = json_decode($stmt->fetchColumn(), true);
	if (!is_array($structure)) {
		echo "Menu {$menuId}: invalid structure\n";
		continue;
	}

	$productFamilyItem = null;
	$final = array();
	$pfPlaced = false;

	foreach ($structure as $item) {
		if (!is_array($item)) {
			continue;
		}
		if (epc_menu_is_product_family($item, $productFamilyContentId)) {
			if ($productFamilyItem === null) {
				$productFamilyItem = $item;
			}
			continue;
		}
		$final[] = $item;
		if (epc_menu_is_vehicle($item, $vehicleContentId) && $productFamilyItem !== null && !$pfPlaced) {
			$final[] = $productFamilyItem;
			$pfPlaced = true;
		}
	}

	if ($productFamilyItem === null) {
		$productFamilyItem = array(
			'value' => 'epc_menu_product_family',
			'class_li' => '', 'class_ul' => '', 'class_a' => '',
			'id_li' => '', 'id_ul' => '', 'id_a' => '',
			'a_innerhtml_mode' => 'auto',
			'a_innerhtml' => 'epc_menu_product_family',
			'link_mode' => $productFamilyContentId ? 'content' : 'url',
			'content_id' => $productFamilyContentId,
			'href' => $productFamilyContentId ? '' : '/product-family',
			'target' => '', 'onclick' => '', 'img_src' => '',
			'$count' => 0, '$level' => 1, '$parent' => 0,
			'id' => time() + 77,
		);
	}

	if (!$pfPlaced) {
		$rebuilt = array();
		$placed = false;
		foreach ($final as $item) {
			$rebuilt[] = $item;
			if (!$placed && epc_menu_is_vehicle($item, $vehicleContentId)) {
				$rebuilt[] = $productFamilyItem;
				$placed = true;
			}
		}
		if (!$placed) {
			$rebuilt[] = $productFamilyItem;
		}
		$final = $rebuilt;
	}

	$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(json_encode($final), $menuId));

	$labels = array();
	foreach ($final as $item) {
		if (!is_array($item)) {
			continue;
		}
		$labels[] = isset($item['a_innerhtml']) ? (string)$item['a_innerhtml'] : (isset($item['href']) ? (string)$item['href'] : '?');
	}
	echo "Menu {$menuId}:\n  " . implode("\n  ", $labels) . "\n\n";
}

echo "OK — Product Family is directly after Vehicle parts catalog.\n";
