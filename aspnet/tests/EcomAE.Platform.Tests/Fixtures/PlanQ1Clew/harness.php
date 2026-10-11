<?php
// PHP 8.3 goldens for plan Q1-clew (shipping-export page). Leftover SEO parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function clew_reset(): void
{
	$GLOBALS['CLEW_LANG'] = 'en';
	$GLOBALS['CLEW_HREF'] = '/en';
	$GLOBALS['CLEW_COUNTRY'] = 'AE';
	$GLOBALS['CLEW_SHIP'] = 'Ships from UAE';
	$GLOBALS['CLEW_COURIER'] = 'DHL / FedEx';
	$GLOBALS['CLEW_HAS_PDO'] = true;
	$GLOBALS['DP_Config'] = (object) array(
		'domain_path' => 'https://www.epartscart.com/',
		'chpu_search_config' => array('level_1' => array('url' => 'parts')),
	);
	$GLOBALS['db_link'] = $GLOBALS['CLEW_HAS_PDO'] ? new stdClass() : null;
	$GLOBALS['multilang_params'] = array();
}

function clew_render(): string
{
	if (!empty($GLOBALS['CLEW_HAS_PDO'])) {
		$GLOBALS['db_link'] = new class extends PDO {
			public function __construct()
			{
			}
		};
	} else {
		$GLOBALS['db_link'] = null;
	}
	ob_start();
	require $GLOBALS['CLEW_PAGE'];
	return (string) ob_get_clean();
}

function clew_clip(string $html): array
{
	return array(strlen($html), substr($html, 0, 80), substr($html, -80));
}

function clew_run_en()
{
	clew_reset();
	$html = clew_render();
	return array(clew_clip($html), strpos($html, 'Shipping &amp; export') !== false ? 1 : 0, strpos($html, '/en/parts') !== false ? 1 : 0);
}

function clew_run_ar()
{
	clew_reset();
	$GLOBALS['CLEW_LANG'] = 'ar';
	$GLOBALS['CLEW_HREF'] = '/ar';
	$GLOBALS['CLEW_SHIP'] = "الشحن من O'man";
	$html = clew_render();
	return array(clew_clip($html), strpos($html, 'الشحن والتصدير') !== false ? 1 : 0, strpos($html, "O&#039;man") !== false ? 1 : 0);
}

function clew_run_ru()
{
	clew_reset();
	$GLOBALS['CLEW_LANG'] = 'ru';
	$GLOBALS['CLEW_HREF'] = '/ru';
	$GLOBALS['CLEW_COUNTRY'] = 'OM';
	$html = clew_render();
	return array(clew_clip($html), strpos($html, 'Доставка и экспорт') !== false ? 1 : 0, strpos($html, 'Tenant country profile: OM') !== false ? 1 : 0);
}

function clew_run_edge()
{
	clew_reset();
	$GLOBALS['CLEW_LANG'] = 'xx';
	$GLOBALS['CLEW_HREF'] = '/xx';
	$GLOBALS['CLEW_HAS_PDO'] = false;
	$GLOBALS['CLEW_SHIP'] = "O'Brien & <GCC>";
	$GLOBALS['DP_Config'] = (object) array(
		'domain_path' => 'https://shop.example/a/',
		'chpu_search_config' => array(),
	);
	$html = clew_render();
	clew_reset();
	$GLOBALS['CLEW_HAS_PDO'] = false;
	$noPdo = clew_render();
	return array(
		clew_clip($html),
		strpos($html, 'Shipping &amp; export') !== false ? 1 : 0,
		strpos($html, 'O&#039;Brien &amp; &lt;GCC&gt;') !== false ? 1 : 0,
		strpos($html, '/xx/parts') !== false ? 1 : 0,
		strpos($html, '/xx/available-brands') !== false ? 1 : 0,
		strpos($noPdo, 'Tenant country profile: AE') !== false ? 1 : 0,
	);
}

function clew_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace(
		"require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_seo_indexing.php';",
		'// leftover seo parent stubbed',
		$code
	);
	file_put_contents($dest, $code);
}

function clew_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_seo_current_lang_code')) {
		eval('function epc_seo_current_lang_code() { return (string) ($GLOBALS["CLEW_LANG"] ?? "en"); }');
	}
	if (!function_exists('epc_seo_lang_href')) {
		eval('function epc_seo_lang_href() { return (string) ($GLOBALS["CLEW_HREF"] ?? "/en"); }');
	}
	if (!function_exists('epc_seo_tenant_country_code')) {
		eval('function epc_seo_tenant_country_code($db) { return (string) ($GLOBALS["CLEW_COUNTRY"] ?? "AE"); }');
	}
	if (!function_exists('epc_seo_regional_shipping_phrase')) {
		eval('function epc_seo_regional_shipping_phrase($lang) { return (string) ($GLOBALS["CLEW_SHIP"] ?? ""); }');
	}
	if (!function_exists('epc_seo_regional_courier_phrase')) {
		eval('function epc_seo_regional_courier_phrase($lang) { return (string) ($GLOBALS["CLEW_COURIER"] ?? ""); }');
	}
	clew_reset();
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_clew_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	clew_patch($root . '/content/general_pages/epc_seo_shipping_export.php', $doc . '/page.php');
	$GLOBALS['CLEW_PAGE'] = $doc . '/page.php';
	clew_boot();
	$fn = 'clew_run_' . $case['name'];
	$result = $fn();
	@unlink($doc . '/page.php');
	@rmdir($doc);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1clew_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1clew_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
