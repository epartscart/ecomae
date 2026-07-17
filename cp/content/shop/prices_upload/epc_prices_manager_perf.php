<?php
/**
 * Performance helpers for CP prices manager (platform Super CP + large tenants).
 *
 * epartscart 524 root causes (addressed here):
 * 1) Per-row sync get_update_history.php on every list during HTML render
 * 2) COUNT(*) GROUP BY over shop_docpart_prices_data (500k+ rows) on every load
 * 3) Occasional pyprices_tables_cleaner DELETE locks during page view
 * 4) ALTER TABLE ADD INDEX on huge prices_data during page view
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_prices_is_platform_operator_request')) {
	function epc_prices_is_platform_operator_request(): bool
	{
		if (function_exists('epc_portal_is_platform_hostname') && epc_portal_is_platform_hostname()) {
			return true;
		}
		$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
		if (strpos($host, ':') !== false) {
			$host = explode(':', $host, 2)[0];
		}
		return in_array($host, array('www.ecomae.com', 'ecomae.com', 'cp.ecomae.com'), true);
	}
}

if (!function_exists('epc_prices_is_large_tenant_host')) {
	/** Large warehouse tenants where CP prices must stay under ~1s TTFB. */
	function epc_prices_is_large_tenant_host(): bool
	{
		$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
		if (strpos($host, ':') !== false) {
			$host = explode(':', $host, 2)[0];
		}
		if ($host === '') {
			return false;
		}
		return (strpos($host, 'epartscart') !== false)
			|| epc_prices_is_platform_operator_request();
	}
}

if (!function_exists('epc_prices_should_run_tables_cleaner')) {
	/**
	 * Never run the pyprices tables cleaner during normal CP page views.
	 * DELETE locks on large tenants caused Cloudflare 524.
	 * Cron / ?epc_clean_pyprices=1 still cleans.
	 */
	function epc_prices_should_run_tables_cleaner(): bool
	{
		return isset($_GET['epc_clean_pyprices']);
	}
}

if (!function_exists('epc_prices_add_index_if_missing')) {
	/**
	 * Idempotent index create. Prefer setup/fix scripts — page load should
	 * not ALTER huge tables (locks → 524).
	 */
	function epc_prices_add_index_if_missing(PDO $db, string $table, string $indexName, string $columnsSpec): void
	{
		if (!preg_match('/^[a-zA-Z0-9_]+$/', $table) || !preg_match('/^[a-zA-Z0-9_]+$/', $indexName)) {
			return;
		}
		try {
			$q = $db->prepare('SHOW INDEX FROM `' . $table . '` WHERE `Key_name` = ?');
			$q->execute(array($indexName));
			if (!$q->fetch()) {
				$db->exec('ALTER TABLE `' . $table . '` ADD INDEX `' . $indexName . '` ' . $columnsSpec);
			}
		} catch (Exception $e) {
		}
	}
}

if (!function_exists('epc_prices_ensure_listing_indexes')) {
	/**
	 * Safe for setup/fix scripts. On CP page load we only probe once per day
	 * via a cache file and never ALTER (listing no longer needs prices_data COUNT).
	 *
	 * Pass $allowAlter=true from CLI/setup to actually create missing indexes.
	 */
	function epc_prices_ensure_listing_indexes(PDO $db, bool $allowAlter = false): void
	{
		static $done = false;
		if ($done) {
			return;
		}
		$done = true;

		// Page view: never SHOW INDEX / ALTER on huge prices_data.
		// Metadata locks + FPM pile-up caused host load ~38 and CP-wide 524s.
		if (!$allowAlter) {
			return;
		}

		$dbName = '';
		try {
			$dbName = (string) $db->query('SELECT DATABASE()')->fetchColumn();
		} catch (Exception $e) {
			$dbName = 'db';
		}
		$cacheFile = rtrim(sys_get_temp_dir(), '/') . '/epc_prices_listing_idx_' . md5($dbName) . '.ok';
		if (is_file($cacheFile) && (time() - (int) filemtime($cacheFile)) < 86400) {
			return;
		}

		$needData = true;
		$needCron = true;
		try {
			$q = $db->prepare('SHOW INDEX FROM `shop_docpart_prices_data` WHERE `Key_name` = ?');
			$q->execute(array('x_price_id'));
			$needData = !$q->fetch();
			$q2 = $db->prepare('SHOW INDEX FROM `shop_docpart_pyprices_crontab_prices` WHERE `Key_name` = ?');
			$q2->execute(array('x_price_id'));
			$needCron = !$q2->fetch();
		} catch (Exception $e) {
			return;
		}

		if ($needData) {
			epc_prices_add_index_if_missing($db, 'shop_docpart_prices_data', 'x_price_id', '(`price_id`)');
		}
		if ($needCron) {
			epc_prices_add_index_if_missing($db, 'shop_docpart_pyprices_crontab_prices', 'x_price_id', '(`price_id`)');
		}
		@file_put_contents($cacheFile, 'ok');
	}
}

if (!function_exists('epc_prices_table_has_column')) {
	function epc_prices_table_has_column(PDO $db, string $table, string $column): bool
	{
		static $cache = array();
		$key = $table . '.' . $column;
		if (array_key_exists($key, $cache)) {
			return $cache[$key];
		}
		if (!preg_match('/^[a-zA-Z0-9_]+$/', $table) || !preg_match('/^[a-zA-Z0-9_]+$/', $column)) {
			$cache[$key] = false;
			return false;
		}
		try {
			$q = $db->prepare(
				'SELECT 1 FROM information_schema.COLUMNS
				 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ? LIMIT 1'
			);
			$q->execute(array($table, $column));
			$cache[$key] = (bool) $q->fetchColumn();
		} catch (Exception $e) {
			$cache[$key] = false;
		}
		return $cache[$key];
	}
}

if (!function_exists('epc_prices_live_counts_map')) {
	/**
	 * Row counts per price list via the x_price_id index (index-only GROUP BY —
	 * fast even on 600k+ rows; there are only a handful of lists).
	 *
	 * @return array<int, int> price_id => rows, or null on failure.
	 */
	function epc_prices_live_counts_map(PDO $db_link): ?array
	{
		try {
			$q = $db_link->query(
				'SELECT `price_id`, COUNT(*) AS `c` FROM `shop_docpart_prices_data` GROUP BY `price_id`'
			);
			$map = array();
			while ($row = $q->fetch(PDO::FETCH_ASSOC)) {
				$map[(int) $row['price_id']] = (int) $row['c'];
			}
			return $map;
		} catch (Throwable $e) {
			return null;
		}
	}
}

if (!function_exists('epc_prices_persist_records_counts')) {
	/** Opportunistically store live counts into records_count (few rows, cheap). */
	function epc_prices_persist_records_counts(PDO $db_link, array $counts): void
	{
		if (!epc_prices_table_has_column($db_link, 'shop_docpart_prices', 'records_count')) {
			return;
		}
		try {
			$st = $db_link->prepare('UPDATE `shop_docpart_prices` SET `records_count` = ? WHERE `id` = ?');
			foreach ($counts as $priceId => $cnt) {
				$st->execute(array((int) $cnt, (int) $priceId));
			}
		} catch (Throwable $e) {
			// Display still works from the live map.
		}
	}
}

if (!function_exists('epc_prices_fetch_lists_query')) {
	/**
	 * Fast listing with resilient fallbacks so the CP table never goes blank.
	 * QTY: prefer denormalized records_count; when the column is missing or all
	 * zeros (e.g. after external imports), fall back to an index-only live count
	 * so warehouses never show empty QTY.
	 */
	function epc_prices_fetch_lists_query(PDO $db_link): PDOStatement
	{
		$hasRecordsCount = epc_prices_table_has_column($db_link, 'shop_docpart_prices', 'records_count');
		$recordsExpr = $hasRecordsCount
			? 'COALESCE(p.`records_count`, 0) AS `records_count`'
			: '0 AS `records_count`';

		$candidates = array();
		$candidates[] = 'SELECT p.*,
			' . $recordsExpr . ',
			COALESCE(pc.`cron_tasks_count`, 0) AS `cron_tasks_count`
			FROM `shop_docpart_prices` p
			LEFT JOIN (
				SELECT `price_id`, COUNT(*) AS `cron_tasks_count`
				FROM `shop_docpart_pyprices_crontab_prices`
				GROUP BY `price_id`
			) pc ON pc.`price_id` = p.`id`
			ORDER BY p.`id`';
		$candidates[] = 'SELECT p.*,
			' . $recordsExpr . ',
			0 AS `cron_tasks_count`
			FROM `shop_docpart_prices` p
			ORDER BY p.`id`';

		$last = null;
		foreach ($candidates as $sql) {
			try {
				$q = $db_link->prepare($sql);
				$q->execute();
				return $q;
			} catch (Throwable $e) {
				$last = $e;
			}
		}

		// Last resort — never throw out of the page renderer.
		try {
			$q = $db_link->prepare('SELECT p.*, 0 AS `records_count`, 0 AS `cron_tasks_count` FROM `shop_docpart_prices` p ORDER BY p.`id`');
			$q->execute();
			return $q;
		} catch (Throwable $e) {
			throw $last ?: $e;
		}
	}
}

if (!function_exists('epc_prices_fetch_lists_rows')) {
	/**
	 * Listing rows with guaranteed QTY (records_count) per price list.
	 *
	 * @return array<int, array<string, mixed>>
	 */
	function epc_prices_fetch_lists_rows(PDO $db_link): array
	{
		$q = epc_prices_fetch_lists_query($db_link);
		$rows = $q->fetchAll(PDO::FETCH_ASSOC) ?: array();
		if ($rows === array()) {
			return $rows;
		}

		$allZero = true;
		foreach ($rows as $row) {
			if ((int) ($row['records_count'] ?? 0) > 0) {
				$allZero = false;
				break;
			}
		}
		if (!$allZero) {
			return $rows;
		}

		// Denormalized counter empty (column missing or never backfilled after
		// external imports / Emex cleanup) — use the index-only live count.
		$live = epc_prices_live_counts_map($db_link);
		if (!is_array($live) || $live === array()) {
			return $rows;
		}
		foreach ($rows as $i => $row) {
			$pid = (int) ($row['id'] ?? 0);
			if ($pid > 0 && isset($live[$pid])) {
				$rows[$i]['records_count'] = (int) $live[$pid];
			}
		}
		epc_prices_persist_records_counts($db_link, $live);
		return $rows;
	}
}

if (!function_exists('epc_prices_defer_inline_update_history')) {
	/**
	 * Always defer per-row pyprices history indicators to AJAX after paint.
	 * Sync embed on epartscart ran N heavy task queries during HTML render → 524.
	 */
	function epc_prices_defer_inline_update_history(): bool
	{
		return true;
	}
}

if (!function_exists('epc_prices_external_poll_interval_ms')) {
	function epc_prices_external_poll_interval_ms(): int
	{
		return epc_prices_is_large_tenant_host() ? 20000 : 8000;
	}
}

if (!function_exists('epc_pyprices_health_check')) {
	/**
	 * @return array{ok:bool, message?:string, raw?:string}
	 */
	function epc_pyprices_health_check(object $DP_Config, int $timeoutSec = 5): array
	{
		$url = rtrim((string) $DP_Config->domain_path, '/') . '/pyprices/pyprices-api.php';
		$postdata = http_build_query(array(
			'key' => $DP_Config->tech_key,
			'just_test_db' => 'yes',
		));
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_POST => true,
			CURLOPT_POSTFIELDS => $postdata,
			CURLOPT_SSL_VERIFYHOST => 0,
			CURLOPT_SSL_VERIFYPEER => 0,
			CURLOPT_FOLLOWLOCATION => true,
			CURLOPT_CONNECTTIMEOUT => min(3, $timeoutSec),
			CURLOPT_TIMEOUT => $timeoutSec,
		));
		$body = curl_exec($ch);
		$err = curl_error($ch);
		curl_close($ch);
		if ($body === false || $body === '') {
			return array('ok' => false, 'message' => $err !== '' ? $err : 'Empty response', 'raw' => '');
		}
		$json = json_decode(trim((string) $body), true);
		if (is_array($json) && !empty($json['status'])) {
			return array('ok' => true, 'message' => (string) ($json['message'] ?? 'OK'));
		}
		return array(
			'ok' => false,
			'message' => is_array($json) ? (string) ($json['message'] ?? 'pyprices check failed') : substr((string) $body, 0, 200),
			'raw' => substr((string) $body, 0, 500),
		);
	}
}
