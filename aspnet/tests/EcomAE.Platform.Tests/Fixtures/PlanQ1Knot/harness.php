<?php
// PHP 8.3 goldens for plan Q1-knot (metadata handler). Leftover page-url parent stays stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function knot_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function knot_open(string $schema): array
{
	list($user, $pass, $host, $port) = knot_dsn();
	$admin = new PDO("mysql:host={$host};port={$port};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$admin->exec("CREATE DATABASE `{$schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
	$db = new PDO("mysql:host={$host};port={$port};dbname={$schema};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$db->exec('CREATE TABLE metadata_handler_rules (content_id INT, title_rule TEXT, description_rule TEXT)');
	$db->exec('CREATE TABLE text_for_url (url VARCHAR(128), title_tag VARCHAR(255), description_tag VARCHAR(255), keywords_tag VARCHAR(255))');
	return array($admin, $db);
}

function knot_run_page(PDO $db, int $contentId, string $pageUrl, array $get, string $title): array
{
	$GLOBALS['db_link'] = $db;
	$GLOBALS['DP_Content'] = (object) array('id' => $contentId, 'title_tag' => $title, 'description_tag' => 'old-desc', 'keywords_tag' => 'old-kw');
	$GLOBALS['DP_Config'] = (object) array('domain_path' => 'cdn.example');
	$GLOBALS['KNOT_PAGE_URL'] = $pageUrl;
	$_GET = $get;
	$DP_Content = $GLOBALS['DP_Content'];
	$DP_Config = $GLOBALS['DP_Config'];
	$db_link = $db;
	include $GLOBALS['KNOT_PAGE'];
	return array(
		(string) $DP_Content->title_tag,
		(string) $DP_Content->description_tag,
		(string) $DP_Content->keywords_tag,
		(string) ($GLOBALS['KNOT_LAST_URL'] ?? ''),
	);
}

function knot_run_none()
{
	$schema = 'ecomae_cpw_knot_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = knot_open($schema);
	try {
		return knot_run_page($db, 9, '/none', array(), 'Keep');
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function knot_run_url()
{
	$schema = 'ecomae_cpw_knot_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = knot_open($schema);
	try {
		$db->prepare('INSERT INTO metadata_handler_rules VALUES (?,?,?)')->execute(array(
			3,
			json_encode(array(
				'type' => 'url',
				'value' => 'https://%0/t?q=%1&f=%2',
				'args' => array(
					array('type' => 'config', 'value' => 'domain_path'),
					array('type' => 'get', 'value' => 'q'),
					array('type' => 'text', 'value' => 'fixed'),
				),
			)),
			json_encode(array('type' => 'like_title')),
		));
		$GLOBALS['KNOT_HTTP'] = "Title O'Brien";
		return knot_run_page($db, 3, '/p', array('q' => 'brake'), 'Keep');
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function knot_run_complex()
{
	$schema = 'ecomae_cpw_knot_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = knot_open($schema);
	try {
		$db->prepare('INSERT INTO metadata_handler_rules VALUES (?,?,?)')->execute(array(
			4,
			json_encode(array(
				'type' => 'complex',
				'value' => 77,
				'args' => array(array('type' => 'get', 'value' => 'art')),
			)),
			'',
		));
		return knot_run_page($db, 4, '/p', array('art' => 'AB12'), 'Keep');
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function knot_run_override()
{
	$schema = 'ecomae_cpw_knot_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = knot_open($schema);
	try {
		$db->prepare('INSERT INTO metadata_handler_rules VALUES (?,?,?)')->execute(array(
			5,
			json_encode(array('type' => 'complex', 'value' => 1, 'args' => array())),
			json_encode(array('type' => 'like_title')),
		));
		$db->prepare('INSERT INTO text_for_url VALUES (?,?,?,?)')->execute(array('/shop', 'URL title', 'URL desc', 'URL kw'));
		return knot_run_page($db, 5, '/shop', array(), 'Keep');
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function knot_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('getPageUrl')) {
		eval('function getPageUrl() { return (string) ($GLOBALS["KNOT_PAGE_URL"] ?? ""); }');
	}
	if (!function_exists('translate_str_by_id')) {
		eval('function translate_str_by_id($id) { return "t".$id." %0"; }');
	}
	if (!function_exists('curl_init')) {
		eval('function curl_init() { return 1; }');
		eval('function curl_setopt($ch, $opt, $val) { if ((int) $opt === 10002) { $GLOBALS["KNOT_LAST_URL"] = (string) $val; } }');
		eval('function curl_exec($ch) { return (string) ($GLOBALS["KNOT_HTTP"] ?? ""); }');
		eval('function curl_close($ch) {}');
	}
	if (!defined('CURLOPT_URL')) {
		define('CURLOPT_URL', 10002);
		define('CURLOPT_HEADER', 42);
		define('CURLOPT_RETURNTRANSFER', 19913);
		define('CURLOPT_SSL_VERIFYHOST', 81);
		define('CURLOPT_SSL_VERIFYPEER', 64);
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_knot_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	file_put_contents($tmp . '/meta.php', file_get_contents($root . '/plugins/metadata_handler/metadata_handler.php'));
	$GLOBALS['KNOT_PAGE'] = $tmp . '/meta.php';
	knot_boot();
	$fn = 'knot_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/meta.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'))
		. ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1knot_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1knot_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
