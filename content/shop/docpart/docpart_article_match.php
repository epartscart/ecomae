<?php
/**
 * Article normalization for price tables — must match part_search_page.php (sweep + UPPER).
 * DB rows often use dashes/spaces (e.g. FILTER-OIL-01) while URLs use stripped form (FILTEROIL01).
 */

function docpart_normalize_article_for_price($article_input)
{
	if ($article_input === null || $article_input === '') {
		return '';
	}
	$sweep = array(" ", "-", "_", "`", "/", "'", '"', "\\", ".", ",", "#", "\r\n", "\r", "\n", "\t");
	return strtoupper(str_replace($sweep, "", $article_input));
}

/**
 * SQL expression applied to shop_docpart_prices_data.article for comparison with docpart_normalize_article_for_price().
 */
function docpart_sql_article_normalized_expr($column = '`article`')
{
	$expr = $column;
	$chars = array(' ', '-', '_', '`', '/', "'", '"', '.', ',', '#');
	foreach ($chars as $ch) {
		if ($ch === "'") {
			$lit = "''";
		} elseif ($ch === '\\') {
			$lit = '\\\\';
		} else {
			$lit = $ch;
		}
		$expr = "REPLACE($expr, '$lit', '')";
	}
	$expr = "REPLACE($expr, CHAR(92), '')";
	$expr = "REPLACE(REPLACE(REPLACE(REPLACE($expr, CHAR(13,10), ''), CHAR(13), ''), CHAR(10), ''), CHAR(9), '')";
	return "UPPER($expr)";
}

/**
 * Ensure indexed article_search column exists for fast warehouse price lookups.
 * Web/search path: probe only (never ALTER — locks → 524 on epartscart).
 * Setup scripts may pass $allowAlter=true.
 */
function docpart_price_data_ensure_article_search_column(PDO $db_link, bool $allowAlter = false): bool
{
	static $ready = null;
	if ($ready !== null) {
		return $ready;
	}
	$ready = false;
	try {
		$db_link->query('SELECT `article_search` FROM `shop_docpart_prices_data` LIMIT 1');
		$ready = true;
	} catch (Throwable $e) {
		if (!$allowAlter) {
			return false;
		}
		try {
			$db_link->exec(
				'ALTER TABLE `shop_docpart_prices_data`
				 ADD COLUMN `article_search` VARCHAR(64) NOT NULL DEFAULT \'\' AFTER `article`,
				 ADD INDEX `x_article_search_price` (`article_search`, `price_id`),
				 ADD INDEX `x_price_article_search` (`price_id`, `article_search`);'
			);
			$ready = true;
		} catch (Throwable $e2) {
			try {
				$db_link->query('SELECT `article_search` FROM `shop_docpart_prices_data` LIMIT 1');
				$ready = true;
			} catch (Throwable $e3) {
				$ready = false;
			}
		}
	}
	return $ready;
}

/**
 * Backfill article_search for one price list (or a limited batch site-wide).
 */
function docpart_price_data_backfill_article_search(PDO $db_link, int $priceId = 0, int $limit = 25000): int
{
	if (!docpart_price_data_ensure_article_search_column($db_link)) {
		return 0;
	}
	$expr = docpart_sql_article_normalized_expr('`article`');
	// Keep batches modest — very large IN (...) lists can fail silently under FPM.
	$limit = max(100, min(2000, $limit));
	try {
		// Prefer primary-key batch updates — PDO rowCount() is unreliable for UPDATE…LIMIT on MariaDB.
		$idSql = 'SELECT `id` FROM `shop_docpart_prices_data`
			WHERE (`article_search` = \'\' OR `article_search` IS NULL)'
			. ($priceId > 0 ? ' AND `price_id` = ' . (int) $priceId : '')
			. ' ORDER BY `id` ASC LIMIT ' . (int) $limit;
		$ids = array();
		foreach ($db_link->query($idSql) as $row) {
			$ids[] = (int) $row['id'];
		}
		if (count($ids) === 0) {
			return 0;
		}
		$ph = implode(',', array_fill(0, count($ids), '?'));
		$st = $db_link->prepare(
			'UPDATE `shop_docpart_prices_data`
			SET `article_search` = ' . $expr . '
			WHERE `id` IN (' . $ph . ')'
		);
		$st->execute($ids);
		return count($ids);
	} catch (Throwable $e) {
		return 0;
	}
}

/**
 * Prefer indexed article_search when available; otherwise nested REPLACE expr.
 */
function docpart_sql_article_match_expr(PDO $db_link = null, $column = '`article`')
{
	if ($db_link instanceof PDO && docpart_price_data_ensure_article_search_column($db_link)) {
		return '`article_search`';
	}
	return docpart_sql_article_normalized_expr($column);
}

/**
 * WHERE fragment matching article values via article_search and/or normalized article.
 * Completes incomplete article_search backfills without forcing live UPDATEs.
 *
 * @param array<int, string> $article_values
 * @param array<int, mixed>  $binding_values Appended in-place
 * @return string SQL boolean expression (no leading AND)
 */
function docpart_sql_article_values_match_clause(PDO $db_link, array $article_values, array &$binding_values, $column = '`article`')
{
	$article_values = array_values(array_filter(array_map('strval', $article_values), static function ($v) {
		return $v !== '';
	}));
	if (count($article_values) === 0) {
		return '0';
	}
	$ph = implode(',', array_fill(0, count($article_values), '?'));
	$norm_expr = docpart_sql_article_normalized_expr($column);
	if (docpart_price_data_ensure_article_search_column($db_link)) {
		foreach ($article_values as $value) {
			$binding_values[] = $value;
		}
		foreach ($article_values as $value) {
			$binding_values[] = $value;
		}
		return '(`article_search` IN (' . $ph . ') OR ' . $norm_expr . ' IN (' . $ph . '))';
	}
	foreach ($article_values as $value) {
		$binding_values[] = $value;
	}
	return '(' . $norm_expr . ' IN (' . $ph . '))';
}

/**
 * Article values to match in shop_docpart_prices_data (requested + cross numbers when enabled).
 */
function docpart_collect_article_candidates($db_link, $article_norm, $use_crosses)
{
	$article_candidates = array();
	if ($article_norm === '') {
		return $article_candidates;
	}
	$article_candidates[$article_norm] = true;
	if (!$use_crosses) {
		return array_keys($article_candidates);
	}
	$art_expr = docpart_sql_article_normalized_expr('`article`');
	$analog_expr = docpart_sql_article_normalized_expr('`analog`');
	// Analogs table has no article_search column — keep REPLACE expr here.
	$analogs_query = $db_link->prepare(
		'SELECT `article`, `analog` FROM `shop_docpart_articles_analogs_list` WHERE ' . $art_expr . ' = ? OR ' . $analog_expr . ' = ? LIMIT 5000;'
	);
	$analogs_query->execute(array($article_norm, $article_norm));
	while ($analog_record = $analogs_query->fetch(PDO::FETCH_ASSOC)) {
		$article_candidate = docpart_normalize_article_for_price($analog_record['article']);
		$analog_candidate = docpart_normalize_article_for_price($analog_record['analog']);
		if ($article_candidate !== '') {
			$article_candidates[$article_candidate] = true;
		}
		if ($analog_candidate !== '') {
			$article_candidates[$analog_candidate] = true;
		}
	}
	return array_keys($article_candidates);
}

/**
 * Same article resolution as ajax_getManufacturersListFromPrices (direct hit vs cross-expanded).
 */
function docpart_resolve_article_search_values($db_link, $DP_Config, $article_input, $price_ids = array())
{
	$article_norm = docpart_normalize_article_for_price($article_input);
	if ($article_norm === '') {
		return array();
	}
	$use_crosses = (isset($DP_Config->local_crosses) && !empty($DP_Config->local_crosses));
	$price_ids = array_values(array_unique(array_map('intval', $price_ids)));
	// Never backfill on the live search path — UPDATE chunks block click-to-result.
	// Use epc-epartscart-warehouse-search-fast.php&apply=1 for offline backfill.
	if ($use_crosses && !empty($price_ids)) {
		$price_placeholders = str_repeat('?,', count($price_ids) - 1) . '?';
		$direct_bindings = array();
		$article_clause = docpart_sql_article_values_match_clause($db_link, array($article_norm), $direct_bindings);
		$direct_check_query = $db_link->prepare(
			'SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE ' . $article_clause
			. ' AND `price_id` IN (' . $price_placeholders . ') LIMIT 1;'
		);
		$direct_check_query->execute(array_merge($direct_bindings, $price_ids));
		if ((int)$direct_check_query->fetchColumn() > 0) {
			return array($article_norm);
		}
	}
	return docpart_collect_article_candidates($db_link, $article_norm, $use_crosses);
}

function docpart_price_ids_from_office_storage_bunches($db_link, $office_storage_bunches)
{
	$price_ids = array();
	if (!is_array($office_storage_bunches)) {
		return $price_ids;
	}
	for ($p = 0; $p < count($office_storage_bunches); $p++) {
		$storage_id = (int)$office_storage_bunches[$p]['storage_id'];
		if ($storage_id < 1) {
			continue;
		}
		$storage_query = $db_link->prepare('SELECT `connection_options` FROM `shop_storages` WHERE `id` = ?;');
		$storage_query->execute(array($storage_id));
		$storage_record = $storage_query->fetch(PDO::FETCH_ASSOC);
		if (!$storage_record) {
			continue;
		}
		$connection_options = json_decode($storage_record['connection_options'], true);
		if (!empty($connection_options['price_id'])) {
			$price_ids[] = (int)$connection_options['price_id'];
		}
	}
	return array_values(array_unique($price_ids));
}

/**
 * CHPU part URL: /{lang}/parts/{brand}/{article} or /{lang}/parts/brands/{article}.
 * Brand slug keeps spaces (rawurlencode → JS%20ASAKASHI); slashes use slash_code.
 */
function epc_chpu_build_part_url($DP_Config, $lang_href, $brand, $article)
{
	$article_norm = docpart_normalize_article_for_price($article);
	if ($article_norm === '') {
		return '';
	}
	$parts_seg = 'parts';
	$brands_seg = 'brands';
	$slash_code = '---';
	if (is_object($DP_Config) && !empty($DP_Config->chpu_search_config)) {
		if (!empty($DP_Config->chpu_search_config['level_1']['url'])) {
			$parts_seg = (string) $DP_Config->chpu_search_config['level_1']['url'];
		}
		if (!empty($DP_Config->chpu_search_config['level_2']['mode_1']['url'])) {
			$brands_seg = (string) $DP_Config->chpu_search_config['level_2']['mode_1']['url'];
		}
		if (isset($DP_Config->chpu_search_config['slash_code'])) {
			$slash_code = (string) $DP_Config->chpu_search_config['slash_code'];
		}
	}
	$lang_href = rtrim((string) $lang_href, '/');
	$brand = trim((string) $brand);
	if ($brand === '') {
		return $lang_href . '/' . $parts_seg . '/' . $brands_seg . '/' . rawurlencode($article_norm);
	}
	$brand = mb_strtoupper($brand, 'UTF-8');
	$brand_alias = str_replace('/', $slash_code, $brand);
	return $lang_href . '/' . $parts_seg . '/' . rawurlencode($brand_alias) . '/' . rawurlencode($article_norm);
}

/**
 * Distinct warehouse brands for an article (shop_docpart_prices_data), case-insensitive.
 *
 * @return array<int, string>
 */
function epc_chpu_distinct_warehouse_brands_for_article($db_link, $DP_Config, $article_input, array $price_ids = array())
{
	$article_norm = docpart_normalize_article_for_price($article_input);
	if ($article_norm === '' || !($db_link instanceof PDO)) {
		return array();
	}
	if (empty($price_ids)) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_stock_brands_helpers.php';
		$price_ids = epc_stock_brand_price_ids_with_stock($db_link);
		$office_price_ids = epc_stock_brand_price_ids($db_link);
		if (count($office_price_ids) > 0) {
			$price_ids = array_values(array_unique(array_merge($price_ids, $office_price_ids)));
		}
		if (count($price_ids) === 0) {
			try {
				$all_prices = $db_link->query('SELECT DISTINCT `id` FROM `shop_docpart_prices` ORDER BY `id` ASC');
				while ($price_row = $all_prices->fetch(PDO::FETCH_ASSOC)) {
					$price_ids[] = (int) $price_row['id'];
				}
			} catch (Exception $e) {
				$price_ids = array(1);
			}
		}
	}
	$price_ids = array_values(array_unique(array_map('intval', $price_ids)));
	if (count($price_ids) === 0) {
		return array();
	}
	$article_values = docpart_resolve_article_search_values($db_link, $DP_Config, $article_input, $price_ids);
	if (count($article_values) === 0) {
		$article_values = array($article_norm);
	}
	$art_expr = docpart_sql_article_match_expr($db_link, '`article`');
	$price_ph = implode(',', array_fill(0, count($price_ids), '?'));
	$art_ph = implode(',', array_fill(0, count($article_values), '?'));
	$sql = 'SELECT MIN(TRIM(`manufacturer`)) AS `brand_name`'
		. ' FROM `shop_docpart_prices_data`'
		. ' WHERE ' . $art_expr . ' IN (' . $art_ph . ')'
		. ' AND `price_id` IN (' . $price_ph . ')'
		. ' AND TRIM(IFNULL(`manufacturer`, \'\')) != \'\''
		. ' GROUP BY UPPER(TRIM(`manufacturer`))'
		. ' ORDER BY UPPER(TRIM(`manufacturer`)) ASC';
	$params = array_merge($article_values, $price_ids);
	$stmt = $db_link->prepare($sql);
	$stmt->execute($params);
	$brands = array();
	$pricing_loaded = false;
	while ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
		$brand_name = trim((string) ($row['brand_name'] ?? ''));
		if ($brand_name === '') {
			continue;
		}
		if (!$pricing_loaded && is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php')) {
			require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php';
			$pricing_loaded = true;
		}
		if ($pricing_loaded && function_exists('epc_pricing_get_brand_rule')) {
			$rule = epc_pricing_get_brand_rule($db_link, 0, $brand_name);
			if (isset($rule['visible']) && (int) $rule['visible'] === 0) {
				continue;
			}
		}
		$brands[] = $brand_name;
	}
	// article_search index can be partially empty (e.g. S-UAE); fall back to normalized article expr.
	if (count($brands) === 0 && $art_expr === '`article_search`') {
		$norm_expr = docpart_sql_article_normalized_expr('`article`');
		$sql_fallback = 'SELECT MIN(TRIM(`manufacturer`)) AS `brand_name`'
			. ' FROM `shop_docpart_prices_data`'
			. ' WHERE ' . $norm_expr . ' IN (' . $art_ph . ')'
			. ' AND `price_id` IN (' . $price_ph . ')'
			. ' AND TRIM(IFNULL(`manufacturer`, \'\')) != \'\''
			. ' GROUP BY UPPER(TRIM(`manufacturer`))'
			. ' ORDER BY UPPER(TRIM(`manufacturer`)) ASC';
		$stmt_fb = $db_link->prepare($sql_fallback);
		$stmt_fb->execute($params);
		while ($row = $stmt_fb->fetch(PDO::FETCH_ASSOC)) {
			$brand_name = trim((string) ($row['brand_name'] ?? ''));
			if ($brand_name === '') {
				continue;
			}
			if (!$pricing_loaded && is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php')) {
				require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php';
				$pricing_loaded = true;
			}
			if ($pricing_loaded && function_exists('epc_pricing_get_brand_rule')) {
				$rule = epc_pricing_get_brand_rule($db_link, 0, $brand_name);
				if (isset($rule['visible']) && (int) $rule['visible'] === 0) {
					continue;
				}
			}
			$brands[] = $brand_name;
		}
	}
	return $brands;
}

/**
 * When warehouse has no brands, ask UMAPI BrandRefinement (single-brand skip only).
 *
 * @return string|null
 */
function epc_chpu_single_brand_from_umapi($DP_Config, $article_input)
{
	if (!is_file($_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_epc_article_brands.php')) {
		return null;
	}
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_epc_article_brands.php';
	$brands = array();
	$seen = array();
	$added = epc_article_brands_from_umapi($DP_Config, $article_input, $brands, $seen);
	if ($added !== 1 || count($brands) !== 1) {
		return null;
	}
	$row = reset($brands);
	$brand = trim((string) ($row['manufacturer'] ?? ''));
	return $brand !== '' ? $brand : null;
}

/**
 * Resolve a single brand for article-only search, or null when picker is needed.
 *
 * @return string|null
 */
function epc_chpu_resolve_single_brand_for_article($db_link, $DP_Config, $article_input)
{
	$warehouse_brands = epc_chpu_distinct_warehouse_brands_for_article($db_link, $DP_Config, $article_input);
	if (count($warehouse_brands) === 1) {
		return $warehouse_brands[0];
	}
	if (count($warehouse_brands) > 1) {
		return null;
	}
	return epc_chpu_single_brand_from_umapi($DP_Config, $article_input);
}

/**
 * 302 target when article maps to exactly one brand; empty string → show brand picker.
 */
function epc_chpu_single_brand_redirect_url($db_link, $DP_Config, $article_input, $lang_href)
{
	if (empty($DP_Config->chpu_search_config['chpu_search_on'])) {
		return '';
	}
	$single_brand = epc_chpu_resolve_single_brand_for_article($db_link, $DP_Config, $article_input);
	if ($single_brand === null || $single_brand === '') {
		return '';
	}
	return epc_chpu_build_part_url($DP_Config, $lang_href, $single_brand, $article_input);
}
