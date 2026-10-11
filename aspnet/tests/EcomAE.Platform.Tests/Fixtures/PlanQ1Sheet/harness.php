<?php
// PHP 8.3 goldens for plan Q1-sheet (CP cross helpers). Leftover docpart parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function sheet_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function sheet_open(string $schema): array
{
	list($user, $pass, $host, $port) = sheet_dsn();
	$admin = new PDO("mysql:host={$host};port={$port};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$admin->exec("CREATE DATABASE `{$schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
	$db = new PDO("mysql:host={$host};port={$port};dbname={$schema};charset=utf8mb4", $user, $pass, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$db->exec('CREATE TABLE shop_docpart_articles_analogs_list (
		id INT AUTO_INCREMENT PRIMARY KEY,
		article VARCHAR(64), analog VARCHAR(64),
		manufacturer_article VARCHAR(64), manufacturer_analog VARCHAR(64)
	)');
	return array($admin, $db);
}

function sheet_run_norm()
{
	return array(
		epc_cp_cross_normalize_article(" ab-12_/`'\"\\. ,#\t\r\n"),
		epc_cp_cross_normalize_article(''),
		epc_cp_cross_prepare_brand(" bosch #`\"'\\"),
		epc_cp_cross_prepare_brand("\t"),
		epc_cp_cross_pair_status(sheet_dummy_pdo(), '', 'BOSCH', 'X', 'Y'),
		epc_cp_cross_pair_status(sheet_dummy_pdo(), 'AB12', 'BOSCH', 'AB12', 'BOSCH'),
	);
}

function sheet_dummy_pdo(): PDO
{
	return new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql;charset=utf8mb4', 'ecomae', getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'));
}

function sheet_run_count()
{
	$schema = 'ecomae_cpw_sheet_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = sheet_open($schema);
	try {
		$db->exec("INSERT INTO shop_docpart_articles_analogs_list (article, analog) VALUES ('AB12','XY9'), ('ZZ','AB12')");
		$ok = epc_cp_cross_count_links_for_anchor($db, 'ab-12', 'BOSCH');
		$empty = epc_cp_cross_count_links_for_anchor($db, '   ');
		$GLOBALS['SHEET_EXPR'] = '`no_such_col`';
		$bad = epc_cp_cross_count_links_for_anchor($db, 'AB12');
		return array($ok, $empty, $bad);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function sheet_run_annotate()
{
	$schema = 'ecomae_cpw_sheet_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = sheet_open($schema);
	try {
		$GLOBALS['SHEET_PAIRS'] = array('AB12|BOSCH|XY9|VALEO' => array('linked' => true, 'id' => 17));
		$GLOBALS['SHEET_BRANDS'] = array('NOBRAND' => 'GATES');
		$GLOBALS['SHEET_USE_FALLBACK'] = false;
		$refs = array(
			array('article' => 'xy-9', 'brand' => ' valeo '),
			array('article' => 'nobrand', 'brand' => ''),
			array('article' => 'miss', 'brand' => ''),
			'skip',
		);
		$out = epc_cp_cross_annotate_references($db, 'ab-12', 'bosch', $refs);
		return $out;
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function sheet_run_import()
{
	$schema = 'ecomae_cpw_sheet_' . substr(md5(uniqid('', true)), 0, 8);
	list($admin, $db) = sheet_open($schema);
	try {
		$GLOBALS['SHEET_PAIRS'] = array('AB12|BOSCH|OLD|VALEO' => array('linked' => true, 'id' => 3));
		$GLOBALS['SHEET_BRANDS'] = array();
		$GLOBALS['SHEET_PERSIST'] = 1;
		$invalid = epc_cp_cross_add_link($db, '  ', 'BOSCH', 'X', 'Y');
		$same = epc_cp_cross_add_link($db, 'AB12', 'BOSCH', 'AB12', 'BOSCH');
		$already = epc_cp_cross_add_link($db, 'AB12', 'BOSCH', 'OLD', 'VALEO');
		$ok = epc_cp_cross_add_link($db, 'AB12', 'BOSCH', 'NEW', 'VALEO');
		$missing = epc_cp_cross_add_link($db, 'AB12', '', 'NEW2', '');
		$imp = epc_cp_cross_import_references($db, 'AB12', 'BOSCH', array(
			'skip',
			array('article' => 'OLD', 'brand' => 'VALEO', 'source' => 'crossbase'),
			array('article' => 'NEW3', 'brand' => 'VALEO', 'source' => 'crossbase'),
			array('article' => 'OTHER', 'brand' => 'VALEO', 'source' => 'manual'),
			array('article' => '', 'brand' => 'VALEO', 'source' => 'crossbase'),
		), true, 'crossbase');
		$GLOBALS['SHEET_HTTP'] = json_encode(array('ok' => 1, 'article' => 'AB12'));
		$cfg = (object) array('domain_path' => 'https://shop.example/', 'tech_key' => 'k1');
		$search = epc_cp_cross_fetch_search($cfg, 'AB12', 'BOSCH', true);
		$blank = epc_cp_cross_fetch_search($cfg, '  ', '', false);
		return array($invalid, $same, $already, $ok, $missing, $imp, $search, $blank);
	} finally {
		$admin->exec("DROP DATABASE IF EXISTS `{$schema}`");
	}
}

function sheet_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';", '// leftover article-match stubbed', $code);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_cross_interchange.php';", '// leftover interchange stubbed', $code);
	$code = preg_replace(
		'/\$html = \'\';.*?return is_array\(\$data\) \? \$data : null;/s',
		'$html = isset($GLOBALS["SHEET_HTTP"]) ? (string) $GLOBALS["SHEET_HTTP"] : ""; if (!is_string($html) || $html === "") { return null; } $data = json_decode($html, true); return is_array($data) ? $data : null;',
		$code
	);
	file_put_contents($dest, $code);
}

function sheet_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	$GLOBALS['SHEET_PAIRS'] = array();
	$GLOBALS['SHEET_BRANDS'] = array();
	$GLOBALS['SHEET_PERSIST'] = 0;
	$GLOBALS['SHEET_EXPR'] = '`article`';
	$GLOBALS['SHEET_USE_FALLBACK'] = true;
	if (!function_exists('docpart_sql_article_normalized_expr')) {
		eval('function docpart_sql_article_normalized_expr($column = "`article`") { return $GLOBALS["SHEET_EXPR"] === "`article`" && $column === "`analog`" ? "`analog`" : ($GLOBALS["SHEET_EXPR"] ?? $column); }');
	}
	if (!function_exists('docpart_cross_pair_exists_with_brands')) {
		eval('function docpart_cross_pair_exists_with_brands($db, $a, $ab, $r, $rb) { $k = $a."|".$ab."|".$r."|".$rb; return $GLOBALS["SHEET_PAIRS"][$k] ?? array("linked" => false, "id" => 0); }');
	}
	if (!function_exists('docpart_cross_resolve_brand_for_article')) {
		eval('function docpart_cross_resolve_brand_for_article($db, $article, $opts = array()) { $article = strtoupper(preg_replace("/[^A-Z0-9]/", "", strtoupper(trim((string) $article)))); if (isset($GLOBALS["SHEET_BRANDS"][$article])) { return $GLOBALS["SHEET_BRANDS"][$article]; } if (!empty($GLOBALS["SHEET_USE_FALLBACK"])) { $fb = isset($opts["fallback_brand"]) ? trim((string) $opts["fallback_brand"]) : ""; return $fb; } return ""; }');
	}
	if (!function_exists('docpart_cross_persist_interchange_pair_bidirectional')) {
		eval('function docpart_cross_persist_interchange_pair_bidirectional($db, $a, $ab, $r, $rb) { return (int) ($GLOBALS["SHEET_PERSIST"] ?? 0); }');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_sheet_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	sheet_patch($root . '/cp/content/shop/crosses/epc_cp_cross_helpers.php', $doc . '/helpers.php');
	sheet_boot();
	require $doc . '/helpers.php';
	$fn = 'sheet_run_' . $case['name'];
	$result = $fn();
	@unlink($doc . '/helpers.php');
	@rmdir($doc);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'))
		. ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1sheet_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1sheet_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
