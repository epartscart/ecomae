<?php
/**
 * Price CSV import helpers: column labels and detailed skip/error rows for upload history.
 */

function epc_price_history_source_label(string $uploadSource): string
{
	static $labels = [
		'cp_wizard' => 'CP upload wizard',
		'pyprices_upload' => 'PC upload (pyprices)',
		'pyprices_pc' => 'PC upload (pyprices)',
		'pyprices' => 'Pyprices',
		'pyprices_ftp' => 'FTP (pyprices)',
		'pyprices_email' => 'Email (pyprices)',
		'pyprices_url' => 'URL (pyprices)',
		'deploy_api' => 'Deploy API',
		'deploy_r_uae' => 'Deploy import',
		'email_backfill' => 'E-mail / file archive',
		'api_legacy' => 'Legacy API',
		'commerce_sales' => 'Commerce sales (*-S)',
		'commerce_purchase' => 'Commerce purchase (*.P)',
		'commerce_inventory' => 'Commerce inventory (*-L)',
	];
	$key = strtolower(trim($uploadSource));
	return $labels[$key] ?? ($uploadSource !== '' ? $uploadSource : 'Unknown');
}

function epc_price_history_format_raw_row(array $row): string
{
	$parts = [];
	foreach ($row as $cell) {
		$parts[] = str_replace(["\r", "\n", '|'], ' ', (string)$cell);
	}
	return implode(' | ', $parts);
}

/**
 * Human-readable explanation for a reason code.
 */
function epc_price_history_reason_label(string $reasonCode): string
{
	static $labels = [
		'empty_row' => 'Skipped: the row is empty',
		'empty_article' => 'Skipped: article/number column is empty after normalization (spaces and special characters removed)',
		'invalid_price' => 'Skipped: price column is zero or not a valid number',
		'import_error' => 'Error: import failed',
		'validation' => 'Error: file validation failed',
		'error' => 'Error: processing error',
		'general' => 'Error: general import error',
		'info' => 'Notice',
	];
	return $labels[$reasonCode] ?? ('Issue: ' . $reasonCode);
}

/**
 * Highest configured 1-based column index in the price list mapping.
 *
 * @param array<string,int> $operationalCols
 */
function epc_price_max_config_column(array $operationalCols): int
{
	$max = 0;
	foreach ($operationalCols as $colNum) {
		$colNum = (int)$colNum;
		if ($colNum > $max) {
			$max = $colNum;
		}
	}
	return $max;
}

/**
 * Build CSV header labels for each source column (file header + price config roles).
 *
 * @param array<int,string>|null $headerRow First row of the file when strings_to_left skips it
 * @param array<string,int> $operationalCols e.g. manufacturer => 1, article => 2
 * @return array<int,string> index => label for fputcsv columns
 */
function epc_price_build_source_column_labels(?array $headerRow, array $operationalCols, int $minColumns = 0): array
{
	$maxCol = max(epc_price_max_config_column($operationalCols), $minColumns);
	if (is_array($headerRow) && count($headerRow) > $maxCol) {
		$maxCol = count($headerRow);
	}
	$roleByIndex = [];
	foreach ($operationalCols as $role => $colNum) {
		$colNum = (int)$colNum;
		if ($colNum > 0) {
			$roleByIndex[$colNum - 1] = (string)$role;
		}
	}
	$labels = [];
	for ($i = 0; $i < $maxCol; $i++) {
		$hdr = '';
		if (is_array($headerRow) && isset($headerRow[$i])) {
			$hdr = trim((string)$headerRow[$i]);
		}
		$role = $roleByIndex[$i] ?? '';
		if ($hdr !== '') {
			$labels[$i] = 'Col' . ($i + 1) . ': ' . $hdr . ($role !== '' ? ' [' . $role . ']' : '');
		} elseif ($role !== '') {
			$labels[$i] = 'Col' . ($i + 1) . ' [' . $role . ']';
		} else {
			$labels[$i] = 'Col' . ($i + 1);
		}
	}
	return $labels;
}

/**
 * Map price list DB row to operational column numbers.
 *
 * @param array<string,mixed> $price
 * @return array<string,int>
 */
/**
 * Normalize stock quantity from CSV (handles decimals mistaken for qty, caps overflow).
 */
function epc_parse_stock_quantity($raw): int
{
	$raw = trim((string)$raw);
	if ($raw === '') {
		return 0;
	}
	$compact = str_replace([' ', "\xC2\xA0"], '', $raw);
	if (preg_match('/^-?\d+[.,]\d+$/', $compact)) {
		$parts = preg_split('/[.,]/', $compact);
		$compact = (string)($parts[0] ?? '0');
	}
	$digits = preg_replace('/[^0-9]/', '', $compact);
	if ($digits === '') {
		return 0;
	}
	$qty = (int)$digits;
	if ($qty > 999999) {
		$qty = 999999;
	}
	return $qty;
}

/**
 * Ensure `exist` accepts warehouse quantities (some installs used TINYINT/SMALLINT).
 */
function epc_ensure_prices_data_exist_column_int(PDO $db): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$done = true;
	try {
		$col = $db->query(
			"SELECT `DATA_TYPE`, `COLUMN_TYPE`
			FROM `information_schema`.`COLUMNS`
			WHERE `TABLE_SCHEMA` = DATABASE()
			AND `TABLE_NAME` = 'shop_docpart_prices_data'
			AND `COLUMN_NAME` = 'exist'
			LIMIT 1"
		)->fetch(PDO::FETCH_ASSOC);
		if (!$col) {
			return;
		}
		$dataType = strtolower((string)($col['DATA_TYPE'] ?? ''));
		if (!in_array($dataType, ['tinyint', 'smallint', 'mediumint'], true)) {
			return;
		}
		$db->exec(
			'ALTER TABLE `shop_docpart_prices_data`
			MODIFY COLUMN `exist` INT(11) NOT NULL DEFAULT 0'
		);
	} catch (Throwable $e) {
		// Keep import running; row-level cap still applies.
	}
}

function epc_price_operational_cols_from_config(array $price): array
{
	return [
		'manufacturer' => (int)($price['manufacturer_col'] ?? 0),
		'article' => (int)($price['article_col'] ?? 0),
		'name' => (int)($price['name_col'] ?? 0),
		'exist' => (int)($price['exist_col'] ?? 0),
		'price' => (int)($price['price_col'] ?? 0),
		'time_to_exe' => (int)($price['time_to_exe_col'] ?? 0),
		'storage' => (int)($price['storage_col'] ?? 0),
		'min_order' => (int)($price['min_order_col'] ?? 0),
	];
}

/**
 * One skip/error record with every source column value and parsed fields.
 *
 * @param array<int,string> $csvRow
 * @param array<int,string> $columnLabels
 * @param array<string,mixed> $parsed manufacturer, article, price, details, ...
 * @return array<string,mixed>
 */
function epc_price_history_issue_detail(
	int $lineNo,
	string $issueType,
	string $reasonCode,
	array $csvRow,
	array $columnLabels,
	array $parsed = []
): array {
	$details = (string)($parsed['details'] ?? '');
	$why = epc_price_history_reason_label($reasonCode);
	if ($details !== '') {
		$why .= ' — ' . $details;
	}

	$issue = [
		'line_no' => $lineNo,
		'issue_type' => $issueType,
		'reason_code' => $reasonCode,
		'why_skipped_or_error' => $why,
		'error_details' => $details,
		'reason' => $reasonCode,
		'details' => $details,
	];

	foreach ($columnLabels as $idx => $label) {
		$issue[$label] = isset($csvRow[$idx]) ? trim((string)$csvRow[$idx]) : '';
	}
	for ($i = count($columnLabels); $i < count($csvRow); $i++) {
		$label = 'Col' . ($i + 1);
		$issue[$label] = trim((string)$csvRow[$i]);
	}

	foreach (['manufacturer', 'article', 'article_show', 'name', 'exist', 'price'] as $key) {
		if (array_key_exists($key, $parsed)) {
			$issue['parsed_' . $key] = (string)$parsed[$key];
			$issue[$key] = (string)$parsed[$key];
		}
	}

	$issue['raw_row'] = epc_price_history_format_raw_row($csvRow);
	return $issue;
}

/**
 * Issue without a source file row (module / validation messages).
 *
 * @param array<string,mixed> $fields
 * @return array<string,mixed>
 */
function epc_price_history_issue_module(int $lineNo, string $issueType, string $reasonCode, array $fields = []): array
{
	$details = (string)($fields['details'] ?? '');
	$why = epc_price_history_reason_label($reasonCode);
	if ($details !== '') {
		$why .= ' — ' . $details;
	}
	return [
		'line_no' => $lineNo,
		'issue_type' => $issueType,
		'reason_code' => $reasonCode,
		'why_skipped_or_error' => $why,
		'error_details' => $details,
		'reason' => $reasonCode,
		'details' => $details,
	];
}

/**
 * @param array<int,array<string,mixed>> $issues
 * @return array<int,string>
 */
function epc_price_history_issues_csv_header_row(array $issues): array
{
	$lead = ['line_no', 'issue_type', 'reason_code', 'why_skipped_or_error', 'error_details'];
	$parsedKeys = ['parsed_manufacturer', 'parsed_article', 'parsed_article_show', 'parsed_name', 'parsed_exist', 'parsed_price'];
	$skipLegacy = ['reason', 'details', 'raw_row', 'manufacturer', 'article', 'article_show', 'name', 'exist', 'price'];
	$srcCols = [];
	$hasParsed = false;

	foreach ($issues as $issue) {
		foreach (array_keys($issue) as $key) {
			if (in_array($key, $lead, true) || in_array($key, $skipLegacy, true)) {
				continue;
			}
			if (strpos($key, 'parsed_') === 0) {
				$hasParsed = true;
				continue;
			}
			$srcCols[$key] = true;
		}
	}

	$srcKeys = array_keys($srcCols);
	usort($srcKeys, static function (string $a, string $b): int {
		$na = 0;
		$nb = 0;
		if (preg_match('/Col(\d+)/', $a, $ma)) {
			$na = (int)$ma[1];
		}
		if (preg_match('/Col(\d+)/', $b, $mb)) {
			$nb = (int)$mb[1];
		}
		if ($na !== $nb) {
			return $na <=> $nb;
		}
		return strcmp($a, $b);
	});

	$header = $lead;
	$header = array_merge($header, $srcKeys);
	if ($hasParsed) {
		foreach ($parsedKeys as $pk) {
			foreach ($issues as $issue) {
				if (array_key_exists($pk, $issue)) {
					$header[] = $pk;
					break;
				}
			}
		}
	}
	return $header;
}

/**
 * @param array<int,array<string,mixed>> $issues
 * @return array<int,array<int,string|int>>
 */
function epc_price_history_issues_to_csv_rows(array $issues): array
{
	$header = epc_price_history_issues_csv_header_row($issues);
	$rows = [$header];
	foreach ($issues as $issue) {
		$row = [];
		foreach ($header as $col) {
			$row[] = $issue[$col] ?? '';
		}
		$rows[] = $row;
	}
	return $rows;
}

/**
 * Scan archived CSV and collect skip issues only (no DB insert).
 *
 * @return array{issues:array<int,array<string,mixed>>,column_labels:array<int,string>,rows_skipped:int}
 */
function epc_price_scan_csv_issues(PDO $db, array $price, string $filePath): array
{
	$skipRows = max(0, (int)$price['strings_to_left']);
	$delimiter = (string)$price['separator'];
	if ($delimiter === '\t' || $delimiter === '\\t') {
		$delimiter = "\t";
	}
	if ($delimiter === '') {
		$delimiter = ',';
	}
	$operationalCols = epc_price_operational_cols_from_config($price);

	$fh = fopen($filePath, 'rb');
	if ($fh === false) {
		return ['issues' => [], 'column_labels' => [], 'rows_skipped' => 0];
	}

	$headerRow = null;
	if ($skipRows > 0) {
		$headerRow = fgetcsv($fh, 0, $delimiter);
		for ($i = 1; $i < $skipRows; $i++) {
			fgetcsv($fh, 0, $delimiter);
		}
	}

	$columnLabels = epc_price_build_source_column_labels(
		is_array($headerRow) ? $headerRow : null,
		$operationalCols
	);

	$issues = [];
	$skipped = 0;
	$lineNo = $skipRows;

	while (($row = fgetcsv($fh, 0, $delimiter)) !== false) {
		$lineNo++;
		if (count($row) > count($columnLabels)) {
			$columnLabels = epc_price_build_source_column_labels(
				is_array($headerRow) ? $headerRow : null,
				$operationalCols,
				count($row)
			);
		}

		if (!$row || (count($row) === 1 && trim((string)$row[0]) === '')) {
			$skipped++;
			$issues[] = epc_price_history_issue_detail(
				$lineNo,
				'skipped',
				'empty_row',
				is_array($row) ? $row : [],
				$columnLabels
			);
			continue;
		}

		$article = '';
		$articleShow = '';
		if ($operationalCols['article'] > 0) {
			$articleShow = trim((string)($row[$operationalCols['article'] - 1] ?? ''));
			$article = strtoupper(str_replace(
				[' ', '-', '_', '`', '/', "'", '"', '\\', '.', ',', '#', "\r\n", "\r", "\n", "\t"],
				'',
				(string)($row[$operationalCols['article'] - 1] ?? '')
			));
		}

		$manufacturer = '';
		if ($operationalCols['manufacturer'] > 0) {
			$manufacturer = trim((string)($row[$operationalCols['manufacturer'] - 1] ?? ''));
		}
		$name = '';
		if ($operationalCols['name'] > 0) {
			$name = trim((string)($row[$operationalCols['name'] - 1] ?? ''));
		}
		$exist = '';
		if ($operationalCols['exist'] > 0) {
			$exist = (string)($row[$operationalCols['exist'] - 1] ?? '');
		}
		$priceVal = 0.0;
		$priceRaw = '';
		if ($operationalCols['price'] > 0) {
			$priceRaw = (string)($row[$operationalCols['price'] - 1] ?? '');
			$priceVal = (float)str_replace([' ', ','], ['', '.'], $priceRaw);
		}

		if ($article === '') {
			$skipped++;
			$issues[] = epc_price_history_issue_detail(
				$lineNo,
				'skipped',
				'empty_article',
				$row,
				$columnLabels,
				[
					'manufacturer' => $manufacturer,
					'article_show' => $articleShow,
					'name' => $name,
					'exist' => $exist,
					'price' => $priceRaw,
					'details' => 'Article column value: "' . $articleShow . '"',
				]
			);
			continue;
		}

		if ($priceVal <= 0) {
			$skipped++;
			$issues[] = epc_price_history_issue_detail(
				$lineNo,
				'skipped',
				'invalid_price',
				$row,
				$columnLabels,
				[
					'manufacturer' => $manufacturer,
					'article' => $article,
					'article_show' => $articleShow,
					'name' => $name,
					'exist' => $exist,
					'price' => $priceRaw,
					'details' => 'Price column value: "' . $priceRaw . '"',
				]
			);
		}
	}

	fclose($fh);
	return ['issues' => $issues, 'column_labels' => $columnLabels, 'rows_skipped' => $skipped];
}
