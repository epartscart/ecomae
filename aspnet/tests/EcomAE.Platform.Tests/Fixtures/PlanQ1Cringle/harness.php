<?php
// PHP 8.3 goldens for plan Q1-cringle (alternative bread crumbs).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function cringle_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function cringle_open(string $schema): array
{
	list($user, $pass, $host, $port) = cringle_dsn();
	$admin = new PDO("mysql:host={$host};port={$port};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$admin->exec("CREATE DATABASE `{$schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
	$db = new PDO("mysql:host={$host};port={$port};dbname={$schema};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$db->exec('CREATE TABLE bread_crumbs_rules (
		url VARCHAR(128), bread_crumb VARCHAR(128),
		bread_crumb_caption TEXT, bread_crumb_href_args TEXT
	)');
	return array($admin, $db);
}

function cringle_seed(PDO $db): void
{
	$db->prepare('INSERT INTO bread_crumbs_rules VALUES (?,?,?,?)')->execute(array(
		'/shop/parts',
		'/catalogue',
		json_encode(array('type' => 'text', 'value' => "O'Brien parts")),
		json_encode(array(array('name' => 'q', 'type' => 'text', 'value' => 'brake'), array('name' => 'lang', 'type' => 'get', 'value' => 'lang'))),
	));
	$db->prepare('INSERT INTO bread_crumbs_rules VALUES (?,?,?,?)')->execute(array(
		'/shop/item',
		'/item',
		json_encode(array('type' => 'get', 'value' => 'title')),
		'',
	));
	$db->prepare('INSERT INTO bread_crumbs_rules VALUES (?,?,?,?)')->execute(array(
		'/shop/ext',
		'/ext',
		json_encode(array(
			'type' => 'url',
			'value' => 'https://%0/label?x=%1&f=%2',
			'args' => array(
				array('type' => 'config', 'value' => 'domain_path'),
				array('type' => 'get', 'value' => 'code'),
				array('type' => 'text', 'value' => 'fixed'),
			),
		)),
		json_encode(array(array('name' => 'id', 'type' => 'get', 'value' => 'code'))),
	));
	$db->prepare('INSERT INTO bread_crumbs_rules VALUES (?,?,?,?)')->execute(array(
		'/shop/empty',
		'/empty',
		'',
		'',
	));
}

function cringle_run_none()
{
	$schema = 'ecomae_cpw_cringle_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = cringle_open($schema);
	try {
		cringle_seed($db);
		$GLOBALS['db_link'] = $db;
		$GLOBALS['DP_Config'] = (object) array('domain_path' => 'cdn.example');
		$_GET = array();
		return array(get_alternative_bread_crumbs('/nope', '/x'), get_alternative_bread_crumbs('/shop/parts', '/wrong'));
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function cringle_run_text()
{
	$schema = 'ecomae_cpw_cringle_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = cringle_open($schema);
	try {
		cringle_seed($db);
		$GLOBALS['db_link'] = $db;
		$GLOBALS['DP_Config'] = (object) array('domain_path' => 'cdn.example');
		$_GET = array('lang' => 'ar');
		return get_alternative_bread_crumbs('/shop/parts', '/catalogue');
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function cringle_run_geturl()
{
	$schema = 'ecomae_cpw_cringle_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = cringle_open($schema);
	try {
		cringle_seed($db);
		$GLOBALS['db_link'] = $db;
		$GLOBALS['DP_Config'] = (object) array('domain_path' => 'cdn.example');
		$_GET = array('title' => 'Pad <b>', 'code' => 'AB12');
		$get = get_alternative_bread_crumbs('/shop/item', '/item');
		$GLOBALS['CRINGLE_HTTP'] = "Remote O'label";
		$url = get_alternative_bread_crumbs('/shop/ext', '/ext');
		return array($get, $url, $GLOBALS['CRINGLE_LAST_URL'] ?? '');
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function cringle_run_href()
{
	$schema = 'ecomae_cpw_cringle_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = cringle_open($schema);
	try {
		cringle_seed($db);
		$GLOBALS['db_link'] = $db;
		$GLOBALS['DP_Config'] = (object) array('domain_path' => 'cdn.example');
		$_GET = array();
		return get_alternative_bread_crumbs('/shop/empty', '/empty');
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function cringle_patch(string $src, string $dest): void
{
	file_put_contents($dest, file_get_contents($src));
}

function cringle_stub_curl(): void
{
	if (!function_exists('curl_init')) {
		eval('function curl_init() { return 1; }');
		eval('function curl_setopt($ch, $opt, $val) { if ((int) $opt === 10002) { $GLOBALS["CRINGLE_LAST_URL"] = (string) $val; } }');
		eval('function curl_exec($ch) { return (string) ($GLOBALS["CRINGLE_HTTP"] ?? ""); }');
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
	$tmp = sys_get_temp_dir() . '/ecomae_cringle_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	cringle_patch($root . '/modules/bread_crumbs/helper.php', $tmp . '/helper.php');
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	cringle_stub_curl();
	require $tmp . '/helper.php';
	$fn = 'cringle_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/helper.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'))
		. ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1cringle_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1cringle_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
