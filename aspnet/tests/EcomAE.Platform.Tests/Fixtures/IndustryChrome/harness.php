<?php
// PHP 8.3 goldens for industry chrome, SEO helpers, logos, templates, schema and CP JS configs.
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_ichr_' . substr(md5(uniqid('', true)), 0, 12);
	foreach (array(
		'/content/general_pages',
		'/content/users',
		'/cp/content/filemanager',
		'/cp/content/shop/order_process',
	) as $dir) {
		@mkdir($doc . $dir, 0777, true);
	}

	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$pdo->exec('CREATE TABLE shop_orders_items_statuses_ref (`id` INT PRIMARY KEY, `count_flag` INT, `for_finish` INT, `for_created` INT)');
	foreach (($case['statuses'] ?? array()) as $id) {
		$pdo->prepare('INSERT INTO shop_orders_items_statuses_ref VALUES (?,?,?,?)')->execute(array($id, 1, 0, 0));
	}

	$footer = $case['footer'] ?? array();
	$kind = $footer['kind'] ?? 'frn';
	$dataPhp = '<?php
function epc_fashion_retail_namshi_footer_columns() { return $GLOBALS["__footer"]["columns"] ?? array(); }
function epc_fashion_retail_namshi_social_links() { return $GLOBALS["__footer"]["social"] ?? array(); }
function epc_fashion_retail_namshi_payment_methods() { return $GLOBALS["__footer"]["payments"] ?? array(); }
function epc_electronics_retail_footer_columns() { return $GLOBALS["__footer"]["columns"] ?? array(); }
function epc_electronics_retail_social_links() { return $GLOBALS["__footer"]["social"] ?? array(); }
function epc_electronics_retail_payment_methods() { return $GLOBALS["__footer"]["payments"] ?? array(); }
function epc_jewellery_retail_kiyasha_footer_columns() { return $GLOBALS["__footer"]["columns"] ?? array(); }
function epc_jewellery_retail_kiyasha_social_links() { return $GLOBALS["__footer"]["social"] ?? array(); }
function epc_jewellery_retail_kiyasha_payment_methods() { return $GLOBALS["__footer"]["payments"] ?? array(); }
function epc_cpi_footer_columns() { return $GLOBALS["__footer"]["columns"] ?? array(); }
function epc_cpi_header_contact() { return $GLOBALS["__footer"]["contact"] ?? array("email"=>"","phone"=>""); }
';
	file_put_contents($doc . '/content/general_pages/epc_fashion_retail_namshi_data.php', $dataPhp);
	file_put_contents($doc . '/content/general_pages/epc_electronics_retail_data.php', $dataPhp);
	file_put_contents($doc . '/content/general_pages/epc_jewellery_retail_kiyasha_data.php', $dataPhp);
	file_put_contents($doc . '/content/general_pages/epc_consulting_primeinvest_data.php', $dataPhp);
	file_put_contents($doc . '/content/general_pages/epc_branding.php', '<?php
if (!function_exists("epc_brand_trade_name")) { function epc_brand_trade_name() { return $GLOBALS["__trade"] ?? "Store"; } }
if (!function_exists("epc_brand_hosted_by_html")) { function epc_brand_hosted_by_html() { return $GLOBALS["__hosted"] ?? ""; } }
');
	file_put_contents($doc . '/content/general_pages/epc_portal.php', '<?php
if (!function_exists("epc_portal_load_site_settings")) { function epc_portal_load_site_settings() { return $GLOBALS["__settings"] ?? array(); } }
if (!function_exists("epc_portal_site_profile")) { function epc_portal_site_profile() { return $GLOBALS["__site"] ?? array(); } }
if (!function_exists("epc_portal_host")) { function epc_portal_host() { return $GLOBALS["__host"] ?? ""; } }
if (!function_exists("epc_portal_active_storefront_package")) { function epc_portal_active_storefront_package() { return $GLOBALS["__package"] ?? ""; } }
if (!function_exists("epc_portal_apply_config")) { function epc_portal_apply_config($c) {} }
');
	file_put_contents($doc . '/content/general_pages/epc_portal_tenant_brand.php', '<?php
if (!function_exists("epc_portal_tenant_brand_enabled")) { function epc_portal_tenant_brand_enabled() { return !empty($GLOBALS["__brand"]); } }
if (!function_exists("epc_portal_tenant_brand_markup")) { function epc_portal_tenant_brand_markup($m) { return (string) ($GLOBALS["__brand"] ?? ""); } }
');
	file_put_contents($doc . '/content/general_pages/epc_storefront_worldclass.php', '<?php
if (!function_exists("epc_storefront_social_links_data")) { function epc_storefront_social_links_data() { return $GLOBALS["__social"] ?? array(); } }
');
	if (empty($case['omit_config'])) {
		$cfg = $case['config'] ?? array();
		$cfg['host'] = $cfg['host'] ?? '127.0.0.1';
		$cfg['db'] = $dbName;
		$cfg['user'] = 'ecomae';
		$cfg['password'] = $password;
		$cfg['backend_dir'] = $cfg['backend_dir'] ?? 'cp';
		file_put_contents($doc . '/config.php', '<?php
if (!class_exists("DP_Config")) {
	class DP_Config {
		public function __construct() {
			foreach (($GLOBALS["__case_config"] ?? array()) as $k => $v) { $this->$k = $v; }
		}
	}
}
');
	}
	$adminSession = !empty($case['admin'])
		? array('csrf_guard_key' => (string) ($case['csrf'] ?? 'tok'))
		: array();
	$adminId = (int) ($case['admin_id'] ?? 0);
	file_put_contents($doc . '/content/users/dp_user.php', '<?php
class DP_User {
	public static function getAdminSession() { return $GLOBALS["__admin_session"] ?? array(); }
	public static function getAdminId() { return (int) ($GLOBALS["__admin_id"] ?? 0); }
}
');
	file_put_contents($doc . '/content/general_pages/epc_cp_translate.php', '<?php
function translate_str_by_id($key, $lang = null) { return "{" . $key . "}"; }
');

	foreach (array(
		'content/general_pages/epc_portal_fashion_retail_namshi_footer.php',
		'content/general_pages/epc_portal_electronics_retail_footer.php',
		'content/general_pages/epc_portal_jewellery_retail_kiyasha_footer.php',
		'content/general_pages/epc_portal_consulting_primeinvest_footer.php',
		'content/general_pages/epc_storefront_animated_logos.php',
		'content/general_pages/epc_cp_page_frame.php',
		'cp/content/filemanager/epc_filemanager_config.php',
		'cp/content/shop/order_process/orders_items_config.php',
	) as $rel) {
		$src = $root . '/' . $rel;
		if (is_file($src)) {
			$dest = $doc . '/' . $rel;
			@mkdir(dirname($dest), 0777, true);
			copy($src, $dest);
		}
	}

	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		if (is_dir($doc)) {
			$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
			foreach ($it as $file) {
				$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
			}
			@rmdir($doc);
		}
	};

	$GLOBALS['__root'] = $root;
	$GLOBALS['__footer'] = $footer;
	$GLOBALS['__trade'] = $case['trade'] ?? 'Store';
	$GLOBALS['__hosted'] = $case['hosted'] ?? '';
	$GLOBALS['__brand'] = $case['brand'] ?? '';
	$GLOBALS['__social'] = $case['social'] ?? array();
	$GLOBALS['__settings'] = $case['settings'] ?? array();
	$GLOBALS['__site'] = $case['site'] ?? array();
	$GLOBALS['__host'] = $case['host'] ?? '';
	$GLOBALS['__package'] = $case['package'] ?? '';
	$GLOBALS['__admin_session'] = $adminSession;
	$GLOBALS['__admin_id'] = $adminId;
	$GLOBALS['__case_config'] = $case['config'] ?? array();
	$GLOBALS['__case_config']['host'] = '127.0.0.1';
	$GLOBALS['__case_config']['db'] = $dbName;
	$GLOBALS['__case_config']['user'] = 'ecomae';
	$GLOBALS['__case_config']['password'] = $password;
	if (!isset($GLOBALS['__case_config']['backend_dir'])) {
		$GLOBALS['__case_config']['backend_dir'] = 'cp';
	}

	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_SERVER['HTTP_HOST'] = $case['http_host'] ?? 'localhost';
	$_GET = $case['get'] ?? array();
	$_POST = array();
	$_COOKIE = $case['cookies'] ?? array();
	$DP_Config = new stdClass();
	foreach ($GLOBALS['__case_config'] as $key => $value) {
		$DP_Config->$key = $value;
	}
	$GLOBALS['DP_Config'] = $DP_Config;
	foreach (($case['vars'] ?? array()) as $key => $value) {
		$$key = $value;
	}
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	if (!function_exists('epc_portal_load_site_settings')) {
		function epc_portal_load_site_settings() { return $GLOBALS['__settings'] ?? array(); }
		function epc_portal_site_profile() { return $GLOBALS['__site'] ?? array(); }
		function epc_portal_host() { return $GLOBALS['__host'] ?? ''; }
		function epc_portal_active_storefront_package() { return $GLOBALS['__package'] ?? ''; }
		function epc_brand_trade_name() { return $GLOBALS['__trade'] ?? 'Store'; }
		function epc_brand_hosted_by_html() { return $GLOBALS['__hosted'] ?? ''; }
	}
	$db_link = $pdo;
	$GLOBALS['db_link'] = $pdo;

	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$headers = array();
		foreach (headers_list() as $header) {
			$headers[] = $header;
		}
		$cleanup();
		echo json_encode(
			array(
				'name' => $case['name'],
				'output' => $html,
				'result' => $GLOBALS['__result'] ?? null,
				'headers' => $headers,
				'status' => http_response_code(),
			),
			JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR
		);
	});
	ob_start();
	if (isset($case['file'])) {
		include $doc . '/' . $case['file'];
	}
	if (isset($case['eval'])) {
		$GLOBALS['__result'] = eval($case['eval']);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/industry_chrome_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/industry_chrome_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
