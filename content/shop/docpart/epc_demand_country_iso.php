<?php
/**
 * ISO 3166-1 alpha-3 demand country helpers + CSV import.
 */

/** @return array<string, string> ISO2 => ISO3 */
function epc_demand_iso2_to_iso3_map(): array
{
	return array(
		'SD' => 'SDN',
		'DZ' => 'DZA',
		'KE' => 'KEN',
		'AE' => 'ARE',
		'EG' => 'EGY',
		'NG' => 'NGA',
		'SA' => 'SAU',
	);
}

function epc_demand_country_registry(): array
{
	return array(
		'SDN' => array('code' => 'SDN', 'name' => 'Sudan', 'iso2' => 'SD'),
		'DZA' => array('code' => 'DZA', 'name' => 'Algeria', 'iso2' => 'DZ'),
		'KEN' => array('code' => 'KEN', 'name' => 'Kenya', 'iso2' => 'KE'),
		'ARE' => array('code' => 'ARE', 'name' => 'United Arab Emirates', 'iso2' => 'AE'),
		'EGY' => array('code' => 'EGY', 'name' => 'Egypt', 'iso2' => 'EG'),
		'NGA' => array('code' => 'NGA', 'name' => 'Nigeria', 'iso2' => 'NG'),
		'SAU' => array('code' => 'SAU', 'name' => 'Saudi Arabia', 'iso2' => 'SA'),
	);
}

/**
 * UAE stock pool only — not a selectable demand market.
 */
function epc_demand_is_stock_pool_country_code(string $code): bool
{
	return in_array($code, array('ARE', 'AE'), true);
}

function epc_demand_normalize_country_code(string $code): string
{
	$code = strtoupper(preg_replace('/[^A-Z]/', '', trim($code)));
	if ($code === '') {
		return '';
	}
	$registry = epc_demand_country_registry();
	if (isset($registry[$code])) {
		return $code;
	}
	if (strlen($code) === 2) {
		$map = epc_demand_iso2_to_iso3_map();
		if (isset($map[$code])) {
			return $map[$code];
		}
	}
	return '';
}

/**
 * Parse "SDN,DZA,KEN" or "SDN; DZA" into unique ISO3 codes (sorted).
 *
 * @return array<int, string>
 */
function epc_demand_parse_country_codes_string(string $raw): array
{
	$raw = trim($raw);
	if ($raw === '') {
		return array();
	}
	$parts = preg_split('/[,;|\/]+/', $raw);
	$codes = array();
	foreach ($parts as $part) {
		$part = trim($part);
		if ($part === '') {
			continue;
		}
		$norm = epc_demand_normalize_country_code($part);
		if ($norm !== '') {
			$codes[$norm] = true;
		}
	}
	$out = array_keys($codes);
	sort($out);
	return $out;
}

/** Comma-separated ISO3 for display, e.g. SDN,DZA,KEN */
function epc_demand_format_countries_display(array $codes): string
{
	$clean = array();
	foreach ($codes as $code) {
		$norm = epc_demand_normalize_country_code((string)$code);
		if ($norm !== '') {
			$clean[$norm] = true;
		}
	}
	$list = array_keys($clean);
	sort($list);
	return implode(',', $list);
}

function epc_demand_migrate_country_codes_to_iso3(PDO $db): void
{
	static $ran = false;
	if ($ran) {
		return;
	}
	$ran = true;

	$needs = false;
	try {
		$sample = $db->query('SELECT `code` FROM `epc_demand_country` LIMIT 1')->fetchColumn();
		if ($sample !== false && strlen((string)$sample) === 2) {
			$needs = true;
		}
	} catch (Throwable $e) {
		return;
	}
	if (!$needs) {
		try {
			$col = $db->query("SHOW COLUMNS FROM `epc_demand_country` LIKE 'code'")->fetch(PDO::FETCH_ASSOC);
			if ($col && isset($col['Type']) && stripos((string)$col['Type'], 'char(2)') !== false) {
				$needs = true;
			}
		} catch (Throwable $e) {
		}
	}
	if (!$needs) {
		return;
	}

	$map = epc_demand_iso2_to_iso3_map();
	$registry = epc_demand_country_registry();
	$order = 0;

	foreach ($registry as $iso3 => $meta) {
		$order += 10;
		$iso2 = isset($meta['iso2']) ? (string)$meta['iso2'] : '';
		$name = (string)$meta['name'];
		if ($iso2 !== '') {
			$db->prepare(
				'INSERT INTO `epc_demand_country` (`code`, `name`, `sort_order`) VALUES (?, ?, ?)
				 ON DUPLICATE KEY UPDATE `name` = VALUES(`name`), `sort_order` = VALUES(`sort_order`)'
			)->execute(array($iso3, $name, $order));
		}
	}

	foreach (array('epc_article_demand', 'epc_price_list_demand', 'epc_user_demand_country') as $table) {
		foreach ($map as $iso2 => $iso3) {
			try {
				$db->prepare("UPDATE `{$table}` SET `country_code` = ? WHERE `country_code` = ?")->execute(array($iso3, $iso2));
			} catch (Throwable $e) {
			}
		}
	}

	foreach ($map as $iso2 => $iso3) {
		try {
			$db->prepare('DELETE FROM `epc_demand_country` WHERE `code` = ?')->execute(array($iso2));
		} catch (Throwable $e) {
		}
	}

	$alters = array(
		'epc_demand_country' => 'ALTER TABLE `epc_demand_country` MODIFY `code` CHAR(3) NOT NULL',
		'epc_article_demand' => 'ALTER TABLE `epc_article_demand` MODIFY `country_code` CHAR(3) NOT NULL',
		'epc_price_list_demand' => 'ALTER TABLE `epc_price_list_demand` MODIFY `country_code` CHAR(3) NOT NULL',
		'epc_user_demand_country' => 'ALTER TABLE `epc_user_demand_country` MODIFY `country_code` CHAR(3) NOT NULL',
	);
	foreach ($alters as $sql) {
		try {
			$db->exec($sql);
		} catch (Throwable $e) {
		}
	}
}

/**
 * @param array<int, string> $headerRow lowercased trimmed headers
 * @param array<int, string> $dataRow
 * @return array{brand:string, article:string, countries:array<int,string>}
 */
function epc_demand_csv_parse_row(array $headerRow, array $dataRow): array
{
	$brand = '';
	$article = '';
	$countries = array();
	$abc = array();

	foreach ($headerRow as $i => $h) {
		$h = strtolower(trim((string)$h));
		$val = isset($dataRow[$i]) ? trim((string)$dataRow[$i]) : '';
		if ($h === '') {
			continue;
		}
		if (in_array($h, array('brand', 'manufacturer', 'make', 'producer'), true)) {
			$brand = $val;
		} elseif (in_array($h, array('article', 'article_show', 'part_number', 'part', 'sku', 'number'), true)) {
			$article = $val;
		} elseif (in_array($h, array('countries', 'demand_countries', 'country_codes', 'demand', 'country'), true)) {
			$countries = array_merge($countries, epc_demand_parse_country_codes_string($val));
		} elseif (preg_match('/^country[_\s-]?([a-z])$/', $h, $m)) {
			$abc[strtoupper($m[1])] = $val;
		} elseif (strlen($h) === 1 && $h >= 'a' && $h <= 'z') {
			$abc[strtoupper($h)] = $val;
		}
	}

	foreach ($abc as $letter => $val) {
		if ($val === '') {
			continue;
		}
		$norm = epc_demand_normalize_country_code($val);
		if ($norm !== '') {
			$countries[] = $norm;
		}
	}

	$countries = array_values(array_unique($countries));
	sort($countries);

	return array(
		'brand' => $brand,
		'article' => $article,
		'countries' => $countries,
	);
}

/**
 * @return array{status:bool, message?:string, rows?:array, stats?:array}
 */
function epc_demand_csv_preview_file(string $filePath, int $maxRows = 25): array
{
	if (!is_readable($filePath)) {
		return array('status' => false, 'message' => 'File not found');
	}
	$fh = fopen($filePath, 'rb');
	if (!$fh) {
		return array('status' => false, 'message' => 'Cannot open file');
	}
	$header = fgetcsv($fh, 0, ',');
	if (!$header) {
		fclose($fh);
		return array('status' => false, 'message' => 'Empty CSV');
	}
	$headerNorm = array();
	foreach ($header as $h) {
		$headerNorm[] = strtolower(trim((string)$h));
	}
	$rows = array();
	$line = 1;
	while (($data = fgetcsv($fh, 0, ',')) !== false) {
		$line++;
		if (count(array_filter($data, static function ($c) {
			return trim((string)$c) !== '';
		})) === 0) {
			continue;
		}
		$parsed = epc_demand_csv_parse_row($headerNorm, $data);
		$errors = array();
		if ($parsed['brand'] === '') {
			$errors[] = 'missing brand';
		}
		if ($parsed['article'] === '') {
			$errors[] = 'missing article';
		}
		if (empty($parsed['countries'])) {
			$errors[] = 'no valid country codes (use ISO3: SDN,DZA,KEN)';
		}
		$rows[] = array(
			'line' => $line,
			'brand' => $parsed['brand'],
			'article' => $parsed['article'],
			'countries' => epc_demand_format_countries_display($parsed['countries']),
			'errors' => $errors,
		);
		if (count($rows) >= $maxRows) {
			break;
		}
	}
	fclose($fh);
	return array('status' => true, 'rows' => $rows);
}

/**
 * @return array{status:bool, message?:string, stats?:array}
 */
function epc_demand_csv_import_file(PDO $db, string $filePath, string $mode = 'merge'): array
{
	require_once __DIR__ . '/docpart_article_match.php';
	require_once __DIR__ . '/epc_demand_intelligence.php';
	epc_demand_ensure_schema($db);

	$fh = fopen($filePath, 'rb');
	if (!$fh) {
		return array('status' => false, 'message' => 'Cannot open file');
	}
	$header = fgetcsv($fh, 0, ',');
	if (!$header) {
		fclose($fh);
		return array('status' => false, 'message' => 'Empty CSV');
	}
	$headerNorm = array();
	foreach ($header as $h) {
		$headerNorm[] = strtolower(trim((string)$h));
	}

	$replaceRow = ($mode === 'replace');
	$now = time();
	$stats = array(
		'rows_read' => 0,
		'rows_ok' => 0,
		'rows_skipped' => 0,
		'tags_inserted' => 0,
		'tags_removed' => 0,
		'errors' => array(),
	);

	$ins = $db->prepare(
		'INSERT INTO `epc_article_demand` (`manufacturer`, `article_norm`, `country_code`, `source`, `notes`, `created_at`)
		 VALUES (?, ?, ?, ?, ?, ?)
		 ON DUPLICATE KEY UPDATE `source` = VALUES(`source`), `notes` = VALUES(`notes`)'
	);
	$delPart = $db->prepare(
		'DELETE FROM `epc_article_demand` WHERE UPPER(`manufacturer`) = UPPER(?) AND `article_norm` = ?'
	);
	$line = 1;
	while (($data = fgetcsv($fh, 0, ',')) !== false) {
		$line++;
		if (count(array_filter($data, static function ($c) {
			return trim((string)$c) !== '';
		})) === 0) {
			continue;
		}
		$stats['rows_read']++;
		$parsed = epc_demand_csv_parse_row($headerNorm, $data);
		$brand = trim($parsed['brand']);
		$article = trim($parsed['article']);
		$countries = $parsed['countries'];
		if ($brand === '' || $article === '' || empty($countries)) {
			$stats['rows_skipped']++;
			if (count($stats['errors']) < 30) {
				$stats['errors'][] = "Line {$line}: invalid row";
			}
			continue;
		}
		$article_norm = docpart_normalize_article_for_price($article);
		if ($article_norm === '') {
			$stats['rows_skipped']++;
			continue;
		}

		if ($replaceRow) {
			$delPart->execute(array($brand, $article_norm));
			$stats['tags_removed'] += (int)$delPart->rowCount();
		}

		foreach ($countries as $code) {
			$code = epc_demand_normalize_country_code($code);
			if ($code === '' || epc_demand_is_stock_pool_country_code($code)) {
				continue;
			}
			try {
				$ins->execute(array($brand, $article_norm, $code, 'cp_csv_upload', 'CP demand CSV', $now));
				$stats['tags_inserted']++;
			} catch (Throwable $e) {
				if (count($stats['errors']) < 30) {
					$stats['errors'][] = "Line {$line}: " . $e->getMessage();
				}
			}
		}
		$stats['rows_ok']++;
	}
	fclose($fh);

	return array(
		'status' => true,
		'stats' => $stats,
		'message' => 'Import finished',
	);
}
