<?php
// PHP 8.3 goldens for plan Q1-tarn (storefront login module). Leftover unique user helper stays injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function tarn_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = preg_replace(
		'/require_once\(\s*[\'"]content\/users\/dp_user\.php[\'"]\s*\)\s*;/',
		'// leftover user injected',
		$code
	);
	$code = preg_replace(
		'/require_once\(\s*\$_SERVER\[[\'"]DOCUMENT_ROOT[\'"]\]\s*\.\s*[\'"]\/content\/users\/dp_user\.php[\'"]\s*\)\s*;/',
		'// leftover user injected',
		$code
	);
	file_put_contents($dest, $code);
}

function tarn_stubs(): void
{
	if (!function_exists('translate_str_by_id')) {
		function translate_str_by_id($id)
		{
			$map = $GLOBALS['TARN_IDS'] ?? array();
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
				return $GLOBALS['TARN_SESSION'] ?? array('csrf_guard_key' => 'tok-1');
			}
			public static function getUserId()
			{
				return $GLOBALS['TARN_USER'] ?? 0;
			}
			public static function getUserProfile()
			{
				return $GLOBALS['TARN_PROFILE'] ?? null;
			}
		}
	}
}

function tarn_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	tarn_stubs();
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['TARN_DOCROOT'];
	$multilang_params = $GLOBALS['TARN_LANG'] ?? array('lang_href' => '/en');
	require $path;
}

function tarn_render(mixed $userId, array $lang, array $session, array $ids, mixed $profile): string
{
	$GLOBALS['TARN_USER'] = $userId;
	$GLOBALS['TARN_LANG'] = $lang;
	$GLOBALS['TARN_SESSION'] = $session;
	$GLOBALS['TARN_IDS'] = $ids;
	$GLOBALS['TARN_PROFILE'] = $profile;
	$tmp = sys_get_temp_dir() . '/ecomae_tarn_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['TARN_PAGE'], $tmp);
	ob_start();
	tarn_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function tarn_ids(): array
{
	return array(
		4758 => 'Sign in',
		4759 => 'Login',
		1311 => 'Password',
		4666 => 'Remember me',
		4008 => 'Enter',
		3987 => 'Register',
		4667 => 'Forgot password',
		4760 => 'Welcome',
		4668 => 'Profile',
		4761 => 'Edit',
		3996 => 'Logout',
		4762 => 'Customer',
	);
}

function tarn_run_guest(): array
{
	$ids = tarn_ids();
	$guest = tarn_render(0, array('lang_href' => '/en'), array('csrf_guard_key' => 'tok-1'), $ids, null);
	$zero = tarn_render('0', array('lang_href' => '/en'), array('csrf_guard_key' => 'tok-1'), $ids, array('name' => 'Ignored'));
	return array($guest, $zero);
}

function tarn_run_signed(): array
{
	$ids = tarn_ids();
	$session = array('csrf_guard_key' => 'tok-1');
	$lang = array('lang_href' => '/en');
	$both = tarn_render(7, $lang, $session, $ids, array('name' => 'Ali', 'surname' => 'Khan'));
	$nameOnly = tarn_render(7, $lang, $session, $ids, array('name' => 'Ali'));
	$empty = tarn_render(7, $lang, $session, $ids, array());
	$nullName = tarn_render(7, $lang, $session, $ids, array('name' => null, 'surname' => 'Khan'));
	$space = tarn_render(7, $lang, $session, $ids, array('name' => 'Ali', 'surname' => ''));
	return array($both, $nameOnly, $empty, $nullName, $space);
}

function tarn_run_labels(): array
{
	$ar = array(
		4758 => 'دخول',
		4759 => 'حساب',
		1311 => 'كلمة',
		4666 => 'تذكرني',
		4008 => 'ادخل',
		3987 => 'تسجيل',
		4667 => 'نسيت',
		4760 => 'أهلا',
		4668 => 'الملف',
		4761 => 'تعديل',
		3996 => 'خروج',
		4762 => 'عميل',
	);
	$guest = tarn_render(0, array('lang_href' => '/ar'), array('csrf_guard_key' => 'tok-ar'), $ar, null);
	$signed = tarn_render(4, array('lang_href' => '/ar'), array('csrf_guard_key' => 'tok-ar'), $ar, array('name' => 'نورا'));
	$missing = tarn_render(0, array('lang_href' => ''), array('csrf_guard_key' => ''), array(), null);
	return array($guest, $signed, $missing);
}

function tarn_run_tenant(): array
{
	$ids = tarn_ids();
	$acme = tarn_render(7, array('lang_href' => '/en'), array('csrf_guard_key' => 'tok-acme'), $ids, array('name' => 'AcmeUser'));
	$beta = tarn_render(8, array('lang_href' => '/ar'), array('csrf_guard_key' => 'tok-beta'), $ids, array('name' => 'BetaUser'));
	return array($acme, $beta);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_tarn_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	tarn_patch($root . '/modules/login/module.php', $tmp . '/page.php');
	$GLOBALS['TARN_PAGE'] = $tmp . '/page.php';
	$GLOBALS['TARN_DOCROOT'] = $tmp;
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	tarn_stubs();
	$fn = 'tarn_run_' . $case['name'];
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1tarn_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1tarn_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
