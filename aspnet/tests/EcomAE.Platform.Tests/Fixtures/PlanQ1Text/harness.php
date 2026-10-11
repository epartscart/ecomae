<?php
// PHP 8.3 goldens for plan Q1-text (stock-brand helpers, tree-list dump, text search).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1t_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/content/shop/docpart', 0777, true);
	@mkdir($doc . '/content/shop/catalogue/tree_lists', 0777, true);
	@mkdir($doc . '/content/shop/order_process', 0777, true);
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	copy($root . '/content/shop/docpart/epc_stock_brands_helpers.php', $doc . '/content/shop/docpart/epc_stock_brands_helpers.php');
	copy($root . '/content/shop/docpart/epc_storefront_storage_flags.php', $doc . '/content/shop/docpart/epc_storefront_storage_flags.php');
	copy($root . '/content/shop/docpart/docpart_manufacturer_synonyms.php', $doc . '/content/shop/docpart/docpart_manufacturer_synonyms.php');
	copy($root . '/content/shop/catalogue/tree_lists/helper.php', $doc . '/content/shop/catalogue/tree_lists/helper.php');
	copy($root . '/content/shop/catalogue/tree_lists/get_tree_list_items.php', $doc . '/content/shop/catalogue/tree_lists/get_tree_list_items.php');
	copy($root . '/content/shop/catalogue/tree_lists/dp_tree_list_item.php', $doc . '/content/shop/catalogue/tree_lists/dp_tree_list_item.php');
	copy($root . '/content/shop/catalogue/text_search_algorithm.php', $doc . '/content/shop/catalogue/text_search_algorithm.php');
	copy($root . '/content/shop/catalogue/cat_lang_general.php', $doc . '/content/shop/catalogue/cat_lang_general.php');
	file_put_contents($doc . '/content/shop/order_process/get_customer_offices.php', "<?php\n\$customer_offices = isset(\$GLOBALS['__customer_offices']) && is_array(\$GLOBALS['__customer_offices']) ? \$GLOBALS['__customer_offices'] : array();\n");
	file_put_contents($doc . '/content/shop/docpart/docpart_genuine_manufacturers.php', "<?php\nfunction epc_genuine_build_frontend_index(\$db_link, \$DP_Config, \$catalog_url) {\n\treturn isset(\$GLOBALS['__genuine_index']) && is_array(\$GLOBALS['__genuine_index']) ? \$GLOBALS['__genuine_index'] : array('brands'=>array(),'meta'=>array());\n}\n");
	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $file) {
			$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
		}
		@rmdir($doc);
	};
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['__doc'] = $doc;
	$GLOBALS['DP_Config'] = (object) array();
	register_shutdown_function(function () use ($cleanup) {
		$cleanup();
	});
	if (isset($case['eval'])) {
		$result = eval($case['eval']);
		echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1t_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1t_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
