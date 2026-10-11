<?php
// PHP 8.3 goldens for plan Q1-stem (accessories catalog). Leftover article-match parent stays stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function stem_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function stem_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';", '// leftover article-match stubbed', $code);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_accessories_taxonomy.php';", '// leftover taxonomy injected', $code);
	file_put_contents($dest, $code);
}

function stem_boot(): void
{
	if (!function_exists('docpart_normalize_article_for_price')) {
		eval('function docpart_normalize_article_for_price($a) { return strtoupper(preg_replace("/[^A-Za-z0-9]/", "", (string) $a)); }');
	}
	if (!function_exists('epc_acc_classify')) {
		eval('function epc_acc_classify($name, $brand = "") { $hay = strtolower((string) $name); if (strpos($hay, "pad") !== false) { return array("category"=>"brakes","subcategory"=>"pads","category_label"=>"Brakes","subcategory_label"=>"Brake Pads"); } return array("category"=>"other","subcategory"=>"general","category_label"=>"Other Parts","subcategory_label"=>"General"); }');
		eval('function epc_acc_warehouse_regions() { return array("S-UAE"=>"Dubai / Sharjah stock","R-UAE"=>"Ras Al Khaimah stock"); }');
		eval('function epc_acc_taxonomy() { return array("brakes"=>array("label"=>"Brakes","icon"=>"fa-stop","subs"=>array("pads"=>array("label"=>"Brake Pads"))),"other"=>array("label"=>"Other Parts","icon"=>"fa-tag","subs"=>array("general"=>array("label"=>"General")))); }');
	}
}

function stem_include(): void
{
	include_once $GLOBALS['STEM_PAGE'];
}

function stem_run_path()
{
	$_SERVER['DOCUMENT_ROOT'] = '/shop/acme';
	stem_include();
	$p = epc_acc_cache_path();
	return array(basename($p), substr(md5('/shop/acme'), 0, 8), strpos($p, 'epc_acc_catalog_v1_') !== false ? 1 : 0);
}

function stem_run_fetch()
{
	stem_include();
	list($user, $pass, $host, $port) = stem_dsn();
	$schema = 'ecomae_cpw_stem_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$empty = epc_acc_fetch_raw_rows($db, 200);
		$db->exec('CREATE TABLE shop_docpart_prices_data (manufacturer VARCHAR(64), article VARCHAR(64), article_show VARCHAR(64), name VARCHAR(128), `exist` INT, price DECIMAL(10,2), storage VARCHAR(32), price_id INT)');
		$db->exec("INSERT INTO shop_docpart_prices_data VALUES ('Bosch','OC-47','OC47','Oil filter',10,12.5,'S-UAE',1),('Bosch','OC-47','OC47','Oil filter',3,9.5,'R-UAE',1),('','SKIP','','',1,1,'S-UAE',2),('Mann','HU712','','',0,8,'S-UAE',3),('Febi','X1','X1','Pad',2,4.2,'S-UAE',4)");
		$rows = epc_acc_fetch_raw_rows($db, 200);
		$brands = array();
		foreach ($rows as $r) {
			$brands[] = $r['brand'];
		}
		return array(count($empty), count($rows), $brands, (float) $rows[0]['qty']);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function stem_run_build()
{
	stem_include();
	$rows = array(
		array('brand' => 'Bosch', 'article' => 'OC-47', 'article_show' => 'OC47', 'name' => '', 'qty' => 10, 'price' => 12.5, 'warehouse' => 'S-UAE'),
		array('brand' => 'Bosch', 'article' => 'OC-47', 'article_show' => 'OC47', 'name' => 'Oil filter', 'qty' => 3, 'price' => 9.5, 'warehouse' => ''),
		array('brand' => 'X', 'article' => 'Z', 'name' => 'Pad', 'qty' => 1, 'price' => 1, 'warehouse' => 'S-UAE'),
		array('brand' => 'Skip', 'article' => 'Z', 'name' => 'Big', 'qty' => 60000, 'price' => 1, 'warehouse' => 'S-UAE'),
	);
	$items = epc_acc_build_items($rows);
	$first = $items[0];
	return array(
		count($items),
		$first['brand'],
		$first['article_norm'],
		$first['name'],
		(float) $first['qty'],
		(float) $first['price'],
		$first['region'],
		$first['category'],
		$items[1]['category'] ?? '',
	);
}

function stem_run_search()
{
	stem_include();
	list($user, $pass, $host, $port) = stem_dsn();
	$schema = 'ecomae_cpw_stem_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->exec('CREATE TABLE shop_docpart_prices_data (manufacturer VARCHAR(64), article VARCHAR(64), article_show VARCHAR(64), name VARCHAR(128), `exist` INT, price DECIMAL(10,2), storage VARCHAR(32), price_id INT)');
		$db->exec("INSERT INTO shop_docpart_prices_data VALUES ('Bosch','OC47','OC47','Oil filter',5,20,'S-UAE',1),('Febi','P1','P1','Pad set',2,8,'R-UAE',2),('Febi','P2','P2','Pad',1,12,'S-UAE',3)");
		$hit = epc_acc_search($db, array('refresh' => 1, 'brand' => 'febi', 'sort' => 'price-asc', 'per_page' => 12));
		$q = epc_acc_search($db, array('refresh' => 1, 'q' => 'OC-47', 'sort' => 'price-desc'));
		$page = epc_acc_search($db, array('refresh' => 1, 'page' => 9, 'per_page' => 12));
		$brands = array();
		foreach ($hit['facets']['brands'] as $b) {
			$brands[] = $b['brand'];
		}
		return array(
			$hit['total'],
			$hit['items'][0]['article'] ?? '',
			(float) $hit['items'][0]['price'],
			$q['total'],
			$q['items'][0]['brand'] ?? '',
			$page['page'],
			$page['from'],
			$page['to'],
			$brands,
			$hit['taxonomy'][0]['slug'] ?? '',
			$hit['sort'],
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_stem_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	stem_patch($root . '/content/shop/docpart/epc_accessories_catalog.php', $tmp . '/acc.php');
	$GLOBALS['STEM_PAGE'] = $tmp . '/acc.php';
	$_SERVER['DOCUMENT_ROOT'] = $root;
	stem_boot();
	$fn = 'stem_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/acc.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1stem_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1stem_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
