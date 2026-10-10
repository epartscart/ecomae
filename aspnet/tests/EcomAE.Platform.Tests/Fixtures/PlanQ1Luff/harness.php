<?php
// PHP 8.3 goldens for plan Q1-luff (auto-parts demo bootstrap). Leftover portal-demo parent stays stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function luff_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function luff_admin(): PDO
{
	list($user, $pass, $host, $port) = luff_dsn();
	return new PDO("mysql:host={$host};port={$port};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function luff_open(PDO $admin, string $schema): PDO
{
	list($user, $pass, $host, $port) = luff_dsn();
	$admin->exec("CREATE DATABASE `{$schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
	return new PDO("mysql:host={$host};port={$port};dbname={$schema};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
}

function luff_slim_verify(array $row): array
{
	return array(
		'ok' => !empty($row['ok']) ? 1 : 0,
		'location' => !empty($row['location']) ? 1 : 0,
		'hours' => !empty($row['hours']) ? 1 : 0,
		'geo_dubai' => !empty($row['geo_dubai']) ? 1 : 0,
		'geo_error' => empty($row['geo_error']) ? 0 : 1,
		'office_error' => empty($row['office_error']) ? 0 : 1,
	);
}

function luff_reset(): void
{
	$GLOBALS['LUFF_SRC'] = null;
	$GLOBALS['LUFF_CLONE_ERR'] = array();
	$GLOBALS['LUFF_CLONED'] = array();
	$GLOBALS['LUFF_PRESET'] = '';
}

function luff_run_preset()
{
	luff_reset();
	$real = epc_demo_autoparts_bootstrap_preset();
	$tables = epc_demo_autoparts_bootstrap_clone_tables();
	$missing = sys_get_temp_dir() . '/ecomae_luff_missing_' . substr(md5(uniqid('', true)), 0, 8) . '.json';
	$GLOBALS['LUFF_PRESET'] = $missing;
	$gone = epc_demo_autoparts_bootstrap_preset();
	$empty = sys_get_temp_dir() . '/ecomae_luff_empty_' . substr(md5(uniqid('', true)), 0, 8) . '.json';
	file_put_contents($empty, '');
	$GLOBALS['LUFF_PRESET'] = $empty;
	$blank = epc_demo_autoparts_bootstrap_preset();
	$bad = sys_get_temp_dir() . '/ecomae_luff_bad_' . substr(md5(uniqid('', true)), 0, 8) . '.json';
	file_put_contents($bad, '{');
	$GLOBALS['LUFF_PRESET'] = $bad;
	$invalid = epc_demo_autoparts_bootstrap_preset();
	$custom = sys_get_temp_dir() . '/ecomae_luff_custom_' . substr(md5(uniqid('', true)), 0, 8) . '.json';
	file_put_contents($custom, json_encode(array(
		'docpart_clone_tables' => array('shop_geo', 'shop_offices`x'),
		'header_verify' => array('location' => "O'man", 'hours' => 'Sat-Sun'),
	)));
	$GLOBALS['LUFF_PRESET'] = $custom;
	$over = epc_demo_autoparts_bootstrap_preset();
	$overTables = epc_demo_autoparts_bootstrap_clone_tables();
	$GLOBALS['LUFF_PRESET'] = '';
	@unlink($empty);
	@unlink($bad);
	@unlink($custom);
	return array(
		basename(epc_demo_autoparts_bootstrap_preset_path()),
		$real['id'] ?? '',
		$real['header_verify'] ?? array(),
		count($tables),
		$tables[0] ?? '',
		$tables[count($tables) - 1] ?? '',
		$gone,
		$blank,
		$invalid,
		$over['header_verify'] ?? array(),
		$overTables,
	);
}

function luff_run_verify()
{
	luff_reset();
	$admin = luff_admin();
	$schema = 'ecomae_cpw_luff_' . substr(md5(uniqid('', true)), 0, 8);
	$db = luff_open($admin, $schema);
	try {
		$missing = luff_slim_verify(epc_demo_autoparts_bootstrap_verify_db($db, array('location' => 'Dubai', 'hours' => 'Mon-Fri from 9:00')));
		$db->exec('CREATE TABLE shop_geo (id INT PRIMARY KEY, name VARCHAR(64))');
		$db->exec('CREATE TABLE shop_offices (id INT PRIMARY KEY, city VARCHAR(64), timetable VARCHAR(128))');
		$db->exec('CREATE TABLE lang_text_strings_translation (id INT AUTO_INCREMENT PRIMARY KEY, value TEXT)');
		$noGeo = luff_slim_verify(epc_demo_autoparts_bootstrap_verify_db($db, array()));
		$db->exec("INSERT INTO shop_geo VALUES (3, 'Dubai')");
		$db->exec("INSERT INTO lang_text_strings_translation (value) VALUES ('Hours: Mon-Fri from 9:00 GST')");
		$ok = luff_slim_verify(epc_demo_autoparts_bootstrap_verify_db($db, array('location' => 'Dubai', 'hours' => 'Mon-Fri from 9:00')));
		$db->exec('DELETE FROM shop_geo');
		$db->exec("INSERT INTO shop_geo VALUES (1, 'A'), (2, 'B')");
		$db->exec('DELETE FROM lang_text_strings_translation');
		$db->exec("INSERT INTO shop_offices VALUES (1, \"O'man City\", 'Open daily')");
		$office = luff_slim_verify(epc_demo_autoparts_bootstrap_verify_db($db, array('location' => "O'man", 'hours' => 'Mon-Fri from 9:00')));
		$db->exec("INSERT INTO lang_text_strings_translation (value) VALUES ('100%_open')");
		$like = luff_slim_verify(epc_demo_autoparts_bootstrap_verify_db($db, array('location' => "O'man", 'hours' => '100%_open')));
		return array($missing, $noGeo, $ok, $office, $like);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function luff_run_apply()
{
	luff_reset();
	$admin = luff_admin();
	$srcName = 'ecomae_cpw_luff_' . substr(md5(uniqid('', true)), 0, 8);
	$dstName = 'ecomae_cpw_luff_' . substr(md5(uniqid('', true)), 0, 8);
	$src = luff_open($admin, $srcName);
	$dst = luff_open($admin, $dstName);
	try {
		foreach (array($src, $dst) as $db) {
			$db->exec('CREATE TABLE content (id INT PRIMARY KEY, main_flag INT, published_flag INT, is_frontend INT, modules_array TEXT)');
			$db->exec('CREATE TABLE shop_catalogue_categories (id INT PRIMARY KEY, published_flag INT, parent INT)');
			$db->exec('CREATE TABLE shop_geo (id INT PRIMARY KEY, name VARCHAR(64))');
			$db->exec('CREATE TABLE shop_offices (id INT PRIMARY KEY, city VARCHAR(64), timetable VARCHAR(128))');
			$db->exec('CREATE TABLE lang_text_strings_translation (id INT AUTO_INCREMENT PRIMARY KEY, value TEXT)');
		}
		$src->exec("INSERT INTO content VALUES (1, 1, 1, 1, '[1,2]')");
		$src->exec("INSERT INTO shop_geo VALUES (3, 'Dubai')");
		$dst->exec("INSERT INTO content VALUES (1, 1, 1, 1, '[]')");
		$dst->exec("INSERT INTO lang_text_strings_translation (value) VALUES ('Mon-Fri from 9:00')");
		$GLOBALS['LUFF_SRC'] = $src;
		$incremental = epc_demo_autoparts_bootstrap_apply($dst, false);
		$clonedInc = $GLOBALS['LUFF_CLONED'];
		$GLOBALS['LUFF_CLONED'] = array();
		$dst->exec("INSERT INTO shop_geo VALUES (3, 'Dubai')");
		$force = epc_demo_autoparts_bootstrap_apply($dst, true);
		return array(
			$incremental['ok'] ? 1 : 0,
			$incremental['force'] ? 1 : 0,
			$incremental['preset'] ?? '',
			$incremental['home_modules'] ? 1 : 0,
			$incremental['root_categories'],
			$incremental['geo_nodes'],
			$incremental['offices'],
			$incremental['message'] ?? '',
			$incremental['verify']['ok'] ? 1 : 0,
			count($clonedInc),
			count($clonedInc[0] ?? array()),
			$force['force'] ? 1 : 0,
			count($GLOBALS['LUFF_CLONED']),
			count($GLOBALS['LUFF_CLONED'][0] ?? array()),
			(int) $dst->query('SELECT COUNT(*) FROM shop_geo')->fetchColumn(),
		);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$srcName}`");
		$admin->exec("DROP DATABASE IF EXISTS `{$dstName}`");
	}
}

function luff_run_fail()
{
	luff_reset();
	$admin = luff_admin();
	$schema = 'ecomae_cpw_luff_' . substr(md5(uniqid('', true)), 0, 8);
	$db = luff_open($admin, $schema);
	try {
		$GLOBALS['LUFF_SRC'] = null;
		$noSrc = epc_demo_autoparts_bootstrap_apply($db, false);
		$db->exec('CREATE TABLE shop_geo (id INT PRIMARY KEY, name VARCHAR(64))');
		$db->exec('CREATE TABLE shop_offices (id INT PRIMARY KEY, city VARCHAR(64), timetable VARCHAR(128))');
		$db->exec('CREATE TABLE lang_text_strings_translation (id INT AUTO_INCREMENT PRIMARY KEY, value TEXT)');
		$db->exec('CREATE TABLE content (id INT PRIMARY KEY, main_flag INT, published_flag INT, is_frontend INT, modules_array TEXT)');
		$db->exec('CREATE TABLE shop_catalogue_categories (id INT PRIMARY KEY, published_flag INT, parent INT)');
		$srcName = 'ecomae_cpw_luff_' . substr(md5(uniqid('', true)), 0, 8);
		$src = luff_open($admin, $srcName);
		$GLOBALS['LUFF_SRC'] = $src;
		$GLOBALS['LUFF_CLONE_ERR'] = array('clone failed');
		$err = epc_demo_autoparts_bootstrap_apply($db, true);
		$admin->exec("DROP DATABASE IF EXISTS `{$srcName}`");
		return array(
			$noSrc['ok'] ? 1 : 0,
			$noSrc['message'] ?? '',
			$err['ok'] ? 1 : 0,
			$err['cloned']['errors'] ?? array(),
			$err['verify']['ok'] ? 1 : 0,
			$err['message'] ?? '',
		);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function luff_patch(string $src, string $dest, string $root): void
{
	$code = file_get_contents($src);
	$code = str_replace(
		"return __DIR__ . '/content/general_pages/epc_theme_presets/automotive_spareparts_pro.json';",
		'if (!empty($GLOBALS["LUFF_PRESET"])) { return (string) $GLOBALS["LUFF_PRESET"]; } return rtrim((string) ($GLOBALS["LUFF_ROOT"] ?? ""), "/") . "/content/general_pages/epc_theme_presets/automotive_spareparts_pro.json";',
		$code
	);
	$code = str_replace(
		"require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';\n\t\t\$srcCreds = epc_portal_resolve_tenant_db_credentials();\n\t\trequire_once \$_SERVER['DOCUMENT_ROOT'] . '/config.php';\n\t\t\$cfg = new DP_Config();\n\t\treturn new PDO(\n\t\t\t'mysql:host=' . \$cfg->host . ';dbname=' . \$srcCreds['db'] . ';charset=utf8',\n\t\t\t\$srcCreds['user'],\n\t\t\t\$srcCreds['password'],\n\t\t\tarray(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)\n\t\t);",
		'$src = $GLOBALS["LUFF_SRC"] ?? null; return $src instanceof PDO ? $src : null;',
		$code
	);
	$code = str_replace(
		"if (!function_exists('epc_portal_demo_php_clone_tables')) {\n\t\trequire_once __DIR__ . '/content/general_pages/epc_portal_demo.php';\n\t}",
		'// leftover clone parent stubbed',
		$code
	);
	file_put_contents($dest, $code);
}

function luff_boot(string $root): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	$GLOBALS['LUFF_ROOT'] = $root;
	if (!function_exists('epc_portal_demo_php_clone_tables')) {
		eval('function epc_portal_demo_php_clone_tables($src, $dst, $tables) {
			$GLOBALS["LUFF_CLONED"][] = array_values($tables);
			if (!empty($GLOBALS["LUFF_CLONE_ERR"])) {
				return array("ok" => false, "tables" => array(), "errors" => $GLOBALS["LUFF_CLONE_ERR"]);
			}
			foreach ($tables as $tbl) {
				$name = str_replace("`", "", (string) $tbl);
				try {
					$dst->query("SELECT COUNT(*) FROM `" . $name . "`");
				} catch (Exception $e) {
					$dst->exec("CREATE TABLE `" . $name . "` (id INT PRIMARY KEY)");
				}
			}
			return array("ok" => true, "tables" => array_values($tables), "errors" => array());
		}');
	}
	luff_reset();
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_luff_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	luff_patch($root . '/epc_demo_autoparts_bootstrap.php', $doc . '/bootstrap.php', $root);
	luff_boot($root);
	require $doc . '/bootstrap.php';
	$fn = 'luff_run_' . $case['name'];
	$result = $fn();
	@unlink($doc . '/bootstrap.php');
	@rmdir($doc);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'))
		. ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1luff_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1luff_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
