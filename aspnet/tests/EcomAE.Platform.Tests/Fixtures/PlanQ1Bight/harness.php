<?php
// PHP 8.3 goldens for plan Q1-bight (spare-parts page + search ajax). Leftover automotive-data stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
$GLOBALS['BIGHT_DBS'] = array();

function bight_pdo(): PDO
{
	static $pdo = null;
	if ($pdo instanceof PDO) {
		return $pdo;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$name = 'ecomae_cpw_bight_' . substr(md5(uniqid('', true)), 0, 8);
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$root->exec('CREATE DATABASE `' . $name . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	$GLOBALS['BIGHT_DBS'][] = $name;
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	return $pdo;
}

function bight_drop(): void
{
	if (empty($GLOBALS['BIGHT_DBS'])) {
		return;
	}
	$pw = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: 'local-throwaway-pw';
	$root = new PDO('mysql:host=127.0.0.1;port=3306;charset=utf8', 'ecomae', $pw, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	foreach ($GLOBALS['BIGHT_DBS'] as $name) {
		$root->exec('DROP DATABASE IF EXISTS `' . $name . '`');
	}
	$GLOBALS['BIGHT_DBS'] = array();
}

function bight_patch_page(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = preg_replace(
		'/require_once \$_SERVER\[\'DOCUMENT_ROOT\'\] \. [\'"]\/content\/general_pages\/epc_automotive_spareparts_data\.php[\'"]\s*;/',
		'// leftover automotive-data injected',
		$code
	);
	$code = preg_replace(
		'/require_once \$_SERVER\[\'DOCUMENT_ROOT\'\] \. [\'"]\/content\/shop\/epc_spare_parts_warehouse\.php[\'"]\s*;/',
		'// warehouse twin injected',
		$code
	);
	file_put_contents($dest, $code);
}

function bight_patch_ajax(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = preg_replace(
		'/require_once \$_SERVER\[\'DOCUMENT_ROOT\'\] \. [\'"]\/config\.php[\'"]\s*;/',
		'// config injected',
		$code
	);
	$code = preg_replace(
		'/require_once \$_SERVER\[\'DOCUMENT_ROOT\'\] \. [\'"]\/content\/shop\/epc_spare_parts_warehouse\.php[\'"]\s*;/',
		'// warehouse twin injected',
		$code
	);
	$code = preg_replace(
		'/define\(\'_ASTEXE_\', 1\);/',
		"if (!defined('_ASTEXE_')) { define('_ASTEXE_', 1); }",
		$code
	);
	$code = str_replace(
		"try {\n\t\$pdo = new PDO(\n\t\t'mysql:host=' . \$DP_Config->host . ';dbname=' . \$DP_Config->db . ';charset=utf8',\n\t\t\$DP_Config->user,\n\t\t\$DP_Config->password,\n\t\tarray(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC)\n\t);\n} catch (Throwable \$e) {\n\thttp_response_code(503);\n\techo json_encode(array('ok' => false, 'message' => 'Database unavailable.'), JSON_UNESCAPED_UNICODE);\n\texit;\n}",
		"try {\n\t\$factory = \$GLOBALS['BIGHT_PDO_FACTORY'] ?? null;\n\t\$pdo = is_callable(\$factory) ? \$factory() : null;\n\tif (!\$pdo instanceof PDO) { throw new PDOException('unavailable'); }\n} catch (Throwable \$e) {\n\thttp_response_code(503);\n\techo json_encode(array('ok' => false, 'message' => 'Database unavailable.'), JSON_UNESCAPED_UNICODE);\n\treturn;\n}",
		$code
	);
	file_put_contents($dest, $code);
}

function bight_stubs(): void
{
	if (!function_exists('epc_asp_home_lang')) {
		function epc_asp_home_lang(array $multilang_params = null): string
		{
			return (string) ($GLOBALS['BIGHT_LANG'] ?? '/en');
		}
	}
	if (!function_exists('epc_spare_parts_oem_brands')) {
		function epc_spare_parts_oem_brands($pdo): array
		{
			return $GLOBALS['BIGHT_BRANDS'] ?? array();
		}
	}
	if (!function_exists('epc_spare_parts_warehouse_search')) {
		function epc_spare_parts_warehouse_search(string $brand, string $article, $pdo, $DP_Config = null): array
		{
			$key = $brand . '|' . $article;
			if (isset($GLOBALS['BIGHT_SEARCH'][$key])) {
				return $GLOBALS['BIGHT_SEARCH'][$key];
			}
			return array('ok' => false, 'message' => 'Enter a valid part number (at least 2 characters).');
		}
	}
	if (!class_exists('DP_Config', false)) {
		class DP_Config
		{
			public $host = '127.0.0.1';
			public $db = 'mysql';
			public $user = 'ecomae';
			public $password = '';
		}
	}
}

function bight_include_page(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	bight_stubs();
	$db_link = $GLOBALS['db_link'] ?? null;
	$DP_Config = $GLOBALS['DP_Config'] ?? null;
	ob_start();
	require $GLOBALS['BIGHT_PAGE'];
	$GLOBALS['BIGHT_HTML'] = (string) ob_get_clean();
}

function bight_reset(): void
{
	$GLOBALS['BIGHT_LANG'] = '/en';
	$GLOBALS['BIGHT_BRANDS'] = array(
		array('value' => 'Toyota', 'label' => 'Toyota'),
		array('value' => 'Bosch', 'label' => 'Bosch'),
	);
	$GLOBALS['BIGHT_SEARCH'] = array(
		'Toyota|1310154101' => array(
			'ok' => true,
			'brand' => 'Toyota',
			'article' => '1310154101',
			'in_warehouse' => true,
			'qty' => 3,
			'warehouse_cost' => 12.5,
			'sell_price' => 18.5,
			'currency' => 'AED',
			'warehouse_rows' => array(array('warehouse' => 'Acme City')),
			'product_id' => 7,
			'product_url' => '/en/oil-filters/toyota/1310154101',
			'parts_url' => '/en/parts/Toyota/1310154101',
			'redirect_url' => '',
			'message' => '',
		),
		'Toyota|NO-SUCH' => array(
			'ok' => true,
			'brand' => 'Toyota',
			'article' => 'NO-SUCH',
			'in_warehouse' => false,
			'qty' => 0,
			'sell_price' => 0,
			'currency' => 'AED',
			'warehouse_rows' => array(),
			'product_url' => '',
			'parts_url' => '/en/parts/Toyota/NO-SUCH',
			'redirect_url' => '',
			'message' => 'Not in stock — contact us for availability.',
		),
		'Beta|1310154101' => array(
			'ok' => true,
			'brand' => 'Toyota',
			'article' => '1310154101',
			'in_warehouse' => true,
			'qty' => 1,
			'sell_price' => 9,
			'currency' => 'AED',
			'warehouse_rows' => array(array('warehouse' => 'Beta Town')),
			'product_url' => '',
			'parts_url' => '',
			'redirect_url' => '',
			'message' => '',
		),
	);
	$GLOBALS['db_link'] = null;
	$GLOBALS['DP_Config'] = (object) array('product_url' => 'alias');
	$_GET = array();
	$_REQUEST = array();
	$_SERVER['REQUEST_URI'] = '/en/spare-parts';
	$GLOBALS['BIGHT_HTML'] = '';
	$GLOBALS['BIGHT_PDO_FACTORY'] = null;
}

function bight_run_page(): array
{
	bight_reset();
	bight_include_page();
	$nodb = $GLOBALS['BIGHT_HTML'];
	$GLOBALS['db_link'] = bight_pdo();
	$_GET = array();
	$_SERVER['REQUEST_URI'] = '/en/spare-parts';
	bight_include_page();
	$empty = $GLOBALS['BIGHT_HTML'];
	$_GET = array('brand' => 'toyota', 'article' => 'C110J');
	$_SERVER['REQUEST_URI'] = '/en/spare-parts';
	bight_include_page();
	$selected = $GLOBALS['BIGHT_HTML'];
	return array($nodb, $empty, $selected);
}

function bight_run_inline(): array
{
	bight_reset();
	$GLOBALS['db_link'] = bight_pdo();
	$_GET = array('brand' => 'Toyota', 'article' => '1310154101');
	bight_include_page();
	$hit = $GLOBALS['BIGHT_HTML'];
	$_GET = array();
	$_SERVER['REQUEST_URI'] = '/en/spare-parts/Toyota/NO-SUCH';
	bight_include_page();
	$miss = $GLOBALS['BIGHT_HTML'];
	$_GET = array('brand' => 'Toyota');
	$_SERVER['REQUEST_URI'] = '/en/spare-parts/Ignored/1310154101';
	bight_include_page();
	$getWins = $GLOBALS['BIGHT_HTML'];
	return array($hit, $miss, $getWins);
}

function bight_capture_ajax(): array
{
	bight_stubs();
	ob_start();
	$headers = array();
	$GLOBALS['BIGHT_HEADERS'] = &$headers;
	require $GLOBALS['BIGHT_AJAX'];
	$body = (string) ob_get_clean();
	return array($body, http_response_code());
}

function bight_run_ajax(): array
{
	bight_reset();
	$GLOBALS['BIGHT_PDO_FACTORY'] = static function () {
		return bight_pdo();
	};
	$_REQUEST = array('brand' => 'Toyota', 'article' => 'A');
	$short = bight_capture_ajax();
	$_REQUEST = array('brand' => 'Toyota', 'article' => '1310154101');
	$hit = bight_capture_ajax();
	$_REQUEST = array('manufacturer' => 'Toyota', 'article' => 'NO-SUCH');
	$alias = bight_capture_ajax();
	$GLOBALS['BIGHT_PDO_FACTORY'] = static function () {
		throw new PDOException('nope');
	};
	$_REQUEST = array('brand' => 'Toyota', 'article' => '1310154101');
	$fail = bight_capture_ajax();
	return array($short, $hit, $alias, $fail);
}

function bight_run_tenant(): array
{
	bight_reset();
	$GLOBALS['db_link'] = bight_pdo();
	$_GET = array('brand' => 'Toyota', 'article' => '1310154101');
	bight_include_page();
	$acme = $GLOBALS['BIGHT_HTML'];
	$_GET = array('brand' => 'Beta', 'article' => '1310154101');
	bight_include_page();
	$beta = $GLOBALS['BIGHT_HTML'];
	$GLOBALS['BIGHT_PDO_FACTORY'] = static function () {
		return bight_pdo();
	};
	$_REQUEST = array('brand' => 'Toyota', 'article' => '1310154101');
	$acmeAjax = bight_capture_ajax();
	$_REQUEST = array('brand' => 'Beta', 'article' => '1310154101');
	$betaAjax = bight_capture_ajax();
	return array($acme, $beta, $acmeAjax, $betaAjax);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_bight_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	bight_patch_page($root . '/content/general_pages/epc_epartscart_spare_parts.php', $tmp . '/page.php');
	bight_patch_ajax($root . '/content/shop/epc_spare_parts_search.php', $tmp . '/ajax.php');
	$GLOBALS['BIGHT_PAGE'] = $tmp . '/page.php';
	$GLOBALS['BIGHT_AJAX'] = $tmp . '/ajax.php';
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	bight_reset();
	$fn = 'bight_run_' . $case['name'];
	register_shutdown_function('bight_drop');
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		bight_drop();
		exit(1);
	}
	bight_drop();
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1bight_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1bight_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
