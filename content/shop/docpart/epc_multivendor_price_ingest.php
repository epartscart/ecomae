<?php
/**
 * Multi-vendor Excel/CSV → one warehouse + price list per vendor (+ data type).
 *
 * Excel must include both:
 *  - vendor_full  → shop_storages.name (backend / CP only)  [= vendor name]
 *  - vendor_short → shop_storages.short_name (storefront) [= vendor code]
 *
 * Match key: data_type + brand + article + vendor_name + vendor_code
 *  - Same vendor code with a different vendor name is a SEPARATE vendor
 *  - inventory: combination is unique (one row; qty summed)
 *  - sales / purchase: keep min + max price; QTY = total across all source rows
 *    for that brand+article+vendor (both min and max rows share the same total)
 *  - sales / purchase: repeats allowed → keep only lowest + highest price rows
 *
 * Customers see vendor code (short_name); CP / office managers see vendor name (name).
 *
 * Separate price lists per type so re-uploads do not wipe other types:
 *  inventory → "{code} · {name}", sales → "{code} · {name} · Sales", etc.
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

require_once __DIR__ . '/docpart_price_upload_history.php';
require_once __DIR__ . '/epc_price_import_helpers.php';
require_once __DIR__ . '/epc_commerce_price_ingest.php';
require_once __DIR__ . '/epc_multivendor_min_price_acl.php';

/**
 * @return array<string,list<string>>
 */
function epc_multivendor_header_aliases(): array
{
	return array(
		'manufacturer' => array(
			'manufacturer', 'brand', 'mfr', 'make', 'brend', 'vendor_brand',
		),
		'article' => array(
			'article', 'sku', 'number', 'part', 'partno', 'part_number', 'part number',
			'oem', 'code', 'item_code', 'articlenumber',
		),
		'name' => array(
			'name', 'description', 'title', 'product', 'item', 'product_name', 'item_name',
		),
		'exist' => array(
			'exist', 'qty', 'quantity', 'stock', 'available', 'onhand', 'on_hand', 'on hand',
			'balance', 'qty_on_hand',
		),
		'price' => array(
			'price', 'sales_price', 'sales price', 'selling_price', 'selling price',
			'unit_price', 'unit price', 'amount', 'sale', 'sales', 'retail',
		),
		'vendor_full' => array(
			'vendor_full', 'vendor full', 'vendor_full_name', 'vendor full name',
			'supplier_full', 'supplier full', 'supplier_full_name', 'supplier full name',
			'warehouse_full', 'warehouse full', 'warehouse_full_name', 'full_vendor',
			'full vendor', 'vendor_name_full', 'company', 'company_name', 'company name',
			'legal_name', 'legal name',
		),
		'vendor_short' => array(
			'vendor_short', 'vendor short', 'vendor_short_name', 'vendor short name',
			'vendor_code', 'vendor code', 'supplier_code', 'supplier code',
			'supplier_short', 'supplier short', 'supplier_short_name', 'supplier short name',
			'warehouse', 'warehouse_short', 'warehouse short', 'warehouse_name', 'warehouse name',
			'short_name', 'short name', 'short_vendor', 'short vendor', 'wh', 'wh_code',
			'wh code', 'storage', 'storage_short', 'storage short',
		),
		'data_type' => array(
			'data_type', 'data type', 'datatype', 'price_type', 'price type',
			'list_type', 'list type', 'channel', 'doc_type', 'doc type',
		),
		'time_to_exe' => array(
			'time_to_exe', 'delivery', 'days', 'lead_time', 'lead time', 'term',
		),
		'min_order' => array(
			'min_order', 'min order', 'moq', 'minimum_order', 'minimum order',
		),
	);
}

/**
 * Normalize data type to inventory|sales|purchase.
 * Pass fallback "combine" only as a mode sentinel (never a stored row type).
 */
function epc_multivendor_normalize_data_type(string $raw, string $fallback = 'inventory'): string
{
	$raw = strtolower(trim($raw));
	$raw = preg_replace('/\s+/', ' ', $raw);
	$raw = str_replace(array('-', '_'), ' ', (string) $raw);
	if ($raw === '' || $raw === 'default' || $raw === 'stock') {
		$raw = strtolower(trim($fallback));
	}
	if (in_array($raw, array('inventory', 'inv', 'wh', 'warehouse', 'stock on hand', 'on hand'), true)
		|| strpos($raw, 'invent') === 0) {
		return 'inventory';
	}
	if (in_array($raw, array('sales', 'sale', 'sell', 'selling', 'retail', 'offer'), true)
		|| strpos($raw, 'sale') === 0) {
		return 'sales';
	}
	if (in_array($raw, array('purchase', 'purchases', 'buy', 'buying', 'cost', 'procurement', 'po'), true)
		|| strpos($raw, 'purch') === 0 || strpos($raw, 'buy') === 0) {
		return 'purchase';
	}
	$fb = strtolower(trim($fallback));
	return in_array($fb, array('inventory', 'sales', 'purchase'), true) ? $fb : 'inventory';
}

/**
 * True when the CP form asks for one mixed file (per-row Data type column).
 */
function epc_multivendor_is_combine_mode(string $raw): bool
{
	$raw = strtolower(trim($raw));
	$raw = str_replace(array('-', '_'), ' ', $raw);
	$raw = preg_replace('/\s+/', ' ', (string) $raw);
	return in_array($raw, array(
		'combine', 'combined', 'mixed', 'mix', 'auto', 'all', 'from file', 'fromfile',
		'one file', 'onefile', 'per row', 'perrow',
	), true);
}

/**
 * Resolve form/API data-type mode: combine|inventory|sales|purchase.
 */
function epc_multivendor_resolve_data_type_mode(string $raw): string
{
	if (epc_multivendor_is_combine_mode($raw) || trim($raw) === '') {
		return 'combine';
	}
	return epc_multivendor_normalize_data_type($raw, 'inventory');
}

function epc_multivendor_data_type_list_suffix(string $dataType): string
{
	if ($dataType === 'sales') {
		return ' · Sales';
	}
	if ($dataType === 'purchase') {
		return ' · Purchase';
	}
	return '';
}

/**
 * Collapse rows that share brand+article (+ vendor already grouped).
 *
 * @param list<array<string,mixed>> $candidates
 * @return list<array<string,mixed>>
 */
function epc_multivendor_collapse_product_candidates(array $candidates, string $dataType): array
{
	if ($candidates === array()) {
		return array();
	}
	if (count($candidates) === 1) {
		return array_values($candidates);
	}

	if ($dataType === 'inventory') {
		// Unique: one row — sum qty, keep lowest positive price (or first).
		$keep = $candidates[0];
		$sumExist = 0;
		$minPrice = null;
		foreach ($candidates as $row) {
			$sumExist += (int) ($row['exist'] ?? 0);
			$p = (float) ($row['price'] ?? 0);
			if ($p > 0 && ($minPrice === null || $p < $minPrice)) {
				$minPrice = $p;
				$keep = $row;
			} elseif ($minPrice === null) {
				$keep = $row;
			}
			if (strlen((string) ($row['name'] ?? '')) > strlen((string) ($keep['name'] ?? ''))) {
				$keep['name'] = (string) $row['name'];
			}
		}
		$keep['exist'] = $sumExist;
		if ($minPrice !== null) {
			$keep['price'] = $minPrice;
		}
		return array($keep);
	}

	// Sales / purchase: keep only lowest and highest price rows.
	// QTY is the combined total for the item across all source rows (not just
	// the qty sitting on the min- or max-price line).
	$minRow = null;
	$maxRow = null;
	$totalExist = 0;
	foreach ($candidates as $row) {
		$totalExist += (int) ($row['exist'] ?? 0);
		$p = (float) ($row['price'] ?? 0);
		if ($minRow === null || $p < (float) $minRow['price']
			|| ($p === (float) $minRow['price'] && (int) ($row['exist'] ?? 0) > (int) ($minRow['exist'] ?? 0))) {
			$minRow = $row;
		}
		if ($maxRow === null || $p > (float) $maxRow['price']
			|| ($p === (float) $maxRow['price'] && (int) ($row['exist'] ?? 0) > (int) ($maxRow['exist'] ?? 0))) {
			$maxRow = $row;
		}
	}
	if ($minRow === null) {
		return array();
	}
	if ($maxRow === null || (float) $minRow['price'] === (float) $maxRow['price']) {
		// Same price — one row with total qty.
		$minRow['exist'] = $totalExist > 0 ? $totalExist : (int) ($minRow['exist'] ?? 0);
		// Single price is the public (max) offer — not a restricted min tier.
		$minRow['storage'] = EPC_MV_MAX_TIER;
		$minRow['epc_price_tier'] = 'max';
		return array($minRow);
	}
	$minRow['exist'] = $totalExist;
	$maxRow['exist'] = $totalExist;
	$minRow['storage'] = EPC_MV_MIN_TIER;
	$minRow['epc_price_tier'] = 'min';
	$maxRow['storage'] = EPC_MV_MAX_TIER;
	$maxRow['epc_price_tier'] = 'max';
	return array($minRow, $maxRow);
}

/**
 * @param list<string> $headerRow
 * @return array<string,int>
 */
function epc_multivendor_map_headers(array $headerRow): array
{
	$aliases = epc_multivendor_header_aliases();
	$map = array();
	foreach (array_keys($aliases) as $role) {
		$map[$role] = -1;
	}
	$usedCols = array();

	// Pass 1: exact header match (longest aliases first within each role).
	foreach ($aliases as $role => $list) {
		$listSorted = $list;
		usort($listSorted, static function ($a, $b) {
			return strlen((string) $b) <=> strlen((string) $a);
		});
		foreach ($headerRow as $idx => $raw) {
			if (isset($usedCols[$idx])) {
				continue;
			}
			$norm = epc_commerce_normalize_header_cell((string) $raw);
			if ($norm === '') {
				continue;
			}
			foreach ($listSorted as $alias) {
				if ($norm === $alias) {
					$map[$role] = (int) $idx;
					$usedCols[$idx] = true;
					break 2;
				}
			}
		}
	}

	// Pass 2: substring match. Vendor columns before generic "name".
	$roleOrder = array(
		'vendor_full', 'vendor_short', 'data_type', 'manufacturer', 'article', 'exist', 'price',
		'time_to_exe', 'min_order', 'name',
	);
	foreach ($roleOrder as $role) {
		if (!isset($aliases[$role]) || $map[$role] >= 0) {
			continue;
		}
		$listSorted = $aliases[$role];
		usort($listSorted, static function ($a, $b) {
			return strlen((string) $b) <=> strlen((string) $a);
		});
		foreach ($headerRow as $idx => $raw) {
			if (isset($usedCols[$idx])) {
				continue;
			}
			$norm = epc_commerce_normalize_header_cell((string) $raw);
			if ($norm === '') {
				continue;
			}
			// Do not let product "name" steal vendor/company headers.
			if ($role === 'name' && preg_match('/\b(vendor|supplier|warehouse|company|legal|short)\b/', $norm)) {
				continue;
			}
			foreach ($listSorted as $alias) {
				if ($alias === '' || strlen($alias) < 2) {
					continue;
				}
				if ($norm === $alias || preg_match('/(^|[^a-z0-9])' . preg_quote($alias, '/') . '([^a-z0-9]|$)/', $norm)) {
					$map[$role] = (int) $idx;
					$usedCols[$idx] = true;
					break 2;
				}
			}
		}
	}
	return $map;
}

function epc_multivendor_sanitize_short(string $raw): string
{
	$raw = trim($raw);
	$raw = preg_replace('/\s+/u', ' ', $raw);
	$raw = preg_replace('/[\/#\'"\\\\]+/u', '', (string) $raw);
	$raw = trim((string) $raw);
	if (function_exists('mb_substr')) {
		return mb_substr($raw, 0, 64, 'UTF-8');
	}
	return substr($raw, 0, 64);
}

function epc_multivendor_sanitize_full(string $raw): string
{
	$raw = trim($raw);
	$raw = preg_replace('/\s+/u', ' ', $raw);
	$raw = trim((string) $raw);
	if (function_exists('mb_substr')) {
		return mb_substr($raw, 0, 255, 'UTF-8');
	}
	return substr($raw, 0, 255);
}

function epc_multivendor_vendor_key(string $full, string $short = ''): string
{
	$fullN = strtoupper(preg_replace('/\s+/u', ' ', trim($full)));
	$shortN = strtoupper(preg_replace('/\s+/u', ' ', trim($short)));
	// Legacy single-arg calls passed only the code.
	if ($shortN === '' && $fullN !== '') {
		$shortN = $fullN;
		$fullN = $fullN;
	}
	if ($shortN === '') {
		return '';
	}
	if ($fullN === '') {
		$fullN = $shortN;
	}
	// Identity for min/max + warehouse buckets: vendor name + vendor code.
	return $fullN . "\x1e" . $shortN;
}

/**
 * Price-list base name unique per vendor name + code (customers never see this).
 */
function epc_multivendor_list_base_name(string $vendorShort, string $vendorFull): string
{
	$vendorShort = epc_multivendor_sanitize_short($vendorShort);
	$vendorFull = epc_multivendor_sanitize_full($vendorFull);
	if ($vendorShort === '') {
		return $vendorFull !== '' ? $vendorFull : 'Vendor';
	}
	if ($vendorFull === '' || strcasecmp($vendorFull, $vendorShort) === 0) {
		return $vendorShort;
	}
	$suffix = $vendorFull;
	if (function_exists('mb_strlen') && mb_strlen($suffix, 'UTF-8') > 90) {
		$suffix = mb_substr($suffix, 0, 90, 'UTF-8');
	} elseif (strlen($suffix) > 90) {
		$suffix = substr($suffix, 0, 90);
	}
	return $vendorShort . ' · ' . $suffix;
}

/**
 * Ensure warehouse exists: name = full (CP / managers), short_name = code (storefront).
 * Identity is name + short together — same code with different names stays separate.
 */
function epc_multivendor_ensure_warehouse(PDO $db, string $vendorFull, string $vendorShort, int $priceId, bool $setPrimaryPriceId = true): int
{
	$vendorFull = epc_multivendor_sanitize_full($vendorFull);
	$vendorShort = epc_multivendor_sanitize_short($vendorShort);
	if ($vendorShort === '' || $priceId <= 0) {
		return 0;
	}
	if ($vendorFull === '') {
		$vendorFull = $vendorShort;
	}

	$storageId = 0;
	$row = null;

	// Primary match: vendor name + vendor code (never code alone).
	try {
		$q = $db->prepare(
			'SELECT `id`, `name`, `short_name`, `connection_options`
			 FROM `shop_storages`
			 WHERE UPPER(TRIM(`name`)) = UPPER(?)
			   AND UPPER(TRIM(`short_name`)) = UPPER(?)
			 LIMIT 1'
		);
		$q->execute(array($vendorFull, $vendorShort));
		$row = $q->fetch(PDO::FETCH_ASSOC) ?: null;
	} catch (Throwable $e) {
		$row = null;
	}

	// Legacy fallback: same full name with empty/missing short, then adopt this code.
	if (!$row) {
		try {
			$q = $db->prepare(
				'SELECT `id`, `name`, `short_name`, `connection_options`
				 FROM `shop_storages`
				 WHERE UPPER(TRIM(`name`)) = UPPER(?)
				   AND (TRIM(COALESCE(`short_name`, \'\')) = \'\' OR UPPER(TRIM(`short_name`)) = UPPER(?))
				 LIMIT 1'
			);
			$q->execute(array($vendorFull, $vendorShort));
			$row = $q->fetch(PDO::FETCH_ASSOC) ?: null;
		} catch (Throwable $e) {
			$row = null;
		}
	}

	$opts = array(
		'probability' => '100',
		'epc_mv_vendor_full' => $vendorFull,
		'epc_mv_vendor_code' => $vendorShort,
	);
	if ($setPrimaryPriceId) {
		$opts['price_id'] = (string) $priceId;
	}
	if ($row) {
		$storageId = (int) $row['id'];
		$existingOpts = json_decode((string) ($row['connection_options'] ?? ''), true);
		if (is_array($existingOpts)) {
			$opts = array_merge($existingOpts, $opts);
			// Sales/purchase must not steal storefront inventory price_id.
			if (!$setPrimaryPriceId && isset($existingOpts['price_id']) && (string) $existingOpts['price_id'] !== '') {
				$opts['price_id'] = (string) $existingOpts['price_id'];
			} elseif ($setPrimaryPriceId) {
				$opts['price_id'] = (string) $priceId;
			}
		}
		// Track typed lists on warehouse without breaking primary.
		$typed = isset($opts['epc_typed_price_ids']) && is_array($opts['epc_typed_price_ids'])
			? $opts['epc_typed_price_ids'] : array();
		$typed[(string) $priceId] = true;
		$opts['epc_typed_price_ids'] = array_keys($typed);
		$db->prepare(
			'UPDATE `shop_storages`
			 SET `name` = ?, `short_name` = ?, `interface_type` = 2, `connection_options` = ?, `hidden` = 0
			 WHERE `id` = ?'
		)->execute(array(
			$vendorFull,
			$vendorShort,
			json_encode($opts, JSON_UNESCAPED_UNICODE),
			$storageId,
		));
	} else {
		$currency = 784;
		try {
			global $DP_Config;
			if (isset($DP_Config) && is_object($DP_Config) && !empty($DP_Config->shop_currency)) {
				$currency = (int) $DP_Config->shop_currency;
			}
		} catch (Throwable $e) {
		}
		$users = '[]';
		try {
			$admin = $db->query('SELECT `id` FROM `users` WHERE `user_type` = 2 ORDER BY `id` ASC LIMIT 1')->fetchColumn();
			if ((int) $admin > 0) {
				$users = json_encode(array((string) (int) $admin));
			}
		} catch (Throwable $e) {
		}
		if (!isset($opts['price_id'])) {
			$opts['price_id'] = (string) $priceId;
		}
		$opts['epc_typed_price_ids'] = array((string) $priceId);
		$db->prepare(
			'INSERT INTO `shop_storages`
			 (`name`, `interface_type`, `users`, `connection_options`, `currency`, `short_name`, `hidden`, `bg_line_color`)
			 VALUES (?, 2, ?, ?, ?, ?, 0, 0)'
		)->execute(array(
			$vendorFull,
			$users,
			json_encode($opts, JSON_UNESCAPED_UNICODE),
			$currency,
			$vendorShort,
		));
		$storageId = (int) $db->lastInsertId();
	}

	if ($storageId <= 0) {
		return 0;
	}

	// Keep name-based helper in sync for primary (inventory) lists only.
	if ($setPrimaryPriceId) {
		$listBase = epc_multivendor_list_base_name($vendorShort, $vendorFull);
		epc_price_link_storage_to_list($db, $listBase, $priceId);
		epc_price_link_storage_to_list($db, $vendorShort, $priceId);
		if (strcasecmp($vendorFull, $vendorShort) !== 0) {
			epc_price_link_storage_to_list($db, $vendorFull, $priceId);
		}
	}

	try {
		$officeId = (int) $db->query('SELECT `id` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1')->fetchColumn();
		if ($officeId > 0) {
			$chk = $db->prepare('SELECT COUNT(*) FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ?');
			$chk->execute(array($officeId, $storageId));
			if ((int) $chk->fetchColumn() === 0) {
				try {
					$db->prepare(
						'INSERT INTO `shop_offices_storages_map` (`office_id`, `storage_id`, `group_id`, `min_point`, `max_point`, `markup`, `additional_time`)
						 VALUES (?, ?, 2, 0, 999999999, 0, 0)'
					)->execute(array($officeId, $storageId));
				} catch (Throwable $e) {
					try {
						$db->prepare(
							'INSERT INTO `shop_offices_storages_map` (`office_id`, `storage_id`) VALUES (?, ?)'
						)->execute(array($officeId, $storageId));
					} catch (Throwable $e2) {
					}
				}
			}
		}
	} catch (Throwable $e) {
	}

	return $storageId;
}

/**
 * @return array{ok:bool,rows:list<array<string,mixed>>,message:string,headers:list<string>,map:array<string,int>,mode?:string}
 */
function epc_multivendor_read_source_rows(string $csvPath, string $defaultDataType = 'combine'): array
{
	$mode = epc_multivendor_resolve_data_type_mode($defaultDataType);
	$combine = ($mode === 'combine');
	$rowFallback = $combine ? '' : $mode;

	$delimiter = epc_commerce_detect_delimiter($csvPath);
	$fh = fopen($csvPath, 'rb');
	if ($fh === false) {
		return array('ok' => false, 'rows' => array(), 'message' => 'Cannot open file', 'headers' => array(), 'map' => array(), 'mode' => $mode);
	}
	$header = fgetcsv($fh, 0, $delimiter);
	if (!is_array($header) || count($header) === 0) {
		fclose($fh);
		return array('ok' => false, 'rows' => array(), 'message' => 'Missing header row', 'headers' => array(), 'map' => array(), 'mode' => $mode);
	}
	$map = epc_multivendor_map_headers($header);
	if ($map['article'] < 0) {
		fclose($fh);
		return array('ok' => false, 'rows' => array(), 'message' => 'Could not find Article/SKU column', 'headers' => $header, 'map' => $map, 'mode' => $mode);
	}
	if ($map['price'] < 0) {
		fclose($fh);
		return array('ok' => false, 'rows' => array(), 'message' => 'Could not find Price column', 'headers' => $header, 'map' => $map, 'mode' => $mode);
	}
	if ($map['vendor_short'] < 0) {
		fclose($fh);
		return array(
			'ok' => false,
			'rows' => array(),
			'message' => 'Could not find Vendor short / Warehouse short column (customer-facing warehouse name)',
			'headers' => $header,
			'map' => $map,
			'mode' => $mode,
		);
	}
	if ($map['vendor_full'] < 0) {
		fclose($fh);
		return array(
			'ok' => false,
			'rows' => array(),
			'message' => 'Could not find Vendor full name column (backend-only warehouse name)',
			'headers' => $header,
			'map' => $map,
			'mode' => $mode,
		);
	}
	if ($combine && $map['data_type'] < 0) {
		fclose($fh);
		return array(
			'ok' => false,
			'rows' => array(),
			'message' => 'Combine mode needs a Data type column (inventory / sales / purchase) so one file can load all three. Or pick a single type override in the form.',
			'headers' => $header,
			'map' => $map,
			'mode' => $mode,
		);
	}

	$rows = array();
	$skipped = 0;
	$skippedNoType = 0;
	while (($raw = fgetcsv($fh, 0, $delimiter)) !== false) {
		if (!$raw || (count($raw) === 1 && trim((string) $raw[0]) === '')) {
			continue;
		}
		$articleShow = trim((string) ($raw[$map['article']] ?? ''));
		$article = epc_commerce_normalize_article($articleShow);
		$price = epc_commerce_parse_number($raw[$map['price']] ?? 0);
		$vendorShort = epc_multivendor_sanitize_short((string) ($raw[$map['vendor_short']] ?? ''));
		$vendorFull = epc_multivendor_sanitize_full((string) ($raw[$map['vendor_full']] ?? ''));
		if ($article === '' || $price <= 0 || $vendorShort === '') {
			$skipped++;
			continue;
		}
		if ($vendorFull === '') {
			$vendorFull = $vendorShort;
		}
		$manufacturer = $map['manufacturer'] >= 0
			? epc_commerce_clip(trim((string) ($raw[$map['manufacturer']] ?? '')))
			: '';
		$name = $map['name'] >= 0
			? epc_commerce_clip(trim((string) ($raw[$map['name']] ?? '')))
			: '';
		$exist = $map['exist'] >= 0 ? epc_parse_stock_quantity($raw[$map['exist']] ?? 0) : 0;
		$timeToExe = $map['time_to_exe'] >= 0 ? (int) epc_commerce_parse_number($raw[$map['time_to_exe']] ?? 0) : 0;
		$minOrder = $map['min_order'] >= 0 ? (int) epc_commerce_parse_number($raw[$map['min_order']] ?? 0) : 0;
		$dataTypeRaw = (isset($map['data_type']) && $map['data_type'] >= 0)
			? trim((string) ($raw[$map['data_type']] ?? ''))
			: '';
		if ($combine) {
			if ($dataTypeRaw === '') {
				$skippedNoType++;
				$skipped++;
				continue;
			}
			$normalizedProbe = strtolower(preg_replace('/[\s\-_]+/', ' ', $dataTypeRaw) ?? '');
			$known = in_array($normalizedProbe, array('inventory', 'inv', 'sales', 'sale', 'purchase', 'buy'), true)
				|| strpos($normalizedProbe, 'invent') === 0
				|| strpos($normalizedProbe, 'sale') === 0
				|| strpos($normalizedProbe, 'purch') === 0
				|| strpos($normalizedProbe, 'buy') === 0;
			if (!$known) {
				$skippedNoType++;
				$skipped++;
				continue;
			}
			$dataType = epc_multivendor_normalize_data_type($dataTypeRaw, 'inventory');
		} else {
			$dataType = epc_multivendor_normalize_data_type($dataTypeRaw, $rowFallback !== '' ? $rowFallback : 'inventory');
		}
		$rows[] = array(
			'manufacturer' => $manufacturer,
			'article' => $article,
			'article_show' => epc_commerce_clip($articleShow),
			'name' => $name,
			'exist' => $exist,
			'price' => $price,
			'time_to_exe' => max(0, $timeToExe),
			'min_order' => max(0, $minOrder),
			'vendor_full' => $vendorFull,
			'vendor_short' => $vendorShort,
			'vendor_key' => epc_multivendor_vendor_key($vendorFull, $vendorShort),
			'data_type' => $dataType,
		);
	}
	fclose($fh);

	if ($rows === array()) {
		$msg = 'No valid product rows found';
		if ($combine && $skippedNoType > 0) {
			$msg = 'Combine mode: every row needs Data type = inventory, sales, or purchase ('
				. $skippedNoType . ' row' . ($skippedNoType === 1 ? '' : 's') . ' missing/invalid type).';
		}
		return array('ok' => false, 'rows' => array(), 'message' => $msg, 'headers' => $header, 'map' => $map, 'mode' => $mode);
	}

	return array(
		'ok' => true,
		'rows' => $rows,
		'message' => 'OK',
		'headers' => $header,
		'map' => $map,
		'mode' => $mode,
		'rows_skipped' => $skipped,
		'rows_skipped_no_type' => $skippedNoType,
	);
}

/**
 * Group rows by vendor_name + vendor_code + data_type, then collapse brand+article by type rules.
 *
 * Match principal: data_type + brand + article + vendor_name + vendor_code
 *  - inventory → unique (one row, qty summed)
 *  - sales/purchase → keep lowest + highest price; QTY = combined total on both rows
 * Same vendor code with a different vendor name is a separate bucket (min/max do not mix).
 *
 * @param list<array<string,mixed>> $rows
 * @return array<string,array{vendor_full:string,vendor_short:string,data_type:string,products:list<array<string,mixed>>}>
 */
function epc_multivendor_group_by_vendor(array $rows): array
{
	$groups = array();
	foreach ($rows as $row) {
		$vendorFull = epc_multivendor_sanitize_full((string) ($row['vendor_full'] ?? ''));
		$vendorShort = epc_multivendor_sanitize_short((string) ($row['vendor_short'] ?? ''));
		$vendorKey = (string) ($row['vendor_key'] ?? '');
		if ($vendorKey === '') {
			$vendorKey = epc_multivendor_vendor_key($vendorFull, $vendorShort);
		}
		if ($vendorKey === '' || $vendorShort === '') {
			continue;
		}
		$dataType = epc_multivendor_normalize_data_type((string) ($row['data_type'] ?? 'inventory'));
		$row['data_type'] = $dataType;
		$row['vendor_full'] = $vendorFull !== '' ? $vendorFull : $vendorShort;
		$row['vendor_short'] = $vendorShort;
		$row['vendor_key'] = $vendorKey;
		$key = $vendorKey . '|' . $dataType;
		if (!isset($groups[$key])) {
			$groups[$key] = array(
				'vendor_full' => (string) $row['vendor_full'],
				'vendor_short' => (string) $row['vendor_short'],
				'data_type' => $dataType,
				'products' => array(),
			);
		} else {
			if (strlen((string) $row['vendor_full']) > strlen((string) $groups[$key]['vendor_full'])) {
				$groups[$key]['vendor_full'] = (string) $row['vendor_full'];
			}
		}
		// Product key inside vendor name+code+type: brand + article.
		$prodKey = strtoupper((string) $row['manufacturer']) . '|' . (string) $row['article'];
		if (!isset($groups[$key]['products'][$prodKey])) {
			$groups[$key]['products'][$prodKey] = array();
		}
		$groups[$key]['products'][$prodKey][] = $row;
	}
	foreach ($groups as $key => $group) {
		$collapsed = array();
		$dataType = (string) ($group['data_type'] ?? 'inventory');
		foreach ($group['products'] as $candidates) {
			foreach (epc_multivendor_collapse_product_candidates($candidates, $dataType) as $row) {
				$collapsed[] = $row;
			}
		}
		$groups[$key]['products'] = $collapsed;
	}
	return $groups;
}

/**
 * @param list<array<string,mixed>> $products
 */
function epc_multivendor_write_docpart_csv(string $path, array $products): bool
{
	$fh = fopen($path, 'wb');
	if ($fh === false) {
		return false;
	}
	fwrite($fh, "Brand,Number,Name,Qty,Price,Delivery,MinOrder,Storage\n");
	foreach ($products as $p) {
		$tier = strtolower(trim((string) ($p['epc_price_tier'] ?? $p['storage'] ?? '')));
		$storage = '';
		if ($tier === 'min' || $tier === EPC_MV_MIN_TIER) {
			$storage = EPC_MV_MIN_TIER;
		} elseif ($tier === 'max' || $tier === EPC_MV_MAX_TIER) {
			$storage = EPC_MV_MAX_TIER;
		} else {
			$storage = trim((string) ($p['storage'] ?? ''));
		}
		$line = array(
			(string) ($p['manufacturer'] ?? ''),
			(string) ($p['article_show'] ?? $p['article'] ?? ''),
			(string) ($p['name'] ?? ''),
			(string) (int) ($p['exist'] ?? 0),
			number_format((float) ($p['price'] ?? 0), 2, '.', ''),
			(string) (int) ($p['time_to_exe'] ?? 0),
			(string) (int) ($p['min_order'] ?? 0),
			$storage,
		);
		fputcsv($fh, $line);
	}
	fclose($fh);
	return true;
}

/**
 * Import one vendor bucket into its price list + warehouse.
 *
 * @param array{vendor_full:string,vendor_short:string,data_type?:string,products:list<array<string,mixed>>} $group
 * @return array<string,mixed>
 */
function epc_multivendor_import_vendor(PDO $db, array $group): array
{
	$vendorShort = epc_multivendor_sanitize_short((string) ($group['vendor_short'] ?? ''));
	$vendorFull = epc_multivendor_sanitize_full((string) ($group['vendor_full'] ?? ''));
	$dataType = epc_multivendor_normalize_data_type((string) ($group['data_type'] ?? 'inventory'));
	$products = isset($group['products']) && is_array($group['products']) ? $group['products'] : array();
	if ($vendorShort === '' || $products === array()) {
		return array('status' => false, 'message' => 'Empty vendor group', 'vendor_short' => $vendorShort);
	}
	if ($vendorFull === '') {
		$vendorFull = $vendorShort;
	}

	$listName = epc_multivendor_list_base_name($vendorShort, $vendorFull)
		. epc_multivendor_data_type_list_suffix($dataType);
	$price = epc_price_resolve_or_create_list($db, 0, $listName);
	if (!$price) {
		return array(
			'status' => false,
			'message' => 'Could not create price list for ' . $vendorShort,
			'vendor_short' => $vendorShort,
			'vendor_full' => $vendorFull,
		);
	}
	$priceId = (int) $price['id'];

	$storageId = epc_multivendor_ensure_warehouse(
		$db,
		$vendorFull,
		$vendorShort,
		$priceId,
		$dataType === 'inventory'
	);

	$tmpCsv = sys_get_temp_dir() . '/epc_mv_' . getmypid() . '_' . time() . '_' . mt_rand(1000, 9999) . '.csv';
	if (!epc_multivendor_write_docpart_csv($tmpCsv, $products)) {
		return array(
			'status' => false,
			'message' => 'Could not write temp CSV',
			'vendor_short' => $vendorShort,
			'vendor_full' => $vendorFull,
			'price_id' => $priceId,
		);
	}

	// Local import supports Delivery + MinOrder columns we wrote.
	$result = epc_multivendor_import_csv_local($db, $price, $tmpCsv);

	$countQ = $db->prepare('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?');
	$countQ->execute(array($priceId));
	$recordsInDb = (int) $countQ->fetchColumn();

	$uploadedBy = (int) ($group['uploaded_by'] ?? 0);
	$uploadSource = trim((string) ($group['upload_source'] ?? 'multivendor'));
	if ($uploadSource === '') {
		$uploadSource = 'multivendor';
	}
	$storedRel = epc_price_history_archive_file($tmpCsv, $priceId, $vendorShort . '.csv');
	$historyId = epc_price_history_save($db, array(
		'price_id' => $priceId,
		'price_name' => $listName,
		'upload_source' => $uploadSource,
		'original_filename' => $vendorShort . '.csv',
		'stored_relpath' => $storedRel,
		'file_size' => is_file($tmpCsv) ? (int) filesize($tmpCsv) : 0,
		'status' => !empty($result['status']) ? 'ok' : 'failed',
		'rows_imported' => (int) ($result['records_handled'] ?? 0),
		'rows_skipped' => (int) ($result['rows_skipped'] ?? 0),
		'rows_in_db' => $recordsInDb,
		'brands_count' => epc_price_history_count_brands($db, $priceId),
		'items_count' => $recordsInDb,
		'error_text' => empty($result['status']) ? (string) ($result['message'] ?? '') : '',
		'uploaded_by' => $uploadedBy,
		'stats_json' => json_encode(array(
			'multivendor' => true,
			'vendor_full' => $vendorFull,
			'vendor_short' => $vendorShort,
			'data_type' => $dataType,
			'storage_id' => $storageId,
			'frontend_vendor' => ($uploadSource === 'vendor_portal'),
		), JSON_UNESCAPED_UNICODE),
	));
	if ($historyId > 0 && !empty($result['status'])) {
		epc_price_history_set_active($db, $priceId, $historyId);
	}
	@unlink($tmpCsv);

	return array(
		'status' => !empty($result['status']),
		'message' => (string) ($result['message'] ?? ''),
		'vendor_full' => $vendorFull,
		'vendor_short' => $vendorShort,
		'data_type' => $dataType,
		'price_id' => $priceId,
		'price_name' => $listName,
		'storage_id' => $storageId,
		'records_handled' => (int) ($result['records_handled'] ?? 0),
		'rows_skipped' => (int) ($result['rows_skipped'] ?? 0),
		'records_in_db' => $recordsInDb,
		'history_id' => $historyId,
		'warehouse_linked' => $storageId > 0,
	);
}

/**
 * @param array<string,mixed> $price
 * @return array<string,mixed>
 */
function epc_multivendor_import_csv_local(PDO $db, array $price, string $filePath): array
{
	$priceId = (int) $price['id'];
	$db->prepare('DELETE FROM `shop_docpart_prices_data` WHERE `price_id` = ?')->execute(array($priceId));
	$fh = fopen($filePath, 'rb');
	if ($fh === false) {
		return array('status' => false, 'message' => 'Cannot open CSV', 'records_handled' => 0, 'rows_skipped' => 0);
	}
	fgetcsv($fh); // header
	$nextId = (int) $db->query('SELECT COALESCE(MAX(`id`), 0) FROM `shop_docpart_prices_data`')->fetchColumn();
	$ins = $db->prepare(
		'INSERT INTO `shop_docpart_prices_data`
		 (`id`,`price_id`,`manufacturer`,`article`,`article_show`,`name`,`exist`,`price`,`time_to_exe`,`storage`,`min_order`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?)'
	);
	$inserted = 0;
	$skipped = 0;
	while (($row = fgetcsv($fh)) !== false) {
		$mfr = epc_commerce_clip(trim((string) ($row[0] ?? '')));
		$show = epc_commerce_clip(trim((string) ($row[1] ?? '')));
		$art = epc_commerce_normalize_article($show);
		$name = epc_commerce_clip(trim((string) ($row[2] ?? '')));
		$exist = epc_parse_stock_quantity($row[3] ?? 0);
		$priceVal = epc_commerce_parse_number($row[4] ?? 0);
		$timeToExe = (int) epc_commerce_parse_number($row[5] ?? 0);
		$minOrder = (int) epc_commerce_parse_number($row[6] ?? 0);
		$storageRaw = trim((string) ($row[7] ?? ''));
		$storage = '';
		if ($storageRaw === EPC_MV_MIN_TIER || strcasecmp($storageRaw, 'min') === 0) {
			$storage = EPC_MV_MIN_TIER;
		} elseif ($storageRaw === EPC_MV_MAX_TIER || strcasecmp($storageRaw, 'max') === 0) {
			$storage = EPC_MV_MAX_TIER;
		} elseif ($storageRaw !== '') {
			$storage = epc_commerce_clip($storageRaw);
		}
		if ($art === '' || $priceVal <= 0) {
			$skipped++;
			continue;
		}
		$nextId++;
		$ins->execute(array(
			$nextId,
			$priceId,
			$mfr,
			$art,
			$show,
			$name,
			$exist,
			$priceVal,
			max(0, $timeToExe),
			$storage,
			max(0, $minOrder),
		));
		$inserted++;
	}
	fclose($fh);
	$db->prepare('UPDATE `shop_docpart_prices` SET `last_updated` = ? WHERE `id` = ?')->execute(array(time(), $priceId));
	try {
		$cntQ = $db->prepare('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?');
		$cntQ->execute(array($priceId));
		$db->prepare('UPDATE `shop_docpart_prices` SET `records_count` = ? WHERE `id` = ?')
			->execute(array((int) $cntQ->fetchColumn(), $priceId));
	} catch (Throwable $e) {
	}
	return array(
		'status' => $inserted > 0,
		'message' => $inserted > 0 ? 'Import completed' : 'No valid rows imported',
		'records_handled' => $inserted,
		'rows_skipped' => $skipped,
	);
}

/**
 * Full multi-vendor ingest from uploaded Excel/CSV.
 *
 * @return array<string,mixed>
 */
function epc_multivendor_ingest_file(PDO $db, string $sourcePath, string $originalFilename = '', string $defaultDataType = 'combine'): array
{
	$mode = epc_multivendor_resolve_data_type_mode($defaultDataType);
	$converted = epc_commerce_excel_to_csv($sourcePath);
	if (empty($converted['ok'])) {
		return array(
			'status' => false,
			'message' => (string) ($converted['message'] ?? 'File conversion failed'),
			'vendors' => array(),
		);
	}
	$csvPath = (string) $converted['path'];
	$read = epc_multivendor_read_source_rows($csvPath, $mode);
	if ($csvPath !== $sourcePath) {
		@unlink($csvPath);
	}
	if (empty($read['ok'])) {
		return array(
			'status' => false,
			'message' => (string) ($read['message'] ?? 'Parse failed'),
			'headers' => $read['headers'] ?? array(),
			'vendors' => array(),
		);
	}

	$groups = epc_multivendor_group_by_vendor($read['rows']);
	$vendorResults = array();
	$okCount = 0;
	$failCount = 0;
	$totalRows = 0;
	$warehousesLinked = 0;

	foreach ($groups as $group) {
		$one = epc_multivendor_import_vendor($db, $group);
		$vendorResults[] = $one;
		if (!empty($one['status'])) {
			$okCount++;
			$totalRows += (int) ($one['records_handled'] ?? 0);
			if ((int) ($one['storage_id'] ?? 0) > 0) {
				$warehousesLinked++;
			}
		} else {
			$failCount++;
		}
	}

	$overallOk = $okCount > 0;
	$typeLabel = ($mode === 'combine')
		? 'combine (per-row Data type)'
		: $mode;
	$message = $overallOk
		? ('Imported ' . $okCount . ' vendor list' . ($okCount === 1 ? '' : 's')
			. ' (' . number_format($totalRows) . ' rows, mode: ' . $typeLabel . ').'
			. ($failCount > 0 ? ' ' . $failCount . ' failed.' : ''))
		: ('No vendors imported. ' . ($failCount > 0 ? $failCount . ' failed.' : 'Check columns.'));

	return array(
		'status' => $overallOk,
		'message' => $message,
		'data_type_default' => $mode,
		'original_filename' => $originalFilename !== '' ? $originalFilename : basename($sourcePath),
		'vendors_total' => count($groups),
		'vendors_ok' => $okCount,
		'vendors_failed' => $failCount,
		'rows_source' => count($read['rows']),
		'rows_skipped_source' => (int) ($read['rows_skipped'] ?? 0),
		'rows_imported' => $totalRows,
		'warehouses_linked' => $warehousesLinked,
		'headers' => $read['headers'] ?? array(),
		'column_map' => $read['map'] ?? array(),
		'vendors' => $vendorResults,
	);
}

/**
 * Frontend vendor portal: ingest a file for ONE vendor only (scoped).
 * Vendor columns in the file are ignored / forced from the vendor account.
 *
 * @return array<string,mixed>
 */
function epc_multivendor_ingest_for_vendor(
	PDO $db,
	string $sourcePath,
	string $originalFilename,
	string $vendorFull,
	string $vendorShort,
	string $defaultDataType = 'inventory',
	int $uploadedBy = 0
): array {
	$vendorFull = epc_multivendor_sanitize_full($vendorFull);
	$vendorShort = epc_multivendor_sanitize_short($vendorShort);
	$defaultDataType = epc_multivendor_normalize_data_type($defaultDataType, 'inventory');
	if ($vendorShort === '') {
		return array('status' => false, 'message' => 'Vendor code is required', 'vendors' => array());
	}
	if ($vendorFull === '') {
		$vendorFull = $vendorShort;
	}

	$converted = epc_commerce_excel_to_csv($sourcePath);
	if (empty($converted['ok'])) {
		return array(
			'status' => false,
			'message' => (string) ($converted['message'] ?? 'File conversion failed'),
			'vendors' => array(),
		);
	}
	$csvPath = (string) $converted['path'];
	$read = epc_multivendor_read_source_rows($csvPath, $defaultDataType);
	if ($csvPath !== $sourcePath) {
		@unlink($csvPath);
	}
	if (empty($read['ok'])) {
		return array(
			'status' => false,
			'message' => (string) ($read['message'] ?? 'Parse failed'),
			'headers' => $read['headers'] ?? array(),
			'vendors' => array(),
		);
	}

	$rows = isset($read['rows']) && is_array($read['rows']) ? $read['rows'] : array();
	$forced = array();
	$rejectedOtherVendor = 0;
	$accountShort = epc_multivendor_sanitize_short($vendorShort);
	foreach ($rows as $row) {
		$fileShort = epc_multivendor_sanitize_short((string) ($row['vendor_short'] ?? ''));
		if ($fileShort !== '' && strcasecmp($fileShort, $accountShort) !== 0) {
			$rejectedOtherVendor++;
			continue;
		}
		$row['vendor_full'] = $vendorFull;
		$row['vendor_short'] = $vendorShort;
		$row['vendor_key'] = epc_multivendor_vendor_key($vendorFull, $vendorShort);
		if (empty($row['data_type'])) {
			$row['data_type'] = $defaultDataType;
		}
		$forced[] = $row;
	}
	if ($forced === array()) {
		return array(
			'status' => false,
			'message' => $rejectedOtherVendor > 0
				? ('No rows for your vendor code. ' . $rejectedOtherVendor . ' rows belonged to other vendors.')
				: 'No valid product rows found. Need Brand, Article, Price columns.',
			'vendors' => array(),
			'rows_rejected_other_vendor' => $rejectedOtherVendor,
		);
	}

	$groups = epc_multivendor_group_by_vendor($forced);
	$vendorResults = array();
	$okCount = 0;
	$failCount = 0;
	$totalRows = 0;
	foreach ($groups as $group) {
		$group['vendor_full'] = $vendorFull;
		$group['vendor_short'] = $vendorShort;
		$group['uploaded_by'] = $uploadedBy;
		$group['upload_source'] = 'vendor_portal';
		$one = epc_multivendor_import_vendor($db, $group);
		$vendorResults[] = $one;
		if (!empty($one['status'])) {
			$okCount++;
			$totalRows += (int) ($one['records_handled'] ?? 0);
		} else {
			$failCount++;
		}
	}

	$overallOk = $okCount > 0;
	return array(
		'status' => $overallOk,
		'message' => $overallOk
			? ('Uploaded ' . number_format($totalRows) . ' rows for ' . $vendorShort . '.'
				. ($rejectedOtherVendor > 0 ? ' Skipped ' . $rejectedOtherVendor . ' other-vendor rows.' : ''))
			: ('Upload failed. ' . ($failCount > 0 ? $failCount . ' list(s) failed.' : 'Check columns.')),
		'data_type_default' => $defaultDataType,
		'original_filename' => $originalFilename !== '' ? $originalFilename : basename($sourcePath),
		'vendor_full' => $vendorFull,
		'vendor_short' => $vendorShort,
		'vendors_ok' => $okCount,
		'vendors_failed' => $failCount,
		'rows_source' => count($rows),
		'rows_imported' => $totalRows,
		'rows_rejected_other_vendor' => $rejectedOtherVendor,
		'headers' => $read['headers'] ?? array(),
		'column_map' => $read['map'] ?? array(),
		'vendors' => $vendorResults,
	);
}

/**
 * Sample CSV for frontend vendor portal (vendor columns optional — account is used).
 */
function epc_vendor_portal_sample_csv(): string
{
	$lines = array(
		array('Brand', 'Article', 'Name', 'Qty', 'Price', 'Data type', 'Delivery'),
		array('TOYOTA', '446610010', 'PAD KIT, DISC BRAKE', '8', '103.51', 'inventory', '0'),
		array('AISIN', 'DT068', 'WATER PUMP', '3', '45.00', 'inventory', '0'),
		array('DENSO', '0671007450', 'FILTER', '12', '18.00', 'sales', '0'),
	);
	$fh = fopen('php://temp', 'r+b');
	foreach ($lines as $line) {
		fputcsv($fh, $line);
	}
	rewind($fh);
	$csv = stream_get_contents($fh);
	fclose($fh);
	return $csv === false ? '' : $csv;
}

/**
 * Sample CSV template contents for CP download.
 */
function epc_multivendor_sample_csv(): string
{
	$lines = array(
		array(
			'Brand', 'Article', 'Name', 'Qty', 'Price',
			'Vendor full name', 'Vendor short', 'Data type', 'Delivery',
		),
		// Inventory: unique brand+article per vendor name+code
		array('TOYOTA', '446610010', 'PAD KIT, DISC BRAKE', '8', '103.51', 'S-UAE Trading LLC', 'S-UAE', 'inventory', '0'),
		array('AISIN', 'DT068', 'WATER PUMP', '3', '45.00', 'R-UAE Spare Parts FZE', 'R-UAE', 'inventory', '0'),
		// Same vendor CODE, different vendor NAME → separate warehouses / min-max buckets
		array('BOSCH', 'F026400039', 'FILTER', '4', '15.00', 'Gulf Parts Trading', 'S-UAE', 'inventory', '0'),
		// Sales: same brand+article+vendor name+code can repeat — import keeps only min + max price
		// with combined total QTY (12+5+2=19) on both rows
		array('DENSO', '0671007450', 'FILTER', '12', '18.00', 'S-UAE Trading LLC', 'S-UAE', 'sales', '0'),
		array('DENSO', '0671007450', 'FILTER', '5', '22.50', 'S-UAE Trading LLC', 'S-UAE', 'sales', '0'),
		array('DENSO', '0671007450', 'FILTER', '2', '29.90', 'S-UAE Trading LLC', 'S-UAE', 'sales', '0'),
	);
	$fh = fopen('php://temp', 'r+b');
	foreach ($lines as $line) {
		fputcsv($fh, $line);
	}
	rewind($fh);
	$csv = stream_get_contents($fh);
	fclose($fh);
	return $csv === false ? '' : $csv;
}

/**
 * List warehouses for the Vendor codes CP panel.
 *
 * @return list<array<string,mixed>>
 */
function epc_multivendor_vendor_codes_list(PDO $db): array
{
	$rows = array();
	try {
		$st = $db->query(
			'SELECT `id`, `name`, `short_name`, `hidden`, `connection_options`, `interface_type`
			 FROM `shop_storages`
			 WHERE `interface_type` = 2
			 ORDER BY TRIM(`short_name`) ASC, TRIM(`name`) ASC, `id` ASC
			 LIMIT 2000'
		);
		$rows = $st ? ($st->fetchAll(PDO::FETCH_ASSOC) ?: array()) : array();
	} catch (Throwable $e) {
		return array();
	}
	$out = array();
	foreach ($rows as $r) {
		$opts = json_decode((string) ($r['connection_options'] ?? ''), true);
		if (!is_array($opts)) {
			$opts = array();
		}
		$priceId = (int) ($opts['price_id'] ?? 0);
		$out[] = array(
			'id' => (int) $r['id'],
			'vendor_full' => (string) ($r['name'] ?? ''),
			'vendor_code' => (string) ($r['short_name'] ?? ''),
			'vendor_short' => (string) ($r['short_name'] ?? ''),
			'hidden' => (int) ($r['hidden'] ?? 0),
			'price_id' => $priceId,
			'is_multivendor' => !empty($opts['epc_mv_vendor_code']) || !empty($opts['epc_typed_price_ids']),
		);
	}
	return $out;
}

/**
 * Update storefront vendor code (short_name) for a warehouse. CP keeps vendor name (name).
 *
 * @return array{ok:bool,message:string,vendor?:array<string,mixed>}
 */
function epc_multivendor_vendor_code_save(PDO $db, int $storageId, string $newCode, string $newFull = ''): array
{
	if ($storageId <= 0) {
		return array('ok' => false, 'message' => 'Invalid warehouse id');
	}
	$newCode = epc_multivendor_sanitize_short($newCode);
	if ($newCode === '') {
		return array('ok' => false, 'message' => 'Vendor code cannot be empty');
	}
	$st = $db->prepare('SELECT `id`, `name`, `short_name`, `connection_options` FROM `shop_storages` WHERE `id` = ? LIMIT 1');
	$st->execute(array($storageId));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return array('ok' => false, 'message' => 'Warehouse not found');
	}
	$oldCode = (string) ($row['short_name'] ?? '');
	$full = $newFull !== '' ? epc_multivendor_sanitize_full($newFull) : (string) ($row['name'] ?? '');
	if ($full === '') {
		$full = $newCode;
	}

	// Do not collide with another warehouse that already uses this name+code pair.
	$chk = $db->prepare(
		'SELECT `id` FROM `shop_storages`
		 WHERE UPPER(TRIM(`name`)) = UPPER(?) AND UPPER(TRIM(`short_name`)) = UPPER(?) AND `id` <> ?
		 LIMIT 1'
	);
	$chk->execute(array($full, $newCode, $storageId));
	if ($chk->fetchColumn()) {
		return array('ok' => false, 'message' => 'Another warehouse already uses this vendor name + code');
	}

	$opts = json_decode((string) ($row['connection_options'] ?? ''), true);
	if (!is_array($opts)) {
		$opts = array();
	}
	$opts['epc_mv_vendor_full'] = $full;
	$opts['epc_mv_vendor_code'] = $newCode;

	$db->prepare(
		'UPDATE `shop_storages` SET `name` = ?, `short_name` = ?, `connection_options` = ? WHERE `id` = ?'
	)->execute(array($full, $newCode, json_encode($opts, JSON_UNESCAPED_UNICODE), $storageId));

	// Rename linked price lists that still start with the old code (best-effort).
	$priceIds = array();
	if (!empty($opts['price_id'])) {
		$priceIds[] = (int) $opts['price_id'];
	}
	if (!empty($opts['epc_typed_price_ids']) && is_array($opts['epc_typed_price_ids'])) {
		foreach ($opts['epc_typed_price_ids'] as $pid) {
			$priceIds[] = (int) $pid;
		}
	}
	$priceIds = array_values(array_unique(array_filter($priceIds)));
	if ($oldCode !== '' && $priceIds !== array()) {
		foreach ($priceIds as $pid) {
			try {
				$pq = $db->prepare('SELECT `id`, `name` FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1');
				$pq->execute(array($pid));
				$pr = $pq->fetch(PDO::FETCH_ASSOC);
				if (!$pr) {
					continue;
				}
				$oldName = (string) ($pr['name'] ?? '');
				$newList = epc_multivendor_list_base_name($newCode, $full);
				foreach (array('', ' · Sales', ' · Purchase') as $suf) {
					$candidateOld = epc_multivendor_list_base_name($oldCode, (string) ($row['name'] ?? $oldCode)) . $suf;
					$candidateOldShort = $oldCode . $suf;
					if ($oldName === $candidateOld || $oldName === $candidateOldShort || $oldName === $oldCode . $suf) {
						$db->prepare('UPDATE `shop_docpart_prices` SET `name` = ? WHERE `id` = ?')
							->execute(array($newList . $suf, $pid));
						break;
					}
				}
			} catch (Throwable $e) {
			}
		}
	}

	return array(
		'ok' => true,
		'message' => 'Vendor code updated — storefront shows the new code; CP still shows the vendor name',
		'vendor' => array(
			'id' => $storageId,
			'vendor_full' => $full,
			'vendor_code' => $newCode,
			'vendor_short' => $newCode,
		),
	);
}
