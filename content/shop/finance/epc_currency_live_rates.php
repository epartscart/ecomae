<?php
/**
 * Live FX rates for shop_currencies — relative to main shop currency.
 *
 * Sources (no paid XE key required):
 *  1) open.er-api.com — ExchangeRate-API free open access (documented provider)
 *  2) api.exchangerate-api.com/v4 — same provider, alternate endpoint
 *  3) floatrates.com — daily JSON with inverseRate (base per 1 foreign)
 *
 * Shop rate model: foreign_price * rate = amount in main currency
 * (rate = units of main currency per 1 unit of foreign currency).
 */
declare(strict_types=1);

if (!function_exists('epc_currency_live_http_get')) {
	/**
	 * @return array{ok:bool,body:string,error:string,http:int}
	 */
	function epc_currency_live_http_get(string $url, int $timeout = 12): array
	{
		$timeout = max(3, min(30, $timeout));
		$body = '';
		$http = 0;
		$error = '';

		if (function_exists('curl_init')) {
			$ch = curl_init($url);
			curl_setopt_array($ch, array(
				CURLOPT_RETURNTRANSFER => true,
				CURLOPT_FOLLOWLOCATION => true,
				CURLOPT_CONNECTTIMEOUT => $timeout,
				CURLOPT_TIMEOUT => $timeout,
				CURLOPT_USERAGENT => 'EPartsCart-CurrencyLive/1.0',
				CURLOPT_HTTPHEADER => array('Accept: application/json'),
				CURLOPT_SSL_VERIFYPEER => true,
			));
			$raw = curl_exec($ch);
			$http = (int) curl_getinfo($ch, CURLINFO_HTTP_CODE);
			if ($raw === false) {
				$error = (string) curl_error($ch);
			} else {
				$body = (string) $raw;
			}
			curl_close($ch);
			if ($body !== '' && $http >= 200 && $http < 300) {
				return array('ok' => true, 'body' => $body, 'error' => '', 'http' => $http);
			}
			if ($error === '' && $http > 0) {
				$error = 'HTTP ' . $http;
			}
		}

		$ctx = stream_context_create(array(
			'http' => array(
				'timeout' => $timeout,
				'header' => "Accept: application/json\r\nUser-Agent: EPartsCart-CurrencyLive/1.0\r\n",
				'ignore_errors' => true,
			),
			'ssl' => array('verify_peer' => true, 'verify_peer_name' => true),
		));
		$raw = @file_get_contents($url, false, $ctx);
		if (is_string($raw) && $raw !== '') {
			return array('ok' => true, 'body' => $raw, 'error' => '', 'http' => $http > 0 ? $http : 200);
		}
		if ($error === '') {
			$error = 'Request failed';
		}
		return array('ok' => false, 'body' => '', 'error' => $error, 'http' => $http);
	}

	/**
	 * Ensure optional audit columns on shop_currencies.
	 */
	function epc_currency_live_ensure_schema(PDO $db): void
	{
		try {
			$cols = array();
			$st = $db->query('SHOW COLUMNS FROM `shop_currencies`');
			while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
				$cols[strtolower((string) ($r['Field'] ?? ''))] = true;
			}
			if (empty($cols['rate_source'])) {
				$db->exec("ALTER TABLE `shop_currencies` ADD COLUMN `rate_source` VARCHAR(64) NOT NULL DEFAULT '' AFTER `rate`");
			}
			if (empty($cols['rate_updated_at'])) {
				$db->exec("ALTER TABLE `shop_currencies` ADD COLUMN `rate_updated_at` INT UNSIGNED NOT NULL DEFAULT 0 AFTER `rate_source`");
			}
		} catch (Throwable $e) {
			// Non-fatal: live rates still work without audit columns.
		}
	}

	/**
	 * Resolve main shop currency ISO alpha (e.g. AED) from config numeric/alpha code.
	 */
	function epc_currency_live_main_alpha(PDO $db, $DP_Config): string
	{
		$code = trim((string) ($DP_Config->shop_currency ?? ''));
		if ($code === '') {
			return 'AED';
		}
		if (preg_match('/^[A-Za-z]{3}$/', $code)) {
			return strtoupper($code);
		}
		$st = $db->prepare('SELECT `iso_name` FROM `shop_currencies` WHERE `iso_code` = ? LIMIT 1');
		$st->execute(array($code));
		$alpha = strtoupper(trim((string) $st->fetchColumn()));
		return $alpha !== '' ? $alpha : 'AED';
	}

	/**
	 * @return array{ok:bool,base:string,date:string,provider:string,rates:array<string,float>,error:string}
	 */
	function epc_currency_live_fetch_bundle(string $baseAlpha): array
	{
		$baseAlpha = strtoupper(preg_replace('/[^A-Z]/', '', $baseAlpha) ?? '');
		if (strlen($baseAlpha) !== 3) {
			return array('ok' => false, 'base' => '', 'date' => '', 'provider' => '', 'rates' => array(), 'error' => 'Invalid base currency');
		}

		$providers = array(
			array(
				'name' => 'ExchangeRate-API (open.er-api.com)',
				'url' => 'https://open.er-api.com/v6/latest/' . rawurlencode($baseAlpha),
				'parse' => 'erapi_v6',
			),
			array(
				'name' => 'ExchangeRate-API v4',
				'url' => 'https://api.exchangerate-api.com/v4/latest/' . rawurlencode($baseAlpha),
				'parse' => 'erapi_v4',
			),
			array(
				'name' => 'FloatRates',
				'url' => 'https://www.floatrates.com/daily/' . strtolower($baseAlpha) . '.json',
				'parse' => 'floatrates',
			),
		);

		$errors = array();
		foreach ($providers as $p) {
			$res = epc_currency_live_http_get($p['url']);
			if (!$res['ok']) {
				$errors[] = $p['name'] . ': ' . $res['error'];
				continue;
			}
			$json = json_decode($res['body'], true);
			if (!is_array($json)) {
				$errors[] = $p['name'] . ': invalid JSON';
				continue;
			}
			$parsed = epc_currency_live_parse_provider($p['parse'], $baseAlpha, $json);
			if (!$parsed['ok']) {
				$errors[] = $p['name'] . ': ' . $parsed['error'];
				continue;
			}
			$parsed['provider'] = $p['name'];
			return $parsed;
		}

		return array(
			'ok' => false,
			'base' => $baseAlpha,
			'date' => '',
			'provider' => '',
			'rates' => array(),
			'error' => 'All FX providers failed. ' . implode(' | ', $errors),
		);
	}

	/**
	 * Normalize provider JSON into rates where value = foreign units per 1 base
	 * (API mid style). Shop rate = 1 / that value.
	 *
	 * @param array<string,mixed> $json
	 * @return array{ok:bool,base:string,date:string,provider:string,rates:array<string,float>,error:string}
	 */
	function epc_currency_live_parse_provider(string $kind, string $baseAlpha, array $json): array
	{
		$empty = array('ok' => false, 'base' => $baseAlpha, 'date' => '', 'provider' => '', 'rates' => array(), 'error' => '');
		$ratesOut = array($baseAlpha => 1.0);
		$date = '';

		if ($kind === 'erapi_v6') {
			if (($json['result'] ?? '') !== 'success' || empty($json['rates']) || !is_array($json['rates'])) {
				$empty['error'] = 'unexpected payload';
				return $empty;
			}
			$date = (string) ($json['time_last_update_utc'] ?? ($json['time_last_update_unix'] ?? ''));
			foreach ($json['rates'] as $code => $val) {
				$code = strtoupper((string) $code);
				$v = (float) $val;
				if ($code === '' || $v <= 0) {
					continue;
				}
				$ratesOut[$code] = $v;
			}
		} elseif ($kind === 'erapi_v4') {
			if (empty($json['rates']) || !is_array($json['rates'])) {
				$empty['error'] = 'unexpected payload';
				return $empty;
			}
			$date = (string) ($json['date'] ?? '');
			foreach ($json['rates'] as $code => $val) {
				$code = strtoupper((string) $code);
				$v = (float) $val;
				if ($code === '' || $v <= 0) {
					continue;
				}
				$ratesOut[$code] = $v;
			}
		} elseif ($kind === 'floatrates') {
			// Each entry: rate = foreign per 1 base, inverseRate = base per 1 foreign
			foreach ($json as $row) {
				if (!is_array($row)) {
					continue;
				}
				$code = strtoupper((string) ($row['code'] ?? $row['alphaCode'] ?? ''));
				$rate = (float) ($row['rate'] ?? 0);
				if ($code === '' || $rate <= 0) {
					continue;
				}
				$ratesOut[$code] = $rate;
				if ($date === '' && !empty($row['date'])) {
					$date = (string) $row['date'];
				}
			}
			$ratesOut[$baseAlpha] = 1.0;
		} else {
			$empty['error'] = 'unknown parser';
			return $empty;
		}

		if (count($ratesOut) < 2) {
			$empty['error'] = 'no rates in response';
			return $empty;
		}

		return array(
			'ok' => true,
			'base' => $baseAlpha,
			'date' => $date,
			'provider' => '',
			'rates' => $ratesOut,
			'error' => '',
		);
	}

	/**
	 * Convert API "foreign per 1 base" map into shop rates "base per 1 foreign".
	 *
	 * @param array<string,float> $apiRates
	 * @return array<string,float> keyed by ISO alpha
	 */
	function epc_currency_live_to_shop_rates(array $apiRates, string $baseAlpha): array
	{
		$baseAlpha = strtoupper($baseAlpha);
		$out = array();
		foreach ($apiRates as $code => $foreignPerBase) {
			$code = strtoupper((string) $code);
			$foreignPerBase = (float) $foreignPerBase;
			if ($code === $baseAlpha) {
				$out[$code] = 1.0;
				continue;
			}
			if ($foreignPerBase <= 0) {
				continue;
			}
			$out[$code] = round(1.0 / $foreignPerBase, 6);
		}
		$out[$baseAlpha] = 1.0;
		return $out;
	}

	/**
	 * Build preview rows for all shop_currencies against live rates.
	 *
	 * @return array{
	 *   ok:bool,
	 *   error:string,
	 *   base_iso_code:string,
	 *   base_alpha:string,
	 *   provider:string,
	 *   as_of:string,
	 *   fetched_at:int,
	 *   rows:array<int,array<string,mixed>>
	 * }
	 */
	function epc_currency_live_preview(PDO $db, $DP_Config): array
	{
		epc_currency_live_ensure_schema($db);
		$baseIso = (string) ($DP_Config->shop_currency ?? '');
		$baseAlpha = epc_currency_live_main_alpha($db, $DP_Config);
		$bundle = epc_currency_live_fetch_bundle($baseAlpha);
		if (!$bundle['ok']) {
			return array(
				'ok' => false,
				'error' => $bundle['error'],
				'base_iso_code' => $baseIso,
				'base_alpha' => $baseAlpha,
				'provider' => '',
				'as_of' => '',
				'fetched_at' => time(),
				'rows' => array(),
			);
		}
		$shopRates = epc_currency_live_to_shop_rates($bundle['rates'], $baseAlpha);
		$rows = array();
		$st = $db->query('SELECT `id`, `iso_code`, `iso_name`, `caption_short`, `rate`, `available`, `order` FROM `shop_currencies` ORDER BY `order`, `iso_name`');
		while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
			$alpha = strtoupper(trim((string) ($r['iso_name'] ?? '')));
			$iso = (string) ($r['iso_code'] ?? '');
			$current = (float) ($r['rate'] ?? 0);
			$isMain = ((string) $iso === (string) $baseIso) || ($alpha === $baseAlpha);
			$live = null;
			$hasLive = false;
			if ($isMain) {
				$live = 1.0;
				$hasLive = true;
			} elseif ($alpha !== '' && isset($shopRates[$alpha])) {
				$live = (float) $shopRates[$alpha];
				$hasLive = true;
			}
			$diffPct = null;
			if ($hasLive && $current > 0 && $live !== null) {
				$diffPct = round((($live - $current) / $current) * 100, 3);
			}
			$rows[] = array(
				'id' => (int) ($r['id'] ?? 0),
				'iso_code' => $iso,
				'iso_name' => $alpha,
				'caption' => (string) ($r['caption_short'] ?? $alpha),
				'available' => (int) ($r['available'] ?? 0),
				'is_main' => $isMain ? 1 : 0,
				'current_rate' => $current,
				'live_rate' => $hasLive ? $live : null,
				'has_live' => $hasLive ? 1 : 0,
				'diff_pct' => $diffPct,
			);
		}

		return array(
			'ok' => true,
			'error' => '',
			'base_iso_code' => $baseIso,
			'base_alpha' => $baseAlpha,
			'provider' => (string) $bundle['provider'],
			'as_of' => (string) $bundle['date'],
			'fetched_at' => time(),
			'rows' => $rows,
		);
	}

	/**
	 * Apply live rates into shop_currencies (main stays 1).
	 *
	 * @param array<int,string>|null $onlyIsoCodes optional filter of iso_code values
	 * @return array{ok:bool,error:string,updated:int,skipped:int,provider:string,as_of:string,rows:array}
	 */
	function epc_currency_live_apply(PDO $db, $DP_Config, ?array $onlyIsoCodes = null): array
	{
		$preview = epc_currency_live_preview($db, $DP_Config);
		if (!$preview['ok']) {
			return array(
				'ok' => false,
				'error' => $preview['error'],
				'updated' => 0,
				'skipped' => 0,
				'provider' => '',
				'as_of' => '',
				'rows' => array(),
			);
		}

		$allow = null;
		if (is_array($onlyIsoCodes) && count($onlyIsoCodes) > 0) {
			$allow = array();
			foreach ($onlyIsoCodes as $c) {
				$allow[(string) $c] = true;
			}
		}

		$updated = 0;
		$skipped = 0;
		$now = time();
		$provider = (string) $preview['provider'];
		$hasSource = false;
		$hasUpdated = false;
		try {
			$cols = array();
			$cst = $db->query('SHOW COLUMNS FROM `shop_currencies`');
			while ($cr = $cst->fetch(PDO::FETCH_ASSOC)) {
				$cols[strtolower((string) ($cr['Field'] ?? ''))] = true;
			}
			$hasSource = !empty($cols['rate_source']);
			$hasUpdated = !empty($cols['rate_updated_at']);
		} catch (Throwable $e) {
		}

		if ($hasSource && $hasUpdated) {
			$upd = $db->prepare('UPDATE `shop_currencies` SET `rate` = ?, `rate_source` = ?, `rate_updated_at` = ? WHERE `iso_code` = ?');
		} elseif ($hasSource) {
			$upd = $db->prepare('UPDATE `shop_currencies` SET `rate` = ?, `rate_source` = ? WHERE `iso_code` = ?');
		} elseif ($hasUpdated) {
			$upd = $db->prepare('UPDATE `shop_currencies` SET `rate` = ?, `rate_updated_at` = ? WHERE `iso_code` = ?');
		} else {
			$upd = $db->prepare('UPDATE `shop_currencies` SET `rate` = ? WHERE `iso_code` = ?');
		}

		$appliedRows = array();
		foreach ($preview['rows'] as $row) {
			$iso = (string) $row['iso_code'];
			if ($allow !== null && empty($allow[$iso])) {
				$skipped++;
				continue;
			}
			if (!empty($row['is_main'])) {
				// Force main = 1
				$rate = 1.0;
			} elseif (empty($row['has_live']) || $row['live_rate'] === null) {
				$skipped++;
				continue;
			} else {
				$rate = (float) $row['live_rate'];
			}
			if ($rate <= 0) {
				$skipped++;
				continue;
			}
			$rate = round($rate, 6);
			if ($hasSource && $hasUpdated) {
				$ok = $upd->execute(array($rate, $provider, $now, $iso));
			} elseif ($hasSource) {
				$ok = $upd->execute(array($rate, $provider, $iso));
			} elseif ($hasUpdated) {
				$ok = $upd->execute(array($rate, $now, $iso));
			} else {
				$ok = $upd->execute(array($rate, $iso));
			}
			if ($ok) {
				$updated++;
				$row['applied_rate'] = $rate;
				$appliedRows[] = $row;
			} else {
				$skipped++;
			}
		}

		return array(
			'ok' => true,
			'error' => '',
			'updated' => $updated,
			'skipped' => $skipped,
			'provider' => $provider,
			'as_of' => (string) $preview['as_of'],
			'rows' => $appliedRows,
		);
	}

	/**
	 * Ensure shared settings table used for FX nightly schedule keys.
	 */
	function epc_currency_live_settings_ensure(PDO $db): void
	{
		try {
			$db->exec(
				"CREATE TABLE IF NOT EXISTS `epc_price_settings` (
					`setting_key` varchar(128) NOT NULL,
					`setting_value` text,
					PRIMARY KEY (`setting_key`)
				) ENGINE=InnoDB DEFAULT CHARSET=utf8"
			);
		} catch (Throwable $e) {
		}
	}

	function epc_currency_live_setting_get(PDO $db, string $key, string $default = ''): string
	{
		try {
			epc_currency_live_settings_ensure($db);
			$st = $db->prepare('SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = ? LIMIT 1');
			$st->execute(array($key));
			$val = $st->fetchColumn();
			return ($val === false || $val === null) ? $default : (string) $val;
		} catch (Throwable $e) {
			return $default;
		}
	}

	function epc_currency_live_setting_set(PDO $db, string $key, string $value): void
	{
		epc_currency_live_settings_ensure($db);
		$db->prepare(
			'INSERT INTO `epc_price_settings` (`setting_key`, `setting_value`)
			 VALUES (?, ?)
			 ON DUPLICATE KEY UPDATE `setting_value` = VALUES(`setting_value`)'
		)->execute(array($key, $value));
	}

	/**
	 * Nightly auto FX schedule (Asia/Dubai by default).
	 *
	 * @return array{
	 *   enabled:int,timezone:string,hour:int,
	 *   last_run_at:int,last_status:string,last_provider:string,last_message:string,
	 *   local_now:string,local_date:string,due:bool,next_window:string
	 * }
	 */
	function epc_currency_live_schedule_get(PDO $db): array
	{
		$enabled = (int) epc_currency_live_setting_get($db, 'fx_live_auto_enabled', '1');
		$tz = trim(epc_currency_live_setting_get($db, 'fx_live_auto_timezone', 'Asia/Dubai'));
		if ($tz === '') {
			$tz = 'Asia/Dubai';
		}
		$hour = (int) epc_currency_live_setting_get($db, 'fx_live_auto_hour', '2');
		if ($hour < 0 || $hour > 23) {
			$hour = 2;
		}
		$lastRun = (int) epc_currency_live_setting_get($db, 'fx_live_auto_last_run_at', '0');
		$lastStatus = epc_currency_live_setting_get($db, 'fx_live_auto_last_status', '');
		$lastProvider = epc_currency_live_setting_get($db, 'fx_live_auto_last_provider', '');
		$lastMessage = epc_currency_live_setting_get($db, 'fx_live_auto_last_message', '');

		$prevTz = date_default_timezone_get();
		try {
			@date_default_timezone_set($tz);
		} catch (Throwable $e) {
			$tz = 'Asia/Dubai';
			@date_default_timezone_set($tz);
		}
		$localNow = date('Y-m-d H:i:s');
		$localDate = date('Y-m-d');
		$localHour = (int) date('G');
		$alreadyToday = false;
		if ($lastRun > 0) {
			$alreadyToday = (date('Y-m-d', $lastRun) === $localDate);
		}
		$due = false;
		if ($enabled === 1) {
			// Nightly window: from configured hour through end of local day, once.
			$due = (!$alreadyToday && $localHour >= $hour);
		}
		$slot = sprintf('%02d:00', $hour) . ' ' . $tz;
		if ($due) {
			$nextWindow = 'due now (' . $localDate . ' ' . $slot . ')';
		} elseif ($localHour < $hour) {
			$nextWindow = $localDate . ' ' . $slot;
		} else {
			$nextWindow = date('Y-m-d', strtotime('+1 day')) . ' ' . $slot;
		}
		@date_default_timezone_set($prevTz);

		return array(
			'enabled' => $enabled ? 1 : 0,
			'timezone' => $tz,
			'hour' => $hour,
			'last_run_at' => $lastRun,
			'last_status' => $lastStatus,
			'last_provider' => $lastProvider,
			'last_message' => $lastMessage,
			'local_now' => $localNow,
			'local_date' => $localDate,
			'due' => $due,
			'next_window' => $nextWindow,
		);
	}

	/**
	 * @param array{enabled?:int|bool,timezone?:string,hour?:int|string} $data
	 * @return array{ok:bool,error:string,schedule:array}
	 */
	function epc_currency_live_schedule_save(PDO $db, array $data): array
	{
		$enabled = !empty($data['enabled']) ? 1 : 0;
		$tz = trim((string) ($data['timezone'] ?? 'Asia/Dubai'));
		if ($tz === '') {
			$tz = 'Asia/Dubai';
		}
		// Validate timezone
		$prevTz = date_default_timezone_get();
		$okTz = @date_default_timezone_set($tz);
		@date_default_timezone_set($prevTz);
		if (!$okTz) {
			return array('ok' => false, 'error' => 'Invalid timezone', 'schedule' => epc_currency_live_schedule_get($db));
		}
		$hour = (int) ($data['hour'] ?? 2);
		if ($hour < 0 || $hour > 23) {
			return array('ok' => false, 'error' => 'Hour must be 0–23', 'schedule' => epc_currency_live_schedule_get($db));
		}
		epc_currency_live_setting_set($db, 'fx_live_auto_enabled', (string) $enabled);
		epc_currency_live_setting_set($db, 'fx_live_auto_timezone', $tz);
		epc_currency_live_setting_set($db, 'fx_live_auto_hour', (string) $hour);
		return array('ok' => true, 'error' => '', 'schedule' => epc_currency_live_schedule_get($db));
	}

	/**
	 * Run scheduled nightly apply when due (or always when $force).
	 *
	 * @return array{ok:bool,ran:bool,skipped:bool,reason:string,updated:int,provider:string,as_of:string,error:string,schedule:array}
	 */
	function epc_currency_live_schedule_tick(PDO $db, $DP_Config, bool $force = false): array
	{
		$schedule = epc_currency_live_schedule_get($db);
		if (!$force) {
			if (empty($schedule['enabled'])) {
				return array(
					'ok' => true,
					'ran' => false,
					'skipped' => true,
					'reason' => 'disabled',
					'updated' => 0,
					'provider' => '',
					'as_of' => '',
					'error' => '',
					'schedule' => $schedule,
				);
			}
			if (empty($schedule['due'])) {
				return array(
					'ok' => true,
					'ran' => false,
					'skipped' => true,
					'reason' => 'not_due',
					'updated' => 0,
					'provider' => '',
					'as_of' => '',
					'error' => '',
					'schedule' => $schedule,
				);
			}
		}

		$out = epc_currency_live_apply($db, $DP_Config, null);
		$now = time();
		if (!$out['ok']) {
			epc_currency_live_setting_set($db, 'fx_live_auto_last_run_at', (string) $now);
			epc_currency_live_setting_set($db, 'fx_live_auto_last_status', 'failed');
			epc_currency_live_setting_set($db, 'fx_live_auto_last_provider', '');
			epc_currency_live_setting_set($db, 'fx_live_auto_last_message', (string) $out['error']);
			$schedule = epc_currency_live_schedule_get($db);
			return array(
				'ok' => false,
				'ran' => true,
				'skipped' => false,
				'reason' => 'apply_failed',
				'updated' => 0,
				'provider' => '',
				'as_of' => '',
				'error' => (string) $out['error'],
				'schedule' => $schedule,
			);
		}

		$msg = 'updated=' . (int) $out['updated'] . ' skipped=' . (int) $out['skipped'];
		epc_currency_live_setting_set($db, 'fx_live_auto_last_run_at', (string) $now);
		epc_currency_live_setting_set($db, 'fx_live_auto_last_status', 'success');
		epc_currency_live_setting_set($db, 'fx_live_auto_last_provider', (string) $out['provider']);
		epc_currency_live_setting_set($db, 'fx_live_auto_last_message', $msg);
		$schedule = epc_currency_live_schedule_get($db);

		return array(
			'ok' => true,
			'ran' => true,
			'skipped' => false,
			'reason' => $force ? 'forced' : 'due',
			'updated' => (int) $out['updated'],
			'provider' => (string) $out['provider'],
			'as_of' => (string) $out['as_of'],
			'error' => '',
			'schedule' => $schedule,
		);
	}
}
