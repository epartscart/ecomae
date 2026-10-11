<?php
// PHP 8.3 goldens for plan Q1-sprit (marketing brochure). Leftover live-deck parent stays stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function sprit_reset(): void
{
	$GLOBALS['SPRIT_LIVE'] = false;
	$GLOBALS['SPRIT_IMG'] = array();
	$GLOBALS['SPRIT_META'] = array();
	$GLOBALS['SPRIT_TOTAL'] = 0;
}

function sprit_run_profile()
{
	sprit_reset();
	$codes = array('epartscart', 'auto_parts', 'AUTO_PARTS', 'Eparts-Cart!', 'Acme-1!', 'ecomae', '', 'jewellery', 'Fashion!');
	$picked = array();
	foreach ($codes as $code) {
		$p = epc_brochure_profile($code);
		$picked[$code] = array(
			'id' => $p['id'] ?? '',
			'name' => $p['name'] ?? '',
			'domain' => $p['domain'] ?? '',
			'accent' => $p['accent'] ?? '',
			'cp_brochure' => $p['cp_brochure'] ?? '',
			'cta' => $p['cta_primary']['label'] ?? '',
		);
	}
	return array($picked, epc_brochure_h("O'Brien & Co"), epc_brochure_h('<x>'), epc_brochure_h(''));
}

function sprit_run_lists()
{
	sprit_reset();
	$out = array();
	foreach (array('epartscart', 'ecomae', 'AUTO_PARTS', 'nope') as $code) {
		$id = epc_brochure_profile($code)['id'];
		$secs = epc_brochure_sections($id);
		$titles = array();
		foreach ($secs as $s) {
			$titles[] = array($s['title'], count($s['points']));
		}
		$css = epc_brochure_css(epc_brochure_profile($code));
		$out[$code] = array(
			'id' => $id,
			'sec' => $titles,
			'stats' => epc_brochure_stats($id),
			'journey' => epc_brochure_journey($id),
			'css_len' => strlen($css),
			'css_head' => substr($css, 0, 80),
			'css_tail' => substr($css, -80),
		);
	}
	return $out;
}

function sprit_run_html()
{
	sprit_reset();
	$plain = epc_brochure_render_html('ecomae');
	$parts = epc_brochure_render_html('epartscart');
	$print = epc_brochure_render_html('ecomae', array('print' => true));
	$print0 = epc_brochure_render_html('ecomae', array('print' => '0'));
	$quote = epc_brochure_render_html('Eparts-Cart!');
	return array(
		strlen($plain),
		substr($plain, 0, 120),
		substr($plain, -80),
		(strpos($plain, 'window.print') !== false) ? 1 : 0,
		(strpos($print, 'window.print') !== false) ? 1 : 0,
		(strpos($print0, 'setTimeout') !== false) ? 1 : 0,
		(strpos($parts, 'Inside the Control Panel') !== false) ? 1 : 0,
		(strpos($parts, 'Control Panel →') !== false) ? 1 : 0,
		(strpos($plain, 'Control Panel →') !== false) ? 1 : 0,
		(strpos($plain, 'Every CP function') !== false) ? 1 : 0,
		(strpos($quote, 'eParts Cart') !== false) ? 1 : 0,
		(strpos($plain, 'og_cover.png') !== false) ? 1 : 0,
	);
}

function sprit_run_live()
{
	sprit_reset();
	$GLOBALS['SPRIT_LIVE'] = true;
	$GLOBALS['SPRIT_IMG']['How work flows'] = '/live/journey.jpg';
	$GLOBALS['SPRIT_IMG']['One Blockchain BOS for the enterprise'] = '/live/sec.jpg';
	$GLOBALS['SPRIT_IMG']['Multi-tenant Super CP for operators'] = '/live/pt.jpg';
	$GLOBALS['SPRIT_META']['Multi-tenant Super CP for operators'] = array('label' => "O'area");
	$GLOBALS['SPRIT_TOTAL'] = 42;
	$html = epc_brochure_render_html('ecomae');
	$parts = epc_brochure_render_html('epartscart');
	return array(
		(strpos($html, '/live/journey.jpg') !== false) ? 1 : 0,
		(strpos($html, '/live/sec.jpg') !== false) ? 1 : 0,
		(strpos($html, '/live/pt.jpg') !== false) ? 1 : 0,
		(strpos($html, "O&#039;area") !== false) ? 1 : 0,
		(strpos($html, '42 functions') !== false) ? 1 : 0,
		(strpos($parts, '42 functions') !== false) ? 1 : 0,
		(strpos($html, 'og_cover.png') !== false) ? 1 : 0,
	);
}

function sprit_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = preg_replace(
		'/\$liveFile = __DIR__ \. \'\/epc_cp_brochure_live\.php\';\s*if \(is_file\(\$liveFile\)\) \{\s*require_once \$liveFile;\s*\}/s',
		'// leftover live-deck parent stubbed',
		$code,
		1
	);
	file_put_contents($dest, $code);
}

function sprit_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_cp_brochure_item_image')) {
		eval('function epc_cp_brochure_item_image($item, $group = "") {
			if (empty($GLOBALS["SPRIT_LIVE"])) { return "/content/general_pages/marketing_screens/og_cover.png"; }
			$name = is_array($item) ? (string) ($item["name"] ?? "") : "";
			if (isset($GLOBALS["SPRIT_IMG"][$name])) { return (string) $GLOBALS["SPRIT_IMG"][$name]; }
			if (isset($GLOBALS["SPRIT_IMG"][$group])) { return (string) $GLOBALS["SPRIT_IMG"][$group]; }
			return "/live/default.jpg";
		}');
	}
	if (!function_exists('epc_cp_brochure_item_photo_meta')) {
		eval('function epc_cp_brochure_item_photo_meta($item, $group = "") {
			if (empty($GLOBALS["SPRIT_LIVE"])) { return array("label" => ""); }
			$name = is_array($item) ? (string) ($item["name"] ?? "") : "";
			return $GLOBALS["SPRIT_META"][$name] ?? array("label" => "");
		}');
	}
	if (!function_exists('epc_cp_brochure_build_live_inventory')) {
		eval('function epc_cp_brochure_build_live_inventory() {
			if (empty($GLOBALS["SPRIT_LIVE"])) { return array("meta" => array("total" => 0)); }
			return array("meta" => array("total" => (int) ($GLOBALS["SPRIT_TOTAL"] ?? 0)));
		}');
	}
	sprit_reset();
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$doc = sys_get_temp_dir() . '/ecomae_sprit_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($doc, 0777, true);
	sprit_patch($root . '/content/general_pages/epc_marketing_brochure.php', $doc . '/brochure.php');
	sprit_boot();
	require $doc . '/brochure.php';
	$fn = 'sprit_run_' . $case['name'];
	$result = $fn();
	@unlink($doc . '/brochure.php');
	@rmdir($doc);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1sprit_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1sprit_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
