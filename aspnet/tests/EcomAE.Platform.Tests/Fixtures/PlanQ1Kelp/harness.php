<?php
// PHP 8.3 goldens for plan Q1-kelp (portal tenant intro). Leftover country-profile / hub-helpers stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function kelp_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once __DIR__ . '/epc_portal_tenant.php';", '// leftover portal tenant injected', $code);
	$code = str_replace("require_once __DIR__ . '/epc_portal_erp_modules.php';", '// leftover erp modules injected', $code);
	$code = str_replace("require_once __DIR__ . '/epc_portal_storefront_packages.php';", '// leftover packages injected', $code);
	$code = str_replace("require_once __DIR__ . '/epc_portal_theme_templates.php';", '// leftover themes injected', $code);
	$code = str_replace("require_once __DIR__ . '/epc_portal_db.php';", '// leftover portal db injected', $code);
	$code = str_replace("require_once __DIR__ . '/epc_portal_cp_menu.php';", '// leftover cp menu injected', $code);
	$code = str_replace("require_once __DIR__ . '/epc_platform_jobs.php';", '// leftover jobs injected', $code);
	$code = str_replace('require_once $_SERVER[\'DOCUMENT_ROOT\'] . \'/content/users/epc_countries.php\';', '// leftover countries injected', $code);
	$code = str_replace("\$intro['submitted_at'] = time();", "\$intro['submitted_at'] = 1700000000;", $code);
	file_put_contents($dest, $code);
}

function kelp_stubs(): void
{
	if (!function_exists('epc_portal_erp_modules_from_post')) {
		function epc_portal_erp_modules_from_post(array $post): array
		{
			if (!empty($post['erp_modules_preset']) && $post['erp_modules_preset'] === 'hr_only') {
				return array('erp_overview', 'erp_people', 'erp_collaboration');
			}
			if (isset($post['erp_modules']) && is_array($post['erp_modules'])) {
				return array_values(array_filter($post['erp_modules'], static function ($id) {
					return $id !== '' && $id !== 'nope';
				}));
			}
			return array();
		}
	}
	if (!function_exists('epc_portal_erp_modules_resolve_for_onboard')) {
		function epc_portal_erp_modules_resolve_for_onboard(array $intro, string $industryCode = '', string $accessMode = 'full'): array
		{
			if (!empty($intro['erp_modules']) && is_array($intro['erp_modules'])) {
				return $intro['erp_modules'];
			}
			return $accessMode === 'erp_only' ? array('erp_overview') : array('erp_sales');
		}
	}
	if (!function_exists('epc_portal_erp_modules_enabled')) {
		function epc_portal_erp_modules_enabled($settings = null): array
		{
			return is_array($settings['erp_modules'] ?? null) ? $settings['erp_modules'] : array('erp_sales');
		}
	}
	if (!function_exists('epc_portal_erp_only_packs')) {
		function epc_portal_erp_only_packs(): array
		{
			return array('erp');
		}
	}
	if (!function_exists('epc_countries_normalize_code')) {
		function epc_countries_normalize_code($value): string
		{
			$v = strtoupper(trim((string) $value));
			if ($v === 'UNITED ARAB EMIRATES' || $v === 'UAE' || $v === 'AE') {
				return 'AE';
			}
			if ($v === 'PAKISTAN' || $v === 'PK') {
				return 'PK';
			}
			return preg_match('/^[A-Z]{2}$/', $v) ? $v : '';
		}
	}
	if (!function_exists('epc_portal_default_site_settings')) {
		function epc_portal_default_site_settings($hostname): array
		{
			return array('host' => (string) $hostname, 'hub_name' => 'Hub', 'enabled_packs' => array('core'), 'access_mode' => 'full');
		}
	}
	if (!function_exists('epc_portal_default_contact')) {
		function epc_portal_default_contact(array $row): array
		{
			return array(
				'trade_name' => (string) ($row['trade_name'] ?? ''),
				'from_email' => (string) ($row['from_email'] ?? ''),
				'country' => (string) ($row['country'] ?? ''),
				'country_code' => (string) ($row['country_code'] ?? ''),
			);
		}
	}
	if (!function_exists('epc_portal_apply_industry_theme_profile')) {
		function epc_portal_apply_industry_theme_profile(&$settings, &$contact, $industry, $opts = array())
		{
			$settings['theme_industry'] = (string) $industry;
			$settings['theme_erp_only'] = !empty($opts['erp_only']) ? 1 : 0;
			return array('message' => 'theme:' . $industry, 'theme_template' => (string) ($opts['theme_template'] ?? 'classic'), 'storefront_package' => (string) ($opts['storefront_package'] ?? ''));
		}
	}
	if (!function_exists('epc_portal_save_site_settings')) {
		function epc_portal_save_site_settings($pdo, array $settings): void
		{
			$GLOBALS['KELP_SAVED_SETTINGS'][] = $settings;
		}
	}
	if (!function_exists('epc_portal_load_site_settings_for_host')) {
		function epc_portal_load_site_settings_for_host($pdo, $host): array
		{
			return $GLOBALS['KELP_SETTINGS'] ?? array('host' => (string) $host, 'access_mode' => 'full');
		}
	}
	if (!function_exists('epc_portal_db_ensure')) {
		function epc_portal_db_ensure($pdo): void
		{
		}
	}
	if (!function_exists('epc_portal_save_tenant')) {
		function epc_portal_save_tenant($pdo, array $data): array
		{
			$st = $pdo->prepare('INSERT INTO `epc_portal_tenants` (`site_key`,`hostname`,`industry_code`,`status`,`trade_name`,`hub_name`,`from_email`,`db_name`,`db_user`,`db_password`,`notes`,`intro_json`,`hosted_on`,`erp_only_shared`,`dedicated_db`,`scale_policy`,`blockchain_mode`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,1700000000,1700000000)');
			$st->execute(array(
				$data['site_key'], $data['hostname'], $data['industry_code'], $data['status'], $data['trade_name'],
				$data['hub_name'], $data['from_email'], $data['db_name'], $data['db_user'], $data['db_password'],
				$data['notes'], '', $data['hosted_on'], (int) ($data['erp_only_shared'] ?? 0),
				(int) ($data['dedicated_db'] ?? 0), $data['scale_policy'], $data['blockchain_mode'],
			));
			return array('ok' => true, 'site_key' => $data['site_key']);
		}
	}
	if (!function_exists('epc_portal_platform_ip')) {
		function epc_portal_platform_ip(): string
		{
			return '203.0.113.10';
		}
	}
	if (!function_exists('epc_portal_tenant_is_shared_erp_row')) {
		function epc_portal_tenant_is_shared_erp_row(array $row): bool
		{
			return !empty($row['erp_only_shared']) || (($row['hosted_on'] ?? '') === 'platform');
		}
	}
	if (!function_exists('epc_portal_resolve_access_mode')) {
		function epc_portal_resolve_access_mode($settings): string
		{
			return (string) ($settings['access_mode'] ?? 'full');
		}
	}
	if (!function_exists('epc_platform_jobs_enqueue')) {
		function epc_platform_jobs_enqueue($type, $siteKey, $payload = array(), $opts = array()): int
		{
			return 77;
		}
	}
}

function kelp_dsn(): string
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	return 'mysql:host=127.0.0.1;port=3306;charset=utf8mb4';
}

function kelp_admin(): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	return new PDO(kelp_dsn(), 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function kelp_schema(PDO $admin): string
{
	$name = 'ecomae_cpw_kelp_' . substr(md5(uniqid('', true)), 0, 8);
	$admin->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	return $name;
}

function kelp_db(string $schema): PDO
{
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$db = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $schema . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$db->exec('CREATE TABLE `epc_portal_tenants` (
		`id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
		`site_key` VARCHAR(64) NOT NULL,
		`hostname` VARCHAR(120) NOT NULL,
		`industry_code` VARCHAR(32) NOT NULL DEFAULT \'auto_parts\',
		`status` VARCHAR(24) NOT NULL DEFAULT \'draft\',
		`trade_name` VARCHAR(120) NOT NULL DEFAULT \'\',
		`hub_name` VARCHAR(120) NOT NULL DEFAULT \'\',
		`from_email` VARCHAR(120) NOT NULL DEFAULT \'\',
		`db_name` VARCHAR(64) NOT NULL DEFAULT \'\',
		`db_user` VARCHAR(64) NOT NULL DEFAULT \'\',
		`db_password` VARCHAR(255) NOT NULL DEFAULT \'\',
		`notes` VARCHAR(500) NOT NULL DEFAULT \'\',
		`intro_json` TEXT NULL,
		`hosted_on` VARCHAR(24) NOT NULL DEFAULT \'client\',
		`erp_only_shared` TINYINT(1) NOT NULL DEFAULT 0,
		`dedicated_db` TINYINT(1) NOT NULL DEFAULT 0,
		`scale_policy` VARCHAR(32) NOT NULL DEFAULT \'shared_docpart\',
		`blockchain_mode` VARCHAR(24) NOT NULL DEFAULT \'anchor\',
		`created_at` INT NOT NULL DEFAULT 0,
		`updated_at` INT NOT NULL DEFAULT 0,
		UNIQUE KEY `site_key` (`site_key`)
	) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4');
	return $db;
}

function kelp_include(): void
{
	kelp_stubs();
	include_once $GLOBALS['KELP_PAGE'];
}

function kelp_run_names()
{
	kelp_include();
	$defs = epc_portal_intro_field_defs();
	$req = array();
	foreach ($defs as $k => $d) {
		$req[$k] = !empty($d['required']) ? 1 : 0;
	}
	$steps = epc_portal_erp_only_onboard_steps();
	$guide = epc_portal_onboard_guide_steps();
	return array(
		array_keys($defs),
		$req,
		epc_portal_intro_defaults(),
		epc_portal_site_key_from_hostname('www.acme-parts.com'),
		epc_portal_site_key_from_hostname('shop.acme.co.uk'),
		epc_portal_site_key_from_hostname(''),
		epc_portal_intro_decode(null),
		epc_portal_intro_decode('{'),
		epc_portal_intro_decode('{"contact_person":"Ada","tagline":"Hello"}')['contact_person'],
		epc_portal_intro_decode('{"contact_person":"Ada","tagline":"Hello"}')['country'],
		count($steps),
		$steps[0]['title'],
		count($guide),
		strpos($guide[2]['body'], '203.0.113.10') !== false ? 1 : 0,
	);
}

function kelp_run_post()
{
	kelp_include();
	$tenant = epc_portal_intro_from_post(array(
		'contact_person' => ' Ada Lovelace ',
		'contact_email' => 'ada@acme.test',
		'country' => 'Pakistan',
		'admin_email' => 'admin@acme.test',
		'theme_template' => 'Modern-Blue!',
		'storefront_package' => 'Pro Pack',
	));
	$erp = epc_portal_intro_from_post(array(
		'erp_only' => 1,
		'erp_modules_preset' => 'hr_only',
		'tenant_mode' => 'full',
	));
	$shared = epc_portal_intro_from_post(array(
		'erp_only_shared' => 1,
		'country_code' => 'pk-1',
	));
	$mode = epc_portal_intro_from_post(array(
		'tenant_mode' => 'erp_only',
		'access_mode' => 'full_commerce',
		'dedicated_db' => 0,
		'scale_policy' => 'shared_docpart',
	));
	return array($tenant, $erp, $shared, $mode);
}

function kelp_run_validate()
{
	kelp_include();
	$ok = epc_portal_intro_validate(array(
		'contact_person' => 'Ada',
		'contact_email' => 'ada@acme.test',
		'country' => 'United Arab Emirates',
		'country_code' => 'AE',
		'admin_email' => 'admin@acme.test',
	), array('hostname' => 'www.acme.test', 'trade_name' => 'Acme'));
	$bad = epc_portal_intro_validate(array(
		'contact_person' => '',
		'contact_email' => 'nope',
		'country' => '',
		'admin_email' => 'also-nope',
	), array('hostname' => 'nodot', 'trade_name' => ''));
	$shared = epc_portal_intro_validate(array(
		'contact_person' => 'Ada',
		'contact_email' => 'ada@acme.test',
		'country' => 'Pakistan',
		'admin_email' => 'admin@acme.test',
		'erp_only_shared' => 1,
	), array('hostname' => '', 'trade_name' => 'Beta', 'hosted_on' => 'platform'));
	$merged = epc_portal_intro_merge(array('contact_person' => 'Old', 'tagline' => 'Keep'), array('contact_person' => 'New', 'tagline' => ''));
	return array($ok, $bad, $shared, $merged['contact_person'], $merged['tagline'], $merged['country']);
}

function kelp_run_db()
{
	kelp_include();
	$admin = kelp_admin();
	$schema = kelp_schema($admin);
	try {
		$db = kelp_db($schema);
		$missing = epc_portal_tenant_get($db, 'nope');
		$emptyKey = epc_portal_tenant_get($db, '!!!');
		$GLOBALS['KELP_SAVED_SETTINGS'] = array();
		$failOnboard = epc_portal_onboard_client($db, array('trade_name' => ''), 'op');
		$okOnboard = epc_portal_onboard_client($db, array(
			'hostname' => 'www.acme-parts.test',
			'site_key' => 'acmeparts',
			'trade_name' => 'Acme Parts',
			'contact_person' => 'Ada',
			'contact_email' => 'ada@acme.test',
			'admin_email' => 'admin@acme.test',
			'country' => 'United Arab Emirates',
			'country_code' => 'AE',
			'status' => 'dns_pending',
			'db_name' => 'acmeparts',
			'erp_modules' => array('erp_sales'),
		), 'op1');
		$row = epc_portal_tenant_get($db, 'acmeparts');
		$intro = epc_portal_intro_decode((string) ($row['intro_json'] ?? ''));
		$check = epc_portal_tenant_launch_checklist($db, 'acmeparts');
		$themeMissing = epc_portal_apply_industry_theme_to_tenant($db, 'missing');
		$theme = epc_portal_apply_industry_theme_to_tenant($db, 'acmeparts', array('theme_template' => 'classic', 'push_client' => 0));
		return array(
			$missing === null ? 1 : 0,
			$emptyKey === null ? 1 : 0,
			$failOnboard['ok'] ? 1 : 0,
			count($failOnboard['errors'] ?? array()),
			$okOnboard['ok'] ? 1 : 0,
			$okOnboard['site_key'],
			$okOnboard['hostname'],
			$okOnboard['dedicated_db'],
			$okOnboard['scale_policy'],
			$okOnboard['warmup_job_id'],
			$okOnboard['country_code'],
			$okOnboard['country_profile']['message'] ?? '',
			$row['trade_name'] ?? '',
			$intro['submitted_at'],
			$intro['submitted_by'],
			$check['total'],
			$check['done'],
			$check['ready'] ? 1 : 0,
			$check['items'][0]['hint'],
			$themeMissing['ok'] ? 1 : 0,
			$themeMissing['message'],
			$theme['ok'] ? 1 : 0,
			$theme['theme_template'],
			count($GLOBALS['KELP_SAVED_SETTINGS']),
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_kelp_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp . '/content/general_pages', 0777, true);
	kelp_patch($root . '/content/general_pages/epc_portal_tenant_intro.php', $tmp . '/page.php');
	$GLOBALS['KELP_PAGE'] = $tmp . '/page.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$fn = 'kelp_run_' . $case['name'];
	$result = $fn();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1kelp_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1kelp_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
