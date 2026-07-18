<?php
/**
 * Accessories marketplace catalog — UAE warehouse rows classified into accessories taxonomy.
 */
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_accessories_taxonomy.php';

if (!function_exists('epc_acc_cache_path')) {
	function epc_acc_cache_path(): string
	{
		$doc = isset($_SERVER['DOCUMENT_ROOT']) ? (string) $_SERVER['DOCUMENT_ROOT'] : 'epartscart';
		return rtrim(sys_get_temp_dir(), '/\\') . '/epc_acc_catalog_v1_' . md5($doc) . '.json';
	}
}

if (!function_exists('epc_acc_fetch_raw_rows')) {
	/**
	 * @return array<int, array<string, mixed>>
	 */
	function epc_acc_fetch_raw_rows(PDO $db, int $limit = 8000): array
	{
		$limit = max(200, min($limit, 12000));
		$article_show_expr = "COALESCE(NULLIF(TRIM(`article_show`), ''), TRIM(`article`))";
		// Prefer storage caption from join when available; fall back to storage column.
		$sql = 'SELECT TRIM(p.`manufacturer`) AS `brand`, TRIM(p.`article`) AS `article`, '
			. $article_show_expr . ' AS `article_show`, '
			. 'TRIM(IFNULL(p.`name`, \'\')) AS `name`, IFNULL(p.`exist`, 0) AS `qty`, IFNULL(p.`price`, 0) AS `price`, '
			. 'TRIM(IFNULL(p.`storage`, \'\')) AS `warehouse`, IFNULL(p.`price_id`, 0) AS `price_id` '
			. 'FROM `shop_docpart_prices_data` p '
			. 'WHERE IFNULL(p.`price`, 0) > 0 AND IFNULL(p.`exist`, 0) > 0 '
			. 'AND TRIM(IFNULL(p.`manufacturer`, \'\')) != \'\' AND TRIM(p.`article`) != \'\' '
			. 'ORDER BY IFNULL(p.`exist`, 0) DESC LIMIT ' . (int) $limit;
		try {
			return $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
		} catch (Exception $e) {
			return array();
		}
	}
}

if (!function_exists('epc_acc_build_items')) {
	/**
	 * @param array<int, array<string, mixed>> $rows
	 * @return array<int, array<string, mixed>>
	 */
	function epc_acc_build_items(array $rows): array
	{
		$regions = epc_acc_warehouse_regions();
		$merged = array();
		foreach ($rows as $row) {
			$brand = mb_strtoupper(trim((string) ($row['brand'] ?? '')), 'UTF-8');
			$article = trim((string) ($row['article_show'] ?? $row['article'] ?? ''));
			$article_norm = docpart_normalize_article_for_price($article);
			if ($brand === '' || $article_norm === '') {
				continue;
			}
			$qty = (float) ($row['qty'] ?? 0);
			if ($qty <= 0 || $qty > 50000) {
				continue;
			}
			$price = (float) ($row['price'] ?? 0);
			if ($price <= 0) {
				continue;
			}
			$name = trim((string) ($row['name'] ?? ''));
			$warehouse = trim((string) ($row['warehouse'] ?? ''));
			$key = $brand . '|' . $article_norm;
			if (!isset($merged[$key])) {
				$class = epc_acc_classify($name !== '' ? $name : $article, $brand);
				$region = isset($regions[$warehouse]) ? $regions[$warehouse] : ($warehouse !== '' ? $warehouse : 'UAE warehouse');
				$merged[$key] = array(
					'id' => substr(md5($key), 0, 12),
					'brand' => $brand,
					'article' => $article,
					'article_norm' => $article_norm,
					'name' => $name !== '' ? $name : ($brand . ' ' . $article),
					'qty' => $qty,
					'price' => $price,
					'warehouse' => $warehouse,
					'region' => $region,
					'category' => $class['category'],
					'subcategory' => $class['subcategory'],
					'category_label' => $class['category_label'],
					'subcategory_label' => $class['subcategory_label'],
				);
				continue;
			}
			$merged[$key]['qty'] += $qty;
			if ($price < (float) $merged[$key]['price']) {
				$merged[$key]['price'] = $price;
			}
			if ($merged[$key]['name'] === ($brand . ' ' . $article) && $name !== '') {
				$merged[$key]['name'] = $name;
				$class = epc_acc_classify($name, $brand);
				$merged[$key]['category'] = $class['category'];
				$merged[$key]['subcategory'] = $class['subcategory'];
				$merged[$key]['category_label'] = $class['category_label'];
				$merged[$key]['subcategory_label'] = $class['subcategory_label'];
			}
			if ($merged[$key]['warehouse'] === '' && $warehouse !== '') {
				$merged[$key]['warehouse'] = $warehouse;
				$merged[$key]['region'] = isset($regions[$warehouse]) ? $regions[$warehouse] : $warehouse;
			}
		}
		return array_values($merged);
	}
}

if (!function_exists('epc_acc_load_catalog')) {
	/**
	 * @return array<int, array<string, mixed>>
	 */
	function epc_acc_load_catalog(PDO $db, bool $refresh = false): array
	{
		$path = epc_acc_cache_path();
		if (!$refresh && is_readable($path)) {
			$raw = @file_get_contents($path);
			$data = is_string($raw) ? json_decode($raw, true) : null;
			if (is_array($data) && !empty($data['built_at']) && !empty($data['items'])
				&& (time() - (int) $data['built_at']) < 600) {
				return $data['items'];
			}
		}
		$items = epc_acc_build_items(epc_acc_fetch_raw_rows($db, 9000));
		@file_put_contents($path, json_encode(array(
			'built_at' => time(),
			'items' => $items,
		), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES));
		return $items;
	}
}

if (!function_exists('epc_acc_search')) {
	/**
	 * @param array<string, mixed> $filters
	 * @return array{total:int, page:int, per_page:int, from:int, to:int, items:array, facets:array, taxonomy:array}
	 */
	function epc_acc_search(PDO $db, array $filters = array()): array
	{
		$items = epc_acc_load_catalog($db, !empty($filters['refresh']));
		$q = isset($filters['q']) ? trim((string) $filters['q']) : '';
		$qNorm = $q !== '' ? docpart_normalize_article_for_price($q) : '';
		$qLower = mb_strtolower($q, 'UTF-8');
		$category = isset($filters['category']) ? trim((string) $filters['category']) : '';
		$subcategory = isset($filters['subcategory']) ? trim((string) $filters['subcategory']) : '';
		$brand = isset($filters['brand']) ? mb_strtoupper(trim((string) $filters['brand']), 'UTF-8') : '';
		$region = isset($filters['region']) ? trim((string) $filters['region']) : '';
		$warehouse = isset($filters['warehouse']) ? trim((string) $filters['warehouse']) : '';
		$priceMin = isset($filters['price_min']) ? (float) $filters['price_min'] : 0.0;
		$priceMax = isset($filters['price_max']) ? (float) $filters['price_max'] : 0.0;
		$inStock = !isset($filters['in_stock']) || (string) $filters['in_stock'] !== '0';
		$sort = isset($filters['sort']) ? (string) $filters['sort'] : 'price-desc';
		$page = max(1, isset($filters['page']) ? (int) $filters['page'] : 1);
		$perPage = isset($filters['per_page']) ? (int) $filters['per_page'] : 24;
		$perPage = max(12, min($perPage, 60));

		$filtered = array();
		foreach ($items as $item) {
			if ($inStock && (float) $item['qty'] <= 0) {
				continue;
			}
			if ($category !== '' && $item['category'] !== $category) {
				continue;
			}
			if ($subcategory !== '' && $item['subcategory'] !== $subcategory) {
				continue;
			}
			if ($brand !== '' && $item['brand'] !== $brand) {
				continue;
			}
			if ($warehouse !== '' && strcasecmp((string) $item['warehouse'], $warehouse) !== 0) {
				continue;
			}
			if ($region !== '' && strcasecmp((string) $item['region'], $region) !== 0) {
				continue;
			}
			if ($priceMin > 0 && (float) $item['price'] < $priceMin) {
				continue;
			}
			if ($priceMax > 0 && (float) $item['price'] > $priceMax) {
				continue;
			}
			if ($q !== '') {
				$hay = mb_strtolower($item['name'] . ' ' . $item['brand'] . ' ' . $item['article'], 'UTF-8');
				$match = (mb_strpos($hay, $qLower) !== false)
					|| ($qNorm !== '' && $item['article_norm'] === $qNorm)
					|| ($qNorm !== '' && mb_strpos($item['article_norm'], $qNorm) !== false);
				if (!$match) {
					continue;
				}
			}
			$filtered[] = $item;
		}

		usort($filtered, function ($a, $b) use ($sort) {
			switch ($sort) {
				case 'price-asc':
					return ((float) $a['price']) <=> ((float) $b['price']);
				case 'qty-desc':
					return ((float) $b['qty']) <=> ((float) $a['qty']);
				case 'name-asc':
					return strcasecmp((string) $a['name'], (string) $b['name']);
				case 'updated-desc':
				case 'top-sales':
					return ((float) $b['qty']) <=> ((float) $a['qty']);
				case 'price-desc':
				default:
					return ((float) $b['price']) <=> ((float) $a['price']);
			}
		});

		$total = count($filtered);
		$pages = max(1, (int) ceil($total / $perPage));
		if ($page > $pages) {
			$page = $pages;
		}
		$offset = ($page - 1) * $perPage;
		$slice = array_slice($filtered, $offset, $perPage);
		$from = $total === 0 ? 0 : ($offset + 1);
		$to = min($total, $offset + count($slice));

		$facetCats = array();
		$facetBrands = array();
		$facetRegions = array();
		foreach ($filtered as $item) {
			$c = $item['category'];
			if (!isset($facetCats[$c])) {
				$facetCats[$c] = array(
					'slug' => $c,
					'label' => $item['category_label'],
					'count' => 0,
					'subs' => array(),
				);
			}
			$facetCats[$c]['count']++;
			$s = $item['subcategory'];
			if (!isset($facetCats[$c]['subs'][$s])) {
				$facetCats[$c]['subs'][$s] = array(
					'slug' => $s,
					'label' => $item['subcategory_label'],
					'count' => 0,
				);
			}
			$facetCats[$c]['subs'][$s]['count']++;

			$b = $item['brand'];
			if (!isset($facetBrands[$b])) {
				$facetBrands[$b] = 0;
			}
			$facetBrands[$b]++;

			$r = (string) $item['region'];
			if ($r !== '') {
				if (!isset($facetRegions[$r])) {
					$facetRegions[$r] = 0;
				}
				$facetRegions[$r]++;
			}
		}
		foreach ($facetCats as &$catRef) {
			$catRef['subs'] = array_values($catRef['subs']);
			usort($catRef['subs'], function ($a, $b) {
				return $b['count'] <=> $a['count'];
			});
		}
		unset($catRef);
		$facetCats = array_values($facetCats);
		usort($facetCats, function ($a, $b) {
			return $b['count'] <=> $a['count'];
		});
		arsort($facetBrands);
		arsort($facetRegions);
		$brandList = array();
		foreach ($facetBrands as $bName => $cnt) {
			$brandList[] = array('brand' => $bName, 'count' => $cnt);
			if (count($brandList) >= 40) {
				break;
			}
		}
		$regionList = array();
		foreach ($facetRegions as $rName => $cnt) {
			$regionList[] = array('region' => $rName, 'count' => $cnt);
		}

		$taxOut = array();
		foreach (epc_acc_taxonomy() as $slug => $cat) {
			$taxOut[] = array(
				'slug' => $slug,
				'label' => $cat['label'],
				'icon' => isset($cat['icon']) ? $cat['icon'] : 'fa-tag',
				'subs' => array_map(function ($subSlug, $sub) {
					return array('slug' => $subSlug, 'label' => $sub['label']);
				}, array_keys($cat['subs']), array_values($cat['subs'])),
			);
		}

		return array(
			'total' => $total,
			'page' => $page,
			'per_page' => $perPage,
			'pages' => $pages,
			'from' => $from,
			'to' => $to,
			'items' => $slice,
			'facets' => array(
				'categories' => $facetCats,
				'brands' => $brandList,
				'regions' => $regionList,
			),
			'taxonomy' => $taxOut,
			'sort' => $sort,
		);
	}
}
