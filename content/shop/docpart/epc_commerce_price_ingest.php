<?php
/**
 * Commerce data → warehouse price lists (S / P / L).
 *
 * Roles:
 *  - sales     → {base}-S   highest sales price becomes shelf price; qty summed
 *  - purchase  → {supplier}.P  cost × (1 + margin%); one list per supplier
 *  - inventory → {base}-L   stock qty + cost/list × (1 + margin%)
 *
 * Accepts CSV/TXT (preferred) and XLS/XLSX via PHPExcel when available.
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

require_once __DIR__ . '/docpart_price_upload_history.php';
require_once __DIR__ . '/epc_price_import_helpers.php';

/**
 * @return array<string,list<string>>
 */
function epc_commerce_header_aliases(): array
{
	return array(
		'manufacturer' => array('manufacturer', 'brand', 'mfr', 'make', 'vendor_brand', 'brend'),
		'article' => array('article', 'sku', 'number', 'part', 'partno', 'part_number', 'part number', 'oem', 'code', 'item_code', 'articlenumber'),
		'name' => array('name', 'description', 'title', 'product', 'item', 'product_name', 'item_name'),
		'exist' => array('exist', 'qty', 'quantity', 'stock', 'available', 'onhand', 'on_hand', 'on hand', 'balance', 'qty_on_hand'),
		'price' => array('price', 'sales_price', 'sales price', 'selling_price', 'selling price', 'unit_price', 'unit price', 'amount', 'sale', 'sales', 'retail'),
		'cost' => array('cost', 'purchase', 'purchase_price', 'purchase price', 'buy', 'net', 'cost_price', 'cost price', 'wholesale'),
		'supplier' => array('supplier', 'vendor', 'seller', 'source', 'supplier_name', 'supplier name'),
	);
}

function epc_commerce_normalize_header_cell(string $value): string
{
	$value = strtolower(trim($value));
	$value = preg_replace('/[\x00-\x1F]+/u', '', $value);
	$value = preg_replace('/\s+/', ' ', $value);
	return (string) $value;
}

/**
 * @param list<string> $headerRow
 * @return array<string,int> role => 0-based column index (-1 if missing)
 */
function epc_commerce_map_headers(array $headerRow): array
{
	$aliases = epc_commerce_header_aliases();
	$map = array();
	foreach (array_keys($aliases) as $role) {
		$map[$role] = -1;
	}
	foreach ($headerRow as $idx => $raw) {
		$norm = epc_commerce_normalize_header_cell((string) $raw);
		if ($norm === '') {
			continue;
		}
		foreach ($aliases as $role => $list) {
			if ($map[$role] >= 0) {
				continue;
			}
			foreach ($list as $alias) {
				if ($norm === $alias || strpos($norm, $alias) !== false) {
					$map[$role] = (int) $idx;
					break 2;
				}
			}
		}
	}
	return $map;
}

function epc_commerce_parse_number($raw): float
{
	$raw = trim((string) $raw);
	if ($raw === '') {
		return 0.0;
	}
	$raw = str_replace(array(' ', "\xc2\xa0"), '', $raw);
	if (preg_match('/^\d{1,3}(\.\d{3})+,\d+$/', $raw)) {
		$raw = str_replace('.', '', $raw);
		$raw = str_replace(',', '.', $raw);
	} else {
		$raw = str_replace(',', '.', $raw);
	}
	$raw = preg_replace('/[^0-9.\-]/', '', $raw);
	return (float) $raw;
}

function epc_commerce_normalize_article(string $raw): string
{
	$sweep = array(' ', '-', '_', '`', '/', "'", '"', '\\', '.', ',', '#', "\r\n", "\r", "\n", "\t");
	return strtoupper(str_replace($sweep, '', $raw));
}

function epc_commerce_clip(string $value, int $max = 255): string
{
	$value = str_replace(array('/', '#', "\r\n", "\r", "\n", "\t", "'", '"', '\\'), '', $value);
	if (function_exists('mb_substr')) {
		return mb_substr($value, 0, $max, 'UTF-8');
	}
	return substr($value, 0, $max);
}

function epc_commerce_role_suffix(string $role): string
{
	$role = strtolower(trim($role));
	if ($role === 'sales' || $role === 's') {
		return 'S';
	}
	if ($role === 'purchase' || $role === 'p') {
		return 'P';
	}
	if ($role === 'inventory' || $role === 'l' || $role === 'local') {
		return 'L';
	}
	return '';
}

/**
 * Build warehouse / price-list name.
 * Sales: BASE-S | Purchase: SUPPLIER.P | Inventory: BASE-L
 */
function epc_commerce_list_name(string $role, string $baseName, string $supplier = ''): string
{
	$suffix = epc_commerce_role_suffix($role);
	$base = trim($baseName);
	if ($base === '') {
		$base = 'EPC';
	}
	$base = preg_replace('/[^A-Za-z0-9_\-\.]+/', '-', $base);
	$base = trim((string) $base, '-_.');
	if ($base === '') {
		$base = 'EPC';
	}
	if ($suffix === 'P') {
		$sup = trim($supplier);
		if ($sup === '') {
			$sup = $base;
		}
		$sup = preg_replace('/[^A-Za-z0-9_\-\.]+/', '-', $sup);
		$sup = trim((string) $sup, '-_.');
		if ($sup === '') {
			$sup = $base;
		}
		// strip trailing .P if already present
		$sup = preg_replace('/\.P$/i', '', $sup);
		return $sup . '.P';
	}
	$base = preg_replace('/-[SL]$/i', '', $base);
	return $base . '-' . $suffix;
}

/**
 * Convert XLS/XLSX to a temp CSV path using PHPExcel when available.
 *
 * @return array{ok:bool,path:string,message:string}
 */
function epc_commerce_excel_to_csv(string $sourcePath): array
{
	$ext = strtolower(pathinfo($sourcePath, PATHINFO_EXTENSION));
	if (in_array($ext, array('csv', 'txt'), true)) {
		return array('ok' => true, 'path' => $sourcePath, 'message' => 'csv');
	}
	if (!in_array($ext, array('xls', 'xlsx'), true)) {
		return array('ok' => false, 'path' => '', 'message' => 'Unsupported file type (use CSV, TXT, XLS, XLSX)');
	}

	// Prefer lightweight XLSX reader (works on modern PHP); fall back to PHPExcel.
	if ($ext === 'xlsx') {
		$native = epc_commerce_xlsx_to_csv_native($sourcePath);
		if (!empty($native['ok'])) {
			return $native;
		}
	}

	$phpExcel = ($_SERVER['DOCUMENT_ROOT'] ?? '') . '/lib/PHPExcel/PHPExcel.php';
	if (!is_file($phpExcel)) {
		return array(
			'ok' => false,
			'path' => '',
			'message' => ($ext === 'xlsx'
				? 'XLSX read failed — upload CSV/TXT, or fix Excel library'
				: 'Excel support unavailable — upload CSV/TXT, or install PHPExcel'),
		);
	}
	try {
		require_once $phpExcel;
		$type = PHPExcel_IOFactory::identify($sourcePath);
		$reader = PHPExcel_IOFactory::createReader($type);
		$reader->setReadDataOnly(true);
		$book = $reader->load($sourcePath);
		$sheet = $book->getActiveSheet();
		$tmp = sys_get_temp_dir() . '/epc_commerce_' . getmypid() . '_' . time() . '.csv';
		$fh = fopen($tmp, 'wb');
		if ($fh === false) {
			return array('ok' => false, 'path' => '', 'message' => 'Cannot write temp CSV');
		}
		foreach ($sheet->getRowIterator() as $row) {
			$cells = array();
			$iterator = $row->getCellIterator();
			$iterator->setIterateOnlyExistingCells(false);
			foreach ($iterator as $cell) {
				$cells[] = (string) $cell->getCalculatedValue();
			}
			while (count($cells) > 0 && trim((string) end($cells)) === '') {
				array_pop($cells);
			}
			if (count($cells) === 0) {
				continue;
			}
			fputcsv($fh, $cells);
		}
		fclose($fh);
		return array('ok' => true, 'path' => $tmp, 'message' => 'excel_converted');
	} catch (Throwable $e) {
		if ($ext === 'xlsx') {
			$native = epc_commerce_xlsx_to_csv_native($sourcePath);
			if (!empty($native['ok'])) {
				return $native;
			}
		}
		return array('ok' => false, 'path' => '', 'message' => 'Excel read failed: ' . $e->getMessage());
	}
}

/**
 * Read simple XLSX (sharedStrings / inlineStr / numbers) via ZipArchive — no PHPExcel.
 *
 * @return array{ok:bool,path:string,message:string}
 */
function epc_commerce_xlsx_to_csv_native(string $sourcePath): array
{
	if (!class_exists('ZipArchive')) {
		return array('ok' => false, 'path' => '', 'message' => 'ZipArchive unavailable');
	}
	$zip = new ZipArchive();
	if ($zip->open($sourcePath) !== true) {
		return array('ok' => false, 'path' => '', 'message' => 'Cannot open XLSX zip');
	}
	$shared = array();
	$ssXml = $zip->getFromName('xl/sharedStrings.xml');
	if (is_string($ssXml) && $ssXml !== '') {
		$ss = @simplexml_load_string($ssXml);
		if ($ss !== false) {
			$ss->registerXPathNamespace('m', 'http://schemas.openxmlformats.org/spreadsheetml/2006/main');
			foreach ($ss->si as $si) {
				$text = '';
				if (isset($si->t)) {
					$text = (string) $si->t;
				} elseif (isset($si->r)) {
					foreach ($si->r as $run) {
						$text .= (string) $run->t;
					}
				}
				$shared[] = $text;
			}
		}
	}
	$sheetXml = $zip->getFromName('xl/worksheets/sheet1.xml');
	if (!is_string($sheetXml) || $sheetXml === '') {
		// first worksheet fallback
		for ($i = 0; $i < $zip->numFiles; $i++) {
			$name = (string) $zip->getNameIndex($i);
			if (preg_match('#^xl/worksheets/sheet\d+\.xml$#', $name)) {
				$sheetXml = $zip->getFromIndex($i);
				break;
			}
		}
	}
	$zip->close();
	if (!is_string($sheetXml) || $sheetXml === '') {
		return array('ok' => false, 'path' => '', 'message' => 'XLSX has no worksheet');
	}
	$sheet = @simplexml_load_string($sheetXml);
	if ($sheet === false) {
		return array('ok' => false, 'path' => '', 'message' => 'XLSX worksheet XML invalid');
	}
	$sheet->registerXPathNamespace('m', 'http://schemas.openxmlformats.org/spreadsheetml/2006/main');
	$rows = $sheet->xpath('//m:sheetData/m:row') ?: array();
	$tmp = sys_get_temp_dir() . '/epc_commerce_xlsx_' . getmypid() . '_' . time() . '.csv';
	$fh = fopen($tmp, 'wb');
	if ($fh === false) {
		return array('ok' => false, 'path' => '', 'message' => 'Cannot write temp CSV');
	}
	$rowCount = 0;
	foreach ($rows as $row) {
		$cells = array();
		$maxCol = -1;
		$byCol = array();
		foreach ($row->c as $c) {
			$ref = (string) ($c['r'] ?? '');
			$colIdx = 0;
			if (preg_match('/^([A-Z]+)/', $ref, $m)) {
				$colIdx = 0;
				$letters = $m[1];
				for ($i = 0, $len = strlen($letters); $i < $len; $i++) {
					$colIdx = $colIdx * 26 + (ord($letters[$i]) - 64);
				}
				$colIdx--; // 0-based
			} else {
				$colIdx = $maxCol + 1;
			}
			$type = (string) ($c['t'] ?? '');
			$val = '';
			if ($type === 'inlineStr') {
				$val = isset($c->is->t) ? (string) $c->is->t : '';
			} elseif ($type === 's') {
				$idx = (int) ((string) ($c->v ?? '-1'));
				$val = isset($shared[$idx]) ? (string) $shared[$idx] : '';
			} else {
				$val = (string) ($c->v ?? '');
			}
			$byCol[$colIdx] = $val;
			if ($colIdx > $maxCol) {
				$maxCol = $colIdx;
			}
		}
		for ($i = 0; $i <= $maxCol; $i++) {
			$cells[] = isset($byCol[$i]) ? $byCol[$i] : '';
		}
		while (count($cells) > 0 && trim((string) end($cells)) === '') {
			array_pop($cells);
		}
		if (count($cells) === 0) {
			continue;
		}
		fputcsv($fh, $cells);
		$rowCount++;
	}
	fclose($fh);
	if ($rowCount === 0) {
		@unlink($tmp);
		return array('ok' => false, 'path' => '', 'message' => 'XLSX contained no rows');
	}
	return array('ok' => true, 'path' => $tmp, 'message' => 'xlsx_native');
}

function epc_commerce_detect_delimiter(string $filePath): string
{
	$fh = fopen($filePath, 'rb');
	if ($fh === false) {
		return ',';
	}
	$line = fgets($fh);
	fclose($fh);
	if ($line === false || trim($line) === '') {
		return ',';
	}
	$counts = array(';' => substr_count($line, ';'), ',' => substr_count($line, ','), "\t" => substr_count($line, "\t"));
	arsort($counts);
	foreach ($counts as $char => $count) {
		if ($count > 0) {
			return $char;
		}
	}
	return ',';
}

/**
 * Read source rows into normalized records.
 *
 * @return array{ok:bool,rows:list<array<string,mixed>>,message:string,headers:list<string>}
 */
function epc_commerce_read_source_rows(string $csvPath, string $role): array
{
	$delimiter = epc_commerce_detect_delimiter($csvPath);
	$fh = fopen($csvPath, 'rb');
	if ($fh === false) {
		return array('ok' => false, 'rows' => array(), 'message' => 'Cannot open file', 'headers' => array());
	}
	$header = fgetcsv($fh, 0, $delimiter);
	if (!is_array($header) || count($header) === 0) {
		fclose($fh);
		return array('ok' => false, 'rows' => array(), 'message' => 'Missing header row', 'headers' => array());
	}
	$map = epc_commerce_map_headers($header);
	if ($map['article'] < 0) {
		fclose($fh);
		return array('ok' => false, 'rows' => array(), 'message' => 'Could not find Article/SKU column in header', 'headers' => $header);
	}
	$role = strtolower($role);
	$needPrice = ($role === 'sales');
	$needCost = ($role === 'purchase' || $role === 'inventory');
	if ($needPrice && $map['price'] < 0 && $map['cost'] < 0) {
		fclose($fh);
		return array('ok' => false, 'rows' => array(), 'message' => 'Sales file needs a Price column', 'headers' => $header);
	}
	if ($needCost && $map['cost'] < 0 && $map['price'] < 0) {
		fclose($fh);
		return array('ok' => false, 'rows' => array(), 'message' => 'Purchase/Inventory file needs Cost or Price column', 'headers' => $header);
	}

	$rows = array();
	while (($raw = fgetcsv($fh, 0, $delimiter)) !== false) {
		if (!$raw || (count($raw) === 1 && trim((string) $raw[0]) === '')) {
			continue;
		}
		$articleShow = trim((string) ($raw[$map['article']] ?? ''));
		$article = epc_commerce_normalize_article($articleShow);
		if ($article === '') {
			continue;
		}
		$manufacturer = $map['manufacturer'] >= 0 ? epc_commerce_clip(trim((string) ($raw[$map['manufacturer']] ?? ''))) : '';
		$name = $map['name'] >= 0 ? epc_commerce_clip(trim((string) ($raw[$map['name']] ?? ''))) : '';
		$exist = $map['exist'] >= 0 ? epc_parse_stock_quantity($raw[$map['exist']] ?? 0) : 0;
		$price = $map['price'] >= 0 ? epc_commerce_parse_number($raw[$map['price']] ?? 0) : 0.0;
		$cost = $map['cost'] >= 0 ? epc_commerce_parse_number($raw[$map['cost']] ?? 0) : 0.0;
		$supplier = $map['supplier'] >= 0 ? epc_commerce_clip(trim((string) ($raw[$map['supplier']] ?? ''))) : '';
		$rows[] = array(
			'manufacturer' => $manufacturer,
			'article' => $article,
			'article_show' => epc_commerce_clip($articleShow),
			'name' => $name,
			'exist' => $exist,
			'price' => $price,
			'cost' => $cost,
			'supplier' => $supplier,
		);
	}
	fclose($fh);
	return array('ok' => true, 'rows' => $rows, 'message' => 'ok', 'headers' => $header);
}

/**
 * Aggregate source rows into Docpart-ready lines keyed by list name.
 *
 * @param list<array<string,mixed>> $rows
 * @return array<string,list<array<string,mixed>>> listName => rows
 */
function epc_commerce_aggregate_rows(string $role, array $rows, string $baseName, float $marginPercent): array
{
	$role = strtolower($role);
	$margin = max(0.0, $marginPercent);
	$factor = 1.0 + ($margin / 100.0);
	$buckets = array();

	foreach ($rows as $row) {
		$mfr = (string) $row['manufacturer'];
		$art = (string) $row['article'];
		$key = strtoupper($mfr) . '|' . $art;

		if ($role === 'sales') {
			$listName = epc_commerce_list_name('sales', $baseName);
			$salesPrice = (float) $row['price'];
			if ($salesPrice <= 0) {
				$salesPrice = (float) $row['cost'];
			}
			if ($salesPrice <= 0) {
				continue;
			}
			if (!isset($buckets[$listName][$key])) {
				$buckets[$listName][$key] = array(
					'manufacturer' => $mfr,
					'article' => $art,
					'article_show' => (string) $row['article_show'],
					'name' => (string) $row['name'],
					'exist' => (int) $row['exist'],
					'price' => $salesPrice,
				);
			} else {
				$buckets[$listName][$key]['exist'] += (int) $row['exist'];
				if ($salesPrice > (float) $buckets[$listName][$key]['price']) {
					$buckets[$listName][$key]['price'] = $salesPrice;
					if ((string) $row['name'] !== '') {
						$buckets[$listName][$key]['name'] = (string) $row['name'];
					}
					if ((string) $row['article_show'] !== '') {
						$buckets[$listName][$key]['article_show'] = (string) $row['article_show'];
					}
				}
			}
			continue;
		}

		if ($role === 'purchase') {
			$supplier = (string) $row['supplier'];
			$listName = epc_commerce_list_name('purchase', $baseName, $supplier !== '' ? $supplier : $baseName);
			$cost = (float) $row['cost'];
			if ($cost <= 0) {
				$cost = (float) $row['price'];
			}
			if ($cost <= 0) {
				continue;
			}
			$sell = round($cost * $factor, 2);
			if ($sell <= 0) {
				continue;
			}
			if (!isset($buckets[$listName][$key])) {
				$buckets[$listName][$key] = array(
					'manufacturer' => $mfr,
					'article' => $art,
					'article_show' => (string) $row['article_show'],
					'name' => (string) $row['name'],
					'exist' => max(0, (int) $row['exist']),
					'price' => $sell,
					'cost' => $cost,
				);
			} else {
				// keep lowest cost (best purchase), refresh sell with margin
				if ($cost < (float) ($buckets[$listName][$key]['cost'] ?? $cost)) {
					$buckets[$listName][$key]['cost'] = $cost;
					$buckets[$listName][$key]['price'] = $sell;
				}
				$buckets[$listName][$key]['exist'] = max(
					(int) $buckets[$listName][$key]['exist'],
					(int) $row['exist']
				);
			}
			continue;
		}

		// inventory
		$listName = epc_commerce_list_name('inventory', $baseName);
		$cost = (float) $row['cost'];
		if ($cost <= 0) {
			$cost = (float) $row['price'];
		}
		if ($cost <= 0) {
			continue;
		}
		$sell = round($cost * $factor, 2);
		$qty = max(0, (int) $row['exist']);
		if (!isset($buckets[$listName][$key])) {
			$buckets[$listName][$key] = array(
				'manufacturer' => $mfr,
				'article' => $art,
				'article_show' => (string) $row['article_show'],
				'name' => (string) $row['name'],
				'exist' => $qty,
				'price' => $sell,
			);
		} else {
			$buckets[$listName][$key]['exist'] += $qty;
			// keep higher inventory shelf price (conservative)
			if ($sell > (float) $buckets[$listName][$key]['price']) {
				$buckets[$listName][$key]['price'] = $sell;
			}
		}
	}

	$out = array();
	foreach ($buckets as $listName => $map) {
		$out[$listName] = array_values($map);
	}
	return $out;
}

/**
 * Write Docpart-format CSV: Brand,Number,Name,Qty,Price,Delivery
 *
 * @param list<array<string,mixed>> $rows
 */
function epc_commerce_write_docpart_csv(string $path, array $rows): bool
{
	$fh = fopen($path, 'wb');
	if ($fh === false) {
		return false;
	}
	fputcsv($fh, array('Brand', 'Number', 'Name', 'Qty', 'Price', 'Delivery'));
	foreach ($rows as $row) {
		fputcsv($fh, array(
			(string) ($row['manufacturer'] ?? ''),
			(string) ($row['article_show'] !== '' ? $row['article_show'] : $row['article']),
			(string) ($row['name'] ?? ''),
			(int) ($row['exist'] ?? 0),
			number_format((float) ($row['price'] ?? 0), 2, '.', ''),
			'1',
		));
	}
	fclose($fh);
	return true;
}

/**
 * Ensure warehouse (interface_type=2) exists and is linked to the price list + first office.
 */
function epc_commerce_ensure_warehouse(PDO $db, string $listName, int $priceId): int
{
	if ($listName === '' || $priceId <= 0) {
		return 0;
	}
	epc_price_link_storage_to_list($db, $listName, $priceId);

	$q = $db->prepare('SELECT `id`, `connection_options` FROM `shop_storages` WHERE UPPER(`name`) = UPPER(?) LIMIT 1');
	$q->execute(array($listName));
	$row = $q->fetch(PDO::FETCH_ASSOC);
	if ($row) {
		$storageId = (int) $row['id'];
		$opts = json_decode((string) $row['connection_options'], true);
		if (!is_array($opts)) {
			$opts = array();
		}
		$opts['price_id'] = (string) $priceId;
		if (!isset($opts['probability'])) {
			$opts['probability'] = '100';
		}
		$db->prepare('UPDATE `shop_storages` SET `interface_type` = 2, `connection_options` = ?, `short_name` = IF(`short_name` = \'\' OR `short_name` IS NULL, ?, `short_name`), `hidden` = 0 WHERE `id` = ?')
			->execute(array(json_encode($opts, JSON_UNESCAPED_UNICODE), $listName, $storageId));
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
		$opts = json_encode(array('price_id' => (string) $priceId, 'probability' => '100'), JSON_UNESCAPED_UNICODE);
		$db->prepare(
			'INSERT INTO `shop_storages`
			 (`name`, `interface_type`, `users`, `connection_options`, `currency`, `short_name`, `hidden`, `bg_line_color`)
			 VALUES (?, 2, ?, ?, ?, ?, 0, 0)'
		)->execute(array($listName, $users, $opts, $currency, $listName));
		$storageId = (int) $db->lastInsertId();
	}

	if ($storageId <= 0) {
		return 0;
	}

	// Link to first office if not already mapped
	try {
		$officeId = (int) $db->query('SELECT `id` FROM `shop_offices` ORDER BY `id` ASC LIMIT 1')->fetchColumn();
		if ($officeId > 0) {
			$chk = $db->prepare('SELECT COUNT(*) FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ?');
			$chk->execute(array($officeId, $storageId));
			if ((int) $chk->fetchColumn() === 0) {
				// Try common column set; ignore if schema differs
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
 * Import aggregated Docpart CSV into a price list (reuses deploy importer when present).
 *
 * @return array<string,mixed>
 */
function epc_commerce_import_into_list(PDO $db, string $listName, string $docpartCsvPath): array
{
	$price = epc_price_resolve_or_create_list($db, 0, $listName);
	if (!$price) {
		return array('status' => false, 'message' => 'Could not create price list ' . $listName);
	}
	$priceId = (int) $price['id'];
	epc_commerce_ensure_warehouse($db, $listName, $priceId);

	// Prefer shared import from deploy API if loaded; else local minimal import.
	if (!function_exists('epc_import_csv')) {
		$deploy = $_SERVER['DOCUMENT_ROOT'] . '/epc-upload-uae-prices.php';
		// Do not include deploy script (side effects). Inline a thin import using same column map.
		require_once __DIR__ . '/epc_price_import_helpers.php';
		$result = epc_commerce_import_csv_local($db, $price, $docpartCsvPath);
	} else {
		$result = epc_import_csv($db, $price, $docpartCsvPath, ',');
	}

	$countQ = $db->prepare('SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?');
	$countQ->execute(array($priceId));
	$recordsInDb = (int) $countQ->fetchColumn();

	$origName = basename($docpartCsvPath);
	$storedRel = epc_price_history_archive_file($docpartCsvPath, $priceId, $listName . '.csv');
	$historyId = epc_price_history_save($db, array(
		'price_id' => $priceId,
		'price_name' => $listName,
		'upload_source' => 'commerce_' . (preg_match('/\.P$/i', $listName) ? 'purchase' : (preg_match('/-L$/i', $listName) ? 'inventory' : 'sales')),
		'original_filename' => $origName,
		'stored_relpath' => $storedRel,
		'file_size' => is_file($docpartCsvPath) ? (int) filesize($docpartCsvPath) : 0,
		'status' => !empty($result['status']) ? 'ok' : 'failed',
		'rows_imported' => (int) ($result['records_handled'] ?? 0),
		'rows_skipped' => (int) ($result['rows_skipped'] ?? 0),
		'rows_in_db' => $recordsInDb,
		'brands_count' => epc_price_history_count_brands($db, $priceId),
		'items_count' => $recordsInDb,
		'error_text' => empty($result['status']) ? (string) ($result['message'] ?? '') : '',
		'stats_json' => json_encode(array('commerce' => true, 'delimiter' => ','), JSON_UNESCAPED_UNICODE),
	));
	if ($historyId > 0 && !empty($result['status'])) {
		epc_price_history_set_active($db, $priceId, $historyId);
	}

	return array(
		'status' => !empty($result['status']),
		'message' => (string) ($result['message'] ?? ''),
		'price_id' => $priceId,
		'price_name' => $listName,
		'storage_id' => epc_commerce_ensure_warehouse($db, $listName, $priceId),
		'records_handled' => (int) ($result['records_handled'] ?? 0),
		'rows_skipped' => (int) ($result['rows_skipped'] ?? 0),
		'records_in_db' => $recordsInDb,
		'history_id' => $historyId,
	);
}

/**
 * Local CSV import when deploy epc_import_csv is not in scope.
 *
 * @param array<string,mixed> $price
 * @return array<string,mixed>
 */
function epc_commerce_import_csv_local(PDO $db, array $price, string $filePath): array
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
		if ($art === '' || $priceVal <= 0) {
			$skipped++;
			continue;
		}
		$nextId++;
		$ins->execute(array($nextId, $priceId, $mfr, $art, $show, $name, $exist, $priceVal, 1, '', 0));
		$inserted++;
	}
	fclose($fh);
	$db->prepare('UPDATE `shop_docpart_prices` SET `last_updated` = ? WHERE `id` = ?')->execute(array(time(), $priceId));
	return array(
		'status' => $inserted > 0,
		'message' => $inserted > 0 ? 'Import completed' : 'No valid rows imported',
		'records_handled' => $inserted,
		'rows_skipped' => $skipped,
	);
}

/**
 * Full ingest pipeline from an uploaded/local file.
 *
 * @return array<string,mixed>
 */
function epc_commerce_ingest_file(
	PDO $db,
	string $sourcePath,
	string $role,
	string $baseName,
	float $marginPercent = 0.0,
	string $sourceUrl = ''
): array {
	$role = strtolower(trim($role));
	if (!in_array($role, array('sales', 'purchase', 'inventory'), true)) {
		return array('status' => false, 'message' => 'role must be sales|purchase|inventory');
	}

	$converted = epc_commerce_excel_to_csv($sourcePath);
	if (empty($converted['ok'])) {
		return array('status' => false, 'message' => (string) $converted['message']);
	}
	$csvPath = (string) $converted['path'];
	$tmpConverted = ($csvPath !== $sourcePath);

	$read = epc_commerce_read_source_rows($csvPath, $role);
	if (empty($read['ok'])) {
		if ($tmpConverted && is_file($csvPath)) {
			@unlink($csvPath);
		}
		return array('status' => false, 'message' => (string) $read['message'], 'headers' => $read['headers']);
	}

	$grouped = epc_commerce_aggregate_rows($role, $read['rows'], $baseName, $marginPercent);
	if (count($grouped) === 0) {
		if ($tmpConverted && is_file($csvPath)) {
			@unlink($csvPath);
		}
		return array('status' => false, 'message' => 'No valid commerce rows after aggregation');
	}

	$lists = array();
	$workDir = sys_get_temp_dir() . '/epc_commerce_out_' . getmypid() . '_' . time();
	@mkdir($workDir, 0755, true);

	foreach ($grouped as $listName => $lines) {
		$outCsv = $workDir . '/' . preg_replace('/[^A-Za-z0-9_\-\.]+/', '_', $listName) . '.csv';
		if (!epc_commerce_write_docpart_csv($outCsv, $lines)) {
			$lists[] = array('status' => false, 'price_name' => $listName, 'message' => 'Could not write CSV');
			continue;
		}
		$imported = epc_commerce_import_into_list($db, $listName, $outCsv);
		if ($sourceUrl !== '' && !empty($imported['price_id'])) {
			epc_commerce_store_source_link(
				$db,
				(int) $imported['price_id'],
				$listName,
				$sourceUrl,
				$role,
				$baseName,
				$marginPercent
			);
		} elseif (!empty($imported['price_id'])) {
			// Keep commerce meta even without URL (re-upload path still knows role/margin).
			epc_commerce_store_meta_only($db, (int) $imported['price_id'], $listName, $role, $baseName, $marginPercent);
		}
		$imported['rows_aggregated'] = count($lines);
		$lists[] = $imported;
	}

	if ($tmpConverted && is_file($csvPath)) {
		@unlink($csvPath);
	}
	foreach (glob($workDir . '/*') ?: array() as $f) {
		@unlink($f);
	}
	@rmdir($workDir);

	$ok = false;
	foreach ($lists as $item) {
		if (!empty($item['status'])) {
			$ok = true;
			break;
		}
	}

	return array(
		'status' => $ok,
		'message' => $ok ? 'Commerce ingest completed' : 'Commerce ingest failed',
		'role' => $role,
		'base_name' => $baseName,
		'margin_percent' => $marginPercent,
		'source_rows' => count($read['rows']),
		'lists' => $lists,
	);
}

/**
 * Encode commerce settings into message_header_substring (unused for URL load mode).
 */
function epc_commerce_meta_encode(string $role, string $baseName, float $marginPercent, string $listName = ''): string
{
	$payload = array(
		'v' => 1,
		'role' => strtolower($role),
		'base' => $baseName,
		'margin' => round($marginPercent, 4),
		'list' => $listName,
		'ts' => time(),
	);
	return 'EPC_COMMERCE:' . json_encode($payload, JSON_UNESCAPED_UNICODE);
}

/**
 * @return array{role:string,base:string,margin:float,list:string}|null
 */
function epc_commerce_meta_decode(string $raw): ?array
{
	$raw = trim($raw);
	if ($raw === '' || strpos($raw, 'EPC_COMMERCE:') !== 0) {
		return null;
	}
	$json = substr($raw, strlen('EPC_COMMERCE:'));
	$data = json_decode($json, true);
	if (!is_array($data)) {
		return null;
	}
	return array(
		'role' => strtolower((string) ($data['role'] ?? '')),
		'base' => (string) ($data['base'] ?? ''),
		'margin' => (float) ($data['margin'] ?? 0),
		'list' => (string) ($data['list'] ?? ''),
	);
}

function epc_commerce_role_from_list_name(string $name): string
{
	if (preg_match('/\.P$/i', $name)) {
		return 'purchase';
	}
	if (preg_match('/-L$/i', $name)) {
		return 'inventory';
	}
	if (preg_match('/-S$/i', $name)) {
		return 'sales';
	}
	return '';
}

function epc_commerce_base_from_list_name(string $name, string $role = ''): string
{
	$role = $role !== '' ? $role : epc_commerce_role_from_list_name($name);
	if ($role === 'purchase') {
		return (string) preg_replace('/\.P$/i', '', $name);
	}
	if ($role === 'inventory') {
		return (string) preg_replace('/-L$/i', '', $name);
	}
	if ($role === 'sales') {
		return (string) preg_replace('/-S$/i', '', $name);
	}
	return $name;
}

function epc_commerce_store_meta_only(
	PDO $db,
	int $priceId,
	string $listName,
	string $role,
	string $baseName,
	float $marginPercent
): void {
	if ($priceId <= 0) {
		return;
	}
	$meta = epc_commerce_meta_encode($role, $baseName, $marginPercent, $listName);
	try {
		$db->prepare('UPDATE `shop_docpart_prices` SET `file_name_substring` = ?, `message_header_substring` = ? WHERE `id` = ?')
			->execute(array($listName, $meta, $priceId));
	} catch (Throwable $e) {
	}
}

function epc_commerce_store_source_link(
	PDO $db,
	int $priceId,
	string $listName,
	string $sourceUrl,
	string $role,
	string $baseName,
	float $marginPercent
): void {
	if ($priceId <= 0 || $sourceUrl === '') {
		return;
	}
	$meta = epc_commerce_meta_encode($role, $baseName, $marginPercent, $listName);
	try {
		$db->prepare(
			'UPDATE `shop_docpart_prices`
			 SET `link` = ?, `load_mode` = 4, `file_name_substring` = ?, `message_header_substring` = ?
			 WHERE `id` = ?'
		)->execute(array($sourceUrl, $listName, $meta, $priceId));
	} catch (Throwable $e) {
	}
}

/**
 * Download a commerce source URL to a temp file.
 *
 * @return array{ok:bool,path:string,message:string,http:int}
 */
function epc_commerce_download_url(string $url): array
{
	$url = trim($url);
	if ($url === '' || !preg_match('#^https?://#i', $url)) {
		return array('ok' => false, 'path' => '', 'message' => 'Invalid http(s) URL', 'http' => 0);
	}
	$ch = curl_init($url);
	curl_setopt_array($ch, array(
		CURLOPT_RETURNTRANSFER => true,
		CURLOPT_FOLLOWLOCATION => true,
		CURLOPT_TIMEOUT => 180,
		CURLOPT_SSL_VERIFYPEER => false,
		CURLOPT_SSL_VERIFYHOST => 0,
		CURLOPT_USERAGENT => 'EPC-CommerceIngest/1.0',
	));
	$body = curl_exec($ch);
	$code = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
	$err = curl_error($ch);
	curl_close($ch);
	if ($body === false || $code >= 400 || $body === '') {
		return array(
			'ok' => false,
			'path' => '',
			'message' => 'Failed to download link' . ($err !== '' ? ': ' . $err : ''),
			'http' => $code,
		);
	}
	$ext = 'csv';
	$pathPart = strtolower((string) parse_url($url, PHP_URL_PATH));
	if (preg_match('/\.(xlsx|xls|csv|txt)$/', $pathPart, $m)) {
		$ext = $m[1];
	}
	$filePath = sys_get_temp_dir() . '/epc_commerce_url_' . getmypid() . '_' . time() . '.' . $ext;
	if (file_put_contents($filePath, $body) === false) {
		return array('ok' => false, 'path' => '', 'message' => 'Could not write downloaded file', 'http' => $code);
	}
	return array('ok' => true, 'path' => $filePath, 'message' => 'ok', 'http' => $code);
}

/**
 * Refresh one price list from its stored URL (+ stored margin/role).
 *
 * @return array<string,mixed>
 */
function epc_commerce_refresh_price_id(PDO $db, int $priceId, ?float $marginOverride = null): array
{
	if ($priceId <= 0) {
		return array('status' => false, 'message' => 'price_id required');
	}
	$q = $db->prepare('SELECT * FROM `shop_docpart_prices` WHERE `id` = ? LIMIT 1');
	$q->execute(array($priceId));
	$price = $q->fetch(PDO::FETCH_ASSOC);
	if (!$price) {
		return array('status' => false, 'message' => 'Price list not found');
	}
	$url = trim((string) ($price['link'] ?? ''));
	if ($url === '' || !preg_match('#^https?://#i', $url)) {
		return array('status' => false, 'message' => 'No http(s) link on this price list', 'price_id' => $priceId);
	}

	$name = (string) ($price['name'] ?? '');
	$meta = epc_commerce_meta_decode((string) ($price['message_header_substring'] ?? ''));
	$role = ($meta && $meta['role'] !== '') ? $meta['role'] : epc_commerce_role_from_list_name($name);
	$base = ($meta && $meta['base'] !== '') ? $meta['base'] : epc_commerce_base_from_list_name($name, $role);
	$margin = $marginOverride !== null
		? (float) $marginOverride
		: (($meta !== null) ? (float) $meta['margin'] : 0.0);

	if ($role === '') {
		return array('status' => false, 'message' => 'Cannot detect commerce role from list name ' . $name, 'price_id' => $priceId);
	}

	$dl = epc_commerce_download_url($url);
	if (empty($dl['ok'])) {
		$dl['status'] = false;
		$dl['price_id'] = $priceId;
		$dl['price_name'] = $name;
		return $dl;
	}

	$result = epc_commerce_ingest_file($db, (string) $dl['path'], $role, $base, $margin, $url);
	@unlink((string) $dl['path']);
	$result['action'] = 'refresh_url';
	$result['source_url'] = $url;
	$result['price_id'] = $priceId;
	$result['price_name'] = $name;
	$result['role'] = $role;
	$result['margin_percent'] = $margin;
	return $result;
}

/**
 * Lists commerce price sources (S/P/L naming or EPC_COMMERCE meta).
 *
 * @return list<array<string,mixed>>
 */
function epc_commerce_list_sources(PDO $db, bool $urlOnly = false): array
{
	$rows = array();
	try {
		$sql = 'SELECT `id`, `name`, `link`, `load_mode`, `message_header_substring`, `last_updated`
			FROM `shop_docpart_prices`
			WHERE (`name` LIKE \'%-S\' OR `name` LIKE \'%.P\' OR `name` LIKE \'%-L\'
				OR `message_header_substring` LIKE \'EPC_COMMERCE:%\')';
		if ($urlOnly) {
			$sql .= ' AND `load_mode` = 4 AND `link` LIKE \'http%\'';
		}
		$sql .= ' ORDER BY `name` ASC';
		$rows = $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
	} catch (Throwable $e) {
		return array();
	}
	$out = array();
	foreach ($rows as $row) {
		$meta = epc_commerce_meta_decode((string) ($row['message_header_substring'] ?? ''));
		$name = (string) ($row['name'] ?? '');
		$role = ($meta && $meta['role'] !== '') ? $meta['role'] : epc_commerce_role_from_list_name($name);
		$out[] = array(
			'price_id' => (int) $row['id'],
			'price_name' => $name,
			'role' => $role,
			'base_name' => ($meta && $meta['base'] !== '') ? $meta['base'] : epc_commerce_base_from_list_name($name, $role),
			'margin_percent' => ($meta !== null) ? (float) $meta['margin'] : 0.0,
			'link' => (string) ($row['link'] ?? ''),
			'load_mode' => (int) ($row['load_mode'] ?? 0),
			'last_updated' => (int) ($row['last_updated'] ?? 0),
			'has_url' => preg_match('#^https?://#i', (string) ($row['link'] ?? '')) === 1,
		);
	}
	return $out;
}

/**
 * Refresh every commerce list that has a stored URL.
 *
 * @return array<string,mixed>
 */
function epc_commerce_refresh_all_linked(PDO $db): array
{
	$sources = epc_commerce_list_sources($db, true);
	$results = array();
	$ok = 0;
	$fail = 0;
	foreach ($sources as $src) {
		$one = epc_commerce_refresh_price_id($db, (int) $src['price_id']);
		if (!empty($one['status'])) {
			$ok++;
		} else {
			$fail++;
		}
		$results[] = array(
			'price_id' => (int) $src['price_id'],
			'price_name' => (string) $src['price_name'],
			'status' => !empty($one['status']),
			'message' => (string) ($one['message'] ?? ''),
			'source_rows' => (int) ($one['source_rows'] ?? 0),
		);
	}
	return array(
		'status' => $ok > 0 || ($ok + $fail) === 0,
		'message' => count($sources) === 0
			? 'No commerce URL-linked lists found'
			: ('Refreshed ' . $ok . ' ok / ' . $fail . ' failed of ' . count($sources)),
		'ok' => $ok,
		'failed' => $fail,
		'total' => count($sources),
		'results' => $results,
	);
}
