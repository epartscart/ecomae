<?php
// PHP 8.3 goldens for plan Q1-loch (storefront search-string module). Leftover unique user helper stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function loch_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = preg_replace(
		'/require_once\(\s*\$_SERVER\[\'DOCUMENT_ROOT\'\]\s*\.\s*["\']\/content\/users\/dp_user\.php["\']\s*\)\s*;/',
		'// leftover user injected',
		$code
	);
	file_put_contents($dest, $code);
}

function loch_stubs(): void
{
	if (!function_exists('translate_str_by_id')) {
		function translate_str_by_id($id)
		{
			$map = $GLOBALS['LOCH_IDS'] ?? array();
			$s = (string) $id;
			if (isset($map[$s])) {
				return $map[$s];
			}
			$i = (int) $s;
			if (isset($map[$i])) {
				return $map[$i];
			}
			return $s;
		}
	}
	if (!class_exists('DP_User', false)) {
		class DP_User
		{
			public static function getUserSession()
			{
				return $GLOBALS['LOCH_SESSION'] ?? array('csrf_guard_key' => 'tok-1');
			}
		}
	}
}

function loch_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	loch_stubs();
	$_GET = $GLOBALS['LOCH_GET'] ?? array();
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['LOCH_DOCROOT'];
	$multilang_params = $GLOBALS['LOCH_LANG'] ?? array('lang_href' => '/en');
	require $path;
}

function loch_render(array $get, array $lang, array $session, array $ids): string
{
	$GLOBALS['LOCH_GET'] = $get;
	$GLOBALS['LOCH_LANG'] = $lang;
	$GLOBALS['LOCH_SESSION'] = $session;
	$GLOBALS['LOCH_IDS'] = $ids;
	$tmp = sys_get_temp_dir() . '/ecomae_loch_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['LOCH_PAGE'], $tmp);
	ob_start();
	loch_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function loch_ids(): array
{
	return array(
		4772 => 'By article',
		4773 => 'By name',
		4774 => 'Search',
		2379 => 'Find',
	);
}

function loch_run_empty(): array
{
	$lang = array('lang_href' => '/en');
	$session = array('csrf_guard_key' => 'tok-1');
	$ids = loch_ids();
	$none = loch_render(array(), $lang, $session, $ids);
	$blank = loch_render(array('article' => ''), $lang, $session, $ids);
	$zero = loch_render(array('article' => '0'), $lang, $session, $ids);
	return array($none, $blank, $zero);
}

function loch_run_article(): array
{
	$lang = array('lang_href' => '/en');
	$session = array('csrf_guard_key' => 'tok-1');
	$ids = loch_ids();
	$plain = loch_render(array('article' => 'C110J'), $lang, $session, $ids);
	$special = loch_render(array('article' => "O'Reilly <x>"), $lang, $session, $ids);
	$spaces = loch_render(array('article' => '  pad  '), $lang, $session, $ids);
	return array($plain, $special, $spaces);
}

function loch_run_labels(): array
{
	$lang = array('lang_href' => '/ar');
	$session = array('csrf_guard_key' => 'tok-ar');
	$ids = array(
		4772 => 'حسب القطعة',
		4773 => 'حسب الاسم',
		4774 => 'بحث',
		2379 => 'إيجاد',
	);
	$ar = loch_render(array('article' => 'فلتر'), $lang, $session, $ids);
	$missing = loch_render(array('article' => 'X'), array('lang_href' => ''), array('csrf_guard_key' => ''), array());
	return array($ar, $missing);
}

function loch_run_tenant(): array
{
	$ids = loch_ids();
	$acme = loch_render(
		array('article' => 'AcmeCity'),
		array('lang_href' => '/en'),
		array('csrf_guard_key' => 'tok-acme'),
		$ids
	);
	$beta = loch_render(
		array('article' => 'BetaTown'),
		array('lang_href' => '/ar'),
		array('csrf_guard_key' => 'tok-beta'),
		$ids
	);
	return array($acme, $beta);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_loch_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	loch_patch($root . '/modules/shop/search_string/search_string.php', $tmp . '/page.php');
	$GLOBALS['LOCH_PAGE'] = $tmp . '/page.php';
	$GLOBALS['LOCH_DOCROOT'] = $tmp;
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'loch_run_' . $case['name'];
	try {
		$result = $fn();
	} catch (Throwable $e) {
		fwrite(STDERR, $e->getMessage() . "\n" . $e->getTraceAsString() . "\n");
		exit(1);
	}
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1loch_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1loch_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
