<?php
/**
 * One-time: Accessories & Spare Parts marketplace page + top menu link.
 * Run: https://www.epartscart.com/epc-accessories-setup.php?token=epartscart-deploy-2026
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

function epc_acc_menu_item($captionKey, $href, $id, $contentId = 0)
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

function epc_acc_href_match($item, array $needles)
{
	$href = isset($item['href']) ? (string) $item['href'] : '';
	foreach ($needles as $n) {
		if ($n !== '' && (strpos($href, $n) !== false || (isset($item['a_innerhtml']) && $item['a_innerhtml'] === $n))) {
			return true;
		}
	}
	return false;
}

epc_acc_tr($pdo, 'epc_accessories_title', 'Accessories & Spare Parts', 'Аксессуары и запчасти');
epc_acc_tr($pdo, 'epc_accessories_desc', 'Browse UAE warehouse car accessories and spare parts by category, brand, price and region.', 'Каталог автоаксессуаров и запчастей со склада ОАЭ: категории, бренды, цены и регионы.');
epc_acc_tr($pdo, 'epc_menu_accessories', 'Accessories', 'Аксессуары');

$modules = '[1,22,32,34]';
$path = '/content/general_pages/epc_epartscart_accessories.php';
$contentId = epc_acc_content(
	$pdo,
	'accessories-spare-parts',
	'accessories_spare_parts',
	'epc_accessories_title',
	'epc_accessories_desc',
	$path,
	$modules
);
// Short alias URL
$contentIdShort = epc_acc_content(
	$pdo,
	'accessories',
	'accessories_hub',
	'epc_accessories_title',
	'epc_accessories_desc',
	$path,
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
	$nextId = time() + 77;
	foreach ($structure as $item) {
		if ((isset($item['content_id']) && ((int) $item['content_id'] === $contentId || (int) $item['content_id'] === $contentIdShort))
			|| (isset($item['a_innerhtml']) && $item['a_innerhtml'] === 'epc_menu_accessories')
			|| epc_acc_href_match($item, array('accessories-spare-parts', 'accessories'))) {
			$has = true;
			break;
		}
	}
	if (!$has) {
		// Insert near top after first item when possible.
		$item = epc_acc_menu_item('epc_menu_accessories', '/accessories-spare-parts', $nextId, $contentId);
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

echo "OK accessories content_id={$contentId} short_id={$contentIdShort} menus=" . implode(',', $updated) . "\n";
echo "URLs: /en/accessories-spare-parts and /en/accessories\n";
exit;
