<?php
/**
 * Register CP routes + menu for SKU Photos & Specifications.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_sku_media.php';

function epc_sku_media_cp_lang(PDO $pdo, string $key, string $en, string $ru): void
{
	$pdo->prepare('INSERT IGNORE INTO `lang_text_strings` (`str_key`, `description`, `same`, `is_error`, `is_custom`, `used_found`) VALUES (?, ?, NULL, 0, 1, 1)')->execute(array($key, $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'en', $en));
	$pdo->prepare('INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?) ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)')->execute(array($key, 'ru', $ru));
}

/**
 * @return array{content_id:int,menu_item_id:int}
 */
function epc_sku_media_cp_install(PDO $pdo, string $backendDir = 'cp', bool $apply = true): array
{
	$backendDir = trim($backendDir, '/');
	if ($backendDir === '') {
		$backendDir = 'cp';
	}
	epc_sku_media_ensure_schema($pdo);
	$result = array('content_id' => 0, 'menu_item_id' => 0);
	if (!$apply) {
		return $result;
	}

	epc_sku_media_cp_lang($pdo, 'epc_sku_media_manager', 'SKU photos & specs', 'Фото и характеристики SKU');

	$contentUrl = 'shop/catalogue/sku_media';
	$phpPath = '/<backend_dir>/content/shop/catalogue/epc_sku_media_manager.php';
	$now = time();

	$parent = $pdo->prepare('SELECT `id`, `level` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$parent->execute(array('shop/catalogue/products'));
	$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	if (!$parentRow) {
		$parent->execute(array('shop/catalogue'));
		$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	}
	if (!$parentRow) {
		$parent->execute(array('shop'));
		$parentRow = $parent->fetch(PDO::FETCH_ASSOC);
	}
	if (!$parentRow) {
		throw new RuntimeException('Parent content not found for SKU media CP route');
	}
	$parentId = (int) $parentRow['id'];
	$level = (int) $parentRow['level'] + 1;

	$existing = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$existing->execute(array($contentUrl));
	$contentId = (int) $existing->fetchColumn();
	if ($contentId > 0) {
		$pdo->prepare(
			'UPDATE `content` SET `published_flag` = 1, `content_type` = \'php\', `content` = ?, `title_tag` = ?, `value` = ?, `parent` = ?, `level` = ?, `alias` = ?, `time_edited` = ? WHERE `id` = ?'
		)->execute(array($phpPath, 'epc_sku_media_manager', 'epc_sku_media_manager', $parentId, $level, 'sku_media', $now, $contentId));
	} else {
		$pdo->prepare(
			'INSERT INTO `content` (`count`, `url`, `level`, `alias`, `value`, `parent`, `description`, `is_frontend`, `content_type`, `content`,
			 `title_tag`, `description_tag`, `keywords_tag`, `author_tag`, `main_flag`, `modules_array`, `css_js`, `robots_tag`,
			 `system_flag`, `published_flag`, `open`, `time_created`, `time_edited`, `order`)
			 VALUES (0, ?, ?, ?, ?, ?, ?, 0, \'php\', ?, ?, \'0\', \'0\', \'0\', 0, \'[]\', \'\', \'\', 0, 1, 0, ?, ?, 55)'
		)->execute(array(
			$contentUrl, $level, 'sku_media', 'epc_sku_media_manager', $parentId,
			'SKU photos & multi-type specifications for any product',
			$phpPath, 'epc_sku_media_manager', $now, $now,
		));
		$contentId = (int) $pdo->lastInsertId();
	}
	$result['content_id'] = $contentId;

	// Copy ACL from catalogue products page when available.
	$ref = $pdo->prepare('SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1');
	$ref->execute(array('shop/catalogue/products'));
	$refId = (int) $ref->fetchColumn();
	if ($refId > 0 && $contentId > 0) {
		$pdo->prepare('DELETE FROM `content_access` WHERE `content_id` = ?')->execute(array($contentId));
		$groups = $pdo->prepare('SELECT DISTINCT `group_id` FROM `content_access` WHERE `content_id` = ?');
		$groups->execute(array($refId));
		$ins = $pdo->prepare('INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)');
		while ($g = $groups->fetch(PDO::FETCH_ASSOC)) {
			try {
				$ins->execute(array($contentId, (int) $g['group_id']));
			} catch (Exception $e) {
			}
		}
	}

	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/epc_cp_mainstream_menu.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_cp_mainstream_menu.php';
		if (function_exists('epc_cp_shop_catalogue_prices_menu_apply')) {
			$menu = epc_cp_shop_catalogue_prices_menu_apply($pdo);
			$result['menu_item_id'] = (int) ($menu['items']['sku_media'] ?? 0);
		} elseif (function_exists('epc_cp_mm_find_shop_group') && function_exists('epc_cp_mm_ensure_item')) {
			$shop = epc_cp_mm_find_shop_group($pdo);
			$shopId = (int) ($shop['id'] ?? 0);
			if ($shopId > 0) {
				$result['menu_item_id'] = (int) epc_cp_mm_ensure_item(
					$pdo,
					$shopId,
					'epc_sku_media_manager',
					'/<backend>/shop/catalogue/sku_media',
					16,
					'#0f766e',
					'fa-picture-o',
					1
				);
			}
		}
	}

	return $result;
}
