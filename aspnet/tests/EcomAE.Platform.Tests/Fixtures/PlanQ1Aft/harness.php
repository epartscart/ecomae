<?php
// PHP 8.3 goldens for plan Q1-aft (marketing strategy playbooks). Site-context stays stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function aft_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_site_context.php';", '// leftover site-context stubbed', $code);
	file_put_contents($dest, $code);
}

function aft_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_site_domain')) {
		eval('function epc_site_domain() { return (string) ($GLOBALS["AFT_DOMAIN"] ?? ""); }');
		eval('function epc_site_host() { return (string) ($GLOBALS["AFT_HOST"] ?? ""); }');
	}
}

function aft_include(): void
{
	include_once $GLOBALS['AFT_PAGE'];
}

function aft_summary(array $catalog): array
{
	$keys = array_keys($catalog);
	$titles = array();
	$tasks = array();
	$kpis = array();
	$guides = array();
	$links = array();
	foreach ($keys as $key) {
		$row = $catalog[$key];
		$titles[] = (string) ($row['title'] ?? '');
		$tasks[] = count((array) ($row['follow_tasks'] ?? array()));
		$kpis[] = count((array) ($row['kpis'] ?? array()));
		$guides[] = count((array) ($row['guidelines'] ?? array()));
		$links[] = count((array) ($row['links'] ?? array()));
	}
	$measureBody = (string) ($catalog['measurement']['guidelines'][1]['body'] ?? '');
	$measureLink = (string) ($catalog['measurement']['links'][0]['url'] ?? '');
	$seoLink = (string) ($catalog['seo']['links'][0]['url'] ?? '');
	$marketBody = (string) ($catalog['marketplaces']['guidelines'][1]['body'] ?? '');
	$intlLink = (string) ($catalog['international']['links'][1]['url'] ?? '');
	$qwLink = (string) ($catalog['quick_wins']['links'][1]['url'] ?? '');
	return array(
		count($keys),
		$keys,
		$titles,
		$tasks,
		$kpis,
		$guides,
		$links,
		strpos($measureBody, 'your domain') !== false ? 1 : 0,
		strpos($measureBody, '&#039;') !== false || strpos($measureBody, '&amp;') !== false || strpos($measureBody, '&quot;') !== false ? 1 : 0,
		$measureLink,
		$seoLink,
		strpos($marketBody, 'https://') !== false || strpos($marketBody, 'storefront') !== false ? 1 : 0,
		$intlLink,
		$qwLink,
		strlen($measureBody),
		(string) ($catalog['partnerships']['links'][1]['url'] ?? ''),
	);
}

function aft_run_empty()
{
	$GLOBALS['AFT_DOMAIN'] = '';
	$GLOBALS['AFT_HOST'] = '';
	unset($GLOBALS['DP_Config']);
	aft_include();
	return aft_summary(epc_marketing_strategies());
}

function aft_run_domain()
{
	$GLOBALS['AFT_DOMAIN'] = 'https://shop.acme.test';
	$GLOBALS['AFT_HOST'] = '';
	unset($GLOBALS['DP_Config']);
	aft_include();
	return aft_summary(epc_marketing_strategies());
}

function aft_run_hostenc()
{
	$GLOBALS['AFT_DOMAIN'] = 'https://a.com';
	$GLOBALS['AFT_HOST'] = 'a&b\'"<>';
	unset($GLOBALS['DP_Config']);
	aft_include();
	return aft_summary(epc_marketing_strategies());
}

function aft_run_config()
{
	$GLOBALS['AFT_DOMAIN'] = '';
	$GLOBALS['AFT_HOST'] = 'cfg.host';
	$cfg = new stdClass();
	$cfg->domain_path = 'https://cfg.test/';
	$GLOBALS['DP_Config'] = $cfg;
	aft_include();
	return aft_summary(epc_marketing_strategies());
}

if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', true);
}
$_SERVER['DOCUMENT_ROOT'] = $root;
$tmp = sys_get_temp_dir() . '/plan_q1aft_' . getmypid();
@mkdir($tmp);
aft_patch($root . '/content/shop/marketing/epc_marketing_strategies_data.php', $tmp . '/strat.php');
$GLOBALS['AFT_PAGE'] = $tmp . '/strat.php';
aft_boot();

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$fn = 'aft_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/strat.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1aft_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1aft_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
