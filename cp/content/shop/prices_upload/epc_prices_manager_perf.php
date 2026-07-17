<?php
/**
 * Performance helpers for CP prices manager (platform Super CP + large tenants).
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

if (!function_exists('epc_prices_should_run_tables_cleaner')) {
	/**
	 * Avoid DELETE locks on every prices manager page view (platform + tenants).
	 * Large tenants (epartscart) were running the cleaner on every load → slow CP.
	 * Cron / ?epc_clean_pyprices=1 still cleans; otherwise ~1/40 chance.
	 */
	function epc_prices_should_run_tables_cleaner(): bool
	{
		return isset($_GET['epc_clean_pyprices']) || (mt_rand(1, 40) === 1);
	}
}

if (!function_exists('epc_prices_add_index_if_missing')) {
	/**
	 * Idempotent, additive-only index creation (SHOW INDEX is a cheap metadata
	 * query, safe to call on every page load). $table/$indexName are internal
	 * literals only — never pass user input.
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
	 * The prices manager listing page (epc_prices_fetch_lists_query) aggregates
	 * COUNT(*) ... GROUP BY price_id over shop_docpart_prices_data and
	 * shop_docpart_pyprices_crontab_prices on every load. Without an index on
	 * price_id, MySQL does a full table scan + filesort for that GROUP BY on
	 * every single page view, which is what made this page slow for tenants
	 * with large price-list data tables. Adding the index makes it an
	 * index-only scan instead; no query/behavior changes.
	 */
	function epc_prices_ensure_listing_indexes(PDO $db): void
	{
		epc_prices_add_index_if_missing($db, 'shop_docpart_prices_data', 'x_price_id', '(`price_id`)');
		epc_prices_add_index_if_missing($db, 'shop_docpart_pyprices_crontab_prices', 'x_price_id', '(`price_id`)');
	}
}

if (!function_exists('epc_prices_fetch_lists_query')) {
	function epc_prices_fetch_lists_query(PDO $db_link): PDOStatement
	{
		$sql = 'SELECT p.*,
			COALESCE(pd.`records_count`, 0) AS `records_count`,
			COALESCE(pc.`cron_tasks_count`, 0) AS `cron_tasks_count`
			FROM `shop_docpart_prices` p
			LEFT JOIN (
				SELECT `price_id`, COUNT(*) AS `records_count`
				FROM `shop_docpart_prices_data`
				GROUP BY `price_id`
			) pd ON pd.`price_id` = p.`id`
			LEFT JOIN (
				SELECT `price_id`, COUNT(*) AS `cron_tasks_count`
				FROM `shop_docpart_pyprices_crontab_prices`
				GROUP BY `price_id`
			) pc ON pc.`price_id` = p.`id`
			ORDER BY p.`id`';
		$q = $db_link->prepare($sql);
		$q->execute();
		return $q;
	}
}

if (!function_exists('epc_prices_defer_inline_update_history')) {
	function epc_prices_defer_inline_update_history(): bool
	{
		return epc_prices_is_platform_operator_request();
	}
}

if (!function_exists('epc_prices_external_poll_interval_ms')) {
	function epc_prices_external_poll_interval_ms(): int
	{
		return epc_prices_is_platform_operator_request() ? 15000 : 5000;
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
