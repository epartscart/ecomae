<?php
/**
 * Register CP page shop/demand_countries (Demand countries CSV upload).
 * Run once: https://www.epartscart.com/epc-demand-countries-cp-setup.php?token=epartscart-deploy-2026
 */
error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

if (!isset($_GET['token']) || $_GET['token'] !== 'epartscart-deploy-2026') {
	exit("Forbidden\n");
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_dc_lang($pdo, $key, $en, $ru)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

epc_dc_lang($pdo, 'epc_demand_countries_cp', 'Demand countries CSV', 'Спрос по странам (CSV)');

$parentUrl = 'shop';
$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$parent->execute(array($parentUrl));
$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
if (!$parentRow) {
	exit("Parent content shop not found\n");
}
$parentId = (int)$parentRow['id'];
$level = (int)$parentRow['level'] + 1;

$url = 'shop/demand_countries';
$phpPath = '/<backend_dir>/content/shop/demand_countries/demand_countries.php';
$now = time();

$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$existing->execute(array($url));
$contentId = (int)$existing->fetchColumn();

if ($contentId > 0) {
	$pdo->prepare(
		'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `is_frontend` = 0, `system_flag` = 0,
		 `content` = ?, `title_tag` = ?, `parent` = ?, `level` = ?, `alias` = \'demand_countries\', `url` = ?, `value` = ?
		 WHERE `id` = ?'
	)->execute(array(
		$phpPath,
		'Demand countries CSV — eParts Cart',
		$parentId,
		$level,
		$url,
		'epc_demand_countries_cp',
		$contentId,
	));
} else {
	$pdo->prepare(
		'INSERT INTO `content`
		(`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
		 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
		 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
		 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 85)'
	)->execute(array(
		$url,
		$level,
		'demand_countries',
		'epc_demand_countries_cp',
		$parentId,
		'Upload brand+article demand tags (ISO3 country codes)',
		$phpPath,
		'Demand countries CSV — eParts Cart',
		$now,
		$now,
	));
	$contentId = (int)$pdo->lastInsertId();
}

$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
$ref->execute(array('shop/crosses'));
$refId = (int)$ref->fetchColumn();
if ($refId > 0 && $contentId > 0) {
	$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
	$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
	$groups->execute(array($refId));
	$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
	while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
		try {
			$ins->execute(array($contentId, (int)$g['group_id']));
		} catch (Exception $e) {
		}
	}
}

require_once __DIR__ . '/content/shop/docpart/epc_demand_intelligence.php';
epc_demand_ensure_schema($pdo);

$itemsGroup = 6;
$crossRef = $pdo->prepare('SELECT `items_group`, `order` FROM `control_items` WHERE `url` LIKE ? LIMIT 1');
$crossRef->execute(array('%/shop/crosses'));
$crossRow = $crossRef->fetch(PDO::FETCH_ASSOC);
if ($crossRow) {
	$itemsGroup = (int)$crossRow['items_group'];
	if ($itemsGroup <= 0) {
		$itemsGroup = 6;
	}
	$menuOrder = (int)$crossRow['order'] + 1;
} else {
	$pricesRef = $pdo->prepare('SELECT `items_group`, `order` FROM `control_items` WHERE `url` LIKE ? LIMIT 1');
	$pricesRef->execute(array('%/shop/prices'));
	$pricesRow = $pricesRef->fetch(PDO::FETCH_ASSOC);
	if ($pricesRow) {
		$itemsGroup = (int)$pricesRow['items_group'];
		if ($itemsGroup <= 0) {
			$itemsGroup = 6;
		}
		$menuOrder = (int)$pricesRow['order'] + 1;
	} else {
		$menuOrder = 706;
	}
}

$controlUrl = '/<backend>/shop/demand_countries';
$existingControl = $pdo->prepare('SELECT `id` FROM `control_items` WHERE `url` = ? LIMIT 1');
$existingControl->execute(array($controlUrl));
$controlId = (int)$existingControl->fetchColumn();
if ($controlId > 0) {
	$pdo->prepare(
		'UPDATE `control_items` SET `items_group` = ?, `caption` = ?, `order` = ?, `background_color` = ?, `fontawesome_class` = ? WHERE `id` = ?'
	)->execute(array($itemsGroup, 'epc_demand_countries_cp', $menuOrder, '#2980b9', 'fas fa-globe-africa', $controlId));
} else {
	$pdo->prepare(
		'INSERT INTO `control_items` (`items_group`, `caption`, `url`, `img`, `order`, `background_color`, `fontawesome_class`, `target`, `show_anyway`)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, 0)'
	)->execute(array($itemsGroup, 'epc_demand_countries_cp', $controlUrl, '', $menuOrder, '#2980b9', 'fas fa-globe-africa', ''));
	$controlId = (int)$pdo->lastInsertId();
}

echo "Demand countries CP page registered.\n";
echo "content_id: {$contentId}\n";
echo "control_items_id: {$controlId}\n";
echo "CP menu: Shop group {$itemsGroup}, order {$menuOrder}\n";
echo "URL: /" . $cfg->backend_dir . "/{$url}\n";
echo "Schema migrated to ISO3 if needed.\n";
