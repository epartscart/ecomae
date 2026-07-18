<?php
/**
 * Multi-vendor Excel/CSV → one warehouse + price list per vendor (+ data type).
 *
 * Excel must include both:
 *  - vendor_full  → shop_storages.name (backend / CP only)  [= vendor name]
 *  - vendor_short → shop_storages.short_name (storefront) [= vendor code]
 *
 * Match key: data_type + brand + article + vendor_name + vendor_code
 *  - inventory: combination is unique (one row; qty summed)
 *  - sales / purchase: repeats allowed → keep only lowest + highest price rows
 *
 * Separate price lists per type so re-uploads do not wipe other types:
 *  inventory → "{short}", sales → "{short} · Sales", purchase → "{short} · Purchase"
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

require_once __DIR__ . '/docpart_price_upload_history.php';
require_once __DIR__ . '/epc_price_import_helpers.php';
require_once __DIR__ . '/epc_commerce_price_ingest.php';

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
	$minRow = null;
	$maxRow = null;
	foreach ($candidates as $row) {
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
		// Same price — one row; sum qtys of equal-price lines.
		$sum = 0;
		foreach ($candidates as $row) {
			if ((float) ($row['price'] ?? 0) === (float) $minRow['price']) {
				$sum += (int) ($row['exist'] ?? 0);
			}
		}
		$minRow['exist'] = $sum > 0 ? $sum : (int) ($minRow['exist'] ?? 0);
		return array($minRow);
	}
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

function epc_multivendor_vendor_key(string $short): string
{
	return strtoupper(preg_replace('/\s+/u', ' ', trim($short)));
}

/**
 * Ensure warehouse exists: name = full (backend), short_name = short (storefront).
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

	// Prefer match on storefront short name (stable warehouse identity).
	try {
		$q = $db->prepare(
			'SELECT `id`, `name`, `short_name`, `connection_options`
			 FROM `shop_storages`
			 WHERE UPPER(TRIM(`short_name`)) = UPPER(?)
			 LIMIT 1'
		);
		$q->execute(array($vendorShort));
		$row = $q->fetch(PDO::FETCH_ASSOC) ?: null;
	} catch (Throwable $e) {
		$row = null;
	}

	if (!$row) {
		$q = $db->prepare(
			'SELECT `id`, `name`, `short_name`, `connection_options`
			 FROM `shop_storages`
			 WHERE UPPER(TRIM(`name`)) = UPPER(?)
			 LIMIT 1'
		);
		$q->execute(array($vendorFull));
		$row = $q->fetch(PDO::FETCH_ASSOC) ?: null;
	}

	if (!$row && strcasecmp($vendorFull, $vendorShort) !== 0) {
		$q = $db->prepare(
			'SELECT `id`, `name`, `short_name`, `connection_options`
			 FROM `shop_storages`
			 WHERE UPPER(TRIM(`name`)) = UPPER(?)
			 LIMIT 1'
		);
		$q->execute(array($vendorShort));
		$row = $q->fetch(PDO::FETCH_ASSOC) ?: null;
	}

	$opts = array('probability' => '100');
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
 * @return array{ok:bool,rows:list<array<string,mixed>>,message:string,headers:list<string>,map:array<string,int>}
 */
function epc_multivendor_read_source_rows(string $csvPath, string $defaultDataType = 'inventory'): array
{
	$defaultDataType = epc_multivendor_normalize_data_type($defaultDataType, 'inventory');

	$delimiter = epc_commerce_detect_delimiter($csvPath);
	$fh = fopen($csvPath, 'rb');
	if ($fh === false) {
		return array('ok' => false, 'rows' => array(), 'message' => 'Cannot open file', 'headers' => array(), 'map' => array());
	}
	$header = fgetcsv($fh, 0, $delimiter);
	if (!is_array($header) || count($header) === 0) {
		fclose($fh);
		return array('ok' => false, 'rows' => array(), 'message' => 'Missing header row', 'headers' => array(), 'map' => array());
	}
	$map = epc_multivendor_map_headers($header);
	if ($map['article'] < 0) {
		fclose($fh);
		return array('ok' => false, 'rows' => array(), 'message' => 'Could not find Article/SKU column', 'headers' => $header, 'map' => $map);
	}
	if ($map['price'] < 0) {
		fclose($fh);
		return array('ok' => false, 'rows' => array(), 'message' => 'Could not find Price column', 'headers' => $header, 'map' => $map);
	}
	if ($map['vendor_short'] < 0) {
		fclose($fh);
		return array(
			'ok' => false,
			'rows' => array(),
			'message' => 'Could not find Vendor short / Warehouse short column (customer-facing warehouse name)',
			'headers' => $header,
			'map' => $map,
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
		);
	}

	$rows = array();
	$skipped = 0;
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
			? (string) ($raw[$map['data_type']] ?? '')
			: '';
		$dataType = epc_multivendor_normalize_data_type($dataTypeRaw, $defaultDataType);
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
			'vendor_key' => epc_multivendor_vendor_key($vendorShort),
			'data_type' => $dataType,
		);
	}
	fclose($fh);

	if ($rows === array()) {
		return array(
			'ok' => false,
			'rows' => array(),
			'message' => 'No valid rows (need article, price > 0, vendor short, vendor full). Skipped: ' . $skipped,
			'headers' => $header,
			'map' => $map,
		);
	}

	return array(
		'ok' => true,
		'rows' => $rows,
		'message' => 'ok',
		'headers' => $header,
		'map' => $map,
		'skipped' => $skipped,
	);
}

/**
 * Group rows by vendor_code + data_type, then collapse brand+article by type rules.
 *
 * Match principal: data_type + brand + article + vendor_name + vendor_code
 *  - inventory → unique (one row, qty summed)
 *  - sales/purchase → keep lowest + highest price only
 *
 * @param list<array<string,mixed>> $rows
 * @return array<string,array{vendor_full:string,vendor_short:string,data_type:string,products:list<array<string,mixed>>}>
 */
function epc_multivendor_group_by_vendor(array $rows): array
{
	$groups = array();
	foreach ($rows as $row) {
		$vendorKey = (string) ($row['vendor_key'] ?? '');
		if ($vendorKey === '') {
			continue;
		}
		$dataType = epc_multivendor_normalize_data_type((string) ($row['data_type'] ?? 'inventory'));
		$row['data_type'] = $dataType;
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
		// Product key inside vendor+type: brand + article (vendor name/code already in group).
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
	fwrite($fh, "Brand,Number,Name,Qty,Price,Delivery,MinOrder\n");
	foreach ($products as $p) {
		$line = array(
			(string) ($p['manufacturer'] ?? ''),
			(string) ($p['article_show'] ?? $p['article'] ?? ''),
			(string) ($p['name'] ?? ''),
			(string) (int) ($p['exist'] ?? 0),
			number_format((float) ($p['price'] ?? 0), 2, '.', ''),
			(string) (int) ($p['time_to_exe'] ?? 0),
			(string) (int) ($p['min_order'] ?? 0),
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

	$listName = $vendorShort . epc_multivendor_data_type_list_suffix($dataType);
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

	$storedRel = epc_price_history_archive_file($tmpCsv, $priceId, $vendorShort . '.csv');
	$historyId = epc_price_history_save($db, array(
		'price_id' => $priceId,
		'price_name' => $listName,
		'upload_source' => 'multivendor',
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
		'stats_json' => json_encode(array(
			'multivendor' => true,
			'vendor_full' => $vendorFull,
			'vendor_short' => $vendorShort,
			'data_type' => $dataType,
			'storage_id' => $storageId,
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
			'',
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
function epc_multivendor_ingest_file(PDO $db, string $sourcePath, string $originalFilename = '', string $defaultDataType = 'inventory'): array
{
	$defaultDataType = epc_multivendor_normalize_data_type($defaultDataType, 'inventory');
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
	$message = $overallOk
		? ('Imported ' . $okCount . ' vendor list' . ($okCount === 1 ? '' : 's')
			. ' (' . number_format($totalRows) . ' rows, data type rules applied).'
			. ($failCount > 0 ? ' ' . $failCount . ' failed.' : ''))
		: ('No vendors imported. ' . ($failCount > 0 ? $failCount . ' failed.' : 'Check columns.'));

	return array(
		'status' => $overallOk,
		'message' => $message,
		'data_type_default' => $defaultDataType,
		'original_filename' => $originalFilename !== '' ? $originalFilename : basename($sourcePath),
		'vendors_total' => count($groups),
		'vendors_ok' => $okCount,
		'vendors_failed' => $failCount,
		'rows_source' => count($read['rows']),
		'rows_skipped_source' => (int) ($read['skipped'] ?? 0),
		'rows_imported' => $totalRows,
		'warehouses_linked' => $warehousesLinked,
		'headers' => $read['headers'] ?? array(),
		'column_map' => $read['map'] ?? array(),
		'vendors' => $vendorResults,
	);
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
		// Inventory: unique brand+article per vendor code
		array('TOYOTA', '446610010', 'PAD KIT, DISC BRAKE', '8', '103.51', 'S-UAE Trading LLC', 'S-UAE', 'inventory', '0'),
		array('AISIN', 'DT068', 'WATER PUMP', '3', '45.00', 'R-UAE Spare Parts FZE', 'R-UAE', 'inventory', '0'),
		// Sales: same brand+article+vendor can repeat — import keeps only min + max price
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
