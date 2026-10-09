<?php
// Runs the authoritative PHP storefront fragments listed in cases.json (one process per case) and records their output.
// A temporary DOCUMENT_ROOT holds stubs for DP_User (guest/user with the case csrf key), lang/dp_lang.php and the login
// form include; translate_str_by_id() prints "{key}". Cases that name a "schema" get a throwaway ecomae_cpw_* MariaDB
// database, dropped afterwards. Case keys: file, get, csrf, user_id, lang_href, config, content_service_data, schema,
// setup, vars (globals set before the include), module_id (replaces <module_id> in the file), eval (PHP run after the
// include; its return value is recorded as "result"), vars_out (globals recorded afterwards), show_create (tables).
// Usage: ECOMAE_LOCAL_MARIADB_E2E_DSN=... php harness.php /workspace > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function stub_pdo($name)
{
	return new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $name . ';charset=utf8mb4', 'ecomae', getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'), array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	$doc = sys_get_temp_dir() . '/ecomae_cpw_frag_' . substr(md5(uniqid('', true)), 0, 12);
	foreach (array('/content/users', '/content/shop/catalogue/tree_lists', '/content/general_pages', '/lang', '/modules/login') as $dir) {
		mkdir($doc . $dir, 0777, true);
	}
	file_put_contents($doc . '/content/users/dp_user.php', '<?php if (!class_exists("DP_User")) { class DP_User { public static function getUserId() { return (int) $GLOBALS["__case_user_id"]; } public static function getUserSession() { return $GLOBALS["__case_csrf"] === null ? false : array("csrf_guard_key" => $GLOBALS["__case_csrf"]); } } }');
	file_put_contents($doc . '/lang/dp_lang.php', '<?php function multilang_init() { return $GLOBALS["multilang_params"]; }');
	file_put_contents($doc . '/modules/login/login_form_general.php', '<?php echo "[[LOGIN_FORM " . $login_form_postfix . " " . $login_form_target . "]]";');
	symlink($root . '/content/shop/catalogue/tree_lists/helper.php', $doc . '/content/shop/catalogue/tree_lists/helper.php');
	symlink($root . '/content/general_pages/epc_eparts_product_route.php', $doc . '/content/general_pages/epc_eparts_product_route.php');

	$admin = null;
	$dbName = null;
	if (!empty($case['schema'])) {
		$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
		$admin->exec('CREATE DATABASE `' . $dbName . '`');
	}
	$cleanup = function () use ($admin, $dbName, $doc) {
		if ($admin) {
			$admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`');
		}
		foreach (array('/content/users/dp_user.php', '/lang/dp_lang.php', '/modules/login/login_form_general.php', '/content/shop/catalogue/tree_lists/helper.php', '/content/general_pages/epc_eparts_product_route.php', '/module.php') as $file) {
			@unlink($doc . $file);
		}
		foreach (array('/content/users', '/content/shop/catalogue/tree_lists', '/content/shop/catalogue', '/content/shop', '/content/general_pages', '/content', '/lang', '/modules/login', '/modules', '') as $dir) {
			@rmdir($doc . $dir);
		}
	};

	$GLOBALS['__case_user_id'] = $case['user_id'] ?? 0;
	$GLOBALS['__case_csrf'] = array_key_exists('csrf', $case) ? $case['csrf'] : null;
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_SERVER['REQUEST_METHOD'] = 'GET';
	$_GET = $case['get'] ?? array();
	$_POST = array();
	$_COOKIE = array();
	$DP_Config = new stdClass();
	foreach (($case['config'] ?? array()) as $key => $value) {
		$DP_Config->$key = $value;
	}
	$DP_Content = new stdClass();
	$DP_Content->service_data = $case['content_service_data'] ?? array();
	$multilang_params = array('lang_href' => array_key_exists('lang_href', $case) ? $case['lang_href'] : '/en', 'lang' => $case['lang'] ?? 'en');
	if ($dbName !== null) {
		try {
			$db_link = stub_pdo($dbName);
			foreach (array_merge($case['schema'], $case['setup'] ?? array()) as $sql) {
				$db_link->exec($sql);
			}
		} catch (Throwable $e) {
			$cleanup();
			throw $e;
		}
	}
	foreach (($case['vars'] ?? array()) as $key => $value) {
		$$key = $value;
	}
	function translate_str_by_id($key) { return '{' . $key . '}'; }
	define('_ASTEXE_', 1);
	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$vars = array();
		foreach (($case['vars_out'] ?? array()) as $name) {
			$vars[$name] = $GLOBALS[$name] ?? null;
		}
		$show = array();
		if (!empty($case['show_create']) && isset($GLOBALS['db_link'])) {
			foreach ($case['show_create'] as $table) {
				$show[$table] = $GLOBALS['db_link']->query('SHOW CREATE TABLE `' . $table . '`')->fetch(PDO::FETCH_NUM)[1];
			}
		}
		$headers = array();
		foreach (headers_list() as $header) {
			$headers[] = preg_replace('/Expires=[^;]+/i', 'Expires=<date>', preg_replace('/Max-Age=\d+/i', 'Max-Age=<n>', $header));
		}
		$cleanup();
		echo json_encode(
			array('name' => $case['name'], 'output' => $html, 'vars' => $vars, 'result' => $GLOBALS['__result'] ?? null, 'show_create' => $show, 'headers' => $headers),
			JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR
		);
	});
	ob_start();
	if (isset($case['file'])) {
		$source = $root . '/' . $case['file'];
		if (isset($case['module_id'])) {
			$source = $doc . '/module.php';
			file_put_contents($source, str_replace('<module_id>', (string) $case['module_id'], file_get_contents($root . '/' . $case['file'])));
		}
		if (!empty($case['preload'])) {
			require_once $root . '/' . $case['preload'];
		}
		include $source;
	}
	if (isset($case['eval'])) {
		$GLOBALS['__result'] = eval($case['eval']);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$out = shell_exec(escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i . ' 2>/dev/null');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
