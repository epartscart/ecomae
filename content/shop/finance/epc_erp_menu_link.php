<?php
/**
 * Top-menu link for frontend ERP portal (/shop/erp).
 */
defined('_ASTEXE_') or die('No access');

function epc_erp_menu_link_item($captionKey, $id, $contentId = 0)
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
		'href' => $contentId ? '' : '/erp',
		'target' => '',
		'onclick' => '',
		'img_src' => '',
		'$count' => 0,
		'$level' => 1,
		'$parent' => 0,
		'id' => $id,
	);
}

function epc_erp_menu_link_matches(array $item, $erpContentId)
{
	if ($erpContentId > 0 && isset($item['content_id']) && (int)$item['content_id'] === (int)$erpContentId) {
		return true;
	}
	if (isset($item['a_innerhtml']) && $item['a_innerhtml'] === 'epc_menu_erp_login') {
		return true;
	}
	$href = isset($item['href']) ? (string)$item['href'] : '';
	return (strpos($href, 'shop/erp') !== false || strpos($href, '/erp') !== false);
}

function epc_erp_menu_link_balance_matches(array $item)
{
	$href = isset($item['href']) ? (string)$item['href'] : '';
	if ($href !== '' && strpos($href, 'balans') !== false) {
		return true;
	}
	if (isset($item['a_innerhtml']) && stripos((string)$item['a_innerhtml'], 'balans') !== false) {
		return true;
	}
	return false;
}

function epc_erp_menu_link_lang(PDO $pdo)
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array('epc_menu_erp_login', 'ERP Login'));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array('epc_menu_erp_login', 'en', 'ERP Login'));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array('epc_menu_erp_login', 'ru', 'Вход ERP'));
}

function epc_erp_menu_link_target_ids(PDO $pdo)
{
	$ids = array();
	$stmt = $pdo->query('SELECT `id` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id`');
	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		if ((int)$row['id'] === 15) {
			$ids[] = 15;
		}
	}
	if (empty($ids)) {
		$first = (int)$pdo->query('SELECT `id` FROM `menu` WHERE `is_frontend` = 1 ORDER BY `id` LIMIT 1')->fetchColumn();
		if ($first > 0) {
			$ids[] = $first;
		}
	}
	return $ids;
}

function epc_erp_menu_link_ensure(PDO $pdo, $erpContentId = 0)
{
	if ($erpContentId <= 0) {
		$erpContentId = (int)$pdo->query("SELECT `id` FROM `content` WHERE `url` = 'shop/erp' AND `is_frontend` = 1 LIMIT 1")->fetchColumn();
	}

	epc_erp_menu_link_lang($pdo);

	$updated = array();
	foreach (epc_erp_menu_link_target_ids($pdo) as $menuId) {
		$stmt = $pdo->prepare('SELECT `structure` FROM `menu` WHERE `id` = ?');
		$stmt->execute(array($menuId));
		$structure = json_decode($stmt->fetchColumn(), true);
		if (!is_array($structure)) {
			$structure = array();
		}

		$nextId = time() + 40;
		$erpItem = null;
		$insertAfter = null;

		foreach ($structure as $index => $item) {
			if (!is_array($item)) {
				continue;
			}
			if (epc_erp_menu_link_matches($item, $erpContentId)) {
				$structure[$index] = epc_erp_menu_link_item('epc_menu_erp_login', isset($item['id']) ? (int)$item['id'] : $nextId++, $erpContentId);
				$updated[] = $menuId;
				break;
			}
			if (epc_erp_menu_link_balance_matches($item)) {
				$insertAfter = $index;
			}
		}

		if (!in_array($menuId, $updated, true)) {
			$newItem = epc_erp_menu_link_item('epc_menu_erp_login', $nextId++, $erpContentId);
			if ($insertAfter !== null) {
				array_splice($structure, $insertAfter + 1, 0, array($newItem));
			} else {
				$structure[] = $newItem;
			}
			$updated[] = $menuId;
		}

		$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(json_encode($structure), $menuId));
	}

	return array(
		'erp_content_id' => (int)$erpContentId,
		'menu_ids' => array_values(array_unique($updated)),
	);
}

function epc_erp_menu_link_remove(PDO $pdo, $erpContentId = 0)
{
	if ($erpContentId <= 0) {
		$erpContentId = (int)$pdo->query("SELECT `id` FROM `content` WHERE `url` = 'shop/erp' AND `is_frontend` = 1 LIMIT 1")->fetchColumn();
	}

	$updated = array();
	foreach (epc_erp_menu_link_target_ids($pdo) as $menuId) {
		$stmt = $pdo->prepare('SELECT `structure` FROM `menu` WHERE `id` = ?');
		$stmt->execute(array($menuId));
		$structure = json_decode($stmt->fetchColumn(), true);
		if (!is_array($structure)) {
			continue;
		}

		$changed = false;
		$newStructure = array();
		foreach ($structure as $item) {
			if (!is_array($item)) {
				$newStructure[] = $item;
				continue;
			}
			if (epc_erp_menu_link_matches($item, $erpContentId)) {
				$changed = true;
				continue;
			}
			if (isset($item['data']) && is_array($item['data'])) {
				$childChanged = false;
				$children = array();
				foreach ($item['data'] as $child) {
					if (is_array($child) && epc_erp_menu_link_matches($child, $erpContentId)) {
						$childChanged = true;
						continue;
					}
					$children[] = $child;
				}
				if ($childChanged) {
					$item['data'] = $children;
					if (isset($item['$count'])) {
						$item['$count'] = count($children);
					}
					$changed = true;
				}
			}
			$newStructure[] = $item;
		}

		if ($changed) {
			$pdo->prepare('UPDATE `menu` SET `structure` = ? WHERE `id` = ?')->execute(array(json_encode($newStructure), $menuId));
			$updated[] = $menuId;
		}
	}

	return array(
		'erp_content_id' => (int)$erpContentId,
		'menu_ids' => $updated,
		'removed' => !empty($updated),
	);
}
