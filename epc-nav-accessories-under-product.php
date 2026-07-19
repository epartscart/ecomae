<?php
/**
 * Move Accessories under Product dropdown only + rename AI nav label.
 *
 * Run:
 *   https://www.epartscart.com/epc-nav-accessories-under-product.php?token=...
 *   https://www.epartscart.com/epc-nav-accessories-under-product.php?token=...&dump=1
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

function epc_nav_label(PDO $pdo, $key)
{
	if ($key === '' || $key === null) {
		return '';
	}
	$stmt = $pdo->prepare('SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ? LIMIT 1');
	$stmt->execute(array($key, 'en'));
	$v = $stmt->fetchColumn();
	return $v !== false ? (string) $v : (string) $key;
}

function epc_nav_is_accessories(array $item, $accContentIds)
{
	if (isset($item['a_innerhtml']) && $item['a_innerhtml'] === 'epc_menu_accessories') {
		return true;
	}
	if (isset($item['value']) && $item['value'] === 'epc_menu_accessories') {
		return true;
	}
	$cid = isset($item['content_id']) ? (int) $item['content_id'] : 0;
	if ($cid && in_array($cid, $accContentIds, true)) {
		return true;
	}
	$href = isset($item['href']) ? (string) $item['href'] : '';
	if ($href !== '' && (strpos($href, 'accessories-spare-parts') !== false || preg_match('#/(en/)?accessories/?$#', $href))) {
		return true;
	}
	return false;
}

function epc_nav_item_label_key(array $item)
{
	if (!empty($item['a_innerhtml'])) {
		return (string) $item['a_innerhtml'];
	}
	if (!empty($item['value'])) {
		return (string) $item['value'];
	}
	return '';
}

function epc_nav_is_product_parent(PDO $pdo, array $item)
{
	$key = epc_nav_item_label_key($item);
	$label = strtolower(trim(epc_nav_label($pdo, $key)));
	if ($label === 'product') {
		return true;
	}
	// Fallback: dropdown that already holds Product Family as a child.
	if (!empty($item['data']) && is_array($item['data'])) {
		foreach ($item['data'] as $child) {
			if (!is_array($child)) {
				continue;
			}
			$ck = epc_nav_item_label_key($child);
			if ($ck === 'epc_menu_product_family') {
				return true;
			}
			$cl = strtolower(trim(epc_nav_label($pdo, $ck)));
			if ($cl === 'product family') {
				return true;
			}
		}
	}
	return false;
}

function epc_nav_acc_child_item($captionKey, $id, $contentId)
{
	return array(
		'value' => $captionKey,
		'class_li' => '',
		'class_ul' => '',
		'class_a' => '',
		'id_li' => '',
		'id_ul' => '',
		'id_a' => '',
		'a_innerhtml_mode' => 'auto',
		'a_innerhtml' => $captionKey,
		'link_mode' => $contentId ? 'content' : 'url',
		'content_id' => $contentId,
		'href' => '',
		'target' => '',
		'onclick' => '',
		'img_src' => '',
		'$count' => 0,
		'$level' => 2,
		'$parent' => 0,
		'id' => $id,
	);
}

function epc_nav_dump(PDO $pdo, array $structure, $depth = 0)
{
	foreach ($structure as $i => $item) {
		if (!is_array($item)) {
			continue;
		}
		$key = epc_nav_item_label_key($item);
		$label = epc_nav_label($pdo, $key);
		$href = isset($item['href']) ? (string) $item['href'] : '';
		$cid = isset($item['content_id']) ? (int) $item['content_id'] : 0;
		echo str_repeat('  ', $depth) . "[{$i}] key={$key} label={$label} cid={$cid} href={$href}\n";
		if (!empty($item['data']) && is_array($item['data'])) {
			epc_nav_dump($pdo, $item['data'], $depth + 1);
		}
	}
}

$accContentIds = array();
foreach (array('accessories-spare-parts', 'accessories') as $url) {
	$stmt = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
	$stmt->execute(array($url));
	$id = (int) $stmt->fetchColumn();
	if ($id) {
		$accContentIds[] = $id;
	}
}
$accContentId = $accContentIds ? $accContentIds[0] : 0;

$aiLabel = 'Vehicle Parts intelligence AI';
$aiKeys = array(
	'epc_demand_intelligence_title' => $aiLabel,
	'epc_menu_demand_intelligence' => $aiLabel,
);
$tr = $pdo->prepare(
	'INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?)
	 ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)'
);
foreach ($aiKeys as $key => $en) {
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?)')->execute(array($key, 'en', $en));
	$tr->execute(array($key, 'en', $en));
	echo "label {$key} => {$en}\n";
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
		epc_nav_dump($pdo, $structure);
		continue;
	}

	$removedTop = array();
	$kept = array();
	foreach ($structure as $item) {
		if (!is_array($item)) {
			continue;
		}
		if (epc_nav_is_accessories($item, $accContentIds)) {
			$removedTop[] = $item;
			continue;
		}
		// Also strip accessories already nested under any dropdown so we re-place once under Product.
		if (!empty($item['data']) && is_array($item['data'])) {
			$newChildren = array();
			foreach ($item['data'] as $child) {
				if (is_array($child) && epc_nav_is_accessories($child, $accContentIds)) {
					$removedTop[] = $child;
					continue;
				}
				$newChildren[] = $child;
			}
			$item['data'] = $newChildren;
			if (isset($item['$count'])) {
				$item['$count'] = count($newChildren);
			}
		}
		$kept[] = $item;
	}
	$structure = $kept;

	$productIndex = null;
	foreach ($structure as $index => $item) {
		if (is_array($item) && epc_nav_is_product_parent($pdo, $item)) {
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

	$source = $removedTop ? $removedTop[0] : null;
	$childId = $source && isset($source['id']) ? (int) $source['id'] : (time() + 77);
	$childCid = $source && !empty($source['content_id']) ? (int) $source['content_id'] : $accContentId;
	if (!$childCid) {
		echo "FAIL menu {$menuId}: accessories content_id missing\n";
		continue;
	}
	$child = epc_nav_acc_child_item('epc_menu_accessories', $childId, $childCid);
	// Preserve any custom fields from the removed top-level item.
	if (is_array($source)) {
		foreach (array('class_li', 'class_ul', 'class_a', 'target', 'onclick', 'img_src') as $k) {
			if (isset($source[$k])) {
				$child[$k] = $source[$k];
			}
		}
	}

	$already = false;
	foreach ($structure[$productIndex]['data'] as $ci => $existing) {
		if (is_array($existing) && epc_nav_is_accessories($existing, $accContentIds)) {
			$structure[$productIndex]['data'][$ci] = $child;
			$already = true;
			break;
		}
	}
	if (!$already) {
		$structure[$productIndex]['data'][] = $child;
	}
	if (isset($structure[$productIndex]['$count'])) {
		$structure[$productIndex]['$count'] = count($structure[$productIndex]['data']);
	}

	$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(
		json_encode($structure, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
		$menuId,
	));
	echo "OK menu {$menuId}: Accessories under Product (removed_top=" . count($removedTop) . ")\n";
	echo "Product children:\n";
	foreach ($structure[$productIndex]['data'] as $ci => $childItem) {
		if (!is_array($childItem)) {
			continue;
		}
		$k = epc_nav_item_label_key($childItem);
		echo "  [{$ci}] " . epc_nav_label($pdo, $k) . " ({$k})\n";
	}
}

echo "Done.\n";
