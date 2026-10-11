<?php
// PHP 8.3 goldens for plan Q1-throat (CP order staff summary). Leftover notify/currency parents stay stubbed.
ini_set('display_errors', 'stderr');
date_default_timezone_set('UTC');
$root = rtrim($argv[1] ?? '/workspace', '/');
$spec = json_decode(file_get_contents(__DIR__ . '/cases.json'), true);

function throat_clip(string $html): array
{
	return array(strlen($html), substr($html, 0, 80), substr($html, -80));
}

function throat_render($order, $orderId, $customerId, $sale, $purchase, $profit, $margin): string
{
	$order = $order;
	$order_id = $orderId;
	$customer_id = $customerId;
	$order_sale_sum_without_vat = $sale;
	$order_purchase_sum_without_vat = $purchase;
	$order_profit_without_vat = $profit;
	$order_margin_percent_without_vat = $margin;
	ob_start();
	include $GLOBALS['THROAT_PAGE'];
	return (string) ob_get_clean();
}

function throat_run_empty()
{
	$html = throat_render(array(), 0, 1, 1, 1, 1, 1);
	$html2 = throat_render(array('id' => 1), 0, 1, 1, 1, 1, 1);
	return array(throat_clip($html), throat_clip($html2));
}

function throat_run_nocrm()
{
	$GLOBALS['THROAT_CRM'] = 0;
	$GLOBALS['THROAT_PROFILE'] = '<table class="epc-kv"><tr><td>Name</td><td>Acme</td></tr></table>';
	$html = throat_render(array('id' => 9), 9, 3, 100.0, 80.0, 20.0, 20.0);
	return array(throat_clip($html), strpos($html, 'Relationship manager') !== false ? 1 : 0, strpos($html, '100.00') !== false ? 1 : 0);
}

function throat_run_crm()
{
	$GLOBALS['THROAT_CRM'] = 44;
	$GLOBALS['THROAT_PROFILE'] = '<p>O\'Neil</p>';
	$html = throat_render(array('id' => 2), 2, 8, 10.5, 4.2, 6.3, 60.0);
	return array(throat_clip($html), strpos($html, 'Relationship manager user ID: <strong>44</strong>') !== false ? 1 : 0);
}

function throat_run_fmt()
{
	$GLOBALS['THROAT_CRM'] = 0;
	$GLOBALS['THROAT_PROFILE'] = '';
	$html = throat_render(array('id' => 1), 1, 1, 1234.5, 1000, 234.5, 19);
	return array(
		throat_clip($html),
		strpos($html, '1,234.50') !== false ? 1 : 0,
		strpos($html, '61.73') !== false ? 1 : 0,
		strpos($html, '19.00') !== false ? 1 : 0,
	);
}

function throat_patch(string $src, string $dest): void
{
	$code = file_get_contents($src);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_admin_notifications.php';", '// leftover notify stubbed', $code);
	$code = str_replace("require_once \$_SERVER['DOCUMENT_ROOT'] . '/content/shop/pricing/epc_currency.php';", '// leftover currency stubbed', $code);
	file_put_contents($dest, $code);
}

function throat_boot(): void
{
	if (!defined('_ASTEXE_')) {
		define('_ASTEXE_', true);
	}
	if (!function_exists('epc_crm_user_id_for_customer')) {
		eval('function epc_crm_user_id_for_customer($id) { return (int) ($GLOBALS["THROAT_CRM"] ?? 0); }');
	}
	if (!function_exists('epc_build_customer_profile_html')) {
		eval('function epc_build_customer_profile_html($id, $order) { return (string) ($GLOBALS["THROAT_PROFILE"] ?? ""); }');
	}
}

if (isset($argv[2])) {
	$case = $spec['cases'][(int) $argv[2]];
	$tmp = sys_get_temp_dir() . '/ecomae_throat_' . substr(md5(uniqid('', true)), 0, 8);
	@mkdir($tmp, 0777, true);
	throat_patch($root . '/cp/content/shop/order_process/epc_order_staff_summary.php', $tmp . '/page.php');
	$GLOBALS['THROAT_PAGE'] = $tmp . '/page.php';
	throat_boot();
	$fn = 'throat_run_' . $case['name'];
	$result = $fn();
	@unlink($tmp . '/page.php');
	@rmdir($tmp);
	echo json_encode(array('name' => $case['name'], 'output' => '', 'result' => $result), JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_PARTIAL_OUTPUT_ON_ERROR);
	exit(0);
}

$results = array();
foreach ($spec['cases'] as $i => $case) {
	$cmd = escapeshellarg(PHP_BINARY) . ' ' . escapeshellarg(__FILE__) . ' ' . escapeshellarg($root) . ' ' . $i;
	$out = shell_exec($cmd . ' 2>/tmp/plan_q1throat_err_' . $i . '.txt');
	$row = json_decode((string) $out, true);
	if (!is_array($row)) {
		fwrite(STDERR, "case {$case['name']} failed:\n" . $out . "\n" . @file_get_contents('/tmp/plan_q1throat_err_' . $i . '.txt') . "\n");
		exit(1);
	}
	$results[] = $row;
}
echo json_encode(array('php' => PHP_VERSION, 'results' => $results), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), "\n";
