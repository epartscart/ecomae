<?php
// PHP 8.3 goldens for plan Q1-bend (CP order WhatsApp share). Leftover WA parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function bend_clip(string $html): array
{
	return array(strlen($html), substr($html, 0, 80), substr($html, -80));
}

function bend_render($order, $orderId, $profile): string
{
	$order = $order;
	$order_id = $orderId;
	$customer_profile = $profile;
	$db_link = $GLOBALS['BEND_DB'];
	$DP_Config = (object) array('from_name' => 'Shop');
	$GLOBALS['DP_Config'] = $DP_Config;
	ob_start();
	include $GLOBALS['BEND_PAGE'];
	return (string) ob_get_clean();
}

function bend_run_empty()
{
	$GLOBALS['BEND_DB'] = 1;
	$html = bend_render(array(), 0, array());
	$html2 = bend_render(array('id' => 1), 0, array());
	return array(bend_clip($html), bend_clip($html2));
}

function bend_run_nophone()
{
	$GLOBALS['BEND_DB'] = 1;
	$GLOBALS['BEND_ITEMS'] = array(array('name' => 'Pad'));
	$GLOBALS['BEND_LPO'] = array();
	$html = bend_render(array('id' => 9), 9, array());
	return array(
		bend_clip($html),
		strpos($html, 'No customer phone') !== false ? 1 : 0,
		strpos($html, 'Share with sales') !== false ? 1 : 0,
		strpos($html, 'Supplier LPO') !== false ? 1 : 0,
	);
}

function bend_run_phone()
{
	$GLOBALS['BEND_DB'] = 1;
	$GLOBALS['BEND_ITEMS'] = array(array('name' => "O'Brien"));
	$GLOBALS['BEND_LPO'] = array();
	$html = bend_render(array('id' => 3, 'phone_not_auth' => '050'), 3, array('phone' => '+971 50 111 2222'));
	return array(
		bend_clip($html),
		strpos($html, 'Message customer') !== false ? 1 : 0,
		strpos($html, '971501112222') !== false ? 1 : 0,
		strpos($html, 'No customer phone') !== false ? 1 : 0,
	);
}

function bend_run_lpo()
{
	$GLOBALS['BEND_DB'] = 1;
	$GLOBALS['BEND_ITEMS'] = array(array('name' => 'Filter'));
	$GLOBALS['BEND_LPO'] = array(
		array('storage_name' => 'Acme WH', 'target_label' => 'supplier', 'wa_href' => 'https://wa.me/97150?t=LPO', 'lpo_message' => 'LPO text'),
		array('storage_name' => "O'Neil", 'target_label' => 'sales (forward LPO)', 'wa_href' => 'https://wa.me/97156?t=FWD', 'lpo_message' => 'fwd'),
	);
	$html = bend_render(array('id' => 4, 'phone_not_auth' => '+971-56-000'), 4, array());
	return array(
		bend_clip($html),
		strpos($html, 'LPO: Acme WH → supplier') !== false ? 1 : 0,
		strpos($html, "LPO: O'Neil") !== false ? 1 : 0,
		strpos($html, '97156000') !== false ? 1 : 0,
	);
}

function bend_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_whatsapp_share.php';", '// leftover wa stubbed', $code);
	file_put_contents($dest, $code);
}

function bend_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_wa_order_items')) {
		eval('function epc_wa_order_items($db, $id) { return (array) ($GLOBALS["BEND_ITEMS"] ?? array()); }');
		eval('function epc_wa_sales_digits($cfg) { return "971567607011"; }');
		eval('function epc_wa_digits($phone) { return preg_replace("/[^0-9]/", "", (string) $phone); }');
		eval('function epc_wa_order_customer_message($cfg, $id, $order, $items) { return "CUST"; }');
		eval('function epc_wa_order_sales_message($cfg, $id, $order, $items, $who) { return "SALES"; }');
		eval('function epc_wa_share_url($digits, $text) { $digits = preg_replace("/[^0-9]/", "", (string) $digits); return $digits === "" ? "" : "https://wa.me/".$digits."?t=".$text; }');
		eval('function epc_wa_order_lpo_groups($db, $cfg, $id, $items) { return (array) ($GLOBALS["BEND_LPO"] ?? array()); }');
		eval('function epc_wa_styles() { return "<style>wa</style>"; }');
		eval('function epc_wa_h($v) { return htmlspecialchars((string) $v, ENT_QUOTES); }');
		eval('function epc_wa_sales_display($cfg) { return "Sales line"; }');
		eval('function epc_wa_button($href, $label, $cls, $title) { return $href === "" ? "" : "<a class=\"".$cls."\" href=\"".$href."\">".$label."</a>"; }');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_bend_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	bend_patch($root . '/cp/content/shop/order_process/epc_order_whatsapp_share.php', $tmp . '/share.php');
	$GLOBALS['BEND_PAGE'] = $tmp . '/share.php';
	bend_boot();
	$fn = 'bend_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/share.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1bend_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1bend_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
