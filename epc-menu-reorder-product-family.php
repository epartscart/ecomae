<?php
/**
 * Storefront menu: Product Family under Product only (no separate top-level item).
 * Run: https://www.epartscart.com/epc-menu-reorder-product-family.php?token=...
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
	$href = isset($item['href']) ? (string) $item['href'] : '';
	$inner = isset($item['a_innerhtml']) ? (string) $item['a_innerhtml'] : '';
	$value = isset($item['value']) ? (string) $item['value'] : '';
	foreach ($needles as $n) {
		if ($n !== '' && (strpos($href, $n) !== false || $inner === $n || $value === $n)) {
			return true;
		}
	}
	return false;
}

function epc_menu_content_id(PDO $pdo, string $url): int
{
	$stmt = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
	$stmt->execute(array($url));
	return (int) $stmt->fetchColumn();
}

function epc_menu_en_label(PDO $pdo, string $key): string
{
	if ($key === '') {
		return '';
	}
	$stmt = $pdo->prepare('SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ? LIMIT 1');
	$stmt->execute(array($key, 'en'));
	$v = $stmt->fetchColumn();
	return $v !== false ? (string) $v : $key;
}

$productFamilyContentId = epc_menu_content_id($pdo, 'product-family');

function epc_menu_is_product_family(array $item, int $productFamilyContentId): bool
{
	return ($productFamilyContentId > 0 && isset($item['content_id']) && (int) $item['content_id'] === $productFamilyContentId)
		|| epc_menu_matches($item, array('/product-family', 'epc_menu_product_family', 'product-family'));
}

function epc_menu_is_product_parent(PDO $pdo, array $item): bool
{
	$key = '';
	if (!empty($item['a_innerhtml'])) {
		$key = (string) $item['a_innerhtml'];
	} elseif (!empty($item['value'])) {
		$key = (string) $item['value'];
	}
	if ($key === 'epc_menu_product') {
		return true;
	}
	return strtolower(trim(epc_menu_en_label($pdo, $key))) === 'product';
}

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

foreach ($menuIds as $menuId) {
	$stmt = $pdo->prepare('SELECT `structure` FROM `menu` WHERE `id` = ?');
	$stmt->execute(array($menuId));
	$structure = json_decode((string) $stmt->fetchColumn(), true);
	if (!is_array($structure)) {
		echo "Menu {$menuId}: invalid structure\n";
		continue;
	}

	$productFamilyItem = null;
	$final = array();
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
	}
	$structure = $final;

	$productIndex = null;
	foreach ($structure as $index => $item) {
		if (is_array($item) && epc_menu_is_product_parent($pdo, $item)) {
			$productIndex = $index;
			break;
		}
	}
	if ($productIndex === null) {
		echo "Menu {$menuId}: Product parent not found\n";
		continue;
	}

	if (!isset($structure[$productIndex]['data']) || !is_array($structure[$productIndex]['data'])) {
		$structure[$productIndex]['data'] = array();
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
			'$count' => 0, '$level' => 2, '$parent' => 0,
			'id' => time() + 77,
		);
	} else {
		$productFamilyItem['$level'] = 2;
		$productFamilyItem['$parent'] = 1;
	}

	$hasUnder = false;
	foreach ($structure[$productIndex]['data'] as $ci => $child) {
		if (is_array($child) && epc_menu_is_product_family($child, $productFamilyContentId)) {
			$structure[$productIndex]['data'][$ci] = $productFamilyItem;
			$hasUnder = true;
			break;
		}
	}
	if (!$hasUnder) {
		array_unshift($structure[$productIndex]['data'], $productFamilyItem);
	}
	if (isset($structure[$productIndex]['$count'])) {
		$structure[$productIndex]['$count'] = count($structure[$productIndex]['data']);
	}

	$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(
		json_encode($structure, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
		$menuId,
	));
	echo "OK menu {$menuId}: Product Family under Product only\n";
}

echo "Done.\n";
