<?php
/**
 * Product Family catalog — UAE in-stock price list grouped by product line.
 * Does not load demand-intelligence at bootstrap (keeps catalog working if demand helpers are missing).
 */
require_once __DIR__ . '/docpart_article_match.php';

function epc_pf_load_demand_helpers(): void
{
	static $loaded = false;
	if ($loaded) {
		return;
	}
	$loaded = true;
	$path = __DIR__ . '/epc_demand_intelligence.php';
	if (is_readable($path)) {
		require_once $path;
	}
}

/**
 * @return array<int, array{brand:string, article:string, article_show:string, name:string, qty:float, price:mixed, warehouse:string, article_norm:string}>
 */
function epc_pf_map_stock_rows(array $rows): array
{
	if (function_exists('epc_demand_map_stock_rows')) {
		return epc_demand_map_stock_rows($rows);
	}
	$out = array();
	foreach ($rows as $row) {
		$brand = trim((string)($row['brand'] ?? ''));
		$article = trim((string)($row['article_show'] ?? $row['article'] ?? ''));
		if ($brand === '' || $article === '') {
			continue;
		}
		$out[] = array(
			'brand' => $brand,
			'article' => $article,
			'article_show' => $article,
			'name' => trim((string)($row['name'] ?? '')),
			'qty' => (float)($row['qty'] ?? 0),
			'price' => $row['price'] ?? '',
			'warehouse' => trim((string)($row['warehouse'] ?? '')),
			'article_norm' => docpart_normalize_article_for_price($row['article'] ?? $article),
		);
	}
	return $out;
}

function epc_pf_infer_product_group(string $umapi_group, string $part_name): string
{
	if (function_exists('epc_demand_infer_product_group')) {
		return epc_demand_infer_product_group($umapi_group, $part_name);
	}
	$group = trim($umapi_group);
	if ($group !== '') {
		return $group;
	}
	$hay = mb_strtolower($part_name, 'UTF-8');
	$rules = array(
		'Piston' => array('piston'),
		'Gasket' => array('gasket'),
		'Oil filter' => array('oil filter'),
		'Filter' => array('filter'),
		'Brake' => array('brake pad', 'brake disc'),
		'Bearing' => array('bearing'),
		'Engine' => array('engine', 'cylinder', 'valve'),
	);
	foreach ($rules as $label => $needles) {
		foreach ($needles as $needle) {
			if ($needle !== '' && strpos($hay, $needle) !== false) {
				return $label;
			}
		}
	}
	return $part_name !== '' ? 'Other parts' : 'Uncategorized';
}

function epc_pf_ensure_umapi_group_schema(PDO $db): void
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_umapi_product_group` (
		`manufacturer` VARCHAR(128) NOT NULL,
		`article_norm` VARCHAR(64) NOT NULL,
		`product_group` VARCHAR(128) NOT NULL DEFAULT '',
		`umapi_raw` VARCHAR(255) NOT NULL DEFAULT '',
		`updated_at` INT UNSIGNED NOT NULL DEFAULT 0,
		PRIMARY KEY (`manufacturer`, `article_norm`),
		KEY `product_group` (`product_group`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8;");
}

/**
 * Top brands by UAE qty for card preview.
 *
 * @param array<int, array{brand:string, parts_count?:int, total_qty?:float}> $brands
 * @return array{top: array, more_count: int, total: int}
 */
function epc_pf_top_brands_slice(array $brands, int $limit = 10): array
{
	$list = array_values($brands);
	usort($list, function ($a, $b) {
		$qa = (float)($a['total_qty'] ?? 0);
		$qb = (float)($b['total_qty'] ?? 0);
		if ($qa !== $qb) {
			return $qb <=> $qa;
		}
		return strcmp((string)($a['brand'] ?? ''), (string)($b['brand'] ?? ''));
	});
	$top = array_slice($list, 0, max(1, $limit));
	return array(
		'top' => $top,
		'more_count' => max(0, count($list) - count($top)),
		'total' => count($list),
	);
}

/**
 * UMAPI article PT_DES (cached) → product family label; falls back to name rules.
 */
function epc_pf_resolve_product_group($DP_Config, ?PDO $db, string $brand, string $article, string $name, int &$umapi_budget): string
{
	$brand = trim($brand);
	$article_norm = docpart_normalize_article_for_price($article);
	$name = trim($name);
	if ($brand === '' || $article_norm === '') {
		return epc_pf_infer_product_group('', $name);
	}

	$umapi_raw = '';
	if ($db instanceof PDO) {
		epc_pf_ensure_umapi_group_schema($db);
		try {
			$stmt = $db->prepare(
				'SELECT `product_group`, `umapi_raw` FROM `epc_umapi_product_group`
				 WHERE `manufacturer` = ? AND `article_norm` = ? LIMIT 1'
			);
			$stmt->execute(array($brand, $article_norm));
			$cached = $stmt->fetch(PDO::FETCH_ASSOC);
			if ($cached && trim((string)($cached['product_group'] ?? '')) !== '') {
				return (string)$cached['product_group'];
			}
			if ($cached && trim((string)($cached['umapi_raw'] ?? '')) !== '') {
				$umapi_raw = (string)$cached['umapi_raw'];
			}
		} catch (Throwable $e) {
		}
	}

	$art_id = 0;
	if ($umapi_raw === '' && is_object($DP_Config) && $umapi_budget > 0) {
		epc_pf_load_demand_helpers();
		if (!function_exists('epc_demand_resolve_umapi_art_id')) {
			return epc_pf_infer_product_group('', $name);
		}
		$umapi_budget--;
		$art_id = epc_demand_resolve_umapi_art_id($DP_Config, $brand, $article);
		if ($art_id > 0) {
			$base = epc_demand_site_base($DP_Config);
			if ($base !== '') {
				$detail = epc_demand_http_json(
					$base . '/api/umapi_proxy.php?' . http_build_query(array('action' => 'article', 'id' => $art_id, 'language' => 'en')),
					12
				);
				if (is_array($detail)) {
					$umapi_raw = trim((string)($detail['PT_DES'] ?? $detail['PRODUCT_GROUP'] ?? $detail['ART_PRODUCT_NAME'] ?? ''));
					if ($umapi_raw === '') {
						$umapi_raw = trim((string)($detail['COMPLETE_DES'] ?? $detail['DES'] ?? ''));
					}
				}
			}
		}
		if ($db instanceof PDO && ($umapi_raw !== '' || $art_id > 0)) {
			try {
				$label = epc_pf_infer_product_group($umapi_raw, $name);
				$db->prepare(
					'INSERT INTO `epc_umapi_product_group` (`manufacturer`, `article_norm`, `product_group`, `umapi_raw`, `updated_at`)
					 VALUES (?, ?, ?, ?, ?)
					 ON DUPLICATE KEY UPDATE `product_group` = VALUES(`product_group`), `umapi_raw` = VALUES(`umapi_raw`), `updated_at` = VALUES(`updated_at`)'
				)->execute(array($brand, $article_norm, $label, $umapi_raw, time()));
				if ($label !== '' && $label !== 'Other parts') {
					return $label;
				}
			} catch (Throwable $e) {
			}
		}
	}

	if ($umapi_raw !== '') {
		$from_umapi = epc_pf_infer_product_group($umapi_raw, $name);
		if ($from_umapi !== '') {
			return $from_umapi;
		}
	}

	return epc_pf_infer_product_group('', $name);
}

/**
 * In-stock UAE lines for product-family grouping (capped for response time).
 *
 * @return array<int, array<string, mixed>>
 */
function epc_pf_fetch_catalog_lines(PDO $db, int $fetch_limit = 2500): array
{
	$fetch_limit = max(100, min($fetch_limit, 5000));
	$article_show_expr = "COALESCE(NULLIF(TRIM(`article_show`), ''), TRIM(`article`))";
	$sql = 'SELECT TRIM(`manufacturer`) AS `brand`, TRIM(`article`) AS `article`, ' . $article_show_expr . ' AS `article_show`, '
		. 'TRIM(IFNULL(`name`, \'\')) AS `name`, IFNULL(`exist`, 0) AS `qty`, IFNULL(`price`, 0) AS `price`, '
		. 'TRIM(IFNULL(`storage`, \'\')) AS `warehouse` '
		. 'FROM `shop_docpart_prices_data` '
		. 'WHERE IFNULL(`price`, 0) > 0 AND IFNULL(`exist`, 0) > 0 '
		. 'AND TRIM(IFNULL(`manufacturer`, \'\')) != \'\' AND TRIM(`article`) != \'\' '
		. 'ORDER BY IFNULL(`exist`, 0) DESC LIMIT ' . (int)min($fetch_limit * 3, 12000);
	try {
		$stmt = $db->query($sql);
		$raw = $stmt->fetchAll(PDO::FETCH_ASSOC);
	} catch (Exception $e) {
		return array();
	}
	$merged = array();
	foreach (epc_pf_map_stock_rows($raw) as $row) {
		if ((float)($row['qty'] ?? 0) > 50000) {
			continue;
		}
		$key = mb_strtoupper(trim((string)$row['brand']), 'UTF-8') . '|' . docpart_normalize_article_for_price($row['article']);
		if ($key === '|') {
			continue;
		}
		if (!isset($merged[$key])) {
			$merged[$key] = $row;
			continue;
		}
		$merged[$key]['qty'] = (float)$merged[$key]['qty'] + (float)$row['qty'];
		if ($merged[$key]['name'] === '' && $row['name'] !== '') {
			$merged[$key]['name'] = $row['name'];
		}
	}
	$rows = array_values($merged);
	usort($rows, function ($a, $b) {
		return ($b['qty'] <=> $a['qty']);
	});
	return array_slice($rows, 0, $fetch_limit);
}

/**
 * @param array<int, array<string, mixed>> $lines
 * @return array{part_lines: array, product_groups: array<string, array>, products: array, summary: array}
 */
function epc_pf_build_catalog_from_lines(array $lines, $DP_Config = null, ?PDO $db = null, int $umapi_lookup_budget = 160): array
{
	$part_lines = array();
	$product_groups = array();
	$umapi_budget = max(0, $umapi_lookup_budget);

	usort($lines, function ($a, $b) {
		return ((float)($b['qty'] ?? 0)) <=> ((float)($a['qty'] ?? 0));
	});

	foreach ($lines as $row) {
		$brand = trim((string)($row['brand'] ?? ''));
		$article = trim((string)($row['article_show'] ?? $row['article'] ?? ''));
		$article_norm = trim((string)($row['article_norm'] ?? docpart_normalize_article_for_price($article)));
		$name = trim((string)($row['name'] ?? ''));
		$qty = (float)($row['qty'] ?? 0);
		if ($brand === '' || $article_norm === '') {
			continue;
		}

		if (is_object($DP_Config) && $db instanceof PDO) {
			$group_label = epc_pf_resolve_product_group($DP_Config, $db, $brand, $article, $name, $umapi_budget);
		} else {
			$group_label = epc_pf_infer_product_group('', $name);
		}
		$group_key = mb_strtolower($group_label, 'UTF-8');
		if ($group_key === '') {
			$group_key = 'other';
			$group_label = 'Other parts';
		}

		$part_row = array(
			'brand' => $brand,
			'article' => $article,
			'article_norm' => $article_norm,
			'name' => $name,
			'product_group' => $group_label,
			'qty' => $qty,
			'price' => $row['price'] ?? 0,
		);
		$part_lines[] = $part_row;

		if (!isset($product_groups[$group_key])) {
			$product_groups[$group_key] = array(
				'label' => $group_label,
				'parts_count' => 0,
				'total_qty' => 0.0,
				'samples' => array(),
				'brands' => array(),
				'parts' => array(),
			);
		}

		$brand_key = mb_strtoupper($brand, 'UTF-8');
		$product_groups[$group_key]['parts'][] = $part_row;
		$product_groups[$group_key]['parts_count'] = (int)$product_groups[$group_key]['parts_count'] + 1;
		$product_groups[$group_key]['total_qty'] = (float)$product_groups[$group_key]['total_qty'] + $qty;

		if (count($product_groups[$group_key]['samples']) < 5) {
			$product_groups[$group_key]['samples'][] = array(
				'brand' => $brand,
				'article' => $article,
			);
		}

		if (!isset($product_groups[$group_key]['brands'][$brand_key])) {
			$product_groups[$group_key]['brands'][$brand_key] = array(
				'brand' => $brand,
				'parts_count' => 0,
				'total_qty' => 0.0,
			);
		}
		$product_groups[$group_key]['brands'][$brand_key]['parts_count'] = (int)$product_groups[$group_key]['brands'][$brand_key]['parts_count'] + 1;
		$product_groups[$group_key]['brands'][$brand_key]['total_qty'] = (float)$product_groups[$group_key]['brands'][$brand_key]['total_qty'] + $qty;
	}

	epc_pf_load_demand_helpers();
	if (function_exists('epc_demand_job_products_list')) {
		$products = epc_demand_job_products_list($product_groups);
		$brands_summary = epc_demand_job_build_brands_summary($part_lines);
	} else {
		$products = epc_pf_job_products_list($product_groups);
		$brands_summary = epc_pf_job_build_brands_summary($part_lines);
	}
	$total_qty = 0.0;
	foreach ($part_lines as $line) {
		$total_qty += (float)($line['qty'] ?? 0);
	}

	$summary = array(
		'parts_count' => count($part_lines),
		'product_groups_count' => count($products),
		'brands_count' => count($brands_summary),
		'total_stock_qty' => $total_qty,
		'brands' => $brands_summary,
	);

	return array(
		'part_lines' => $part_lines,
		'product_groups' => $product_groups,
		'products' => $products,
		'summary' => $summary,
	);
}

/**
 * @param array<string, array<string, mixed>> $product_groups_map
 * @return array<int, array<string, mixed>>
 */
function epc_pf_job_products_list(array $product_groups_map): array
{
	$list = array();
	foreach ($product_groups_map as $group) {
		$brands_map = isset($group['brands']) && is_array($group['brands']) ? $group['brands'] : array();
		$brands_list = array_values($brands_map);
		usort($brands_list, function ($a, $b) {
			$qa = (float)($a['total_qty'] ?? 0);
			$qb = (float)($b['total_qty'] ?? 0);
			if ($qa !== $qb) {
				return $qb <=> $qa;
			}
			return strcmp((string)($a['brand'] ?? ''), (string)($b['brand'] ?? ''));
		});
		$parts = isset($group['parts']) && is_array($group['parts']) ? array_values($group['parts']) : array();
		$list[] = array(
			'label' => (string)($group['label'] ?? ''),
			'parts_count' => (int)($group['parts_count'] ?? 0),
			'total_qty' => (float)($group['total_qty'] ?? 0),
			'samples' => isset($group['samples']) && is_array($group['samples']) ? $group['samples'] : array(),
			'parts' => $parts,
			'brands' => $brands_list,
		);
	}
	usort($list, function ($a, $b) {
		$ca = (int)($a['parts_count'] ?? 0);
		$cb = (int)($b['parts_count'] ?? 0);
		if ($ca !== $cb) {
			return $cb <=> $ca;
		}
		return strcmp((string)($a['label'] ?? ''), (string)($b['label'] ?? ''));
	});
	return $list;
}

/**
 * @param array<int, array<string, mixed>> $part_lines
 * @return array<int, array<string, mixed>>
 */
function epc_pf_job_build_brands_summary(array $part_lines): array
{
	$brands = array();
	foreach ($part_lines as $line) {
		$brand = trim((string)($line['brand'] ?? ''));
		if ($brand === '') {
			continue;
		}
		$key = mb_strtoupper($brand, 'UTF-8');
		if (!isset($brands[$key])) {
			$brands[$key] = array(
				'brand' => $brand,
				'parts_count' => 0,
				'total_qty' => 0.0,
				'product_groups' => array(),
			);
		}
		$brands[$key]['parts_count'] = (int)$brands[$key]['parts_count'] + 1;
		$brands[$key]['total_qty'] = (float)$brands[$key]['total_qty'] + (float)($line['qty'] ?? 0);
		$group_label = trim((string)($line['product_group'] ?? ''));
		if ($group_label !== '' && !in_array($group_label, $brands[$key]['product_groups'], true)) {
			$brands[$key]['product_groups'][] = $group_label;
		}
	}
	$list = array_values($brands);
	usort($list, function ($a, $b) {
		return ((int)($b['parts_count'] ?? 0)) <=> ((int)($a['parts_count'] ?? 0));
	});
	return $list;
}

/**
 * Strip heavy part lists for summary API (cards only).
 *
 * @param array<int, array<string, mixed>> $products
 * @return array<int, array<string, mixed>>
 */
function epc_pf_products_for_cards(array $products, int $brand_preview_limit = 10): array
{
	$out = array();
	foreach ($products as $p) {
		$brands = isset($p['brands']) && is_array($p['brands']) ? $p['brands'] : array();
		$slice = epc_pf_top_brands_slice($brands, $brand_preview_limit);
		$out[] = array(
			'label' => (string)($p['label'] ?? ''),
			'parts_count' => (int)($p['parts_count'] ?? 0),
			'total_qty' => (float)($p['total_qty'] ?? 0),
			'samples' => isset($p['samples']) && is_array($p['samples']) ? $p['samples'] : array(),
			'brands_top' => $slice['top'],
			'brands_more_count' => (int)$slice['more_count'],
			'brands_total' => (int)$slice['total'],
		);
	}
	return $out;
}

/**
 * @param array<string, array<string, mixed>> $product_groups
 */
function epc_pf_find_group(array $product_groups, string $label): ?array
{
	$needle = mb_strtolower(trim($label), 'UTF-8');
	foreach ($product_groups as $key => $group) {
		$glabel = mb_strtolower(trim((string)($group['label'] ?? '')), 'UTF-8');
		if ($glabel === $needle || $key === $needle) {
			return $group;
		}
	}
	return null;
}

/**
 * @param array<string, mixed> $group
 * @param string $brand_filter
 * @return array{group: array, brands: array, parts: array}
 */
function epc_pf_group_detail(array $group, string $brand_filter = ''): array
{
	$parts = isset($group['parts']) && is_array($group['parts']) ? $group['parts'] : array();
	$brand_filter = trim($brand_filter);
	if ($brand_filter !== '') {
		$bu = mb_strtoupper($brand_filter, 'UTF-8');
		$parts = array_values(array_filter($parts, function ($p) use ($bu) {
			return mb_strtoupper(trim((string)($p['brand'] ?? '')), 'UTF-8') === $bu;
		}));
	}

	$brands_map = array();
	foreach ($parts as $p) {
		$b = trim((string)($p['brand'] ?? ''));
		if ($b === '') {
			continue;
		}
		$bk = mb_strtoupper($b, 'UTF-8');
		if (!isset($brands_map[$bk])) {
			$brands_map[$bk] = array('brand' => $b, 'parts_count' => 0, 'total_qty' => 0.0);
		}
		$brands_map[$bk]['parts_count']++;
		$brands_map[$bk]['total_qty'] += (float)($p['qty'] ?? 0);
	}
	$brands = array_values($brands_map);
	usort($brands, function ($a, $b) {
		$qa = (float)($a['total_qty'] ?? 0);
		$qb = (float)($b['total_qty'] ?? 0);
		if ($qa !== $qb) {
			return $qb <=> $qa;
		}
		return ((int)($b['parts_count'] ?? 0)) <=> ((int)($a['parts_count'] ?? 0));
	});

	usort($parts, function ($a, $b) {
		$ba = mb_strtoupper((string)($a['brand'] ?? ''), 'UTF-8');
		$bb = mb_strtoupper((string)($b['brand'] ?? ''), 'UTF-8');
		if ($ba !== $bb) {
			return strcmp($ba, $bb);
		}
		return strcmp((string)($a['article'] ?? ''), (string)($b['article'] ?? ''));
	});

	return array(
		'label' => (string)($group['label'] ?? ''),
		'parts_count' => count($parts),
		'total_qty' => array_reduce($parts, function ($s, $p) {
			return $s + (float)($p['qty'] ?? 0);
		}, 0.0),
		'brands' => $brands,
		'parts' => $parts,
	);
}
