<?php
// PHP 8.3 goldens for plan Q1-draft (cross interchange). Article-match is included; host load is stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function draft_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function draft_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_article_match.php';", "require_once \$GLOBALS['DRAFT_MATCH'];", $code);
	$code = str_replace("function_exists('sys_getloadavg')", 'false', $code);
	$code = str_replace("@\$db_link->exec('SET SESSION max_statement_time = 3');", '// timeout stubbed', $code);
	$code = str_replace("@\$db_link->exec('SET SESSION MAX_EXECUTION_TIME = 3000');", '// timeout stubbed', $code);
	$code = str_replace("@\$db_link->exec('SET SESSION max_statement_time = 2');", '// timeout stubbed', $code);
	$code = str_replace("@\$db_link->exec('SET SESSION MAX_EXECUTION_TIME = 2000');", '// timeout stubbed', $code);
	file_put_contents($dest, $code);
}

function draft_include(): void
{
	include_once $GLOBALS['DRAFT_PAGE'];
}

function draft_run_names()
{
	draft_include();
	return array(
		docpart_price_name_cluster_core(''),
		docpart_price_name_cluster_core('ab'),
		docpart_price_name_cluster_core('FILTER'),
		docpart_price_name_cluster_core('OIL FILTER 90915-YZZD1'),
		docpart_price_name_cluster_core('AIR FILTER'),
		docpart_price_name_cluster_core('SPARK PLUG X'),
		docpart_price_name_cluster_core('WIDGET 12345'),
		docpart_price_name_cluster_core('fuel  filter'),
		docpart_cross_prepare_brand_name(' Toyota '),
		docpart_cross_prepare_brand_name("bosch#\n"),
		docpart_cross_prepare_brand_name("a`b'\""),
		docpart_cross_infer_brand_from_article_norm('90915YZZD1'),
		docpart_cross_infer_brand_from_article_norm('15400AA'),
		docpart_cross_infer_brand_from_article_norm('15208X'),
		docpart_cross_infer_brand_from_article_norm('26300A'),
		docpart_cross_infer_brand_from_article_norm('06A123'),
		docpart_cross_infer_brand_from_article_norm('A000123456'),
		docpart_cross_infer_brand_from_article_norm('B6Y1X'),
		docpart_cross_infer_brand_from_article_norm('12279A'),
		docpart_cross_infer_brand_from_article_norm('46256X'),
		docpart_cross_infer_brand_from_article_norm('OC47'),
		docpart_analogs_host_load1(),
	);
}

function draft_run_partners()
{
	draft_include();
	list($user, $pass, $host, $port) = draft_dsn();
	$schema = 'ecomae_cpw_draft_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$empty = docpart_load_interchange_partners($db, '');
		$missing = docpart_load_interchange_partners($db, 'OC-47');
		$db->exec('CREATE TABLE shop_docpart_articles_analogs_list (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer_article VARCHAR(64), analog VARCHAR(64), manufacturer_analog VARCHAR(64))');
		$db->exec("INSERT INTO shop_docpart_articles_analogs_list (article, manufacturer_article, analog, manufacturer_analog) VALUES ('OC-47','Bosch','OC47X','Mann'),('HU712','Febi','OC47X','Mann')");
		$rows = docpart_load_interchange_partners($db, 'OC-47', 6, 5000);
		$brands = array();
		$arts = array();
		foreach ($rows as $r) {
			$brands[] = $r['brand'];
			$arts[] = $r['article_norm'];
		}
		$capped = docpart_load_interchange_partners($db, 'OC-47', 0, 10);
		return array(count($empty), count($missing), count($rows), $brands, $arts, count($capped));
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function draft_run_refs()
{
	draft_include();
	list($user, $pass, $host, $port) = draft_dsn();
	$schema = 'ecomae_cpw_draft_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->exec('CREATE TABLE shop_docpart_prices_data (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), article_show VARCHAR(64), manufacturer VARCHAR(64), name VARCHAR(128), `exist` INT, price DECIMAL(10,2))');
		$db->exec("INSERT INTO shop_docpart_prices_data (article, article_show, manufacturer, name, `exist`, price) VALUES ('OC47A','OC47A','Bosch','OIL FILTER 90915YZZD1',2,12.5),('HU712','','Mann','OIL FILTER KIT',1,8),('HU816','HU-816','Wix','Fits OC47A Toyota',3,9),('SKIP','','X','WIDGET',1,1),('Z0','','Y','OIL FILTER ZERO',0,4)");
		$cluster = docpart_cross_refs_from_price_name_cluster($db, 'OC47A');
		$oem = docpart_cross_refs_from_stock_oem_mention($db, 'OC47A');
		$short = docpart_cross_refs_from_stock_oem_mention($db, 'AB');
		$cArts = array();
		foreach ($cluster as $r) {
			$cArts[] = $r['article'];
		}
		$oArts = array();
		foreach ($oem as $r) {
			$oArts[] = $r['article'];
		}
		return array(count($cluster), $cArts, $cluster[0]['brand'] ?? '', count($oem), $oArts, count($short));
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function draft_run_persist()
{
	draft_include();
	list($user, $pass, $host, $port) = draft_dsn();
	$schema = 'ecomae_cpw_draft_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->exec('CREATE TABLE shop_docpart_prices_data (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer VARCHAR(64), `exist` INT, price DECIMAL(10,2))');
		$db->exec("INSERT INTO shop_docpart_prices_data (article, manufacturer, `exist`, price) VALUES ('OC-47','Bosch',2,12.5)");
		$db->exec('CREATE TABLE shop_docpart_articles_analogs_list (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer_article VARCHAR(64), analog VARCHAR(64), manufacturer_analog VARCHAR(64))');
		$db->exec("INSERT INTO shop_docpart_articles_analogs_list (article, manufacturer_article, analog, manufacturer_analog) VALUES ('HU712','Febi','OC47X','Mann'),('OC-47','','90915YZZD1',''),('ZZ','','YY','')");
		$fallback = docpart_cross_resolve_brand_for_article($db, 'ZZ', array('fallback_brand' => ' acme '));
		$fromPrice = docpart_cross_resolve_brand_for_article($db, 'OC-47');
		$fromCross = docpart_cross_resolve_brand_for_article($db, 'HU712');
		$fromInfer = docpart_cross_resolve_brand_for_article($db, '90915YZZD1');
		$same = docpart_cross_pair_exists_with_brands($db, 'OC47', 'Bosch', 'OC47', 'Bosch');
		$ok = docpart_cross_persist_interchange_pair($db, 'OC-47', 'Bosch', 'OC47X', 'Mann') ? 1 : 0;
		$dup = docpart_cross_persist_interchange_pair($db, 'OC-47', 'Bosch', 'OC47X', 'Mann') ? 1 : 0;
		$exists = docpart_cross_pair_exists_with_brands($db, 'OC-47', 'Bosch', 'OC47X', 'Mann');
		$bi = docpart_cross_persist_interchange_pair_bidirectional($db, 'P1', 'Febi', 'P2', 'Valeo');
		$repair = docpart_cross_repair_empty_manufacturers($db, 'OC-47', 50);
		return array(
			$fallback,
			$fromPrice,
			$fromCross,
			$fromInfer,
			$same['linked'] ? 1 : 0,
			$ok,
			$dup,
			$exists['linked'] ? 1 : 0,
			$exists['id'] > 0 ? 1 : 0,
			$bi,
			$repair['updated'],
			$repair['skipped'],
			$repair['error'],
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_draft_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	$GLOBALS['DRAFT_MATCH'] = $root . '/content/shop/docpart/docpart_article_match.php';
	draft_patch($root . '/content/shop/docpart/docpart_cross_interchange.php', $tmp . '/cross.php');
	$GLOBALS['DRAFT_PAGE'] = $tmp . '/cross.php';
	$_SERVER['DOCUMENT_ROOT'] = $root;
	$fn = 'draft_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/cross.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1draft_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1draft_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
