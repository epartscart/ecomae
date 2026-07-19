<?php
/**
 * Remove Sell with us / Vendor registration from top menu.
 * Vendor access stays on the right: Vendor login/Register next to ERP + Customer auth.
 *
 * https://www.epartscart.com/epc-vendor-nav-fix.php?token=...
 */
error_reporting(E_ALL);
ini_set('display_errors', 0);
header('Content-Type: text/plain; charset=utf-8');

require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8mb4', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_vn_is_vendor_nav(array $item)
{
	$key = (string) ($item['a_innerhtml'] ?? $item['value'] ?? '');
	if (in_array($key, array('epc_menu_vendor_portal', 'epc_menu_vendor_register'), true)) {
		return true;
	}
	$href = (string) ($item['href'] ?? '');
	if ($href !== '' && preg_match('#/(en/)?vendor(/register)?/?$#', $href)) {
		return true;
	}
	$cid = isset($item['content_id']) ? (int) $item['content_id'] : 0;
	return false;
}

function epc_vn_strip(array $structure, array $vendorContentIds)
{
	$kept = array();
	foreach ($structure as $item) {
		if (!is_array($item)) {
			continue;
		}
		$cid = isset($item['content_id']) ? (int) $item['content_id'] : 0;
		if (epc_vn_is_vendor_nav($item) || ($cid && in_array($cid, $vendorContentIds, true))) {
			continue;
		}
		if (!empty($item['data']) && is_array($item['data'])) {
			$item['data'] = epc_vn_strip($item['data'], $vendorContentIds);
			if (isset($item['$count'])) {
				$item['$count'] = count($item['data']);
			}
		}
		$kept[] = $item;
	}
	return $kept;
}

$vendorContentIds = array();
foreach (array('vendor', 'vendor/register', 'vendor/upload') as $url) {
	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
	$st->execute(array($url));
	$id = (int) $st->fetchColumn();
	if ($id) {
		$vendorContentIds[] = $id;
	}
}

$menuIds = array(15);
$first = $pdo->query('SELECT `id` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id` LIMIT 1')->fetchColumn();
if ($first && !in_array((int) $first, $menuIds, true)) {
	$menuIds[] = (int) $first;
}

foreach ($menuIds as $menuId) {
	$st = $pdo->prepare('SELECT `structure` FROM `menu` WHERE `id` = ?');
	$st->execute(array($menuId));
	$raw = $st->fetchColumn();
	if ($raw === false) {
		continue;
	}
	$structure = json_decode((string) $raw, true);
	if (!is_array($structure)) {
		continue;
	}
	$before = count($structure);
	$structure = epc_vn_strip($structure, $vendorContentIds);
	$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(
		json_encode($structure, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
		$menuId,
	));
	echo "OK menu {$menuId}: removed vendor top-nav items ({$before} -> " . count($structure) . ")\n";
}

echo "Done. Use right-side Vendor login/Register instead of Sell with us.\n";
