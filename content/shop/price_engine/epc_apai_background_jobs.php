<?php
/**
 * Auto Price AI — unified background jobs (crawl, warehouse match, compare refresh).
 * No single HTTP request should exceed ~20s origin time; jobs tick in job_status or cron.
 */
defined('_ASTEXE_') or die('No access');

/** Max seconds per job tick (stay under Cloudflare 524 / 20s rule). */
define('EPC_APAI_BG_TICK_MAX_SEC', 18);

/** Warehouse match rows per tick. */
define('EPC_APAI_BG_WH_BATCH', 50);

/**
 * @return array<int,string>
 */
function epc_apai_bg_job_types(): array
{
	return array('crawl_quick', 'crawl_full', 'warehouse_market_match', 'discover_seed', 'compare_refresh');
}

function epc_apai_bg_ensure_schema(PDO $pdo): void
{
	static $done = array();
	$key = spl_object_hash($pdo);
	if (isset($done[$key])) {
		return;
	}
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_apai_background_jobs` (
			`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
			`site_key` VARCHAR(64) NOT NULL DEFAULT \'\',
			`job_type` VARCHAR(32) NOT NULL DEFAULT \'\',
			`status` VARCHAR(16) NOT NULL DEFAULT \'pending\',
			`progress_pct` TINYINT UNSIGNED NOT NULL DEFAULT 0,
			`progress_msg` VARCHAR(512) NOT NULL DEFAULT \'\',
			`options_json` TEXT NULL,
			`result_json` TEXT NULL,
			`created_at` INT NOT NULL DEFAULT 0,
			`started_at` INT NOT NULL DEFAULT 0,
			`finished_at` INT NOT NULL DEFAULT 0,
			KEY `site_status` (`site_key`, `status`),
			KEY `status_created` (`status`, `created_at`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8'
	);
	$done[$key] = true;
}

/**
 * @param array<string,mixed> $options
 */
function epc_apai_bg_start(PDO $pdo, string $siteKey, string $jobType, array $options = array()): int
{
	epc_apai_bg_ensure_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$jobType = preg_replace('/[^a-z_]/', '', strtolower(trim($jobType)));
	if (!in_array($jobType, epc_apai_bg_job_types(), true)) {
		throw new InvalidArgumentException('Unknown job type: ' . $jobType);
	}

	$now = time();
	try {
		$pdo->prepare(
			'UPDATE `epc_apai_background_jobs` SET `status` = \'failed\', `progress_msg` = ?, `finished_at` = ?
			 WHERE `site_key` = ? AND `job_type` = ? AND `status` IN (\'pending\', \'running\')'
		)->execute(array('Superseded by new job', $now, $siteKey, $jobType));
	} catch (Throwable $e) {
	}

	$pdo->prepare(
		'INSERT INTO `epc_apai_background_jobs`
		 (`site_key`, `job_type`, `status`, `progress_pct`, `progress_msg`, `options_json`, `created_at`)
		 VALUES (?, ?, \'pending\', 0, ?, ?, ?)'
	)->execute(array(
		$siteKey,
		$jobType,
		'Queued…',
		json_encode($options, JSON_UNESCAPED_UNICODE),
		$now,
	));
	return (int) $pdo->lastInsertId();
}

/**
 * @return array<string,mixed>|null
 */
function epc_apai_bg_get(PDO $pdo, int $jobId, string $siteKey = ''): ?array
{
	epc_apai_bg_ensure_schema($pdo);
	if ($jobId <= 0) {
		return null;
	}
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	if ($siteKey !== '') {
		$stmt = $pdo->prepare('SELECT * FROM `epc_apai_background_jobs` WHERE `id` = ? AND `site_key` = ? LIMIT 1');
		$stmt->execute(array($jobId, $siteKey));
	} else {
		$stmt = $pdo->prepare('SELECT * FROM `epc_apai_background_jobs` WHERE `id` = ? LIMIT 1');
		$stmt->execute(array($jobId));
	}
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

/**
 * @return array<string,mixed>|null
 */
function epc_apai_bg_active(PDO $pdo, string $siteKey, string $jobType = ''): ?array
{
	epc_apai_bg_ensure_schema($pdo);
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$sql = 'SELECT * FROM `epc_apai_background_jobs` WHERE `site_key` = ? AND `status` IN (\'pending\', \'running\')';
	$params = array($siteKey);
	if ($jobType !== '') {
		$sql .= ' AND `job_type` = ?';
		$params[] = preg_replace('/[^a-z_]/', '', strtolower(trim($jobType)));
	}
	$sql .= ' ORDER BY `id` DESC LIMIT 1';
	$stmt = $pdo->prepare($sql);
	$stmt->execute($params);
	$row = $stmt->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

/**
 * Format job row for AJAX job_status response.
 *
 * @return array<string,mixed>
 */
function epc_apai_bg_status_payload(array $job): array
{
	$result = json_decode((string) ($job['result_json'] ?? ''), true);
	if (!is_array($result)) {
		$result = array();
	}
	$elapsed = 0;
	$started = (int) ($job['started_at'] ?? 0);
	if ($started > 0) {
		$finished = (int) ($job['finished_at'] ?? 0);
		$elapsed = ($finished > 0 ? $finished : time()) - $started;
	}
	return array(
		'ok' => true,
		'job_id' => (int) ($job['id'] ?? 0),
		'job_type' => (string) ($job['job_type'] ?? ''),
		'status' => (string) ($job['status'] ?? 'pending'),
		'progress_pct' => (int) ($job['progress_pct'] ?? 0),
		'progress_msg' => (string) ($job['progress_msg'] ?? ''),
		'elapsed_sec' => $elapsed,
		'result' => $result,
		'message' => (string) ($result['message'] ?? $job['progress_msg'] ?? ''),
	);
}

function epc_apai_bg_update(PDO $pdo, int $jobId, array $fields): void
{
	$allowed = array('status', 'progress_pct', 'progress_msg', 'result_json', 'started_at', 'finished_at');
	$sets = array();
	$params = array();
	foreach ($fields as $k => $v) {
		if (!in_array($k, $allowed, true)) {
			continue;
		}
		$sets[] = '`' . $k . '` = ?';
		$params[] = $v;
	}
	if (!$sets) {
		return;
	}
	$params[] = $jobId;
	$pdo->prepare('UPDATE `epc_apai_background_jobs` SET ' . implode(', ', $sets) . ' WHERE `id` = ? LIMIT 1')->execute($params);
}

/**
 * Process one tick of a job (max EPC_APAI_BG_TICK_MAX_SEC). Returns updated job row.
 *
 * @return array<string,mixed>|null
 */
function epc_apai_bg_tick(PDO $pdo, int $jobId, string $siteKey = ''): ?array
{
	epc_apai_bg_ensure_schema($pdo);
	require_once __DIR__ . '/epc_auto_price_engine.php';
	require_once __DIR__ . '/epc_discovery_adapters.php';

	$job = epc_apai_bg_get($pdo, $jobId, $siteKey);
	if (!$job) {
		return null;
	}
	$status = (string) ($job['status'] ?? '');
	if ($status === 'done' || $status === 'failed') {
		return $job;
	}

	$now = time();
	if ($status === 'pending') {
		epc_apai_bg_update($pdo, $jobId, array(
			'status' => 'running',
			'started_at' => $now,
			'progress_msg' => 'Starting…',
		));
		$job['status'] = 'running';
		$job['started_at'] = $now;
	}

	$jobType = (string) ($job['job_type'] ?? '');
	$jobSite = (string) ($job['site_key'] ?? '');
	$tickStarted = microtime(true);

	try {
		if ($jobType === 'crawl_quick' || $jobType === 'crawl_full') {
			$job = epc_apai_bg_tick_crawl($pdo, $job);
		} elseif ($jobType === 'warehouse_market_match') {
			$job = epc_apai_bg_tick_warehouse_match($pdo, $job);
		} elseif ($jobType === 'discover_seed') {
			$job = epc_apai_bg_tick_discover_seed($pdo, $job);
		} elseif ($jobType === 'compare_refresh') {
			$job = epc_apai_bg_tick_compare_refresh($pdo, $job);
		} else {
			epc_apai_bg_update($pdo, $jobId, array(
				'status' => 'failed',
				'progress_msg' => 'Unknown job type',
				'finished_at' => time(),
			));
		}
	} catch (Throwable $e) {
		epc_apai_bg_update($pdo, $jobId, array(
			'status' => 'failed',
			'progress_msg' => 'Error: ' . $e->getMessage(),
			'result_json' => json_encode(array('ok' => false, 'message' => $e->getMessage()), JSON_UNESCAPED_UNICODE),
			'finished_at' => time(),
		));
	}

	unset($tickStarted);
	return epc_apai_bg_get($pdo, $jobId, $siteKey);
}

/**
 * Process oldest pending/running job for tenant (or platform-wide if siteKey empty).
 */
function epc_apai_bg_process_pending(PDO $pdo, string $siteKey = '', int $limit = 1): int
{
	epc_apai_bg_ensure_schema($pdo);
	$processed = 0;
	for ($i = 0; $i < $limit; $i++) {
		$sql = 'SELECT `id`, `site_key` FROM `epc_apai_background_jobs` WHERE `status` IN (\'pending\', \'running\')';
		$params = array();
		if ($siteKey !== '') {
			$sql .= ' AND `site_key` = ?';
			$params[] = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
		}
		$sql .= ' ORDER BY `id` ASC LIMIT 1';
		$stmt = $pdo->prepare($sql);
		$stmt->execute($params);
		$row = $stmt->fetch(PDO::FETCH_ASSOC);
		if (!$row) {
			break;
		}
		epc_apai_bg_tick($pdo, (int) $row['id'], (string) ($row['site_key'] ?? ''));
		$processed++;
	}
	return $processed;
}

/**
 * @param array<string,mixed> $job
 * @return array<string,mixed>
 */
function epc_apai_bg_tick_crawl(PDO $pdo, array $job): array
{
	$jobId = (int) ($job['id'] ?? 0);
	$siteKey = (string) ($job['site_key'] ?? '');
	$jobType = (string) ($job['job_type'] ?? 'crawl_quick');
	$mode = ($jobType === 'crawl_full') ? 'full' : 'quick';
	$opts = json_decode((string) ($job['options_json'] ?? ''), true);
	if (!is_array($opts)) {
		$opts = array();
	}

	if (function_exists('epc_apai_link_warehouse_price_lists')) {
		epc_apai_link_warehouse_price_lists($pdo, $siteKey);
	}
	if (function_exists('epc_disc_auto_seed_if_empty')) {
		epc_disc_auto_seed_if_empty($pdo, $siteKey);
	}

	$sourcesAll = epc_disc_sources_for_search($pdo, $siteKey, max(0, (int) ($opts['taxonomy_id'] ?? 0)), '', true);
	$industryKey = epc_apai_resolve_industry($pdo, $siteKey);
	if ($industryKey === 'auto_parts' && function_exists('epc_disc_sort_sources_autoparts_primary')) {
		$sourcesAll = epc_disc_sort_sources_autoparts_primary($sourcesAll, $industryKey);
	}
	$sources = ($mode === 'quick')
		? epc_disc_sources_for_crawl_mode($sourcesAll, 'quick')
		: $sourcesAll;
	$sourceTotal = count($sources);
	$firstDomain = (string) (($sources[0]['domain'] ?? '') ?: 'sources');

	epc_apai_bg_update($pdo, $jobId, array(
		'progress_pct' => 15,
		'progress_msg' => 'Crawling ' . $firstDomain . '… 0/' . max(1, $sourceTotal) . ' sources',
	));

	$res = epc_disc_crawl_sources($pdo, $siteKey, array_merge($opts, array('mode' => $mode)));
	$fetched = (int) ($res['sources_fetched'] ?? $res['sources_crawled'] ?? $sourceTotal);
	$total = max(1, (int) ($res['sources_total'] ?? $sourceTotal));
	$msg = (string) ($res['message'] ?? 'Crawl complete');
	$progressMsg = 'Crawled ' . min($fetched, $total) . '/' . $total . ' sources — ' . $msg;

	epc_apai_bg_update($pdo, $jobId, array(
		'status' => 'done',
		'progress_pct' => 100,
		'progress_msg' => $progressMsg,
		'result_json' => json_encode(array_merge($res, array(
			'ok' => !empty($res['ok']),
			'message' => $progressMsg,
		)), JSON_UNESCAPED_UNICODE),
		'finished_at' => time(),
	));

	return epc_apai_bg_get($pdo, $jobId, $siteKey) ?: $job;
}

/**
 * @param array<string,mixed> $job
 * @return array<string,mixed>
 */
function epc_apai_bg_tick_warehouse_match(PDO $pdo, array $job): array
{
	$jobId = (int) ($job['id'] ?? 0);
	$siteKey = (string) ($job['site_key'] ?? '');
	$state = json_decode((string) ($job['result_json'] ?? ''), true);
	if (!is_array($state)) {
		$state = array();
	}

	if (function_exists('epc_apai_link_warehouse_price_lists')) {
		epc_apai_link_warehouse_price_lists($pdo, $siteKey);
	}
	epc_disc_cross_source_match($pdo, $siteKey);

	if (empty($state['offset'])) {
		$maps = epc_disc_resolve_warehouse_price_lists($pdo, $siteKey);
		$priceIds = array();
		foreach ($maps as $m) {
			$pid = (int) ($m['price_list_id'] ?? 0);
			if ($pid > 0) {
				$priceIds[$pid] = $pid;
			}
		}
		if (!$priceIds) {
			epc_apai_bg_update($pdo, $jobId, array(
				'status' => 'failed',
				'progress_pct' => 0,
				'progress_msg' => 'No warehouse price lists linked',
				'result_json' => json_encode(array('ok' => false, 'count' => 0, 'message' => 'No warehouse price lists linked'), JSON_UNESCAPED_UNICODE),
				'finished_at' => time(),
			));
			return epc_apai_bg_get($pdo, $jobId, $siteKey) ?: $job;
		}
		$idList = implode(',', array_map('intval', array_values($priceIds)));
		$total = (int) $pdo->query(
			"SELECT COUNT(*) FROM (
				SELECT 1 FROM `shop_docpart_prices_data`
				WHERE `price_id` IN ({$idList}) AND IFNULL(`price`, 0) > 0
				  AND TRIM(COALESCE(NULLIF(TRIM(`article`), ''), TRIM(`article_show`), '')) != ''
				GROUP BY LOWER(TRIM(`manufacturer`)),
				         UPPER(REPLACE(REPLACE(REPLACE(COALESCE(NULLIF(TRIM(`article`), ''), TRIM(`article_show`)), ' ', ''), '-', ''), '.', ''))
			) t"
		)->fetchColumn();
		$state = array(
			'offset' => 0,
			'total' => $total,
			'upserted' => 0,
			'price_ids' => array_values($priceIds),
		);
		epc_apai_bg_update($pdo, $jobId, array(
			'progress_pct' => 1,
			'progress_msg' => 'Matching ' . $total . ' warehouse SKU(s)…',
			'result_json' => json_encode($state, JSON_UNESCAPED_UNICODE),
		));
	}

	$batch = epc_disc_match_warehouse_to_market_batch(
		$pdo,
		$siteKey,
		(array) ($state['price_ids'] ?? array()),
		(int) ($state['offset'] ?? 0),
		EPC_APAI_BG_WH_BATCH
	);
	$state['offset'] = (int) ($batch['next_offset'] ?? 0);
	$state['upserted'] = (int) ($state['upserted'] ?? 0) + (int) ($batch['count'] ?? 0);
	$total = max(1, (int) ($state['total'] ?? 1));
	$done = ($state['offset'] >= $total) || empty($batch['has_more']);

	if ($done) {
		$counts = epc_disc_warehouse_market_counts($pdo, $siteKey);
		$msg = (int) $state['upserted'] . ' warehouse SKU(s) matched to market';
		epc_apai_bg_update($pdo, $jobId, array(
			'status' => 'done',
			'progress_pct' => 100,
			'progress_msg' => $msg,
			'result_json' => json_encode(array(
				'ok' => true,
				'count' => (int) $state['upserted'],
				'total' => (int) ($counts['total'] ?? $state['upserted']),
				'counts' => $counts,
				'message' => $msg,
			), JSON_UNESCAPED_UNICODE),
			'finished_at' => time(),
		));
	} else {
		$pct = min(99, max(2, (int) floor(($state['offset'] / $total) * 100)));
		$msg = 'Matching warehouse SKUs… ' . $state['offset'] . '/' . $total;
		epc_apai_bg_update($pdo, $jobId, array(
			'progress_pct' => $pct,
			'progress_msg' => $msg,
			'result_json' => json_encode($state, JSON_UNESCAPED_UNICODE),
		));
	}

	return epc_apai_bg_get($pdo, $jobId, $siteKey) ?: $job;
}

/**
 * @param array<string,mixed> $job
 * @return array<string,mixed>
 */
function epc_apai_bg_tick_discover_seed(PDO $pdo, array $job): array
{
	$jobId = (int) ($job['id'] ?? 0);
	$siteKey = (string) ($job['site_key'] ?? '');
	require_once __DIR__ . '/epc_apai_country_sources.php';

	epc_apai_bg_update($pdo, $jobId, array('progress_pct' => 10, 'progress_msg' => 'Installing country sources…'));
	$added = epc_apai_install_country_sources($pdo, $siteKey);
	$marketAdded = 0;
	if (function_exists('epc_apai_install_sell_marketplaces')) {
		epc_apai_bg_update($pdo, $jobId, array('progress_pct' => 50, 'progress_msg' => 'Installing sell marketplaces…'));
		$marketAdded = epc_apai_install_sell_marketplaces($pdo, $siteKey);
	}
	if (function_exists('epc_disc_auto_seed_if_empty')) {
		epc_apai_bg_update($pdo, $jobId, array('progress_pct' => 80, 'progress_msg' => 'Seeding discovery queue…'));
		epc_disc_auto_seed_if_empty($pdo, $siteKey);
	}
	$msg = $added . ' source(s) installed' . ($marketAdded ? ', ' . $marketAdded . ' marketplace(s)' : '');
	epc_apai_bg_update($pdo, $jobId, array(
		'status' => 'done',
		'progress_pct' => 100,
		'progress_msg' => $msg,
		'result_json' => json_encode(array('ok' => true, 'sources_added' => $added, 'marketplaces_added' => $marketAdded, 'message' => $msg), JSON_UNESCAPED_UNICODE),
		'finished_at' => time(),
	));
	return epc_apai_bg_get($pdo, $jobId, $siteKey) ?: $job;
}

/**
 * @param array<string,mixed> $job
 * @return array<string,mixed>
 */
function epc_apai_bg_tick_compare_refresh(PDO $pdo, array $job): array
{
	$jobId = (int) ($job['id'] ?? 0);
	$siteKey = (string) ($job['site_key'] ?? '');

	epc_apai_bg_update($pdo, $jobId, array('progress_pct' => 20, 'progress_msg' => 'Cross-source match…'));
	epc_disc_cross_source_match($pdo, $siteKey);
	$catMatch = array('count' => 0);
	if (function_exists('epc_disc_match_catalogue_to_market')) {
		epc_apai_bg_update($pdo, $jobId, array('progress_pct' => 60, 'progress_msg' => 'Catalogue vs market…'));
		$catMatch = epc_disc_match_catalogue_to_market($pdo, $siteKey);
	}
	$msg = 'Compare refreshed — ' . (int) ($catMatch['count'] ?? 0) . ' catalogue match(es)';
	epc_apai_bg_update($pdo, $jobId, array(
		'status' => 'done',
		'progress_pct' => 100,
		'progress_msg' => $msg,
		'result_json' => json_encode(array_merge(array('ok' => true, 'message' => $msg), $catMatch), JSON_UNESCAPED_UNICODE),
		'finished_at' => time(),
	));
	return epc_apai_bg_get($pdo, $jobId, $siteKey) ?: $job;
}

/**
 * Fire non-blocking worker HTTP request (cron endpoint processes crawl jobs).
 */
function epc_apai_bg_trigger_worker(string $siteKey, int $jobId = 0): void
{
	$siteKey = preg_replace('/[^a-z0-9_]/', '', strtolower(trim($siteKey)));
	$token = 'epartscart-deploy-2026';
	if (is_file($_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/epc_deploy_auth.php';
		if (defined('EPC_DEPLOY_TOKEN')) {
			$token = (string) EPC_DEPLOY_TOKEN;
		}
	}
	$host = $_SERVER['HTTP_HOST'] ?? 'www.ecomae.com';
	$qs = 'token=' . rawurlencode($token) . '&site_key=' . rawurlencode($siteKey);
	if ($jobId > 0) {
		$qs .= '&job_id=' . (int) $jobId;
	}
	$url = 'https://' . $host . '/epc-apai-background-jobs-cron.php?' . $qs;
	if (function_exists('curl_init')) {
		$ch = curl_init($url);
		curl_setopt_array($ch, array(
			CURLOPT_RETURNTRANSFER => true,
			CURLOPT_TIMEOUT_MS => 800,
			CURLOPT_CONNECTTIMEOUT_MS => 800,
			CURLOPT_NOSIGNAL => true,
			CURLOPT_SSL_VERIFYPEER => false,
		));
		@curl_exec($ch);
		@curl_close($ch);
	}
}

/**
 * Kick first tick after start_job — flush response then continue if possible.
 */
function epc_apai_bg_kick_async(PDO $pdo, int $jobId, string $siteKey): void
{
	if (function_exists('fastcgi_finish_request')) {
		@fastcgi_finish_request();
	} elseif (function_exists('litespeed_finish_request')) {
		@litespeed_finish_request();
	}
	@set_time_limit(25);
	epc_apai_bg_tick($pdo, $jobId, $siteKey);
}
