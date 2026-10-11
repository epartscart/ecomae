<?php
// PHP 8.3 goldens for plan Q1 named helpers.
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1_' . substr(md5(uniqid('', true)), 0, 12);
	foreach (array(
		'/content/general_pages',
		'/content/shop/docpart/cache',
		'/content/files/epc_cache',
		'/cp',
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
	$pdo->exec('CREATE TABLE `shop_storages` (`id` INT PRIMARY KEY, `name` VARCHAR(64), `short_name` VARCHAR(32), `currency` VARCHAR(8), `connection_options` TEXT, `storefront_temp_disabled` INT)');
	$pdo->exec('CREATE TABLE `shop_currencies` (`iso_code` VARCHAR(8) PRIMARY KEY, `rate` DECIMAL(10,4))');
	$pdo->exec('CREATE TABLE `shop_offices` (`id` INT PRIMARY KEY, `caption` VARCHAR(64), `users` VARCHAR(128))');
	$pdo->exec('CREATE TABLE `shop_offices_storages_map` (`office_id` INT, `storage_id` INT, `group_id` INT, `additional_time` INT, `min_point` INT, `max_point` INT, `markup` DECIMAL(10,4))');
	$pdo->exec("INSERT INTO `shop_currencies` VALUES ('AED', 1.0000)");
	$pdo->exec("INSERT INTO `shop_storages` VALUES (10,'Warehouse','WH','AED','{\"price_id\":5,\"probability\":90,\"color\":\"#f00\"}',0),(11,'Closed','CL','AED','{\"price_id\":6}',1),(12,'NoPrice','NP','AED','{}',0)");
	$pdo->exec("INSERT INTO `shop_offices` VALUES (1,'Dubai','[\"3\"]'),(2,'Sharjah','[]')");
	$pdo->exec("INSERT INTO `shop_offices_storages_map` VALUES (1,10,7,48,0,100,20),(1,10,7,48,100,999,15)");
	$pdo->exec('CREATE TABLE `control_groups` (`id` INT, `name` VARCHAR(32), `order` INT)');
	$pdo->exec('CREATE TABLE `control_items` (`id` INT, `name` VARCHAR(32), `order` INT, `group_id` INT)');
	$pdo->exec("INSERT INTO `control_groups` VALUES (1,'Shop',1)");
	$pdo->exec("INSERT INTO `control_items` VALUES (2,'Orders',1,1)");
	$pdo->exec('CREATE TABLE `epc_umapi_manufacturers` (`manufacturer` VARCHAR(64), `section` VARCHAR(32))');
	$pdo->exec("INSERT INTO `epc_umapi_manufacturers` VALUES ('Bosch','passenger'),('Bosch','commercial'),('Febi','passenger'),('','motorbike')");
	$pdo->exec('CREATE TABLE `shop_docpart_manufacturers` (`id` INT PRIMARY KEY, `name` VARCHAR(64))');
	$pdo->exec('CREATE TABLE `shop_docpart_manufacturers_synonyms` (`manufacturer_id` INT, `synonym` VARCHAR(64))');
	$pdo->exec("INSERT INTO `shop_docpart_manufacturers` VALUES (1,'Bosch'),(2,'Febi')");
	$pdo->exec("INSERT INTO `shop_docpart_manufacturers_synonyms` VALUES (1,'BOSCH-K')");

	file_put_contents($doc . '/content/general_pages/epc_portal.php', '<?php
if (!function_exists("epc_portal_site_profile")) { function epc_portal_site_profile() { return $GLOBALS["__site"] ?? array(); } }
if (!function_exists("epc_portal_host")) { function epc_portal_host() { return $GLOBALS["__host"] ?? ""; } }
if (!function_exists("epc_portal_is_client_hostname")) { function epc_portal_is_client_hostname() { return !empty($GLOBALS["__client_host"]); } }
');
	@mkdir($doc . '/content/shop/docpart', 0777, true);
	copy($root . '/content/shop/docpart/docpart_genuine_manufacturers.php', $doc . '/content/shop/docpart/docpart_genuine_manufacturers.php');
	copy($root . '/content/shop/docpart/docpart_manufacturer_synonyms.php', $doc . '/content/shop/docpart/docpart_manufacturer_synonyms.php');

	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $file) {
			$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
		}
		@rmdir($doc);
	};

	$GLOBALS['__root'] = $root;
	$GLOBALS['__doc'] = $doc;
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$db_link = $pdo;
	$GLOBALS['db_link'] = $pdo;
	$GLOBALS['DP_Config'] = (object) array('backend_dir' => 'cp');

	$name = $case['name'];
	if (strpos($name, 'boot_') === 0) {
		require $root . '/cp/epc_cp_bootstrap_light.php';
	} elseif (strpos($name, 'dep_') === 0) {
		require $root . '/epc_deploy_auth.php';
	} elseif (strpos($name, 'bos_') === 0) {
		require $root . '/content/general_pages/epc_bos_security.php';
	} elseif (strpos($name, 'ref_') === 0) {
		require $root . '/content/general_pages/epc_php_reference_router.php';
	} elseif (strpos($name, 'par_') === 0) {
		require $root . '/content/general_pages/epc_cp_common_parity.php';
	} elseif (strpos($name, 'meta_') === 0) {
		require $root . '/content/shop/docpart/epc_prices_office_storage_meta.php';
	} elseif (strpos($name, 'cache_') === 0) {
		require $root . '/content/general_pages/epc_perf_cache.php';
	} elseif (strpos($name, 'brand_') === 0) {
		require $root . '/content/general_pages/epc_portal_tenant_brand.php';
	} elseif (strpos($name, 'gen_') === 0) {
		require $doc . '/content/shop/docpart/docpart_genuine_manufacturers.php';
	}

	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$cleanup();
		echo json_encode(
			array(
				'name' => $case['name'],
				'output' => $html,
				'result' => $GLOBALS['__result'] ?? null,
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
