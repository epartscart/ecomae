<?php
/**
 * Auto Price AI — data-driven product line rankings by industry.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_industry_taxonomy.php';
if (is_file(__DIR__ . '/epc_auto_price_storefront.php')) {
	require_once __DIR__ . '/epc_auto_price_storefront.php';
}

/** Brand-aware discovery search templates for auto_parts (brand + article queries). */
function epc_apai_auto_parts_search_templates(): array
{
	return array(
		'auto-oem-toyota' => 'Toyota 1310154101',
		'auto-oem-toyota-engine' => 'Toyota 1780131090',
		'auto-oem-nissan' => 'Nissan 1520865F0E',
		'auto-oem-honda' => 'Honda 15400-PLM-A02',
		'auto-oem-bmw' => 'BMW 11427566327',
		'auto-oem-mercedes' => 'Mercedes 0009056103',
		'auto-engine-filters-oil' => 'Toyota 04152YZZA6',
		'auto-engine-filters' => 'Toyota 1780131090',
		'auto-brakes-pads' => 'Toyota 0446533471',
		'auto-electrical-batteries' => 'Toyota 28800-33070',
		'auto-engine-spark' => 'Toyota 1310154101',
		'auto-suspension-shocks' => 'Toyota 4851033C50',
		'auto-fluids-engine-oil' => 'Toyota 0888080880',
	);
}

/** Curated industry bestseller slugs when discovery queue is sparse. */
function epc_apai_curated_bestseller_slugs(string $industryKey): array
{
	$map = array(
		'electronics' => array(
			'cell-phones', 'computers-laptops', 'gaming-consoles', 'smart-home',
			'tv-video-televisions', 'pc-components-gpu', 'wearables-smartwatches', 'headphones-earbuds-true-wireless',
		),
		'auto_parts' => array(
			'auto-engine-filters', 'auto-brakes-pads', 'auto-electrical-batteries',
			'auto-engine-spark', 'auto-suspension-shocks', 'auto-oem-toyota', 'auto-fluids-engine-oil',
		),
		'fashion' => array(
			'fashion-women', 'fashion-men', 'fashion-men-footwear', 'fashion-women-dresses', 'fashion-accessories-bags',
		),
		'jewellery' => array('jewellery-rings', 'jewellery-watches', 'jewellery-necklaces'),
		'general_retail' => array('retail-home', 'retail-home-garden', 'retail-industrial', 'retail-health'),
	);
	return $map[$industryKey] ?? array();
}

/**
 * @return array<int,array{id:int,parent_id:int,slug:string,name_en:string,level:int}>
 */
function epc_apai_tax_flat_for_industry(PDO $pdo, string $industryKey): array
{
	$stmt = $pdo->prepare(
		'SELECT `id`, `parent_id`, `slug`, `name_en`, `level`, `sort`
		 FROM `epc_product_taxonomy_nodes`
		 WHERE `active` = 1 AND `industry_key` = ?
		 ORDER BY `level`, `sort`, `name_en`'
	);
	$stmt->execute(array($industryKey));
	return $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

/**
 * @return array<int,int[]>
 */
function epc_apai_tax_descendant_map(array $flat): array
{
	$children = array();
	foreach ($flat as $row) {
		$pid = (int) ($row['parent_id'] ?? 0);
		$id = (int) ($row['id'] ?? 0);
		if (!isset($children[$pid])) {
			$children[$pid] = array();
		}
		$children[$pid][] = $id;
	}
	$desc = array();
	$walk = function (int $nodeId) use (&$walk, &$desc, $children): array {
		if (isset($desc[$nodeId])) {
			return $desc[$nodeId];
		}
		$ids = array($nodeId);
		foreach ($children[$nodeId] ?? array() as $cid) {
			$ids = array_merge($ids, $walk($cid));
		}
		$desc[$nodeId] = $ids;
		return $ids;
	};
	foreach ($flat as $row) {
		$walk((int) $row['id']);
	}
	return $desc;
}

function epc_apai_rankings_preview_image(PDO $pdo, string $siteKey, int $taxonomyNodeId): string
{
	$stmt = $pdo->prepare(
		'SELECT `image_urls`, `local_image_paths`
		 FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `taxonomy_node_id` = ? AND `status` IN (\'suggested\', \'imported\')
		 ORDER BY `status` = \'imported\' DESC, `updated_at` DESC
		 LIMIT 1'
	);
	$stmt->execute(array($siteKey, $taxonomyNodeId));
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return '';
	}
	$local = json_decode((string) ($row['local_image_paths'] ?? ''), true);
	if (is_array($local) && !empty($local[0])) {
		return (string) $local[0];
	}
	$imgs = json_decode((string) ($row['image_urls'] ?? ''), true);
	if (is_array($imgs) && !empty($imgs[0])) {
		return (string) $imgs[0];
	}
	if (function_exists('epc_disc_queue_preview_image')) {
		return epc_disc_queue_preview_image($row);
	}
	return '';
}

function epc_apai_pl_rankings_cache_file(string $siteKey, string $industryKey, bool $fastPartial): string
{
	$dir = __DIR__ . '/_cache/apai_pl_rankings';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	$suffix = $fastPartial ? 'fast' : 'full';
	$key = preg_replace('/[^a-z0-9_\-]/', '_', $siteKey . '_' . $industryKey . '_' . $suffix);
	return $dir . '/' . $key . '.json';
}

/**
 * Market price range for one product line (on expand / click).
 *
 * @return array{price_min:float,price_max:float,currency:string,source_price_rows:int}
 */
function epc_apai_product_line_market_prices(PDO $pdo, string $siteKey, int $taxonomyNodeId, string $industryKey = ''): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($industryKey === '') {
		$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	}
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($industryKey)));
	$currency = 'AED';
	if (function_exists('epc_apai_tenant_country') && function_exists('epc_apai_country_meta')) {
		$currency = (string) (epc_apai_country_meta(epc_apai_tenant_country($siteKey, $pdo))['currency'] ?? 'AED');
	}
	if ($taxonomyNodeId <= 0) {
		return array('price_min' => 0.0, 'price_max' => 0.0, 'currency' => $currency, 'source_price_rows' => 0);
	}
	$flat = epc_apai_tax_flat_for_industry($pdo, $industryKey);
	$descMap = epc_apai_tax_descendant_map($flat);
	$descIds = $descMap[$taxonomyNodeId] ?? array($taxonomyNodeId);
	$placeholders = implode(',', array_fill(0, count($descIds), '?'));
	$params = array_merge(array($siteKey), $descIds);
	$priceMin = 0.0;
	$priceMax = 0.0;
	$sourcePriceRows = 0;
	$qStmt = $pdo->prepare(
		'SELECT MIN(NULLIF(`suggested_price`, 0)) AS `pmin`, MAX(`suggested_price`) AS `pmax`
		 FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `taxonomy_node_id` IN (' . $placeholders . ')'
	);
	$qStmt->execute($params);
	$qr = $qStmt->fetch(PDO::FETCH_ASSOC);
	if ($qr) {
		$priceMin = (float) ($qr['pmin'] ?? 0);
		$priceMax = (float) ($qr['pmax'] ?? 0);
	}
	epc_apai_source_prices_schema($pdo);
	$pStmt = $pdo->prepare(
		'SELECT COUNT(sp.`id`) AS `rows`,
		        MIN(NULLIF(sp.`price`, 0)) AS `pmin`,
		        MAX(sp.`price`) AS `pmax`
		 FROM `epc_product_source_prices` sp
		 INNER JOIN `epc_product_discovery_queue` q ON q.`id` = sp.`discovery_queue_id` AND q.`site_key` = sp.`site_key`
		 WHERE sp.`site_key` = ? AND q.`taxonomy_node_id` IN (' . $placeholders . ')'
	);
	$pStmt->execute($params);
	$pr = $pStmt->fetch(PDO::FETCH_ASSOC);
	if ($pr) {
		$sourcePriceRows = (int) ($pr['rows'] ?? 0);
		$mmin = (float) ($pr['pmin'] ?? 0);
		$mmax = (float) ($pr['pmax'] ?? 0);
		if ($mmin > 0) {
			$priceMin = ($priceMin <= 0) ? $mmin : min($priceMin, $mmin);
		}
		if ($mmax > 0) {
			$priceMax = max($priceMax, $mmax);
		}
	}
	return array(
		'price_min' => $priceMin,
		'price_max' => $priceMax,
		'currency' => $currency,
		'source_price_rows' => $sourcePriceRows,
	);
}

/**
 * Aggregate product-line demand scores from discovery queue, source prices, and curated seeds.
 *
 * @param array<string,mixed> $opts fast_partial, skip_market_prices, skip_preview_images, limit, offset, skip_cache
 * @return array{rankings:array<int,array>,top:array<int,array>,currency:string,industry_key:string,configured_sources:int,total_ranked?:int,has_more?:bool}
 */
function epc_apai_product_line_rankings(PDO $pdo, string $siteKey, string $industryKey = '', array $opts = array()): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($industryKey === '') {
		$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	}
	$industryKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($industryKey)));
	$fastPartial = !empty($opts['fast_partial']);
	$skipMarketPrices = $fastPartial || !empty($opts['skip_market_prices']);
	$skipPreviewImages = $fastPartial || !empty($opts['skip_preview_images']);
	$limit = max(0, (int) ($opts['limit'] ?? 0));
	$offset = max(0, (int) ($opts['offset'] ?? 0));
	$skipCache = !empty($opts['skip_cache']);
	$cacheTtl = 900;

	if (!$skipCache) {
		$cacheFile = epc_apai_pl_rankings_cache_file($siteKey, $industryKey, $fastPartial);
		if (is_file($cacheFile) && (time() - (int) filemtime($cacheFile)) < $cacheTtl) {
			$cached = json_decode((string) file_get_contents($cacheFile), true);
			if (is_array($cached) && isset($cached['rankings']) && is_array($cached['rankings'])) {
				return epc_apai_pl_rankings_apply_slice($cached, $limit, $offset);
			}
		}
	}

	$flat = epc_apai_tax_flat_for_industry($pdo, $industryKey);
	if (!$flat) {
		return array('rankings' => array(), 'top' => array(), 'currency' => 'AED', 'industry_key' => $industryKey, 'configured_sources' => 0);
	}

	$byId = array();
	foreach ($flat as $row) {
		$byId[(int) $row['id']] = $row;
	}
	$descMap = epc_apai_tax_descendant_map($flat);

	$currency = 'AED';
	if (function_exists('epc_apai_tenant_country') && function_exists('epc_apai_country_meta')) {
		$currency = (string) (epc_apai_country_meta(epc_apai_tenant_country($siteKey, $pdo))['currency'] ?? 'AED');
	}

	$configuredSources = count(epc_disc_sources_list($pdo, $siteKey));

	// Queue stats per taxonomy node
	$queueStats = array();
	$qStmt = $pdo->prepare(
		'SELECT `taxonomy_node_id`,
		        COUNT(*) AS `queue_count`,
		        SUM(`status` = \'imported\') AS `imported_count`,
		        SUM(`status` = \'suggested\') AS `suggested_count`,
		        MIN(NULLIF(`suggested_price`, 0)) AS `price_min`,
		        MAX(`suggested_price`) AS `price_max`,
		        AVG(NULLIF(`suggested_price`, 0)) AS `price_avg`,
		        COUNT(DISTINCT `source_domain`) AS `source_domains`,
		        MAX(`updated_at`) AS `last_activity`,
		        SUM(`updated_at` >= ?) AS `recent_updates`
		 FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `taxonomy_node_id` > 0
		 GROUP BY `taxonomy_node_id`'
	);
	$weekAgo = time() - (7 * 86400);
	$qStmt->execute(array($weekAgo, $siteKey));
	foreach ($qStmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $r) {
		$queueStats[(int) $r['taxonomy_node_id']] = $r;
	}

	// Source domains per taxonomy from queue
	$domainByTax = array();
	$dStmt = $pdo->prepare(
		'SELECT `taxonomy_node_id`, `source_domain`, COUNT(*) AS `cnt`
		 FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `taxonomy_node_id` > 0 AND `source_domain` <> \'\'
		 GROUP BY `taxonomy_node_id`, `source_domain`'
	);
	$dStmt->execute(array($siteKey));
	foreach ($dStmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $r) {
		$tid = (int) $r['taxonomy_node_id'];
		if (!isset($domainByTax[$tid])) {
			$domainByTax[$tid] = array();
		}
		$domainByTax[$tid][(string) $r['source_domain']] = (int) $r['cnt'];
	}

	$priceStats = array();
	if (!$skipMarketPrices) {
		epc_apai_source_prices_schema($pdo);
		$pStmt = $pdo->prepare(
			'SELECT q.`taxonomy_node_id`,
			        COUNT(sp.`id`) AS `source_price_rows`,
			        COUNT(DISTINCT sp.`source_domain`) AS `price_source_domains`,
			        MIN(NULLIF(sp.`price`, 0)) AS `market_price_min`,
			        MAX(sp.`price`) AS `market_price_max`
			 FROM `epc_product_source_prices` sp
			 INNER JOIN `epc_product_discovery_queue` q ON q.`id` = sp.`discovery_queue_id` AND q.`site_key` = sp.`site_key`
			 WHERE sp.`site_key` = ? AND q.`taxonomy_node_id` > 0
			 GROUP BY q.`taxonomy_node_id`'
		);
		$pStmt->execute(array($siteKey));
		foreach ($pStmt->fetchAll(PDO::FETCH_ASSOC) ?: array() as $r) {
			$priceStats[(int) $r['taxonomy_node_id']] = $r;
		}
	}

	$curatedSlugs = epc_apai_curated_bestseller_slugs($industryKey);
	$curatedIds = array();
	foreach ($curatedSlugs as $slug) {
		$node = epc_apai_tax_by_slug($pdo, $industryKey, $slug);
		if ($node) {
			$curatedIds[(int) $node['id']] = true;
		}
	}

	$totalQueue = array_sum(array_map(function ($s) {
		return (int) ($s['queue_count'] ?? 0);
	}, $queueStats));

	$rankings = array();
	foreach ($flat as $row) {
		$nodeId = (int) $row['id'];
		$level = (int) ($row['level'] ?? 1);
		if ($level > 2) {
			continue;
		}

		$descIds = $descMap[$nodeId] ?? array($nodeId);
		$queueCount = 0;
		$importedCount = 0;
		$suggestedCount = 0;
		$sourceDomains = array();
		$sourcePriceRows = 0;
		$priceMin = null;
		$priceMax = null;
		$lastActivity = 0;
		$recentUpdates = 0;

		foreach ($descIds as $did) {
			if (isset($queueStats[$did])) {
				$qs = $queueStats[$did];
				$queueCount += (int) ($qs['queue_count'] ?? 0);
				$importedCount += (int) ($qs['imported_count'] ?? 0);
				$suggestedCount += (int) ($qs['suggested_count'] ?? 0);
				$lastActivity = max($lastActivity, (int) ($qs['last_activity'] ?? 0));
				$recentUpdates += (int) ($qs['recent_updates'] ?? 0);
				$pmin = (float) ($qs['price_min'] ?? 0);
				$pmax = (float) ($qs['price_max'] ?? 0);
				if ($pmin > 0) {
					$priceMin = ($priceMin === null) ? $pmin : min($priceMin, $pmin);
				}
				if ($pmax > 0) {
					$priceMax = ($priceMax === null) ? $pmax : max($priceMax, $pmax);
				}
			}
			if (isset($domainByTax[$did])) {
				foreach ($domainByTax[$did] as $dom => $cnt) {
					$sourceDomains[$dom] = ($sourceDomains[$dom] ?? 0) + $cnt;
				}
			}
			if (isset($priceStats[$did])) {
				$ps = $priceStats[$did];
				$sourcePriceRows += (int) ($ps['source_price_rows'] ?? 0);
				$mmin = (float) ($ps['market_price_min'] ?? 0);
				$mmax = (float) ($ps['market_price_max'] ?? 0);
				if ($mmin > 0) {
					$priceMin = ($priceMin === null) ? $mmin : min($priceMin, $mmin);
				}
				if ($mmax > 0) {
					$priceMax = ($priceMax === null) ? $mmax : max($priceMax, $mmax);
				}
			}
		}

		$sourceDomainCount = count($sourceDomains);
		$score = ($queueCount * 3) + ($sourcePriceRows * 2) + ($importedCount * 5) + ($sourceDomainCount * 1);

		// Curated seed boost when tenant queue is empty or line is a known bestseller
		if ($totalQueue === 0 && isset($curatedIds[$nodeId])) {
			$score += 50;
		} elseif (isset($curatedIds[$nodeId]) && $queueCount === 0) {
			$score += 25;
		}

		$trend = 'stable';
		if ($recentUpdates >= 2 || ($recentUpdates >= 1 && $queueCount >= 3)) {
			$trend = 'hot';
		} elseif ($queueCount > 0 && $lastActivity >= $weekAgo) {
			$trend = 'hot';
		}

		$previewImage = '';
		if (!$skipPreviewImages) {
			foreach ($descIds as $did) {
				$previewImage = epc_apai_rankings_preview_image($pdo, $siteKey, $did);
				if ($previewImage !== '') {
					break;
				}
			}
		}

		arsort($sourceDomains);
		$sourceList = array();
		foreach (array_keys($sourceDomains) as $dom) {
			$sourceList[] = array(
				'domain' => $dom,
				'label' => epc_apai_rankings_domain_label($dom),
				'count' => (int) $sourceDomains[$dom],
			);
		}

		$rankings[] = array(
			'id' => $nodeId,
			'parent_id' => (int) ($row['parent_id'] ?? 0),
			'slug' => (string) ($row['slug'] ?? ''),
			'name_en' => (string) ($row['name_en'] ?? ''),
			'level' => $level,
			'score' => $score,
			'queue_count' => $queueCount,
			'suggested_count' => $suggestedCount,
			'imported_count' => $importedCount,
			'source_price_rows' => $sourcePriceRows,
			'source_coverage' => $sourceDomainCount,
			'source_domains' => $sourceList,
			'price_min' => $priceMin,
			'price_max' => $priceMax,
			'currency' => $currency,
			'trend' => $trend,
			'preview_image' => $previewImage,
			'is_curated' => isset($curatedIds[$nodeId]),
			'last_activity' => $lastActivity,
		);
	}

	usort($rankings, function ($a, $b) {
		if ($b['score'] !== $a['score']) {
			return $b['score'] <=> $a['score'];
		}
		return strcmp($a['name_en'], $b['name_en']);
	});

	foreach ($rankings as $i => &$r) {
		$r['rank'] = $i + 1;
	}
	unset($r);

	$top = array();
	foreach ($rankings as $r) {
		if ((int) $r['level'] === 1) {
			$top[] = $r;
		}
		if (count($top) >= 8) {
			break;
		}
	}
	if (count($top) < 5) {
		$top = array_slice($rankings, 0, min(8, count($rankings)));
	} else {
		$top = array_slice($top, 0, 8);
	}

	$result = array(
		'rankings' => $rankings,
		'top' => $top,
		'currency' => $currency,
		'industry_key' => $industryKey,
		'configured_sources' => $configuredSources,
		'total_ranked' => count($rankings),
	);

	if (!$skipCache) {
		$cacheFile = epc_apai_pl_rankings_cache_file($siteKey, $industryKey, $fastPartial);
		@file_put_contents($cacheFile, json_encode($result, JSON_UNESCAPED_UNICODE), LOCK_EX);
	}

	return epc_apai_pl_rankings_apply_slice($result, $limit, $offset);
}

/**
 * @param array<string,mixed> $result
 * @return array<string,mixed>
 */
function epc_apai_pl_rankings_apply_slice(array $result, int $limit, int $offset): array
{
	$all = (array) ($result['rankings'] ?? array());
	$total = (int) ($result['total_ranked'] ?? count($all));
	if ($limit > 0) {
		$result['rankings'] = array_slice($all, $offset, $limit);
		$result['total_ranked'] = $total;
		$result['has_more'] = ($offset + $limit) < $total;
		$result['offset'] = $offset;
		$result['limit'] = $limit;
	} else {
		$result['total_ranked'] = $total;
		$result['has_more'] = false;
	}
	return $result;
}

function epc_apai_rankings_domain_label(string $domain): string
{
	$domain = strtolower(preg_replace('/^www\./', '', trim($domain)));
	$labels = array(
		'noon.com' => 'Noon',
		'amazon.ae' => 'Amazon.ae',
		'sharafdg.com' => 'Sharaf DG',
		'jumbo.ae' => 'Jumbo',
		'emaxme.com' => 'EMAX',
		'carrefouruae.com' => 'Carrefour',
		'namshi.com' => 'Namshi',
		'ounass.ae' => 'Ounass',
		'6thstreet.com' => '6thStreet',
		'damasjewellery.com' => 'Damas',
		'autodoc.ae' => 'AutoDoc',
		'epartscart.com' => 'eParts Cart',
		'spare247.com' => 'Spare247',
		'autoparts.ae' => 'AutoParts.ae',
		'partsouq.com' => 'Partsouq',
		'amayama.com' => 'Amayama',
		'partslink24.com' => 'Partslink24',
		'rockauto.com' => 'RockAuto',
		'alfuttaimparts.com' => 'Al-Futtaim',
		'virginmegastore.ae' => 'Virgin Megastore',
		'microless.com' => 'Microless',
		'aceuae.com' => 'ACE Hardware',
		'danubehome.com' => 'Danube Home',
		'homecentre.com' => 'Home Centre',
		'toolsmart.ae' => 'Toolmart',
	);
	if (isset($labels[$domain])) {
		return $labels[$domain];
	}
	$parts = explode('.', $domain);
	$name = $parts[0] ?? $domain;
	return ucfirst(str_replace('-', ' ', $name));
}

/**
 * Filter compare matrix rows to products linked to a taxonomy node (and descendants).
 */
function epc_apai_compare_matrix_for_taxonomy(PDO $pdo, string $siteKey, string $industryKey, int $taxonomyNodeId, array $matrix): array
{
	if ($taxonomyNodeId <= 0 || !$matrix) {
		return $matrix;
	}
	$flat = epc_apai_tax_flat_for_industry($pdo, $industryKey);
	$descMap = epc_apai_tax_descendant_map($flat);
	$allowedIds = $descMap[$taxonomyNodeId] ?? array($taxonomyNodeId);
	$placeholders = implode(',', array_fill(0, count($allowedIds), '?'));
	$params = array_merge(array($siteKey), $allowedIds);
	$stmt = $pdo->prepare(
		'SELECT DISTINCT `product_id` FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `status` = \'imported\' AND `product_id` > 0
		   AND `taxonomy_node_id` IN (' . $placeholders . ')'
	);
	$stmt->execute($params);
	$pids = array();
	foreach ($stmt->fetchAll(PDO::FETCH_COLUMN) ?: array() as $pid) {
		$pids[(int) $pid] = true;
	}
	if (!$pids) {
		return array();
	}
	return array_values(array_filter($matrix, function ($row) use ($pids) {
		return isset($pids[(int) ($row['product_id'] ?? 0)]);
	}));
}
