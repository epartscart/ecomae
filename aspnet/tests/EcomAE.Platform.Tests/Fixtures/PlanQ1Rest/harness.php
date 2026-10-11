<?php
// PHP 8.3 goldens for plan Q1-rest (demand ISO / industry themes / brochure photos / theme templates / packages).
// Usage: php harness.php /workspace-wt/small-done > golden.json
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_cpw_q1r_' . substr(md5(uniqid('', true)), 0, 12);
	@mkdir($doc . '/content/general_pages', 0777, true);
	@mkdir($doc . '/content/shop/docpart', 0777, true);
	$password = getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN');
	if ($password === false || $password === '') {
		fwrite(STDERR, "ECOMAE_LOCAL_MARIADB_E2E_DSN is required\n");
		exit(2);
	}
	$admin = new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$dbName = 'ecomae_cpw_' . substr(md5(uniqid('', true)), 0, 12);
	$admin->exec('CREATE DATABASE `' . $dbName . '`');
	$pdo = new PDO('mysql:host=127.0.0.1;port=3306;dbname=' . $dbName . ';charset=utf8', 'ecomae', $password, array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION));
	$name = (string) $case['name'];
	$gp = array();
	$dp = array();
	if (in_array($name, array('demand_maps', 'demand_csv_parse', 'demand_csv_preview', 'demand_migrate', 'demand_import'), true)) {
		$dp[] = 'epc_demand_country_iso.php';
		file_put_contents($doc . '/content/shop/docpart/docpart_article_match.php', "<?php\nfunction docpart_normalize_article_for_price(\$a){ return strtoupper(preg_replace('/[^A-Z0-9]/','', (string)\$a)); }\n");
		file_put_contents($doc . '/content/shop/docpart/epc_demand_intelligence.php', "<?php\nfunction epc_demand_ensure_schema(\$db){}\n");
	} elseif ($name === 'themes_data') {
		$gp[] = 'epc_storefront_industry_themes.php';
	} elseif ($name === 'brochure_topics') {
		$gp[] = 'epc_cp_brochure_topic_photos.php';
	} elseif ($name === 'theme_slots') {
		$gp[] = 'epc_portal_theme_templates.php';
		file_put_contents($doc . '/content/general_pages/epc_portal.php', "<?php\nfunction epc_portal_industries(){ return array('auto_parts'=>array('name'=>'Auto parts','theme'=>array('primary'=>'#dc2626')), 'electronics'=>array('name'=>'Electronics','theme'=>array()), 'unknown_vert'=>array('name'=>'Unknown Vert','theme'=>array('primary'=>'#111111','primary_dark'=>'#222222','accent'=>'#333333','sidebar_from'=>'#444444','sidebar_to'=>'#555555','hero_from'=>'#666666','hero_to'=>'#777777'))); }\nfunction epc_portal_industry(\$code){ \$all=epc_portal_industries(); return isset(\$all[\$code]) ? \$all[\$code] : array('name'=>(string)\$code,'theme'=>array()); }\n");
	} elseif ($name === 'packages_data') {
		$gp[] = 'epc_portal_theme_templates.php';
		$gp[] = 'epc_portal_storefront_packages.php';
		file_put_contents($doc . '/content/general_pages/epc_portal.php', "<?php\nfunction epc_portal_industries(){ return array('auto_parts'=>array('name'=>'Auto parts','theme'=>array('primary'=>'#dc2626')), 'electronics'=>array('name'=>'Electronics','theme'=>array()), 'jewellery'=>array('name'=>'Jewellery','theme'=>array()), 'fashion'=>array('name'=>'Fashion','theme'=>array()), 'tax_advisory'=>array('name'=>'Tax','theme'=>array())); }\nfunction epc_portal_industry(\$code){ \$all=epc_portal_industries(); return isset(\$all[\$code]) ? \$all[\$code] : array('name'=>(string)\$code,'theme'=>array()); }\nfunction epc_portal_load_site_settings(){ return array(); }\n");
	}
	foreach ($gp as $php) {
		copy($root . '/content/general_pages/' . $php, $doc . '/content/general_pages/' . $php);
	}
	foreach ($dp as $php) {
		copy($root . '/content/shop/docpart/' . $php, $doc . '/content/shop/docpart/' . $php);
	}
	$cleanup = function () use ($doc, $admin, $dbName) {
		try { $admin->exec('DROP DATABASE IF EXISTS `' . $dbName . '`'); } catch (Throwable $e) {}
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $file) {
			$file->isDir() ? @rmdir($file->getPathname()) : @unlink($file->getPathname());
		}
		@rmdir($doc);
	};
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	$GLOBALS['db_link'] = $pdo;
	foreach ($gp as $php) {
		require $doc . '/content/general_pages/' . $php;
	}
	foreach ($dp as $php) {
		require $doc . '/content/shop/docpart/' . $php;
	}
	register_shutdown_function(function () use ($case, $cleanup) {
		$html = ob_get_clean();
		$cleanup();
		echo json_encode(array('name' => $case['name'], 'output' => $html, 'result' => $GLOBALS['__result'] ?? null), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	});
	ob_start();
	if (isset($case['eval'])) {
		$GLOBALS['__result'] = eval($case['eval']);
	}
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) . ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1r_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1r_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
