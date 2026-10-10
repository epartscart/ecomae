<?php
// PHP 8.3 goldens for plan Q1-halyard (price-upload tmp folder delete).
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function halyard_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace('function clear_dir($dir, $clear_only)', 'if (!function_exists("clear_dir")) { function clear_dir($dir, $clear_only)', $code);
	$code = str_replace("\t}\n}\n// -------------------------------------------------------------------------------", "\t}\n}\n}\n// -------------------------------------------------------------------------------", $code);
	$code = str_replace("header('Content-Type: application/json; charset=utf-8');", '', $code);
	$code = str_replace(
		"require_once(\$_SERVER[\"DOCUMENT_ROOT\"].\"/config.php\");\n\$DP_Config = new DP_Config;",
		'$DP_Config = $GLOBALS["HALYARD_CFG"];',
		$code
	);
	$code = preg_replace('/try\s*\{\s*\$db_link = new PDO.*?\}\s*catch \(PDOException \$e\)\s*\{.*?\}/s',
		'if (empty($GLOBALS["HALYARD_DB"])) { $answer = array("status"=>false,"message"=>"No DB Connect"); echo json_encode($answer); return; } $db_link = $GLOBALS["HALYARD_DB"];',
		$code
	);
	$code = str_replace('$db_link->query("SET NAMES utf8;");', '', $code);
	$code = str_replace("require_once(\$_SERVER[\"DOCUMENT_ROOT\"].\"/lang/dp_lang.php\");\n\$multilang_params = multilang_init();", '$multilang_params = array();', $code);
	$code = str_replace('require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");', '// leftover csrf stubbed', $code);
	$code = str_replace('require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");', '// leftover user stubbed', $code);
	$code = str_replace('require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/check_user_access.php");', '// leftover access stubbed', $code);
	$code = str_replace('exit(json_encode($answer));', 'echo json_encode($answer); return;', $code);
	file_put_contents($dest, $code);
}

function halyard_boot(): void
{
	if (!function_exists('translate_str_by_id')) {
		eval('function translate_str_by_id($id) { return "t".$id; }');
	}
}

function halyard_run(string $page, $name, bool $db, bool $makeDir): array
{
	$doc = sys_get_temp_dir() . '/ecomae_halyard_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc . '/cp/tmp_prices/keep', 0777, true);
	$GLOBALS['HALYARD_CFG'] = (object) array('backend_dir' => 'cp', 'tmp_dir_prices_upload' => '/tmp_prices');
	$GLOBALS['HALYARD_DB'] = $db ? new PDO('mysql:host=127.0.0.1;port=3306;dbname=mysql;charset=utf8mb4', 'ecomae', getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN')) : null;
	$_SERVER['DOCUMENT_ROOT'] = $doc;
	$_POST['tmp_folder_name'] = $name;
	if ($makeDir && is_string($name) && $name !== '') {
		$dir = $doc . '/cp/tmp_prices/' . $name;
		@mkdir($dir . '/nested', 0777, true);
		file_put_contents($dir . '/index.html', 'keep');
		file_put_contents($dir . '/price.csv', 'x');
		file_put_contents($dir . '/nested/x.txt', 'y');
	}
	ob_start();
	include $page;
	$raw = (string) ob_get_clean();
	$json = json_decode($raw, true);
	if (is_array($json) && isset($json['tmp_folder_name'])) {
		$json['tmp_folder_name'] = str_replace($doc, '{doc}', (string) $json['tmp_folder_name']);
	}
	$folderLeft = is_dir($doc . '/cp/tmp_prices/' . (string) $name) ? 1 : 0;
	$indexLeft = is_file($doc . '/cp/tmp_prices/' . (string) $name . '/index.html') ? 1 : 0;
	// cleanup
	if (is_dir($doc)) {
		$it = new RecursiveIteratorIterator(new RecursiveDirectoryIterator($doc, FilesystemIterator::SKIP_DOTS), RecursiveIteratorIterator::CHILD_FIRST);
		foreach ($it as $f) {
			$f->isDir() ? @rmdir($f->getPathname()) : @unlink($f->getPathname());
		}
		@rmdir($doc);
	}
	return array($json, $folderLeft, $indexLeft);
}

function halyard_run_name()
{
	return array(
		halyard_run($GLOBALS['HALYARD_PAGE'], 'bad.name', true, false),
		halyard_run($GLOBALS['HALYARD_PAGE'], 'ABC', true, false),
		halyard_run($GLOBALS['HALYARD_PAGE'], 'ok_name 1', true, false),
	);
}

function halyard_run_missing()
{
	return halyard_run($GLOBALS['HALYARD_PAGE'], 'gone_dir', true, false);
}

function halyard_run_delete()
{
	return halyard_run($GLOBALS['HALYARD_PAGE'], 'ok_dir', true, true);
}

function halyard_run_nodb()
{
	return halyard_run($GLOBALS['HALYARD_PAGE'], 'ok_dir', false, true);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_halyard_src_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	halyard_patch($root . '/cp/content/shop/prices_upload/for_pyprices/del_tmp_folder.php', $tmp . '/del.php');
	halyard_boot();
	$GLOBALS['HALYARD_PAGE'] = $tmp . '/del.php';
	$fn = 'halyard_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/del.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = 'ECOMAE_LOCAL_MARIADB_E2E_DSN=' . escapeshellarg((string) getenv('ECOMAE_LOCAL_MARIADB_E2E_DSN'))
		. ' ' . escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1halyard_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1halyard_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
