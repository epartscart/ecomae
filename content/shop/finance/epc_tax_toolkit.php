<?php
/**
 * EPC Tax Toolkit — installable jurisdiction kits, tenant profiles, tax resolution.
 * Tax resolves from tenant/site jurisdiction (NOT customer country). UAE via AE-UAE-VAT + epc_uae_vat delegate.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/epc_erp_schema.php';
require_once __DIR__ . '/epc_tax_toolkit_world.php';

function epc_tax_toolkit_h($v): string
{
	return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
}

function epc_tax_toolkit_ensure_schema(PDO $db): void
{
	$db->exec("CREATE TABLE IF NOT EXISTS `epc_tax_toolkits` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`kit_code` varchar(32) NOT NULL,
		`name` varchar(128) NOT NULL DEFAULT '',
		`jurisdiction` varchar(64) NOT NULL DEFAULT '',
		`country_codes_json` text NOT NULL,
		`tax_type` varchar(32) NOT NULL DEFAULT 'vat',
		`rules_json` mediumtext NOT NULL,
		`is_system` tinyint(1) NOT NULL DEFAULT 1,
		`active` tinyint(1) NOT NULL DEFAULT 1,
		`time_created` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_kit_code` (`kit_code`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tax jurisdiction kit catalog';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_tax_toolkit_installs` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`kit_id` int(11) NOT NULL,
		`kit_code` varchar(32) NOT NULL,
		`is_default` tinyint(1) NOT NULL DEFAULT 0,
		`installed_by` int(11) NOT NULL DEFAULT 0,
		`time_installed` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_kit_code` (`kit_code`),
		KEY `x_kit_id` (`kit_id`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Installed tax kits for tenant';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_customer_tax_profile` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`user_id` int(11) DEFAULT NULL,
		`contact_id` int(11) DEFAULT NULL,
		`country_code` varchar(8) NOT NULL DEFAULT 'AE',
		`trade_type` varchar(16) NOT NULL DEFAULT 'retail',
		`installed_kit_id` int(11) NOT NULL DEFAULT 0,
		`kit_code` varchar(32) NOT NULL DEFAULT '',
		`reg_number` varchar(64) NOT NULL DEFAULT '',
		`rate_override` decimal(6,3) DEFAULT NULL,
		`zero_rated` tinyint(1) NOT NULL DEFAULT 0,
		`auto_assigned` tinyint(1) NOT NULL DEFAULT 0,
		`notes` varchar(512) NOT NULL DEFAULT '',
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_user` (`user_id`),
		UNIQUE KEY `x_contact` (`contact_id`),
		KEY `x_kit` (`kit_code`),
		KEY `x_country` (`country_code`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Customer tax toolkit assignment';");

	epc_erp_schema_add_column_if_missing($db, 'epc_customer_tax_profile', 'user_id', 'int(11) DEFAULT NULL');
	epc_erp_schema_add_column_if_missing($db, 'epc_customer_tax_profile', 'contact_id', 'int(11) DEFAULT NULL');
	try {
		$db->exec('ALTER TABLE `epc_customer_tax_profile` MODIFY `user_id` int(11) DEFAULT NULL');
		$db->exec('ALTER TABLE `epc_customer_tax_profile` MODIFY `contact_id` int(11) DEFAULT NULL');
	} catch (Exception $e) {
	}
	$db->exec('UPDATE `epc_customer_tax_profile` SET `user_id` = NULL WHERE `user_id` = 0');
	$db->exec('UPDATE `epc_customer_tax_profile` SET `contact_id` = NULL WHERE `contact_id` = 0');

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_tax_toolkit_tenant_profile` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`site_key` varchar(64) NOT NULL DEFAULT '',
		`country_code` varchar(8) NOT NULL DEFAULT 'AE',
		`kit_code` varchar(32) NOT NULL DEFAULT '',
		`reg_number` varchar(64) NOT NULL DEFAULT '',
		`installed_kit_id` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		UNIQUE KEY `x_site_key` (`site_key`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tenant jurisdiction tax kit (one per site/DB)';");

	$db->exec("CREATE TABLE IF NOT EXISTS `epc_tax_toolkit_updates` (
		`id` int(11) NOT NULL AUTO_INCREMENT,
		`kit_code` varchar(32) NOT NULL DEFAULT '',
		`source` varchar(32) NOT NULL DEFAULT 'seed',
		`changelog` varchar(512) NOT NULL DEFAULT '',
		`rules_hash` varchar(64) NOT NULL DEFAULT '',
		`admin_id` int(11) NOT NULL DEFAULT 0,
		`time_updated` int(11) NOT NULL DEFAULT 0,
		PRIMARY KEY (`id`),
		KEY `x_kit_code` (`kit_code`),
		KEY `x_time` (`time_updated`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='Tax kit refresh history';");
}

function epc_tax_toolkit_eu_country_codes(): array
{
	return array(
		'AT', 'BE', 'BG', 'HR', 'CY', 'CZ', 'DK', 'EE', 'FI', 'FR', 'DE', 'GR', 'HU', 'IE', 'IT',
		'LV', 'LT', 'LU', 'MT', 'NL', 'PL', 'PT', 'RO', 'SK', 'SI', 'ES', 'SE',
	);
}

function epc_tax_toolkit_country_to_kit_code(string $country): string
{
	$cc = strtoupper(trim($country));
	if ($cc === '' || $cc === 'UAE') {
		$cc = 'AE';
	}
	if (strlen($cc) > 3) {
		$iso = epc_tax_toolkit_country_name_to_iso($country);
		if ($iso !== '') {
			$cc = $iso;
		}
	}
	return epc_tax_toolkit_kit_code_for_country($cc);
}

function epc_tax_toolkit_catalog_definitions(): array
{
	return epc_tax_toolkit_world_catalog_definitions();
}

function epc_tax_toolkit_site_key_for_db(PDO $db, string $fallback = ''): string
{
	if ($fallback !== '') {
		return preg_replace('/[^a-z0-9_]/', '', strtolower($fallback));
	}
	if (!empty($GLOBALS['epc_demo_cp_site_key'])) {
		return preg_replace('/[^a-z0-9_]/', '', strtolower((string) $GLOBALS['epc_demo_cp_site_key']));
	}
	if (!empty($GLOBALS['epc_demo_storefront_site_key'])) {
		return preg_replace('/[^a-z0-9_]/', '', strtolower((string) $GLOBALS['epc_demo_storefront_site_key']));
	}
	if (function_exists('epc_portal_host')) {
		$host = strtolower(trim(epc_portal_host()));
		if ($host !== '' && function_exists('epc_portal_load_tenant_by_host')) {
			$row = epc_portal_load_tenant_by_host($host);
			if ($row !== null && !empty($row['site_key'])) {
				return preg_replace('/[^a-z0-9_]/', '', strtolower((string) $row['site_key']));
			}
		}
	}
	return '';
}

function epc_tax_toolkit_read_erp_company_country(PDO $db): string
{
	$pricingFile = __DIR__ . '/../pricing/epc_pricing.php';
	if (!is_readable($pricingFile)) {
		return '';
	}
	require_once $pricingFile;
	if (!function_exists('epc_pricing_get_setting')) {
		return '';
	}
	$cc = strtoupper(trim((string) epc_pricing_get_setting($db, 'company_country_code', '')));
	return ($cc === 'UAE') ? 'AE' : $cc;
}

function epc_tax_toolkit_read_portal_country(PDO $db): string
{
	if (!function_exists('epc_portal_load_site_settings')) {
		return '';
	}
	try {
		$settings = epc_portal_load_site_settings($db);
		$countryName = trim((string) (($settings['contact']['country'] ?? '')));
		if ($countryName === '') {
			return '';
		}
		$iso = epc_tax_toolkit_country_name_to_iso($countryName);
		return $iso !== '' ? $iso : '';
	} catch (Throwable $e) {
		return '';
	}
}

function epc_tax_toolkit_known_tenant_countries(): array
{
	return array(
		'epartscart' => 'AE', 'taxofinca' => 'AE', 'electronicae' => 'AE',
		'stylenlook' => 'AE', 'thejewellerytrend' => 'AE', 'ecomae' => 'AE',
		'platform' => 'AE', 'asap' => 'AE', 'docpart' => 'AE',
	);
}

function epc_tax_toolkit_detect_tenant_country(PDO $db, string $siteKey = ''): string
{
	$profile = epc_tax_toolkit_get_tenant_profile($db, $siteKey);
	if ($profile !== null && !empty($profile['country_code'])) {
		return strtoupper((string) $profile['country_code']);
	}
	$erp = epc_tax_toolkit_read_erp_company_country($db);
	if ($erp !== '' && strlen($erp) >= 2) {
		return $erp;
	}
	$portal = epc_tax_toolkit_read_portal_country($db);
	if ($portal !== '') {
		return $portal;
	}
	if ($siteKey === '') {
		$siteKey = epc_tax_toolkit_site_key_for_db($db);
	}
	$known = epc_tax_toolkit_known_tenant_countries();
	if ($siteKey !== '' && isset($known[$siteKey])) {
		return $known[$siteKey];
	}
	return 'AE';
}

function epc_tax_toolkit_get_tenant_profile(PDO $db, string $siteKey = ''): ?array
{
	epc_tax_toolkit_ensure_schema($db);
	if ($siteKey === '') {
		$siteKey = epc_tax_toolkit_site_key_for_db($db);
	}
	if ($siteKey !== '') {
		$st = $db->prepare('SELECT * FROM `epc_tax_toolkit_tenant_profile` WHERE `site_key` = ? LIMIT 1');
		$st->execute(array($siteKey));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			return $row;
		}
	}
	$st = $db->query('SELECT * FROM `epc_tax_toolkit_tenant_profile` ORDER BY `time_updated` DESC LIMIT 1');
	$row = $st->fetch(PDO::FETCH_ASSOC);
	return $row ?: null;
}

function epc_tax_toolkit_assign_tenant(
	PDO $db,
	string $countryCode = '',
	string $kitCode = '',
	string $regNumber = '',
	string $siteKey = '',
	bool $autoDetected = false
): int {
	epc_tax_toolkit_ensure_schema($db);
	if ($siteKey === '') {
		$siteKey = epc_tax_toolkit_site_key_for_db($db);
	}
	if ($siteKey === '') {
		$siteKey = 'default';
	}
	$countryCode = strtoupper(trim($countryCode));
	if ($countryCode === '') {
		$countryCode = epc_tax_toolkit_detect_tenant_country($db, $siteKey);
	}
	if ($kitCode === '') {
		$kitCode = epc_tax_toolkit_country_to_kit_code($countryCode);
	}
	$kit = epc_tax_toolkit_get_kit($db, $kitCode);
	if (!$kit) {
		epc_tax_toolkit_seed_kits($db);
		$kit = epc_tax_toolkit_get_kit($db, $kitCode);
	}
	if (!$kit) {
		throw new Exception('Kit not in catalog: ' . $kitCode);
	}
	epc_tax_toolkit_install($db, $kitCode, true);
	$installSt = $db->prepare('SELECT `id` FROM `epc_tax_toolkit_installs` WHERE `kit_code` = ? LIMIT 1');
	$installSt->execute(array($kitCode));
	$installId = (int) $installSt->fetchColumn();
	if ($regNumber === '') {
		$regNumber = epc_tax_toolkit_read_erp_company_reg($db);
	}
	$now = time();
	$existing = epc_tax_toolkit_get_tenant_profile($db, $siteKey);
	if ($existing) {
		$db->prepare(
			'UPDATE `epc_tax_toolkit_tenant_profile` SET `country_code`=?, `kit_code`=?, `reg_number`=?, `installed_kit_id`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array($countryCode, $kitCode, $regNumber, $installId, $now, (int) $existing['id']));
		return (int) $existing['id'];
	}
	$db->prepare(
		'INSERT INTO `epc_tax_toolkit_tenant_profile` (`site_key`, `country_code`, `kit_code`, `reg_number`, `installed_kit_id`, `time_updated`)
		 VALUES (?,?,?,?,?,?)'
	)->execute(array($siteKey, $countryCode, $kitCode, $regNumber, $installId, $now));
	return (int) $db->lastInsertId();
}

function epc_tax_toolkit_read_erp_company_reg(PDO $db): string
{
	$pricingFile = __DIR__ . '/../pricing/epc_pricing.php';
	if (!is_readable($pricingFile)) {
		return '';
	}
	require_once $pricingFile;
	if (!function_exists('epc_pricing_get_setting')) {
		return '';
	}
	return trim((string) epc_pricing_get_setting($db, 'company_trn', ''));
}

function epc_tax_toolkit_tenant_context(PDO $db): array
{
	epc_tax_toolkit_ensure_schema($db);
	$siteKey = epc_tax_toolkit_site_key_for_db($db);
	$profile = epc_tax_toolkit_get_tenant_profile($db, $siteKey);
	if ($profile !== null && !empty($profile['kit_code'])) {
		return array(
			'site_key' => (string) ($profile['site_key'] ?? $siteKey),
			'country_code' => strtoupper((string) $profile['country_code']),
			'kit_code' => (string) $profile['kit_code'],
			'reg_number' => (string) ($profile['reg_number'] ?? ''),
			'profile_id' => (int) $profile['id'],
			'source' => 'tenant_profile',
		);
	}
	$country = epc_tax_toolkit_detect_tenant_country($db, $siteKey);
	$kitCode = epc_tax_toolkit_default_kit_code($db);
	if ($kitCode === '' || $kitCode === 'AE-UAE-VAT') {
		$kitCode = epc_tax_toolkit_country_to_kit_code($country);
	}
	return array(
		'site_key' => $siteKey,
		'country_code' => $country,
		'kit_code' => $kitCode,
		'reg_number' => epc_tax_toolkit_read_erp_company_reg($db),
		'profile_id' => 0,
		'source' => 'default_detect',
	);
}

function epc_tax_toolkit_catalog_def_for_code(string $kitCode): ?array
{
	foreach (epc_tax_toolkit_catalog_definitions() as $def) {
		if (($def['kit_code'] ?? '') === $kitCode) {
			return $def;
		}
	}
	return null;
}

function epc_tax_toolkit_log_update(PDO $db, string $kitCode, string $source, string $changelog, array $rules, int $adminId = 0): void
{
	epc_tax_toolkit_ensure_schema($db);
	$hash = md5(json_encode($rules, JSON_UNESCAPED_UNICODE));
	$db->prepare(
		'INSERT INTO `epc_tax_toolkit_updates` (`kit_code`, `source`, `changelog`, `rules_hash`, `admin_id`, `time_updated`)
		 VALUES (?,?,?,?,?,?)'
	)->execute(array($kitCode, $source, mb_substr($changelog, 0, 512), $hash, $adminId, time()));
}

function epc_tax_toolkit_list_updates(PDO $db, string $kitCode = '', int $limit = 10): array
{
	epc_tax_toolkit_ensure_schema($db);
	if ($kitCode !== '') {
		$st = $db->prepare(
			'SELECT * FROM `epc_tax_toolkit_updates` WHERE `kit_code` = ? ORDER BY `time_updated` DESC LIMIT ' . max(1, min(50, $limit))
		);
		$st->execute(array($kitCode));
		return $st->fetchAll(PDO::FETCH_ASSOC);
	}
	return $db->query(
		'SELECT * FROM `epc_tax_toolkit_updates` ORDER BY `time_updated` DESC LIMIT ' . max(1, min(50, $limit))
	)->fetchAll(PDO::FETCH_ASSOC);
}

function epc_tax_toolkit_refresh_kit_rules(PDO $db, string $kitCode, int $adminId = 0, bool $forceFta = false): array
{
	epc_tax_toolkit_ensure_schema($db);
	$def = epc_tax_toolkit_catalog_def_for_code($kitCode);
	if (!$def) {
		throw new Exception('Kit not in seed catalog: ' . $kitCode);
	}
	$rules = $def['rules'];
	$source = 'seed';
	$changelogParts = array('Refreshed from worldwide seed catalog');
	$ftaResult = null;

	if ($kitCode === 'AE-UAE-VAT' || !empty($rules['delegate_uae_vat'])) {
		$uaeFile = __DIR__ . '/epc_uae_tax_compliance.php';
		if (is_readable($uaeFile)) {
			require_once $uaeFile;
			if (function_exists('epc_uae_fta_cron_fetch_legislation')) {
				try {
					$ftaResult = epc_uae_fta_cron_fetch_legislation($db, $forceFta || true);
					$source = 'fta';
					$legCount = (int) ($ftaResult['legislation_count'] ?? 0);
					$newCount = (int) ($ftaResult['new_count'] ?? 0);
					$changelogParts[] = 'FTA legislation sync: ' . $legCount . ' entries';
					if ($newCount > 0) {
						$changelogParts[] = $newCount . ' new since last fetch';
					}
					$rules['fta_last_sync'] = date('Y-m-d H:i:s');
					$rules['fta_legislation_count'] = $legCount;
				} catch (Throwable $e) {
					$changelogParts[] = 'FTA sync skipped: ' . $e->getMessage();
					$source = 'seed+fta_error';
				}
			}
		}
		$rules['last_updated'] = date('Y-m-d');
		$rules['source'] = $source;
	}

	$now = time();
	$db->prepare(
		'UPDATE `epc_tax_toolkits` SET `name`=?, `jurisdiction`=?, `country_codes_json`=?, `tax_type`=?, `rules_json`=?, `active`=1
		 WHERE `kit_code`=?'
	)->execute(array(
		$def['name'],
		$def['jurisdiction'],
		json_encode($def['country_codes'], JSON_UNESCAPED_UNICODE),
		$def['tax_type'],
		json_encode($rules, JSON_UNESCAPED_UNICODE),
		$kitCode,
	));

	$changelog = implode('; ', $changelogParts);
	epc_tax_toolkit_log_update($db, $kitCode, $source, $changelog, $rules, $adminId);

	return array(
		'ok' => true,
		'kit_code' => $kitCode,
		'source' => $source,
		'changelog' => $changelog,
		'last_updated' => $rules['last_updated'] ?? date('Y-m-d'),
		'fta' => $ftaResult,
	);
}

function epc_tax_toolkit_refresh_all_kits(PDO $db, int $adminId = 0): array
{
	epc_tax_toolkit_seed_kits($db);
	$results = array();
	foreach (epc_tax_toolkit_catalog_definitions() as $def) {
		$code = (string) ($def['kit_code'] ?? '');
		if ($code === '') {
			continue;
		}
		try {
			$results[$code] = epc_tax_toolkit_refresh_kit_rules($db, $code, $adminId, $code === 'AE-UAE-VAT');
		} catch (Throwable $e) {
			$results[$code] = array('ok' => false, 'error' => $e->getMessage());
		}
	}
	return $results;
}

function epc_tax_toolkit_calc_cit_estimate(PDO $db, float $taxableProfit, string $kitCode = ''): array
{
	if ($kitCode === '') {
		$kitCode = epc_tax_toolkit_tenant_context($db)['kit_code'] ?? '';
	}
	$kit = epc_tax_toolkit_get_kit($db, $kitCode);
	$rules = $kit['rules'] ?? array();
	$cit = $rules['direct']['corporate_tax'] ?? array();
	$rate = isset($cit['rate']) ? (float) $cit['rate'] : null;
	$threshold = (float) ($cit['threshold'] ?? $cit['threshold_aed'] ?? 0);
	$taxableProfit = round(max(0, $taxableProfit), 2);
	$applicableProfit = $threshold > 0 ? max(0, $taxableProfit - $threshold) : $taxableProfit;
	$citAmount = ($rate !== null && $rate > 0) ? round($applicableProfit * $rate / 100, 2) : 0.0;
	return array(
		'kit_code' => $kitCode,
		'taxable_profit' => $taxableProfit,
		'threshold' => $threshold,
		'taxable_above_threshold' => $applicableProfit,
		'cit_rate' => $rate,
		'cit_estimate' => $citAmount,
		'notes' => (string) ($cit['notes'] ?? ''),
		'erp_note' => 'CIT estimate for reporting — not posted to GL automatically',
	);
}

function epc_tax_toolkit_purchase_amounts(PDO $db, float $amountEx, int $supplierId = 0, array $flags = array()): array
{
	$amountEx = round(max(0, $amountEx), 2);
	$ctx = epc_tax_toolkit_resolve($db, 0, 0, $flags);
	$rules = $ctx['rules'] ?? array();
	$hooks = $rules['erp_hooks'] ?? array();
	$importDutyRate = (float) ($rules['trade']['import_duty_default'] ?? $rules['import_rules']['import_duty_rate'] ?? 0);
	$isImport = !empty($flags['import']) || !empty($flags['cross_border']);
	$importDuty = ($isImport && !empty($hooks['import_duty_on_cost']) && $importDutyRate > 0)
		? round($amountEx * $importDutyRate / 100, 2) : 0.0;

	if (!empty($rules['delegate_uae_vat']) && $supplierId > 0) {
		require_once __DIR__ . '/epc_uae_vat.php';
		$vatCalc = epc_uae_vat_purchase_amounts($db, $supplierId, $amountEx);
		return array_merge($vatCalc, array(
			'import_duty' => $importDuty,
			'landed_cost_ex' => round($amountEx + $importDuty, 2),
			'total_with_duty' => round((float) ($vatCalc['total_amount'] ?? $amountEx) + $importDuty, 2),
			'tax_context' => 'uae_delegate_purchase',
			'erp_hooks' => $hooks,
		));
	}

	$rate = (float) $ctx['tax_rate'];
	$recoverable = ($hooks['purchase_inventory'] ?? '') === 'vat_on_purchase_recoverable';
	$vatAmount = ($rate > 0 && $recoverable) ? round($amountEx * $rate / 100, 2) : 0.0;
	return array(
		'amount_ex_vat' => $amountEx,
		'vat_amount' => $vatAmount,
		'tax_amount' => $vatAmount,
		'total_amount' => round($amountEx + $vatAmount, 2),
		'vat_applicable' => $recoverable && $rate > 0,
		'tax_rate' => $rate,
		'import_duty' => $importDuty,
		'landed_cost_ex' => round($amountEx + $importDuty, 2),
		'total_with_duty' => round($amountEx + $vatAmount + $importDuty, 2),
		'tax_context' => 'tenant_toolkit_purchase',
		'kit_code' => $ctx['kit_code'],
		'erp_hooks' => $hooks,
	);
}

function epc_tax_toolkit_sales_amounts(PDO $db, float $amountEx, int $userId = 0, int $contactId = 0, array $flags = array()): array
{
	$calc = epc_tax_toolkit_calc_amounts($db, $amountEx, $userId, $contactId, $flags);
	$hooks = $calc['context']['rules']['erp_hooks'] ?? array();
	return array_merge($calc, array(
		'erp_hooks' => $hooks,
		'tax_context' => 'tenant_toolkit_sales',
	));
}

function epc_tax_toolkit_seed_kits(PDO $db): int
{
	epc_tax_toolkit_ensure_schema($db);
	$now = time();
	$n = 0;
	$ins = $db->prepare(
		'INSERT INTO `epc_tax_toolkits` (`kit_code`, `name`, `jurisdiction`, `country_codes_json`, `tax_type`, `rules_json`, `is_system`, `active`, `time_created`)
		 VALUES (?,?,?,?,?,?,1,1,?)
		 ON DUPLICATE KEY UPDATE `name`=VALUES(`name`), `jurisdiction`=VALUES(`jurisdiction`), `country_codes_json`=VALUES(`country_codes_json`),
		 `tax_type`=VALUES(`tax_type`), `rules_json`=VALUES(`rules_json`), `active`=1'
	);
	foreach (epc_tax_toolkit_catalog_definitions() as $def) {
		$ins->execute(array(
			$def['kit_code'],
			$def['name'],
			$def['jurisdiction'],
			json_encode($def['country_codes'], JSON_UNESCAPED_UNICODE),
			$def['tax_type'],
			json_encode($def['rules'], JSON_UNESCAPED_UNICODE),
			$now,
		));
		$n++;
	}
	return $n;
}

function epc_tax_toolkit_get_kit(PDO $db, string $kitCode): ?array
{
	epc_tax_toolkit_ensure_schema($db);
	$st = $db->prepare('SELECT * FROM `epc_tax_toolkits` WHERE `kit_code` = ? AND `active` = 1 LIMIT 1');
	$st->execute(array($kitCode));
	$row = $st->fetch(PDO::FETCH_ASSOC);
	if (!$row) {
		return null;
	}
	$row['country_codes'] = json_decode((string) $row['country_codes_json'], true) ?: array();
	$row['rules'] = json_decode((string) $row['rules_json'], true) ?: array();
	return $row;
}

function epc_tax_toolkit_list_catalog(PDO $db): array
{
	epc_tax_toolkit_ensure_schema($db);
	$rows = $db->query('SELECT * FROM `epc_tax_toolkits` WHERE `active` = 1 ORDER BY `kit_code`')->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$r) {
		$r['country_codes'] = json_decode((string) $r['country_codes_json'], true) ?: array();
		$r['rules'] = json_decode((string) $r['rules_json'], true) ?: array();
	}
	unset($r);
	return $rows;
}

function epc_tax_toolkit_list_installed(PDO $db): array
{
	epc_tax_toolkit_ensure_schema($db);
	$sql = 'SELECT i.*, t.`name`, t.`jurisdiction`, t.`tax_type`, t.`rules_json`, t.`country_codes_json`
		FROM `epc_tax_toolkit_installs` i
		INNER JOIN `epc_tax_toolkits` t ON t.`id` = i.`kit_id`
		ORDER BY i.`is_default` DESC, t.`kit_code`';
	$rows = $db->query($sql)->fetchAll(PDO::FETCH_ASSOC);
	foreach ($rows as &$r) {
		$r['rules'] = json_decode((string) $r['rules_json'], true) ?: array();
		$r['country_codes'] = json_decode((string) $r['country_codes_json'], true) ?: array();
	}
	unset($r);
	return $rows;
}

function epc_tax_toolkit_default_kit_code(PDO $db): string
{
	epc_tax_toolkit_ensure_schema($db);
	$st = $db->query('SELECT `kit_code` FROM `epc_tax_toolkit_installs` WHERE `is_default` = 1 LIMIT 1');
	$code = (string) $st->fetchColumn();
	return $code !== '' ? $code : 'AE-UAE-VAT';
}

function epc_tax_toolkit_install(PDO $db, string $kitCode, bool $setDefault = false, int $adminId = 0): int
{
	epc_tax_toolkit_ensure_schema($db);
	$kit = epc_tax_toolkit_get_kit($db, $kitCode);
	if (!$kit) {
		throw new Exception('Tax kit not found: ' . $kitCode);
	}
	$now = time();
	$chk = $db->prepare('SELECT `id` FROM `epc_tax_toolkit_installs` WHERE `kit_code` = ? LIMIT 1');
	$chk->execute(array($kitCode));
	$installId = (int) $chk->fetchColumn();
	if ($installId <= 0) {
		$db->prepare(
			'INSERT INTO `epc_tax_toolkit_installs` (`kit_id`, `kit_code`, `is_default`, `installed_by`, `time_installed`)
			 VALUES (?,?,?,?,?)'
		)->execute(array((int) $kit['id'], $kitCode, $setDefault ? 1 : 0, $adminId, $now));
		$installId = (int) $db->lastInsertId();
	}
	if ($setDefault) {
		$db->exec('UPDATE `epc_tax_toolkit_installs` SET `is_default` = 0');
		$db->prepare('UPDATE `epc_tax_toolkit_installs` SET `is_default` = 1 WHERE `id` = ?')->execute(array($installId));
	}
	return $installId;
}

function epc_tax_toolkit_install_defaults(PDO $db, int $adminId = 0): array
{
	epc_tax_toolkit_seed_kits($db);
	$tenant = epc_tax_toolkit_tenant_context($db);
	$defaultCode = $tenant['kit_code'] !== '' ? $tenant['kit_code'] : 'AE-UAE-VAT';
	$installed = array();
	$installed[] = epc_tax_toolkit_install($db, $defaultCode, true, $adminId);
	try {
		epc_tax_toolkit_assign_tenant($db, $tenant['country_code'], $defaultCode, $tenant['reg_number'], $tenant['site_key']);
	} catch (Exception $e) {
	}
	return $installed;
}

function epc_tax_toolkit_install_all_kits(PDO $db, int $adminId = 0): int
{
	epc_tax_toolkit_seed_kits($db);
	$n = 0;
	foreach (epc_tax_toolkit_list_catalog($db) as $kit) {
		try {
			epc_tax_toolkit_install($db, (string) $kit['kit_code'], false, $adminId);
			$n++;
		} catch (Exception $e) {
		}
	}
	$tenant = epc_tax_toolkit_tenant_context($db);
	$defaultCode = $tenant['kit_code'] !== '' ? $tenant['kit_code'] : 'AE-UAE-VAT';
	epc_tax_toolkit_install($db, $defaultCode, true, $adminId);
	return $n;
}

function epc_tax_toolkit_read_user_country(PDO $db, int $userId): string
{
	if ($userId <= 0) {
		return 'AE';
	}
	$keys = array('epc_reg_country', 'epc_demand_country', 'country', 'country_code');
	$ph = implode(',', array_fill(0, count($keys), '?'));
	$st = $db->prepare(
		'SELECT `data_key`, `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` IN (' . $ph . ')'
	);
	$st->execute(array_merge(array($userId), $keys));
	$found = array();
	while ($r = $st->fetch(PDO::FETCH_ASSOC)) {
		$found[$r['data_key']] = strtoupper(trim((string) $r['data_value']));
	}
	foreach ($keys as $k) {
		if (!empty($found[$k]) && strlen($found[$k]) >= 2) {
			return $found[$k] === 'UAE' ? 'AE' : $found[$k];
		}
	}
	return 'AE';
}

function epc_tax_toolkit_read_user_trade_type(PDO $db, int $userId): string
{
	if ($userId <= 0) {
		return 'retail';
	}
	$st = $db->prepare('SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ? LIMIT 1');
	$st->execute(array($userId, 'epc_customer_type'));
	$v = strtolower(trim((string) $st->fetchColumn()));
	return in_array($v, array('retail', 'wholesale'), true) ? $v : 'retail';
}

function epc_tax_toolkit_read_user_reg(PDO $db, int $userId): string
{
	if ($userId <= 0) {
		return '';
	}
	$st = $db->prepare(
		'SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` IN (\'epc_reg_trn\', \'trn\') ORDER BY FIELD(`data_key`, \'epc_reg_trn\', \'trn\') LIMIT 1'
	);
	$st->execute(array($userId));
	return trim((string) $st->fetchColumn());
}

function epc_tax_toolkit_get_profile(PDO $db, int $userId = 0, int $contactId = 0): ?array
{
	epc_tax_toolkit_ensure_schema($db);
	if ($userId > 0) {
		$st = $db->prepare('SELECT * FROM `epc_customer_tax_profile` WHERE `user_id` = ? LIMIT 1');
		$st->execute(array($userId));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			return $row;
		}
	}
	if ($contactId > 0) {
		$st = $db->prepare('SELECT * FROM `epc_customer_tax_profile` WHERE `contact_id` = ? LIMIT 1');
		$st->execute(array($contactId));
		$row = $st->fetch(PDO::FETCH_ASSOC);
		if ($row) {
			return $row;
		}
	}
	return null;
}

function epc_tax_toolkit_assign_customer(
	PDO $db,
	int $userId = 0,
	int $contactId = 0,
	string $countryCode = '',
	string $kitCode = '',
	string $tradeType = 'retail',
	string $regNumber = '',
	?float $rateOverride = null,
	bool $zeroRated = false,
	bool $autoAssigned = false,
	string $notes = ''
): int {
	epc_tax_toolkit_ensure_schema($db);
	if ($userId <= 0 && $contactId <= 0) {
		throw new Exception('user_id or contact_id required');
	}
	$userIdParam = $userId > 0 ? $userId : null;
	$contactIdParam = $contactId > 0 ? $contactId : null;
	$countryCode = strtoupper(trim($countryCode));
	if ($countryCode === '' && $userId > 0) {
		$countryCode = epc_tax_toolkit_read_user_country($db, $userId);
	}
	if ($countryCode === '' && $contactId > 0) {
		$st = $db->prepare('SELECT `country_code` FROM `epc_erp_contacts` WHERE `id` = ? LIMIT 1');
		$st->execute(array($contactId));
		$countryCode = strtoupper(trim((string) $st->fetchColumn())) ?: 'AE';
	}
	if ($kitCode === '') {
		$kitCode = epc_tax_toolkit_country_to_kit_code($countryCode);
	}
	$kit = epc_tax_toolkit_get_kit($db, $kitCode);
	if (!$kit) {
		throw new Exception('Kit not in catalog: ' . $kitCode);
	}
	try {
		epc_tax_toolkit_install($db, $kitCode);
	} catch (Exception $e) {
	}
	$installSt = $db->prepare('SELECT `id` FROM `epc_tax_toolkit_installs` WHERE `kit_code` = ? LIMIT 1');
	$installSt->execute(array($kitCode));
	$installId = (int) $installSt->fetchColumn();
	if ($tradeType === '' && $userId > 0) {
		$tradeType = epc_tax_toolkit_read_user_trade_type($db, $userId);
	}
	if (!in_array($tradeType, array('retail', 'wholesale'), true)) {
		$tradeType = 'retail';
	}
	if ($regNumber === '' && $userId > 0) {
		$regNumber = epc_tax_toolkit_read_user_reg($db, $userId);
	}
	$now = time();
	$existing = epc_tax_toolkit_get_profile($db, $userId, $contactId);
	$fields = array(
		'country_code' => $countryCode ?: 'AE',
		'trade_type' => $tradeType,
		'installed_kit_id' => $installId,
		'kit_code' => $kitCode,
		'reg_number' => $regNumber,
		'rate_override' => $rateOverride,
		'zero_rated' => $zeroRated ? 1 : 0,
		'auto_assigned' => $autoAssigned ? 1 : 0,
		'notes' => $notes,
		'time_updated' => $now,
	);
	if ($existing) {
		$db->prepare(
			'UPDATE `epc_customer_tax_profile` SET `country_code`=?, `trade_type`=?, `installed_kit_id`=?, `kit_code`=?, `reg_number`=?,
			 `rate_override`=?, `zero_rated`=?, `auto_assigned`=?, `notes`=?, `time_updated`=? WHERE `id`=?'
		)->execute(array(
			$fields['country_code'], $fields['trade_type'], $fields['installed_kit_id'], $fields['kit_code'],
			$fields['reg_number'], $fields['rate_override'], $fields['zero_rated'], $fields['auto_assigned'],
			$fields['notes'], $fields['time_updated'], (int) $existing['id'],
		));
		return (int) $existing['id'];
	}
	$db->prepare(
		'INSERT INTO `epc_customer_tax_profile` (`user_id`, `contact_id`, `country_code`, `trade_type`, `installed_kit_id`, `kit_code`,
		 `reg_number`, `rate_override`, `zero_rated`, `auto_assigned`, `notes`, `time_updated`)
		 VALUES (?,?,?,?,?,?,?,?,?,?,?,?)'
	)->execute(array(
		$userIdParam, $contactIdParam, $fields['country_code'], $fields['trade_type'], $fields['installed_kit_id'],
		$fields['kit_code'], $fields['reg_number'], $fields['rate_override'], $fields['zero_rated'],
		$fields['auto_assigned'], $fields['notes'], $fields['time_updated'],
	));
	return (int) $db->lastInsertId();
}

function epc_tax_toolkit_suggest_kit(PDO $db, string $countryCode): array
{
	$kitCode = epc_tax_toolkit_country_to_kit_code($countryCode);
	$kit = epc_tax_toolkit_get_kit($db, $kitCode);
	return array(
		'kit_code' => $kitCode,
		'kit_name' => $kit['name'] ?? $kitCode,
		'standard_rate' => (float) ($kit['rules']['standard_rate'] ?? 5),
	);
}

function epc_tax_toolkit_resolve(PDO $db, int $userId = 0, int $contactId = 0, array $flags = array()): array
{
	epc_tax_toolkit_ensure_schema($db);
	$tenant = epc_tax_toolkit_tenant_context($db);
	$country = strtoupper((string) $tenant['country_code']);
	$kitCode = (string) $tenant['kit_code'];
	$tradeType = 'retail';
	$rateOverride = null;
	$zeroRated = !empty($flags['export']) || !empty($flags['zero_rated']);

	if ($userId > 0) {
		$tradeType = epc_tax_toolkit_read_user_trade_type($db, $userId);
	} elseif ($contactId > 0) {
		$st = $db->prepare('SELECT `customer_type` FROM `epc_erp_contacts` WHERE `id` = ? LIMIT 1');
		$st->execute(array($contactId));
		$ct = strtolower(trim((string) $st->fetchColumn()));
		if (in_array($ct, array('retail', 'wholesale'), true)) {
			$tradeType = $ct;
		}
	}

	if ($kitCode === '') {
		$kitCode = epc_tax_toolkit_default_kit_code($db);
	}
	$kit = epc_tax_toolkit_get_kit($db, $kitCode);
	if (!$kit) {
		$kit = epc_tax_toolkit_get_kit($db, 'AE-UAE-VAT');
		$kitCode = 'AE-UAE-VAT';
	}
	$rules = $kit['rules'] ?? array();
	$standardRate = (float) ($rules['standard_rate'] ?? 5.0);
	$taxCategory = 'S';
	$taxRate = $standardRate;
	$reason = 'tenant_jurisdiction';

	if ($rateOverride !== null) {
		$taxRate = $rateOverride;
		$reason = 'rate_override';
	} elseif ($zeroRated) {
		$taxRate = 0.0;
		$taxCategory = 'Z';
		$reason = 'zero_rated_flag';
	} elseif (!empty($rules['delegate_uae_vat'])) {
		require_once __DIR__ . '/epc_uae_tax_compliance.php';
		$buyer = array('buyer_country_code' => $country, 'country_code' => $country);
		$txFlags = array();
		if (!empty($flags['export'])) {
			$txFlags['exports'] = true;
		}
		$cat = epc_uae_vat_supply_tax_category($db, $buyer, $txFlags);
		$taxRate = (float) $cat['tax_rate'];
		$taxCategory = (string) $cat['tax_category'];
		$reason = (string) ($cat['reason'] ?? 'uae_delegate');
	} else {
		$exportRules = $rules['export_rules'] ?? array();
		if (!empty($flags['export']) && !empty($exportRules['zero_rate_export'])) {
			$taxRate = 0.0;
			$taxCategory = 'Z';
			$reason = 'export';
		}
	}

	return array(
		'kit_code' => $kitCode,
		'kit_name' => $kit['name'] ?? $kitCode,
		'jurisdiction' => $kit['jurisdiction'] ?? '',
		'tax_type' => $kit['tax_type'] ?? 'vat',
		'tax_label' => $rules['tax_label'] ?? 'VAT',
		'reg_number_label' => $rules['reg_number_label'] ?? 'Tax ID',
		'country_code' => $country,
		'trade_type' => $tradeType,
		'tax_rate' => round($taxRate, 3),
		'tax_category' => $taxCategory,
		'reason' => $reason,
		'pricing_mode' => $rules['pricing_mode'] ?? 'exclusive',
		'rules' => $rules,
		'erp_hooks' => $rules['erp_hooks'] ?? array(),
		'corporate_tax_rate' => isset($rules['direct']['corporate_tax']['rate']) ? $rules['direct']['corporate_tax']['rate'] : null,
		'import_duty_default' => $rules['trade']['import_duty_default'] ?? null,
		'ftc_available' => !empty($rules['international']['ftc_available']),
		'last_updated' => $rules['last_updated'] ?? null,
		'source' => $rules['source'] ?? 'seed',
		'profile_id' => (int) ($tenant['profile_id'] ?? 0),
		'tenant_site_key' => (string) ($tenant['site_key'] ?? ''),
		'tenant_source' => (string) ($tenant['source'] ?? ''),
	);
}

function epc_tax_toolkit_calc_amounts(PDO $db, float $amountEx, int $userId = 0, int $contactId = 0, array $flags = array()): array
{
	$amountEx = round(max(0, $amountEx), 2);
	$ctx = epc_tax_toolkit_resolve($db, $userId, $contactId, $flags);
	$rate = (float) $ctx['tax_rate'];
	$taxAmount = $rate > 0 ? round($amountEx * $rate / 100, 2) : 0.0;
	return array(
		'amount_ex_vat' => $amountEx,
		'vat_amount' => $taxAmount,
		'tax_amount' => $taxAmount,
		'total_amount' => round($amountEx + $taxAmount, 2),
		'tax_rate' => $rate,
		'tax_category' => $ctx['tax_category'],
		'tax_label' => $ctx['tax_label'],
		'kit_code' => $ctx['kit_code'],
		'kit_name' => $ctx['kit_name'],
		'reason' => $ctx['reason'],
		'context' => $ctx,
	);
}

function epc_tax_toolkit_calc_line(PDO $db, float $lineEx, int $userId = 0, int $contactId = 0, array $flags = array()): array
{
	$calc = epc_tax_toolkit_calc_amounts($db, $lineEx, $userId, $contactId, $flags);
	return array(
		'line_net' => $calc['amount_ex_vat'],
		'tax_rate' => $calc['tax_rate'],
		'tax_amount' => $calc['tax_amount'],
		'vat_line_aed' => $calc['tax_amount'],
		'gross_amount' => $calc['total_amount'],
		'line_amount_aed' => $calc['total_amount'],
		'tax_category' => $calc['tax_category'],
		'kit_code' => $calc['kit_code'],
	);
}

function epc_tax_toolkit_migrate_tenant(PDO $db, string $siteKey = '', string $host = ''): array
{
	epc_tax_toolkit_ensure_schema($db);
	epc_tax_toolkit_seed_kits($db);
	if ($siteKey === '' && $host !== '') {
		$hostNorm = preg_replace('/^www\./', '', strtolower(trim($host)));
		foreach (epc_tax_toolkit_known_tenant_countries() as $key => $cc) {
			if ($hostNorm !== '' && strpos($hostNorm, str_replace('_', '', $key)) !== false) {
				$siteKey = $key;
				break;
			}
		}
	}
	if ($siteKey === '') {
		$siteKey = epc_tax_toolkit_site_key_for_db($db);
	}
	$country = epc_tax_toolkit_detect_tenant_country($db, $siteKey);
	$kit = epc_tax_toolkit_country_to_kit_code($country);
	try {
		epc_tax_toolkit_assign_tenant($db, $country, $kit, '', $siteKey !== '' ? $siteKey : 'default', true);
		return array(
			'ok' => true,
			'site_key' => $siteKey !== '' ? $siteKey : 'default',
			'country_code' => $country,
			'kit_code' => $kit,
		);
	} catch (Exception $e) {
		return array('ok' => false, 'site_key' => $siteKey, 'error' => $e->getMessage());
	}
}

function epc_tax_toolkit_migrate_customers(PDO $db, bool $dryRun = false): array
{
	return array('users' => 0, 'contacts' => 0, 'skipped' => 0, 'errors' => array(), 'note' => 'Customer-level kit assignment deprecated — use tenant migration');
}

function epc_tax_toolkit_profile_counts(PDO $db): array
{
	epc_tax_toolkit_ensure_schema($db);
	$installed = (int) $db->query('SELECT COUNT(*) FROM `epc_tax_toolkit_installs`')->fetchColumn();
	$catalog = (int) $db->query('SELECT COUNT(*) FROM `epc_tax_toolkits` WHERE `active` = 1')->fetchColumn();
	$tenants = (int) $db->query('SELECT COUNT(*) FROM `epc_tax_toolkit_tenant_profile`')->fetchColumn();
	$profiles = (int) $db->query('SELECT COUNT(*) FROM `epc_customer_tax_profile`')->fetchColumn();
	$byKit = $db->query(
		'SELECT `kit_code`, COUNT(*) AS cnt FROM `epc_tax_toolkit_tenant_profile` GROUP BY `kit_code` ORDER BY cnt DESC'
	)->fetchAll(PDO::FETCH_ASSOC);
	return array('installed' => $installed, 'catalog' => $catalog, 'tenants' => $tenants, 'profiles' => $profiles, 'by_kit' => $byKit);
}
