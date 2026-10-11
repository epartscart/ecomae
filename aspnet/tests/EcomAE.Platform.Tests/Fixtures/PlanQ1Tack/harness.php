<?php
// PHP 8.3 goldens for plan Q1-tack (product exist limit cron).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function tack_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function tack_open(string $schema): array
{
	list($user, $pass, $host, $port) = tack_dsn();
	$admin = new PDO("mysql:host={$host};port={$port};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$admin->exec("CREATE DATABASE `{$schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
	$db = new PDO("mysql:host={$host};port={$port};dbname={$schema};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$db->exec('CREATE TABLE shop_catalogue_products (
		id INT PRIMARY KEY, category_id INT, caption VARCHAR(64),
		min_limit INT, min_limit_enable VARCHAR(8), min_limit_status VARCHAR(8)
	)');
	$db->exec('CREATE TABLE shop_storages_data (
		id INT AUTO_INCREMENT PRIMARY KEY, storage_id INT, product_id INT, category_id INT,
		price DECIMAL(10,2), exist INT, reserved INT, issued INT
	)');
	return array($admin, $db);
}

function tack_seed(PDO $db, bool $enableLimits): void
{
	$db->exec("INSERT INTO shop_catalogue_products VALUES
		(1, 10, 'Pad', 5, " . ($enableLimits ? "'1'" : "'0'") . ", '1'),
		(2, 10, 'Disc', 2, '1', '1'),
		(3, 11, 'Oil', 8, '1', '1'),
		(4, 12, 'Air', 1, '1', '0')");
	$db->exec("INSERT INTO shop_storages_data (storage_id, product_id, category_id, price, exist, reserved, issued) VALUES
		(7, 1, 10, 1.00, 2, 0, 0),
		(8, 1, 10, 1.00, 1, 0, 0),
		(7, 2, 10, 1.00, 9, 0, 0),
		(7, 3, 99, 1.00, 0, 0, 0),
		(7, 4, 12, 1.00, 0, 0, 0)");
}

function tack_run_script(PDO $db, string $patched, string $cwd): array
{
	$old = getcwd();
	chdir($cwd);
	$db->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_SILENT);
	$GLOBALS['TACK_DB'] = $db;
	$GLOBALS['TACK_ANSWER'] = null;
	require $patched;
	chdir($old);
	$rows = $db->query('SELECT id, min_limit_status FROM shop_catalogue_products ORDER BY id')->fetchAll(PDO::FETCH_ASSOC);
	$log = is_file($cwd . '/cron_update_products_limit_log.txt') ? trim(file_get_contents($cwd . '/cron_update_products_limit_log.txt')) : '';
	$logHasTime = $log !== '' && preg_match('/^[0-9]+$/', $log) ? 1 : 0;
	return array($rows, $logHasTime);
}

function tack_run_reset()
{
	$schema = 'ecomae_cpw_tack_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = tack_open($schema);
	$cwd = sys_get_temp_dir() . '/' . $schema;
	@mkdir($cwd, 0777, true);
	try {
		tack_seed($db, false);
		return tack_run_script($db, $GLOBALS['TACK_PAGE'], $cwd);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
		@unlink($cwd . '/cron_update_products_limit_log.txt');
		@rmdir($cwd);
	}
}

function tack_run_limit()
{
	$schema = 'ecomae_cpw_tack_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = tack_open($schema);
	$cwd = sys_get_temp_dir() . '/' . $schema;
	@mkdir($cwd, 0777, true);
	try {
		tack_seed($db, true);
		return tack_run_script($db, $GLOBALS['TACK_PAGE'], $cwd);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
		@unlink($cwd . '/cron_update_products_limit_log.txt');
		@rmdir($cwd);
	}
}

function tack_run_skip()
{
	$schema = 'ecomae_cpw_tack_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = tack_open($schema);
	$cwd = sys_get_temp_dir() . '/' . $schema;
	@mkdir($cwd, 0777, true);
	try {
		$db->exec("INSERT INTO shop_catalogue_products VALUES (9, 20, 'X', 3, '1', '1')");
		$db->exec("INSERT INTO shop_storages_data (storage_id, product_id, category_id, price, exist, reserved, issued) VALUES (1, 9, 21, 1.00, 0, 0, 0)");
		return tack_run_script($db, $GLOBALS['TACK_PAGE'], $cwd);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
		@unlink($cwd . '/cron_update_products_limit_log.txt');
		@rmdir($cwd);
	}
}

function tack_run_empty()
{
	$schema = 'ecomae_cpw_tack_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = tack_open($schema);
	$cwd = sys_get_temp_dir() . '/' . $schema;
	@mkdir($cwd, 0777, true);
	try {
		$db->exec("INSERT INTO shop_catalogue_products VALUES (5, 1, 'Y', 1, '0', '1')");
		return tack_run_script($db, $GLOBALS['TACK_PAGE'], $cwd);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
		@unlink($cwd . '/cron_update_products_limit_log.txt');
		@rmdir($cwd);
	}
}

function tack_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace(
		"require_once(\$_SERVER[\"DOCUMENT_ROOT\"].\"/config.php\");\n\n\$DP_Config = new DP_Config;//Конфигурация CMS\n//Подключение к БД\ntry\n{\n\t\$db_link = new PDO('mysql:host='.\$DP_Config->host.';dbname='.\$DP_Config->db, \$DP_Config->user, \$DP_Config->password);\n}\ncatch (PDOException \$e) \n{\n    exit(\"No DB connect\");\n}\n\$db_link->query(\"SET NAMES utf8;\");",
		'$db_link = $GLOBALS["TACK_DB"];',
		$code
	);
	$code = str_replace(
		"require_once(\$_SERVER[\"DOCUMENT_ROOT\"].\"/lang/dp_lang.php\");\n\$multilang_params = multilang_init();",
		'$multilang_params = array();',
		$code
	);
	$code = str_replace(
		"\$answer = array('status'=>false, 'message' => translate_str_by_key('1711375224_1_5f735d1486aa51eb9a61df1cd635a0fb'));",
		"\$answer = array('status'=>false, 'message' => 'limit-msg');",
		$code
	);
	file_put_contents($dest, $code);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_tack_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	tack_patch($root . '/content/cron/product_exist_limit.php', $doc . '/cron.php');
	$GLOBALS['TACK_PAGE'] = $doc . '/cron.php';
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	$fn = 'tack_run_' . $case['name'];
	$result = $fn();
	@unlink($doc . '/cron.php');
	@rmdir($doc);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'))
		. ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1tack_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1tack_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
