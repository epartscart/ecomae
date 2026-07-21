<?php
/**
 * Anti-crawl / anti-scrape gate for storefront price + qty JSON endpoints.
 *
 * Goals:
 * - Block known crawlers/bots from price/qty APIs
 * - Rate-limit anonymous and authenticated scrapers by IP
 * - Never trust client-supplied user_id for price visibility
 * - Keep CP/internal tech_key + cp_bulk paths working
 */
defined('_ASTEXE_') or define('_ASTEXE_', true);

/**
 * Client IP (edge-aware when behind a private/local proxy).
 */
function epc_storefront_anti_crawl_client_ip(): string
{
	$remote = isset($_SERVER['REMOTE_ADDR']) ? (string) $_SERVER['REMOTE_ADDR'] : '';
	$trustProxy = $remote !== '' && (
		filter_var($remote, FILTER_VALIDATE_IP, FILTER_FLAG_NO_PRIV_RANGE | FILTER_FLAG_NO_RES_RANGE) === false
		|| $remote === '127.0.0.1'
		|| $remote === '::1'
	);
	$keys = $trustProxy
		? array('HTTP_CF_CONNECTING_IP', 'HTTP_X_FORWARDED_FOR', 'REMOTE_ADDR')
		: array('REMOTE_ADDR');
	foreach ($keys as $key) {
		if (empty($_SERVER[$key])) {
			continue;
		}
		$raw = (string) $_SERVER[$key];
		if ($key === 'HTTP_X_FORWARDED_FOR') {
			$parts = explode(',', $raw);
			$raw = trim($parts[0]);
		}
		if (filter_var($raw, FILTER_VALIDATE_IP)) {
			return $raw;
		}
	}
	return $remote !== '' ? $remote : '0.0.0.0';
}

/**
 * Expanded crawler / scraper / headless UA detector.
 */
function epc_storefront_anti_crawl_is_bot(?string $ua = null): bool
{
	$ua = strtolower(trim((string) ($ua !== null ? $ua : ($_SERVER['HTTP_USER_AGENT'] ?? ''))));
	if ($ua === '') {
		return true;
	}
	// Allow our own server-side CP fetchers.
	if (strpos($ua, 'epartscart cp') !== false || strpos($ua, 'ecomae cp') !== false) {
		return false;
	}
	$needles = array(
		'bot', 'crawl', 'spider', 'slurp', 'scrapy', 'curl/', 'wget', 'python-requests',
		'python-urllib', 'httpclient', 'libwww', 'httpunit', 'nutch', 'httrack',
		'phantomjs', 'headlesschrome', 'headless', 'selenium', 'puppeteer', 'playwright',
		'axios/', 'go-http-client', 'java/', 'okhttp', 'node-fetch', 'postmanruntime',
		'insomnia', 'apache-httpclient', 'mechanize', 'beautifulsoup', 'http.rb',
		'aiohttp', 'facebookexternalhit', 'bytespider', 'gptbot', 'claudebot',
		'ccbot', 'anthropic', 'petalbot', 'semrush', 'ahrefs', 'mj12bot', 'dotbot',
		'dataforseo', 'serpstat', 'screaming frog', 'siteauditbot', 'bingpreview',
		'yandex', 'baiduspider', 'duckduckbot', 'applebot', 'ia_archiver',
		'googlebot', 'adsbot-google', 'mediapartners-google', 'apis-google',
		'storebot-google', 'google-inspectiontool', 'chrome-lighthouse',
		'pingdom', 'uptimerobot', 'statuscake', 'monitor',
	);
	foreach ($needles as $needle) {
		if (strpos($ua, $needle) !== false) {
			return true;
		}
	}
	return false;
}

/**
 * True when request carries a valid platform tech_key (CP / internal bulk).
 */
function epc_storefront_anti_crawl_has_tech_key($DP_Config = null): bool
{
	if ($DP_Config === null && isset($GLOBALS['DP_Config'])) {
		$DP_Config = $GLOBALS['DP_Config'];
	}
	if (!is_object($DP_Config) || empty($DP_Config->tech_key)) {
		return false;
	}
	$provided = '';
	if (isset($_POST['tech_key'])) {
		$provided = (string) $_POST['tech_key'];
	} elseif (isset($_GET['tech_key'])) {
		$provided = (string) $_GET['tech_key'];
	} elseif (isset($_REQUEST['tech_key'])) {
		$provided = (string) $_REQUEST['tech_key'];
	}
	if ($provided === '') {
		return false;
	}
	return hash_equals((string) $DP_Config->tech_key, $provided);
}

/**
 * Session user id only — never trust client-posted user_id for visibility.
 */
function epc_storefront_anti_crawl_session_user_id(): int
{
	if (!class_exists('DP_User')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	}
	return (int) DP_User::getUserId();
}

/**
 * File-backed sliding window rate limit.
 *
 * @return array{blocked:bool,remaining:int,retry_after:int,count:int}
 */
function epc_storefront_anti_crawl_rate_limit(string $bucket, int $maxRequests, int $windowSeconds): array
{
	$out = array(
		'blocked' => false,
		'remaining' => $maxRequests,
		'retry_after' => 0,
		'count' => 0,
	);
	$ip = epc_storefront_anti_crawl_client_ip();
	$dir = rtrim(sys_get_temp_dir(), DIRECTORY_SEPARATOR) . DIRECTORY_SEPARATOR . 'epc_anti_crawl';
	if (!is_dir($dir)) {
		@mkdir($dir, 0755, true);
	}
	$file = $dir . DIRECTORY_SEPARATOR . preg_replace('/[^a-zA-Z0-9._-]/', '_', $bucket . '_' . $ip) . '.json';
	$now = time();
	$windowStart = $now - max(1, $windowSeconds);
	$hits = array();
	$fp = @fopen($file, 'c+');
	if (!$fp) {
		return $out; // fail open
	}
	try {
		flock($fp, LOCK_EX);
		$raw = stream_get_contents($fp);
		$data = (is_string($raw) && $raw !== '') ? json_decode($raw, true) : null;
		if (is_array($data) && !empty($data['hits']) && is_array($data['hits'])) {
			foreach ($data['hits'] as $t) {
				$t = (int) $t;
				if ($t >= $windowStart) {
					$hits[] = $t;
				}
			}
		}
		$hits[] = $now;
		$out['count'] = count($hits);
		$out['remaining'] = max(0, $maxRequests - $out['count']);
		if ($out['count'] > $maxRequests) {
			$out['blocked'] = true;
			$oldest = min($hits);
			$out['retry_after'] = max(1, ($oldest + $windowSeconds) - $now);
		}
		ftruncate($fp, 0);
		rewind($fp);
		fwrite($fp, json_encode(array('hits' => $hits), JSON_UNESCAPED_SLASHES));
		fflush($fp);
		flock($fp, LOCK_UN);
	} catch (Throwable $e) {
		@flock($fp, LOCK_UN);
	}
	fclose($fp);
	return $out;
}

/**
 * Enforce anti-crawl policy for a price/qty AJAX endpoint.
 *
 * @param object|null $DP_Config
 * @param array{bucket?:string,guest_max?:int,user_max?:int,window?:int,allow_tech_key?:bool} $opts
 * @return array{ok:bool,session_user_id:int,is_bot:bool,prices_visible:bool,tech_key:bool}
 */
function epc_storefront_anti_crawl_enforce($DP_Config = null, array $opts = array()): array
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_storefront_prices_helpers.php';

	$bucket = isset($opts['bucket']) ? (string) $opts['bucket'] : 'price_ajax';
	$guestMax = isset($opts['guest_max']) ? (int) $opts['guest_max'] : 30;
	$userMax = isset($opts['user_max']) ? (int) $opts['user_max'] : 120;
	$window = isset($opts['window']) ? (int) $opts['window'] : 60;
	$allowTechKey = !array_key_exists('allow_tech_key', $opts) || !empty($opts['allow_tech_key']);

	$techKeyOk = $allowTechKey && epc_storefront_anti_crawl_has_tech_key($DP_Config);
	$sessionUserId = epc_storefront_anti_crawl_session_user_id();
	$isBot = epc_storefront_anti_crawl_is_bot();

	// Internal CP / tech_key bulk: skip bot + rate-limit hard blocks.
	if ($techKeyOk) {
		return array(
			'ok' => true,
			'session_user_id' => $sessionUserId,
			'is_bot' => false,
			'prices_visible' => true,
			'tech_key' => true,
		);
	}

	if ($isBot) {
		epc_storefront_anti_crawl_deny(403, 'Crawler access blocked', $bucket);
	}

	$max = $sessionUserId > 0 ? $userMax : $guestMax;
	$rl = epc_storefront_anti_crawl_rate_limit($bucket, $max, $window);
	if (!empty($rl['blocked'])) {
		header('Retry-After: ' . (int) $rl['retry_after']);
		epc_storefront_anti_crawl_deny(429, 'Too many price requests — slow down', $bucket, (int) $rl['retry_after']);
	}

	$pricesVisible = epc_storefront_prices_visible_for_user($sessionUserId);

	return array(
		'ok' => true,
		'session_user_id' => $sessionUserId,
		'is_bot' => false,
		'prices_visible' => $pricesVisible,
		'tech_key' => false,
	);
}

/**
 * Exit with JSON deny payload.
 */
function epc_storefront_anti_crawl_deny(int $httpCode, string $message, string $bucket = '', int $retryAfter = 0): void
{
	if (!headers_sent()) {
		http_response_code($httpCode);
		header('Content-Type: application/json; charset=utf-8');
		header('X-Content-Type-Options: nosniff');
		header('Cache-Control: no-store');
		header('X-Robots-Tag: noindex, nofollow, noarchive');
		if ($retryAfter > 0) {
			header('Retry-After: ' . $retryAfter);
		}
	}
	$payload = array(
		'status' => false,
		'code' => $httpCode === 429 ? 'rate_limited' : 'forbidden',
		'message' => $message,
		'Products' => array(),
		'products' => array(),
		'stock' => array(),
		'references' => array(),
		'prices_visible' => false,
		'anti_crawl' => true,
	);
	if ($bucket !== '') {
		$payload['bucket'] = $bucket;
	}
	echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	exit;
}

/**
 * Redact cross-search stock rows (price / qty / warehouse).
 *
 * @param array<int,array<string,mixed>> $stock
 */
function epc_storefront_anti_crawl_redact_cross_stock(array &$stock): void
{
	$mask = function_exists('epc_storefront_sensitive_mask') ? epc_storefront_sensitive_mask() : '**';
	foreach ($stock as &$row) {
		if (!is_array($row)) {
			continue;
		}
		foreach (array('price', 'price_purchase', 'purchase', 'qty', 'exist', 'delivery', 'time_to_exe') as $k) {
			if (array_key_exists($k, $row)) {
				$row[$k] = ($k === 'qty' || $k === 'exist') ? null : 0;
			}
		}
		if (array_key_exists('warehouse', $row)) {
			$row['warehouse'] = $mask;
		}
		if (array_key_exists('storage_id', $row)) {
			$row['storage_id'] = 0;
		}
		if (array_key_exists('price_id', $row)) {
			$row['price_id'] = 0;
		}
		$row['prices_visible'] = false;
	}
	unset($row);
}

/**
 * Resolve user_id / group_id for product bunch markup WITHOUT trusting spoofed IDs.
 *
 * @return array{user_id:int,group_id:int}
 */
function epc_storefront_anti_crawl_resolve_pricing_identity($DP_Config = null): array
{
	$sessionUserId = epc_storefront_anti_crawl_session_user_id();
	$groupId = 0;
	if (!class_exists('DP_User')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
	}
	if ($sessionUserId > 0) {
		$profile = DP_User::getUserProfile();
		if (is_array($profile) && !empty($profile['groups'][0])) {
			$groupId = (int) $profile['groups'][0];
		}
		return array('user_id' => $sessionUserId, 'group_id' => $groupId);
	}

	// Guest: ignore client user_id/group_id entirely.
	return array('user_id' => 0, 'group_id' => 0);
}
