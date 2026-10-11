<?php
// PHP 8.3 goldens for plan Q1-ford (BOC product brochure launcher). Leftover unique portal + Super-CP host stay injected.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function ford_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	file_put_contents($dest, $code);
}

function ford_stubs(bool $withSuperFn, bool $withFrameFns): void
{
	if ($withSuperFn && !function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['FORD_SUPER']);
		}
	}
	if ($withFrameFns && !function_exists('epc_cp_page_frame_open')) {
		function epc_cp_page_frame_open($opts = array()): void
		{
			$opts = is_array($opts) ? $opts : array();
			echo 'FRAME_OPEN:' . (string) ($opts['class'] ?? '');
		}
	}
	if ($withFrameFns && !function_exists('epc_cp_page_frame_close')) {
		function epc_cp_page_frame_close(): void
		{
			echo 'FRAME_CLOSE';
		}
	}
}

function ford_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	ford_stubs(!empty($GLOBALS['FORD_SUPER_FN']), !empty($GLOBALS['FORD_FRAME_FNS']));
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['FORD_DOCROOT'];
	if (array_key_exists('DP_Config', $GLOBALS)) {
		$GLOBALS['DP_Config'] = $GLOBALS['DP_Config'];
	}
	require $path;
}

function ford_render(mixed $config, mixed $super, bool $superFn, bool $frameFile, bool $frameFns): string
{
	$GLOBALS['FORD_SUPER'] = $super;
	$GLOBALS['FORD_SUPER_FN'] = $superFn;
	$GLOBALS['FORD_FRAME_FNS'] = $frameFns;
	if ($config === '__unset__') {
		unset($GLOBALS['DP_Config']);
	} else {
		$GLOBALS['DP_Config'] = $config;
	}
	$frameDir = $GLOBALS['FORD_DOCROOT'] . '/content/general_pages';
	@mkdir($frameDir, 0777, true);
	$framePath = $frameDir . '/epc_cp_page_frame.php';
	if ($frameFile) {
		file_put_contents($framePath, "<?php\n// leftover page frame injected\n");
	} else {
		@unlink($framePath);
	}
	$tmp = sys_get_temp_dir() . '/ecomae_ford_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['FORD_PAGE'], $tmp);
	ob_start();
	ford_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function ford_run_empty(): array
{
	$none = ford_render('__unset__', 0, false, false, false);
	$tenant = ford_render((object) array('backend_dir' => 'acme'), 0, true, false, false);
	return array($none, $tenant);
}

function ford_run_super(): array
{
	$cfg = (object) array('backend_dir' => 'cp');
	$plain = ford_render($cfg, 1, true, false, false);
	$framed = ford_render($cfg, 1, true, true, true);
	$closeOnly = ford_render($cfg, 1, true, false, true);
	return array($plain, $framed, $closeOnly);
}

function ford_run_escape(): array
{
	$emptyDir = ford_render((object) array('backend_dir' => ''), 1, true, false, false);
	$html = ford_render((object) array('backend_dir' => 'acme"><x'), 1, true, true, true);
	return array($html, $emptyDir);
}

function ford_run_tenant(): array
{
	$acme = ford_render((object) array('backend_dir' => 'acme'), 1, true, true, true);
	$beta = ford_render((object) array('backend_dir' => 'beta'), 1, true, true, true);
	$denied = ford_render((object) array('backend_dir' => 'acme'), 0, true, true, true);
	return array($acme, $beta, $denied);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_ford_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	ford_patch($root . '/cp/content/control/portal/epc_boc_product_brochure.php', $tmp . '/page.php');
	$GLOBALS['FORD_PAGE'] = $tmp . '/page.php';
	$GLOBALS['FORD_DOCROOT'] = $tmp;
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'ford_run_' . $case['name'];
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1ford_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1ford_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
