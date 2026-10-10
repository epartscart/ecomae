<?php
// PHP 8.3 goldens for plan Q1-tide (tenant country profile). Leftover tax-toolkit / APAI stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function tide_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("defined('_ASTEXE_') or die('No access');", "// access gate stubbed", $code);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';", '// leftover countries injected', $code);
	$code = str_replace('require_once $_SERVER[\'DOCUMENT_ROOT\'] . \'/content/users/epc_countries.php\';', '// leftover countries injected', $code);
	$code = str_replace('require_once $_SERVER[\'DOCUMENT_ROOT\'] . \'/content/shop/finance/epc_tax_toolkit.php\';', '// leftover tax toolkit injected', $code);
	$code = str_replace('require_once $_SERVER[\'DOCUMENT_ROOT\'] . \'/content/general_pages/epc_portal_tenant.php\';', '// leftover portal tenant injected', $code);
	$code = str_replace('require_once $_SERVER[\'DOCUMENT_ROOT\'] . \'/content/general_pages/epc_portal_db.php\';', '// leftover portal db injected', $code);
	$code = str_replace('require_once $_SERVER[\'DOCUMENT_ROOT\'] . \'/content/general_pages/epc_portal_tenant_intro.php\';', '// leftover tenant intro injected', $code);
	$code = str_replace('require_once $_SERVER[\'DOCUMENT_ROOT\'] . \'/config.php\';', '// leftover config injected', $code);
	$code = str_replace('$taxFile = $_SERVER[\'DOCUMENT_ROOT\'] . \'/content/shop/finance/epc_tax_toolkit.php\';', '$taxFile = \'\';', $code);
	$code = str_replace('$apaiFile = $_SERVER[\'DOCUMENT_ROOT\'] . \'/content/shop/price_engine/epc_apai_country_sources.php\';', '$apaiFile = \'\';', $code);
	$code = str_replace('require_once $_SERVER[\'DOCUMENT_ROOT\'] . \'/content/shop/price_engine/epc_apai_country_sources.php\';', '// leftover apai injected', $code);
	file_put_contents($dest, $code);
}

function tide_stubs(): void
{
	if (!function_exists('epc_countries_iso3166_alpha2')) {
		function epc_countries_iso3166_alpha2(): array
		{
			return array('AE' => 'United Arab Emirates', 'PK' => 'Pakistan', 'US' => 'United States', 'GB' => 'United Kingdom', 'IN' => 'India');
		}
	}
	if (!function_exists('epc_tax_toolkit_country_name_to_iso')) {
		function epc_tax_toolkit_country_name_to_iso($value): string
		{
			$v = strtoupper(trim((string) $value));
			return $v === 'UAE' ? 'AE' : '';
		}
	}
	if (!function_exists('epc_apai_country_meta')) {
		function epc_apai_country_meta($cc): array
		{
			$map = array('US' => 'USD', 'GB' => 'GBP', 'PK' => 'PKR', 'IN' => 'INR');
			$code = strtoupper((string) $cc);
			return array('currency' => $map[$code] ?? 'AED', 'label' => $code === 'PK' ? 'Pakistan' : ($code === 'US' ? 'United States' : 'United Arab Emirates'));
		}
	}
	if (!function_exists('epc_portal_db_ensure')) {
		function epc_portal_db_ensure($pdo): void
		{
		}
	}
	if (!function_exists('epc_portal_tenant_get')) {
		function epc_portal_tenant_get($pdo, $siteKey)
		{
			$st = $pdo->prepare('SELECT * FROM `epc_portal_tenants` WHERE `site_key` = ? LIMIT 1');
			$st->execute(array($siteKey));
			$row = $st->fetch(PDO::FETCH_ASSOC);
			return $row ?: null;
		}
	}
	if (!function_exists('epc_portal_load_site_settings_for_host')) {
		function epc_portal_load_site_settings_for_host($pdo, $host): array
		{
			return array('host' => (string) $host, 'contact' => array());
		}
	}
	if (!function_exists('epc_portal_save_site_settings')) {
		function epc_portal_save_site_settings($pdo, array $settings): void
		{
			$GLOBALS['TIDE_SAVED'][] = $settings;
		}
	}
	if (!function_exists('epc_apai_tenant_country')) {
		function epc_apai_tenant_country($siteKey, $pdo = null): string
		{
			return (string) ($GLOBALS['TIDE_APAI_CC'] ?? 'AE');
		}
	}
	if (!function_exists('epc_tax_toolkit_site_key_for_db')) {
		function epc_tax_toolkit_site_key_for_db($pdo): string
		{
			return 'fromdb';
		}
	}
}

function tide_include(): void
{
	tide_stubs();
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	include_once $GLOBALS['TIDE_PAGE'];
}

function tide_admin(): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	return new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function tide_db(PDO $admin): array
{
	$schema = 'ecomae_cpw_tide_' . substr(md5(uniqid('', true)), 0, 8);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$db = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $schema . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$db->exec('CREATE TABLE `epc_portal_tenants` (
		`site_key` VARCHAR(64) NOT NULL PRIMARY KEY,
		`hostname` VARCHAR(120) NOT NULL DEFAULT \'\',
		`trade_name` VARCHAR(120) NOT NULL DEFAULT \'\',
		`db_name` VARCHAR(64) NOT NULL DEFAULT \'\',
		`country_code` CHAR(2) NOT NULL DEFAULT \'\',
		`updated_at` INT NOT NULL DEFAULT 0
	)');
	$db->exec('CREATE TABLE `epc_price_settings` (`setting_key` VARCHAR(64) NOT NULL PRIMARY KEY, `setting_value` VARCHAR(255) NOT NULL)');
	return array($schema, $db);
}

function tide_run_names()
{
	tide_include();
	return array(
		epc_tenant_country_normalize('ae'),
		epc_tenant_country_normalize('UAE'),
		epc_tenant_country_normalize('Pakistan'),
		epc_tenant_country_normalize('xx'),
		epc_tenant_country_normalize(''),
	);
}

function tide_run_defaults()
{
	tide_include();
	return array(
		epc_tenant_country_erp_defaults('US'),
		epc_tenant_country_erp_defaults('GB'),
		epc_tenant_country_erp_defaults('AE'),
		epc_tenant_country_erp_defaults('PK'),
	);
}

function tide_run_apply()
{
	tide_include();
	$admin = tide_admin();
	[$schema, $db] = tide_db($admin);
	try {
		$GLOBALS['TIDE_SAVED'] = array();
		$bad = epc_tenant_apply_country_profile('', 'AE', $db);
		$unknown = epc_tenant_apply_country_profile('acme', 'ZZ', $db);
		$db->exec("INSERT INTO `epc_portal_tenants` (`site_key`,`hostname`,`trade_name`,`db_name`) VALUES ('acme','www.acme.test','Acme','')");
		$ok = epc_tenant_apply_country_profile('acme', 'PK', $db);
		$st = $db->query("SELECT `country_code` FROM `epc_portal_tenants` WHERE `site_key`='acme'");
		$cc = $st->fetchColumn();
		$price = $db->query("SELECT COUNT(*) FROM `epc_price_settings`")->fetchColumn();
		return array(
			$bad['ok'] ? 1 : 0,
			$bad['errors'],
			$unknown['ok'] ? 1 : 0,
			$ok['ok'] ? 1 : 0,
			$ok['country_code'],
			$ok['country_name'],
			$ok['steps']['registry'] ?? '',
			$ok['steps']['platform_site_settings'] ?? '',
			$ok['steps']['erp'] ?? '',
			isset($ok['steps']['tax_toolkit']) ? 1 : 0,
			isset($ok['steps']['apai_sources']) ? 1 : 0,
			$cc,
			(int) $price,
			count($GLOBALS['TIDE_SAVED']),
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function tide_run_market()
{
	tide_include();
	$GLOBALS['TIDE_APAI_CC'] = 'PK';
	$with = epc_tenant_country_market_label(null, 'acme');
	$GLOBALS['TIDE_APAI_CC'] = 'AE';
	$ae = epc_tenant_country_market_label(null, 'acme');
	return array($with, $ae);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_tide_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	tide_patch($root . '/content/shop/tenant_hub/epc_tenant_country_profile.php', $tmp . '/page.php');
	$GLOBALS['TIDE_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'tide_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1tide_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1tide_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
