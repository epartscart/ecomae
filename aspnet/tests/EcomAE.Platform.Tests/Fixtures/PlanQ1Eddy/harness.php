<?php
// PHP 8.3 goldens for plan Q1-eddy (in-CP brochure page). Leftover unique Super-CP host check stays injected.
// Full-brochure render is already mentioned and stays stubbed so this wrapper's args stay the golden.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function eddy_patch(string $src, string $dest): void
{
	$code = str_replace("\r\n", "\n", file_get_contents($src));
	$code = str_replace(
		"require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_full_brochure.php';",
		'// leftover full-brochure injected',
		$code
	);
	$code = preg_replace(
		'/while \(ob_get_level\(\) > 0\) \{\s*@ob_end_clean\(\);\s*\}/',
		'// output buffers stay with the harness',
		$code
	);
	file_put_contents($dest, $code);
}

function eddy_stubs(): void
{
	if (!function_exists('epc_portal_is_super_cp_host')) {
		function epc_portal_is_super_cp_host(): bool
		{
			return !empty($GLOBALS['EDDY_SUPER']);
		}
	}
	if (!function_exists('epc_cp_full_brochure_render_and_exit')) {
		function epc_cp_full_brochure_render_and_exit($opts): void
		{
			$opts = is_array($opts) ? $opts : array();
			$print = !empty($opts['print']) ? '1' : '0';
			echo 'brand=' . (string) ($opts['brand'] ?? '') . "\n"
				. 'scope=' . (string) ($opts['scope'] ?? '') . "\n"
				. 'print=' . $print . "\n"
				. 'base_path=' . (string) ($opts['base_path'] ?? '') . "\n";
		}
	}
}

function eddy_include(string $path): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', 1);
	}
	eddy_stubs();
	$_SERVER['DOCUMENT_ROOT'] = $GLOBALS['EDDY_DOCROOT'];
	$_GET = $GLOBALS['EDDY_GET'] ?? array();
	if (array_key_exists('DP_Config', $GLOBALS)) {
		$DP_Config = $GLOBALS['DP_Config'];
	}
	require $path;
}

function eddy_render(array $get, mixed $config, mixed $super): string
{
	$GLOBALS['EDDY_GET'] = $get;
	$GLOBALS['EDDY_SUPER'] = $super;
	if ($config === '__unset__') {
		unset($GLOBALS['DP_Config']);
	} else {
		$GLOBALS['DP_Config'] = $config;
	}
	$tmp = sys_get_temp_dir() . '/ecomae_eddy_' . substr(md5(uniqid('', true)), 0, 12) . '.php';
	copy($GLOBALS['EDDY_PAGE'], $tmp);
	ob_start();
	eddy_include($tmp);
	$html = (string) ob_get_clean();
	@unlink($tmp);
	return $html;
}

function eddy_run_empty(): array
{
	$none = eddy_render(array(), '__unset__', 0);
	$zeroDir = eddy_render(array(), (object) array('backend_dir' => '0'), 0);
	$blankDir = eddy_render(array(), (object) array('backend_dir' => ''), false);
	return array($none, $zeroDir, $blankDir);
}

function eddy_run_super(): array
{
	$cfg = (object) array('backend_dir' => 'cp');
	$plain = eddy_render(array(), $cfg, 1);
	$client = eddy_render(array('scope' => 'client'), $cfg, 1);
	$emptyScope = eddy_render(array('scope' => ''), $cfg, 1);
	return array($plain, $client, $emptyScope);
}

function eddy_run_query(): array
{
	$cfg = (object) array('backend_dir' => '/cp/');
	$dirty = eddy_render(array('scope' => 'ALL-Client!', 'print' => '1'), $cfg, 0);
	$digits = eddy_render(array('scope' => '123', 'print' => '0'), $cfg, 1);
	$printOff = eddy_render(array('print' => '1.0'), $cfg, 0);
	$hostile = eddy_render(array('scope' => 'acme<script>'), $cfg, 0);
	return array($dirty, $digits, $printOff, $hostile);
}

function eddy_run_tenant(): array
{
	$acme = eddy_render(array(), (object) array('backend_dir' => 'acme'), 0);
	$beta = eddy_render(array('scope' => 'client', 'print' => '1'), (object) array('backend_dir' => 'beta'), 1);
	return array($acme, $beta);
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_eddy_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	eddy_patch($root . '/cp/content/control/epc_cp_brochure_page.php', $tmp . '/page.php');
	$GLOBALS['EDDY_PAGE'] = $tmp . '/page.php';
	$GLOBALS['EDDY_DOCROOT'] = $tmp;
	$_SERVER['DOCUMENT_ROOT'] = $tmp;
	$fn = 'eddy_run_' . $case['name'];
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
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1eddy_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1eddy_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
