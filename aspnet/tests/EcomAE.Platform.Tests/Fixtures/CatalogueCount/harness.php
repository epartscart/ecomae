<?php
// PHP 8.3 runtime goldens for the storefront catalogue product COUNT endpoint:
//   content/shop/catalogue/ajax_get_products_count.php
// run unmodified (symlinked) together with the real
//   content/shop/catalogue/query_builder/query_products_all.php
//   content/shop/order_process/get_customer_offices.php
//   content/shop/catalogue/text_search_algorithm.php (+ cat_lang_general.php)
// Every case runs in its own child process on its own throwaway MariaDB database named ecomae_cpw_<random>,
// which is dropped in a finally block (and from a shutdown function in case a script exit()s).
//
// Usage: ECOMAE_LOCAL_MARIADB_E2E_DSN=<password of user ecomae> php harness.php /workspace > golden.json
//
// Stubs (only what the scripts need from the rest of the CMS):
//   config.php               class DP_Config pointing at the throwaway database
//   content/users/dp_user.php DP_User::getUserProfile() with the real group lookup rules (guest group / bound groups /
//                            first registered group) for the case's user_id
//   $multilang_params        array('lang' => <case lang, default en>)
// A case that makes PHP throw (PDOException, TypeError ...) is recorded as {"php_error": <class>} instead of "output".
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);
ini_set('display_errors', 'stderr');
ini_set('error_reporting', (string) (E_ALL & ~E_WARNING & ~E_NOTICE & ~E_DEPRECATED));

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
	try {
		$setup = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
		foreach (array_merge($spec['schema'], $spec['base'], $case['setup']) as $sql) {
			$setup->exec($sql);
		}
		$setup = null;
		build_docroot($root, $docRoot, $dbName, $password);

		$_SERVER['DOCUMENT_ROOT'] = $docRoot;
		$_COOKIE = array();
		if (array_key_exists('cookie', $case) && $case['cookie'] !== null) {
			$_COOKIE['my_city'] = $case['cookie'];
		}
		$GLOBALS['case_user_id'] = (int) ($case['user_id'] ?? 0);
		$multilang_params = array('lang' => $case['lang'] ?? 'en', 'lang_href' => '/en');
		$_POST = array('propucts_request' => array_key_exists('request_raw', $case) ? $case['request_raw'] : json_encode($case['request']));

		ob_start();
		try {
			include $docRoot . '/content/shop/catalogue/ajax_get_products_count.php';
			$result['output'] = ob_get_clean();
		} catch (Throwable $e) {
			ob_end_clean();
			$result['php_error'] = get_class($e);
		}
	} finally {
		$cleanup();
	}
	echo json_encode($result, JSON_UNESCAPED_UNICODE);
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
	$results[] = $row;
}
echo json_encode(array(
	'php' => PHP_VERSION,
	'source' => 'content/shop/catalogue/ajax_get_products_count.php',
	'results' => $results,
), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";

function build_docroot($root, $docRoot, $dbName, $password)
{
	$dirs = array('/content/users', '/content/shop/catalogue/query_builder', '/content/shop/order_process');
	foreach ($dirs as $dir) {
		mkdir($docRoot . $dir, 0777, true);
	}
	$real = array(
		'/content/shop/catalogue/ajax_get_products_count.php',
		'/content/shop/catalogue/query_builder/query_products_all.php',
		'/content/shop/catalogue/text_search_algorithm.php',
		'/content/shop/catalogue/cat_lang_general.php',
		'/content/shop/order_process/get_customer_offices.php',
	);
	foreach ($real as $file) {
		symlink($root . $file, $docRoot . $file);
	}
	file_put_contents($docRoot . '/config.php', "<?php\nclass DP_Config {\n\tpublic \$host = '127.0.0.1;port=3306';\n\tpublic \$db = " . var_export($dbName, true) . ";\n\tpublic \$user = 'ecomae';\n\tpublic \$password = " . var_export($password, true) . ";\n}\n");
	file_put_contents($docRoot . '/content/users/dp_user.php', <<<'PHP'
<?php
class DP_User
{
	public static function getUserProfile()
	{
		global $db_link;
		$user_id = (int) $GLOBALS['case_user_id'];
		if ($user_id == 0) {
			$q = $db_link->prepare('SELECT * FROM `groups` WHERE `for_guests`=?;');
			$q->execute(array(1));
			$r = $q->fetch();
			return array('user_id' => 0, 'groups' => array($r["id"]));
		}
		$groups = array();
		$q = $db_link->prepare('SELECT * FROM `users_groups_bind` WHERE `user_id` = ?;');
		$q->execute(array($user_id));
		while ($r = $q->fetch()) {
			array_push($groups, $r["group_id"]);
		}
		if (count($groups) == 0) {
			$q = $db_link->prepare('SELECT `id` FROM `groups` WHERE `for_registrated` = 1 ORDER BY `id` ASC LIMIT 1;');
			$q->execute();
			$r = $q->fetch();
			if ($r != false) {
				$groups = array((int) $r['id']);
			}
		}
		return array('user_id' => $user_id, 'groups' => $groups);
	}
}
PHP
	);
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
