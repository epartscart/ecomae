<?php
/**
 * One-time: Spare parts page (/spare-parts) + top menu link for ePartsCart.
 * Run: https://www.epartscart.com/epc-spare-parts-page-setup.php?token=epartscart-deploy-2026
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

$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_sp_tr($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$ins = $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)');
	$ins->execute(array($key, 'en', $en));
	$ins->execute(array($key, 'ru', $ru));
}

function epc_sp_content($pdo, $url, $alias, $titleKey, $descKey, $path, $modules)
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

function epc_sp_menu_item($captionKey, $href, $id, $contentId = 0)
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
		'$level' => 1,
		'$parent' => 0,
		'id' => $id,
	);
}

function epc_sp_href_match($item, array $needles)
{
	$href = isset($item['href']) ? (string) $item['href'] : '';
	foreach ($needles as $n) {
		if ($n !== '' && (strpos($href, $n) !== false || (isset($item['a_innerhtml']) && $item['a_innerhtml'] === $n))) {
			return true;
		}
	}
	return false;
}

epc_sp_tr($pdo, 'epc_spare_parts_title', 'Spare parts search', 'Поиск запчастей');
epc_sp_tr($pdo, 'epc_spare_parts_desc', 'Search UAE warehouse stock by brand and part number.', 'Поиск по бренду и артикулу на складе ОАЭ.');
epc_sp_tr($pdo, 'epc_menu_spare_parts', 'Spare Parts', 'Запчасти');

$modules = '[1,22,32,34]';
$contentId = epc_sp_content(
	$pdo,
	'spare-parts',
	'spare_parts',
	'epc_spare_parts_title',
	'epc_spare_parts_desc',
	'/content/general_pages/epc_epartscart_spare_parts.php',
	$modules
);

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
	$nextId = time() + 80;
	foreach ($structure as $index => $item) {
		if ((isset($item['content_id']) && (int) $item['content_id'] === $contentId)
			|| epc_sp_href_match($item, array('spare-parts', 'epc_menu_spare_parts'))) {
			$has = true;
			$structure[$index] = epc_sp_menu_item('epc_menu_spare_parts', '/spare-parts', isset($item['id']) ? (int) $item['id'] : $nextId++, $contentId);
			break;
		}
	}
	if (!$has) {
		$newItem = epc_sp_menu_item('epc_menu_spare_parts', '/spare-parts', $nextId++, $contentId);
		$insertAt = null;
		foreach ($structure as $index => $item) {
			if (epc_sp_href_match($item, array('/shop/cart', 'Cart'))) {
				$insertAt = $index;
				break;
			}
		}
		if ($insertAt !== null) {
			array_splice($structure, $insertAt, 0, array($newItem));
		} else {
			array_unshift($structure, $newItem);
		}
	}

	$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(json_encode($structure), $menuId));
	$updated[] = $menuId;
}

$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
$groups = $pdo->query('SELECT `id` FROM `groups`');
while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
	$pdo->prepare('INSERT IGNORE INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)')->execute(array($contentId, (int) $g['id']));
}

echo "Spare parts content_id={$contentId}\n";
echo "URL: /spare-parts (/en/spare-parts)\n";
echo "Page: /content/general_pages/epc_epartscart_spare_parts.php\n";
echo "API: /content/shop/epc_spare_parts_search.php\n";
echo "Menu updated: " . implode(',', $updated) . "\n";
echo "db=" . $cfg->db . "\n";
echo "OK\n";
