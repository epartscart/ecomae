<?php
/**
 * Warehouse / multivendor product custom fields (engine code, size, cross ref, …)
 * + searchable attribute index for storefront “More info” search.
 *
 * Storage uses side tables (no ALTER on shop_docpart_prices_data) to avoid
 * table locks on large live price catalogs.
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

/**
 * Catalog of known searchable fields.
 *
 * @return array<string,array{label:string,aliases:list<string>,searchable:bool}>
 */
function epc_price_extra_field_catalog(): array
{
	return array(
		'engine_code' => array(
			'label' => 'Engine code',
			'aliases' => array(
				'engine_code', 'engine code', 'engine_number', 'engine number',
				'engine no', 'engine_no', 'eng_code', 'eng code', 'engine',
			),
			'searchable' => true,
		),
		'country_code' => array(
			'label' => 'Country code',
			'aliases' => array(
				'country_code', 'country code', 'country', 'origin', 'made_in',
				'made in', 'coo', 'country of origin',
			),
			'searchable' => true,
		),
		'size' => array(
			'label' => 'Size',
			'aliases' => array(
				'size', 'dimension', 'dimensions', 'dia', 'diameter', 'width',
				'length', 'height', 'measure',
			),
			'searchable' => true,
		),
		'cross_reference' => array(
			'label' => 'Cross reference',
			'aliases' => array(
				'cross_reference', 'cross reference', 'cross_ref', 'cross ref',
				'xref', 'x-ref', 'interchange', 'supersession', 'supersede',
				'reference', 'ref_no', 'ref no',
			),
			'searchable' => true,
		),
		'oe_number' => array(
			'label' => 'OE number',
			'aliases' => array(
				'oe_number', 'oe number', 'oe', 'oe_no', 'oe no',
				'original_number', 'original number', 'oem_number', 'oem number',
			),
			'searchable' => true,
		),
		'color' => array(
			'label' => 'Color',
			'aliases' => array('color', 'colour'),
			'searchable' => true,
		),
		'weight' => array(
			'label' => 'Weight',
			'aliases' => array('weight', 'wt', 'net_weight', 'net weight'),
			'searchable' => true,
		),
		'model' => array(
			'label' => 'Model',
			'aliases' => array('model', 'car_model', 'car model', 'vehicle_model', 'vehicle model'),
			'searchable' => true,
		),
		'year' => array(
			'label' => 'Year',
			'aliases' => array('year', 'years', 'model_year', 'model year', 'yr'),
			'searchable' => true,
		),
		'position' => array(
			'label' => 'Position',
			'aliases' => array('position', 'pos', 'side', 'location', 'lh_rh', 'lh/rh'),
			'searchable' => true,
		),
		'material' => array(
			'label' => 'Material',
			'aliases' => array('material', 'mat', 'composition'),
			'searchable' => true,
		),
		'voltage' => array(
			'label' => 'Voltage',
			'aliases' => array('voltage', 'volt', 'v'),
			'searchable' => true,
		),
		'other' => array(
			'label' => 'Other information',
			'aliases' => array(
				'other', 'other_information', 'other information', 'other_info',
				'other info', 'remarks', 'notes', 'comment', 'comments',
				'additional', 'additional_info', 'additional info', 'info',
				'description2', 'extra', 'extras',
			),
			'searchable' => true,
		),
	);
}

/**
 * Searchable field options for storefront UI (includes “All fields”).
 *
 * @return list<array{key:string,label:string}>
 */
function epc_price_extra_search_options(): array
{
	$out = array(array('key' => 'all', 'label' => 'All fields'));
	foreach (epc_price_extra_field_catalog() as $key => $meta) {
		if (empty($meta['searchable'])) {
			continue;
		}
		$out[] = array('key' => $key, 'label' => (string) $meta['label']);
	}
	return $out;
}

function epc_price_extra_label(string $key): string
{
	$catalog = epc_price_extra_field_catalog();
	if (isset($catalog[$key]['label'])) {
		return (string) $catalog[$key]['label'];
	}
	$key = str_replace('_', ' ', $key);
	return ucwords($key);
}

function epc_price_extra_normalize_header(string $raw): string
{
	$raw = strtolower(trim($raw));
	$raw = preg_replace('/[\x{FEFF}\x{200B}]+/u', '', $raw);
	$raw = str_replace(array('_', '-', '/', '\\', '.', ':'), ' ', (string) $raw);
	$raw = preg_replace('/\s+/u', ' ', (string) $raw);
	return trim((string) $raw);
}

function epc_price_extra_slug_key(string $header): string
{
	$norm = epc_price_extra_normalize_header($header);
	$slug = preg_replace('/[^a-z0-9]+/', '_', $norm);
	$slug = trim((string) $slug, '_');
	if ($slug === '') {
		return 'custom';
	}
	if (function_exists('mb_substr')) {
		return mb_substr($slug, 0, 48, 'UTF-8');
	}
	return substr($slug, 0, 48);
}

/**
 * Normalize a value for index matching.
 */
function epc_price_extra_normalize_value(string $raw): string
{
	$raw = trim($raw);
	if ($raw === '') {
		return '';
	}
	if (function_exists('mb_strtoupper')) {
		$raw = mb_strtoupper($raw, 'UTF-8');
	} else {
		$raw = strtoupper($raw);
	}
	// Match article-style search: ignore spaces / punctuation noise.
	$raw = preg_replace('/[^A-Z0-9]/u', '', (string) $raw);
	if (function_exists('mb_substr')) {
		return mb_substr((string) $raw, 0, 191, 'UTF-8');
	}
	return substr((string) $raw, 0, 191);
}

/**
 * Map unused CSV header columns → field keys (known catalog + custom slugs).
 *
 * @param list<string> $headerRow
 * @param array<string,int> $coreMap roles from epc_multivendor_map_headers
 * @return array<int,string> column index => field_key
 */
function epc_price_extra_map_header_columns(array $headerRow, array $coreMap): array
{
	$used = array();
	foreach ($coreMap as $idx) {
		if ((int) $idx >= 0) {
			$used[(int) $idx] = true;
		}
	}

	$aliasToKey = array();
	foreach (epc_price_extra_field_catalog() as $key => $meta) {
		foreach ($meta['aliases'] as $alias) {
			$aliasToKey[epc_price_extra_normalize_header($alias)] = $key;
		}
	}

	$out = array();
	foreach ($headerRow as $idx => $rawHeader) {
		$idx = (int) $idx;
		if (isset($used[$idx])) {
			continue;
		}
		$norm = epc_price_extra_normalize_header((string) $rawHeader);
		if ($norm === '') {
			continue;
		}
		if (isset($aliasToKey[$norm])) {
			$out[$idx] = $aliasToKey[$norm];
			continue;
		}
		// Soft alias match (substring for longer headers).
		$matched = '';
		foreach ($aliasToKey as $alias => $key) {
			if ($alias === '' || strlen($alias) < 2) {
				continue;
			}
			if ($norm === $alias || preg_match('/(^|[^a-z0-9])' . preg_quote($alias, '/') . '([^a-z0-9]|$)/', $norm)) {
				$matched = $key;
				break;
			}
		}
		if ($matched !== '') {
			$out[$idx] = $matched;
			continue;
		}
		$out[$idx] = epc_price_extra_slug_key((string) $rawHeader);
	}
	return $out;
}

/**
 * Extract extras from one CSV data row.
 *
 * @param list<string>|array<int,mixed> $raw
 * @param array<int,string> $extraColMap
 * @return array<string,string>
 */
function epc_price_extra_extract_from_row(array $raw, array $extraColMap): array
{
	$extras = array();
	$otherBits = array();
	foreach ($extraColMap as $colIdx => $fieldKey) {
		$val = trim((string) ($raw[$colIdx] ?? ''));
		if ($val === '') {
			continue;
		}
		if (function_exists('mb_substr')) {
			$val = mb_substr($val, 0, 500, 'UTF-8');
		} else {
			$val = substr($val, 0, 500);
		}
		if ($fieldKey === 'other') {
			$otherBits[] = $val;
			continue;
		}
		if (!isset($extras[$fieldKey]) || $extras[$fieldKey] === '') {
			$extras[$fieldKey] = $val;
		} elseif (strcasecmp((string) $extras[$fieldKey], $val) !== 0) {
			// Keep first; append unique into other.
			$otherBits[] = epc_price_extra_label($fieldKey) . ': ' . $val;
		}
	}
	if ($otherBits !== array()) {
		$joined = implode(' | ', array_unique($otherBits));
		if (isset($extras['other']) && $extras['other'] !== '') {
			$extras['other'] = $extras['other'] . ' | ' . $joined;
		} else {
			$extras['other'] = $joined;
		}
	}
	return $extras;
}

/**
 * Merge extras when collapsing duplicate brand+article rows.
 *
 * @param array<string,string> $a
 * @param array<string,string> $b
 * @return array<string,string>
 */
function epc_price_extra_merge(array $a, array $b): array
{
	$out = $a;
	foreach ($b as $k => $v) {
		$v = trim((string) $v);
		if ($v === '') {
			continue;
		}
		if (!isset($out[$k]) || trim((string) $out[$k]) === '') {
			$out[$k] = $v;
			continue;
		}
		if (strcasecmp((string) $out[$k], $v) === 0) {
			continue;
		}
		if ($k === 'other') {
			$parts = preg_split('/\s*\|\s*/', (string) $out[$k] . ' | ' . $v) ?: array();
			$seen = array();
			$uniq = array();
			foreach ($parts as $p) {
				$p = trim((string) $p);
				if ($p === '') {
					continue;
				}
				$uk = strtoupper($p);
				if (isset($seen[$uk])) {
					continue;
				}
				$seen[$uk] = true;
				$uniq[] = $p;
			}
			$out[$k] = implode(' | ', $uniq);
		}
	}
	return $out;
}

function epc_price_extra_encode(array $extras): string
{
	if ($extras === array()) {
		return '';
	}
	ksort($extras);
	$json = json_encode($extras, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	return is_string($json) ? $json : '';
}

/**
 * @return array<string,string>
 */
function epc_price_extra_decode($raw): array
{
	if (is_array($raw)) {
		$out = array();
		foreach ($raw as $k => $v) {
			$k = trim((string) $k);
			$v = trim((string) $v);
			if ($k === '' || $v === '') {
				continue;
			}
			$out[$k] = $v;
		}
		return $out;
	}
	$raw = trim((string) $raw);
	if ($raw === '') {
		return array();
	}
	$data = json_decode($raw, true);
	if (!is_array($data)) {
		return array();
	}
	return epc_price_extra_decode($data);
}

/**
 * Ensure side tables exist (safe CREATE IF NOT EXISTS).
 */
function epc_price_extra_ensure_schema(PDO $db): bool
{
	static $ready = null;
	if ($ready !== null) {
		return $ready;
	}
	$ready = false;
	try {
		$db->exec(
			'CREATE TABLE IF NOT EXISTS `epc_price_data_extras` (
				`price_data_id` INT(11) NOT NULL,
				`price_id` INT(11) NOT NULL,
				`extra_json` MEDIUMTEXT NOT NULL,
				PRIMARY KEY (`price_data_id`),
				KEY `idx_epc_extras_price_id` (`price_id`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
		);
		$db->exec(
			'CREATE TABLE IF NOT EXISTS `epc_price_attr_index` (
				`id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
				`price_data_id` INT(11) NOT NULL,
				`price_id` INT(11) NOT NULL,
				`field_key` VARCHAR(64) NOT NULL,
				`value_norm` VARCHAR(191) NOT NULL,
				`value_raw` VARCHAR(255) NOT NULL DEFAULT \'\',
				`manufacturer` VARCHAR(128) NOT NULL DEFAULT \'\',
				`article` VARCHAR(64) NOT NULL DEFAULT \'\',
				`article_show` VARCHAR(64) NOT NULL DEFAULT \'\',
				`name` VARCHAR(255) NOT NULL DEFAULT \'\',
				PRIMARY KEY (`id`),
				KEY `idx_epc_attr_field_value` (`field_key`, `value_norm`),
				KEY `idx_epc_attr_value` (`value_norm`),
				KEY `idx_epc_attr_price_data` (`price_data_id`),
				KEY `idx_epc_attr_price_id` (`price_id`)
			) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
		);
		$ready = true;
	} catch (Throwable $e) {
		$ready = false;
	}
	return $ready;
}

function epc_price_extra_clear_for_price(PDO $db, int $priceId): void
{
	if ($priceId <= 0 || !epc_price_extra_ensure_schema($db)) {
		return;
	}
	try {
		$db->prepare('DELETE FROM `epc_price_attr_index` WHERE `price_id` = ?')->execute(array($priceId));
		$db->prepare('DELETE FROM `epc_price_data_extras` WHERE `price_id` = ?')->execute(array($priceId));
	} catch (Throwable $e) {
	}
}

/**
 * Persist extras + searchable index rows for one price_data row.
 *
 * @param array<string,string> $extras
 */
function epc_price_extra_save_for_row(
	PDO $db,
	int $priceDataId,
	int $priceId,
	array $extras,
	string $manufacturer,
	string $article,
	string $articleShow,
	string $name
): void {
	if ($priceDataId <= 0 || $priceId <= 0 || $extras === array()) {
		return;
	}
	if (!epc_price_extra_ensure_schema($db)) {
		return;
	}
	$json = epc_price_extra_encode($extras);
	if ($json === '') {
		return;
	}
	try {
		$db->prepare(
			'INSERT INTO `epc_price_data_extras` (`price_data_id`, `price_id`, `extra_json`)
			 VALUES (?, ?, ?)
			 ON DUPLICATE KEY UPDATE `price_id` = VALUES(`price_id`), `extra_json` = VALUES(`extra_json`)'
		)->execute(array($priceDataId, $priceId, $json));
	} catch (Throwable $e) {
		return;
	}

	try {
		$db->prepare('DELETE FROM `epc_price_attr_index` WHERE `price_data_id` = ?')->execute(array($priceDataId));
	} catch (Throwable $e) {
	}

	$ins = $db->prepare(
		'INSERT INTO `epc_price_attr_index`
		 (`price_data_id`,`price_id`,`field_key`,`value_norm`,`value_raw`,`manufacturer`,`article`,`article_show`,`name`)
		 VALUES (?,?,?,?,?,?,?,?,?)'
	);
	foreach ($extras as $fieldKey => $rawVal) {
		$rawVal = trim((string) $rawVal);
		if ($rawVal === '') {
			continue;
		}
		// Split multi-value other / cross_reference on common separators.
		$chunks = preg_split('/\s*[|;,]\s*/u', $rawVal) ?: array($rawVal);
		$seenNorm = array();
		foreach ($chunks as $chunk) {
			$chunk = trim((string) $chunk);
			if ($chunk === '') {
				continue;
			}
			$norm = epc_price_extra_normalize_value($chunk);
			if ($norm === '' || isset($seenNorm[$norm])) {
				continue;
			}
			$seenNorm[$norm] = true;
			$rawStore = $chunk;
			if (function_exists('mb_substr')) {
				$rawStore = mb_substr($rawStore, 0, 255, 'UTF-8');
			} else {
				$rawStore = substr($rawStore, 0, 255);
			}
			try {
				$ins->execute(array(
					$priceDataId,
					$priceId,
					(string) $fieldKey,
					$norm,
					$rawStore,
					$manufacturer,
					$article,
					$articleShow,
					$name,
				));
			} catch (Throwable $e) {
			}
		}
	}
}

/**
 * Storefront / API search against the attribute index.
 *
 * @return array{ok:bool,message:string,field:string,q:string,count:int,items:list<array<string,mixed>>}
 */
function epc_price_attr_search(PDO $db, string $fieldKey, string $query, int $limit = 80): array
{
	$fieldKey = trim($fieldKey);
	if ($fieldKey === '') {
		$fieldKey = 'all';
	}
	$query = trim($query);
	$limit = max(1, min(200, $limit));
	$empty = array(
		'ok' => false,
		'message' => '',
		'field' => $fieldKey,
		'q' => $query,
		'count' => 0,
		'items' => array(),
	);
	if (strlen(preg_replace('/\s+/u', '', $query) ?? '') < 2) {
		$empty['message'] = 'Enter at least 2 characters.';
		return $empty;
	}
	if (!epc_price_extra_ensure_schema($db)) {
		$empty['message'] = 'Search index is not available.';
		return $empty;
	}

	$catalog = epc_price_extra_field_catalog();
	if ($fieldKey !== 'all' && !isset($catalog[$fieldKey])) {
		// Allow custom slug keys that were indexed from free-form columns.
		if (!preg_match('/^[a-z0-9_]{1,48}$/', $fieldKey)) {
			$empty['message'] = 'Unknown search field.';
			return $empty;
		}
	}

	$norm = epc_price_extra_normalize_value($query);
	if ($norm === '') {
		$empty['message'] = 'Enter a valid search value.';
		return $empty;
	}
	$like = $norm . '%';

	$sql = 'SELECT `price_data_id`, `price_id`, `field_key`, `value_raw`, `value_norm`,
			`manufacturer`, `article`, `article_show`, `name`
		FROM `epc_price_attr_index`
		WHERE `value_norm` LIKE ?';
	$bind = array($like);
	if ($fieldKey !== 'all') {
		$sql .= ' AND `field_key` = ?';
		$bind[] = $fieldKey;
	}
	$sql .= ' ORDER BY
		CASE WHEN `value_norm` = ? THEN 0 WHEN `value_norm` LIKE ? THEN 1 ELSE 2 END,
		`manufacturer` ASC, `article_show` ASC
		LIMIT ' . (int) $limit;
	$bind[] = $norm;
	$bind[] = $like;

	try {
		$st = $db->prepare($sql);
		$st->execute($bind);
		$rows = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	} catch (Throwable $e) {
		$empty['message'] = 'Search failed.';
		return $empty;
	}

	// Deduplicate by brand+article (keep first / best match).
	$seen = array();
	$items = array();
	foreach ($rows as $row) {
		$mfr = trim((string) ($row['manufacturer'] ?? ''));
		$artShow = trim((string) ($row['article_show'] ?? ''));
		$art = trim((string) ($row['article'] ?? ''));
		if ($artShow === '') {
			$artShow = $art;
		}
		$uk = strtoupper($mfr) . '|' . strtoupper($art !== '' ? $art : $artShow);
		if (isset($seen[$uk])) {
			continue;
		}
		$seen[$uk] = true;
		$fk = (string) ($row['field_key'] ?? '');
		$items[] = array(
			'manufacturer' => $mfr,
			'article' => $art,
			'article_show' => $artShow,
			'name' => trim((string) ($row['name'] ?? '')),
			'matched_field' => $fk,
			'matched_field_label' => epc_price_extra_label($fk),
			'matched_value' => trim((string) ($row['value_raw'] ?? '')),
			'price_id' => (int) ($row['price_id'] ?? 0),
			'price_data_id' => (int) ($row['price_data_id'] ?? 0),
		);
	}

	return array(
		'ok' => true,
		'message' => count($items) > 0 ? 'OK' : 'No matching warehouse products.',
		'field' => $fieldKey,
		'q' => $query,
		'count' => count($items),
		'items' => $items,
	);
}

/**
 * Lookup extras for a warehouse product row (request-cached).
 *
 * @return array<string,string>
 */
function epc_price_extra_lookup(PDO $db, int $priceId, string $manufacturer, string $articleShow): array
{
	static $cache = array();
	if ($priceId <= 0) {
		return array();
	}
	$artKey = strtoupper(preg_replace('/[^A-Z0-9]/i', '', $articleShow) ?? '');
	$ck = $priceId . '|' . strtoupper(trim($manufacturer)) . '|' . $artKey;
	if (isset($cache[$ck])) {
		return $cache[$ck];
	}
	$cache[$ck] = array();
	if (!epc_price_extra_ensure_schema($db)) {
		return array();
	}
	try {
		$st = $db->prepare(
			'SELECT e.`extra_json`
			 FROM `epc_price_data_extras` e
			 INNER JOIN `shop_docpart_prices_data` d ON d.`id` = e.`price_data_id`
			 WHERE e.`price_id` = ?
			   AND UPPER(TRIM(d.`manufacturer`)) = UPPER(TRIM(?))
			   AND (
					UPPER(REPLACE(REPLACE(REPLACE(d.`article_show`, \'-\', \'\'), \' \', \'\'), \'.\', \'\')) = ?
				 OR UPPER(REPLACE(REPLACE(REPLACE(d.`article`, \'-\', \'\'), \' \', \'\'), \'.\', \'\')) = ?
			   )
			 LIMIT 1'
		);
		$st->execute(array($priceId, $manufacturer, $artKey, $artKey));
		$json = (string) $st->fetchColumn();
		$cache[$ck] = epc_price_extra_decode($json);
	} catch (Throwable $e) {
		$cache[$ck] = array();
	}
	return $cache[$ck];
}

/**
 * Build DocpartProduct json_params string including warehouse attrs.
 */
function epc_price_extra_json_params(PDO $db, int $priceId, string $manufacturer, string $articleShow, $existing = null): string
{
	$base = array();
	if (is_string($existing) && $existing !== '') {
		$decoded = json_decode($existing, true);
		if (is_array($decoded)) {
			$base = $decoded;
		}
	} elseif (is_array($existing)) {
		$base = $existing;
	}
	$extras = epc_price_extra_lookup($db, $priceId, $manufacturer, $articleShow);
	if ($extras === array()) {
		if ($base === array()) {
			return '';
		}
		$json = json_encode($base, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
		return is_string($json) ? $json : '';
	}
	$base['warehouse_attrs'] = $extras;
	$base['used'] = 1;
	$json = json_encode($base, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	return is_string($json) ? $json : '';
}
