<?php
// PHP 8.3 goldens for plan Q1-bow (product family). Leftover article-match and demand stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function bow_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function bow_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once __DIR__ . '/docpart_article_match.php';", '// leftover article-match stubbed', $code);
	file_put_contents($dest, $code);
}

function bow_boot(): void
{
	if (!function_exists('docpart_normalize_article_for_price')) {
		eval('function docpart_normalize_article_for_price($a) { return strtoupper(preg_replace("/[^A-Za-z0-9]/", "", (string) $a)); }');
	}
}

function bow_include(): void
{
	include_once $GLOBALS['BOW_PAGE'];
}

function bow_run_infer()
{
	bow_include();
	return array(
		epc_pf_infer_product_group('', 'Piston ring'),
		epc_pf_infer_product_group('', 'Oil filter kit'),
		epc_pf_infer_product_group('', 'Cabin filter'),
		epc_pf_infer_product_group('', 'Brake pad set'),
		epc_pf_infer_product_group('', 'Brake disc'),
		epc_pf_infer_product_group('', 'Wheel bearing'),
		epc_pf_infer_product_group('', 'Cylinder head'),
		epc_pf_infer_product_group('', ''),
		epc_pf_infer_product_group('', 'Widget'),
		epc_pf_infer_product_group('Custom group', 'Piston'),
	);
}

function bow_run_map()
{
	bow_include();
	$rows = array(
		array('brand' => ' Bosch ', 'article' => 'OC-47', 'article_show' => 'OC47', 'name' => 'Oil filter', 'qty' => '10', 'price' => 12.5, 'warehouse' => ' S-UAE '),
		array('brand' => '', 'article' => 'X', 'name' => 'Skip'),
		array('brand' => 'Febi', 'article' => '', 'article_show' => '', 'name' => 'Skip2'),
		array('brand' => 'Mann', 'article' => 'HU-712', 'name' => 'Filter'),
	);
	$mapped = epc_pf_map_stock_rows($rows);
	$slice = epc_pf_top_brands_slice(array(
		array('brand' => 'Zed', 'total_qty' => 5),
		array('brand' => 'Ace', 'total_qty' => 5),
		array('brand' => 'Big', 'total_qty' => 20),
	), 2);
	$one = epc_pf_top_brands_slice(array(
		array('brand' => 'Solo', 'total_qty' => 1),
	), 0);
	return array(
		count($mapped),
		$mapped[0]['brand'],
		$mapped[0]['article'],
		$mapped[0]['article_norm'],
		$mapped[0]['warehouse'],
		(float) $mapped[0]['qty'],
		$mapped[1]['article_norm'],
		$slice['total'],
		$slice['more_count'],
		$slice['top'][0]['brand'],
		$slice['top'][1]['brand'],
		$one['more_count'],
		$one['top'][0]['brand'],
	);
}

function bow_run_catalog()
{
	bow_include();
	$lines = array(
		array('brand' => 'Bosch', 'article' => 'OC-47', 'article_show' => 'OC47', 'article_norm' => 'OC47', 'name' => 'Oil filter', 'qty' => 10, 'price' => 12.5),
		array('brand' => 'Mann', 'article' => 'HU712', 'name' => 'Oil filter kit', 'qty' => 4, 'price' => 8),
		array('brand' => 'Febi', 'article' => 'P1', 'name' => 'Brake pad', 'qty' => 2, 'price' => 4),
		array('brand' => 'Skip', 'article' => '', 'name' => 'No art', 'qty' => 1),
		array('brand' => 'X', 'article' => 'Z', 'name' => 'Widget', 'qty' => 1, 'price' => 1),
	);
	$cat = epc_pf_build_catalog_from_lines($lines);
	$cards = epc_pf_products_for_cards($cat['products'], 1);
	$found = epc_pf_find_group($cat['product_groups'], 'Oil filter');
	$foundCase = epc_pf_find_group($cat['product_groups'], 'OIL FILTER');
	$miss = epc_pf_find_group($cat['product_groups'], 'missing');
	$detail = epc_pf_group_detail($found, 'bosch');
	$detailAll = epc_pf_group_detail($found, '');
	$labels = array();
	$counts = array();
	foreach ($cat['products'] as $p) {
		$labels[] = $p['label'];
		$counts[] = $p['parts_count'];
	}
	return array(
		$cat['summary']['parts_count'],
		$cat['summary']['product_groups_count'],
		$cat['summary']['brands_count'],
		(float) $cat['summary']['total_stock_qty'],
		$labels,
		$counts,
		(int) $cards[0]['brands_more_count'],
		(int) $cards[0]['brands_total'],
		$cards[0]['label'],
		$found ? $found['label'] : '',
		$foundCase ? $foundCase['label'] : '',
		$miss === null ? 1 : 0,
		$detail['parts_count'],
		(float) $detail['total_qty'],
		$detailAll['parts_count'],
		(float) $detailAll['total_qty'],
	);
}

function bow_run_fetch()
{
	bow_include();
	list($user, $pass, $host, $port) = bow_dsn();
	$schema = 'ecomae_cpw_bow_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$missing = epc_pf_fetch_catalog_lines($db, 200);
		$db->exec('CREATE TABLE shop_docpart_prices_data (manufacturer VARCHAR(64), article VARCHAR(64), article_show VARCHAR(64), name VARCHAR(128), `exist` INT, price DECIMAL(10,2), storage VARCHAR(32))');
		$empty = epc_pf_fetch_catalog_lines($db, 200);
		$db->exec("INSERT INTO shop_docpart_prices_data VALUES ('Bosch','OC-47','OC47','Oil filter',10,12.5,'S-UAE'),('Bosch','OC-47','OC47','Oil filter',3,9.5,'R-UAE'),('','SKIP','','',1,1,'S-UAE'),('Mann','HU712','','',0,8,'S-UAE'),('Febi','X1','X1','Pad',2,4.2,'S-UAE'),('Skip','BIG','BIG','Huge',60001,1,'S-UAE'),('Zero','Z1','Z1','Zero',2,0,'S-UAE')");
		$rows = epc_pf_fetch_catalog_lines($db, 200);
		$brands = array();
		$qtys = array();
		foreach ($rows as $r) {
			$brands[] = $r['brand'];
			$qtys[] = (float) $r['qty'];
		}
		$cfg = new stdClass();
		$budget = 5;
		$emptyBrand = epc_pf_resolve_product_group($cfg, $db, '', 'OC47', 'Piston', $budget);
		$inferred = epc_pf_resolve_product_group($cfg, $db, 'Bosch', 'OC-47', 'Oil filter', $budget);
		$db->exec("INSERT INTO `epc_umapi_product_group` (`manufacturer`,`article_norm`,`product_group`,`umapi_raw`,`updated_at`) VALUES ('Bosch','OC47','CachedFam','raw',1)");
		$cached = epc_pf_resolve_product_group($cfg, $db, 'Bosch', 'OC-47', 'Oil filter', $budget);
		return array(
			count($missing),
			count($empty),
			count($rows),
			$brands,
			$qtys,
			$rows[0]['article_norm'] ?? '',
			$emptyBrand,
			$inferred,
			$cached,
			$budget,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_bow_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	bow_patch($root . '/content/shop/docpart/epc_product_family.php', $tmp . '/pf.php');
	$GLOBALS['BOW_PAGE'] = $tmp . '/pf.php';
	$_SERVER['DOCUMENT_ROOT'] = $root;
	bow_boot();
	$fn = 'bow_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/pf.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1bow_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1bow_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
