<?php
/**
 * Remove top-level "Product Family" from storefront nav.
 * Keep it only under the Product dropdown (already present).
 *
 * Run:
 *   https://www.epartscart.com/epc-nav-product-family-under-product.php?token=...
 *   https://www.epartscart.com/epc-nav-product-family-under-product.php?token=...&dump=1
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

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

function epc_pf_nav_label(PDO $pdo, $key)
{
	if ($key === '' || $key === null) {
		return '';
	}
	$stmt = $pdo->prepare('SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ? LIMIT 1');
	$stmt->execute(array($key, 'en'));
	$v = $stmt->fetchColumn();
	return $v !== false ? (string) $v : (string) $key;
}

function epc_pf_nav_key(array $item)
{
	if (!empty($item['a_innerhtml'])) {
		return (string) $item['a_innerhtml'];
	}
	if (!empty($item['value'])) {
		return (string) $item['value'];
	}
	return '';
}

function epc_pf_nav_is_product_family(PDO $pdo, array $item, array $pfContentIds)
{
	$key = epc_pf_nav_key($item);
	if ($key === 'epc_menu_product_family') {
		return true;
	}
	$cid = isset($item['content_id']) ? (int) $item['content_id'] : 0;
	if ($cid && in_array($cid, $pfContentIds, true)) {
		return true;
	}
	$href = isset($item['href']) ? (string) $item['href'] : '';
	if ($href !== '' && strpos($href, 'product-family') !== false) {
		return true;
	}
	$label = strtolower(trim(epc_pf_nav_label($pdo, $key)));
	return $label === 'product family';
}

function epc_pf_nav_is_product_parent(PDO $pdo, array $item)
{
	$key = epc_pf_nav_key($item);
	if ($key === 'epc_menu_product') {
		return true;
	}
	$label = strtolower(trim(epc_pf_nav_label($pdo, $key)));
	return $label === 'product';
}

function epc_pf_nav_child_item($id, $contentId)
{
	return array(
		'value' => 'epc_menu_product_family',
		'class_li' => '',
		'class_ul' => '',
		'class_a' => '',
		'id_li' => '',
		'id_ul' => '',
		'id_a' => '',
		'a_innerhtml_mode' => 'auto',
		'a_innerhtml' => 'epc_menu_product_family',
		'link_mode' => $contentId ? 'content' : 'url',
		'content_id' => $contentId,
		'href' => $contentId ? '' : '/product-family',
		'target' => '',
		'onclick' => '',
		'img_src' => '',
		'$count' => 0,
		'$level' => 2,
		'$parent' => 0,
		'id' => $id,
	);
}

function epc_pf_nav_dump(PDO $pdo, array $structure, $depth = 0)
{
	foreach ($structure as $i => $item) {
		if (!is_array($item)) {
			continue;
		}
		$key = epc_pf_nav_key($item);
		$label = epc_pf_nav_label($pdo, $key);
		$href = isset($item['href']) ? (string) $item['href'] : '';
		$cid = isset($item['content_id']) ? (int) $item['content_id'] : 0;
		echo str_repeat('  ', $depth) . "[{$i}] key={$key} label={$label} cid={$cid} href={$href}\n";
		if (!empty($item['data']) && is_array($item['data'])) {
			epc_pf_nav_dump($pdo, $item['data'], $depth + 1);
		}
	}
}

$pfContentIds = array();
$stmt = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
$stmt->execute(array('product-family'));
$pfId = (int) $stmt->fetchColumn();
if ($pfId) {
	$pfContentIds[] = $pfId;
}

$menuIds = array();
$stmt = $pdo->query('SELECT `id` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id`');
while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
	if ((int) $row['id'] === 15) {
		$menuIds[] = 15;
	}
}
if (!$menuIds) {
	$first = $pdo->query('SELECT `id` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id` LIMIT 1')->fetchColumn();
	if ($first) {
		$menuIds[] = (int) $first;
	}
}

$dump = isset($_GET['dump']) && $_GET['dump'] === '1';

foreach ($menuIds as $menuId) {
	$stmt = $pdo->prepare('SELECT `structure` FROM `menu` WHERE `id` = ?');
	$stmt->execute(array($menuId));
	$structure = json_decode((string) $stmt->fetchColumn(), true);
	if (!is_array($structure)) {
		$structure = array();
	}

	if ($dump) {
		echo "=== menu {$menuId} dump ===\n";
		epc_pf_nav_dump($pdo, $structure);
		continue;
	}

	$removedTop = array();
	$kept = array();
	foreach ($structure as $item) {
		if (!is_array($item)) {
			continue;
		}
		if (epc_pf_nav_is_product_family($pdo, $item, $pfContentIds)) {
			$removedTop[] = $item;
			continue;
		}
		$kept[] = $item;
	}
	$structure = $kept;

	$productIndex = null;
	foreach ($structure as $index => $item) {
		if (is_array($item) && epc_pf_nav_is_product_parent($pdo, $item)) {
			$productIndex = $index;
			break;
		}
	}
	if ($productIndex === null) {
		echo "FAIL menu {$menuId}: Product parent not found\n";
		continue;
	}

	if (!isset($structure[$productIndex]['data']) || !is_array($structure[$productIndex]['data'])) {
		$structure[$productIndex]['data'] = array();
	}

	$hasUnderProduct = false;
	foreach ($structure[$productIndex]['data'] as $child) {
		if (is_array($child) && epc_pf_nav_is_product_family($pdo, $child, $pfContentIds)) {
			$hasUnderProduct = true;
			break;
		}
	}

	if (!$hasUnderProduct) {
		$source = $removedTop ? $removedTop[0] : null;
		$childId = $source && isset($source['id']) ? (int) $source['id'] : (time() + 88);
		$childCid = $source && !empty($source['content_id']) ? (int) $source['content_id'] : $pfId;
		if (!$childCid) {
			echo "FAIL menu {$menuId}: product-family content_id missing\n";
			continue;
		}
		$structure[$productIndex]['data'][] = epc_pf_nav_child_item($childId, $childCid);
	}

	if (isset($structure[$productIndex]['$count'])) {
		$structure[$productIndex]['$count'] = count($structure[$productIndex]['data']);
	}

	$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(
		json_encode($structure, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
		$menuId,
	));
	echo "OK menu {$menuId}: Product Family under Product only (removed_top=" . count($removedTop) . ")\n";
	echo "Product children:\n";
	foreach ($structure[$productIndex]['data'] as $ci => $childItem) {
		if (!is_array($childItem)) {
			continue;
		}
		$k = epc_pf_nav_key($childItem);
		echo "  [{$ci}] " . epc_pf_nav_label($pdo, $k) . " ({$k})\n";
	}
}

echo "Done.\n";
