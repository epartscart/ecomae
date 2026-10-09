<?php
// PHP 8.3 goldens for named-function non-ERP helpers (taxonomy, hashes, cache, legal, branding).
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_named_' . substr(md5(uniqid('', true)), 0, 12);
	foreach (array(
		'/content/general_pages',
		'/content/shop/docpart/cache/crossbase',
		'/content/users',
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
	$pdo->exec('CREATE TABLE `shop_docpart_prices` (`id` INT PRIMARY KEY, `name` VARCHAR(128), `load_mode` INT, `last_updated` VARCHAR(32))');
	$pdo->exec('CREATE TABLE `shop_docpart_prices_data` (`id` INT AUTO_INCREMENT PRIMARY KEY, `price_id` INT)');
	$pdo->exec('CREATE TABLE `epc_price_upload_history` (`id` INT AUTO_INCREMENT PRIMARY KEY, `upload_source` VARCHAR(32), `created_at` VARCHAR(32))');
	$pdo->exec('CREATE TABLE `shop_docpart_pyprices_crontab` (`id` INT AUTO_INCREMENT PRIMARY KEY)');
	$pdo->exec('CREATE TABLE `shop_docpart_pyprices_crontab_prices` (`id` INT AUTO_INCREMENT PRIMARY KEY)');
	$pdo->exec('CREATE TABLE `shop_docpart_pyprices_tasks` (`id` INT AUTO_INCREMENT PRIMARY KEY, `status` VARCHAR(32) NULL)');
	$pdo->exec("INSERT INTO `shop_docpart_prices` VALUES (1,'Alpha',1,'2026-01-02 03:04:05'),(2,'Beta FTP',2,'2026-02-01 00:00:00')");
	$pdo->exec('INSERT INTO `shop_docpart_prices_data` (`price_id`) VALUES (1),(1),(2)');
	$pdo->exec("INSERT INTO `epc_price_upload_history` (`upload_source`,`created_at`) VALUES ('ftp','2026-03-01 12:00:00'),('ftp','2026-03-02 12:00:00'),('manual','2026-03-03 00:00:00')");
	$pdo->exec('INSERT INTO `shop_docpart_pyprices_crontab` VALUES (NULL),(NULL)');
	$pdo->exec('INSERT INTO `shop_docpart_pyprices_crontab_prices` VALUES (NULL),(NULL),(NULL)');
	$pdo->exec("INSERT INTO `shop_docpart_pyprices_tasks` (`status`) VALUES (''),('done')");

	file_put_contents($doc . '/content/general_pages/epc_portal.php', '<?php
if (!function_exists("epc_portal_site_profile")) { function epc_portal_site_profile() { return $GLOBALS["__site"] ?? array(); } }
if (!function_exists("epc_portal_load_site_settings")) { function epc_portal_load_site_settings() { return $GLOBALS["__settings"] ?? array(); } }
if (!function_exists("epc_portal_is_client_hostname")) { function epc_portal_is_client_hostname() { return !empty($GLOBALS["__client_host"]); } }
if (!function_exists("epc_portal_demo_is_autoparts_parity")) { function epc_portal_demo_is_autoparts_parity() { return !empty($GLOBALS["__autoparts"]); } }
if (!function_exists("epc_portal_is_platform_hostname")) { function epc_portal_is_platform_hostname() { return !empty($GLOBALS["__platform_host"]); } }
if (!function_exists("epc_portal_is_cp_request")) { function epc_portal_is_cp_request() { return false; } }
if (!function_exists("epc_portal_demo_cp_is_erp_only")) { function epc_portal_demo_cp_is_erp_only() { return false; } }
if (!function_exists("epc_portal_is_epartscart_hostname")) { function epc_portal_is_epartscart_hostname() { return false; } }
if (!function_exists("epc_platform_erp_is_active")) { function epc_platform_erp_is_active() { return false; } }
');
	file_put_contents($doc . '/content/general_pages/epc_ecomae_legal_content.php', '<?php
if (!function_exists("epc_ecomae_legal_effective_date")) { function epc_ecomae_legal_effective_date() { return "16 July 2026"; } }
if (!function_exists("epc_ecomae_legal_catalog")) {
	function epc_ecomae_legal_catalog() {
		return array(
			"privacy" => array(
				"title" => "Privacy Policy",
				"summary" => "How ECOM AE collects, uses, stores, and protects personal and business data across the platform, Super CP, and tenant workspaces.",
				"icon" => "fa-user-secret",
				"sections" => array(),
			),
			"terms" => array(
				"title" => "Terms of Use",
				"summary" => "Terms for using the ECOM AE platform.",
				"icon" => "fa-file-text",
				"sections" => array(),
			),
		);
	}
}
if (!function_exists("epc_ecomae_h")) { function epc_ecomae_h($v) { return htmlspecialchars((string) $v, ENT_QUOTES, "UTF-8"); } }
if (!function_exists("epc_ecomae_platform_base_url")) { function epc_ecomae_platform_base_url() { return "https://www.ecomae.com/"; } }
');
	foreach (array(
		'content/shop/docpart/docpart_article_match.php',
		'content/general_pages/epc_ecomae_legal_pages.php',
	) as $rel) {
		$src = $root . '/' . $rel;
		if (is_file($src)) {
			copy($src, $doc . '/' . $rel);
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
	$GLOBALS['__site'] = $case['site'] ?? array();
	$GLOBALS['__settings'] = $case['settings'] ?? array();
	$GLOBALS['__client_host'] = !empty($case['client_host']);
	$GLOBALS['__autoparts'] = !empty($case['autoparts']);
	$GLOBALS['__platform_host'] = !empty($case['platform_host']);

	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_SERVER['HTTP_HOST'] = $case['http_host'] ?? 'localhost';
	$_SERVER['HTTP_USER_AGENT'] = (string) ($case['ua'] ?? '');
	$_GET = array();
	$_POST = array();
	$_COOKIE = array();
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$db_link = $pdo;
	$GLOBALS['db_link'] = $pdo;

	if (!empty($case['session'])) {
		session_save_path(sys_get_temp_dir());
		if (session_status() !== PHP_SESSION_ACTIVE) {
			session_start();
		}
	}

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
	if (isset($case['eval'])) {
		$GLOBALS['__result'] = eval($case['eval']);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/named_batch_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/named_batch_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
