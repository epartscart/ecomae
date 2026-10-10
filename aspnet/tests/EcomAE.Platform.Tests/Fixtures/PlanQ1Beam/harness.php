<?php
// PHP 8.3 goldens for plan Q1-beam (article-match). Stock/pricing/synonym/UMAPI parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function beam_dsn(): array
{
	$pass = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN') ?: '';
	if ($pass === '') {
		throw new RuntimeException('missing ECOMAE_LOCAL_MARIADB_E2E_DSN');
	}
	return array('ecomae', $pass, '127.0.0.1', '3306');
}

function beam_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("function_exists('sys_getloadavg')", 'false', $code);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_stock_brands_helpers.php';", '// stock helpers injected', $code);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php';", '// pricing injected', $code);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_manufacturer_synonyms.php';", '// synonyms injected', $code);
	$code = str_replace("is_file(\$_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_manufacturer_synonyms.php')", 'false', $code);
	$code = str_replace("is_file(\$_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_pricing.php')", 'false', $code);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/docpart_epc_article_brands.php';", '// leftover article-brands injected', $code);
	file_put_contents($dest, $code);
}

function beam_include(): void
{
	include_once $GLOBALS['BEAM_PAGE'];
}

function beam_run_norm()
{
	beam_include();
	$cfg = new stdClass();
	$cfg->chpu_search_config = array(
		'level_1' => array('url' => 'parts'),
		'level_2' => array('mode_1' => array('url' => 'brands')),
		'slash_code' => '---',
	);
	return array(
		docpart_normalize_article_for_price(null),
		docpart_normalize_article_for_price(''),
		docpart_normalize_article_for_price('OC-47'),
		docpart_normalize_article_for_price("filter oil_01/`.'\"#\\\t"),
		docpart_normalize_article_for_price('oc@47'),
		docpart_sql_article_normalized_expr(),
		docpart_sql_article_normalized_expr('`analog`'),
		epc_chpu_build_part_url($cfg, '/en/', 'Bosch', 'OC-47'),
		epc_chpu_build_part_url($cfg, '/en/', '', 'OC-47'),
		epc_chpu_build_part_url($cfg, '/en/', 'JS ASAKASHI', 'FILTER-OIL-01'),
		epc_chpu_build_part_url($cfg, '/en/', 'A/B', 'X1'),
		epc_chpu_build_part_url($cfg, '/en/', 'Bosch', ''),
	);
}

function beam_run_clause()
{
	beam_include();
	list($user, $pass, $host, $port) = beam_dsn();
	$schema = 'ecomae_cpw_beam_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$bind = array();
		$empty = docpart_sql_article_values_match_clause($db, array('', null), $bind);
		$noTable = docpart_sql_article_values_match_clause($db, array('OC47'), $bind);
		$db->exec('CREATE TABLE shop_docpart_prices_data (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer VARCHAR(64), price_id INT, `exist` INT, price DECIMAL(10,2))');
		$probe = docpart_price_data_ensure_article_search_column($db, false) ? 1 : 0;
		$bind2 = array();
		$afterProbe = docpart_sql_article_values_match_clause($db, array('OC47'), $bind2);
		$hasAnalogs = docpart_analogs_has_search_columns($db) ? 1 : 0;
		list($art, $analog) = docpart_analogs_match_exprs($db);
		return array(
			$empty,
			$noTable,
			count($bind),
			$probe,
			$afterProbe,
			count($bind2),
			$hasAnalogs,
			$art === '`article_search`' ? 1 : 0,
			substr($analog, 0, 6),
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function beam_run_collect()
{
	beam_include();
	list($user, $pass, $host, $port) = beam_dsn();
	$schema = 'ecomae_cpw_beam_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->exec('CREATE TABLE shop_docpart_articles_analogs_list (article VARCHAR(64), analog VARCHAR(64), manufacturer_article VARCHAR(64), manufacturer_analog VARCHAR(64))');
		$db->exec("INSERT INTO shop_docpart_articles_analogs_list VALUES ('OC-47','OC47X','Bosch','Mann'),('ZZ','OC-47','Skip','Febi')");
		$db->exec('CREATE TABLE shop_docpart_prices_data (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer VARCHAR(64), price_id INT, `exist` INT, price DECIMAL(10,2))');
		$db->exec("INSERT INTO shop_docpart_prices_data (article, manufacturer, price_id, `exist`, price) VALUES ('OC-47','Bosch',1,2,12.5)");
		$db->exec('CREATE TABLE shop_storages (id INT PRIMARY KEY, connection_options TEXT)');
		$db->exec("INSERT INTO shop_storages VALUES (8, '{\"price_id\":1}'),(9, '{\"price_id\":2}')");
		$none = docpart_collect_article_candidates($db, '', true);
		$direct = docpart_collect_article_candidates($db, 'OC-47', false);
		$cross = docpart_collect_article_candidates($db, 'OC-47', true);
		$cfg = new stdClass();
		$cfg->local_crosses = 1;
		$resolvedHit = docpart_resolve_article_search_values($db, $cfg, 'OC-47', array(1));
		$cfg0 = new stdClass();
		$cfg0->local_crosses = 0;
		$resolvedOff = docpart_resolve_article_search_values($db, $cfg0, 'OC-47', array(1));
		$ids = docpart_price_ids_from_office_storage_bunches($db, array(
			array('storage_id' => 8),
			array('storage_id' => 8),
			array('storage_id' => 9),
			array('storage_id' => 0),
		));
		$emptyIds = docpart_price_ids_from_office_storage_bunches($db, array());
		return array(
			$none,
			$direct,
			$cross,
			$resolvedHit,
			$resolvedOff,
			$ids,
			$emptyIds,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

function beam_run_chpu()
{
	beam_include();
	if (!function_exists('epc_article_brands_from_umapi')) {
		eval('function epc_article_brands_from_umapi($cfg, $article, &$brands, &$seen) { $brands["BOSCH"] = array("manufacturer"=>"Bosch"); $seen["BOSCH"]=true; return 1; }');
	}
	if (!function_exists('epc_stock_brand_price_ids_with_stock')) {
		eval('function epc_stock_brand_price_ids_with_stock($db) { return array(); }');
		eval('function epc_stock_brand_price_ids($db) { return array(); }');
	}
	list($user, $pass, $host, $port) = beam_dsn();
	$schema = 'ecomae_cpw_beam_' . substr(md5(uniqid('', true)), 0, 8);
	$admin = new PDO('mysql:host=' . $host . ';port=' . $port . ';charset=utf8mb4', $user, $pass);
	$admin->exec('CREATE DATABASE `' . $schema . '` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci');
	try {
		$db = new PDO('mysql:host=' . $host . ';port=' . $port . ';dbname=' . $schema . ';charset=utf8mb4', $user, $pass);
		$db->exec('CREATE TABLE shop_docpart_prices (id INT PRIMARY KEY)');
		$db->exec('INSERT INTO shop_docpart_prices VALUES (1)');
		$db->exec('CREATE TABLE shop_docpart_prices_data (id INT PRIMARY KEY AUTO_INCREMENT, article VARCHAR(64), manufacturer VARCHAR(64), price_id INT, `exist` INT, price DECIMAL(10,2))');
		$db->exec("INSERT INTO shop_docpart_prices_data (article, manufacturer, price_id, `exist`, price) VALUES ('OC-47','Bosch',1,2,12.5),('OC-47','MANN',1,1,8)");
		$cfg = new stdClass();
		$cfg->local_crosses = 0;
		$cfg->chpu_search_config = array(
			'chpu_search_on' => 1,
			'level_1' => array('url' => 'parts'),
			'level_2' => array('mode_1' => array('url' => 'brands')),
			'slash_code' => '---',
		);
		$two = epc_chpu_distinct_warehouse_brands_for_article($db, $cfg, 'OC-47', array(1));
		$single = epc_chpu_resolve_single_brand_for_article($db, $cfg, 'OC-47');
		$off = new stdClass();
		$off->chpu_search_config = array('chpu_search_on' => 0);
		$redirOff = epc_chpu_single_brand_redirect_url($db, $off, 'OC-47', '/en');
		$db->exec("DELETE FROM shop_docpart_prices_data WHERE manufacturer='MANN'");
		$one = epc_chpu_distinct_warehouse_brands_for_article($db, $cfg, 'OC-47', array(1));
		$resolved = epc_chpu_resolve_single_brand_for_article($db, $cfg, 'OC-47');
		$redir = epc_chpu_single_brand_redirect_url($db, $cfg, 'OC-47', '/en');
		$db->exec('DELETE FROM shop_docpart_prices_data');
		$emptyWh = epc_chpu_distinct_warehouse_brands_for_article($db, $cfg, 'OC-47', array(1));
		$fromUmapi = epc_chpu_resolve_single_brand_for_article($db, $cfg, 'OC-47');
		$umapiOnly = epc_chpu_single_brand_from_umapi($cfg, 'OC-47');
		return array(
			$two,
			$single === null ? 1 : 0,
			$redirOff,
			$one,
			$resolved,
			$redir,
			$emptyWh,
			$fromUmapi,
			$umapiOnly,
		);
	} finally {
		$admin->exec('DROP DATABASE IF EXISTS `' . $schema . '`');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_beam_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	beam_patch($root . '/content/shop/docpart/docpart_article_match.php', $tmp . '/match.php');
	$GLOBALS['BEAM_PAGE'] = $tmp . '/match.php';
	$_SERVER['DOCUMENT_ROOT'] = $root;
	$fn = 'beam_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/match.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1beam_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1beam_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
