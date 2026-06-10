<?php
/**
 * Electronicae / electronics retail — product-line driven storefront (Auto Price AI taxonomy).
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_industry_taxonomy.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_categories.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_apai_product_line_rankings.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_storefront.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_images.php';

function epc_electronicae_storefront_active(): bool
{
	return function_exists('epc_portal_electronics_retail_enabled') && epc_portal_electronics_retail_enabled();
}

function epc_electronicae_site_key(PDO $pdo): string
{
	$sk = epc_apai_resolve_storefront_site_key();
	if ($sk !== '') {
		return $sk;
	}
	return 'electronicae';
}

function epc_electronicae_lang_prefix(): string
{
	return function_exists('epc_apai_storefront_lang_prefix') ? epc_apai_storefront_lang_prefix() : '/en';
}

function epc_electronicae_href(string $path, string $lang = ''): string
{
	if ($lang === '') {
		$lang = epc_electronicae_lang_prefix();
	}
	$path = (string) $path;
	if ($path !== '' && $path[0] === '/') {
		return rtrim($lang, '/') . $path;
	}
	return $path;
}

/** Preferred L1 product lines for homepage grid (slug fragments). */
function epc_electronicae_preferred_line_slugs(): array
{
	return array(
		'cell-phones',
		'computers-laptops',
		'tv-video',
		'gaming',
		'smart-home',
		'headphones',
		'wearables',
		'cameras',
	);
}

/** Icon + accent per taxonomy slug fragment. */
function epc_electronicae_line_visual(string $slug): array
{
	$slug = strtolower($slug);
	$map = array(
		'cell-phones' => array('icon' => 'fa-mobile', 'accent' => '#1a73e8', 'label' => 'Cell Phones'),
		'computers-laptops' => array('icon' => 'fa-laptop', 'accent' => '#5f6368', 'label' => 'Laptops'),
		'laptops' => array('icon' => 'fa-laptop', 'accent' => '#5f6368', 'label' => 'Laptops'),
		'tv-video' => array('icon' => 'fa-television', 'accent' => '#202124', 'label' => 'TV'),
		'televisions' => array('icon' => 'fa-television', 'accent' => '#202124', 'label' => 'TV'),
		'gaming' => array('icon' => 'fa-gamepad', 'accent' => '#e10a0a', 'label' => 'Gaming'),
		'smart-home' => array('icon' => 'fa-home', 'accent' => '#34a853', 'label' => 'Smart Home'),
		'headphones' => array('icon' => 'fa-headphones', 'accent' => '#9334e6', 'label' => 'Headphones'),
		'wearables' => array('icon' => 'fa-heartbeat', 'accent' => '#ea4335', 'label' => 'Wearables'),
		'cameras' => array('icon' => 'fa-camera', 'accent' => '#fbbc04', 'label' => 'Camera'),
		'computers-tablets' => array('icon' => 'fa-tablet', 'accent' => '#4285f4', 'label' => 'Tablets'),
		'audio' => array('icon' => 'fa-volume-up', 'accent' => '#9334e6', 'label' => 'Audio'),
	);
	foreach ($map as $needle => $meta) {
		if (strpos($slug, $needle) !== false) {
			return $meta;
		}
	}
	return array('icon' => 'fa-microchip', 'accent' => '#e10a0a', 'label' => '');
}

function epc_electronicae_normalize_image_url(string $path): string
{
	$path = trim($path);
	if ($path === '') {
		return '';
	}
	if (preg_match('#^https?://#i', $path)) {
		return $path;
	}
	return epc_product_image_url($path);
}

function epc_electronicae_root_category_id(PDO $pdo, string $siteKey, string $industryKey = ''): int
{
	if ($industryKey === '') {
		$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	}
	$rootAlias = epc_apai_category_slug($industryKey, 'apai-root');
	$stmt = $pdo->prepare('SELECT `id`, `url` FROM `shop_catalogue_categories` WHERE `alias` = ? AND `parent` = 0 LIMIT 1');
	$stmt->execute(array($rootAlias));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	return $row ? (int) ($row['id'] ?? 0) : 0;
}

function epc_electronicae_category_url(PDO $pdo, string $siteKey, int $taxonomyNodeId): string
{
	$catId = epc_apai_category_for_taxonomy($pdo, $siteKey, $taxonomyNodeId);
	if ($catId <= 0) {
		return '';
	}
	$stmt = $pdo->prepare('SELECT `url` FROM `shop_catalogue_categories` WHERE `id` = ? LIMIT 1');
	$stmt->execute(array($catId));
	return trim((string) ($stmt->fetchColumn() ?: ''), '/');
}

function epc_electronicae_line_product_image(PDO $pdo, string $siteKey, int $taxonomyNodeId): string
{
	$flat = epc_apai_tax_flat_for_industry($pdo, epc_apai_resolve_industry($pdo, $siteKey));
	$descMap = epc_apai_tax_descendant_map($flat);
	$ids = $descMap[$taxonomyNodeId] ?? array($taxonomyNodeId);
	if (!$ids) {
		return '';
	}
	$ph = implode(',', array_fill(0, count($ids), '?'));
	$params = array_merge(array($siteKey), $ids);
	$stmt = $pdo->prepare(
		'SELECT q.`product_id`
		 FROM `epc_product_discovery_queue` q
		 WHERE q.`site_key` = ? AND q.`status` = \'imported\' AND q.`product_id` > 0
		   AND q.`taxonomy_node_id` IN (' . $ph . ')
		 ORDER BY q.`updated_at` DESC
		 LIMIT 1'
	);
	$stmt->execute($params);
	$productId = (int) $stmt->fetchColumn();
	if ($productId <= 0) {
		return '';
	}
	$imgStmt = $pdo->prepare(
		'SELECT `file_name` FROM `shop_products_images` WHERE `product_id` = ? ORDER BY `id` ASC LIMIT 1'
	);
	$imgStmt->execute(array($productId));
	$file = (string) ($imgStmt->fetchColumn() ?: '');
	return $file !== '' ? epc_electronicae_normalize_image_url($file) : '';
}

function epc_electronicae_line_tile(PDO $pdo, string $siteKey, array $line): array
{
	$nodeId = (int) ($line['id'] ?? 0);
	$slug = (string) ($line['slug'] ?? '');
	$name = (string) ($line['name_en'] ?? '');
	$visual = epc_electronicae_line_visual($slug);
	if ($name === '' && $visual['label'] !== '') {
		$name = $visual['label'];
	}
	$url = epc_electronicae_category_url($pdo, $siteKey, $nodeId);
	$href = $url !== '' ? '/' . $url : '/shop/search?q=' . rawurlencode($name);

	$image = '';
	if (!empty($line['preview_image'])) {
		$image = epc_electronicae_normalize_image_url((string) $line['preview_image']);
	}
	if ($image === '') {
		$image = epc_electronicae_line_product_image($pdo, $siteKey, $nodeId);
	}

	$productCount = epc_electronicae_line_product_count($pdo, $siteKey, $nodeId);
	$imported = (int) ($line['imported_count'] ?? 0);

	return array(
		'id' => $nodeId,
		'name' => $name,
		'slug' => $slug,
		'href' => $href,
		'image' => $image,
		'icon' => $visual['icon'],
		'accent' => $visual['accent'],
		'product_count' => max($productCount, $imported),
		'trend' => (string) ($line['trend'] ?? ''),
		'score' => (int) ($line['score'] ?? 0),
	);
}

function epc_electronicae_line_product_count(PDO $pdo, string $siteKey, int $taxonomyNodeId): int
{
	$flat = epc_apai_tax_flat_for_industry($pdo, epc_apai_resolve_industry($pdo, $siteKey));
	$descMap = epc_apai_tax_descendant_map($flat);
	$ids = $descMap[$taxonomyNodeId] ?? array($taxonomyNodeId);
	if (!$ids) {
		return 0;
	}
	$ph = implode(',', array_fill(0, count($ids), '?'));
	$params = array_merge(array($siteKey), $ids);
	$stmt = $pdo->prepare(
		'SELECT COUNT(DISTINCT q.`product_id`)
		 FROM `epc_product_discovery_queue` q
		 INNER JOIN `shop_catalogue_products` scp ON scp.`id` = q.`product_id` AND scp.`published_flag` = 1
		 WHERE q.`site_key` = ? AND q.`status` = \'imported\' AND q.`taxonomy_node_id` IN (' . $ph . ')'
	);
	$stmt->execute($params);
	return (int) $stmt->fetchColumn();
}

/**
 * Top product-line tiles for homepage (8–12 lines, real images + CHPU URLs).
 *
 * @return array<int,array<string,mixed>>
 */
function epc_electronicae_product_line_tiles(PDO $pdo, string $siteKey = '', int $limit = 12): array
{
	if ($siteKey === '') {
		$siteKey = epc_electronicae_site_key($pdo);
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_perf_cache.php';
	$cacheKey = 'epc_pl_tiles:v1:' . $siteKey . ':' . $limit;
	return epc_perf_cache_remember($cacheKey, 900, static function () use ($pdo, $siteKey, $limit) {
		epc_apai_sync_categories($pdo, $siteKey);
		$data = epc_apai_product_line_rankings($pdo, $siteKey);
		$rankings = (array) ($data['rankings'] ?? array());

		$bySlug = array();
		foreach ($rankings as $line) {
			if ((int) ($line['level'] ?? 0) !== 1) {
				continue;
			}
			$bySlug[(string) ($line['slug'] ?? '')] = $line;
		}

		$chosen = array();
		foreach (epc_electronicae_preferred_line_slugs() as $pref) {
			foreach ($bySlug as $slug => $line) {
				if (strpos($slug, $pref) === 0 || $slug === $pref) {
					$chosen[$slug] = $line;
					break;
				}
			}
		}
		foreach ($rankings as $line) {
			if ((int) ($line['level'] ?? 0) !== 1) {
				continue;
			}
			$slug = (string) ($line['slug'] ?? '');
			if (!isset($chosen[$slug]) && count($chosen) < $limit) {
				$chosen[$slug] = $line;
			}
		}

		$tiles = array();
		foreach (array_slice(array_values($chosen), 0, $limit) as $line) {
			$tiles[] = epc_electronicae_line_tile($pdo, $siteKey, $line);
		}
		return $tiles;
	});
}

/**
 * Mega-nav items from synced product lines (L1 taxonomy).
 *
 * @return array<int,array{label:string,href:string,highlight?:bool,count?:int}>
 */
function epc_electronicae_mega_nav(PDO $pdo, string $siteKey = ''): array
{
	$tiles = epc_electronicae_product_line_tiles($pdo, $siteKey, 12);
	$nav = array();
	foreach ($tiles as $tile) {
		$nav[] = array(
			'label' => (string) ($tile['name'] ?? ''),
			'href' => (string) ($tile['href'] ?? '/'),
			'count' => (int) ($tile['product_count'] ?? 0),
		);
	}
	if ($nav) {
		$nav[] = array('label' => 'All product lines', 'href' => epc_electronicae_all_lines_href($pdo, $siteKey), 'highlight' => true);
	}
	return $nav;
}

function epc_electronicae_all_lines_href(PDO $pdo, string $siteKey = ''): string
{
	if ($siteKey === '') {
		$siteKey = epc_electronicae_site_key($pdo);
	}
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$rootAlias = epc_apai_category_slug($industryKey, 'apai-root');
	$stmt = $pdo->prepare('SELECT `url` FROM `shop_catalogue_categories` WHERE `alias` = ? LIMIT 1');
	$stmt->execute(array($rootAlias));
	$url = trim((string) ($stmt->fetchColumn() ?: ''), '/');
	return $url !== '' ? '/' . $url : '/shop/catalogue';
}

/**
 * Hero carousel slides driven by top product lines (real catalogue images).
 *
 * @return array<int,array<string,mixed>>
 */
function epc_electronicae_hero_slides(PDO $pdo, string $siteKey = ''): array
{
	$tiles = array_slice(epc_electronicae_product_line_tiles($pdo, $siteKey, 4), 0, 4);
	$slides = array();
	$tones = array('dark', 'light', 'dark', 'light');
	foreach ($tiles as $i => $tile) {
		if ((string) ($tile['name'] ?? '') === '') {
			continue;
		}
		$slides[] = array(
			'title' => (string) $tile['name'],
			'sub' => ((int) ($tile['product_count'] ?? 0) > 0)
				? 'Shop verified UAE listings — official warranty, prices in AED'
				: 'New arrivals coming soon — browse the full range',
			'cta' => 'Shop ' . (string) $tile['name'],
			'href' => (string) ($tile['href'] ?? '/'),
			'image' => (string) ($tile['image'] ?? ''),
			'icon' => (string) ($tile['icon'] ?? 'fa-microchip'),
			'accent' => (string) ($tile['accent'] ?? '#e10a0a'),
			'alt' => (string) $tile['name'],
			'tone' => $tones[$i % count($tones)],
		);
	}
	return $slides;
}

function epc_electronicae_product_card(PDO $pdo, array $row, string $productUrlMode = 'alias'): array
{
	$pid = (int) ($row['product_id'] ?? $row['id'] ?? 0);
	$href = '';
	if (function_exists('epc_apai_catalogue_product_path')) {
		$href = epc_apai_catalogue_product_path($row, $productUrlMode);
	}
	if ($href === '') {
		$catUrl = trim((string) ($row['category_url'] ?? ''), '/');
		$slug = ($productUrlMode === 'id') ? (string) $pid : (string) ($row['alias'] ?? '');
		$href = ($catUrl !== '' && $slug !== '') ? '/' . $catUrl . '/' . $slug : '/shop/products/product?id=' . $pid;
	}

	$image = '';
	if (!empty($row['file_name'])) {
		$image = epc_electronicae_normalize_image_url((string) $row['file_name']);
	}
	$price = (float) ($row['price'] ?? 0);
	$brand = (string) ($row['manufacturer'] ?? '');
	$displayName = (string) ($row['caption'] ?? $row['title'] ?? '');
	if ($brand === '' && $displayName !== '') {
		$parts = preg_split('/\s+/', trim($displayName), 2);
		$brand = strtoupper((string) ($parts[0] ?? ''));
	}

	return array(
		'id' => $pid,
		'brand' => $brand,
		'name' => $displayName,
		'price' => $price,
		'was' => 0,
		'image' => $image,
		'href' => $href,
		'alt' => $displayName,
		'sku' => '',
	);
}

/**
 * Imported products grouped by L1 product line for homepage deal rows.
 *
 * @return array<int,array{title:string,href:string,products:array}>
 */
function epc_electronicae_home_product_sections(PDO $pdo, string $siteKey = '', int $sectionLimit = 3, int $productsPerSection = 6): array
{
	if ($siteKey === '') {
		$siteKey = epc_electronicae_site_key($pdo);
	}
	global $DP_Config;
	$productUrlMode = is_object($DP_Config) ? (string) ($DP_Config->product_url ?? 'alias') : 'alias';

	$tiles = epc_electronicae_product_line_tiles($pdo, $siteKey, $sectionLimit + 2);
	$sections = array();
	foreach ($tiles as $tile) {
		if (count($sections) >= $sectionLimit) {
			break;
		}
		$nodeId = (int) ($tile['id'] ?? 0);
		if ($nodeId <= 0) {
			continue;
		}
		$flat = epc_apai_tax_flat_for_industry($pdo, epc_apai_resolve_industry($pdo, $siteKey));
		$descMap = epc_apai_tax_descendant_map($flat);
		$taxIds = $descMap[$nodeId] ?? array($nodeId);
		$ph = implode(',', array_fill(0, count($taxIds), '?'));
		$params = array_merge(array($siteKey), $taxIds);
		$stmt = $pdo->prepare(
			'SELECT scp.`id`, scp.`alias`, scp.`caption`,
			        scc.`url` AS `category_url`, q.`title`,
			        (SELECT MIN(sd.`price`) FROM `shop_storages_data` sd WHERE sd.`product_id` = scp.`id` AND sd.`price` > 0) AS `price`,
			        (SELECT spi.`file_name` FROM `shop_products_images` spi WHERE spi.`product_id` = scp.`id` ORDER BY spi.`id` ASC LIMIT 1) AS `file_name`
			 FROM `epc_product_discovery_queue` q
			 INNER JOIN `shop_catalogue_products` scp ON scp.`id` = q.`product_id` AND scp.`published_flag` = 1
			 LEFT JOIN `shop_catalogue_categories` scc ON scc.`id` = scp.`category_id`
			 WHERE q.`site_key` = ? AND q.`status` = \'imported\' AND q.`taxonomy_node_id` IN (' . $ph . ')
			 ORDER BY q.`updated_at` DESC
			 LIMIT ' . (int) $productsPerSection
		);
		$stmt->execute($params);
		$products = array();
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$card = epc_electronicae_product_card($pdo, $row, $productUrlMode);
			if ($card['image'] !== '') {
				$products[] = $card;
			}
		}
		if (!$products) {
			continue;
		}
		$sections[] = array(
			'title' => (string) ($tile['name'] ?? 'Featured'),
			'href' => (string) ($tile['href'] ?? '/'),
			'products' => $products,
		);
	}
	return $sections;
}

/**
 * Keep only Auto Price AI industry tree in catalogue menu (hide autoparts orphans).
 *
 * @param array<int,array<string,mixed>> $tree
 * @return array<int,array<string,mixed>>
 */
function epc_electronicae_filter_menu_tree(PDO $pdo, array $tree, string $siteKey = ''): array
{
	if ($siteKey === '') {
		$siteKey = epc_electronicae_site_key($pdo);
	}
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	$rootAlias = epc_apai_category_slug($industryKey, 'apai-root');

	$extract = function (array $nodes) use (&$extract, $rootAlias): array {
		foreach ($nodes as $node) {
			$alias = (string) ($node['alias'] ?? '');
			if ($alias === $rootAlias) {
				$children = (array) ($node['data'] ?? array());
				return $children ?: array($node);
			}
			$sub = (array) ($node['data'] ?? array());
			if ($sub) {
				$found = $extract($sub);
				if ($found) {
					return $found;
				}
			}
		}
		return array();
	};

	$filtered = $extract($tree);
	if ($filtered) {
		return $filtered;
	}

	$out = array();
	foreach ($tree as $node) {
		$alias = (string) ($node['alias'] ?? '');
		if (strpos($alias, 'apai-') === 0) {
			$out[] = $node;
		}
	}
	return $out ?: $tree;
}

function epc_electronicae_category_subtree_ids(PDO $pdo, int $categoryId): array
{
	$ids = array($categoryId);
	$walk = function (int $parentId) use (&$walk, &$ids, $pdo): void {
		$childStmt = $pdo->prepare(
			'SELECT `id` FROM `shop_catalogue_categories` WHERE `parent` = ? AND `published_flag` = 1'
		);
		$childStmt->execute(array($parentId));
		while ($cid = (int) $childStmt->fetchColumn()) {
			$ids[] = $cid;
			$walk($cid);
		}
	};
	$walk($categoryId);
	return array_values(array_unique(array_filter($ids)));
}

function epc_electronicae_category_sql_in(PDO $pdo, int $categoryId): string
{
	$ids = epc_electronicae_category_subtree_ids($pdo, $categoryId);
	if (!$ids) {
		return (string) (int) $categoryId;
	}
	return implode(',', array_map('intval', $ids));
}

{
	if ($categoryId <= 0) {
		return false;
	}
	$stmt = $pdo->prepare(
		'SELECT COUNT(*) FROM `shop_catalogue_products` WHERE `category_id` = ? AND `published_flag` = 1 LIMIT 1'
	);
	$stmt->execute(array($categoryId));
	if ((int) $stmt->fetchColumn() > 0) {
		return true;
	}
	$walk = function (int $parentId) use (&$walk, $pdo): bool {
		$childStmt = $pdo->prepare(
			'SELECT `id` FROM `shop_catalogue_categories` WHERE `parent` = ? AND `published_flag` = 1'
		);
		$childStmt->execute(array($parentId));
		while ($cid = (int) $childStmt->fetchColumn()) {
			$pStmt = $pdo->prepare(
				'SELECT COUNT(*) FROM `shop_catalogue_products` WHERE `category_id` = ? AND `published_flag` = 1'
			);
			$pStmt->execute(array($cid));
			if ((int) $pStmt->fetchColumn() > 0) {
				return true;
			}
			if ($walk($cid)) {
				return true;
			}
		}
		return false;
	};
	return $walk($categoryId);
}

/**
 * When a branch has imported products, prefer product grid over empty subcategory tiles.
 */
function epc_electronicae_category_prefers_products(PDO $pdo, int $categoryId, int $childCount): bool
{
	if ($childCount <= 0) {
		return true;
	}
	return epc_electronicae_category_has_products($pdo, $categoryId);
}

function epc_electronicae_render_empty_category(): string
{
	return '<div class="epc-el-empty-category"><div class="epc-el-empty-category__inner">'
		. '<i class="fa fa-clock-o" aria-hidden="true"></i>'
		. '<h3>Coming soon</h3>'
		. '<p>New products for this category are being added. Check back shortly or browse our featured product lines.</p>'
		. '<a class="epc-er-btn epc-er-btn--primary" href="' . htmlspecialchars(epc_electronicae_href('/'), ENT_QUOTES, 'UTF-8') . '">Back to shop</a>'
		. '</div></div>';
}
