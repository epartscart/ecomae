<?php
/**
 * Auto Price AI — supplier linkage, margin meta, order-level fulfillment stamps.
 */
defined('_ASTEXE_') or die('No access');

/**
 * Human label for a buy-source domain (Spare247, Sharaf DG, …).
 */
function epc_apai_buy_source_label(string $domain): string
{
	$domain = strtolower(trim($domain));
	$domain = preg_replace('/^www\./', '', $domain);
	if ($domain === '') {
		return 'Unknown supplier';
	}
	if (is_file(__DIR__ . '/epc_apai_product_line_rankings.php')) {
		require_once __DIR__ . '/epc_apai_product_line_rankings.php';
		if (function_exists('epc_apai_rankings_domain_label')) {
			return epc_apai_rankings_domain_label($domain);
		}
	}
	$base = preg_replace('/\.(com|ae|co\.uk|net|org)$/i', '', $domain);
	$base = str_replace(array('.', '-', '_'), ' ', $base);
	return ucwords(trim($base));
}

/**
 * @return array{margin_abs:float,margin_pct:float}
 */
function epc_apai_margin_calc(float $cost, float $sell): array
{
	$marginAbs = $sell > 0 && $cost > 0 ? round($sell - $cost, 2) : 0.0;
	$marginPct = $cost > 0 && $marginAbs > 0 ? round(($marginAbs / $cost) * 100, 1) : 0.0;
	return array('margin_abs' => $marginAbs, 'margin_pct' => $marginPct);
}

/**
 * Pick cheapest buy-source domain from discovery row meta.
 *
 * @return array{domain:string,label:string,price:float}
 */
function epc_apai_resolve_buy_source(array $row, array $meta = array()): array
{
	$bestDomain = '';
	$bestPrice = 0.0;
	$buySources = (array) ($meta['buy_sources'] ?? array());
	if ($buySources) {
		foreach ($buySources as $bs) {
			if (!is_array($bs)) {
				continue;
			}
			$p = (float) ($bs['price'] ?? 0);
			$d = (string) ($bs['source_domain'] ?? '');
			if ($p > 0 && ($bestPrice <= 0 || $p < $bestPrice)) {
				$bestPrice = $p;
				$bestDomain = $d;
			}
		}
	}
	if ($bestDomain === '') {
		$bestDomain = (string) ($row['source_domain'] ?? '');
	}
	if ($bestPrice <= 0) {
		$bestPrice = (float) ($meta['buy_price_min'] ?? $row['cost_estimate'] ?? 0);
	}
	$label = epc_apai_buy_source_label($bestDomain);
	if (is_file(__DIR__ . '/epc_apai_marketplace_channels.php')) {
		require_once __DIR__ . '/epc_apai_marketplace_channels.php';
		if ($bestDomain !== '' && function_exists('epc_apai_source_role') && epc_apai_source_role($bestDomain, '', null) === 'sell_marketplace') {
			foreach ($buySources as $bs) {
				if (!is_array($bs)) {
					continue;
				}
				$d = (string) ($bs['source_domain'] ?? '');
				if ($d !== '' && epc_apai_source_role($d, '', null) !== 'sell_marketplace') {
					$bestDomain = $d;
					$bestPrice = (float) ($bs['price'] ?? $bestPrice);
					break;
				}
			}
			$label = epc_apai_buy_source_label($bestDomain);
		}
	}
	return array(
		'domain' => $bestDomain,
		'label' => $label,
		'price' => $bestPrice,
	);
}

/**
 * Ensure ERP supplier record for a buy source; returns supplier id.
 */
function epc_apai_ensure_supplier_for_buy_source(PDO $pdo, string $buyDomain, string $label = ''): int
{
	$buyDomain = strtolower(trim(preg_replace('/^www\./', '', $buyDomain)));
	if ($buyDomain === '') {
		return 0;
	}
	if ($label === '') {
		$label = epc_apai_buy_source_label($buyDomain);
	}
	if (!is_file(__DIR__ . '/../finance/epc_erp_helpers.php')) {
		return 0;
	}
	require_once __DIR__ . '/../finance/epc_erp_helpers.php';
	epc_erp_ensure_schema($pdo);

	$stmt = $pdo->prepare('SELECT `id` FROM `epc_erp_suppliers` WHERE `active` = 1 AND (`name` = ? OR `notes` LIKE ?) LIMIT 1');
	$stmt->execute(array($label, '%apai_domain:' . $buyDomain . '%'));
	$existing = (int) $stmt->fetchColumn();
	if ($existing > 0) {
		return $existing;
	}

	$notes = 'APAI buy source · apai_domain:' . $buyDomain;
	try {
		return epc_erp_create_supplier($pdo, array(
			'name' => $label,
			'notes' => $notes,
			'country_code' => 'AE',
			'currency_code' => 'AED',
		));
	} catch (Throwable $e) {
		return 0;
	}
}

/**
 * Find or create epc_price_sources row for buy domain.
 */
function epc_apai_ensure_price_source_for_buy(PDO $pdo, string $siteKey, string $buyDomain, string $label): int
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$buyDomain = strtolower(trim(preg_replace('/^www\./', '', $buyDomain)));
	if ($siteKey === '' || $buyDomain === '') {
		return 0;
	}
	if (!function_exists('epc_ape_source_save')) {
		require_once __DIR__ . '/epc_auto_price_engine.php';
	}
	$stmt = $pdo->prepare(
		'SELECT `id` FROM `epc_price_sources` WHERE `site_key` = ? AND (`external_ref` = ? OR LOWER(`name`) = LOWER(?)) LIMIT 1'
	);
	$stmt->execute(array($siteKey, $buyDomain, $label));
	$existing = (int) $stmt->fetchColumn();
	if ($existing > 0) {
		return $existing;
	}
	return epc_ape_source_save($pdo, $siteKey, array(
		'source_type' => 'supplier',
		'name' => $label,
		'external_ref' => $buyDomain,
		'sort_order' => 50,
		'active' => 1,
	));
}

/**
 * Attach fulfillment supplier + margin meta during catalogue import.
 *
 * @return array{meta_patch:array<string,mixed>,supplier_id:int,source_product_id:int}
 */
function epc_apai_import_attach_fulfillment(
	PDO $pdo,
	string $siteKey,
	int $productId,
	array $row,
	array $metaPre,
	float $cost,
	float $sellPrice,
	array $specs,
	int $queueId
): array {
	$buy = epc_apai_resolve_buy_source($row, $metaPre);
	$buyDomain = (string) ($buy['domain'] ?? '');
	$buyLabel = (string) ($buy['label'] ?? '');
	if ($cost <= 0) {
		$cost = (float) ($buy['price'] ?? 0);
	}
	$margin = epc_apai_margin_calc($cost, $sellPrice);
	$supplierId = epc_apai_ensure_supplier_for_buy_source($pdo, $buyDomain, $buyLabel);

	$srcProductId = 0;
	$sourceId = epc_apai_ensure_price_source_for_buy($pdo, $siteKey, $buyDomain, $buyLabel);
	if ($sourceId > 0 && $productId > 0 && function_exists('epc_ape_source_product_save')) {
		$baKey = (string) ($specs['brand_article_key'] ?? '');
		$externalSku = $baKey !== '' ? $baKey : ('DISC-' . $queueId);
		$srcMeta = array(
			'supplier_domain' => $buyDomain,
			'supplier_label' => $buyLabel,
			'warehouse_cost' => $cost,
			'sell_price' => $sellPrice,
			'margin_abs' => $margin['margin_abs'],
			'margin_pct' => $margin['margin_pct'],
			'apai_supplier_id' => $supplierId,
			'discovery_queue_id' => $queueId,
		);
		$srcProductId = epc_ape_source_product_save($pdo, $sourceId, array(
			'product_id' => $productId,
			'external_sku' => $externalSku,
			'external_url' => (string) ($row['source_url'] ?? ''),
			'title' => (string) ($row['title'] ?? ''),
			'last_price' => $sellPrice,
			'warehouse_cost' => $cost,
		));
		if ($srcProductId > 0) {
			try {
				$pdo->prepare('UPDATE `epc_price_source_products` SET `meta_json` = ? WHERE `id` = ?')
					->execute(array(json_encode($srcMeta, JSON_UNESCAPED_UNICODE), $srcProductId));
			} catch (Throwable $e) {
			}
		}
	}

	if ($productId > 0 && $cost > 0 && function_exists('epc_ape_set_catalogue_storage_price')) {
		epc_ape_set_catalogue_storage_price($pdo, $productId, $sellPrice, $cost);
	}

	$warehouseLabel = '';
	$tenantCfg = function_exists('epc_ape_tenant_config_get') ? epc_ape_tenant_config_get($pdo, $siteKey) : array();
	if ((string) ($tenantCfg['profile'] ?? '') === 'warehouse_supplier') {
		foreach (epc_ape_sources_list($pdo, $siteKey) as $src) {
			if (in_array((string) ($src['source_type'] ?? ''), array('warehouse', 'warehouse_supplier'), true)) {
				$warehouseLabel = (string) ($src['name'] ?? 'Warehouse');
				break;
			}
		}
	}

	return array(
		'meta_patch' => array(
			'apai_buy_source' => $buyDomain,
			'apai_buy_source_label' => $buyLabel,
			'apai_supplier_id' => $supplierId,
			'apai_supplier_name' => $buyLabel,
			'apai_cost' => $cost,
			'apai_sell_price' => $sellPrice,
			'apai_margin' => $margin['margin_abs'],
			'apai_margin_pct' => $margin['margin_pct'],
			'apai_warehouse_label' => $warehouseLabel,
			'apai_fulfillment_source' => $buyLabel !== '' ? $buyLabel : $warehouseLabel,
		),
		'supplier_id' => $supplierId,
		'source_product_id' => $srcProductId,
	);
}

/**
 * Load APAI fulfillment meta for a catalogue product (for cart / order stamp).
 *
 * @return array<string,mixed>|null
 */
function epc_apai_product_fulfillment_meta(PDO $pdo, string $siteKey, int $productId): ?array
{
	if ($productId <= 0) {
		return null;
	}
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));

	$stmt = $pdo->prepare(
		'SELECT `meta_json` FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `product_id` = ? AND `status` = \'imported\'
		 ORDER BY `id` DESC LIMIT 1'
	);
	$stmt->execute(array($siteKey, $productId));
	$metaJson = (string) $stmt->fetchColumn();
	$meta = json_decode($metaJson, true);
	if (is_array($meta) && !empty($meta['apai_supplier_name'])) {
		return array(
			'apai_buy_source' => (string) ($meta['apai_buy_source'] ?? ''),
			'apai_supplier_id' => (int) ($meta['apai_supplier_id'] ?? 0),
			'apai_supplier_name' => (string) ($meta['apai_supplier_name'] ?? ''),
			'apai_cost' => (float) ($meta['apai_cost'] ?? 0),
			'apai_sell_price' => (float) ($meta['apai_sell_price'] ?? 0),
			'apai_margin' => (float) ($meta['apai_margin'] ?? 0),
			'apai_margin_pct' => (float) ($meta['apai_margin_pct'] ?? 0),
			'apai_fulfillment_source' => (string) ($meta['apai_fulfillment_source'] ?? $meta['apai_supplier_name'] ?? ''),
		);
	}

	$sp = $pdo->prepare(
		'SELECT sp.`warehouse_cost`, sp.`last_price`, sp.`meta_json`, ps.`name` AS `source_name`, ps.`external_ref`
		 FROM `epc_price_source_products` sp
		 INNER JOIN `epc_price_sources` ps ON ps.`id` = sp.`source_id`
		 WHERE sp.`product_id` = ? AND ps.`site_key` = ?
		 ORDER BY sp.`updated_at` DESC LIMIT 1'
	);
	$sp->execute(array($productId, $siteKey));
	$row = $sp->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return null;
	}
	$srcMeta = json_decode((string) ($row['meta_json'] ?? ''), true);
	if (!is_array($srcMeta)) {
		$srcMeta = array();
	}
	$cost = (float) ($srcMeta['warehouse_cost'] ?? $row['warehouse_cost'] ?? 0);
	$sell = (float) ($srcMeta['sell_price'] ?? $row['last_price'] ?? 0);
	$margin = epc_apai_margin_calc($cost, $sell);
	$buyDomain = (string) ($srcMeta['supplier_domain'] ?? $row['external_ref'] ?? '');
	$label = (string) ($srcMeta['supplier_label'] ?? $row['source_name'] ?? epc_apai_buy_source_label($buyDomain));

	return array(
		'apai_buy_source' => $buyDomain,
		'apai_supplier_id' => (int) ($srcMeta['apai_supplier_id'] ?? 0),
		'apai_supplier_name' => $label,
		'apai_cost' => $cost,
		'apai_sell_price' => $sell,
		'apai_margin' => (float) ($srcMeta['margin_abs'] ?? $margin['margin_abs']),
		'apai_margin_pct' => (float) ($srcMeta['margin_pct'] ?? $margin['margin_pct']),
		'apai_fulfillment_source' => $label,
	);
}

/**
 * @param array<string,mixed> $meta
 */
function epc_apai_order_item_json_params(array $meta): string
{
	$out = array();
	foreach (array(
		'apai_buy_source', 'apai_supplier_id', 'apai_supplier_name',
		'apai_cost', 'apai_sell_price', 'apai_margin', 'apai_margin_pct', 'apai_fulfillment_source',
	) as $k) {
		if (array_key_exists($k, $meta)) {
			$out[$k] = $meta[$k];
		}
	}
	return $out ? json_encode($out, JSON_UNESCAPED_UNICODE) : '';
}

/**
 * Decode order-line t2_json_params safely (legacy double-encoding, non-JSON).
 *
 * @return array<string,mixed>
 */
function epc_apai_decode_order_item_meta(string $jsonParams): array
{
	$jsonParams = trim($jsonParams);
	if ($jsonParams === '') {
		return array();
	}
	$meta = json_decode($jsonParams, true);
	if (!is_array($meta)) {
		$meta = json_decode(stripslashes($jsonParams), true);
	}
	if (!is_array($meta) && preg_match('/^\s*\{/', $jsonParams)) {
		$meta = json_decode(preg_replace('/[\x00-\x1F\x7F]/', '', $jsonParams) ?? $jsonParams, true);
	}
	return is_array($meta) ? $meta : array();
}

/**
 * Render CP badge HTML for order line APAI meta.
 */
function epc_apai_order_fulfillment_badge_html(string $jsonParams): string
{
	$meta = epc_apai_decode_order_item_meta($jsonParams);
	if (empty($meta['apai_supplier_name'])) {
		return '';
	}
	$fulfill = htmlspecialchars((string) ($meta['apai_fulfillment_source'] ?? $meta['apai_supplier_name']), ENT_QUOTES, 'UTF-8');
	$cost = (float) ($meta['apai_cost'] ?? 0);
	$sell = (float) ($meta['apai_sell_price'] ?? 0);
	$margin = (float) ($meta['apai_margin'] ?? 0);
	$marginPct = (float) ($meta['apai_margin_pct'] ?? 0);
	$marginTxt = $cost > 0
		? 'Cost ' . number_format($cost, 2) . ' → Sold ' . number_format($sell, 2) . ' = ' . number_format($margin, 2) . ' AED (' . number_format($marginPct, 1) . '%)'
		: '';
	$html = '<br><span class="label label-info" style="font-size:11px;margin-top:4px;display:inline-block;">Fulfill from: ' . $fulfill . '</span>';
	if ($marginTxt !== '') {
		$html .= ' <span class="label label-success" style="font-size:11px;">' . htmlspecialchars($marginTxt, ENT_QUOTES, 'UTF-8') . '</span>';
	}
	return $html;
}

/**
 * Backfill supplier + margin meta on already-imported APAI products.
 *
 * @return array{updated:int,skipped:int,errors:array<int,string>}
 */
function epc_apai_backfill_imported_fulfillment(PDO $pdo, string $siteKey, int $limit = 50): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$updated = 0;
	$skipped = 0;
	$errors = array();
	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `status` = \'imported\' AND `product_id` > 0
		 ORDER BY `id` DESC LIMIT ' . max(1, min(200, $limit))
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	foreach ($rows as $row) {
		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		if (!is_array($meta)) {
			$meta = array();
		}
		if (!empty($meta['apai_supplier_name']) && !empty($meta['apai_margin_pct'])) {
			$skipped++;
			continue;
		}
		$productId = (int) ($row['product_id'] ?? 0);
		$cost = (float) ($meta['import_warehouse_cost'] ?? $row['cost_estimate'] ?? $meta['buy_price_min'] ?? 0);
		$sell = (float) ($meta['estimated_marketplace_price'] ?? $row['suggested_price'] ?? 0);
		if ($cost <= 0 && $productId > 0) {
			try {
				$cStmt = $pdo->prepare(
					'SELECT sp.`warehouse_cost`, sp.`last_price`
					 FROM `epc_price_source_products` sp
					 INNER JOIN `epc_price_sources` ps ON ps.`id` = sp.`source_id`
					 WHERE sp.`product_id` = ? AND ps.`site_key` = ?
					 ORDER BY sp.`warehouse_cost` DESC LIMIT 1'
				);
				$cStmt->execute(array($productId, $siteKey));
				$cRow = $cStmt->fetch(PDO::FETCH_ASSOC);
				if ($cRow) {
					if ($cost <= 0) {
						$cost = (float) ($cRow['warehouse_cost'] ?? 0);
					}
					if ($sell <= 0) {
						$sell = (float) ($cRow['last_price'] ?? 0);
					}
				}
			} catch (Throwable $e) {
			}
		}
		if ($sell <= 0 && $productId > 0) {
			try {
				$sell = (float) $pdo->prepare('SELECT MIN(`price`) FROM `shop_storages_data` WHERE `product_id` = ? AND `price` > 0')
					->execute(array($productId)) ?: 0;
				$sStmt = $pdo->prepare('SELECT MIN(`price`) FROM `shop_storages_data` WHERE `product_id` = ? AND `price` > 0');
				$sStmt->execute(array($productId));
				$sell = (float) $sStmt->fetchColumn();
			} catch (Throwable $e) {
			}
		}
		$specs = json_decode((string) ($row['specs_json'] ?? ''), true);
		if (!is_array($specs)) {
			$specs = array();
		}
		try {
			$fulfillment = epc_apai_import_attach_fulfillment(
				$pdo,
				$siteKey,
				$productId,
				$row,
				$meta,
				$cost,
				$sell,
				$specs,
				(int) ($row['id'] ?? 0)
			);
			$meta = array_merge($meta, (array) ($fulfillment['meta_patch'] ?? array()));
			$pdo->prepare('UPDATE `epc_product_discovery_queue` SET `meta_json` = ?, `updated_at` = ? WHERE `id` = ?')
				->execute(array(json_encode($meta, JSON_UNESCAPED_UNICODE), time(), (int) $row['id']));
			$updated++;
		} catch (Throwable $e) {
			$errors[] = 'queue#' . (int) ($row['id'] ?? 0) . ': ' . $e->getMessage();
		}
	}
	return array('updated' => $updated, 'skipped' => $skipped, 'errors' => $errors);
}

/**
 * Copy APAI sell/cost onto the storefront-visible catalogue storage (interface_type=1 + office map).
 *
 * @return array{updated:int,skipped:int,errors:array<int,string>,storefront_storage_id:int}
 */
function epc_apai_backfill_storefront_prices(PDO $pdo, string $siteKey, int $limit = 50): array
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$updated = 0;
	$skipped = 0;
	$errors = array();
	if (!function_exists('epc_ape_set_catalogue_storage_price')) {
		return array(
			'updated' => 0,
			'skipped' => 0,
			'errors' => array('epc_ape_set_catalogue_storage_price missing'),
			'storefront_storage_id' => 0,
		);
	}
	$storageId = function_exists('epc_ape_resolve_storefront_storage_id')
		? epc_ape_resolve_storefront_storage_id($pdo)
		: 0;
	if ($storageId > 0 && function_exists('epc_ape_ensure_storefront_storage_mapped')) {
		epc_ape_ensure_storefront_storage_mapped($pdo, $storageId);
	}
	$stmt = $pdo->prepare(
		'SELECT * FROM `epc_product_discovery_queue`
		 WHERE `site_key` = ? AND `status` = \'imported\' AND `product_id` > 0
		 ORDER BY `id` DESC LIMIT ' . max(1, min(200, $limit))
	);
	$stmt->execute(array($siteKey));
	$rows = $stmt->fetchAll(PDO::FETCH_ASSOC) ?: array();
	foreach ($rows as $row) {
		$productId = (int) ($row['product_id'] ?? 0);
		if ($productId <= 0) {
			$skipped++;
			continue;
		}
		$meta = json_decode((string) ($row['meta_json'] ?? ''), true);
		if (!is_array($meta)) {
			$meta = array();
		}
		$sell = (float) ($meta['apai_sell_price'] ?? $meta['estimated_marketplace_price'] ?? $row['suggested_price'] ?? 0);
		$cost = (float) ($meta['apai_cost'] ?? $meta['import_warehouse_cost'] ?? $row['cost_estimate'] ?? 0);
		if ($sell <= 0) {
			try {
				$sStmt = $pdo->prepare('SELECT MAX(`price`) FROM `shop_storages_data` WHERE `product_id` = ? AND `price` > 0');
				$sStmt->execute(array($productId));
				$sell = (float) $sStmt->fetchColumn();
			} catch (Throwable $e) {
			}
		}
		if ($cost <= 0 && $sell > 0) {
			try {
				$cStmt = $pdo->prepare('SELECT MAX(`price_purchase`) FROM `shop_storages_data` WHERE `product_id` = ? AND `price_purchase` > 0');
				$cStmt->execute(array($productId));
				$cost = (float) $cStmt->fetchColumn();
			} catch (Throwable $e) {
			}
		}
		if ($sell <= 0) {
			$skipped++;
			continue;
		}
		if ($storageId > 0) {
			try {
				$vis = $pdo->prepare(
					'SELECT `id` FROM `shop_storages_data`
					 WHERE `product_id` = ? AND `storage_id` = ? AND `price` > 0 AND `exist` > 0 LIMIT 1'
				);
				$vis->execute(array($productId, $storageId));
				if ((int) $vis->fetchColumn() > 0) {
					$skipped++;
					continue;
				}
			} catch (Throwable $e) {
			}
		}
		try {
			epc_ape_set_catalogue_storage_price($pdo, $productId, $sell, $cost > 0 ? $cost : 0);
			$updated++;
		} catch (Throwable $e) {
			$errors[] = 'product#' . $productId . ': ' . $e->getMessage();
		}
	}
	return array(
		'updated' => $updated,
		'skipped' => $skipped,
		'errors' => $errors,
		'storefront_storage_id' => $storageId,
	);
}

/**
 * True when product has a storefront-visible offer (same rules as catalogue SQL).
 */
function epc_apai_product_storefront_offer_visible(PDO $pdo, int $productId): bool
{
	if ($productId <= 0) {
		return false;
	}
	try {
		$stmt = $pdo->prepare(
			'SELECT sd.`id`
			 FROM `shop_storages_data` sd
			 INNER JOIN `shop_storages` s ON s.`id` = sd.`storage_id` AND s.`interface_type` = 1
			 INNER JOIN `shop_offices_storages_map` m ON m.`storage_id` = sd.`storage_id`
			 WHERE sd.`product_id` = ? AND sd.`price` > 0 AND sd.`exist` > 0
			 LIMIT 1'
		);
		$stmt->execute(array($productId));
		return (int) $stmt->fetchColumn() > 0;
	} catch (Throwable $e) {
		return false;
	}
}
