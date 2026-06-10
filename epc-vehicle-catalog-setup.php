<?php
/*
 * One-time installer for Vehicle parts catalog page.
 * Registers /vehicle-catalog route and menu link under Selection catalogs.
 */

error_reporting(E_ALL);
ini_set('display_errors', 1);
header('Content-Type: text/plain; charset=utf-8');

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';

$cfg = new DP_Config();
$pdo = new PDO('mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8', $cfg->user, $cfg->password);
$pdo->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

function epc_vc_translation($pdo, $key, $en, $ru)
{
    $stmt = $pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)');
    $stmt->execute(array($key, $en));
    $stmt = $pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)');
    $stmt->execute(array($key, 'en', $en));
    $stmt->execute(array($key, 'ru', $ru));
}

function epc_vc_content($pdo, $url, $alias, $titleKey, $descriptionKey, $contentPath, $modules)
{
    $now = time();
    $stmt = $pdo->prepare('SELECT `id` FROM `content` WHERE `is_frontend` = 1 AND `url` = ? LIMIT 1');
    $stmt->execute(array($url));
    $id = $stmt->fetchColumn();
    if ($id) {
        $upd = $pdo->prepare('UPDATE `content` SET `alias`=?, `value`=?, `description`=?, `content_type`="php", `content`=?, `title_tag`=?, `description_tag`=?, `keywords_tag`=?, `modules_array`=?, `published_flag`=1, `time_edited`=? WHERE `id`=?');
        $upd->execute(array($alias, $titleKey, $descriptionKey, $contentPath, $titleKey, $descriptionKey, $descriptionKey, $modules, $now, $id));
        return (int) $id;
    }

    $maxOrder = (int) $pdo->query('SELECT COALESCE(MAX(`order`), 0) FROM `content` WHERE `is_frontend` = 1')->fetchColumn();
    $ins = $pdo->prepare('INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`, `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`, `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`) VALUES (0, ?, 1, ?, ?, 0, ?, 1, "php", ?, ?, ?, ?, "0", 0, ?, "", "", 0, 1, 0, ?, ?, ?)');
    $ins->execute(array($url, $alias, $titleKey, $descriptionKey, $contentPath, $titleKey, $descriptionKey, $descriptionKey, $modules, $now, $now, $maxOrder + 1));
    return (int) $pdo->lastInsertId();
}

function epc_vc_menu_item($captionKey, $href, $id, $contentId = 0)
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

epc_vc_translation($pdo, 'epc_vehicle_catalog_title', 'Vehicle parts catalog', 'Каталог запчастей по авто');
epc_vc_translation($pdo, 'epc_vehicle_catalog_desc', 'Browse parts by year, make, model and engine using the eparts catalog.', 'Подбор запчастей по году, марке, модели и двигателю через каталог UMAPI.');
epc_vc_translation($pdo, 'epc_menu_vehicle_catalog', 'Vehicle parts catalog', 'Каталог по авто');

$commonModules = '[1,22,32,34]';
$vehicleCatalogId = epc_vc_content(
    $pdo,
    'vehicle-catalog',
    'vehicle_catalog',
    'epc_vehicle_catalog_title',
    'epc_vehicle_catalog_desc',
    '/content/vehicle_catalog.php',
    $commonModules
);

$targetMenuIds = array();
$stmt = $pdo->query('SELECT `id`, `structure` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id`');
while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
    if ((int) $row['id'] === 15) {
        $targetMenuIds[] = (int) $row['id'];
    }
}
if (empty($targetMenuIds)) {
    $first = $pdo->query('SELECT `id` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id` LIMIT 1')->fetchColumn();
    if ($first) {
        $targetMenuIds[] = (int) $first;
    }
}

foreach ($targetMenuIds as $menuId) {
    $stmt = $pdo->prepare('SELECT `structure` FROM `menu` WHERE `id` = ?');
    $stmt->execute(array($menuId));
    $structure = json_decode($stmt->fetchColumn(), true);
    if (!is_array($structure)) {
        $structure = array();
    }

    $hasVehicleCatalog = false;
    foreach ($structure as $index => $item) {
        if ((isset($item['content_id']) && (int) $item['content_id'] === $vehicleCatalogId)
            || (isset($item['a_innerhtml']) && $item['a_innerhtml'] === 'epc_menu_vehicle_catalog')
            || (isset($item['href']) && $item['href'] === '/vehicle-catalog')) {
            $hasVehicleCatalog = true;
            $structure[$index] = epc_vc_menu_item('epc_menu_vehicle_catalog', '/vehicle-catalog', isset($item['id']) ? (int) $item['id'] : time() + 20, $vehicleCatalogId);
            break;
        }
        if (isset($item['data']) && is_array($item['data'])) {
            foreach ($item['data'] as $childIndex => $child) {
                if ((isset($child['content_id']) && (int) $child['content_id'] === $vehicleCatalogId)
                    || (isset($child['a_innerhtml']) && $child['a_innerhtml'] === 'epc_menu_vehicle_catalog')
                    || (isset($child['href']) && $child['href'] === '/vehicle-catalog')) {
                    $hasVehicleCatalog = true;
                    $structure[$index]['data'][$childIndex] = epc_vc_menu_item(
                        'epc_menu_vehicle_catalog',
                        '/vehicle-catalog',
                        isset($child['id']) ? (int) $child['id'] : time() + 20,
                        $vehicleCatalogId
                    );
                    if (isset($structure[$index]['$count'])) {
                        $structure[$index]['$count'] = max((int) $structure[$index]['$count'], count($structure[$index]['data']));
                    }
                    break 2;
                }
            }
        }
    }

    if (!$hasVehicleCatalog) {
        $structure[] = epc_vc_menu_item('epc_menu_vehicle_catalog', '/vehicle-catalog', time() + 20, $vehicleCatalogId);
    }

    $upd = $pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?');
    $upd->execute(array(json_encode($structure), $menuId));
}

echo "Installed Vehicle parts catalog route ID: " . $vehicleCatalogId . "\n";
echo "URL: /vehicle-catalog\n";
echo "Updated menu IDs: " . implode(',', $targetMenuIds) . "\n";
echo "OK\n";
@unlink(__FILE__);
?>
