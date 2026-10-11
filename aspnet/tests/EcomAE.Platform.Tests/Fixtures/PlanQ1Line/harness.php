<?php
// PHP 8.3 goldens for plan Q1-line (article brands). Leftover article-match / synonym / cache parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function line_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function line_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once(\$_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/docpart_article_match.php');", '// leftover article-match stubbed', $code);
	$code = str_replace("require_once(\$_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/docpart_manufacturer_synonyms.php');", '// leftover synonyms stubbed', $code);
	$code = str_replace("require_once(\$_SERVER['DOCUMENT_ROOT'].'/content/shop/docpart/epc_crossbase_cache.php');", '// leftover cache stubbed', $code);
	$code = preg_replace(
		'/function epc_article_brands_fetch_url\(\$url\)\s*\{.*?\n\}/s',
		"function epc_article_brands_fetch_url(\$url)\n{\n\treturn (string) (\$GLOBALS['LINE_FETCH'] ?? '');\n}",
		$code
	);
	file_put_contents($dest, $code);
}

function line_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	if (!function_exists('curl_init')) {
		eval('function curl_init($url = null) { $GLOBALS["LINE_CURL_URL"] = (string) $url; return 1; }');
		eval('function curl_setopt($ch, $opt, $val) {}');
		eval('function curl_setopt_array($ch, $opts) {}');
		eval('function curl_exec($ch) { return (string) ($GLOBALS["LINE_FETCH"] ?? ""); }');
		eval('function curl_close($ch) {}');
	} else {
		// Force leftover HTTP through the injected fetch helper only.
		$GLOBALS['LINE_FETCH'] = $GLOBALS['LINE_FETCH'] ?? '';
	}
	if (!function_exists('docpart_normalize_article_for_price')) {
		eval('function docpart_normalize_article_for_price($a) { return strtoupper(preg_replace("/[^A-Za-z0-9]/", "", (string) $a)); }');
		eval('function docpart_analogs_match_exprs($db) { return array("`article`", "`analog`"); }');
		eval('function docpart_sql_article_normalized_expr($col) { return $col; }');
		eval('function epc_crossbase_cache_read($k, $ttl = 0, $any = false) { return (string) ($GLOBALS["LINE_CACHE"] ?? ""); }');
		eval('function epc_crossbase_cache_write($k, $html) { $GLOBALS["LINE_CACHE_W"] = (string) $html; }');
		eval('function docpart_load_manufacturer_canonical_map($db) { return (array) ($GLOBALS["LINE_CANON"] ?? array()); }');
		eval('function docpart_synonym_canonical_brand($b, $map) { $b = (string) $b; return isset($map[$b]) ? (string) $map[$b] : ""; }');
		eval('function epc_chpu_distinct_warehouse_brands_for_article($db, $cfg, $art) { return (array) ($GLOBALS["LINE_WH"] ?? array()); }');
	}
}

function line_include(): void
{
	include_once $GLOBALS['LINE_PAGE'];
}

function line_run_empty()
{
	line_include();
	$cfg = (object) array('local_crosses' => 1);
	$a = epc_collect_article_catalog_brands(null, $cfg, '');
	$b = epc_collect_article_catalog_brands(null, $cfg, '---');
	return array($a['status'] ? 1 : 0, $a['message'], $b['status'] ? 1 : 0, $b['message']);
}

function line_run_addkey()
{
	line_include();
	$brands = array();
	$seen = array();
	$short = epc_article_brands_add($brands, $seen, 'X', '', 't') ? 1 : 0;
	$ok = epc_article_brands_add($brands, $seen, 'Bosch', 'Pad', 'umapi') ? 1 : 0;
	$dup = epc_article_brands_add($brands, $seen, 'BOSCH', 'Better', 'warehouse') ? 1 : 0;
	$cfg = array('umapi_api_key' => '', 'umapi_api_url' => 'https://api.umapi.ru/v1/KEY99/');
	return array(
		$short,
		$ok,
		$dup,
		$brands['BOSCH']['name'],
		$brands['BOSCH']['sources'],
		epc_article_brands_umapi_key((object) $cfg),
		epc_article_brands_umapi_key((object) array('umapi_api_key' => '  abc  ')),
	);
}

function line_run_crosshtml()
{
	line_include();
	$brand = epc_article_brands_crossbase_brand('OC47', 'Bosch OC47');
	$brand2 = epc_article_brands_crossbase_brand('OC47', 'Mann <b>OC47</b>');
	$html = '<tr><td>1</td><td><a href="/cross/?q=OC47">Bosch OC47</a></td></tr><a href="/cross/?q=OC90">Skip OC90</a>';
	$GLOBALS['LINE_CACHE'] = $html;
	$brands = array();
	$seen = array();
	$n = epc_article_brands_from_crossbase('OC-47', docpart_normalize_article_for_price('OC-47'), $brands, $seen);
	$ids = array_keys($brands);
	return array($brand, $brand2, $n, $ids[0] ?? '', $brands[$ids[0] ?? '']['sources'] ?? array());
}

function line_run_collect()
{
	line_include();
	list($user, $pass, $host, $port) = line_dsn();
	$schema = 'ecomae_cpw_line_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->exec('CREATE TABLE shop_docpart_articles_analogs_list (article VARCHAR(64), manufacturer_article VARCHAR(64), analog VARCHAR(64), manufacturer_analog VARCHAR(64))');
		$db->exec("INSERT INTO shop_docpart_articles_analogs_list VALUES ('OC47','Bosch','OC47X','Mann'),('ZZ','Skip','OC47','Febi')");
		$GLOBALS['LINE_WH'] = array('ACME', 'B');
		$GLOBALS['LINE_CANON'] = array('Bosch' => 'BOSCH', 'Mann' => 'MANN');
		$GLOBALS['LINE_CACHE'] = '';
		$cfg = (object) array('local_crosses' => 1, 'umapi_api_key' => '');
		$row = epc_collect_article_catalog_brands($db, $cfg, 'OC-47');
		$shows = array();
		foreach ($row['manufacturers'] as $m) {
			$shows[] = $m['manufacturer_show'];
		}
		return array(
			$row['status'] ? 1 : 0,
			$row['article'],
			$row['warehouse_count'],
			$row['cp_crosses_count'],
			$row['crossbase_count'],
			$row['umapi_count'],
			$shows,
			$row['manufacturers'][0]['params']['type'] ?? '',
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_line_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	line_patch($root . '/content/shop/docpart/docpart_epc_article_brands.php', $tmp . '/brands.php');
	$GLOBALS['LINE_PAGE'] = $tmp . '/brands.php';
	$_SERVER['DOCUMENT_ROOT'] = $root;
	line_boot();
	$fn = 'line_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/brands.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1line_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1line_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
