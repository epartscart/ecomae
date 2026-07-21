<?php
/**
 * Offline tests for unified Insights Suite.
 * php tests/erp_advanced/run_insights_suite_tests.php
 */
declare(strict_types=1);

$root = dirname(__DIR__, 2);
if (!defined('_ASTEXE_')) {
	define('_ASTEXE_', 1);
}
require_once $root . '/content/shop/finance/epc_insights_suite.php';

$failed = 0;
function check(string $label, bool $ok): void
{
	global $failed;
	echo ($ok ? '[OK] ' : '[FAIL] ') . $label . PHP_EOL;
	if (!$ok) {
		$failed++;
	}
}

$card = epc_insights_card('revenue', 'Revenue', 1000.0, 'money', 800.0, true, 'good', 'Growing', '/x', 'Open');
check('card has key', ($card['key'] ?? '') === 'revenue');
check('card delta positive', is_numeric($card['delta_pct'] ?? null) && (float) $card['delta_pct'] > 0);
check('format money', strpos(epc_insights_format_value(12.5, 'money', 'AED'), 'AED') !== false);
check('format pct', epc_insights_format_value(18.25, 'pct') === '18.3%');
check('format number', epc_insights_format_value(42, 'number') === '42');

$alerts = epc_insights_suite_alerts(
	array(
		epc_insights_card('margin', 'Margin', 8.0, 'pct', null, true, 'bad', 'Soft margin', '/pl', 'P&L'),
		epc_insights_card('ar_dso', 'AR', 5000.0, 'money', null, false, 'warn', 'Slow DSO', '/aging', 'Aging'),
	),
	array(
		epc_insights_card('open_orders', 'Open', 12.0, 'number', null, false, 'warn', 'Backlog', '/orders', 'OMS'),
	),
	array(
		epc_insights_card('price_lists', 'Prices', 0.0, 'number', null, true, 'warn', 'No lists', '/prices', 'Prices'),
	),
	epc_insights_suite_urls('/cp')
);
check('alerts non-empty', count($alerts) > 0);
check('alerts capped', count($alerts) <= 4);

$suite = array(
	'currency' => 'AED',
	'period' => array('label' => 'MTD', 'from_label' => '2026-07-01', 'to_label' => '2026-07-21'),
	'financial' => array($card),
	'business' => array(epc_insights_card('orders_week', 'Orders', 10, 'number', 8, true, 'good', 'Up', '/o', 'OMS')),
	'cp' => array(epc_insights_card('catalogue', 'Products', 100, 'number', null, true, 'good', 'Ready', '/c', 'Catalogue')),
	'alerts' => $alerts,
	'urls' => epc_insights_suite_urls('/cp'),
);
$html = epc_insights_suite_render($suite, 'cp');
check('render has financial heading', strpos($html, 'Financial insights') !== false);
check('render has business heading', strpos($html, 'Business insights') !== false);
check('render has CP heading', strpos($html, 'Control panel insights') !== false);
check('render has anchor id', strpos($html, 'id="epc-insights"') !== false);
check('urls include aging', strpos((string) epc_insights_suite_urls('/cp')['aging'], 'aging') !== false);
check('file exists suite', is_file($root . '/content/shop/finance/epc_insights_suite.php'));
check('file exists css', is_file($root . '/content/shop/finance/epc_insights_suite.css'));

echo $failed === 0 ? "ALL PASSED\n" : "FAILED {$failed}\n";
exit($failed === 0 ? 0 : 1);
