<?php
/**
 * First-party website traffic tracker — sessions, pageviews, clicks, search, geo.
 * Data lives in the platform DB (site_key scoped) for Super CP + every tenant CP.
 */
declare(strict_types=1);

if (!defined('_ASTEXE_') && !defined('EPC_WEB_TRACKER_STANDALONE')) {
	// Allow standalone collect endpoint.
}

function epc_web_tracker_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_web_tracker_ensure_schema(PDO $pdo): void
{
	static $done = false;
	if ($done) {
		return;
	}
	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_web_tracker_sessions` (
			`id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
			`session_uid` CHAR(36) NOT NULL,
			`visitor_uid` CHAR(36) NOT NULL DEFAULT \'\',
			`site_key` VARCHAR(64) NOT NULL,
			`hostname` VARCHAR(255) NOT NULL DEFAULT \'\',
			`user_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`is_registered` TINYINT(1) NOT NULL DEFAULT 0,
			`first_seen_at` INT UNSIGNED NOT NULL DEFAULT 0,
			`last_seen_at` INT UNSIGNED NOT NULL DEFAULT 0,
			`pageview_count` INT UNSIGNED NOT NULL DEFAULT 0,
			`event_count` INT UNSIGNED NOT NULL DEFAULT 0,
			`duration_ms` INT UNSIGNED NOT NULL DEFAULT 0,
			`landing_path` VARCHAR(512) NOT NULL DEFAULT \'\',
			`landing_title` VARCHAR(255) NOT NULL DEFAULT \'\',
			`exit_path` VARCHAR(512) NOT NULL DEFAULT \'\',
			`referrer` VARCHAR(1024) NOT NULL DEFAULT \'\',
			`referrer_host` VARCHAR(255) NOT NULL DEFAULT \'\',
			`utm_source` VARCHAR(128) NOT NULL DEFAULT \'\',
			`utm_medium` VARCHAR(128) NOT NULL DEFAULT \'\',
			`utm_campaign` VARCHAR(128) NOT NULL DEFAULT \'\',
			`utm_term` VARCHAR(128) NOT NULL DEFAULT \'\',
			`utm_content` VARCHAR(128) NOT NULL DEFAULT \'\',
			`ip` VARCHAR(45) NOT NULL DEFAULT \'\',
			`country_code` VARCHAR(8) NOT NULL DEFAULT \'\',
			`country_name` VARCHAR(64) NOT NULL DEFAULT \'\',
			`region` VARCHAR(128) NOT NULL DEFAULT \'\',
			`city` VARCHAR(128) NOT NULL DEFAULT \'\',
			`ua` VARCHAR(512) NOT NULL DEFAULT \'\',
			`device_type` VARCHAR(16) NOT NULL DEFAULT \'\',
			`browser` VARCHAR(64) NOT NULL DEFAULT \'\',
			`os` VARCHAR(64) NOT NULL DEFAULT \'\',
			`screen_w` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
			`screen_h` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
			`language` VARCHAR(32) NOT NULL DEFAULT \'\',
			`timezone` VARCHAR(64) NOT NULL DEFAULT \'\',
			PRIMARY KEY (`id`),
			UNIQUE KEY `uq_site_session` (`site_key`, `session_uid`),
			KEY `idx_site_last` (`site_key`, `last_seen_at`),
			KEY `idx_site_first` (`site_key`, `first_seen_at`),
			KEY `idx_country` (`site_key`, `country_code`),
			KEY `idx_user` (`site_key`, `user_id`),
			KEY `idx_visitor` (`site_key`, `visitor_uid`)
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_web_tracker_pageviews` (
			`id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
			`session_id` BIGINT UNSIGNED NOT NULL DEFAULT 0,
			`session_uid` CHAR(36) NOT NULL,
			`site_key` VARCHAR(64) NOT NULL,
			`user_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`ts` INT UNSIGNED NOT NULL DEFAULT 0,
			`path` VARCHAR(512) NOT NULL DEFAULT \'\',
			`query_string` VARCHAR(1024) NOT NULL DEFAULT \'\',
			`title` VARCHAR(255) NOT NULL DEFAULT \'\',
			`referrer` VARCHAR(1024) NOT NULL DEFAULT \'\',
			`load_time_ms` INT UNSIGNED NOT NULL DEFAULT 0,
			`time_on_page_ms` INT UNSIGNED NOT NULL DEFAULT 0,
			`scroll_max_pct` TINYINT UNSIGNED NOT NULL DEFAULT 0,
			`viewport_w` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
			`viewport_h` SMALLINT UNSIGNED NOT NULL DEFAULT 0,
			PRIMARY KEY (`id`),
			KEY `idx_site_ts` (`site_key`, `ts`),
			KEY `idx_session` (`session_id`),
			KEY `idx_path` (`site_key`, `path`(191))
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);

	$pdo->exec(
		'CREATE TABLE IF NOT EXISTS `epc_web_tracker_events` (
			`id` BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
			`session_id` BIGINT UNSIGNED NOT NULL DEFAULT 0,
			`pageview_id` BIGINT UNSIGNED NOT NULL DEFAULT 0,
			`session_uid` CHAR(36) NOT NULL,
			`site_key` VARCHAR(64) NOT NULL,
			`user_id` INT UNSIGNED NOT NULL DEFAULT 0,
			`ts` INT UNSIGNED NOT NULL DEFAULT 0,
			`event_type` VARCHAR(32) NOT NULL DEFAULT \'\',
			`path` VARCHAR(512) NOT NULL DEFAULT \'\',
			`x` INT NOT NULL DEFAULT 0,
			`y` INT NOT NULL DEFAULT 0,
			`page_x` INT NOT NULL DEFAULT 0,
			`page_y` INT NOT NULL DEFAULT 0,
			`element_tag` VARCHAR(32) NOT NULL DEFAULT \'\',
			`element_id` VARCHAR(128) NOT NULL DEFAULT \'\',
			`element_class` VARCHAR(255) NOT NULL DEFAULT \'\',
			`element_text` VARCHAR(255) NOT NULL DEFAULT \'\',
			`element_href` VARCHAR(1024) NOT NULL DEFAULT \'\',
			`element_name` VARCHAR(128) NOT NULL DEFAULT \'\',
			`css_path` VARCHAR(512) NOT NULL DEFAULT \'\',
			`search_query` VARCHAR(512) NOT NULL DEFAULT \'\',
			`search_context` VARCHAR(64) NOT NULL DEFAULT \'\',
			`meta_json` TEXT NULL,
			PRIMARY KEY (`id`),
			KEY `idx_site_ts` (`site_key`, `ts`),
			KEY `idx_session` (`session_id`),
			KEY `idx_type` (`site_key`, `event_type`, `ts`),
			KEY `idx_search` (`site_key`, `search_query`(191))
		) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4'
	);
	$done = true;
}

/** @return array{code:string,name:string,region:string,city:string} */
function epc_web_tracker_geo_from_request(): array
{
	$code = '';
	$name = '';
	$region = '';
	$city = '';
	foreach (array('HTTP_CF_IPCOUNTRY', 'HTTP_X_COUNTRY_CODE', 'HTTP_X_APPENGINE_COUNTRY') as $h) {
		if (!empty($_SERVER[$h]) && preg_match('/^[A-Za-z]{2}$/', (string) $_SERVER[$h])) {
			$code = strtoupper((string) $_SERVER[$h]);
			break;
		}
	}
	if ($code === 'XX' || $code === 'T1') {
		$code = '';
	}
	$ip = epc_web_tracker_client_ip();
	if ($code === '' && $ip !== '') {
		$looked = epc_web_tracker_lookup_ip($ip);
		$code = $looked['code'];
		$name = $looked['name'];
		$region = $looked['region'];
		$city = $looked['city'];
	} elseif ($code !== '' && $name === '') {
		$name = epc_web_tracker_country_name($code);
	}
	return array('code' => $code, 'name' => $name, 'region' => $region, 'city' => $city);
}

function epc_web_tracker_client_ip(): string
{
	$candidates = array();
	foreach (array('HTTP_CF_CONNECTING_IP', 'HTTP_X_REAL_IP', 'HTTP_X_FORWARDED_FOR', 'REMOTE_ADDR') as $h) {
		if (empty($_SERVER[$h])) {
			continue;
		}
		$raw = (string) $_SERVER[$h];
		if ($h === 'HTTP_X_FORWARDED_FOR') {
			$parts = explode(',', $raw);
			$raw = trim($parts[0]);
		}
		$candidates[] = $raw;
	}
	foreach ($candidates as $ip) {
		if (filter_var($ip, FILTER_VALIDATE_IP)) {
			return $ip;
		}
	}
	return '';
}

/** @return array{code:string,name:string,region:string,city:string} */
function epc_web_tracker_lookup_ip(string $ip): array
{
	static $mem = array();
	$empty = array('code' => '', 'name' => '', 'region' => '', 'city' => '');
	$ip = trim($ip);
	if ($ip === '' || isset($mem[$ip])) {
		return $ip !== '' && isset($mem[$ip]) ? $mem[$ip] : $empty;
	}
	if (!filter_var($ip, FILTER_VALIDATE_IP, FILTER_FLAG_NO_PRIV_RANGE | FILTER_FLAG_NO_RES_RANGE)) {
		return $mem[$ip] = $empty;
	}
	$cacheDir = sys_get_temp_dir() . '/epc_web_tracker_geo';
	if (!is_dir($cacheDir)) {
		@mkdir($cacheDir, 0700, true);
	}
	$cacheFile = $cacheDir . '/' . hash('sha256', $ip) . '.json';
	if (is_file($cacheFile) && (time() - (int) @filemtime($cacheFile)) < 2592000) {
		$cached = json_decode((string) @file_get_contents($cacheFile), true);
		if (is_array($cached) && !empty($cached['code'])) {
			return $mem[$ip] = array(
				'code' => strtoupper((string) $cached['code']),
				'name' => (string) ($cached['name'] ?? ''),
				'region' => (string) ($cached['region'] ?? ''),
				'city' => (string) ($cached['city'] ?? ''),
			);
		}
	}
	$url = 'https://ipapi.co/' . rawurlencode($ip) . '/json/';
	$ctx = stream_context_create(array(
		'http' => array(
			'timeout' => 1.5,
			'ignore_errors' => true,
			'header' => "User-Agent: ECOM-AE-WebTracker/1.0\r\nAccept: application/json\r\n",
		),
	));
	$raw = @file_get_contents($url, false, $ctx);
	$code = '';
	$name = '';
	$region = '';
	$city = '';
	if ($raw !== false && $raw !== '') {
		$data = json_decode($raw, true);
		if (is_array($data) && empty($data['error']) && !empty($data['country_code'])) {
			$code = strtoupper(substr((string) $data['country_code'], 0, 8));
			$name = trim((string) ($data['country_name'] ?? ''));
			$region = trim((string) ($data['region'] ?? ''));
			$city = trim((string) ($data['city'] ?? ''));
		}
	}
	if ($code !== '' && $name === '') {
		$name = epc_web_tracker_country_name($code);
	}
	$result = array('code' => $code, 'name' => $name, 'region' => $region, 'city' => $city);
	if ($code !== '') {
		@file_put_contents($cacheFile, json_encode($result, JSON_UNESCAPED_UNICODE), LOCK_EX);
	}
	return $mem[$ip] = $result;
}

function epc_web_tracker_country_name(string $code): string
{
	$map = array(
		'AE' => 'United Arab Emirates', 'SA' => 'Saudi Arabia', 'QA' => 'Qatar', 'KW' => 'Kuwait',
		'BH' => 'Bahrain', 'OM' => 'Oman', 'IN' => 'India', 'PK' => 'Pakistan', 'US' => 'United States',
		'GB' => 'United Kingdom', 'DE' => 'Germany', 'FR' => 'France', 'RU' => 'Russia', 'CN' => 'China',
		'JP' => 'Japan', 'KR' => 'South Korea', 'TR' => 'Turkey', 'EG' => 'Egypt', 'ZA' => 'South Africa',
		'AU' => 'Australia', 'CA' => 'Canada', 'SG' => 'Singapore', 'MY' => 'Malaysia', 'PH' => 'Philippines',
		'ID' => 'Indonesia', 'TH' => 'Thailand', 'VN' => 'Vietnam', 'NG' => 'Nigeria', 'KE' => 'Kenya',
		'UA' => 'Ukraine', 'PL' => 'Poland', 'IT' => 'Italy', 'ES' => 'Spain', 'NL' => 'Netherlands',
		'SE' => 'Sweden', 'NO' => 'Norway', 'FI' => 'Finland', 'DK' => 'Denmark', 'CH' => 'Switzerland',
		'BR' => 'Brazil', 'MX' => 'Mexico', 'AR' => 'Argentina', 'CL' => 'Chile', 'NZ' => 'New Zealand',
	);
	$code = strtoupper($code);
	return $map[$code] ?? $code;
}

function epc_web_tracker_parse_ua(string $ua): array
{
	$device = 'desktop';
	$browser = 'Other';
	$os = 'Other';
	$uaL = strtolower($ua);
	if (strpos($uaL, 'tablet') !== false || strpos($uaL, 'ipad') !== false) {
		$device = 'tablet';
	} elseif (strpos($uaL, 'mobi') !== false || strpos($uaL, 'android') !== false || strpos($uaL, 'iphone') !== false) {
		$device = 'mobile';
	}
	if (strpos($uaL, 'edg/') !== false) {
		$browser = 'Edge';
	} elseif (strpos($uaL, 'chrome') !== false && strpos($uaL, 'chromium') === false) {
		$browser = 'Chrome';
	} elseif (strpos($uaL, 'safari') !== false && strpos($uaL, 'chrome') === false) {
		$browser = 'Safari';
	} elseif (strpos($uaL, 'firefox') !== false) {
		$browser = 'Firefox';
	} elseif (strpos($uaL, 'msie') !== false || strpos($uaL, 'trident') !== false) {
		$browser = 'IE';
	}
	if (strpos($uaL, 'windows') !== false) {
		$os = 'Windows';
	} elseif (strpos($uaL, 'mac os') !== false || strpos($uaL, 'macintosh') !== false) {
		$os = 'macOS';
	} elseif (strpos($uaL, 'android') !== false) {
		$os = 'Android';
	} elseif (strpos($uaL, 'iphone') !== false || strpos($uaL, 'ipad') !== false) {
		$os = 'iOS';
	} elseif (strpos($uaL, 'linux') !== false) {
		$os = 'Linux';
	}
	return array('device_type' => $device, 'browser' => $browser, 'os' => $os);
}

function epc_web_tracker_clip(string $s, int $max): string
{
	$s = trim(preg_replace('/\s+/u', ' ', $s) ?? $s);
	if (function_exists('mb_substr')) {
		return (string) mb_substr($s, 0, $max, 'UTF-8');
	}
	return substr($s, 0, $max);
}

function epc_web_tracker_uuid_ok(string $uid): bool
{
	return (bool) preg_match('/^[a-f0-9\-]{8,36}$/i', $uid);
}

/**
 * Ingest a batch payload from the browser beacon.
 *
 * @param array<string,mixed> $payload
 * @return array{ok:bool,session_id:int,pageviews:int,events:int,error?:string}
 */
function epc_web_tracker_ingest(PDO $pdo, array $payload): array
{
	epc_web_tracker_ensure_schema($pdo);

	$siteKey = preg_replace('/[^a-z0-9_\-]/', '', strtolower((string) ($payload['site_key'] ?? '')));
	$sessionUid = (string) ($payload['session_uid'] ?? '');
	$visitorUid = (string) ($payload['visitor_uid'] ?? '');
	if ($siteKey === '' || !epc_web_tracker_uuid_ok($sessionUid)) {
		return array('ok' => false, 'session_id' => 0, 'pageviews' => 0, 'events' => 0, 'error' => 'bad_ids');
	}
	if ($visitorUid !== '' && !epc_web_tracker_uuid_ok($visitorUid)) {
		$visitorUid = '';
	}

	$hostname = epc_web_tracker_clip((string) ($payload['hostname'] ?? ($_SERVER['HTTP_HOST'] ?? '')), 255);
	$userId = max(0, (int) ($payload['user_id'] ?? 0));
	$isReg = !empty($payload['is_registered']) || $userId > 0 ? 1 : 0;
	$now = time();
	$ip = epc_web_tracker_client_ip();
	$geo = epc_web_tracker_geo_from_request();
	$ua = epc_web_tracker_clip((string) ($_SERVER['HTTP_USER_AGENT'] ?? ($payload['ua'] ?? '')), 512);
	$parsedUa = epc_web_tracker_parse_ua($ua);

	$screenW = max(0, min(65535, (int) ($payload['screen_w'] ?? 0)));
	$screenH = max(0, min(65535, (int) ($payload['screen_h'] ?? 0)));
	$language = epc_web_tracker_clip((string) ($payload['language'] ?? ''), 32);
	$timezone = epc_web_tracker_clip((string) ($payload['timezone'] ?? ''), 64);

	$utm = is_array($payload['utm'] ?? null) ? $payload['utm'] : array();
	$utmSource = epc_web_tracker_clip((string) ($utm['source'] ?? ''), 128);
	$utmMedium = epc_web_tracker_clip((string) ($utm['medium'] ?? ''), 128);
	$utmCampaign = epc_web_tracker_clip((string) ($utm['campaign'] ?? ''), 128);
	$utmTerm = epc_web_tracker_clip((string) ($utm['term'] ?? ''), 128);
	$utmContent = epc_web_tracker_clip((string) ($utm['content'] ?? ''), 128);

	$referrer = epc_web_tracker_clip((string) ($payload['referrer'] ?? ''), 1024);
	$referrerHost = '';
	if ($referrer !== '') {
		$p = parse_url($referrer);
		$referrerHost = epc_web_tracker_clip((string) ($p['host'] ?? ''), 255);
	}

	$pageviews = is_array($payload['pageviews'] ?? null) ? $payload['pageviews'] : array();
	$events = is_array($payload['events'] ?? null) ? $payload['events'] : array();
	if (count($pageviews) > 20) {
		$pageviews = array_slice($pageviews, 0, 20);
	}
	if (count($events) > 80) {
		$events = array_slice($events, 0, 80);
	}

	$landingPath = '';
	$landingTitle = '';
	$exitPath = '';
	if ($pageviews) {
		$first = $pageviews[0];
		$last = $pageviews[count($pageviews) - 1];
		$landingPath = epc_web_tracker_clip((string) ($first['path'] ?? ''), 512);
		$landingTitle = epc_web_tracker_clip((string) ($first['title'] ?? ''), 255);
		$exitPath = epc_web_tracker_clip((string) ($last['path'] ?? ''), 512);
	}

	$durationMs = max(0, (int) ($payload['duration_ms'] ?? 0));
	if ($durationMs > 86400000) {
		$durationMs = 86400000;
	}

	$st = $pdo->prepare('SELECT * FROM `epc_web_tracker_sessions` WHERE `site_key` = ? AND `session_uid` = ? LIMIT 1');
	$st->execute(array($siteKey, $sessionUid));
	$row = $st->fetch(PDO::FETCH_ASSOC);

	if ($row) {
		$sessionId = (int) $row['id'];
		$upd = $pdo->prepare(
			'UPDATE `epc_web_tracker_sessions` SET
				`visitor_uid` = IF(? <> \'\', ?, `visitor_uid`),
				`hostname` = IF(? <> \'\', ?, `hostname`),
				`user_id` = IF(? > 0, ?, `user_id`),
				`is_registered` = IF(? > 0, 1, `is_registered`),
				`last_seen_at` = ?,
				`pageview_count` = `pageview_count` + ?,
				`event_count` = `event_count` + ?,
				`duration_ms` = GREATEST(`duration_ms`, ?),
				`exit_path` = IF(? <> \'\', ?, `exit_path`),
				`landing_path` = IF(`landing_path` = \'\' AND ? <> \'\', ?, `landing_path`),
				`landing_title` = IF(`landing_title` = \'\' AND ? <> \'\', ?, `landing_title`),
				`referrer` = IF(`referrer` = \'\' AND ? <> \'\', ?, `referrer`),
				`referrer_host` = IF(`referrer_host` = \'\' AND ? <> \'\', ?, `referrer_host`),
				`utm_source` = IF(`utm_source` = \'\' AND ? <> \'\', ?, `utm_source`),
				`utm_medium` = IF(`utm_medium` = \'\' AND ? <> \'\', ?, `utm_medium`),
				`utm_campaign` = IF(`utm_campaign` = \'\' AND ? <> \'\', ?, `utm_campaign`),
				`utm_term` = IF(`utm_term` = \'\' AND ? <> \'\', ?, `utm_term`),
				`utm_content` = IF(`utm_content` = \'\' AND ? <> \'\', ?, `utm_content`),
				`ip` = IF(? <> \'\', ?, `ip`),
				`country_code` = IF(`country_code` = \'\' AND ? <> \'\', ?, `country_code`),
				`country_name` = IF(`country_name` = \'\' AND ? <> \'\', ?, `country_name`),
				`region` = IF(`region` = \'\' AND ? <> \'\', ?, `region`),
				`city` = IF(`city` = \'\' AND ? <> \'\', ?, `city`),
				`ua` = IF(`ua` = \'\' AND ? <> \'\', ?, `ua`),
				`device_type` = IF(`device_type` = \'\' AND ? <> \'\', ?, `device_type`),
				`browser` = IF(`browser` = \'\' AND ? <> \'\', ?, `browser`),
				`os` = IF(`os` = \'\' AND ? <> \'\', ?, `os`),
				`screen_w` = IF(`screen_w` = 0 AND ? > 0, ?, `screen_w`),
				`screen_h` = IF(`screen_h` = 0 AND ? > 0, ?, `screen_h`),
				`language` = IF(`language` = \'\' AND ? <> \'\', ?, `language`),
				`timezone` = IF(`timezone` = \'\' AND ? <> \'\', ?, `timezone`)
			 WHERE `id` = ?'
		);
		$upd->execute(array(
			$visitorUid, $visitorUid,
			$hostname, $hostname,
			$userId, $userId,
			$userId,
			$now,
			count($pageviews),
			count($events),
			$durationMs,
			$exitPath, $exitPath,
			$landingPath, $landingPath,
			$landingTitle, $landingTitle,
			$referrer, $referrer,
			$referrerHost, $referrerHost,
			$utmSource, $utmSource,
			$utmMedium, $utmMedium,
			$utmCampaign, $utmCampaign,
			$utmTerm, $utmTerm,
			$utmContent, $utmContent,
			$ip, $ip,
			$geo['code'], $geo['code'],
			$geo['name'], $geo['name'],
			$geo['region'], $geo['region'],
			$geo['city'], $geo['city'],
			$ua, $ua,
			$parsedUa['device_type'], $parsedUa['device_type'],
			$parsedUa['browser'], $parsedUa['browser'],
			$parsedUa['os'], $parsedUa['os'],
			$screenW, $screenW,
			$screenH, $screenH,
			$language, $language,
			$timezone, $timezone,
			$sessionId,
		));
	} else {
		$ins = $pdo->prepare(
			'INSERT INTO `epc_web_tracker_sessions` (
				`session_uid`,`visitor_uid`,`site_key`,`hostname`,`user_id`,`is_registered`,
				`first_seen_at`,`last_seen_at`,`pageview_count`,`event_count`,`duration_ms`,
				`landing_path`,`landing_title`,`exit_path`,`referrer`,`referrer_host`,
				`utm_source`,`utm_medium`,`utm_campaign`,`utm_term`,`utm_content`,
				`ip`,`country_code`,`country_name`,`region`,`city`,
				`ua`,`device_type`,`browser`,`os`,`screen_w`,`screen_h`,`language`,`timezone`
			) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
		);
		$ins->execute(array(
			$sessionUid, $visitorUid, $siteKey, $hostname, $userId, $isReg,
			$now, $now, count($pageviews), count($events), $durationMs,
			$landingPath, $landingTitle, $exitPath, $referrer, $referrerHost,
			$utmSource, $utmMedium, $utmCampaign, $utmTerm, $utmContent,
			$ip, $geo['code'], $geo['name'], $geo['region'], $geo['city'],
			$ua, $parsedUa['device_type'], $parsedUa['browser'], $parsedUa['os'],
			$screenW, $screenH, $language, $timezone,
		));
		$sessionId = (int) $pdo->lastInsertId();
	}

	$pvIns = $pdo->prepare(
		'INSERT INTO `epc_web_tracker_pageviews` (
			`session_id`,`session_uid`,`site_key`,`user_id`,`ts`,`path`,`query_string`,`title`,`referrer`,
			`load_time_ms`,`time_on_page_ms`,`scroll_max_pct`,`viewport_w`,`viewport_h`
		) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	);
	$pvCount = 0;
	$lastPvId = 0;
	foreach ($pageviews as $pv) {
		if (!is_array($pv)) {
			continue;
		}
		$path = epc_web_tracker_clip((string) ($pv['path'] ?? ''), 512);
		if ($path === '') {
			continue;
		}
		$ts = (int) ($pv['ts'] ?? $now);
		if ($ts < 1000000000 || $ts > $now + 3600) {
			$ts = $now;
		}
		$pvIns->execute(array(
			$sessionId, $sessionUid, $siteKey, $userId, $ts,
			$path,
			epc_web_tracker_clip((string) ($pv['query'] ?? ''), 1024),
			epc_web_tracker_clip((string) ($pv['title'] ?? ''), 255),
			epc_web_tracker_clip((string) ($pv['referrer'] ?? ''), 1024),
			max(0, min(600000, (int) ($pv['load_time_ms'] ?? 0))),
			max(0, min(86400000, (int) ($pv['time_on_page_ms'] ?? 0))),
			max(0, min(100, (int) ($pv['scroll_max_pct'] ?? 0))),
			max(0, min(65535, (int) ($pv['viewport_w'] ?? 0))),
			max(0, min(65535, (int) ($pv['viewport_h'] ?? 0))),
		));
		$lastPvId = (int) $pdo->lastInsertId();
		$pvCount++;
	}

	$evIns = $pdo->prepare(
		'INSERT INTO `epc_web_tracker_events` (
			`session_id`,`pageview_id`,`session_uid`,`site_key`,`user_id`,`ts`,`event_type`,`path`,
			`x`,`y`,`page_x`,`page_y`,`element_tag`,`element_id`,`element_class`,`element_text`,
			`element_href`,`element_name`,`css_path`,`search_query`,`search_context`,`meta_json`
		) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)'
	);
	$evCount = 0;
	foreach ($events as $ev) {
		if (!is_array($ev)) {
			continue;
		}
		$type = preg_replace('/[^a-z0-9_]/', '', strtolower((string) ($ev['type'] ?? '')));
		if ($type === '' || strlen($type) > 32) {
			continue;
		}
		$ts = (int) ($ev['ts'] ?? $now);
		if ($ts < 1000000000 || $ts > $now + 3600) {
			$ts = $now;
		}
		$meta = null;
		if (isset($ev['meta']) && (is_array($ev['meta']) || is_string($ev['meta']))) {
			$meta = is_string($ev['meta']) ? $ev['meta'] : json_encode($ev['meta'], JSON_UNESCAPED_UNICODE);
			if (is_string($meta) && strlen($meta) > 4000) {
				$meta = substr($meta, 0, 4000);
			}
		}
		$evIns->execute(array(
			$sessionId,
			$lastPvId,
			$sessionUid, $siteKey, $userId, $ts, $type,
			epc_web_tracker_clip((string) ($ev['path'] ?? ''), 512),
			(int) ($ev['x'] ?? 0), (int) ($ev['y'] ?? 0),
			(int) ($ev['page_x'] ?? 0), (int) ($ev['page_y'] ?? 0),
			epc_web_tracker_clip((string) ($ev['tag'] ?? ''), 32),
			epc_web_tracker_clip((string) ($ev['id'] ?? ''), 128),
			epc_web_tracker_clip((string) ($ev['class'] ?? ''), 255),
			epc_web_tracker_clip((string) ($ev['text'] ?? ''), 255),
			epc_web_tracker_clip((string) ($ev['href'] ?? ''), 1024),
			epc_web_tracker_clip((string) ($ev['name'] ?? ''), 128),
			epc_web_tracker_clip((string) ($ev['css'] ?? ''), 512),
			epc_web_tracker_clip((string) ($ev['search'] ?? ''), 512),
			epc_web_tracker_clip((string) ($ev['search_ctx'] ?? ''), 64),
			$meta,
		));
		$evCount++;
	}

	return array('ok' => true, 'session_id' => $sessionId, 'pageviews' => $pvCount, 'events' => $evCount);
}

function epc_web_tracker_resolve_site_key(): string
{
	if (function_exists('epc_portal_site_profile')) {
		$p = epc_portal_site_profile();
		$key = preg_replace('/[^a-z0-9_\-]/', '', strtolower((string) ($p['site_key'] ?? '')));
		if ($key !== '') {
			return $key;
		}
	}
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$host = preg_replace('/:\d+$/', '', $host);
	if ($host === 'www.ecomae.com' || $host === 'ecomae.com' || $host === 'cp.ecomae.com') {
		return 'ecomae';
	}
	if (strpos($host, 'epartscart') !== false) {
		return 'epartscart';
	}
	$key = preg_replace('/^www\./', '', $host);
	$key = preg_replace('/[^a-z0-9]+/', '_', $key);
	return trim($key, '_') ?: 'unknown';
}

/**
 * HTML/JS snippet to inject on storefront + marketing pages.
 */
function epc_web_tracker_beacon_html(): string
{
	$userId = 0;
	if (class_exists('DP_User', false) && method_exists('DP_User', 'getUserId')) {
		try {
			$userId = (int) DP_User::getUserId();
		} catch (Throwable $e) {
			$userId = 0;
		}
	}
	$siteKey = epc_web_tracker_resolve_site_key();
	$host = strtolower((string) ($_SERVER['HTTP_HOST'] ?? ''));
	$cfg = array(
		'endpoint' => '/epc-web-tracker-collect.php',
		'site_key' => $siteKey,
		'hostname' => $host,
		'user_id' => $userId,
		'is_registered' => $userId > 0,
		'v' => '20260718',
	);
	$json = json_encode($cfg, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
	if ($json === false) {
		return '';
	}
	return "\n<script>window.EPC_WEB_TRACKER=" . $json . ';</script>'
		. "\n<script src=\"/content/general_pages/epc_web_tracker.js?v=20260718\" defer></script>\n";
}

/** @return array{from:int,to:int} */
function epc_web_tracker_range_from_request(): array
{
	$to = time();
	$from = $to - 7 * 86400;
	if (!empty($_GET['from'])) {
		$t = strtotime((string) $_GET['from'] . ' 00:00:00');
		if ($t) {
			$from = (int) $t;
		}
	}
	if (!empty($_GET['to'])) {
		$t = strtotime((string) $_GET['to'] . ' 23:59:59');
		if ($t) {
			$to = (int) $t;
		}
	}
	if ($from > $to) {
		$tmp = $from;
		$from = $to;
		$to = $tmp;
	}
	if (($to - $from) > 366 * 86400) {
		$from = $to - 366 * 86400;
	}
	return array('from' => $from, 'to' => $to);
}

/**
 * @return array<string,mixed>
 */
function epc_web_tracker_dashboard(PDO $pdo, string $siteKey, int $from, int $to, bool $allSites = false): array
{
	epc_web_tracker_ensure_schema($pdo);
	$params = array($from, $to);
	$siteSql = '';
	if (!$allSites && $siteKey !== '' && $siteKey !== '_all') {
		$siteSql = ' AND `site_key` = ? ';
		$params[] = $siteKey;
	}

	$summary = array(
		'sessions' => 0,
		'visitors' => 0,
		'pageviews' => 0,
		'events' => 0,
		'clicks' => 0,
		'searches' => 0,
		'registered_sessions' => 0,
		'guest_sessions' => 0,
		'avg_duration_ms' => 0,
		'avg_pages' => 0,
		'bounce_rate' => 0,
	);

	$st = $pdo->prepare(
		'SELECT COUNT(*) AS sessions,
			COUNT(DISTINCT NULLIF(`visitor_uid`,\'\')) AS visitors,
			COALESCE(SUM(`pageview_count`),0) AS pageviews,
			COALESCE(SUM(`event_count`),0) AS events,
			SUM(CASE WHEN `is_registered` = 1 THEN 1 ELSE 0 END) AS registered_sessions,
			SUM(CASE WHEN `is_registered` = 0 THEN 1 ELSE 0 END) AS guest_sessions,
			COALESCE(AVG(`duration_ms`),0) AS avg_duration_ms,
			COALESCE(AVG(`pageview_count`),0) AS avg_pages,
			SUM(CASE WHEN `pageview_count` <= 1 THEN 1 ELSE 0 END) AS bounces
		 FROM `epc_web_tracker_sessions`
		 WHERE `last_seen_at` BETWEEN ? AND ?' . $siteSql
	);
	$st->execute($params);
	$row = $st->fetch(PDO::FETCH_ASSOC) ?: array();
	$sessions = (int) ($row['sessions'] ?? 0);
	$summary['sessions'] = $sessions;
	$summary['visitors'] = (int) ($row['visitors'] ?? 0);
	$summary['pageviews'] = (int) ($row['pageviews'] ?? 0);
	$summary['events'] = (int) ($row['events'] ?? 0);
	$summary['registered_sessions'] = (int) ($row['registered_sessions'] ?? 0);
	$summary['guest_sessions'] = (int) ($row['guest_sessions'] ?? 0);
	$summary['avg_duration_ms'] = (int) round((float) ($row['avg_duration_ms'] ?? 0));
	$summary['avg_pages'] = round((float) ($row['avg_pages'] ?? 0), 2);
	$summary['bounce_rate'] = $sessions > 0
		? round(100 * ((int) ($row['bounces'] ?? 0)) / $sessions, 1)
		: 0;

	$evParams = array($from, $to);
	$evSite = '';
	if (!$allSites && $siteKey !== '' && $siteKey !== '_all') {
		$evSite = ' AND `site_key` = ? ';
		$evParams[] = $siteKey;
	}
	$st = $pdo->prepare(
		'SELECT
			SUM(CASE WHEN `event_type` = \'click\' THEN 1 ELSE 0 END) AS clicks,
			SUM(CASE WHEN `event_type` = \'search\' THEN 1 ELSE 0 END) AS searches
		 FROM `epc_web_tracker_events`
		 WHERE `ts` BETWEEN ? AND ?' . $evSite
	);
	$st->execute($evParams);
	$er = $st->fetch(PDO::FETCH_ASSOC) ?: array();
	$summary['clicks'] = (int) ($er['clicks'] ?? 0);
	$summary['searches'] = (int) ($er['searches'] ?? 0);

	$daily = array();
	$st = $pdo->prepare(
		'SELECT FROM_UNIXTIME(`last_seen_at`, \'%Y-%m-%d\') AS d,
			COUNT(*) AS sessions,
			COALESCE(SUM(`pageview_count`),0) AS pageviews
		 FROM `epc_web_tracker_sessions`
		 WHERE `last_seen_at` BETWEEN ? AND ?' . $siteSql . '
		 GROUP BY d ORDER BY d ASC'
	);
	$st->execute($params);
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$daily[] = array(
			'date' => (string) $r['d'],
			'sessions' => (int) $r['sessions'],
			'pageviews' => (int) $r['pageviews'],
		);
	}

	$topPages = array();
	$st = $pdo->prepare(
		'SELECT `path`, COUNT(*) AS views, COUNT(DISTINCT `session_uid`) AS sessions,
			ROUND(AVG(`time_on_page_ms`)) AS avg_time_ms,
			ROUND(AVG(`scroll_max_pct`)) AS avg_scroll
		 FROM `epc_web_tracker_pageviews`
		 WHERE `ts` BETWEEN ? AND ?' . $evSite . '
		 GROUP BY `path` ORDER BY views DESC LIMIT 40'
	);
	$st->execute($evParams);
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$topPages[] = $r;
	}

	$geo = array();
	$st = $pdo->prepare(
		'SELECT `country_code`, `country_name`, `city`, COUNT(*) AS sessions
		 FROM `epc_web_tracker_sessions`
		 WHERE `last_seen_at` BETWEEN ? AND ?' . $siteSql . '
		 GROUP BY `country_code`, `country_name`, `city`
		 ORDER BY sessions DESC LIMIT 40'
	);
	$st->execute($params);
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$geo[] = $r;
	}

	$devices = array();
	$st = $pdo->prepare(
		'SELECT `device_type`, `browser`, `os`, COUNT(*) AS sessions
		 FROM `epc_web_tracker_sessions`
		 WHERE `last_seen_at` BETWEEN ? AND ?' . $siteSql . '
		 GROUP BY `device_type`, `browser`, `os`
		 ORDER BY sessions DESC LIMIT 30'
	);
	$st->execute($params);
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$devices[] = $r;
	}

	$searches = array();
	$st = $pdo->prepare(
		'SELECT `search_query`, `search_context`, COUNT(*) AS hits, COUNT(DISTINCT `session_uid`) AS sessions
		 FROM `epc_web_tracker_events`
		 WHERE `ts` BETWEEN ? AND ? AND `event_type` = \'search\' AND `search_query` <> \'\'' . $evSite . '
		 GROUP BY `search_query`, `search_context`
		 ORDER BY hits DESC LIMIT 50'
	);
	$st->execute($evParams);
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$searches[] = $r;
	}

	$topClicks = array();
	$st = $pdo->prepare(
		'SELECT `path`, `element_tag`, `element_id`, `element_text`, `element_href`, COUNT(*) AS hits
		 FROM `epc_web_tracker_events`
		 WHERE `ts` BETWEEN ? AND ? AND `event_type` = \'click\'' . $evSite . '
		 GROUP BY `path`, `element_tag`, `element_id`, `element_text`, `element_href`
		 ORDER BY hits DESC LIMIT 50'
	);
	$st->execute($evParams);
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$topClicks[] = $r;
	}

	$referrers = array();
	$st = $pdo->prepare(
		'SELECT IF(`referrer_host`=\'\', \'(direct)\', `referrer_host`) AS host,
			`utm_source`, `utm_medium`, `utm_campaign`, COUNT(*) AS sessions
		 FROM `epc_web_tracker_sessions`
		 WHERE `last_seen_at` BETWEEN ? AND ?' . $siteSql . '
		 GROUP BY host, `utm_source`, `utm_medium`, `utm_campaign`
		 ORDER BY sessions DESC LIMIT 40'
	);
	$st->execute($params);
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$referrers[] = $r;
	}

	$recent = array();
	$st = $pdo->prepare(
		'SELECT `id`, `session_uid`, `site_key`, `hostname`, `user_id`, `is_registered`,
			`first_seen_at`, `last_seen_at`, `pageview_count`, `event_count`, `duration_ms`,
			`landing_path`, `exit_path`, `country_code`, `country_name`, `city`, `region`,
			`device_type`, `browser`, `os`, `ip`, `referrer_host`, `utm_source`
		 FROM `epc_web_tracker_sessions`
		 WHERE `last_seen_at` BETWEEN ? AND ?' . $siteSql . '
		 ORDER BY `last_seen_at` DESC LIMIT 60'
	);
	$st->execute($params);
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$recent[] = $r;
	}

	$byTenant = array();
	if ($allSites || $siteKey === '_all') {
		$st = $pdo->prepare(
			'SELECT `site_key`, `hostname`, COUNT(*) AS sessions,
				COALESCE(SUM(`pageview_count`),0) AS pageviews,
				COUNT(DISTINCT NULLIF(`visitor_uid`,\'\')) AS visitors
			 FROM `epc_web_tracker_sessions`
			 WHERE `last_seen_at` BETWEEN ? AND ?
			 GROUP BY `site_key`, `hostname`
			 ORDER BY sessions DESC LIMIT 100'
		);
		$st->execute(array($from, $to));
		while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
			$byTenant[] = $r;
		}
	}

	return array(
		'summary' => $summary,
		'daily' => $daily,
		'top_pages' => $topPages,
		'geo' => $geo,
		'devices' => $devices,
		'searches' => $searches,
		'top_clicks' => $topClicks,
		'referrers' => $referrers,
		'recent_sessions' => $recent,
		'by_tenant' => $byTenant,
	);
}

/**
 * @return array{session:?array,pageviews:array,events:array}
 */
function epc_web_tracker_session_detail(PDO $pdo, int $sessionId, string $allowedSiteKey = '', bool $allSites = false): array
{
	epc_web_tracker_ensure_schema($pdo);
	$st = $pdo->prepare('SELECT * FROM `epc_web_tracker_sessions` WHERE `id` = ? LIMIT 1');
	$st->execute(array($sessionId));
	$session = $st->fetch(PDO::FETCH_ASSOC) ?: null;
	if (!$session) {
		return array('session' => null, 'pageviews' => array(), 'events' => array());
	}
	if (!$allSites && $allowedSiteKey !== '' && (string) $session['site_key'] !== $allowedSiteKey) {
		return array('session' => null, 'pageviews' => array(), 'events' => array());
	}
	$st = $pdo->prepare(
		'SELECT * FROM `epc_web_tracker_pageviews` WHERE `session_id` = ? ORDER BY `ts` ASC, `id` ASC LIMIT 500'
	);
	$st->execute(array($sessionId));
	$pageviews = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	$st = $pdo->prepare(
		'SELECT * FROM `epc_web_tracker_events` WHERE `session_id` = ? ORDER BY `ts` ASC, `id` ASC LIMIT 2000'
	);
	$st->execute(array($sessionId));
	$events = $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
	return array('session' => $session, 'pageviews' => $pageviews, 'events' => $events);
}

function epc_web_tracker_format_duration(int $ms): string
{
	if ($ms < 1000) {
		return $ms . ' ms';
	}
	$s = (int) round($ms / 1000);
	if ($s < 60) {
		return $s . 's';
	}
	$m = intdiv($s, 60);
	$rs = $s % 60;
	if ($m < 60) {
		return $m . 'm ' . $rs . 's';
	}
	$h = intdiv($m, 60);
	$rm = $m % 60;
	return $h . 'h ' . $rm . 'm';
}
