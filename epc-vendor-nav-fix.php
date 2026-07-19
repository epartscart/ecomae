<?php
/**
 * Ensure top nav shows Sell with us + Vendor registration (visible CTAs).
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

function epc_vn_lang(PDO $pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')
		->execute(array($key, $en));
	$ins = $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)');
	$ins->execute(array($key, 'en', $en));
	$ins->execute(array($key, 'ru', $ru));
}

function epc_vn_content_id(PDO $pdo, $url)
{
	$st = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
	$st->execute(array($url));
	return (int) $st->fetchColumn();
}

function epc_vn_item($captionKey, $contentId, $id, $level = 1, $classA = '', $classLi = '')
{
	return array(
		'value' => $captionKey,
		'class_li' => $classLi,
		'class_ul' => '',
		'class_a' => $classA,
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
		'$level' => (int) $level,
		'$parent' => 0,
		'id' => $id,
	);
}

function epc_vn_is_vendor_nav(array $item)
{
	$key = (string) ($item['a_innerhtml'] ?? $item['value'] ?? '');
	if (in_array($key, array('epc_menu_vendor_portal', 'epc_menu_vendor_register'), true)) {
		return true;
	}
	$href = (string) ($item['href'] ?? '');
	return (strpos($href, '/vendor') !== false);
}

epc_vn_lang($pdo, 'epc_menu_vendor_portal', 'Sell with us', 'Стать продавцом');
epc_vn_lang($pdo, 'epc_menu_vendor_register', 'Vendor registration', 'Регистрация продавца');

$portalId = epc_vn_content_id($pdo, 'vendor');
$registerId = epc_vn_content_id($pdo, 'vendor/register');
if ($portalId < 1 || $registerId < 1) {
	exit("FAIL missing content pages portal={$portalId} register={$registerId}\n");
}

// Ensure pages published.
$pdo->prepare('UPDATE `content` SET `published_flag` = 1 WHERE `id` IN (?, ?)')->execute(array($portalId, $registerId));

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

foreach ($menuIds as $menuId) {
	$st = $pdo->prepare('SELECT `structure` FROM `menu` WHERE `id` = ?');
	$st->execute(array($menuId));
	$structure = json_decode((string) $st->fetchColumn(), true);
	if (!is_array($structure)) {
		$structure = array();
	}

	$kept = array();
	foreach ($structure as $item) {
		if (!is_array($item)) {
			continue;
		}
		if (epc_vn_is_vendor_nav($item)) {
			continue;
		}
		if (!empty($item['data']) && is_array($item['data'])) {
			$children = array();
			foreach ($item['data'] as $child) {
				if (is_array($child) && epc_vn_is_vendor_nav($child)) {
					continue;
				}
				$children[] = $child;
			}
			$item['data'] = $children;
			if (isset($item['$count'])) {
				$item['$count'] = count($children);
			}
		}
		$kept[] = $item;
	}
	$structure = $kept;

	$sell = epc_vn_item('epc_menu_vendor_portal', $portalId, time() + 91, 1, 'epc-nav-sell-cta', 'epc-nav-sell-li');
	$reg = epc_vn_item('epc_menu_vendor_register', $registerId, time() + 92, 1, 'epc-nav-sell-cta epc-nav-register-cta', 'epc-nav-register-li');

	// Place immediately after Home (index 0) so they stay visible.
	$insertAt = 1;
	foreach ($structure as $i => $item) {
		$key = (string) ($item['a_innerhtml'] ?? $item['value'] ?? '');
		$href = (string) ($item['href'] ?? '');
		if ($key === '812' || $href === '/' || $href === '/en/' || $href === '/en') {
			$insertAt = $i + 1;
			break;
		}
	}
	array_splice($structure, $insertAt, 0, array($sell, $reg));

	$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(
		json_encode($structure, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES),
		$menuId,
	));
	echo "OK menu {$menuId}: Sell with us + Vendor registration after Home\n";
}

echo "Done.\n";
