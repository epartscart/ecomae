<?php
// PHP 8.3 goldens for plan Q1-jib (tenant showcase). Leftover marketing-data / home-h parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function jib_reset(): void
{
	$GLOBALS['JIB_TENANTS'] = array();
	$GLOBALS['JIB_SHOTS'] = array();
	$GLOBALS['JIB_BASE'] = 'https://www.ecomae.com/';
}

function jib_tenants_default(): array
{
	return array(
		array(
			'key' => 'epartscart',
			'name' => 'eParts Cart',
			'industry' => 'auto_parts',
			'outcome' => 'Flagship',
			'site_url' => 'https://www.epartscart.com/',
			'portal_url' => 'https://www.epartscart.com/cp/',
			'logo_url' => '/logos/epartscart.png',
		),
		array(
			'key' => 'taxofinca',
			'name' => "O'Brien tax",
			'outcome' => 'Advisory',
			'site_url' => 'https://www.taxofinca.com/',
			'portal_url' => 'https://www.taxofinca.com/cp/',
			'logo_url' => '',
		),
		array(
			'key' => 'unknown_tn',
			'name' => 'Ghost',
			'outcome' => '',
		),
	);
}

function jib_run_themes()
{
	jib_reset();
	$themes = epc_ecomae_platform_tenant_showcase_themes();
	$codes = array('auto_parts', 'AUTO_PARTS', 'Acme-1!', 'jewellery', 'nope', 'fashion', 'electronics', 'tax_advisory', '');
	$picked = array();
	$keys = array();
	$mods = array();
	foreach ($codes as $code) {
		$picked[$code] = epc_ecomae_platform_tenant_showcase_theme($code);
		$keys[$code] = epc_ecomae_platform_tenant_key_for_industry($code);
		$mods[$code] = epc_ecomae_platform_tenant_cp_modules($code);
	}
	$css = epc_ecomae_platform_tenant_showcase_styles();
	return array(
		array_keys($themes),
		$themes['jewellery'],
		$picked,
		$keys,
		$mods,
		strlen($css),
		substr($css, 0, 80),
		substr($css, -80),
	);
}

function jib_run_rows()
{
	jib_reset();
	$GLOBALS['JIB_TENANTS'] = jib_tenants_default();
	$rows = epc_ecomae_platform_tenant_showcase_rows();
	$slim = array();
	foreach ($rows as $row) {
		$slim[] = array(
			'key' => $row['key'] ?? '',
			'industry' => $row['industry'] ?? '',
			'theme' => $row['theme_meta']['theme'] ?? '',
			'label' => $row['theme_meta']['label'] ?? '',
			'name' => $row['name'] ?? '',
		);
	}
	$GLOBALS['JIB_TENANTS'] = array();
	$empty = epc_ecomae_platform_tenant_showcase_rows();
	$GLOBALS['JIB_TENANTS'] = array(
		array('key' => 'stylenlook', 'name' => 'Style', 'industry' => 'Fashion!'),
	);
	$mapped = epc_ecomae_platform_tenant_showcase_rows();
	return array($slim, $empty, $mapped[0]['industry'] ?? '', $mapped[0]['theme_meta']['hero_type'] ?? '');
}

function jib_run_shots()
{
	jib_reset();
	$miss = epc_ecomae_platform_tenant_storefront_screenshot('');
	$blank = epc_ecomae_platform_tenant_storefront_screenshot('!!!');
	$none = epc_ecomae_platform_tenant_storefront_screenshot('epartscart');
	$GLOBALS['JIB_SHOTS']['tenant-epartscart-storefront'] = '/content/files/images/tenant-epartscart-storefront.webp';
	$webp = epc_ecomae_platform_tenant_storefront_screenshot('Eparts-Cart!');
	$GLOBALS['JIB_SHOTS']['tenant-electronicae-storefront'] = '/content/files/images/tenant-electronicae-storefront.PNG';
	$png = epc_ecomae_platform_tenant_storefront_screenshot('electronicae');
	$previewLive = epc_ecomae_platform_tenant_storefront_preview('auto_parts', 'Storefront', 'epartscart');
	$previewAnim = epc_ecomae_platform_tenant_storefront_preview('jewellery', "Store o'front", '');
	$previewKey = epc_ecomae_platform_tenant_storefront_preview('electronics');
	return array($miss, $blank, $none, $webp, $png, $previewLive, $previewAnim, $previewKey);
}

function jib_run_html()
{
	jib_reset();
	$GLOBALS['JIB_TENANTS'] = jib_tenants_default();
	$GLOBALS['JIB_SHOTS']['tenant-epartscart-storefront'] = '/content/files/images/tenant-epartscart-storefront.png';
	$GLOBALS['JIB_BASE'] = 'https://www.ecomae.com/';
	$logos = array();
	foreach (array('auto_parts', 'tax_advisory', 'electronics', 'fashion', 'jewellery', 'nope') as $code) {
		$logos[$code] = epc_ecomae_platform_tenant_animated_logo($code, $code === 'fashion' ? "O'Name" : '');
	}
	$heroes = array();
	foreach (array('auto_parts', 'tax_advisory', 'electronics', 'fashion', 'jewellery') as $code) {
		$heroes[$code] = epc_ecomae_platform_tenant_mini_hero_visual($code);
	}
	$cp = epc_ecomae_platform_tenant_cp_preview('jewellery', "CP o'panel");
	$cpDefault = epc_ecomae_platform_tenant_cp_preview('nope');
	$card = epc_ecomae_platform_tenant_showcase_card(array(
		'key' => 'epartscart',
		'name' => "O'Parts",
		'industry' => 'auto_parts',
		'outcome' => "Flagship o'biz",
		'site_url' => 'https://www.epartscart.com/',
		'portal_url' => 'https://www.epartscart.com/cp/',
		'logo_url' => '/logos/x.png',
	));
	$cardNoLogo = epc_ecomae_platform_tenant_showcase_card(array(
		'key' => 'taxofinca',
		'name' => 'taxofinca',
		'industry' => 'tax_advisory',
		'outcome' => 'Advisory',
		'site_url' => '#',
		'portal_url' => '#',
		'logo_url' => '',
	));
	$page = epc_ecomae_platform_tenant_showcase_section('page');
	$home = epc_ecomae_platform_tenant_showcase_section('home');
	$themedLive = epc_ecomae_platform_industry_themed_previews('auto_parts', 'Auto parts');
	$themedAnim = epc_ecomae_platform_industry_themed_previews('fashion', "Fashion o'shop");
	return array($logos, $heroes, $cp, $cpDefault, $card, $cardNoLogo, $page, $home, $themedLive, $themedAnim);
}

function jib_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace(
		'/require_once \$_SERVER\[\'DOCUMENT_ROOT\'\] \. \'\/content\/general_pages\/epc_ecomae_platform_data\.php\';/',
		'// leftover marketing-data parent stubbed',
		$code,
		1
	);
	file_put_contents($dest, $code);
}

function jib_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_ecomae_h')) {
		eval('function epc_ecomae_h($v) { return htmlspecialchars((string) $v, ENT_QUOTES, "UTF-8"); }');
	}
	if (!function_exists('epc_ecomae_platform_customer_results')) {
		eval('function epc_ecomae_platform_customer_results() { return $GLOBALS["JIB_TENANTS"] ?? array(); }');
	}
	if (!function_exists('epc_ecomae_platform_screenshot')) {
		eval('function epc_ecomae_platform_screenshot($slug, $allowLegacyFallback = true) { return (string) (($GLOBALS["JIB_SHOTS"] ?? array())[$slug] ?? ""); }');
	}
	if (!function_exists('epc_ecomae_platform_base_url')) {
		eval('function epc_ecomae_platform_base_url() { return (string) ($GLOBALS["JIB_BASE"] ?? "https://www.ecomae.com/"); }');
	}
	jib_reset();
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_jib_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	jib_patch($root . '/content/general_pages/epc_ecomae_platform_tenant_showcase.php', $doc . '/showcase.php');
	jib_boot();
	require $doc . '/showcase.php';
	$fn = 'jib_run_' . $case['name'];
	$result = $fn();
	@unlink($doc . '/showcase.php');
	@rmdir($doc);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1jib_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1jib_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
