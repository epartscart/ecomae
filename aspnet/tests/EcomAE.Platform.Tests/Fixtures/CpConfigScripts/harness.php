<?php
// PHP 8.3 runtime goldens for the CP page scripts that answer with one JavaScript assignment (or the public sample CSV):
//   cp/content/shop/prices_upload/epc_storefront_storage_toggle_config.php
//   cp/content/shop/prices_upload/epc_prices_upload_history_config.php
//   cp/content/shop/prices_upload/epc_commerce_cp_config.php
//   cp/content/shop/order_process/orders_items_edit_config.php
//   cp/content/shop/prices_upload/epc_multivendor_sample_file.php
// run unmodified (symlinked) with the real content/users/dp_user.php (DP_User::getAdminSession) and the real
// content/shop/docpart/epc_multivendor_price_ingest.php. Every case runs in its own child process on its own throwaway
// MariaDB database named ecomae_cpw_<random>, dropped in a finally block (and from a shutdown function in case a script exit()s).
//
// Usage: ECOMAE_LOCAL_MARIADB_E2E_DSN=<password of user ecomae> php harness.php <repo root> > golden.json
//
// Stubs (only what the scripts need from the rest of the CMS):
//   config.php                                     class DP_Config pointing at the throwaway database (backend_dir 'cp') and
//                                                  the PDO handle $db_link that the production config.php creates
//   content/general_pages/epc_portal.php           epc_portal_apply_config() as a no-op: it only routes the request to the
//                                                  tenant database, which ASP.NET does in TenantResolutionMiddleware
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
ini_set('display_errors', 'stderr');
ini_set('error_reporting', (string) (E_ALL & ~E_WARNING & ~E_NOTICE & ~E_DEPRECATED));

$scripts = array(
	'storage_toggle' => '/cp/content/shop/prices_upload/epc_storefront_storage_toggle_config.php',
	'upload_history' => '/cp/content/shop/prices_upload/epc_prices_upload_history_config.php',
	'commerce' => '/cp/content/shop/prices_upload/epc_commerce_cp_config.php',
	'order_item_edit' => '/cp/content/shop/order_process/orders_items_edit_config.php',
	'multivendor_sample' => '/cp/content/shop/prices_upload/epc_multivendor_sample_file.php',
);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$docRoot = sys_get_temp_dir() . '/ecomae_cpw_docroot_' . $dbName;
	$cleaned = false;
	$cleanup = function () use (&$cleaned, $admin, $dbName, $docRoot) {
		if ($cleaned) {
			return;
		}
		$cleaned = true;
		$admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`');
		remove_tree($docRoot);
	};
	register_shutdown_function($cleanup);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$result = array('name' => $case['name']);
	$report = function () use (&$result) {
		if (isset($result['php_error'])) {
			return;
		}
		$result['output'] = ob_get_clean();
		echo json_encode($result, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
	};
	try {
		$setup = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		foreach (array_merge($spec['schema'], $spec['base'], $case['setup']) as $sql) {
			$setup->exec($sql);
		}
		$setup = null;
		build_docroot($root, $docRoot, $dbName, $password);

		$_SERVER['DOCUMENT_ROOT'] = $docRoot;
		$_COOKIE = $case['cookie'];
		$_GET = array();
		parse_str($case['query'], $_GET);
		$_POST = array();

		ob_start();
		register_shutdown_function($report);
		include $docRoot . $scripts[$case['script']];
	} catch (Throwable $e) {
		$result['php_error'] = get_class($e);
	} finally {
		if ($result['php_error'] ?? false) {
			@ob_end_clean();
			echo json_encode($result);
		} else {
			// exit() inside the script jumps to the shutdown functions, which report; a script that returns normally falls through here.
			if (ob_get_level() > 0) {
				exit(0);
			}
		}
		$cleanup();
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$out = shell_exec(escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i);
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n");
		exit(1);
	}
	$row['script'] = $case['script'];
	$results[] = $row;
}
echo json_encode(array(
	'php' => PHP_VERSION,
	'source' => 'cp/content/shop/{prices_upload,order_process}/*_config.php + epc_multivendor_sample_file.php',
	'results' => $results,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";

function build_docroot($root, $docRoot, $dbName, $password)
{
	$dirs = array('/cp/content/shop/prices_upload', '/cp/content/shop/order_process', '/content/users', '/content/general_pages', '/content/shop/docpart');
	foreach ($dirs as $dir) {
		mkdir($docRoot . $dir, 0777, true);
	}
	$real = array(
		'/cp/content/shop/prices_upload/epc_storefront_storage_toggle_config.php',
		'/cp/content/shop/prices_upload/epc_prices_upload_history_config.php',
		'/cp/content/shop/prices_upload/epc_commerce_cp_config.php',
		'/cp/content/shop/prices_upload/epc_multivendor_sample_file.php',
		'/cp/content/shop/order_process/orders_items_edit_config.php',
		'/content/users/dp_user.php',
		'/content/shop/docpart/epc_multivendor_price_ingest.php',
	);
	foreach ($real as $file) {
		symlink($root . $file, $docRoot . $file);
	}
	file_put_contents($docRoot . '/config.php', "<?php\nclass DP_Config {\n\tpublic \$host = '127.0.0.1;port=3306';\n\tpublic \$db = " . var_export($dbName, true) . ";\n\tpublic \$user = 'ecomae';\n\tpublic \$password = " . var_export($password, true) . ";\n\tpublic \$backend_dir = '/cp/';\n}\n\$db_link = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . " . var_export($dbName, true) . " . ';charset=utf8', 'ecomae', " . var_export($password, true) . ");\n\$db_link->query('SET NAMES utf8;');\n");
	file_put_contents($docRoot . '/content/general_pages/epc_portal.php', "<?php\nfunction epc_portal_apply_config(\$DP_Config)\n{\n}\n");
}

function remove_tree($path)
{
	if (!file_exists($path) && !is_link($path)) {
		return;
	}
	if (is_link($path) || is_file($path)) {
		@unlink($path);
		return;
	}
	foreach (scandir($path) as $entry) {
		if ($entry !== '.' && $entry !== '..') {
			remove_tree($path . '/' . $entry);
		}
	}
	@rmdir($path);
}
