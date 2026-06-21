<?php
/**
 * Auto Price AI — sync industry taxonomy → shop_catalogue_categories.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_industry_taxonomy.php';

function epc_apai_category_map_schema(PDO $pdo): void
{
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_taxonomy_category_map` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`industry_key` VARCHAR(32) NOT NULL DEFAULT \'\',
			`taxonomy_node_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`category_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`updated_at` INT NOT NULL DEFAULT 0,
			UNIQUE KEY `site_taxonomy` (`site_key`, `taxonomy_node_id`),
			KEY `category_id` (`category_id`),
			KEY `industry` (`industry_key`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
}

function epc_apai_category_slug(string $slug, string $prefix = 'apai'): string
{
	$slug = preg_replace('/[^a-z0-9\-]+/', '-', strtolower(trim($slug)));
	$slug = trim($slug, '-');
	if ($slug === '') {
		$slug = 'node';
	}
	return $prefix . '-' . substr($slug, 0, 80);
}

function epc_apai_category_upsert(PDO $pdo, string $name, string $alias, int $parentId, int $level, int $order, string $url = ''): int
{
	$alias = substr($alias, 0, 255);
	if ($url === '') {
		$url = $alias;
	}
	$url = substr($url, 0, 255);
	$name = trim($name);
	if ($name === '') {
		$name = $alias;
	}

	$chk = $pdo->prepare('SELECT `id` FROM `shop_catalogue_categories` WHERE `alias` = ? AND `parent` = ? LIMIT 1');
	$chk->execute(array($alias, $parentId));
	$id = (int) $chk->fetchColumn();

	if ($id > 0) {
		$pdo->prepare(
			'UPDATE `shop_catalogue_categories` SET `value`=?, `url`=?, `level`=?, `order`=?, `published_flag`=1 WHERE `id`=?'
		)->execute(array($name, $url, $level, $order, $id));
		return $id;
	}

	try {
		$maxOrder = (int) $pdo->prepare('SELECT COALESCE(MAX(`order`), 0) FROM `shop_catalogue_categories` WHERE `parent` = ?')
			->execute(array($parentId)) ?: 0;
	} catch (Throwable $e) {
		$maxOrder = 0;
	}
	$ordStmt = $pdo->prepare('SELECT COALESCE(MAX(`order`), 0) FROM `shop_catalogue_categories` WHERE `parent` = ?');
	$ordStmt->execute(array($parentId));
	$maxOrder = (int) $ordStmt->fetchColumn();
	if ($order <= 0) {
		$order = $maxOrder + 10;
	}

	$pdo->prepare(
		'INSERT INTO `shop_catalogue_categories`
		 (`alias`, `url`, `count`, `level`, `value`, `parent`, `title_tag`, `description_tag`, `keywords_tag`, `order`, `published_flag`)
		 VALUES (?, ?, 0, ?, ?, ?, ?, ?, ?, ?, 1)'
	)->execute(array($alias, $url, $level, $name, $parentId, $name, $name, $name, $order));
	return (int) $pdo->lastInsertId();
}

function epc_apai_sync_category_node(PDO $pdo, string $siteKey, string $industryKey, array $node, int $parentCatId, int $level, int &$synced, int $order = 10, string $parentUrl = ''): void
{
	$nodeId = (int) ($node['id'] ?? 0);
	$slug = (string) ($node['slug'] ?? 'node-' . $nodeId);
	$name = (string) ($node['name_en'] ?? $slug);
	$alias = epc_apai_category_slug($slug, 'apai-' . substr($industryKey, 0, 12));
	$seg = preg_replace('/^apai-[a-z0-9_]+-/', '', $alias);
	if ($seg === '') {
		$seg = $alias;
	}
	$url = ($parentUrl !== '') ? rtrim($parentUrl, '/') . '/' . $seg : $seg;

	$catId = epc_apai_category_upsert($pdo, $name, $alias, $parentCatId, $level, $order, $url);
	$now = time();

	$pdo->prepare(
		'INSERT INTO `epc_taxonomy_category_map` (`site_key`, `industry_key`, `taxonomy_node_id`, `category_id`, `updated_at`)
		 VALUES (?, ?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE `category_id` = VALUES(`category_id`), `industry_key` = VALUES(`industry_key`), `updated_at` = VALUES(`updated_at`)'
	)->execute(array($siteKey, $industryKey, $nodeId, $catId, $now));

	if ($nodeId > 0) {
		try {
			$pdo->prepare('UPDATE `epc_product_taxonomy_nodes` SET `catalogue_category_id` = ? WHERE `id` = ?')
				->execute(array($catId, $nodeId));
		} catch (Throwable $e) {
		}
	}
	$synced++;

	$ci = 0;
	foreach ((array) ($node['children'] ?? array()) as $child) {
		if (!is_array($child)) {
			continue;
		}
		epc_apai_sync_category_node($pdo, $siteKey, $industryKey, $child, $catId, $level + 1, $synced, 10 + ($ci * 10), $url);
		$ci++;
	}
}

/** Recompute `count` (direct children) for synced Auto Price AI categories. */
function epc_apai_refresh_category_counts(PDO $pdo, int $rootCategoryId = 0): int
{
	$where = '`alias` LIKE \'apai-%\'';
	$args = array();
	if ($rootCategoryId > 0) {
		$where = '(`alias` LIKE \'apai-%\' OR `id` = ? OR `parent` = ?)';
		$args = array($rootCategoryId, $rootCategoryId);
	}
	$ids = $pdo->prepare('SELECT `id` FROM `shop_catalogue_categories` WHERE ' . $where);
	$ids->execute($args);
	$catIds = $ids->fetchAll(PDO::FETCH_COLUMN) ?: array();
	if (!$catIds) {
		return 0;
	}
	$upd = $pdo->prepare(
		'UPDATE `shop_catalogue_categories` SET `count` = (
			SELECT COUNT(*) FROM (SELECT `id` FROM `shop_catalogue_categories` WHERE `parent` = ?) AS `ch`
		) WHERE `id` = ?'
	);
	$refreshed = 0;
	foreach ($catIds as $cid) {
		$upd->execute(array((int) $cid, (int) $cid));
		$refreshed++;
	}
	return $refreshed;
}

/**
 * Sync taxonomy tree for tenant industry into shop_catalogue_categories.
 *
 * @return array{synced:int,root_category_id:int,industry_key:string}
 */
function epc_apai_sync_categories(PDO $pdo, string $siteKey, string $industryKey = ''): array
{
	epc_apai_category_map_schema($pdo);
	epc_apai_taxonomy_migrate_schema($pdo);

	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($industryKey === '') {
		$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	}
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower($industryKey));

	$profiles = epc_apai_industry_profiles();
	$rootLabel = (string) (($profiles[$industryKey]['label'] ?? ucfirst(str_replace('_', ' ', $industryKey))) . ' — Product lines');
	$rootAlias = epc_apai_category_slug($industryKey, 'apai-root');

	$rootChk = $pdo->prepare('SELECT `id` FROM `shop_catalogue_categories` WHERE `alias` = ? AND `parent` = 0 LIMIT 1');
	$rootChk->execute(array($rootAlias));
	$rootCatId = (int) $rootChk->fetchColumn();
	if ($rootCatId <= 0) {
		$rootCatId = epc_apai_category_upsert($pdo, $rootLabel, $rootAlias, 0, 1, 900);
	} else {
		$pdo->prepare('UPDATE `shop_catalogue_categories` SET `value`=?, `published_flag`=1 WHERE `id`=?')
			->execute(array($rootLabel, $rootCatId));
	}

	$rootUrl = '';
	$rootRow = $pdo->prepare('SELECT `url` FROM `shop_catalogue_categories` WHERE `id` = ? LIMIT 1');
	$rootRow->execute(array($rootCatId));
	$rootUrl = (string) ($rootRow->fetchColumn() ?: '');

	$synced = 0;
	$tree = epc_apai_tax_list_tree($pdo, $industryKey);
	$oi = 0;
	foreach ($tree as $rootNode) {
		epc_apai_sync_category_node($pdo, $siteKey, $industryKey, $rootNode, $rootCatId, 2, $synced, 10 + ($oi * 10), $rootUrl);
		$oi++;
	}

	$ccStmt = $pdo->prepare('SELECT COUNT(*) FROM `shop_catalogue_categories` WHERE `parent` = ?');
	$ccStmt->execute(array($rootCatId));
	$pdo->prepare('UPDATE `shop_catalogue_categories` SET `count` = ? WHERE `id` = ?')
		->execute(array((int) $ccStmt->fetchColumn(), $rootCatId));
	epc_apai_refresh_category_counts($pdo, $rootCatId);

	return array(
		'synced' => $synced,
		'root_category_id' => $rootCatId,
		'industry_key' => $industryKey,
		'category_map_count' => epc_apai_category_count($pdo, $siteKey, $industryKey),
	);
}

function epc_apai_category_count(PDO $pdo, string $siteKey, string $industryKey = ''): int
{
	epc_apai_category_map_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($industryKey === '') {
		$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	}
	$stmt = $pdo->prepare('SELECT COUNT(*) FROM `epc_taxonomy_category_map` WHERE `site_key` = ? AND `industry_key` = ?');
	$stmt->execute(array($siteKey, $industryKey));
	return (int) $stmt->fetchColumn();
}

function epc_apai_category_for_taxonomy(PDO $pdo, string $siteKey, int $taxonomyNodeId): int
{
	if ($taxonomyNodeId <= 0) {
		return 0;
	}
	epc_apai_category_map_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$stmt = $pdo->prepare('SELECT `category_id` FROM `epc_taxonomy_category_map` WHERE `site_key` = ? AND `taxonomy_node_id` = ? LIMIT 1');
	$stmt->execute(array($siteKey, $taxonomyNodeId));
	$catId = (int) $stmt->fetchColumn();
	if ($catId > 0) {
		return $catId;
	}
	try {
		$stmt = $pdo->prepare('SELECT `catalogue_category_id` FROM `epc_product_taxonomy_nodes` WHERE `id` = ? LIMIT 1');
		$stmt->execute(array($taxonomyNodeId));
		return (int) $stmt->fetchColumn();
	} catch (Throwable $e) {
		return 0;
	}
}

function epc_apai_assign_product_category(PDO $pdo, int $productId, int $categoryId): void
{
	if ($productId <= 0 || $categoryId <= 0) {
		return;
	}
	try {
		$pdo->prepare(
			'UPDATE `shop_catalogue_products` SET `category_id` = ?, `published_flag` = 1 WHERE `id` = ?'
		)->execute(array($categoryId, $productId));
	} catch (Throwable $e) {
	}
}

/** Storefront lang prefix for CHPU product URLs (e.g. /en). */
function epc_apai_storefront_lang_prefix(): string
{
	global $DP_Config;
	if (empty($DP_Config->multilang)) {
		return '';
	}
	$isFrontMode = 1;
	$langHref = '';
	if (function_exists('multilang_init')) {
		$m = multilang_init();
		$langHref = (string) ($m['lang_href'] ?? '');
	}
	if ($langHref !== '') {
		return $langHref;
	}
	$lang = 'en';
	if (function_exists('get_work_lang')) {
		$work = get_work_lang();
		if (is_string($work) && $work !== '') {
			$lang = $work;
		}
	}
	return '/' . $lang;
}

/**
 * Brand/part CHPU slug for spare-parts catalogue products (e.g. toyota/1310154101).
 */
function epc_apai_product_chpu(string $brand, string $article, string $category_slug = ''): string
{
	$brand = epc_apai_normalize_brand($brand);
	$article = strtolower(epc_apai_normalize_article($article));
	if ($brand === '' || $article === '') {
		return '';
	}
	return $brand . '/' . $article;
}

/** Best-effort brand + article from discovery title (auto_parts fixup / legacy imports). */
function epc_apai_extract_brand_article_from_title(string $title): array
{
	$title = trim(preg_replace('/\s+—.*$/', '', $title));
	if ($title === '') {
		return array('brand' => '', 'article' => '');
	}
	$brand = '';
	$article = '';
	if (preg_match('/^([A-Za-z][A-Za-z0-9&\.\-]{0,20})\b/u', $title, $bm)) {
		$brand = trim((string) $bm[1]);
	}
	if (preg_match('/\b([0-9]{6,12})\b/', $title, $oem)) {
		$article = (string) $oem[1];
	} elseif (preg_match('/\b([A-Z]{1,4}[\-\s][0-9]{2,}[A-Z0-9\-]*)\b/i', $title, $pn)) {
		$article = preg_replace('/\s+/', '', strtoupper((string) $pn[1]));
	} elseif (preg_match('/\b([A-Z]{2,}[0-9]{2,}[A-Z0-9\-]*)\b/i', $title, $pn)) {
		$article = strtoupper((string) $pn[1]);
	}
	if ($brand !== '' && $article !== '') {
		return array('brand' => $brand, 'article' => $article);
	}
	return array('brand' => $brand, 'article' => $article);
}

/** Parse brand + article from a product alias or brand_article_key. */
function epc_apai_parse_product_chpu(string $slug): array
{
	$slug = trim($slug);
	if ($slug === '') {
		return array('brand' => '', 'article' => '');
	}
	if (strpos($slug, ':') !== false && strpos($slug, '/') === false) {
		$parts = explode(':', $slug, 2);
		return array(
			'brand' => epc_apai_normalize_brand((string) ($parts[0] ?? '')),
			'article' => epc_apai_normalize_article((string) ($parts[1] ?? '')),
		);
	}
	if (strpos($slug, '/') !== false) {
		$parts = explode('/', $slug, 2);
		return array(
			'brand' => epc_apai_normalize_brand((string) ($parts[0] ?? '')),
			'article' => epc_apai_normalize_article((string) ($parts[1] ?? '')),
		);
	}
	return array('brand' => '', 'article' => '');
}

/**
 * Storefront path for a catalogue product (supports brand/article alias with slash).
 */
function epc_apai_catalogue_product_path(array $productRow, string $productUrlMode = 'alias'): string
{
	$catUrl = trim((string) ($productRow['category_url'] ?? ''), '/');
	if ($catUrl === '') {
		return '';
	}
	if ($productUrlMode === 'id') {
		$pid = (int) ($productRow['id'] ?? 0);
		return $pid > 0 ? '/' . $catUrl . '/' . $pid : '';
	}
	$alias = trim((string) ($productRow['alias'] ?? ''), '/');
	if ($alias === '') {
		return '';
	}
	return '/' . $catUrl . '/' . $alias;
}

/**
 * Resolve category + product from a CHPU route (supports brand/article two-segment slugs).
 *
 * @return array{category:array,product:array,category_url:string}|null
 */
function epc_apai_resolve_catalogue_product_route(PDO $pdo, string $urlRoute, string $productUrlMode = 'alias'): ?array
{
	$urlRoute = trim($urlRoute, '/');
	if ($urlRoute === '') {
		return null;
	}
	$components = explode('/', $urlRoute);
	$n = count($components);
	if ($n < 2) {
		return null;
	}

	$catStmt = $pdo->prepare('SELECT * FROM `shop_catalogue_categories` WHERE `url` = ? LIMIT 1');
	$prodByAlias = $pdo->prepare(
		'SELECT * FROM `shop_catalogue_products` WHERE `category_id` = ? AND `alias` = ? LIMIT 1'
	);
	$prodById = $pdo->prepare(
		'SELECT * FROM `shop_catalogue_products` WHERE `category_id` = ? AND `id` = ? LIMIT 1'
	);

	for ($splitAt = $n - 1; $splitAt >= 1; $splitAt--) {
		$catUrl = implode('/', array_slice($components, 0, $splitAt));
		$catStmt->execute(array($catUrl));
		$category = $catStmt->fetch(PDO::FETCH_ASSOC);
		if (!$category) {
			continue;
		}
		$catId = (int) ($category['id'] ?? 0);
		$remain = array_slice($components, $splitAt);
		if (!$remain) {
			continue;
		}

		if ($productUrlMode === 'id' && count($remain) === 1) {
			$prodById->execute(array($catId, (int) $remain[0]));
			$product = $prodById->fetch(PDO::FETCH_ASSOC);
			if ($product) {
				return array('category' => $category, 'product' => $product, 'category_url' => $catUrl);
			}
			continue;
		}

		$alias = implode('/', $remain);
		$prodByAlias->execute(array($catId, $alias));
		$product = $prodByAlias->fetch(PDO::FETCH_ASSOC);
		if ($product) {
			return array('category' => $category, 'product' => $product, 'category_url' => $catUrl);
		}

		if (count($remain) === 1) {
			$prodByAlias->execute(array($catId, $remain[0]));
			$product = $prodByAlias->fetch(PDO::FETCH_ASSOC);
			if ($product) {
				return array('category' => $category, 'product' => $product, 'category_url' => $catUrl);
			}
		}
	}
	return null;
}

/**
 * Assign manufacturer + article properties on a catalogue product (auto_parts search/display).
 */
function epc_apai_assign_product_part_properties(PDO $pdo, int $productId, int $categoryId, string $brand, string $article): bool
{
	if ($productId <= 0 || $categoryId <= 0 || $brand === '' || $article === '') {
		return false;
	}
	$articleNorm = epc_apai_normalize_article($article);
	$brandDisplay = strtoupper(trim($brand));
	if ($articleNorm === '' || $brandDisplay === '') {
		return false;
	}

	if (!function_exists('save_custom_translation')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/lang/dp_lang.php';
	}
	global $db_link;
	if (!$db_link instanceof PDO) {
		$db_link = $pdo;
	}

	$articlePropStmt = $pdo->prepare(
		'SELECT `id` FROM `shop_categories_properties_map`
		 WHERE `category_id` = ? AND `property_type_id` = 3
		   AND `value` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN ("Артикул", "Article"))
		 LIMIT 1'
	);
	$articlePropStmt->execute(array($categoryId));
	$articlePropId = (int) $articlePropStmt->fetchColumn();

	$mfrPropStmt = $pdo->prepare(
		'SELECT `id`, `list_id` FROM `shop_categories_properties_map`
		 WHERE `category_id` = ? AND `property_type_id` = 5
		   AND `value` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` IN ("Производитель", "Manufacturer"))
		 LIMIT 1'
	);
	$mfrPropStmt->execute(array($categoryId));
	$mfrProp = $mfrPropStmt->fetch(PDO::FETCH_ASSOC) ?: array();
	$mfrPropId = (int) ($mfrProp['id'] ?? 0);
	$listId = (int) ($mfrProp['list_id'] ?? 0);

	$ok = false;

	if ($articlePropId > 0) {
		$strId = save_custom_translation(0, $articleNorm);
		if ($strId > 0) {
			$pdo->prepare('DELETE FROM `shop_properties_values_text` WHERE `product_id` = ? AND `property_id` = ?')
				->execute(array($productId, $articlePropId));
			$pdo->prepare(
				'INSERT INTO `shop_properties_values_text` (`product_id`, `property_id`, `category_id`, `value`)
				 VALUES (?, ?, ?, ?)'
			)->execute(array($productId, $articlePropId, $categoryId, $strId));
			$ok = true;
		}
	}

	if ($mfrPropId > 0 && $listId > 0) {
		$listItemId = 0;
		$items = $pdo->prepare('SELECT `id`, `value` FROM `shop_line_lists_items` WHERE `line_list_id` = ?');
		$items->execute(array($listId));
		while ($item = $items->fetch(PDO::FETCH_ASSOC)) {
			$label = function_exists('translate_str_by_id') ? translate_str_by_id((int) ($item['value'] ?? 0)) : '';
			if (strcasecmp($label, $brandDisplay) === 0) {
				$listItemId = (int) ($item['id'] ?? 0);
				break;
			}
		}
		if ($listItemId <= 0) {
			$strId = save_custom_translation(0, $brandDisplay);
			if ($strId > 0) {
				$ordStmt = $pdo->prepare('SELECT COALESCE(MAX(`order`), 0) FROM `shop_line_lists_items` WHERE `line_list_id` = ?');
				$ordStmt->execute(array($listId));
				$ord = (int) $ordStmt->fetchColumn() + 1;
				$pdo->prepare('INSERT INTO `shop_line_lists_items` (`line_list_id`, `value`, `order`) VALUES (?, ?, ?)')
					->execute(array($listId, $strId, $ord));
				$listItemId = (int) $pdo->lastInsertId();
			}
		}
		if ($listItemId > 0) {
			$pdo->prepare('DELETE FROM `shop_properties_values_list` WHERE `product_id` = ? AND `property_id` = ?')
				->execute(array($productId, $mfrPropId));
			$pdo->prepare(
				'INSERT INTO `shop_properties_values_list` (`product_id`, `property_id`, `category_id`, `value`)
				 VALUES (?, ?, ?, ?)'
			)->execute(array($productId, $mfrPropId, $categoryId, $listItemId));
			$ok = true;
		}
	}

	return $ok;
}

/** Copy manufacturer + article property maps onto an APAI-synced category from a donor category. */
function epc_apai_ensure_category_base_properties(PDO $pdo, int $categoryId, string $industryKey = 'auto_parts'): void
{
	if ($categoryId <= 0 || $industryKey !== 'auto_parts') {
		return;
	}
	$chk = $pdo->prepare(
		'SELECT COUNT(*) FROM `shop_categories_properties_map`
		 WHERE `category_id` = ? AND `property_type_id` IN (3, 5)'
	);
	$chk->execute(array($categoryId));
	if ((int) $chk->fetchColumn() >= 2) {
		return;
	}
	$donor = $pdo->query(
		'SELECT `category_id` FROM `shop_categories_properties_map`
		 WHERE `property_type_id` IN (3, 5)
		 GROUP BY `category_id` HAVING COUNT(*) >= 2
		 ORDER BY `category_id` ASC LIMIT 1'
	);
	$donorCatId = (int) ($donor ? $donor->fetchColumn() : 0);
	if ($donorCatId <= 0) {
		return;
	}
	$props = $pdo->prepare(
		'SELECT * FROM `shop_categories_properties_map`
		 WHERE `category_id` = ? AND `property_type_id` IN (3, 5)'
	);
	$props->execute(array($donorCatId));
	while ($prop = $props->fetch(PDO::FETCH_ASSOC)) {
		$typeId = (int) ($prop['property_type_id'] ?? 0);
		$exists = $pdo->prepare(
			'SELECT `id` FROM `shop_categories_properties_map`
			 WHERE `category_id` = ? AND `property_type_id` = ? LIMIT 1'
		);
		$exists->execute(array($categoryId, $typeId));
		if ((int) $exists->fetchColumn() > 0) {
			continue;
		}
		$pdo->prepare(
			'INSERT INTO `shop_categories_properties_map`
			 (`category_id`, `property_type_id`, `value`, `list_id`, `order`, `for_similar`, `is_option`)
			 VALUES (?, ?, ?, ?, ?, ?, ?)'
		)->execute(array(
			$categoryId,
			$typeId,
			$prop['value'],
			(int) ($prop['list_id'] ?? 0),
			(int) ($prop['order'] ?? 10),
			(int) ($prop['for_similar'] ?? 0),
			(int) ($prop['is_option'] ?? 0),
		));
	}
}

/**
 * Reassign Auto Price AI imported products to synced taxonomy categories.
 *
 * @return array{synced:int,fixed:int,products:array<int,array<string,mixed>>}
 */
function epc_apai_fixup_imported_products(PDO $pdo, string $siteKey, array $productIds = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$catSync = epc_apai_sync_categories($pdo, $siteKey);
	$fixed = 0;
	$products = array();

	$sql = 'SELECT `id`, `product_id`, `taxonomy_node_id`, `title`, `source_url`, `specs_json`, `meta_json`
		 FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `status` = \'imported\' AND `product_id` > 0';
	$args = array($siteKey);
	if ($productIds) {
		$placeholders = implode(',', array_fill(0, count($productIds), '?'));
		$sql .= ' AND `product_id` IN (' . $placeholders . ')';
		$args = array_merge($args, array_map('intval', $productIds));
	}
	$sql .= ' ORDER BY `id` ASC';
	$stmt = $pdo->prepare($sql);
	$stmt->execute($args);

	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);

	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$productId = (int) ($row['product_id'] ?? 0);
		$taxonomyNodeId = (int) ($row['taxonomy_node_id'] ?? 0);
		$categoryId = epc_apai_category_for_taxonomy($pdo, $siteKey, $taxonomyNodeId);
		if ($categoryId <= 0 && $taxonomyNodeId > 0) {
			$catSync = epc_apai_sync_categories($pdo, $siteKey);
			$categoryId = epc_apai_category_for_taxonomy($pdo, $siteKey, $taxonomyNodeId);
		}
		if ($categoryId <= 0) {
			$leaf = $pdo->prepare(
				'SELECT `category_id` FROM `epc_taxonomy_category_map`
				 WHERE `site_key` = ? ORDER BY `taxonomy_node_id` DESC LIMIT 1'
			);
			$leaf->execute(array($siteKey));
			$categoryId = (int) $leaf->fetchColumn();
		}
		if ($categoryId > 0) {
			epc_apai_assign_product_category($pdo, $productId, $categoryId);
			if ($industryKey === 'auto_parts') {
				epc_apai_ensure_category_base_properties($pdo, $categoryId, $industryKey);
			}
			$fixed++;
		}

		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		if (!is_array($meta)) {
			$meta = array();
		}
		$specs = (array) ($meta['specs'] ?? array());
		if (!$specs) {
			$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
			if (!is_array($specs)) {
				$specs = array();
			}
		}
		if (function_exists('epc_apai_specs_enrich_brand_article')) {
			$specs = epc_apai_specs_enrich_brand_article(
				$specs,
				(string) ($row['source_url'] ?? ''),
				(string) ($row['title'] ?? '')
			);
		}
		$brand = (string) ($specs['brand'] ?? '');
		$article = (string) ($specs['article_number'] ?? '');
		if (($brand === '' || $article === '') && $industryKey === 'auto_parts') {
			$fromTitle = epc_apai_extract_brand_article_from_title((string) ($row['title'] ?? ''));
			if ($brand === '' && $fromTitle['brand'] !== '') {
				$brand = (string) $fromTitle['brand'];
			}
			if ($article === '' && $fromTitle['article'] !== '') {
				$article = (string) $fromTitle['article'];
			}
		}
		$newAlias = '';
		if ($industryKey === 'auto_parts' && $brand !== '' && $article !== '') {
			$newAlias = epc_apai_product_chpu($brand, $article);
		}
		$aliasFixed = false;
		if ($newAlias !== '' && $productId > 0) {
			$cur = $pdo->prepare('SELECT `alias` FROM `shop_catalogue_products` WHERE `id` = ? LIMIT 1');
			$cur->execute(array($productId));
			$curAlias = (string) ($cur->fetchColumn() ?: '');
			if ($curAlias !== $newAlias) {
				$dup = $pdo->prepare(
					'SELECT `id` FROM `shop_catalogue_products` WHERE `alias` = ? AND `id` != ? LIMIT 1'
				);
				$dup->execute(array($newAlias, $productId));
				if ((int) $dup->fetchColumn() === 0) {
					$pdo->prepare('UPDATE `shop_catalogue_products` SET `alias` = ? WHERE `id` = ?')
						->execute(array($newAlias, $productId));
					$aliasFixed = true;
				}
			}
			if ($categoryId > 0) {
				epc_apai_assign_product_part_properties($pdo, $productId, $categoryId, $brand, $article);
			}
		}

		$catRow = $pdo->prepare('SELECT `url`, `published_flag`, `count` FROM `shop_catalogue_categories` WHERE `id` = ? LIMIT 1');
		$catRow->execute(array($categoryId));
		$cat = $catRow->fetch(PDO::FETCH_ASSOC) ?: array();
		$prodAlias = $newAlias;
		if ($prodAlias === '') {
			$aStmt = $pdo->prepare('SELECT `alias` FROM `shop_catalogue_products` WHERE `id` = ? LIMIT 1');
			$aStmt->execute(array($productId));
			$prodAlias = (string) ($aStmt->fetchColumn() ?: '');
		}
		$products[$productId] = array(
			'queue_id' => (int) ($row['id'] ?? 0),
			'title' => (string) ($row['title'] ?? ''),
			'taxonomy_node_id' => $taxonomyNodeId,
			'category_id' => $categoryId,
			'category_url' => (string) ($cat['url'] ?? ''),
			'alias' => $prodAlias,
			'brand' => $brand,
			'article' => $article,
			'alias_fixed' => $aliasFixed,
			'chpu_path' => ($cat['url'] ?? '') !== '' && $prodAlias !== ''
				? '/' . trim((string) $cat['url'], '/') . '/' . $prodAlias
				: '',
		);
	}

	// Demo / matrix products without discovery queue rows (e.g. Samsung Tab demo #100).
	$demoAliases = array(
		100 => 'apai-electronics-computers-tablets',
	);
	foreach ($demoAliases as $demoProductId => $catAlias) {
		if ($productIds && !in_array($demoProductId, $productIds, true)) {
			continue;
		}
		$catStmt = $pdo->prepare('SELECT `id`, `url` FROM `shop_catalogue_categories` WHERE `alias` = ? LIMIT 1');
		$catStmt->execute(array($catAlias));
		$cat = $catStmt->fetch(PDO::FETCH_ASSOC);
		if (!$cat) {
			continue;
		}
		epc_apai_assign_product_category($pdo, $demoProductId, (int) $cat['id']);
		$fixed++;
		$products[$demoProductId] = array(
			'queue_id' => 0,
			'title' => 'demo',
			'taxonomy_node_id' => 0,
			'category_id' => (int) $cat['id'],
			'category_url' => (string) ($cat['url'] ?? ''),
		);
	}

	return array(
		'synced' => (int) ($catSync['synced'] ?? 0),
		'root_category_id' => (int) ($catSync['root_category_id'] ?? 0),
		'fixed' => $fixed,
		'products' => $products,
	);
}

/** Keyword → taxonomy slug hints per industry (shared by enrich + category advisor). */
function epc_apai_taxonomy_keyword_map(string $industryKey = ''): array
{
	$map = array(
		'auto_parts' => array(
			'filter' => 'auto-engine-filters-oil', 'brake' => 'auto-brakes-pads', 'pad' => 'auto-brakes-pads',
			'spark' => 'auto-engine-spark', 'belt' => 'auto-engine-belts', 'shock' => 'auto-brakes-shocks',
			'light' => 'auto-body-lights', 'mirror' => 'auto-body-mirrors', 'mat' => 'auto-interior-mats',
		),
		'electronics' => array(
			'iphone' => 'cell-phones-unlocked-iphone', 'samsung galaxy' => 'cell-phones-unlocked-android',
			'samsung' => 'cell-phones-unlocked-android', 'phone' => 'cell-phones', 'tablet' => 'computers-tablets',
			'laptop' => 'computers-laptops', 'headphone' => 'headphones', 'earbud' => 'headphones-earbuds',
			'airpod' => 'headphones-earbuds', 'tv' => 'tv-video-televisions', 'television' => 'tv-video-televisions',
			'playstation' => 'gaming-consoles-playstation', 'xbox' => 'gaming-consoles-xbox', 'console' => 'gaming-consoles',
			'smartwatch' => 'wearables-smartwatches', 'watch' => 'wearables-smartwatches', 'camera' => 'cameras-digital',
			'nest hub' => 'smart-home-assistants', 'echo dot' => 'smart-home-assistants', 'alexa' => 'smart-home-assistants',
			'smart speaker' => 'smart-home-assistants', 'smart display' => 'smart-home-assistants',
		),
		'fashion' => array(
			'men' => 'fashion-men-shirts', 'shirt' => 'fashion-men-shirts', 'dress' => 'fashion-women-dresses',
			'abaya' => 'fashion-women-dresses', 'kid' => 'fashion-kids', 'bag' => 'fashion-accessories-bags',
			'sunglass' => 'fashion-accessories-sunglasses', 'shoe' => 'fashion-men-footwear',
		),
		'jewellery' => array(
			'ring' => 'jewellery-rings-gold', 'gold' => 'jewellery-rings-gold-22k', 'diamond' => 'jewellery-rings-diamond',
			'necklace' => 'jewellery-necklaces-pendant', 'watch' => 'jewellery-watches-luxury',
			'earring' => 'jewellery-earrings-studs', 'bracelet' => 'jewellery-bracelets',
		),
		'general_retail' => array(
			'coffee' => 'retail-home-appliances-coffee', 'blender' => 'retail-home-appliances-blenders',
			'office' => 'retail-office-supplies', 'vitamin' => 'retail-health-vitamins',
			'gift' => 'retail-gifts-hampers', 'decor' => 'retail-home-decor',
		),
	);
	if ($industryKey !== '' && isset($map[$industryKey])) {
		return $map[$industryKey];
	}
	return $map;
}

function epc_apai_advisor_product_text(array $queueRow): string
{
	$parts = array(
		(string) ($queueRow['title'] ?? ''),
		(string) ($queueRow['description'] ?? ''),
	);
	$specs = json_decode((string) ($queueRow['specs_json'] ?? ''), true);
	if (is_array($specs)) {
		foreach ($specs as $k => $v) {
			$parts[] = (string) $k . ' ' . (string) $v;
		}
	}
	return strtolower(implode(' ', $parts));
}

/**
 * Score taxonomy nodes against product text; returns best node + score.
 *
 * @return array{node:array,score:float,confidence:string,source:string}|null
 */
function epc_apai_match_taxonomy_scored(PDO $pdo, string $industryKey, string $text): ?array
{
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower($industryKey));
	$text = strtolower($text);
	if ($text === '') {
		return null;
	}

	$kwMap = epc_apai_taxonomy_keyword_map($industryKey);
	uksort($kwMap, static function ($a, $b) {
		return strlen($b) - strlen($a);
	});
	foreach ($kwMap as $kw => $slug) {
		if (strpos($text, $kw) !== false) {
			$node = epc_apai_tax_by_slug($pdo, $industryKey, $slug);
			if ($node) {
				return array(
					'node' => $node,
					'score' => 100.0,
					'confidence' => 'high',
					'source' => 'keyword:' . $kw,
				);
			}
		}
	}

	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_product_taxonomy_nodes` WHERE `industry_key` = ? AND `active` = 1 ORDER BY `level` DESC, `sort`'
	);
	$stmt->execute(array($industryKey));
	$best = null;
	$bestScore = 0.0;
	$genericTokens = array('portable', 'home', 'smart', 'digital', 'wireless', 'model', 'series', 'device', 'system', 'premium', 'professional', 'mini', 'pro', 'plus', 'max', 'ultra');
	foreach ($stmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $row) {
		$slug = (string) ($row['slug'] ?? '');
		$name = (string) ($row['name_en'] ?? '');
		$tokens = preg_split('/[^a-z0-9]+/', strtolower($slug . ' ' . $name), -1, PREG_SPLIT_NO_EMPTY);
		$score = 0.0;
		foreach ($tokens as $tok) {
			if (strlen($tok) < 4 || in_array($tok, $genericTokens, true)) {
				continue;
			}
			if (strpos($text, $tok) !== false) {
				$score += strlen($tok) >= 7 ? 3.0 : 2.0;
			}
		}
		if ((int) ($row['level'] ?? 0) >= 3) {
			$score += 0.25;
		}
		if ($score > $bestScore) {
			$bestScore = $score;
			$best = $row;
		}
	}
	if (!$best || $bestScore < 4.0) {
		return null;
	}
	return array(
		'node' => $best,
		'score' => $bestScore,
		'confidence' => $bestScore >= 4.0 ? 'high' : 'medium',
		'source' => 'token_score',
	);
}

function epc_apai_propose_category_from_product(string $title, string $description = ''): array
{
	$raw = html_entity_decode(strip_tags($title . ' ' . $description), ENT_QUOTES, 'UTF-8');
	$stop = array('the', 'and', 'for', 'with', 'new', 'pro', 'max', 'plus', 'ultra', 'series', 'edition', 'buy', 'online', 'uae', 'aed');
	$words = preg_split('/[^a-zA-Z0-9]+/', strtolower($raw), -1, PREG_SPLIT_NO_EMPTY);
	$kept = array();
	foreach ($words as $w) {
		if (strlen($w) < 3 || in_array($w, $stop, true) || preg_match('/^\d+$/', $w)) {
			continue;
		}
		if (!in_array($w, $kept, true)) {
			$kept[] = $w;
		}
		if (count($kept) >= 4) {
			break;
		}
	}
	if (!$kept) {
		$kept = array('general', 'products');
	}
	$name = ucwords(implode(' ', array_slice($kept, 0, 3)));
	$slugBase = implode('-', array_slice($kept, 0, 3));
	$slug = preg_replace('/[^a-z0-9\-]+/', '-', $slugBase);
	$slug = trim($slug, '-');
	if ($slug === '') {
		$slug = 'general-products';
	}
	$slug = 'auto-' . substr($slug, 0, 48);
	return array('name' => $name, 'slug' => $slug);
}

function epc_apai_category_name_by_id(PDO $pdo, int $categoryId): string
{
	if ($categoryId <= 0) {
		return '';
	}
	$stmt = $pdo->prepare('SELECT `value` FROM `shop_catalogue_categories` WHERE `id` = ? LIMIT 1');
	$stmt->execute(array($categoryId));
	return (string) ($stmt->fetchColumn() ?: '');
}

/** Flat list of synced industry categories for Discover dropdown. */
function epc_apai_list_industry_categories(PDO $pdo, string $siteKey, string $industryKey = ''): array
{
	epc_apai_category_map_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($industryKey === '') {
		$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	}
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower($industryKey));
	epc_apai_sync_categories($pdo, $siteKey, $industryKey);

	$rootAlias = epc_apai_category_slug($industryKey, 'apai-root');
	$rootStmt = $pdo->prepare('SELECT `id` FROM `shop_catalogue_categories` WHERE `alias` = ? AND `parent` = 0 LIMIT 1');
	$rootStmt->execute(array($rootAlias));
	$rootId = (int) $rootStmt->fetchColumn();
	if ($rootId <= 0) {
		return array();
	}

	$stmt = $pdo->prepare(
		'SELECT c.`id`, c.`value`, c.`level`, c.`parent`, c.`url`
		 FROM `shop_catalogue_categories` c
		 INNER JOIN `epc_taxonomy_category_map` m ON m.`category_id` = c.`id` AND m.`site_key` = ? AND m.`industry_key` = ?
		 WHERE c.`published_flag` = 1
		 ORDER BY c.`level`, c.`order`, c.`value`'
	);
	$stmt->execute(array($siteKey, $industryKey));
	$out = array();
	foreach ($stmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $row) {
		$level = max(1, (int) ($row['level'] ?? 1));
		$out[] = array(
			'id' => (int) $row['id'],
			'name' => (string) ($row['value'] ?? ''),
			'level' => $level,
			'parent' => (int) ($row['parent'] ?? 0),
			'url' => (string) ($row['url'] ?? ''),
			'label' => str_repeat('— ', max(0, $level - 2)) . (string) ($row['value'] ?? ''),
		);
	}
	return $out;
}

/** Ensure catalogue category exists for a taxonomy node (sync parent chain). */
function epc_apai_ensure_category_for_taxonomy(PDO $pdo, string $siteKey, string $industryKey, int $taxonomyNodeId): int
{
	if ($taxonomyNodeId <= 0) {
		return 0;
	}
	epc_apai_category_map_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower($industryKey));

	$existing = epc_apai_category_for_taxonomy($pdo, $siteKey, $taxonomyNodeId);
	if ($existing > 0) {
		return $existing;
	}

	$sync = epc_apai_sync_categories($pdo, $siteKey, $industryKey);
	$rootCatId = (int) ($sync['root_category_id'] ?? 0);
	$rootUrl = '';
	if ($rootCatId > 0) {
		$ru = $pdo->prepare('SELECT `url` FROM `shop_catalogue_categories` WHERE `id` = ? LIMIT 1');
		$ru->execute(array($rootCatId));
		$rootUrl = (string) ($ru->fetchColumn() ?: '');
	}

	$nodeStmt = $pdo->prepare('SELECT * FROM `epc_product_taxonomy_nodes` WHERE `id` = ? LIMIT 1');
	$nodeStmt->execute(array($taxonomyNodeId));
	$node = $nodeStmt->fetch(PDO::FETCH_ASSOC);
	if (!$node) {
		return 0;
	}

	$parentTaxId = (int) ($node['parent_id'] ?? 0);
	$parentCatId = $rootCatId;
	if ($parentTaxId > 0) {
		$parentCatId = epc_apai_ensure_category_for_taxonomy($pdo, $siteKey, $industryKey, $parentTaxId);
		if ($parentCatId <= 0) {
			$parentCatId = $rootCatId;
		}
	}

	$parentUrl = $rootUrl;
	if ($parentCatId > 0 && $parentCatId !== $rootCatId) {
		$pu = $pdo->prepare('SELECT `url` FROM `shop_catalogue_categories` WHERE `id` = ? LIMIT 1');
		$pu->execute(array($parentCatId));
		$parentUrl = (string) ($pu->fetchColumn() ?: $rootUrl);
	}

	$slug = (string) ($node['slug'] ?? 'node-' . $taxonomyNodeId);
	$name = (string) ($node['name_en'] ?? $slug);
	$alias = epc_apai_category_slug($slug, 'apai-' . substr($industryKey, 0, 12));
	$seg = preg_replace('/^apai-[a-z0-9_]+-/', '', $alias);
	if ($seg === '') {
		$seg = $alias;
	}
	$url = ($parentUrl !== '') ? rtrim($parentUrl, '/') . '/' . $seg : $seg;
	$level = max(2, (int) ($node['level'] ?? 2) + 1);

	$catId = epc_apai_category_upsert($pdo, $name, $alias, $parentCatId, $level, 10, $url);
	$now = time();
	$pdo->prepare(
		'INSERT INTO `epc_taxonomy_category_map` (`site_key`, `industry_key`, `taxonomy_node_id`, `category_id`, `updated_at`)
		 VALUES (?, ?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE `category_id` = VALUES(`category_id`), `updated_at` = VALUES(`updated_at`)'
	)->execute(array($siteKey, $industryKey, $taxonomyNodeId, $catId, $now));
	try {
		$pdo->prepare('UPDATE `epc_product_taxonomy_nodes` SET `catalogue_category_id` = ? WHERE `id` = ?')
			->execute(array($catId, $taxonomyNodeId));
	} catch (Throwable $e) {
	}
	epc_apai_refresh_category_counts($pdo, $rootCatId);
	return $catId;
}

/**
 * Create auto taxonomy leaf + catalogue category under industry root.
 *
 * @return array{taxonomy_node_id:int,category_id:int,name:string,slug:string}
 */
function epc_apai_create_auto_taxonomy_and_category(PDO $pdo, string $siteKey, string $industryKey, string $name, string $slug): array
{
	epc_apai_taxonomy_migrate_schema($pdo);
	epc_apai_category_map_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower($industryKey));
	$slug = preg_replace('/[^a-z0-9\-]+/', '-', strtolower(trim($slug)));
	$slug = trim($slug, '-');
	if ($slug === '') {
		$slug = 'auto-product-' . substr(md5($name . $siteKey), 0, 8);
	}
	$name = trim($name);
	if ($name === '') {
		$name = ucwords(str_replace('-', ' ', $slug));
	}

	$chk = $pdo->prepare('SELECT `id` FROM `epc_product_taxonomy_nodes` WHERE `industry_key` = ? AND `slug` = ? LIMIT 1');
	$chk->execute(array($industryKey, $slug));
	$taxId = (int) $chk->fetchColumn();
	if ($taxId <= 0) {
		$maxSort = (int) $pdo->prepare('SELECT COALESCE(MAX(`sort`), 0) FROM `epc_product_taxonomy_nodes` WHERE `industry_key` = ? AND `parent_id` = 0')
			->execute(array($industryKey)) ?: 0;
		$sortStmt = $pdo->prepare('SELECT COALESCE(MAX(`sort`), 0) FROM `epc_product_taxonomy_nodes` WHERE `industry_key` = ? AND `parent_id` = 0');
		$sortStmt->execute(array($industryKey));
		$maxSort = (int) $sortStmt->fetchColumn();
		$pdo->prepare(
			'INSERT INTO `epc_product_taxonomy_nodes`
			 (`industry_key`, `parent_id`, `slug`, `name_en`, `amazon_node_ref`, `sort`, `level`, `active`, `auto_created`)
			 VALUES (?, 0, ?, ?, ?, ?, 1, 1, 1)'
		)->execute(array($industryKey, $slug, $name, '', $maxSort + 10));
		$taxId = (int) $pdo->lastInsertId();
	} else {
		try {
			$pdo->prepare('UPDATE `epc_product_taxonomy_nodes` SET `auto_created` = 1, `name_en` = ? WHERE `id` = ?')
				->execute(array($name, $taxId));
		} catch (Throwable $e) {
		}
	}

	$catId = epc_apai_ensure_category_for_taxonomy($pdo, $siteKey, $industryKey, $taxId);
	return array(
		'taxonomy_node_id' => $taxId,
		'category_id' => $catId,
		'name' => $name,
		'slug' => $slug,
	);
}

/**
 * Smart category advisor for discovery queue items.
 *
 * @return array<string,mixed>
 */
function epc_apai_advise_category(PDO $pdo, string $siteKey, array $queueRow): array
{
	require_once __DIR__ . '/epc_industry_taxonomy.php';
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$industryKey = (string) ($queueRow['industry_key'] ?? '');
	if ($industryKey === '') {
		$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	}
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower($industryKey));

	$text = epc_apai_advisor_product_text($queueRow);
	$match = epc_apai_match_taxonomy_scored($pdo, $industryKey, $text);

	if ($match && !empty($match['node'])) {
		$node = $match['node'];
		$taxId = (int) ($node['id'] ?? 0);
		$catId = epc_apai_category_for_taxonomy($pdo, $siteKey, $taxId);
		$willCreate = ($catId <= 0);
		$catName = $catId > 0
			? epc_apai_category_name_by_id($pdo, $catId)
			: (string) ($node['name_en'] ?? '');

		return array(
			'ok' => true,
			'category_id' => $catId,
			'category_name' => $catName,
			'taxonomy_slug' => (string) ($node['slug'] ?? ''),
			'taxonomy_node_id' => $taxId,
			'confidence' => (string) ($match['confidence'] ?? 'medium'),
			'create_new' => $willCreate,
			'will_create' => $willCreate,
			'auto_created' => false,
			'action' => $willCreate ? 'create_category' : 'use_existing',
			'match_source' => (string) ($match['source'] ?? ''),
			'industry_key' => $industryKey,
		);
	}

	$title = (string) ($queueRow['title'] ?? 'Product');
	$desc = (string) ($queueRow['description'] ?? '');
	$proposed = epc_apai_propose_category_from_product($title, $desc);

	return array(
		'ok' => true,
		'category_id' => 0,
		'category_name' => $proposed['name'],
		'taxonomy_slug' => $proposed['slug'],
		'taxonomy_node_id' => 0,
		'confidence' => 'medium',
		'create_new' => true,
		'will_create' => true,
		'auto_created' => true,
		'action' => 'create_taxonomy_and_category',
		'proposed_name' => $proposed['name'],
		'proposed_slug' => $proposed['slug'],
		'industry_key' => $industryKey,
	);
}

/**
 * Resolve category_id from advisory + optional override for import.
 *
 * @param array<string,mixed> $options category_id, category_mode (auto|override|create_from_name)
 */
function epc_apai_resolve_import_category(PDO $pdo, string $siteKey, array $queueRow, array $advisory, array $options = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$industryKey = (string) ($advisory['industry_key'] ?? epc_apai_resolve_industry($pdo, $siteKey));
	$mode = (string) ($options['category_mode'] ?? 'auto');
	$overrideId = max(0, (int) ($options['category_id'] ?? 0));

	if ($mode === 'override' && $overrideId > 0) {
		return array(
			'category_id' => $overrideId,
			'taxonomy_node_id' => (int) ($queueRow['taxonomy_node_id'] ?? 0),
			'created' => false,
		);
	}

	if ($mode === 'create_from_name') {
		$proposed = epc_apai_propose_category_from_product(
			(string) ($queueRow['title'] ?? ''),
			(string) ($queueRow['description'] ?? '')
		);
		$created = epc_apai_create_auto_taxonomy_and_category($pdo, $siteKey, $industryKey, $proposed['name'], $proposed['slug']);
		return array(
			'category_id' => (int) ($created['category_id'] ?? 0),
			'taxonomy_node_id' => (int) ($created['taxonomy_node_id'] ?? 0),
			'created' => true,
		);
	}

	$action = (string) ($advisory['action'] ?? 'use_existing');
	if ($action === 'use_existing' && (int) ($advisory['category_id'] ?? 0) > 0) {
		return array(
			'category_id' => (int) $advisory['category_id'],
			'taxonomy_node_id' => (int) ($advisory['taxonomy_node_id'] ?? 0),
			'created' => false,
		);
	}

	if ($action === 'create_category') {
		$taxId = (int) ($advisory['taxonomy_node_id'] ?? 0);
		$catId = epc_apai_ensure_category_for_taxonomy($pdo, $siteKey, $industryKey, $taxId);
		return array(
			'category_id' => $catId,
			'taxonomy_node_id' => $taxId,
			'created' => true,
		);
	}

	if ($action === 'create_taxonomy_and_category') {
		$name = (string) ($advisory['proposed_name'] ?? $advisory['category_name'] ?? 'General products');
		$slug = (string) ($advisory['proposed_slug'] ?? $advisory['taxonomy_slug'] ?? '');
		$created = epc_apai_create_auto_taxonomy_and_category($pdo, $siteKey, $industryKey, $name, $slug);
		return array(
			'category_id' => (int) ($created['category_id'] ?? 0),
			'taxonomy_node_id' => (int) ($created['taxonomy_node_id'] ?? 0),
			'created' => true,
		);
	}

	return array(
		'category_id' => 0,
		'taxonomy_node_id' => (int) ($queueRow['taxonomy_node_id'] ?? 0),
		'created' => false,
	);
}
