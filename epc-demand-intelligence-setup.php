<?php
/*
 * One-time installer: Demand intelligence page route + top menu (Selection catalogs).
 * Run once: https://www.epartscart.com/epc-demand-intelligence-setup.php
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

function epc_di_translation($pdo, $key, $en, $ru)
{
	$stmt = $pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)');
	$stmt->execute(array($key, $en));
	$stmt = $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)');
	$stmt->execute(array($key, 'en', $en));
	$stmt->execute(array($key, 'ru', $ru));
}

function epc_di_content($pdo, $url, $alias, $titleKey, $descriptionKey, $contentPath, $modules)
{
	$now = time();
	$stmt = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
	$stmt->execute(array($url));
	$id = $stmt->fetchColumn();
	if ($id) {
		$upd = $pdo->prepare('UPDATE `content` SET `alias`=?, `value`=?, `description`=?, `content_type`="php", `content`=?, `title_tag`=?, `description_tag`=?, `keywords_tag`=?, `modules_array`=?, `published_flag`=1, `time_edited`=? WHERE `id`=?');
		$upd->execute(array($alias, $titleKey, $descriptionKey, $contentPath, $titleKey, $descriptionKey, $descriptionKey, $modules, $now, $id));
		return (int)$id;
	}
	$maxOrder = (int)$pdo->query('SELECT COALESCE(MAX(`order`), 0) FROM `content` WHERE `is_frontend` = 1')->fetchColumn();
	$ins = $pdo->prepare('INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`) VALUES (0, ?, 1, ?, ?, 0, ?, 1, "php", ?, ?, ?, ?, "0", 0, ?, "", "", 0, 1, 0, ?, ?, ?)');
	$ins->execute(array($url, $alias, $titleKey, $descriptionKey, $contentPath, $titleKey, $descriptionKey, $descriptionKey, $modules, $now, $now, $maxOrder + 1));
	return (int)$pdo->lastInsertId();
}

function epc_di_menu_item($captionKey, $href, $id, $contentId = 0)
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
		'href' => $contentId ? '' : $href,
		'target' => '',
		'onclick' => '',
		'img_src' => '',
		'$count' => 0,
		'$level' => 1,
		'$parent' => 0,
		'id' => $id,
	);
}

function epc_di_menu_item_child($captionKey, $href, $id, $contentId = 0)
{
	$item = epc_di_menu_item($captionKey, $href, $id, $contentId);
	$item['$level'] = 2;
	return $item;
}

function epc_di_href_matches($item, $needles)
{
	$href = isset($item['href']) ? (string)$item['href'] : '';
	foreach ($needles as $needle) {
		if ($needle !== '' && strpos($href, $needle) !== false) {
			return true;
		}
	}
	return false;
}

epc_di_translation($pdo, 'epc_demand_intelligence_title', 'Vehicle Parts intelligence AI', 'Страна — авто и запчасти (AI)');
epc_di_translation($pdo, 'epc_demand_intelligence_desc', 'Country demand tags, UAE stock, in-stock crosses and vehicle fitment for planning.', 'Теги спроса по странам, склад ОАЭ, кроссы в наличии и применимость по автомобилям.');
epc_di_translation($pdo, 'epc_menu_demand_intelligence', 'Vehicle Parts intelligence AI', 'Страна — авто и запчасти (AI)');

$commonModules = '[1,22,32,34]';
$demandContentId = epc_di_content(
	$pdo,
	'demand-intelligence',
	'demand_intelligence',
	'epc_demand_intelligence_title',
	'epc_demand_intelligence_desc',
	'/content/demand_intelligence.php',
	$commonModules
);

$targetMenuIds = array();
$stmt = $pdo->query('SELECT `id`, `structure` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id`');
while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
	if ((int)$row['id'] === 15) {
		$targetMenuIds[] = (int)$row['id'];
	}
}
if (empty($targetMenuIds)) {
	$first = $pdo->query('SELECT `id` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id` LIMIT 1')->fetchColumn();
	if ($first) {
		$targetMenuIds[] = (int)$first;
	}
}

$menuUpdated = array();
foreach ($targetMenuIds as $menuId) {
	$stmt = $pdo->prepare('SELECT `structure` FROM `menu` WHERE `id` = ?');
	$stmt->execute(array($menuId));
	$structure = json_decode($stmt->fetchColumn(), true);
	if (!is_array($structure)) {
		$structure = array();
	}

	$hasDemand = false;
	$nextId = time() + 30;

	foreach ($structure as $index => $item) {
		if ((isset($item['content_id']) && (int)$item['content_id'] === $demandContentId)
			|| (isset($item['a_innerhtml']) && $item['a_innerhtml'] === 'epc_menu_demand_intelligence')
			|| epc_di_href_matches($item, array('/demand-intelligence', 'demand-intelligence'))) {
			$hasDemand = true;
			$structure[$index] = epc_di_menu_item('epc_menu_demand_intelligence', '/demand-intelligence', isset($item['id']) ? (int)$item['id'] : $nextId++, $demandContentId);
			break;
		}
		if (isset($item['data']) && is_array($item['data'])) {
			$inCatalogDropdown = false;
			foreach ($item['data'] as $child) {
				if (epc_di_href_matches($child, array('umapi_catalog', 'vehicle-catalog', 'katalogi-ucats'))) {
					$inCatalogDropdown = true;
					break;
				}
			}
			if ($inCatalogDropdown) {
				foreach ($item['data'] as $childIndex => $child) {
					if ((isset($child['content_id']) && (int)$child['content_id'] === $demandContentId)
						|| (isset($child['a_innerhtml']) && $child['a_innerhtml'] === 'epc_menu_demand_intelligence')
						|| epc_di_href_matches($child, array('/demand-intelligence', 'demand-intelligence'))) {
						$hasDemand = true;
						$structure[$index]['data'][$childIndex] = epc_di_menu_item_child(
							'epc_menu_demand_intelligence',
							'/demand-intelligence',
							isset($child['id']) ? (int)$child['id'] : $nextId++,
							$demandContentId
						);
						break 2;
					}
				}
				if (!$hasDemand) {
					$structure[$index]['data'][] = epc_di_menu_item_child(
						'epc_menu_demand_intelligence',
						'/demand-intelligence',
						$nextId++,
						$demandContentId
					);
					if (isset($structure[$index]['$count'])) {
						$structure[$index]['$count'] = count($structure[$index]['data']);
					}
					$hasDemand = true;
					break;
				}
			}
		}
	}

	if (!$hasDemand) {
		$structure[] = epc_di_menu_item('epc_menu_demand_intelligence', '/demand-intelligence', $nextId++, $demandContentId);
	}

	$upd = $pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?');
	$upd->execute(array(json_encode($structure), $menuId));
	$menuUpdated[] = $menuId;
}

echo "Installed Vehicle intelligence content ID: " . $demandContentId . "\n";
echo "URL: /demand-intelligence (also /en/demand-intelligence)\n";
echo "Content file: /content/demand_intelligence.php\n";
echo "API: /content/shop/docpart/ajax_epc_demand_card.php\n";
echo "Updated menu IDs: " . implode(',', $menuUpdated) . "\n";
echo "OK\n";
