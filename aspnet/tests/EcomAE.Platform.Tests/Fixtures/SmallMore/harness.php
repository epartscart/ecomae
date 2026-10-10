<?php
// PHP 8.3 goldens for the next self-contained non-ERP includes.
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_smore_' . substr(md5(uniqid('', true)), 0, 12);
	foreach (array(
		'/content/users',
		'/content/shop/workshop',
		'/content/general_pages',
		'/lang',
	) as $dir) {
		@mkdir($doc . $dir, 0777, true);
	}

	file_put_contents($doc . '/config.php', '<?php if (!class_exists("DP_Config")) { class DP_Config { public function __construct() { foreach (($GLOBALS["__case_config"] ?? array()) as $k => $v) { $this->$k = $v; } } } }');
	file_put_contents($doc . '/content/users/dp_user.php', '<?php');
	file_put_contents(
		$doc . '/content/shop/workshop/epc_workshop_helpers.php',
		'<?php function epc_ws_staff_ok() { return !empty($GLOBALS["__staff"]); }'
	);
	$hero = $case['hero'] ?? array();
	$pfx = $hero['prefix'] ?? 'cpi';
	$heroPhp = '<?php
function epc_' . $pfx . '_pro_hero_eyebrow() { return $GLOBALS["__hero"]["eyebrow"] ?? ""; }
function epc_' . $pfx . '_pro_hero_title() { return $GLOBALS["__hero"]["title"] ?? ""; }
function epc_' . $pfx . '_pro_hero_copy() { return $GLOBALS["__hero"]["copy"] ?? ""; }
function epc_' . $pfx . '_pro_hero_actions($lang) { return $GLOBALS["__hero"]["actions"] ?? array(); }
function epc_' . $pfx . '_pro_hero_stats() { return $GLOBALS["__hero"]["stats"] ?? array(); }
';
	file_put_contents($doc . '/content/general_pages/epc_consulting_primeinvest_data.php', $heroPhp);
	file_put_contents($doc . '/content/general_pages/epc_fashion_retail_namshi_data.php', $heroPhp . '
function epc_fashion_retail_namshi_category_chips() { return $GLOBALS["__mega_chips"] ?? array(); }
function epc_fashion_retail_namshi_img($k, $w, $h) { return "/chip.png"; }
');
	file_put_contents($doc . '/content/general_pages/epc_jewellery_retail_kiyasha_data.php', $heroPhp);
	file_put_contents($doc . '/content/general_pages/epc_electronics_retail_data.php', $heroPhp);
	file_put_contents(
		$doc . '/content/general_pages/epc_electronicae_storefront.php',
		'<?php function epc_electronicae_product_line_tiles($pdo, $site, $n) { return $GLOBALS["__er_tiles"] ?? array(); } function epc_electronicae_site_key($pdo) { return ""; }'
	);
	file_put_contents(
		$doc . '/content/general_pages/epc_portal_industry_catalog.php',
		'<?php function epc_portal_industry_catalog_categories($p) { return $GLOBALS["__mega_items"] ?? array(); }'
	);

	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$pdo->exec('CREATE TABLE shop_special_searches (`id` INT AUTO_INCREMENT PRIMARY KEY, `active` INT, `order` INT, `alias` VARCHAR(64), `img` VARCHAR(64), `caption` VARCHAR(64))');
	foreach (($case['specials'] ?? array()) as $i => $row) {
		$pdo->prepare('INSERT INTO shop_special_searches (`active`,`order`,`alias`,`img`,`caption`) VALUES (1,?,?,?,?)')->execute(array($i, $row['alias'], $row['img'], $row['caption']));
	}
	$pdo->exec('CREATE TABLE modules (id INT PRIMARY KEY, data TEXT)');
	if (isset($case['module_id'])) {
		$pdo->prepare('INSERT INTO modules VALUES (?, ?)')->execute(array($case['module_id'], json_encode($case['module_data'] ?? array())));
	}
	$pdo->exec('CREATE TABLE content (id INT AUTO_INCREMENT PRIMARY KEY, parent INT, value INT, time_created INT, description_tag INT, url VARCHAR(64))');
	if (isset($case['content_root'])) {
		$pdo->prepare('INSERT INTO content VALUES (?, 0, 0, 0, 0, ?)')->execute(array($case['content_root']['id'], $case['content_root']['url']));
	}
	foreach (($case['news'] ?? array()) as $row) {
		$pdo->prepare('INSERT INTO content VALUES (NULL, ?, ?, ?, ?, ?)')->execute(array(
			$case['module_data']['root_content'], $row['value'], $row['time_created'], $row['description_tag'], $row['url']
		));
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
	$GLOBALS['__staff'] = !empty($case['staff']);
	$GLOBALS['__case_config'] = $case['config'] ?? array();
	$GLOBALS['__hero'] = $hero;
	$GLOBALS['__mega_items'] = $case['mega_items'] ?? array();
	$GLOBALS['__mega_chips'] = $case['mega_chips'] ?? array();
	$GLOBALS['__er_tiles'] = $case['er_tiles'] ?? array();
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_SERVER['HTTP_HOST'] = $case['host'] ?? 'localhost';
	$_GET = $case['get'] ?? array();
	$_POST = array();
	$DP_Config = new stdClass();
	foreach (($case['config'] ?? array()) as $key => $value) {
		$DP_Config->$key = $value;
	}
	$GLOBALS['DP_Config'] = $DP_Config;
	foreach (($case['vars'] ?? array()) as $key => $value) {
		$$key = $value;
	}
	function translate_str_by_id($key, $lang = null) { return '{' . $key . '}'; }
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
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
	foreach (array(
		'content/general_pages/epc_consulting_primeinvest_hero_banner.php',
		'content/general_pages/epc_fashion_retail_namshi_hero_banner.php',
		'content/general_pages/epc_jewellery_retail_kiyasha_hero_banner.php',
		'content/general_pages/epc_electronics_retail_virgin_hero_banner.php',
		'content/general_pages/epc_fashion_retail_namshi_mega_menu.php',
	) as $rel) {
		$srcFile = $root . '/' . $rel;
		if (is_file($srcFile)) {
			$dest = $doc . '/' . $rel;
			@mkdir(dirname($dest), 0777, true);
			copy($srcFile, $dest);
		}
	}

	if (isset($case['file'])) {
		$include = $root . '/' . $case['file'];
		if (str_starts_with($case['file'], 'content/general_pages/')) {
			$include = $doc . '/' . $case['file'];
		}
		if ($case['file'] === 'modules/news/module.php') {
			$src = str_replace('<module_id>', (string) ($case['module_id'] ?? 3), file_get_contents($include));
			$copy = $doc . '/' . $case['file'];
			@mkdir(dirname($copy), 0777, true);
			file_put_contents($copy, $src);
			$include = $copy;
		}
		include $include;
	}
	if (isset($case['eval'])) {
		$GLOBALS['__result'] = eval($case['eval']);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/small_more_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/small_more_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
