<?php
/**
 * ePartsCart — warehouse-only spare parts search (brand + article).
 * Source of truth: shop_docpart_prices_data (+ linked price lists).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_engine.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_price_categories.php';

if (is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_demand_intelligence.php')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_demand_intelligence.php';
}

/** @return array<int, array{value:string,label:string}> */
function epc_spare_parts_oem_brands(PDO $pdo): array
{
	static $cache = null;
	if ($cache !== null) {
		return $cache;
	}

	$labels = array(
		'Toyota', 'Lexus', 'Nissan', 'Infiniti', 'Honda', 'Acura', 'BMW', 'Mercedes-Benz',
		'Ford', 'Hyundai', 'Kia', 'Mitsubishi', 'Land Rover', 'Chevrolet', 'GMC',
		'Bosch', 'Denso', 'NGK', 'JS ASAKASHI', '555', 'Hino', 'Isuzu', 'Volkswagen', 'Audi',
	);
	if (function_exists('epc_auto_tax_seed_tree')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/price_engine/epc_auto_parts_taxonomy.php';
		foreach (epc_auto_tax_seed_tree() as $node) {
			if (($node['slug'] ?? '') !== 'auto-oem-brands' || empty($node['children'])) {
				continue;
			}
			foreach ($node['children'] as $child) {
				$name = trim((string) ($child['name'] ?? ''));
				if ($name !== '') {
					$labels[] = preg_replace('/\s*&.*$/', '', $name);
				}
			}
		}
	}

	try {
		$stmt = $pdo->query(
			'SELECT DISTINCT TRIM(`manufacturer`) AS `brand`
			 FROM `shop_docpart_prices_data`
			 WHERE TRIM(`manufacturer`) <> \'\' AND IFNULL(`price`, 0) > 0
			 ORDER BY `brand` ASC
			 LIMIT 120'
		);
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$labels[] = trim((string) ($row['brand'] ?? ''));
		}
	} catch (Throwable $e) {
	}

	$seen = array();
	$out = array();
	foreach ($labels as $label) {
		$label = trim($label);
		if ($label === '') {
			continue;
		}
		$key = mb_strtoupper($label, 'UTF-8');
		if (isset($seen[$key])) {
			continue;
		}
		$seen[$key] = true;
		$out[] = array('value' => $label, 'label' => $label);
	}
	usort($out, static function (array $a, array $b): int {
		return strcasecmp($a['label'], $b['label']);
	});
	$cache = $out;
	return $out;
}

function epc_spare_parts_catalogue_product_url(PDO $pdo, int $productId, $DP_Config = null): string
{
	if ($productId <= 0) {
		return '';
	}
	$productUrlMode = is_object($DP_Config) ? (string) ($DP_Config->product_url ?? 'alias') : 'alias';
	$stmt = $pdo->prepare(
		'SELECT scp.`id`, scp.`alias`, scp.`caption`, scc.`url` AS `category_url`
		 FROM `shop_catalogue_products` scp
		 LEFT JOIN `shop_catalogue_categories` scc ON scc.`id` = scp.`category_id`
		 WHERE scp.`id` = ? AND scp.`published_flag` = 1
		 LIMIT 1'
	);
	$stmt->execute(array($productId));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return '';
	}
	$path = epc_apai_catalogue_product_path($row, $productUrlMode);
	if ($path === '') {
		return '';
	}
	$lang = function_exists('epc_apai_storefront_lang_prefix') ? epc_apai_storefront_lang_prefix() : '/en';
	return rtrim($lang, '/') . $path;
}

function epc_spare_parts_catalogue_sell_price(PDO $pdo, int $productId): float
{
	if ($productId <= 0) {
		return 0.0;
	}
	try {
		$stmt = $pdo->prepare(
			'SELECT MIN(`price`) FROM `shop_storages_data`
			 WHERE `product_id` = ? AND IFNULL(`price`, 0) > 0'
		);
		$stmt->execute(array($productId));
		return (float) $stmt->fetchColumn();
	} catch (Throwable $e) {
		return 0.0;
	}
}

/**
 * Warehouse search by brand + article (storefront — no external suppliers).
 *
 * @return array<string,mixed>
 */
function epc_spare_parts_warehouse_search(string $brand, string $article, PDO $pdo, $DP_Config = null): array
{
	$brand = trim($brand);
	$article = trim($article);
	$articleNorm = epc_apai_normalize_article($article);
	$brandNorm = epc_apai_normalize_brand($brand);
	$baKey = epc_apai_brand_article_key($brand, $article);

	if ($articleNorm === '' || strlen($articleNorm) < 2) {
		return array(
			'ok' => false,
			'message' => 'Enter a valid part number (at least 2 characters).',
		);
	}
	if ($brand === '') {
		return array(
			'ok' => false,
			'message' => 'Select a brand.',
		);
	}

	$rows = array();
	try {
		if (function_exists('docpart_sql_article_normalized_expr')) {
			$artExpr = docpart_sql_article_normalized_expr('`article`');
		} else {
			$artExpr = "UPPER(REPLACE(REPLACE(REPLACE(`article`, ' ', ''), '-', ''), '.', ''))";
		}
		$stmt = $pdo->prepare(
			'SELECT d.*, p.`name` AS `price_list_name`
			 FROM `shop_docpart_prices_data` d
			 LEFT JOIN `shop_docpart_prices` p ON p.`id` = d.`price_id`
			 WHERE ' . $artExpr . ' = ?
			   AND IFNULL(d.`price`, 0) > 0
			 ORDER BY IFNULL(d.`exist`, 0) DESC, d.`price` ASC
			 LIMIT 40'
		);
		$stmt->execute(array($articleNorm));
		$brandUpper = mb_strtoupper(trim($brand), 'UTF-8');
		while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
			$rowBrand = trim((string) ($row['manufacturer'] ?? ''));
			$rowBrandNorm = epc_apai_normalize_brand($rowBrand);
			if ($brandNorm !== '' && $rowBrandNorm !== '' && $rowBrandNorm !== $brandNorm) {
				if (mb_strtoupper($rowBrand, 'UTF-8') !== $brandUpper) {
					continue;
				}
			}
			$rows[] = array(
				'brand' => $rowBrand !== '' ? $rowBrand : $brand,
				'article' => !empty($row['article_show']) ? (string) $row['article_show'] : (string) ($row['article'] ?? $article),
				'warehouse_cost' => (float) ($row['price'] ?? 0),
				'qty' => (float) ($row['exist'] ?? 0),
				'warehouse' => (string) ($row['storage'] ?? ''),
				'price_list' => (string) ($row['price_list_name'] ?? ''),
				'price_id' => (int) ($row['price_id'] ?? 0),
			);
		}
	} catch (Throwable $e) {
		return array('ok' => false, 'message' => 'Warehouse lookup failed.');
	}

	$productId = function_exists('epc_disc_find_catalogue_by_brand_article')
		? epc_disc_find_catalogue_by_brand_article($pdo, $baKey)
		: 0;
	$sellPrice = epc_spare_parts_catalogue_sell_price($pdo, $productId);
	$productUrl = epc_spare_parts_catalogue_product_url($pdo, $productId, $DP_Config);
	$partsUrl = function_exists('epc_demand_chpu_part_url') && is_object($DP_Config)
		? epc_demand_chpu_part_url($DP_Config, $brand, $article)
		: '';

	$totalQty = 0.0;
	$bestCost = 0.0;
	$displayBrand = $brand;
	$displayArticle = $article;
	foreach ($rows as $r) {
		$totalQty += (float) ($r['qty'] ?? 0);
		if ($bestCost <= 0 && (float) ($r['warehouse_cost'] ?? 0) > 0) {
			$bestCost = (float) $r['warehouse_cost'];
		}
		if ($displayBrand === '' && !empty($r['brand'])) {
			$displayBrand = (string) $r['brand'];
		}
		if ($displayArticle === '' && !empty($r['article'])) {
			$displayArticle = (string) $r['article'];
		}
	}
	if ($sellPrice <= 0 && $bestCost > 0) {
		$sellPrice = $bestCost;
	}

	$inWarehouse = $totalQty > 0 || !empty($rows);
	$lang = function_exists('epc_apai_storefront_lang_prefix') ? epc_apai_storefront_lang_prefix() : '/en';

	return array(
		'ok' => true,
		'brand' => $displayBrand,
		'article' => $displayArticle,
		'brand_article_key' => $baKey,
		'in_warehouse' => $inWarehouse,
		'qty' => $totalQty,
		'warehouse_cost' => $bestCost,
		'sell_price' => $sellPrice,
		'currency' => 'AED',
		'warehouse_rows' => $rows,
		'product_id' => $productId,
		'product_url' => $productUrl,
		'parts_url' => $partsUrl,
		'redirect_url' => $productUrl !== '' ? $productUrl : '',
		'spare_parts_url' => rtrim($lang, '/') . '/spare-parts/' . rawurlencode($displayBrand) . '/' . rawurlencode($displayArticle),
		'message' => $inWarehouse ? '' : 'Not in stock — contact us for availability.',
	);
}
